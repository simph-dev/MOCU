namespace Simph.MoogProbe.Protocol
{
    /// <summary>
    /// The Motion Command Word, CDS7330 note 4.3-2. Only the first four are used
    /// here; the drawbridge values are listed so the numbering is visibly
    /// complete rather than looking like it stops at Reset by accident.
    /// </summary>
    public enum MotionCommand : uint
    {
        /// <summary>No command update - the normal operating value.</summary>
        Null = 0,

        /// <summary>Valid only in READY. The start pose is the one carried in this packet.</summary>
        Engage = 1,

        /// <summary>Returns the platform to park and removes power from the motors.</summary>
        Disengage = 2,

        /// <summary>Recovers from FAULT and clears INHIBIT.</summary>
        Reset = 3,

        /// <summary>Like Engage, but with a caller-defined start heave.</summary>
        Start = 4,

        DrawbridgeUp = 5,
        DrawbridgeDown = 6,
        DrawbridgeStop = 7
    }
}
