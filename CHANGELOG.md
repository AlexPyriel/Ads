# Changelog

## [1.0.1] - 2026-11-02

### Changed

- **Breaking:** root namespace renamed from `Ads` to `GDF.Ads`. Consumers must update
  `using Ads;` → `using GDF.Ads;` (and installer sub-namespaces similarly). The rename removes a
  potential conflict with Unity's built-in `UnityEngine.Advertisements` namespace and aligns with
  the GDF.* namespace discipline across the library family.

### Fixed

- VContainer installer compilation: added missing `using VContainer.Unity;` so `IInstaller`
  (which lives in `VContainer.Unity`, not `VContainer`) resolves. Without this fix the
  installer assembly failed with `CS0246`.

## [1.0.0] - 2026-11-02

Initial public release.

- Vendor-agnostic interfaces: `IAdsInitializer`, `IAdsBanner`, `IAdsInterstitial`, `IAdsRewarded`.
- `IAdsService` facade: `InitializeAsync`, `ShowBanner` / `HideBanner`, `ShowInterstitialAsync`, `ShowRewardedAsync`, cap management, remove-ads flag.
- Events: `AdsDisabledChanged(bool)`, `RewardedWatched(string placement)`.
- Enforces per-placement daily caps on rewarded videos (UTC day roll-over resets counters).
- IAP-driven remove-ads flag (`SetAdsDisabled`) persisted through `com.gdf.local-data-storage`.
- `EAdsRewardedResult`: `Rewarded`, `Skipped`, `Failed`, `CapReached`, `Disabled`.
- Non-vendor by design: no AppLovin / IronSource / UnityAds SDK is pulled; adapters live in the host project.
- DI installers for VContainer, Zenject and Reflex with fluent / override hooks for optional vendor adapters (banner-less games stay valid).
- EditMode tests cover disabled-flag gating, daily cap, rewarded-event firing and counter persistence.
