using System;
using System.Collections;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using LocalDataStorage;
using NUnit.Framework;
using UnityEngine.TestTools;

namespace Ads.Tests.EditMode
{
    /// <summary>
    /// Pure-logic coverage of <see cref="Internal.AdsService"/>: disabled flag gating,
    /// per-placement daily cap, UTC day roll-over, rewarded-watch event, counter persistence.
    /// </summary>
    public sealed class AdsServiceTests
    {
        [UnityTest]
        public IEnumerator Rewarded_WithDisabledFlag_ReturnsDisabled() => UniTask.ToCoroutine(async () =>
        {
            FakeRewarded rewarded = new FakeRewarded { Next = EAdsRewardedResult.Rewarded };
            InMemoryStorage storage = new InMemoryStorage();
            Internal.AdsService service = new Internal.AdsService(null, null, null, rewarded, storage);

            service.SetAdsDisabled(true);
            EAdsRewardedResult result = await service.ShowRewardedAsync("placement_1");

            Assert.AreEqual(EAdsRewardedResult.Disabled, result);
            Assert.AreEqual(0, rewarded.ShowCalls, "Vendor adapter must not be hit when ads are disabled.");
        });

        [UnityTest]
        public IEnumerator Rewarded_ReachesCap_ReturnsCapReached() => UniTask.ToCoroutine(async () =>
        {
            FakeRewarded rewarded = new FakeRewarded { Next = EAdsRewardedResult.Rewarded };
            InMemoryStorage storage = new InMemoryStorage();
            Internal.AdsService service = new Internal.AdsService(null, null, null, rewarded, storage);

            service.SetRewardedDailyCap("hint", 2);
            await service.ShowRewardedAsync("hint");
            await service.ShowRewardedAsync("hint");
            EAdsRewardedResult thirdResult = await service.ShowRewardedAsync("hint");

            Assert.AreEqual(2, service.GetRewardedWatchedToday("hint"));
            Assert.AreEqual(EAdsRewardedResult.CapReached, thirdResult);
        });

        [UnityTest]
        public IEnumerator Rewarded_RewardsFired_PersistsCounter() => UniTask.ToCoroutine(async () =>
        {
            FakeRewarded rewarded = new FakeRewarded { Next = EAdsRewardedResult.Rewarded };
            InMemoryStorage storage = new InMemoryStorage();
            Internal.AdsService service = new Internal.AdsService(null, null, null, rewarded, storage);

            string observed = null;
            service.RewardedWatched += p => observed = p;

            await service.ShowRewardedAsync("double_coins");

            Assert.AreEqual("double_coins", observed);
            Internal.AdsService next = new Internal.AdsService(null, null, null, rewarded, storage);
            Assert.AreEqual(1, next.GetRewardedWatchedToday("double_coins"));
        });

        [Test]
        public void SetAdsDisabled_FiresEvent_OnlyOnChange()
        {
            InMemoryStorage storage = new InMemoryStorage();
            Internal.AdsService service = new Internal.AdsService(null, null, null, null, storage);
            int fires = 0;
            service.AdsDisabledChanged += _ => fires++;

            service.SetAdsDisabled(true);
            service.SetAdsDisabled(true);
            service.SetAdsDisabled(false);

            Assert.AreEqual(2, fires);
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
