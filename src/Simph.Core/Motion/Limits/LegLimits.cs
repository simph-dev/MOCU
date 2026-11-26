using System;

using Simph.Core.Platform;

namespace Simph.Core.Motion.Limits
{
    /// <summary>
    /// How short and how long each leg may be, joint to joint. The limits per
    /// axis hold with the other axes at neutral only; a pose that combines them,
    /// such as far forward and low, can still ask a leg for more stroke than it
    /// has. The MBC clamps a leg at its software length limit, which would bend
    /// the movement without saying so, so such a movement is refused before it
    /// is sent.
    /// </summary>
    public sealed class LegLimits
    {
        public LegLimits(HexapodGeometry geometry, double shortestMetres, double longestMetres)
        {
            Geometry = geometry ?? throw new ArgumentNullException(nameof(geometry));

            if (!(shortestMetres > 0 && longestMetres > shortestMetres))
                throw new ArgumentOutOfRangeException(nameof(longestMetres), longestMetres,
                    $"Needs 0 < shortest < longest; got {shortestMetres} and {longestMetres}.");

            ShortestMetres = shortestMetres;
            LongestMetres = longestMetres;
        }

        /// <summary>Works out each leg's length.</summary>
        public HexapodGeometry Geometry { get; }

        public double ShortestMetres { get; }
        public double LongestMetres { get; }

        /// <summary>
        /// The lab's MB-E-6DOF/12/1000. The MBC limits each leg's command to
        /// 0.02032-0.32004 m of stroke (its geometry.xml, Limits > Length
        /// (Command), read on 2026-11-26; it faults past 0.01778-0.32258, of a
        /// full stroke of 0.34036). Stroke is counted from a retracted joint
        /// length the MBC takes as 0.71882 m; against our geometry its reported
        /// leg lengths put that at 0.71863 m (0.718621-0.718635 over the six
        /// legs, measured 2026-11-25), and the limits are set on that, so they
        /// are where the MBC applies them. They are stricter than the data
        /// sheet: sway 0.25 m at neutral needs 0.331 m of stroke, so the sheet's
        /// +/-0.259 m sideways is not reachable at neutral heave on this
        /// configuration; the MBC would clamp the legs.
        /// </summary>
        public static LegLimits MoogMbE6Dof { get; } =
            new LegLimits(HexapodGeometry.MoogMbE6Dof, 0.71863 + 0.02032, 0.71863 + 0.32004);
    }
}
