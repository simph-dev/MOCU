namespace MoogModule
{
    public enum EncodedMachineState : uint
    {
        Initializing    = 0b0000,   // Motion control software is initializing is operational parameters and internal states.
        Ready           = 0b0001,   // System is ready to accept an Engage command.
        Standby         = 0b0010,   // System is Engaging.
        Engaged         = 0b0011,   // System is Engaged and can accept commanded set-points.
        Parking         = 0b0100,   // System is Disengaging.
        Fault1          = 0b1000,   // Class 1 fault has occurred and the system is waiting for it to clear.
        Fault2          = 0b1001,   // Class 2 fault has occurred and the system requires a Reset to clear its fault condition.
        Fault2Hold      = 0b1010,   // Class 2 fault has occurred and the system is waiting for it to clear.
        Disabled        = 0b1011,   // Unused
        Inhibited       = 0b1100,   // Unused
        Frozen          = 0b1101,   // System is Engaged, but motion is currently frozen.
    }
}