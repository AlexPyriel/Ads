using System.Threading;
using Cysharp.Threading.Tasks;

namespace Ads
{
    /// <summary>Vendor-side interstitial adapter.</summary>
    public interface IAdsInterstitial
    {
        /// <summary>Whether an interstitial ad is cached and ready to show.</summary>
        public bool IsReady { get; }

        /// <summary>Preloads the next interstitial. Call opportunistically (e.g. after level start).</summary>
        public UniTask Load(CancellationToken cancellationToken = default);

        /// <summary>Shows the interstitial. Returns <see langword="true"/> on a successful show.</summary>
        public UniTask<bool> Show(CancellationToken cancellationToken = default);
    }
}
