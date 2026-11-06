namespace GDF.Ads.Messages
{
    /// <summary>
    /// Published once per completed <see cref="IAdsService.ShowInterstitialAsync"/> call, shown or not,
    /// so analytics and pacing logic see every interstitial opportunity.
    /// </summary>
    public readonly struct InterstitialAdFinishedMessage
    {
        /// <summary>Creates the message.</summary>
        /// <param name="placement">Placement id passed to the call.</param>
        /// <param name="shown">Whether the vendor actually showed the ad.</param>
        public InterstitialAdFinishedMessage(string placement, bool shown)
        {
            Placement = placement;
            Shown = shown;
        }

        /// <summary>Placement id passed to the call.</summary>
        public string Placement { get; }

        /// <summary>
        /// <see langword="true"/> when the vendor showed the ad; <see langword="false"/> when ads are
        /// disabled, no interstitial adapter is registered or the vendor refused to serve.
        /// </summary>
        public bool Shown { get; }
    }
}
