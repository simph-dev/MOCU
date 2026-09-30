using UnityEngine;


namespace MirrorExperiment
{
    /// The rear-view mirror: a flat screen in front of the participant showing what
    /// a second camera sees looking straight back from the eye. With it the scene
    /// can be physically truthful - the camera travels backward together with the
    /// platform - while what the participant looks at still expands like forward
    /// motion, the way a car's mirror does when reversing.
    ///
    /// Plain floats for the same reason as StarFieldSettings.
    ///
    /// See README.md for how this combines with the two heading transforms, and in
    /// particular for which way left and right end up.
    public class MirrorSettings
    {
        /// Off by default, so a config written before the mirror existed runs
        /// exactly as it did.
        ///
        /// Switching it on changes nothing else. The cloud stays where StarField
        /// puts it - the mirror only shows stars if some of them are behind the
        /// participant - and the camera still travels along VisualTransform.
        public bool Enabled { get; set; } = false;

        /// Screen size in meters.
        public float ScreenWidth { get; set; } = 0.40f;
        public float ScreenHeight { get; set; } = 0.20f;

        /// Where the centre of the screen sits relative to the eye's calibrated
        /// place, in meters. The screen is fixed there, like a mirror in a car: it
        /// moves with the stimulus trajectory, never with the head. An offset to the
        /// side or up turns it to face that place, as a real rear-view mirror is
        /// turned toward the driver, so the picture is not skewed. The default
        /// distance is that of the fixation point.
        public float Distance { get; set; } = 0.66f;
        public float OffsetRight { get; set; } = 0f;
        public float OffsetUp { get; set; } = 0f;

        /// The centre of the screen relative to the eye's place, in world axes,
        /// with the distance kept off zero. The one place it is worked out, so
        /// that anything put on the mirror - the fixation point - lands exactly
        /// where the mirror is. A method, so the config file does not get it.
        public Vector3 ScreenCenter() => new Vector3(OffsetRight, OffsetUp, Mathf.Max(0.01f, Distance));

        /// True reverses the picture left to right, as glass does: something behind
        /// the participant's right shoulder shows on the right of the screen.
        /// False shows what they would see had they turned round, so it shows on
        /// the left.
        ///
        /// This decides which side the heading appears on, so it is as much a part
        /// of the stimulus as SwapLeftRight is. The screen shows the nominal visual
        /// heading when this equals VisualTransform.SwapLeftRight.
        public bool FlipHorizontally { get; set; } = true;

        /// 1 makes the screen a window of true angular size: the camera's field of
        /// view is whatever the screen subtends at the eye, so a heading of 4
        /// degrees sits 4 degrees off the centre of the screen. Below 1 the picture
        /// is minified, like a convex mirror: more of the scene fits, every angle on
        /// the screen shrinks by that factor - headings included - and the far edges
        /// of the cloud can come into view.
        public float Magnification { get; set; } = 1f;

        /// Resolution of the texture behind the screen, per degree of visual angle
        /// the screen subtends. Wants to be near the headset's own pixel density:
        /// well below it the stars blur, well above it they shimmer, because the
        /// texture is sampled without mipmaps.
        public float TexturePixelsPerDegree { get; set; } = 30f;

        /// Name of an image in a Resources folder, without the extension, drawn as
        /// a frame round the screen. Empty for no frame. MirrorExperiment/Resources
        /// has "MirrorFrame" as an example.
        ///
        /// The image is stretched over the screen plus FrameWidth on every side, and
        /// the screen covers its middle, so only its outer border shows. Draw it at
        /// the proportions of that outer rectangle; paint black whatever should not
        /// be seen.
        ///
        /// The frame stays put while the flow moves next to it, so it is also a
        /// stationary visual reference - part of the stimulus, not decoration.
        public string FrameTexture { get; set; } = "";

        /// Width of the frame on each side of the screen, in meters.
        public float FrameWidth { get; set; } = 0.015f;
    }
}
