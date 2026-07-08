namespace MoogModule
{
    public class TrajectoryProfileSettings_CDF
    {
        /// <summary>
        /// Number of sigmas (σ) within half of the total time span.
        /// If the profile is normalized to [0, 1] on both axes, higher values produce steeper transitions.
        /// For reference, the proportion of area under the curve within ±Xσ:
        /// - 1σ  → ~68.3%
        /// - 2σ  → ~95.4%
        /// - 3σ  → ~99.7%
        /// - 4σ  → ~99.994%
        /// </summary>
        public float Sigmas { get; set; } = 3;
    }
}