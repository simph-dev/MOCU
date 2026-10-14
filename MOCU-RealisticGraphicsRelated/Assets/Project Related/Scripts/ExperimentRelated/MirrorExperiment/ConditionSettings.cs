using Newtonsoft.Json;


namespace MirrorExperiment
{
    /// One stimulus condition. Each one gets its own staircase and its own share
    /// of the trials, and they are interleaved.
    ///
    /// The set of conditions is a list in Parameters, not a fixed enum, so the same
    /// code runs the default seven-condition design and a single-condition design
    /// without changes.
    ///
    /// Three cues, each switched on or off per condition - which gives the three
    /// unimodal, three bimodal and one trimodal combination:
    ///
    ///     HasVisualInside     cen: the optic flow on the screen in the centre
    ///     HasVisualOutside    per: the optic flow seen directly, in the periphery
    ///     HasVestibular       ves: the platform
    ///
    /// Every trial has the same structure regardless of condition. The flags only
    /// decide what is left out: a visual field that is off has no stars in it, no
    /// vestibular means no commands go to the platform. The mirror itself, its frame
    /// and the fixation point stay the same in every condition, so nothing on the
    /// screen tells the participant which condition this is.
    public class ConditionSettings
    {
        /// Set false to leave a condition in the config but out of the run. JSON has
        /// no comments, so this is how a condition gets parked during piloting
        /// without deleting it and having to type it back later.
        public bool Enabled { get; set; } = true;

        /// Stars on the screen. Needs RearView.Enabled.
        public bool HasVisualInside { get; set; } = false;

        /// How the screen shows them: Screen (one flat picture, a parking display)
        /// or Mirror (a picture for each eye, a real mirror). Per condition, so the
        /// two can be interleaved in one run, each with its own staircase. See
        /// InsideView.
        public InsideView InsideView { get; set; } = InsideView.Screen;

        /// Stars seen directly, everywhere the mirror does not cover.
        public bool HasVisualOutside { get; set; } = true;

        public bool HasVestibular { get; set; } = true;

        /// Fraction of stars that stay put on each noise tick. Ignored when both
        /// visual fields are off, since there is no optic flow to degrade.
        public float Coherence { get; set; } = 1f;

        /// Visual/vestibular conflict in degrees: the vestibular heading is offset
        /// by +Delta/2 and the visual heading - both visual fields - by -Delta/2.
        /// Only applied when a visual field and the platform are both present -
        /// ignored otherwise, whatever it says - and 0 means they agree.
        ///
        /// Conditions come in pairs of opposite sign (+6 and -6) so that the
        /// participant's personal bias cancels: the weighting effect flips sign
        /// between the two and the bias does not, so the difference of the two points
        /// of subjective equality isolates the weighting, and their mean gives the
        /// bias. One sign alone cannot separate them. See README.md.
        public float Delta { get; set; } = 0f;

        /// The single visual flag from before there was an inside and an outside.
        /// Not used: a config that still has it is refused, rather than guessed at -
        /// it could mean either field. Null, and left out of the file, otherwise.
        [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
        public bool? HasVisual { get; set; }

        /// Either visual field.
        [JsonIgnore]
        public bool HasAnyVisual => HasVisualInside || HasVisualOutside;

        /// Human-readable name for logs and the UI tab, in the design's own terms:
        /// vis or comb (visual alone, or with the platform), then per, cen or both,
        /// or ves for the platform alone. E.g. "comb cen 65% delta=+6", or with
        /// the real mirror on the screen, "vis cen (mirror) 100%".
        public string Label
        {
            get
            {
                string field = HasVisualInside && HasVisualOutside ? "both"
                    : HasVisualInside ? "cen"
                    : HasVisualOutside ? "per"
                    : null;

                if (field == null)
                    return HasVestibular ? "ves" : "empty";

                string label = $"{(HasVestibular ? "comb" : "vis")} {field}";

                if (HasVisualInside && InsideView == InsideView.Mirror)
                    label += " (mirror)";

                label += $" {Coherence * 100f:F0}%";

                if (HasVestibular)
                    label += $" delta={(Delta >= 0f ? "+" : "")}{Delta:0.#}";

                return label;
            }
        }
    }
}
