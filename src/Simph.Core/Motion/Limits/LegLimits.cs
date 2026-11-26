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
        /// The lab's MB-E-6DOF/12/1000. The actuators' software length limits,
        /// 0.02032 to 0.32004 m of stroke, are the defaults in CDS32830 5.2.2 and
        /// still to be checked against the MBC's own configuration; the joint
        /// length of a fully retracted leg, 0.71863 m, was measured on
        /// 2026-11-25 against the MBC's reported leg lengths (0.718621-0.718635
        /// over the six legs). These limits are stricter than the data sheet:
        /// sway 0.25 m at neutral needs 0.331 m of stroke, so either this
        /// machine's limits are wider than the defaults or the sheet's sideways
        /// excursion is not reachable at neutral heave. Until the MBC's own
        /// numbers are read, the stricter answer is the one kept.
        /// </summary>
        public static LegLimits MoogMbE6Dof { get; } =
            new LegLimits(HexapodGeometry.MoogMbE6Dof, 0.71863 + 0.02032, 0.71863 + 0.32004);
    }
}
