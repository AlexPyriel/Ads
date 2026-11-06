namespace GDF.Ads.Messages
{
    /// <summary>
    /// Published when the global remove-ads flag actually flips (typically after an IAP grant), so
    /// banner slots, shop offers and layout can update.
    /// </summary>
    public readonly struct AdsDisabledChangedMessage
    {
        /// <summary>Creates the message.</summary>
        /// <param name="disabled">New value of <see cref="IAdsService.AdsDisabled"/>.</param>
        public AdsDisabledChangedMessage(bool disabled)
        {
            Disabled = disabled;
        }

        /// <summary>New value of <see cref="IAdsService.AdsDisabled"/>.</summary>
        public bool Disabled { get; }
    }
}
