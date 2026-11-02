namespace GDF.Ads
{
    /// <summary>Vendor-side banner adapter.</summary>
    public interface IAdsBanner
    {
        /// <summary>Shows the banner (loads it first when needed). No-op when already visible.</summary>
        public void Show();

        /// <summary>Hides the banner without unloading. Call to pause ads during menus / cinematics.</summary>
        public void Hide();

        /// <summary>Hides and destroys the banner instance. Call before scene unload if the banner is scene-scoped.</summary>
        public void Destroy();
    }
}
