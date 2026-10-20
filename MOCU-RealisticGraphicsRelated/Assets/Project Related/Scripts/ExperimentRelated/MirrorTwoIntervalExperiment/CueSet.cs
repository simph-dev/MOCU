using Newtonsoft.Json;

using InsideView = MirrorExperiment.InsideView;


namespace MirrorTwoIntervalExperiment
{
    /// What one interval is made of: the platform, and the two visual fields of
    /// MirrorExperiment - the stars on the screen in the centre, and the stars seen
    /// directly round it, in the periphery.
    ///
    /// Whatever is left out simply is not there: no stars in a field that is off,
    /// no commands to the platform without vestibular. The screen, its frame and
    /// the fixation point stay in every interval, so nothing on the screen tells
    /// the participant which interval this is.
    public class CueSet
    {
        public bool HasVestibular { get; set; } = true;

        /// Stars on the screen. Needs RearView.Enabled.
        public bool HasVisualInside { get; set; } = false;

        /// How the screen shows them: Forward (a window onto the scene ahead -
        /// Center), Screen (one flat picture looking back - AntiCenter) or Mirror
        /// (a picture for each eye, a real mirror). See MirrorExperiment.InsideView.
        public InsideView InsideView { get; set; } = InsideView.Forward;

        /// Stars seen directly, everywhere the screen does not cover.
        public bool HasVisualOutside { get; set; } = false;

        [JsonIgnore]
        public bool HasAnyVisual => HasVisualInside || HasVisualOutside;

        [JsonIgnore]
        public bool IsEmpty => !HasVestibular && !HasAnyVisual;

        /// In the design's own terms: ves, then cen (Forward), anti (Screen) or
        /// mirror for the screen, per for the field seen directly. E.g. "ves+cen+per".
        public string Label
        {
            get
            {
                string label = HasVestibular ? "ves" : "";

                if (HasVisualInside)
                    label += (label.Length > 0 ? "+" : "") + InsideView switch
                    {
                        InsideView.Forward => "cen",
                        InsideView.Screen => "anti",
                        _ => "mirror"
                    };

                if (HasVisualOutside)
                    label += (label.Length > 0 ? "+" : "") + "per";

                return label.Length > 0 ? label : "empty";
            }
        }
    }
}
