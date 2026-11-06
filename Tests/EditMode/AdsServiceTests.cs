using System;
using System.Collections;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using GDF.Ads.Messages;
using LocalDataStorage;
using MessagePipe;
using NUnit.Framework;
using UnityEngine.TestTools;

namespace GDF.Ads.Tests.EditMode
{
    /// <summary>
    /// Pure-logic coverage of <see cref="Internal.AdsService"/>: disabled flag gating,
    /// per-placement daily cap, counter persistence and the published outcome messages (real MessagePipe broker).
    /// </summary>
    public sealed class AdsServiceTests
    {
        private IPublisher<RewardedAdFinishedMessage> _rewardedPublisher;
        private IPublisher<InterstitialAdFinishedMessage> _interstitialPublisher;
        private IPublisher<AdsDisabledChangedMessage> _disabledPublisher;
        private List<RewardedAdFinishedMessage> _rewardedFinished;
        private List<InterstitialAdFinishedMessage> _interstitialFinished;
        private List<AdsDisabledChangedMessage> _disabledChanged;
        private IDisposable _subscriptions;
        private InMemoryStorage _storage;

        [SetUp]
        public void SetUp()
        {
            BuiltinContainerBuilder pipe = new BuiltinContainerBuilder();
            pipe.AddMessagePipe();
            pipe.AddMessageBroker<RewardedAdFinishedMessage>();
            pipe.AddMessageBroker<InterstitialAdFinishedMessage>();
            pipe.AddMessageBroker<AdsDisabledChangedMessage>();
            IServiceProvider provider = pipe.BuildServiceProvider();
            _rewardedPublisher = provider.GetRequiredService<IPublisher<RewardedAdFinishedMessage>>();
            _interstitialPublisher = provider.GetRequiredService<IPublisher<InterstitialAdFinishedMessage>>();
            _disabledPublisher = provider.GetRequiredService<IPublisher<AdsDisabledChangedMessage>>();

            _rewardedFinished = new List<RewardedAdFinishedMessage>();
            _interstitialFinished = new List<InterstitialAdFinishedMessage>();
            _disabledChanged = new List<AdsDisabledChangedMessage>();
            _subscriptions = DisposableBag.Create(
                provider.GetRequiredService<ISubscriber<RewardedAdFinishedMessage>>().Subscribe(_rewardedFinished.Add),
                provider.GetRequiredService<ISubscriber<InterstitialAdFinishedMessage>>().Subscribe(_interstitialFinished.Add),
                provider.GetRequiredService<ISubscriber<AdsDisabledChangedMessage>>().Subscribe(_disabledChanged.Add));

            _storage = new InMemoryStorage();
        }

        [TearDown]
        public void TearDown()
        {
            _subscriptions.Dispose();
        }

        [Test]
        public void Ctor_NullPublisher_Throws()
        {
            Assert.Throws<ArgumentNullException>(() => new Internal.AdsService(null, null, null, null, _storage, null, _interstitialPublisher, _disabledPublisher));
            Assert.Throws<ArgumentNullException>(() => new Internal.AdsService(null, null, null, null, _storage, _rewardedPublisher, null, _disabledPublisher));
            Assert.Throws<ArgumentNullException>(() => new Internal.AdsService(null, null, null, null, _storage, _rewardedPublisher, _interstitialPublisher, null));
        }

        [UnityTest]
        public IEnumerator Rewarded_WithDisabledFlag_ReturnsDisabled_AndPublishesIt() => UniTask.ToCoroutine(async () =>
        {
            FakeRewarded rewarded = new FakeRewarded { Next = EAdsRewardedResult.Rewarded };
            Internal.AdsService service = CreateService(rewarded: rewarded);

            service.SetAdsDisabled(true);
            EAdsRewardedResult result = await service.ShowRewardedAsync("placement_1");

            Assert.AreEqual(EAdsRewardedResult.Disabled, result);
            Assert.AreEqual(0, rewarded.ShowCalls, "Vendor adapter must not be hit when ads are disabled.");
            AssertSingleRewarded("placement_1", EAdsRewardedResult.Disabled);
        });

