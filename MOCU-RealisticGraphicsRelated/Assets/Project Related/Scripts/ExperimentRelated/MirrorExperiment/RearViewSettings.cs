using UnityEngine;


namespace MirrorExperiment
{
    /// The rear-view screen: a flat screen in front of the participant showing the
    /// scene behind them, like a car's parking display - or its mirror. The car
    /// drives forward, and on the screen the same motion shows as the scene behind
    /// receding. Always mirror-reversed, as both of those are in a car, so that
    /// stars drift across it the same way as they do outside it.
    ///
    /// Three ways of filling the screen, chosen per condition (InsideView): a parking
    /// display, one picture for both eyes; a real mirror, a picture for each; or
    /// Forward, the parking display's camera turned round to look ahead through the
    /// screen, not reversed - a window. The settings below say which they apply to;
    /// those of the Screen view apply to Forward as well.
    ///
    /// Plain floats for the same reason as StarFieldSettings.
    ///
    /// See README.md for how this combines with the two heading transforms, and in
    /// particular for which way left and right end up.
    public class RearViewSettings
    {
        /// On by default: the default conditions show stars on the screen. Off, no
        /// screen and no camera behind it, and conditions with stars on the screen
        /// (HasVisualInside) are refused. The stars keep their hole either way,
        /// worked out from the screen settings below.
        public bool Enabled { get; set; } = true;

        // ---- the screen, both views ----------------------------------------------------------

        /// What the screen looks like: the name of an image in a Resources folder,
        /// without the extension - a car's mirror, a parking display, anything.
        /// Three kinds of pixel in it:
        ///
        ///     transparent     nothing there at all
        ///     green           the glass: where the view behind the car shows
        ///     anything else   the thing itself - housing, bezel, frame - as drawn
        ///
        /// Green is any colour clearly greener than it is red or blue: pure green,
        /// lime, salad. MirrorExperiment/Resources has a few to choose from - an
        /// interior mirror, the default; a parking display; a door mirror; a plain
        /// rectangle with a frame or without (see README.md). Start refuses an
        /// empty name or one that is not found.
        ///
        /// The image stays still while the flow moves inside it, so its frame is a
        /// stationary visual reference - part of the stimulus, not decoration. It
        /// is there in every condition, stars in it or not: its presence must not
        /// tell the participant which condition this is.
        public string ScreenImage { get; set; } = "ScreenRearMirror";

        /// Width of the screen in meters - of the whole image, frame and all. 0.42
        /// puts the default mirror's glass out to 16.9 degrees either side, at
        /// 0.66 m: what the glass shows is what decides how wide a heading can
        /// still be seen on it (see README.md).
        public float ScreenWidth { get; set; } = 0.42f;

        /// Height of the screen in meters, frame and all. 0, the default: whatever
        /// the image's own proportions give for ScreenWidth, so the image is never
        /// stretched. Above 0 the image is stretched to this height - its frame
        /// along with it - while the view behind stays true: the cameras take
        /// their field of view from the screen's actual size, not the image's.
        public float ScreenHeight { get; set; } = 0f;

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
        /// that anything put on the screen - the fixation point - lands exactly
        /// where the screen is. A method, so the config file does not get it.
        public Vector3 ScreenCenter() => new Vector3(OffsetRight, OffsetUp, Mathf.Max(0.01f, Distance));

        /// How far the furthest corner of the screen can be from the eye's place,
        /// for when there is no screen built to ask (RearView.FurthestCornerDistance
        /// knows the image's height and gives the exact figure). Without it, and
        /// with no ScreenHeight given, the screen is taken to be as high as it is
        /// wide - more than any of the images, so the hole in the stars errs on the
        /// large side. The screen faces that place square on, so it is the distance
        /// to the centre and half the diagonal put together
        /// (ExperimentHandler.StarHoleRadius).
        public float FurthestCornerDistance()
        {
            float halfWidth = ScreenWidth / 2f;
            float halfHeight = (ScreenHeight > 0f ? ScreenHeight : ScreenWidth) / 2f;

            return Mathf.Sqrt(ScreenCenter().sqrMagnitude + halfWidth * halfWidth + halfHeight * halfHeight);
        }

        /// Resolution of the textures behind the screen, per degree of visual angle
        /// the screen subtends. Wants to be near the headset's own pixel density:
        /// well below it the stars blur, well above it they shimmer, because the
        /// textures are sampled without mipmaps.
        public float TexturePixelsPerDegree { get; set; } = 30f;

        // ---- the Mirror view -----------------------------------------------------------------

        /// True: the eyes are where the headset says they are, so the picture in the
        /// mirror shifts as the head moves - a real mirror. False: the eyes are held
        /// at the calibrated place, head straight - depth behind the glass stays,
        /// the parallax goes.
        public bool MirrorFollowsHead { get; set; } = true;

        /// Distance between the eyes in meters, used only when the headset does not
        /// report where its eyes are - without VR, on a laptop. With a headset its
        /// own figure is used, so the participant's real eye distance.
        public float EyeSeparation { get; set; } = 0.064f;

        /// What share of the cloud's stars the Mirror view shows, above 0 and up to
        /// 1, and how many times larger it draws them. 1 and 1 is a real mirror,
        /// which shows the stars behind as they are: the eye sees them by way of
        /// the glass, twice the mirror's distance further away than stars seen
        /// directly, so about four times as many fall in each degree and each is
        /// two times smaller or so. Around 0.25 and 2 even that out for the mirror
        /// at 0.66 m and the default torus (see README.md); the right numbers are a
        /// matter for piloting. Either way the flow in the mirror stays slower:
        /// that comes from the distance, not from the stars.
        ///
        /// The stars shown are a fixed share of the same cloud, so they move and
        /// take part in the noise exactly as all the others do. The Screen view
        /// always shows all of them at their own size: its camera is at the eye,
        /// so they already look there as stars seen directly do.
        public float MirrorViewStarFraction { get; set; } = 1f;
        public float MirrorViewStarScale { get; set; } = 1f;

        // ---- the Screen view, and Forward ----------------------------------------------------

        /// 1 makes the screen a window of true angular size: the camera's field of
        /// view is whatever the screen subtends at the eye, so a heading of 4
        /// degrees sits 4 degrees off the centre of the screen. Below 1 the picture
        /// is minified, like a convex mirror: more of the scene fits, every angle on
        /// the screen shrinks by that factor - headings included - and the far edges
        /// of the cloud can come into view. The Mirror view has no such setting: its
        /// field is whatever the glass shows.
        public float Magnification { get; set; } = 1f;

        /// Where the Screen view's camera sits relative to the eye's calibrated
        /// place, in meters: to the right, up, and back. All zero - the camera at
        /// the eye, looking straight back - is the experiment's setup. Further back,
        /// like a parking camera on a bumper, only makes sense in a scene that has
        /// something there to see; the star cloud would have to be moved to match.
        /// Angles on the screen are true only at zero.
        public float ScreenCameraOffsetRight { get; set; } = 0f;
        public float ScreenCameraOffsetUp { get; set; } = 0f;
        public float ScreenCameraOffsetBack { get; set; } = 0f;

        /// How far the Screen view's camera is tilted down, in degrees, as parking
        /// cameras are. 0 is level. Forward uses the same camera, at the same
        /// place and tilt, aimed at the screen's centre instead of straight back.
        public float ScreenCameraPitch { get; set; } = 0f;
    }
}
