using System;
using System.Threading;
using Cysharp.Threading.Tasks;

namespace GDF.Ads
{
    /// <summary>
    /// Facade over the vendor adapters (<see cref="IAdsInitializer"/>, <see cref="IAdsBanner"/>,
    /// <see cref="IAdsInterstitial"/>, <see cref="IAdsRewarded"/>). Enforces the "ads disabled"
    /// flag (IAP remove-ads) and the daily cap on rewarded videos. Game code calls into this facade
    /// and never into vendor SDKs directly.
    /// </summary>
    public interface IAdsService
    {
        /// <summary>Whether ads are disabled (IAP removed ads). Governs banner/interstitial calls.</summary>
        public bool AdsDisabled { get; }

        /// <summary>Raised when <see cref="AdsDisabled"/> flips.</summary>
        public event Action<bool> AdsDisabledChanged;

        /// <summary>Raised after a successful rewarded view with the chosen placement id.</summary>
        public event Action<string> RewardedWatched;

        /// <summary>Initializes the vendor SDK through the registered <see cref="IAdsInitializer"/>.</summary>
        public UniTask<bool> InitializeAsync(CancellationToken cancellationToken = default);

        /// <summary>Shows the banner (no-op when disabled).</summary>
        public void ShowBanner();

        /// <summary>Hides the banner.</summary>
        public void HideBanner();

        /// <summary>
        /// Shows an interstitial. Returns <see langword="false"/> when disabled, not initialized or when
        /// the vendor refused to serve.
        /// </summary>
        public UniTask<bool> ShowInterstitialAsync(string placement, CancellationToken cancellationToken = default);

        /// <summary>
        /// Shows a rewarded video for a placement. Enforces the per-placement daily cap and the
        /// global disabled flag before hitting the vendor.
        /// </summary>
        public UniTask<EAdsRewardedResult> ShowRewardedAsync(string placement, CancellationToken cancellationToken = default);

        /// <summary>Current number of rewarded views for the placement in the active UTC day.</summary>
        public int GetRewardedWatchedToday(string placement);

        /// <summary>Daily cap for a placement (<see cref="int.MaxValue"/> means no cap).</summary>
        public int GetRewardedDailyCap(string placement);

        /// <summary>Sets the daily cap for a placement. Pass <see cref="int.MaxValue"/> to disable the cap.</summary>
        public void SetRewardedDailyCap(string placement, int cap);

        /// <summary>Flips the global disabled flag (typically called after an IAP "remove ads" grant).</summary>
        public void SetAdsDisabled(bool disabled);
    }
}