        [UnityTest]
        public IEnumerator Rewarded_ReachesCap_ReturnsCapReached_AndPublishesEveryAttempt() => UniTask.ToCoroutine(async () =>
        {
            FakeRewarded rewarded = new FakeRewarded { Next = EAdsRewardedResult.Rewarded };
            Internal.AdsService service = CreateService(rewarded: rewarded);

            service.SetRewardedDailyCap("hint", 2);
            await service.ShowRewardedAsync("hint");
            await service.ShowRewardedAsync("hint");
            EAdsRewardedResult thirdResult = await service.ShowRewardedAsync("hint");

            Assert.AreEqual(2, service.GetRewardedWatchedToday("hint"));
            Assert.AreEqual(EAdsRewardedResult.CapReached, thirdResult);
            Assert.AreEqual(3, _rewardedFinished.Count);
            Assert.AreEqual(EAdsRewardedResult.Rewarded, _rewardedFinished[0].Result);
            Assert.AreEqual(EAdsRewardedResult.Rewarded, _rewardedFinished[1].Result);
            Assert.AreEqual(EAdsRewardedResult.CapReached, _rewardedFinished[2].Result);
        });

        [UnityTest]
        public IEnumerator Rewarded_Rewarded_PersistsCounter_AndPublishesPlacement() => UniTask.ToCoroutine(async () =>
        {
            FakeRewarded rewarded = new FakeRewarded { Next = EAdsRewardedResult.Rewarded };
            Internal.AdsService service = CreateService(rewarded: rewarded);

            await service.ShowRewardedAsync("double_coins");

            AssertSingleRewarded("double_coins", EAdsRewardedResult.Rewarded);
            Internal.AdsService next = CreateService(rewarded: rewarded);
            Assert.AreEqual(1, next.GetRewardedWatchedToday("double_coins"));
        });

        [UnityTest]
        public IEnumerator Rewarded_Skipped_DoesNotCount_ButIsPublished() => UniTask.ToCoroutine(async () =>
        {
            FakeRewarded rewarded = new FakeRewarded { Next = EAdsRewardedResult.Skipped };
            Internal.AdsService service = CreateService(rewarded: rewarded);

            EAdsRewardedResult result = await service.ShowRewardedAsync("hint");

            Assert.AreEqual(EAdsRewardedResult.Skipped, result);
            Assert.AreEqual(0, service.GetRewardedWatchedToday("hint"));
            AssertSingleRewarded("hint", EAdsRewardedResult.Skipped);
        });

        [UnityTest]
        public IEnumerator Rewarded_NoAdapter_PublishesFailed() => UniTask.ToCoroutine(async () =>
        {
            Internal.AdsService service = CreateService();

            EAdsRewardedResult result = await service.ShowRewardedAsync("hint");

            Assert.AreEqual(EAdsRewardedResult.Failed, result);
            AssertSingleRewarded("hint", EAdsRewardedResult.Failed);
        });

        [UnityTest]
        public IEnumerator Interstitial_Shown_PublishesShown() => UniTask.ToCoroutine(async () =>
        {
            FakeInterstitial interstitial = new FakeInterstitial { Next = true };
            Internal.AdsService service = CreateService(interstitial: interstitial);

            bool shown = await service.ShowInterstitialAsync("level_end");

            Assert.IsTrue(shown);
            Assert.AreEqual(1, _interstitialFinished.Count);
            Assert.AreEqual("level_end", _interstitialFinished[0].Placement);
            Assert.IsTrue(_interstitialFinished[0].Shown);
        });

        [UnityTest]
        public IEnumerator Interstitial_Disabled_PublishesNotShown_WithoutHittingVendor() => UniTask.ToCoroutine(async () =>
        {
            FakeInterstitial interstitial = new FakeInterstitial { Next = true };
            Internal.AdsService service = CreateService(interstitial: interstitial);
            service.SetAdsDisabled(true);

            bool shown = await service.ShowInterstitialAsync("level_end");

            Assert.IsFalse(shown);
            Assert.AreEqual(0, interstitial.ShowCalls);
            Assert.AreEqual(1, _interstitialFinished.Count);
            Assert.IsFalse(_interstitialFinished[0].Shown);
        });

