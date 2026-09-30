namespace MirrorExperiment
{
    /// What the fixation point is fixed to.
    public enum FixationAnchor
    {
        /// Straight ahead of the eye's calibrated place, fixed to the car: it moves
        /// with the stimulus trajectory and never with the head. From the chair it
        /// is simply always in front of the body. The default.
        Body,

        /// On the centre of the mirror screen - where a heading straight back
        /// shows - and fixed to the car along with the mirror. It goes where the
        /// mirror settings put the screen, whether or not the mirror is shown.
        Mirror,

        /// Straight ahead of where the head actually is, following it as it moves
        /// but not turning with it. How the dot behaved while it was an object in
        /// the scene with a PositionConstraint on the headset camera.
        Head
    }
}
