using System.Threading;
using Cysharp.Threading.Tasks;

namespace GDF.Ads
{
    /// <summary>
    /// Vendor-side SDK initialization. Implemented once per vendor (AppLovin, IronSource, UnityAds).
    /// </summary>
    public interface IAdsInitializer
    {
        /// <summary>Whether the vendor SDK finished its handshake and is ready to show ads.</summary>
        public bool IsReady { get; }

        /// <summary>Boots the vendor SDK. Returns when the SDK is ready or when the attempt fails.</summary>
        public UniTask<bool> InitializeAsync(CancellationToken cancellationToken = default);
    }
}
