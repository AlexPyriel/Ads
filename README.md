# GDF Ads

Vendor-agnostic ad orchestration for Unity: banner, interstitial, rewarded video, daily
reward-video caps, IAP-driven ad disabling. Vendor SDKs (AppLovin, UnityAds, IronSource,
AdMob, …) are plugged in through adapter implementations in the host project.

Package name: `com.gdf.ads`
Assembly name: `GDF.Ads`

## Features

- `IAdsService` facade: `InitializeAsync`, `ShowBanner` / `HideBanner`, `ShowInterstitialAsync`, `ShowRewardedAsync`, `SetRewardedDailyCap`, `SetAdsDisabled`.
- Vendor-side interfaces game code never touches directly: `IAdsInitializer`, `IAdsBanner`, `IAdsInterstitial`, `IAdsRewarded`.
- Per-placement **daily cap** on rewarded videos with UTC day roll-over.
- **Remove-ads** flag toggled by IAP; disables banners and interstitials and short-circuits rewarded calls.
- Rewarded outcome enum (`Rewarded`, `Skipped`, `Failed`, `CapReached`, `Disabled`) — one return value per attempt.
- Persistence through `com.gdf.local-data-storage` (`AdsStateData` with caps + per-UTC-day counters + disabled flag).
- Optional channels: pass only the adapters your game actually uses — banner-less or interstitial-less games stay valid.
- MessagePipe messages for every outcome: `RewardedAdFinishedMessage`, `InterstitialAdFinishedMessage`,
  `AdsDisabledChangedMessage`.
- VContainer installer; public `sealed` `AdsService` for any other DI container.

## Requirements

- Unity `6000.0+`
- `com.gdf.local-data-storage` `3.0.0+`
- `com.cysharp.unitask`
- `com.cysharp.messagepipe` `1.8.2+` (required). The host registers MessagePipe once at its composition root;
  the package installer does not.
- `jp.hadashikick.vcontainer` and `com.cysharp.messagepipe.vcontainer` when using the bundled installer.
- Vendor SDK (AppLovin, UnityAds, IronSource, AdMob, …) added to the host project when a real
  monetization network is used. The package itself ships no vendor SDK.

## Installation (UPM via Git)

Add this dependency to `Packages/manifest.json`:

```json
"com.gdf.ads": "https://github.com/AlexPyriel/Ads.git#v2.0.0"
```

For development tracking (not recommended for production), use `#main` instead of a tag.

You can also install it from Unity Package Manager:

- `Window -> Package Manager -> + -> Add package from git URL...`
- `https://github.com/AlexPyriel/Ads.git#v2.0.0`

## Usage

### 1. Author vendor adapters

Implement the four interfaces once per vendor. Minimal AppLovin shim for rewarded video:

```csharp
using System.Threading;
using Cysharp.Threading.Tasks;
using GDF.Ads;

public sealed class AppLovinRewarded : IAdsRewarded
{
    private readonly string _adUnit;

    public bool IsReady => MaxSdk.IsRewardedAdReady(_adUnit);

    public async UniTask Load(CancellationToken cancellationToken = default)
    {
        MaxSdk.LoadRewardedAd(_adUnit);
        await UniTask.WaitUntil(() => IsReady || cancellationToken.IsCancellationRequested);
    }

    public async UniTask<EAdsRewardedResult> Show(CancellationToken cancellationToken = default)
    {
        UniTaskCompletionSource<EAdsRewardedResult> tcs = new();
        MaxSdkCallbacks.Rewarded.OnAdReceivedRewardEvent += (_, _) => tcs.TrySetResult(EAdsRewardedResult.Rewarded);
        MaxSdkCallbacks.Rewarded.OnAdHiddenEvent += _ => tcs.TrySetResult(EAdsRewardedResult.Skipped);
        MaxSdkCallbacks.Rewarded.OnAdDisplayFailedEvent += (_, _) => tcs.TrySetResult(EAdsRewardedResult.Failed);
        MaxSdk.ShowRewardedAd(_adUnit);
        return await tcs.Task.AttachExternalCancellation(cancellationToken);
    }
}
```

### 2. Register the service

```csharp
using GDF.Ads.Installers.VContainer;
using LocalDataStorage.Installers.VContainer;
using MessagePipe;
using VContainer;

protected override void Configure(IContainerBuilder builder)
{
    builder.RegisterMessagePipe();      // once per application, in the root scope
    new LocalDataStorageVContainerInstaller().WithFileBackend().Install(builder);

    new AdsVContainerInstaller()
        .WithInitializer(new AppLovinInitializer())
        .WithRewarded(new AppLovinRewarded())
        .WithInterstitial(new AppLovinInterstitial())
        .Install(builder);
}
```

### 3. Initialize and configure caps at startup

