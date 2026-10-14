namespace MirrorExperiment
{
    /// The star cloud: a ring lying round the participant at eye height, its axis
    /// upright through the eye's calibrated place, with a square cross-section - a
    /// thick-walled tube, cut as high as it is thick. Stars all the way round:
    /// ahead for the field seen directly, behind for the screen, so that whatever
    /// the heading, the car moves through stars on every side.
    ///
    /// Made at the start of every trial and left where it is while the car moves
    /// through it. Nothing is added to it or taken from it on the way, so no star
    /// appears or vanishes as the car moves; only the noise moves stars about.
    ///
    /// The hole in the middle is not set here. It is worked out from the screen and
    /// the trial's travel (ExperimentHandler.StarHoleRadius), so that no star ever
    /// comes between the eye and the screen, wherever the trial takes the car.
    ///
    /// Plain floats rather than Vector3, because Unity's vector types serialise
    /// badly through Newtonsoft - they carry computed properties like normalized
    /// and magnitude that have no business in a config file.
    public class StarFieldSettings
    {
        /// How thick the ring is, in meters: the side of its square cross-section.
        /// The stars reach this much further out than the hole, and from half of
        /// it below eye height to half of it above, at every distance alike.
        public float TorusWidth { get; set; } = 1.0f;

        /// Stars per cubic meter. 1250 is the paper's 0.00125 per cm^3.
        public float DensityPerCubicMeter { get; set; } = 1250f;

        /// Triangle bounding box in meters. The paper used 0.005 (0.5 cm); that is
        /// only a few pixels on this headset, so the default here is larger.
        /// Star size changes detectability and therefore visual reliability, so any
        /// coherence calibration is tied to the size it was made at.
        public float StarWidth { get; set; } = 0.01f;
        public float StarHeight { get; set; } = 0.01f;

        /// False, as in the earlier experiments: every star faces straight ahead.
        /// Those straight to the sides are then seen edge-on, and all but vanish;
        /// those ahead and behind are seen face-on.
        ///
        /// True: each star is turned to face the eye's place where it is put - at
        /// the start of the trial, or wherever the noise moves it - and does not
        /// turn after that, so as the car moves the stars stay as still as ever.
        public bool StarsFaceCenter { get; set; } = false;
    }
}
