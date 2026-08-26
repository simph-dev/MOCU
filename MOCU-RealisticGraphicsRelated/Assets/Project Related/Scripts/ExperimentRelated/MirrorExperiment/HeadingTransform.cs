namespace MirrorExperiment
{
    /// Turns a nominal, forward-referenced heading into the direction one channel
    /// actually travels. Applied separately to the visual and the vestibular cue,
    /// after the Delta split, so Delta stays defined in the nominal frame.
    ///
    ///     ( 0, false)  follows the nominal heading - the plain task from the paper
    ///     (180, false) reversed: a nominal heading to the right travels back-left,
    ///                  the way a reversed velocity vector points
    ///     (180, true)  reversed but still veering right
    ///     ( 90, false) sideways
    public class HeadingTransform
    {
        /// Degrees added to the heading.
        public float Rotation { get; set; } = 0f;

        /// Negates the heading before the rotation, flipping which side it veers to.
        public bool SwapLeftRight { get; set; } = false;

        public float Apply(float headingDegrees) =>
            (SwapLeftRight ? -headingDegrees : headingDegrees) + Rotation;
    }
}
