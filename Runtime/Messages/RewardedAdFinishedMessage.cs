namespace GDF.Ads.Messages
{
    /// <summary>
    /// Published once per completed <see cref="IAdsService.ShowRewardedAsync"/> call, whatever the
    /// outcome, so analytics, rewards and UI can react to every rewarded attempt in one place.
    /// </summary>
    public readonly struct RewardedAdFinishedMessage
    {
        /// <summary>Creates the message.</summary>
        /// <param name="placement">Placement id passed to the call.</param>
        /// <param name="result">Outcome of the attempt.</param>
        public RewardedAdFinishedMessage(string placement, EAdsRewardedResult result)
        {
            Placement = placement;
            Result = result;
        }

        /// <summary>Placement id passed to the call; may be empty when the caller passed none.</summary>
        public string Placement { get; }

        /// <summary>Outcome of the attempt; only <see cref="EAdsRewardedResult.Rewarded"/> grants a reward.</summary>
        public EAdsRewardedResult Result { get; }
    }
}
