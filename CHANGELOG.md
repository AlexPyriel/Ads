# Changelog

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
