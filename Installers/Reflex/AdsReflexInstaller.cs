using LocalDataStorage;
using Reflex.Core;
using UnityEngine;

namespace Ads.Installers.Reflex
{
    /// <summary>
    /// Registers <see cref="IAdsService"/> in a Reflex container. Subclass this installer to provide
    /// concrete vendor adapters through <see cref="BuildInitializer"/> and friends; the base keeps
    /// nulls for channels the game does not use.
    /// </summary>
    public class AdsReflexInstaller : MonoBehaviour, IInstaller
    {
        /// <inheritdoc />
        public void InstallBindings(ContainerBuilder builder)
        {
            IAdsInitializer initializer = BuildInitializer();
            IAdsBanner banner = BuildBanner();
            IAdsInterstitial interstitial = BuildInterstitial();
            IAdsRewarded rewarded = BuildRewarded();

            builder.AddSingleton((Reflex.Core.Container container) => (IAdsService)new Internal.AdsService(
                initializer,
                banner,
                interstitial,
                rewarded,
                container.Resolve<ILocalDataStorage>()), typeof(IAdsService));
        }

        /// <summary>Override to provide the vendor SDK initializer.</summary>
        protected virtual IAdsInitializer BuildInitializer() => null;

        /// <summary>Override to provide the banner adapter.</summary>
        protected virtual IAdsBanner BuildBanner() => null;

        /// <summary>Override to provide the interstitial adapter.</summary>
        protected virtual IAdsInterstitial BuildInterstitial() => null;

        /// <summary>Override to provide the rewarded-video adapter.</summary>
        protected virtual IAdsRewarded BuildRewarded() => null;
    }
}
