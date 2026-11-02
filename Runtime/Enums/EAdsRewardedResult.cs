namespace GDF.Ads
{
    /// <summary>Outcome of a rewarded-video attempt reported back by the vendor adapter.</summary>
    public enum EAdsRewardedResult
    {
        /// <summary>User watched the full ad and the reward must be granted.</summary>
        Rewarded = 0,

        /// <summary>User closed the ad before the reward threshold; no reward.</summary>
        Skipped = 1,

        /// <summary>The ad failed to load or play (network error, fill failure, SDK not initialized).</summary>
        Failed = 2,

        /// <summary>The daily cap is reached and the request was refused before hitting the vendor.</summary>
        CapReached = 3,

        /// <summary>Ads are disabled (IAP removed ads) and the request was refused.</summary>
        Disabled = 4,
    }
}
