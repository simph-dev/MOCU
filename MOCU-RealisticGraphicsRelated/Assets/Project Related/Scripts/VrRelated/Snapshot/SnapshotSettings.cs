namespace SnapshotModule
{
    /// What HeadsetSnapshot takes, and at what size. Kept in an experiment's own
    /// config; when to take the pictures is the experiment's to decide.
    public class SnapshotSettings
    {
        public bool Enabled { get; set; } = false;

        /// From between the eyes, straight ahead and level, with a field of view of
        /// its own and centred on straight ahead: the figure.
        public bool Center { get; set; } = true;

        /// True: the field the headset shows, both eyes together - read from the
        /// headset itself, so the picture holds exactly what the participant can
        /// see, no more and no less. Its width then follows from Height. False, or
        /// with no headset running: VerticalFieldOfView, at Width x Height.
        public bool HeadsetFieldOfView { get; set; } = true;

        /// In pixels. Width is ignored with HeadsetFieldOfView.
        public int Width { get; set; } = 3840;
        public int Height { get; set; } = 2160;

        /// Vertical, in degrees, when not the headset's. 70 at 16:9 is about 102
        /// horizontally.
        public float VerticalFieldOfView { get; set; } = 70f;

        /// Each eye as the headset renders it - from that eye's place, through its
        /// own off-centre field of view - but with the head held straight ahead and
        /// level. Straight ahead is therefore not in the middle of these pictures,
        /// just as it is not in the middle of what each eye is shown. Off by
        /// default: apart from the crop and the eye's 3 cm to the side they show
        /// what Center does; useful to check what an eye is given.
        public bool LeftEye { get; set; } = false;
        public bool RightEye { get; set; } = false;

        /// Times the headset's own resolution for an eye.
        public float EyeResolutionScale { get; set; } = 1f;
    }
}
