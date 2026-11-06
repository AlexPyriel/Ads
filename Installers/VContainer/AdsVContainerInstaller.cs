using GDF.Ads.Messages;
using LocalDataStorage;
using MessagePipe;
using VContainer;
using VContainer.Unity;

namespace GDF.Ads.Installers.VContainer
{
    /// <summary>
    /// Registers <see cref="IAdsService"/> in a VContainer scope. Vendor adapters
    /// (<see cref="IAdsInitializer"/>, <see cref="IAdsBanner"/>, <see cref="IAdsInterstitial"/>,
    /// <see cref="IAdsRewarded"/>) are supplied through fluent methods — any subset is accepted,
    /// missing adapters disable that channel (banner-less games stay valid).
    /// </summary>
    public sealed class AdsVContainerInstaller : IInstaller
    {
        private IAdsInitializer _initializer;
        private IAdsBanner _banner;
        private IAdsInterstitial _interstitial;
        private IAdsRewarded _rewarded;

        public AdsVContainerInstaller WithInitializer(IAdsInitializer initializer) { _initializer = initializer; return this; }
        public AdsVContainerInstaller WithBanner(IAdsBanner banner) { _banner = banner; return this; }
        public AdsVContainerInstaller WithInterstitial(IAdsInterstitial interstitial) { _interstitial = interstitial; return this; }
        public AdsVContainerInstaller WithRewarded(IAdsRewarded rewarded) { _rewarded = rewarded; return this; }

        /// <inheritdoc />
        public void Install(IContainerBuilder builder)
        {
            builder.Register(resolver => new Internal.AdsService(
                    _initializer,
                    _banner,
                    _interstitial,
                    _rewarded,
                    resolver.Resolve<ILocalDataStorage>(),
                    resolver.Resolve<IPublisher<RewardedAdFinishedMessage>>(),
                    resolver.Resolve<IPublisher<InterstitialAdFinishedMessage>>(),
                    resolver.Resolve<IPublisher<AdsDisabledChangedMessage>>()),
                Lifetime.Singleton)
                .As<IAdsService>();
        }
    }
}
