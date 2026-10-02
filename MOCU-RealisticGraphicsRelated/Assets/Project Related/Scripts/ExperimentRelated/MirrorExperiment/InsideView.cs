namespace MirrorExperiment
{
    /// How the field inside the mirror is shown. Chosen per condition, so one run
    /// can interleave both, each with its own staircase.
    public enum InsideView
    {
        /// A real mirror: a camera for each eye, at that eye reflected in the glass,
        /// looking through the glass at the scene behind. Each eye gets its own
        /// picture, so there is depth behind the glass and parallax as the head
        /// moves - the same as for the field seen directly. Always reversed left to
        /// right, as glass is. The default.
        Mirror,

        /// A flat picture from one camera looking back, the same for both eyes and
        /// independent of the head: a parking screen. Reversed or not by
        /// Mirror.FlipHorizontally; its camera can be moved and tilted
        /// (Mirror.ScreenCamera*).
        Screen
    }
}
