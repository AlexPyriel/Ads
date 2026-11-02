using System;
using System.Collections.Generic;
using System.Threading;
using GDF.Ads.Data;
using Cysharp.Threading.Tasks;
using LocalDataStorage;
using UnityEngine;

namespace GDF.Ads.Internal
{
    /// <summary>
    /// Default <see cref="IAdsService"/> implementation. Composes the vendor adapters, enforces the
    /// global remove-ads flag and the per-placement daily caps, and persists state through
    /// <see cref="ILocalDataStorage"/>. Vendor adapters stay thin: the facade handles policy and
    /// SDK calls only happen through it.
    /// </summary>
    internal sealed class AdsService : IAdsService
    {
        private readonly IAdsInitializer _initializer;
        private readonly IAdsBanner _banner;
        private readonly IAdsInterstitial _interstitial;
        private readonly IAdsRewarded _rewarded;
        private readonly ILocalDataStorage _storage;

        private AdsStateData _state;
        private Dictionary<string, int> _caps = new();
        private Dictionary<string, int> _counters = new();

        public event Action<bool> AdsDisabledChanged;
        public event Action<string> RewardedWatched;

        public AdsService(
            IAdsInitializer initializer,
            IAdsBanner banner,
            IAdsInterstitial interstitial,
            IAdsRewarded rewarded,
            ILocalDataStorage storage)
        {
            _initializer = initializer;    // may be null in tests
            _banner = banner;              // optional (not every game uses banners)
            _interstitial = interstitial;  // optional
            _rewarded = rewarded;          // optional
            _storage = storage ?? throw new ArgumentNullException(nameof(storage));
            LoadState();
        }

        public bool AdsDisabled => _state.AdsDisabled;

        public async UniTask<bool> InitializeAsync(CancellationToken cancellationToken = default)
        {
            if (_initializer == null)
                return false;

            bool ok = await _initializer.InitializeAsync(cancellationToken);
            return ok;
        }

        public void ShowBanner()
        {
            if (_state.AdsDisabled || _banner == null)
                return;

            _banner.Show();
        }

        public void HideBanner()
        {
            _banner?.Hide();
        }

        public async UniTask<bool> ShowInterstitialAsync(string placement, CancellationToken cancellationToken = default)
        {
            if (_state.AdsDisabled || _interstitial == null)
                return false;

            if (!_interstitial.IsReady)
                await _interstitial.Load(cancellationToken);

            return await _interstitial.Show(cancellationToken);
        }

        public async UniTask<EAdsRewardedResult> ShowRewardedAsync(string placement, CancellationToken cancellationToken = default)
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
                RewardedWatched?.Invoke(placement);
            }

            return result;
        }

        public int GetRewardedWatchedToday(string placement)
        {
            ResetCountersIfNewDay();
            return _counters.TryGetValue(placement ?? string.Empty, out int c) ? c : 0;
        }

        public int GetRewardedDailyCap(string placement)
        {
            return _caps.TryGetValue(placement ?? string.Empty, out int c) ? c : int.MaxValue;
        }

        public void SetRewardedDailyCap(string placement, int cap)
        {
            if (string.IsNullOrEmpty(placement))
                return;

            _caps[placement] = Math.Max(0, cap);
            PersistState();
        }

        public void SetAdsDisabled(bool disabled)
        {
            if (_state.AdsDisabled == disabled)
                return;

            _state.AdsDisabled = disabled;
            PersistState();
            AdsDisabledChanged?.Invoke(disabled);

            if (disabled)
                HideBanner();
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
