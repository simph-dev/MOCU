namespace MirrorExperiment
{
    /// What the fixation point is fixed to.
    public enum FixationAnchor
    {
        /// Straight ahead of the eye's calibrated place, fixed to the car: it moves
        /// with the stimulus trajectory and never with the head - the participant
        /// can lean in the chair and it stays where it is, as the screen does. The
        /// default.
        Car,

        /// On the centre of the rear-view screen - where a heading straight back
        /// shows - and fixed to the car along with the screen. It goes where the
        /// rear-view settings put the screen, whether or not the screen is shown.
        Screen,

        /// Straight ahead of where the head actually is, following it as it moves
        /// but not turning with it. How the dot behaved while it was an object in
        /// the scene with a PositionConstraint on the headset camera.
        Head
    }
}
