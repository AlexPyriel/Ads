using System.Threading;
using Cysharp.Threading.Tasks;

namespace Ads
{
    /// <summary>Vendor-side rewarded-video adapter.</summary>
    public interface IAdsRewarded
    {
        /// <summary>Whether a rewarded ad is cached and ready to show.</summary>
        public bool IsReady { get; }

        /// <summary>Preloads the next rewarded video. Call after a reward or on scene load.</summary>
        public UniTask Load(CancellationToken cancellationToken = default);

        /// <summary>Shows the rewarded video. Returns the resulting <see cref="EAdsRewardedResult"/>.</summary>
        public UniTask<EAdsRewardedResult> Show(CancellationToken cancellationToken = default);
    }
}