```csharp
using GDF.Ads;

await _ads.InitializeAsync();
_ads.SetRewardedDailyCap("hint", 3);
_ads.SetRewardedDailyCap("double_coins", 5);
```

### 4. Call from gameplay

```csharp
using GDF.Ads;

EAdsRewardedResult result = await _ads.ShowRewardedAsync("hint");
switch (result)
{
    case EAdsRewardedResult.Rewarded: GrantHint(); break;
    case EAdsRewardedResult.CapReached: ShowSoftWall(); break;
    case EAdsRewardedResult.Disabled: GrantHint(); break;   // remove-ads players still get the content
}
```

### 5. Remove-ads IAP

```csharp
using GDF.Ads;

public void OnRemoveAdsPurchased() => _ads.SetAdsDisabled(true);
```

### 6. React to outcomes elsewhere

State is read with queries (`AdsDisabled`, `GetRewardedWatchedToday`); facts arrive as messages. The caller
still gets the result from the `await`; messages let analytics, pacing or a banner slot react without
wrapping every call site:

```csharp
using GDF.Ads.Messages;
using MessagePipe;

_rewardedFinished.Subscribe(message => _analytics.TrackInfo("ad:rewarded", "result", message.Result.ToString()));
_adsDisabledChanged.Subscribe(message => _bannerSlot.SetActive(!message.Disabled));
```

`ShowRewardedAsync` and `ShowInterstitialAsync` publish exactly one message per call, with the returned outcome,
including `Disabled`, `CapReached` and `Failed`. A cancelled call publishes nothing. `AdsDisabledChangedMessage`
is published only when the flag actually flips.

## Assembly Definition Notes

`GDF.Ads` is a dedicated runtime assembly that depends on `GDF.LocalDataStorage`, `UniTask` and
`MessagePipe`. The VContainer installer lives in `GDF.Ads.Installers.VContainer` and compiles only when
`jp.hadashikick.vcontainer` is installed. `GDF.Ads.Internal.AdsService` is `public sealed` so any container
can construct it. Vendor adapters are plain C# classes in the host project; they can live in any assembly
and are passed into the installer as instances.

## Using with Zenject / Reflex

The package ships only a VContainer installer. Other containers provide the three message brokers and build
the service with the adapters they use (pass `null` for a missing channel).

Zenject (`MessagePipe.Zenject`, in the `ProjectContext` installer):

```csharp
using GDF.Ads;
using GDF.Ads.Internal;
using GDF.Ads.Messages;
using LocalDataStorage;
using MessagePipe;

MessagePipeOptions options = Container.BindMessagePipe();
Container.BindMessageBroker<RewardedAdFinishedMessage>(options);
Container.BindMessageBroker<InterstitialAdFinishedMessage>(options);
Container.BindMessageBroker<AdsDisabledChangedMessage>(options);

Container.Bind<IAdsService>().FromMethod(context => new AdsService(
        new AppLovinInitializer(), null, new AppLovinInterstitial(), new AppLovinRewarded(),
        context.Container.Resolve<ILocalDataStorage>(),
        context.Container.Resolve<IPublisher<RewardedAdFinishedMessage>>(),
        context.Container.Resolve<IPublisher<InterstitialAdFinishedMessage>>(),
        context.Container.Resolve<IPublisher<AdsDisabledChangedMessage>>()))
    .AsSingle();
```

Reflex (MessagePipe's built-in container provides the brokers):

```csharp
using System;
using GDF.Ads;
using GDF.Ads.Internal;
using GDF.Ads.Messages;
using LocalDataStorage;
using MessagePipe;

BuiltinContainerBuilder pipe = new BuiltinContainerBuilder();
pipe.AddMessagePipe();
pipe.AddMessageBroker<RewardedAdFinishedMessage>();
pipe.AddMessageBroker<InterstitialAdFinishedMessage>();
pipe.AddMessageBroker<AdsDisabledChangedMessage>();
IServiceProvider provider = pipe.BuildServiceProvider();

// Register ISubscriber<T> for each message the same way, for the project's subscribers.
builder.AddSingleton(provider.GetRequiredService<ISubscriber<RewardedAdFinishedMessage>>(), typeof(ISubscriber<RewardedAdFinishedMessage>));

builder.AddSingleton(container => new AdsService(
        new AppLovinInitializer(), null, new AppLovinInterstitial(), new AppLovinRewarded(),
        container.Resolve<ILocalDataStorage>(),
        provider.GetRequiredService<IPublisher<RewardedAdFinishedMessage>>(),
        provider.GetRequiredService<IPublisher<InterstitialAdFinishedMessage>>(),
        provider.GetRequiredService<IPublisher<AdsDisabledChangedMessage>>()),
    typeof(IAdsService));
```

## License

MIT. See `LICENSE.md`.
