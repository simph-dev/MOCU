namespace MirrorExperiment
{
    /// Which heading the correct answer is scored against.
    ///
    /// Only matters in the combined conditions, where the visual and vestibular
    /// headings are split by Delta. In the unisensory conditions Delta is 0 and all
    /// three options coincide.
    public enum CorrectAnswerReference
    {
        /// The heading the staircase set, i.e. the midpoint between the two cues.
        /// This is the default and what the paper scores against.
        Nominal,

        /// For runs where the participant is instructed to report the direction of
        /// the inertial motion only. At headings smaller than Delta/2 this flips
        /// relative to Nominal, since the vestibular heading crosses zero.
        Vestibular,

        /// The mirror case of Vestibular, for instructing on optic flow only.
        Visual
    }
}
