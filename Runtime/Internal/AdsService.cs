using System;
using System.Collections.Generic;
using System.Threading;
using GDF.Ads.Data;
using GDF.Ads.Messages;
using Cysharp.Threading.Tasks;
using LocalDataStorage;
using MessagePipe;
using UnityEngine;

namespace GDF.Ads.Internal
{
    /// <summary>
    /// Default <see cref="IAdsService"/> implementation. Composes the vendor adapters, enforces the
    /// global remove-ads flag and the per-placement daily caps, and persists state through
    /// <see cref="ILocalDataStorage"/>. Vendor adapters stay thin: the facade handles policy and
    /// SDK calls only happen through it. Outcomes are published through MessagePipe.
    /// </summary>
    public sealed class AdsService : IAdsService
    {
        private readonly IAdsInitializer _initializer;
        private readonly IAdsBanner _banner;
        private readonly IAdsInterstitial _interstitial;
        private readonly IAdsRewarded _rewarded;
        private readonly ILocalDataStorage _storage;
        private readonly IPublisher<RewardedAdFinishedMessage> _rewardedFinishedPublisher;
        private readonly IPublisher<InterstitialAdFinishedMessage> _interstitialFinishedPublisher;
        private readonly IPublisher<AdsDisabledChangedMessage> _adsDisabledChangedPublisher;

        private AdsStateData _state;
        private Dictionary<string, int> _caps = new();
        private Dictionary<string, int> _counters = new();

        /// <summary>Creates the facade. Vendor adapters are optional; a missing one turns its calls into safe no-ops.</summary>
        /// <param name="initializer">Vendor SDK initializer, or <see langword="null"/>.</param>
        /// <param name="banner">Banner adapter, or <see langword="null"/> when the game has no banners.</param>
        /// <param name="interstitial">Interstitial adapter, or <see langword="null"/>.</param>
        /// <param name="rewarded">Rewarded-video adapter, or <see langword="null"/>.</param>
        /// <param name="storage">Storage that persists <see cref="AdsStateData"/>.</param>
        /// <param name="rewardedFinishedPublisher">Channel for <see cref="RewardedAdFinishedMessage"/>.</param>
        /// <param name="interstitialFinishedPublisher">Channel for <see cref="InterstitialAdFinishedMessage"/>.</param>
        /// <param name="adsDisabledChangedPublisher">Channel for <see cref="AdsDisabledChangedMessage"/>.</param>
        public AdsService(
            IAdsInitializer initializer,
            IAdsBanner banner,
            IAdsInterstitial interstitial,
            IAdsRewarded rewarded,
            ILocalDataStorage storage,
            IPublisher<RewardedAdFinishedMessage> rewardedFinishedPublisher,
            IPublisher<InterstitialAdFinishedMessage> interstitialFinishedPublisher,
            IPublisher<AdsDisabledChangedMessage> adsDisabledChangedPublisher)
        {
            _initializer = initializer;    // may be null in tests
            _banner = banner;              // optional (not every game uses banners)
            _interstitial = interstitial;  // optional
            _rewarded = rewarded;          // optional
            _storage = storage ?? throw new ArgumentNullException(nameof(storage));
            _rewardedFinishedPublisher = rewardedFinishedPublisher ?? throw new ArgumentNullException(nameof(rewardedFinishedPublisher));
            _interstitialFinishedPublisher = interstitialFinishedPublisher ?? throw new ArgumentNullException(nameof(interstitialFinishedPublisher));
            _adsDisabledChangedPublisher = adsDisabledChangedPublisher ?? throw new ArgumentNullException(nameof(adsDisabledChangedPublisher));
            LoadState();
        }

        /// <inheritdoc />
        public bool AdsDisabled => _state.AdsDisabled;

        /// <inheritdoc />
        public async UniTask<bool> InitializeAsync(CancellationToken cancellationToken = default)
        {
            if (_initializer == null)
                return false;

            bool ok = await _initializer.InitializeAsync(cancellationToken);
            return ok;
        }

        /// <inheritdoc />
        public void ShowBanner()
        {
            if (_state.AdsDisabled || _banner == null)
                return;

            _banner.Show();
        }

        /// <inheritdoc />
        public void HideBanner()
        {
            _banner?.Hide();
        }

        /// <inheritdoc />
        public async UniTask<bool> ShowInterstitialAsync(string placement, CancellationToken cancellationToken = default)
        {
            bool shown = await ShowInterstitialCoreAsync(cancellationToken);
            _interstitialFinishedPublisher.Publish(new InterstitialAdFinishedMessage(placement, shown));
            return shown;
        }

