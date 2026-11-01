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
- DI installers for VContainer, Zenject and Reflex.

## Requirements

- Unity `6000.0+`
- `com.gdf.local-data-storage` `2.0.0+`
- `com.cysharp.unitask`
- One of the supported DI containers (optional, required only when using the matching installer):
  - `jp.hadashikick.vcontainer`
  - `com.mathijsbakker.extenject`
  - `com.gustavopsantos.reflex`
- Vendor SDK (AppLovin, UnityAds, IronSource, AdMob, …) added to the host project when a real
  monetization network is used. The package itself ships no vendor SDK.

## Installation (UPM via Git)

Add this dependency to `Packages/manifest.json`:

```json
"com.gdf.ads": "https://github.com/AlexPyriel/Ads.git#v1.0.0"
```

For development tracking (not recommended for production), use `#main` instead of a tag.

You can also install it from Unity Package Manager:

- `Window -> Package Manager -> + -> Add package from git URL...`
- `https://github.com/AlexPyriel/Ads.git#v1.0.0`

## Usage

### 1. Author vendor adapters

Implement the four interfaces once per vendor. Minimal AppLovin shim for rewarded video:

```csharp
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
protected override void Configure(IContainerBuilder builder)
{
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
await _ads.InitializeAsync();
_ads.SetRewardedDailyCap("hint", 3);
_ads.SetRewardedDailyCap("double_coins", 5);
```

### 4. Call from gameplay

```csharp
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
public void OnRemoveAdsPurchased() => _ads.SetAdsDisabled(true);
```

## Assembly Definition Notes

`GDF.Ads` is a dedicated runtime assembly. DI installers live in their own assemblies
(`GDF.Ads.Installers.VContainer`, `.Zenject`, `.Reflex`). Internals are accessible to installers
through `InternalsVisibleTo`. Vendor adapters are plain C# classes in the host project; they can
live in any assembly and are passed into the installer as instances.

## License

MIT. See `LICENSE.md`.
