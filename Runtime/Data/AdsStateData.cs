using System;
using System.Collections.Generic;
using LocalDataStorage;

namespace GDF.Ads.Data
{
    /// <summary>Persisted ads-service state: disabled flag, per-placement caps, per-placement daily counters.</summary>
    [Serializable]
    public sealed class AdsStateData : LocalData
    {
        /// <summary>Global remove-ads flag.</summary>
        public bool AdsDisabled;

        /// <summary>UTC day boundary (YYYYMMDD) of the last counter reset.</summary>
        public int CountersUtcDay;

        /// <summary>Per-placement daily caps.</summary>
        public List<CapEntry> Caps = new List<CapEntry>();

        /// <summary>Per-placement rewarded-watch counters for the current UTC day.</summary>
        public List<CounterEntry> Counters = new List<CounterEntry>();

        /// <summary>Daily cap entry.</summary>
        [Serializable]
        public struct CapEntry
        {
            /// <summary>Placement identifier.</summary>
            public string Placement;

            /// <summary>Daily cap value (<see cref="int.MaxValue"/> means no cap).</summary>
            public int Cap;
        }

        /// <summary>Counter entry.</summary>
        [Serializable]
        public struct CounterEntry
        {
            /// <summary>Placement identifier.</summary>
            public string Placement;

            /// <summary>Current count for the UTC day.</summary>
            public int Count;
        }
    }
}
