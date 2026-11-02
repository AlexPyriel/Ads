using LocalDataStorage;
using Zenject;

namespace GDF.Ads.Installers.Zenject
{
    /// <summary>
    /// Registers <see cref="IAdsService"/> in a Zenject container. Vendor adapters are injected into
    /// the installer as scoped arguments; nulls are accepted for channels the game does not use.
    /// </summary>
    public sealed class AdsZenjectInstaller : Installer<AdsZenjectInstaller.Args, AdsZenjectInstaller>
    {
        [Inject]
        private Args _args;

        /// <summary>Vendor-adapter bundle passed to the installer.</summary>
        public sealed class Args
        {
            public IAdsInitializer Initializer;
            public IAdsBanner Banner;
            public IAdsInterstitial Interstitial;
            public IAdsRewarded Rewarded;
        }

        /// <inheritdoc />
        public override void InstallBindings()
        {
            Args a = _args ?? new Args();
            Container.Bind<IAdsService>().FromMethod(ctx => new Internal.AdsService(
                a.Initializer,
                a.Banner,
                a.Interstitial,
                a.Rewarded,
                ctx.Container.Resolve<ILocalDataStorage>())).AsSingle();
        }
    }
}
