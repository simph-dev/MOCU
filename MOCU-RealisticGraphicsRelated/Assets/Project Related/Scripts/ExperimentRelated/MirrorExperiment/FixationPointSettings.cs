namespace MirrorExperiment
{
    /// The dot the participant keeps their eyes on. On in the paper, in every
    /// condition. It is there to hold the eyes still: eyes that follow the stars add
    /// their own rotation to the flow on the retina, and that shifts the perceived
    /// heading. A mirror gives the gaze somewhere to rest, but not a point to hold.
    ///
    /// Built in code (FixationPoint.cs), like the mirror - nothing in the scene.
    public class FixationPointSettings
    {
        public bool Enabled { get; set; } = true;

        /// What the dot is fixed to: Body, Mirror or Head. See FixationAnchor.
        public FixationAnchor Anchor { get; set; } = FixationAnchor.Body;

        /// How far straight ahead of the eye the dot hangs, in meters. Used by Body
        /// and Head; at Mirror the dot sits on the centre of the screen instead.
        public float Distance { get; set; } = 0.66f;

        /// Diameter in meters. 1 cm at 0.66 m is 0.87 degrees.
        public float Diameter { get; set; } = 0.01f;
    }
}
