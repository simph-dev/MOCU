namespace MirrorExperiment
{
    /// Geometry of the star cloud. Plain floats rather than Vector3, because Unity's
    /// vector types serialise badly through Newtonsoft - they carry computed
    /// properties like normalized and magnitude that have no business in a config file.
    ///
    /// Defaults are the paper's, except the star size: see README.md.
    public class StarFieldSettings
    {
        /// Cloud dimensions in meters. The paper's 130 x 130 x 100 cm.
        public float VolumeWidth { get; set; } = 1.30f;
        public float VolumeHeight { get; set; } = 1.30f;
        public float VolumeDepth { get; set; } = 1.00f;

        /// How far in front of the participant the cloud is centred, in meters.
        public float DistanceToCloudCenter { get; set; } = 0.66f;

        /// Stars per cubic meter. 1250 is the paper's 0.00125 per cm^3, which over
        /// the default volume works out to 2112 triangles.
        public float DensityPerCubicMeter { get; set; } = 1250f;

        /// Triangle bounding box in meters. The paper used 0.005 (0.5 cm); that is
        /// only a few pixels on this headset, so the default here is larger.
        /// Star size changes detectability and therefore visual reliability, so any
        /// coherence calibration is tied to the size it was made at.
        public float StarWidth { get; set; } = 0.01f;
        public float StarHeight { get; set; } = 0.01f;
    }
}