        [Test]
        public void SetAdsDisabled_PublishesOnlyOnChange()
        {
            Internal.AdsService service = CreateService();

            service.SetAdsDisabled(true);
            service.SetAdsDisabled(true);
            service.SetAdsDisabled(false);

            Assert.AreEqual(2, _disabledChanged.Count);
            Assert.IsTrue(_disabledChanged[0].Disabled);
            Assert.IsFalse(_disabledChanged[1].Disabled);
        }

        private Internal.AdsService CreateService(IAdsInterstitial interstitial = null, IAdsRewarded rewarded = null)
        {
            return new Internal.AdsService(
                null, null, interstitial, rewarded, _storage,
                _rewardedPublisher, _interstitialPublisher, _disabledPublisher);
        }

        private void AssertSingleRewarded(string placement, EAdsRewardedResult result)
        {
            Assert.AreEqual(1, _rewardedFinished.Count);
            Assert.AreEqual(placement, _rewardedFinished[0].Placement);
            Assert.AreEqual(result, _rewardedFinished[0].Result);
        }

        private sealed class FakeInterstitial : IAdsInterstitial
        {
            public bool Next;
            public int ShowCalls;
            public bool IsReady => true;
            public UniTask Load(CancellationToken cancellationToken = default) => UniTask.CompletedTask;
            public UniTask<bool> Show(CancellationToken cancellationToken = default)
            {
                ShowCalls++;
                return UniTask.FromResult(Next);
            }
        }

        private sealed class FakeRewarded : IAdsRewarded
        {
            public EAdsRewardedResult Next;
            public int ShowCalls;
            public bool IsReady => true;
            public UniTask Load(CancellationToken cancellationToken = default) => UniTask.CompletedTask;
            public UniTask<EAdsRewardedResult> Show(CancellationToken cancellationToken = default)
            {
                ShowCalls++;
                return UniTask.FromResult(Next);
            }
        }

        private sealed class InMemoryStorage : ILocalDataStorage
        {
            private readonly Dictionary<Type, LocalData> _store = new();
            public event Action<Type> DataSaved;
            public event Action<Type> DataDeleted;
            public event Action AllDeleted;
            public void Save<T>(T data) where T : LocalData { _store[typeof(T)] = data; DataSaved?.Invoke(typeof(T)); }
            public void Save<T>(string slot, T data) where T : LocalData => Save(data);
            public bool TryLoad<T>(out T data) where T : LocalData
            {
                if (_store.TryGetValue(typeof(T), out LocalData stored) && stored is T typed) { data = typed; return true; }
                data = null; return false;
            }
            public bool TryLoad<T>(string slot, out T data) where T : LocalData => TryLoad(out data);
            public void Delete<T>() where T : LocalData { _store.Remove(typeof(T)); DataDeleted?.Invoke(typeof(T)); }
            public void Delete<T>(string slot) where T : LocalData => Delete<T>();
            public IEnumerable<string> ListSlots<T>() where T : LocalData => _store.ContainsKey(typeof(T)) ? new[] { string.Empty } : Array.Empty<string>();
            public void DeleteAll() { _store.Clear(); AllDeleted?.Invoke(); }
            public UniTask SaveAsync<T>(T data, CancellationToken ct = default) where T : LocalData { Save(data); return UniTask.CompletedTask; }
            public UniTask SaveAsync<T>(string slot, T data, CancellationToken ct = default) where T : LocalData { Save(data); return UniTask.CompletedTask; }
            public UniTask<(bool Found, T Data)> TryLoadAsync<T>(CancellationToken ct = default) where T : LocalData => UniTask.FromResult<(bool, T)>((TryLoad(out T d), d));
            public UniTask<(bool Found, T Data)> TryLoadAsync<T>(string slot, CancellationToken ct = default) where T : LocalData => UniTask.FromResult<(bool, T)>((TryLoad(out T d), d));
            public UniTask DeleteAsync<T>(string slot = "", CancellationToken ct = default) where T : LocalData { Delete<T>(); return UniTask.CompletedTask; }
            public UniTask DeleteAllAsync(CancellationToken ct = default) { DeleteAll(); return UniTask.CompletedTask; }
        }
    }
}
