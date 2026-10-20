namespace MirrorExperiment
{
    /// How the field inside the mirror is shown. Chosen per condition, so one run
    /// can interleave them, each with its own staircase.
    public enum InsideView
    {
        /// A real mirror: a camera for each eye, at that eye reflected in the glass,
        /// looking through the glass at the scene behind. Each eye gets its own
        /// picture, so there is depth behind the glass and parallax as the head
        /// moves - the same as for the field seen directly.
        Mirror,

        /// A flat picture from one camera looking back, the same for both eyes and
        /// independent of the head: a car's parking display. Its camera sits at the
        /// eye, so the stars on it look the size they do seen directly; it can be
        /// moved and tilted (RearView.ScreenCamera*). The default.
        ///
        /// Both views are mirror-reversed, as a car's mirror and its parking
        /// display both are.
        Screen,

        /// A flat picture from one camera looking forward, through the screen -
        /// a window. Aimed at the screen's centre, and with the field of view the
        /// screen subtends, so from the eye's calibrated place every point of the
        /// screen shows the very star that lies behind it: the same field as seen
        /// directly, only flat - no stereo depth, and no parallax as the head
        /// moves. Not reversed, since nothing is reflected. Moved and tilted by
        /// the same RearView.ScreenCamera* settings as the Screen view.
        Forward
    }
}
