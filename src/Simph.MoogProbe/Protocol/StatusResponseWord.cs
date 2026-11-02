using System;

namespace Simph.MoogProbe.Protocol
{
    /// <summary>
    /// The Status Response Word the HOST puts in every command message. It is
    /// not a status we read - it is what we tell the MBC to expect from us.
    ///
    /// Bit layout, CDS7330 note 4.3-3:
    ///
    ///   d0 .. d7    Requested Feedback Data - which blocks the MBC should put
    ///               in its Status Response (200) message.
    ///   d8 .. d15   Replay File id, 0..255. Playback mode only.
    ///   d16 .. d31  Update Rate, samples per second, d16 = LSB.
    ///
    /// The Update Rate field is the one that matters here. Rev. F of the manual
    /// defines it as "the expected Host-to-MBC Communication rate (Hz). Valid
    /// Values: 2000 to 10. This value defines the command rate that the MBC will
    /// expect from the Host."
    ///
    /// Rev. C, from the XP machine beside this platform, gives a different range
    /// for the same field: "1000Hz, 500Hz, 250Hz, 125Hz, 100Hz, 50Hz, 40Hz, 25Hz,
    /// 20Hz, 10Hz. Length and DOF modes only operate with 1000Hz." Neither is
    /// quite this machine. The installed firmware is CA90430 5.0.1.9, which sits
    /// between Rev. D (2010, SW R004, the first to cover the MB-E-6DOF/1X00KG
    /// family) and Rev. E (2013, SW R006). Rev. D is not in hand, and Rev. F's
    /// record also says older text had the Update Rate "used only in Motion
    /// Cueing", so in DOF mode this firmware may ignore the field altogether.
    ///
    /// What is measured: the field comes back verbatim for every value tried, and
    /// duplicated packets, which Rev. F requires, are rejected one by one. A clean
    /// 1000 Hz stream declaring 1000 reaches the filter chain sample for sample.
    /// MOCU-MoogControl declares 0x07D00040, which is 2000, while sending 988.8
    /// packets/s, and its samples do not arrive one per frame. Whether the declared
    /// rate or the send rate causes that has not been tested, so the rate stays a
    /// parameter here and never a constant.
    /// </summary>
    public static class StatusResponseWord
    {
        // Requested Feedback Data bits (CDS7330 Rev. F, chapter 4).
        public const uint FeedbackLength     = 0x80;
        public const uint FeedbackDof        = 0x40;
        public const uint FeedbackAccel      = 0x20;
        public const uint FeedbackActuator   = 0x10;
        public const uint FeedbackAlarmList  = 0x08;

        /// <summary>What MOCU-MoogControl has always sent: 2000 Hz declared, DOF feedback.</summary>
        public const uint LegacyMocu = 0x07D00040;

        public static uint Build(int updateRateHz, uint requestedFeedback = FeedbackDof, byte replayFile = 0)
        {
            if (updateRateHz < 10 || updateRateHz > 2000)
                throw new ArgumentOutOfRangeException(nameof(updateRateHz), updateRateHz,
                    "CDS7330 gives the field a range of 2000 down to 10.");

            if (requestedFeedback > 0xFF)
                throw new ArgumentOutOfRangeException(nameof(requestedFeedback), requestedFeedback,
                    "Requested Feedback Data is the low byte.");

            return ((uint)updateRateHz << 16) | ((uint)replayFile << 8) | requestedFeedback;
        }

        public static int UpdateRate(uint word) => (int)(word >> 16);

        public static byte ReplayFile(uint word) => (byte)((word >> 8) & 0xFF);

        public static uint RequestedFeedback(uint word) => word & 0xFF;

        public static string Describe(uint word) =>
            $"0x{word:X8} (update rate {UpdateRate(word)} Hz, feedback 0x{RequestedFeedback(word):X2}, replay file {ReplayFile(word)})";
    }
}