        /// <inheritdoc />
        public async UniTask<EAdsRewardedResult> ShowRewardedAsync(string placement, CancellationToken cancellationToken = default)
        {
            EAdsRewardedResult result = await ShowRewardedCoreAsync(placement, cancellationToken);
            _rewardedFinishedPublisher.Publish(new RewardedAdFinishedMessage(placement, result));
            return result;
        }

        /// <inheritdoc />
        public int GetRewardedWatchedToday(string placement)
        {
            ResetCountersIfNewDay();
            return _counters.TryGetValue(placement ?? string.Empty, out int c) ? c : 0;
        }

        /// <inheritdoc />
        public int GetRewardedDailyCap(string placement)
        {
            return _caps.TryGetValue(placement ?? string.Empty, out int c) ? c : int.MaxValue;
        }

        /// <inheritdoc />
        public void SetRewardedDailyCap(string placement, int cap)
        {
            if (string.IsNullOrEmpty(placement))
                return;

            _caps[placement] = Math.Max(0, cap);
            PersistState();
        }

        /// <inheritdoc />
        public void SetAdsDisabled(bool disabled)
        {
            if (_state.AdsDisabled == disabled)
                return;

            _state.AdsDisabled = disabled;
            PersistState();

            if (disabled)
                HideBanner();

            _adsDisabledChangedPublisher.Publish(new AdsDisabledChangedMessage(disabled));
        }

        private async UniTask<bool> ShowInterstitialCoreAsync(CancellationToken cancellationToken)
        {
            if (_state.AdsDisabled || _interstitial == null)
                return false;

            if (!_interstitial.IsReady)
                await _interstitial.Load(cancellationToken);

            return await _interstitial.Show(cancellationToken);
        }

        private async UniTask<EAdsRewardedResult> ShowRewardedCoreAsync(string placement, CancellationToken cancellationToken)
        {
            if (string.IsNullOrEmpty(placement))
            {
                Debug.LogWarning("[Ads] ShowRewardedAsync called with an empty placement.");
                return EAdsRewardedResult.Failed;
            }

            if (_state.AdsDisabled)
                return EAdsRewardedResult.Disabled;

            if (_rewarded == null)
                return EAdsRewardedResult.Failed;

            ResetCountersIfNewDay();

            int cap = _caps.TryGetValue(placement, out int c) ? c : int.MaxValue;
            int watched = _counters.TryGetValue(placement, out int w) ? w : 0;
            if (watched >= cap)
                return EAdsRewardedResult.CapReached;

            if (!_rewarded.IsReady)
                await _rewarded.Load(cancellationToken);

            EAdsRewardedResult result = await _rewarded.Show(cancellationToken);
            if (result == EAdsRewardedResult.Rewarded)
            {
                _counters[placement] = watched + 1;
                PersistState();
            }

            return result;
        }

        private void LoadState()
        {
            if (_storage.TryLoad(out AdsStateData loaded))
            {
                _state = loaded;
            }
            else
            {
                _state = new AdsStateData { CountersUtcDay = CurrentUtcDay() };
                _storage.Save(_state);
            }

            _caps.Clear();
            foreach (AdsStateData.CapEntry entry in _state.Caps)
                if (!string.IsNullOrEmpty(entry.Placement))
                    _caps[entry.Placement] = entry.Cap;

            _counters.Clear();
            foreach (AdsStateData.CounterEntry entry in _state.Counters)
                if (!string.IsNullOrEmpty(entry.Placement))
                    _counters[entry.Placement] = entry.Count;

            ResetCountersIfNewDay();
        }

        private void ResetCountersIfNewDay()
        {
            int today = CurrentUtcDay();
            if (_state.CountersUtcDay == today)
                return;

            _state.CountersUtcDay = today;
            _counters.Clear();
            PersistState();
        }

        private void PersistState()
        {
            _state.Caps = new List<AdsStateData.CapEntry>();
            foreach (KeyValuePair<string, int> pair in _caps)
                _state.Caps.Add(new AdsStateData.CapEntry { Placement = pair.Key, Cap = pair.Value });

            _state.Counters = new List<AdsStateData.CounterEntry>();
            foreach (KeyValuePair<string, int> pair in _counters)
                _state.Counters.Add(new AdsStateData.CounterEntry { Placement = pair.Key, Count = pair.Value });

            _storage.Save(_state);
        }

        private static int CurrentUtcDay()
        {
            DateTime now = DateTime.UtcNow;
            return now.Year * 10000 + now.Month * 100 + now.Day;
        }
    }
}
