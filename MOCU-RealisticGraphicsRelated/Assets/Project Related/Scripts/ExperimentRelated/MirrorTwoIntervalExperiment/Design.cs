using InsideView = MirrorExperiment.InsideView;


namespace MirrorTwoIntervalExperiment
{
    /// Which of the three experiments runs. Each one is a run of its own: every
    /// trial pairs a Vestibular interval with a Combined one, and the design says
    /// what the two are made of.
    ///
    ///     Center                  ves         vs   ves + cen          screen looks forward
    ///     AntiCenter              ves         vs   ves + anti         screen looks back
    ///     PeripheralAntiCenter    ves + per   vs   ves + per + anti   screen looks back
    ///     Custom                  Parameters.CustomVestibular vs Parameters.CustomCombined
    public enum Design
    {
        Center,
        AntiCenter,
        PeripheralAntiCenter,
        Custom
    }

    public static class DesignCues
    {
        public static CueSet Vestibular(Parameters parameters) => parameters.Design switch
        {
            Design.PeripheralAntiCenter => new CueSet { HasVestibular = true, HasVisualOutside = true },
            Design.Custom => parameters.CustomVestibular,
            _ => new CueSet { HasVestibular = true }
        };

        public static CueSet Combined(Parameters parameters) => parameters.Design switch
        {
            Design.Center => new CueSet { HasVestibular = true, HasVisualInside = true, InsideView = InsideView.Forward },
            Design.AntiCenter => new CueSet { HasVestibular = true, HasVisualInside = true, InsideView = InsideView.Screen },
            Design.PeripheralAntiCenter => new CueSet { HasVestibular = true, HasVisualOutside = true, HasVisualInside = true, InsideView = InsideView.Screen },
            _ => parameters.CustomCombined
        };
    }
}
