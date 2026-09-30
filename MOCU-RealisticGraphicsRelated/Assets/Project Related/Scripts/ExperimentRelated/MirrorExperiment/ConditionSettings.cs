namespace MirrorExperiment
{
    /// One stimulus condition. Each one gets its own staircase and its own share
    /// of the trials, and they are interleaved.
    ///
    /// The set of conditions is a list in Parameters, not a fixed enum, so the same
    /// code runs the paper's five-condition design and a single-condition design
    /// without changes.
    ///
    /// Every trial has the same structure regardless of condition. The two flags
    /// only decide what is left out: no visual means the stars are not drawn, no
    /// vestibular means no commands go to the platform. The fixation point, if
    /// shown at all, is shown the same way, and the timing is identical either way.
    public class ConditionSettings
    {
        /// Set false to leave a condition in the config but out of the run. JSON has
        /// no comments, so this is how a condition gets parked during piloting
        /// without deleting it and having to type it back later.
        public bool Enabled { get; set; } = true;

        public bool HasVisual { get; set; } = true;
        public bool HasVestibular { get; set; } = true;

        /// Fraction of stars that stay put on each noise tick. Ignored when
        /// HasVisual is false, since there is no optic flow to degrade.
        public float Coherence { get; set; } = 1f;

        /// Visual/vestibular conflict in degrees: the vestibular heading is offset
        /// by +Delta/2 and the visual heading by -Delta/2. Only meaningful when both
        /// cues are present, and 0 means the two agree.
        ///
        /// Conditions come in pairs of opposite sign (+6 and -6) so that the
        /// participant's personal bias cancels: the weighting effect flips sign
        /// between the two and the bias does not, so the difference of the two points
        /// of subjective equality isolates the weighting, and their mean gives the
        /// bias. One sign alone cannot separate them. See README.md.
        public float Delta { get; set; } = 0f;

        /// Human-readable name for logs and the UI tab.
        public string Label
        {
            get
            {
                if (!HasVisual && !HasVestibular)
                    return "Empty";

                if (!HasVisual)
                    return "Vestibular";

                string coherence = $"{Coherence * 100f:F0}%";

                if (!HasVestibular)
                    return $"Visual {coherence}";

                return $"Combined {coherence} delta={(Delta >= 0f ? "+" : "")}{Delta:0.#}";
            }
        }
    }
}
