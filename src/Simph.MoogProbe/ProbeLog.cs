using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;

using Simph.Core.Time;

using Simph.MoogProbe.Protocol;

namespace Simph.MoogProbe
{
    /// <summary>
    /// Writes a run to disk and prints the verdict that can be read without
    /// opening anything.
    ///
    /// Three files per run, in a folder named after the start time:
    ///   sent.csv      - one row per packet that left the host
    ///   received.csv  - one row per status response that came back
    ///   summary.txt   - the same figures printed to the console
    ///
    /// All times are seconds from the first send, so they line up with the
    /// recorder files on the MBC, which carry no timestamps and are indexed by
    /// frame.
    /// </summary>
    public sealed class ProbeLog
    {
        private readonly string _directory;
        private readonly string _label;
        private int _datagrams;
        private string? _firstUnparsed;

        public ProbeLog(string root, string label)
        {
            _label = label;
            _directory = Path.Combine(root, $"{DateTime.Now:yyyy-MM-dd_HH-mm-ss}_{label}");
            Directory.CreateDirectory(_directory);
        }

        public string Folder => _directory;

        public void Write(IReadOnlyList<SentRecord> sent, IReadOnlyList<ReceivedRecord> received,
            long slotsDropped, string header, int datagrams = -1, string? firstUnparsed = null)
        {
            _datagrams = datagrams < 0 ? received.Count : datagrams;
            _firstUnparsed = firstUnparsed;

            if (sent.Count == 0)
            {
                Console.WriteLine("Nothing was sent; no log written.");
                return;
            }

            long origin = sent[0].Sent.Nanoseconds;

            WriteSent(sent, origin);
            WriteReceived(received, origin);

            string summary = Summarise(sent, received, slotsDropped, header);
            File.WriteAllText(Path.Combine(_directory, "summary.txt"), summary);
            Console.Write(summary);
            Console.WriteLine($"Log written to {_directory}");
        }

        /// <summary>Adds a section to the summary, on screen and in the file.</summary>
        public void Append(string text)
        {
            if (string.IsNullOrEmpty(text))
                return;

            File.AppendAllText(Path.Combine(_directory, "summary.txt"), text);
            Console.Write(text);
        }

        private void WriteSent(IReadOnlyList<SentRecord> sent, long origin)
        {
            using var writer = new StreamWriter(Path.Combine(_directory, "sent.csv"));
            writer.WriteLine("slot,sequence,duplicate,command,due_s,sent_s,late_us,gap_us,roll,pitch,yaw,surge,sway,heave");

            long previous = 0;

            for (int i = 0; i < sent.Count; i++)
            {
                var r = sent[i];
                double due = Seconds(r.Due.Nanoseconds - origin);
                double at = Seconds(r.Sent.Nanoseconds - origin);
                double late = Micros(r.Sent.Nanoseconds - r.Due.Nanoseconds);
                double gap = i == 0 ? 0 : Micros(r.Sent.Nanoseconds - previous);
                previous = r.Sent.Nanoseconds;

                writer.WriteLine(string.Join(",",
                    N(r.Slot), N(r.Sequence), r.IsDuplicate ? "1" : "0", r.Command.ToString(),
                    F(due, 6), F(at, 6), F(late, 1), F(gap, 1),
                    F(r.Pose.Roll, 7), F(r.Pose.Pitch, 7), F(r.Pose.Yaw, 7),
                    F(r.Pose.Surge, 7), F(r.Pose.Sway, 7), F(r.Pose.Heave, 7)));
            }
        }

        private void WriteReceived(IReadOnlyList<ReceivedRecord> received, long origin)
        {
            using var writer = new StreamWriter(Path.Combine(_directory, "received.csv"));
            // Rotations come last, after the translations, so that every column
            // of the older logs, which have none, keeps its position and one
            // reader handles both. They are needed to check the tilt: a leg
            // model fitted without them takes the platform as level.
            // Leg lengths and actuator data follow for the same reason. Of the
            // ten actuator floats only the first three are written; the summary
            // says whether the other seven were ever non-zero.
            writer.WriteLine("mbc_count,mbc_step,received_s,gap_us,state,status_word,declared_echo,io,fault1,fault2,fault3,bytes,surge,sway,heave,roll,pitch,yaw," +
                "leg_a,leg_b,leg_c,leg_d,leg_e,leg_f," +
                string.Join(",", "abcdef".SelectMany(a => new[] { $"act_{a}0", $"act_{a}1", $"act_{a}2" })));

            long previous = 0;
            uint previousCount = 0;

            for (int i = 0; i < received.Count; i++)
            {
                var r = received[i];
                double at = Seconds(r.Received.Nanoseconds - origin);
                double gap = i == 0 ? 0 : Micros(r.Received.Nanoseconds - previous);
                long step = i == 0 ? 0 : (long)r.Response.MbcPacketCount - previousCount;
                previous = r.Received.Nanoseconds;
                previousCount = r.Response.MbcPacketCount;

                writer.WriteLine(string.Join(",",
                    N(r.Response.MbcPacketCount), N(step), F(at, 6), F(gap, 1),
                    r.Response.State.ToString(),
                    "0x" + r.Response.MachineStatusWord.ToString("X8"),
                    N(r.Response.DeclaredRateEcho),
                    r.Response.Io.ToString().Replace(", ", "+"),
                    "0x" + r.Response.FaultWord1.ToString("X8"),
                    "0x" + r.Response.FaultWord2.ToString("X8"),
                    "0x" + r.Response.FaultWord3.ToString("X8"),
                    N(r.Length),
                    r.HasFeedback ? F(r.Feedback.Surge, 7) : "",
                    r.HasFeedback ? F(r.Feedback.Sway, 7) : "",
                    r.HasFeedback ? F(r.Feedback.Heave, 7) : "",
                    r.HasFeedback ? F(r.Feedback.Roll, 8) : "",
                    r.HasFeedback ? F(r.Feedback.Pitch, 8) : "",
                    r.HasFeedback ? F(r.Feedback.Yaw, 8) : "",
                    Floats(r.Bytes, FeedbackBlock.LegLengths, 6, i => i, 8),
                    Floats(r.Bytes, FeedbackBlock.ActuatorData, 18, i => 10 * (i / 3) + i % 3, 6)));
            }
        }

        /// <summary>
        /// Some values of one feedback block as CSV fields, or as many empty
        /// fields when the reply does not carry the block.
        /// </summary>
        private static string Floats(byte[] bytes, FeedbackBlock block, int count, Func<int, int> index, int digits)
        {
            if (!StatusResponse.TryReadBlock(bytes, block, out var values))
                return new string(',', count - 1);

            return string.Join(",", Enumerable.Range(0, count).Select(i => F(values[index(i)], digits)));
        }

        private string Summarise(IReadOnlyList<SentRecord> sent, IReadOnlyList<ReceivedRecord> received,
            long slotsDropped, string header)
        {
            var text = new StringBuilder();
            text.AppendLine();
            text.AppendLine($"=== {_label} ===");
            text.AppendLine(header);
            text.AppendLine();

            // --- how well the host kept to its own grid ----------------------
            var gaps = new List<double>(sent.Count);
            for (int i = 1; i < sent.Count; i++)
                gaps.Add(Micros(sent[i].Sent.Nanoseconds - sent[i - 1].Sent.Nanoseconds));

            // The first send is always late by a millisecond or two: it carries
            // the JIT of the whole send path and the socket's first write. It is
            // in sent.csv like everything else, but averaging it in would make
            // every run look as though the loop stalls, so the lateness figures
            // below start at the second packet.
            var lateness = sent.Skip(1).Select(r => Micros(r.Sent.Nanoseconds - r.Due.Nanoseconds)).ToList();

            if (lateness.Count == 0)
                lateness.Add(0);

            double span = Seconds(sent[sent.Count - 1].Sent.Nanoseconds - sent[0].Sent.Nanoseconds);

            text.AppendLine("host send loop");
            text.AppendLine($"  packets              {sent.Count} over {span:F3} s = {sent.Count / Math.Max(span, 1e-9):F1} packets/s");
            text.AppendLine($"  slots dropped        {slotsDropped}");

            if (gaps.Count > 0)
            {
                text.AppendLine($"  gap between sends    median {Median(gaps):F1} us, p99 {Percentile(gaps, 0.99):F1} us, max {gaps.Max():F1} us");
                text.AppendLine($"  gaps over 2 ms       {gaps.Count(g => g > 2000)}");
            }

            text.AppendLine($"  late against grid    median {Median(lateness):F1} us, p99 {Percentile(lateness, 0.99):F1} us, max {lateness.Max():F1} us (first send excluded)");
            text.AppendLine();

            // --- what came back ----------------------------------------------
            text.AppendLine("status responses");
            text.AppendLine($"  datagrams arrived    {_datagrams}, of which {received.Count} parsed as message 200");

            if (_firstUnparsed != null)
            {
                text.AppendLine($"  first unparsed       {_firstUnparsed}");
            }

            if (received.Count == 0)
            {
                if (_datagrams == 0)
                {
                    text.AppendLine("  Nothing reached the socket at all. The MBC replies to every command");
                    text.AppendLine("  message whatever state it is in, so this is the link or the listening");
                    text.AppendLine("  side, not the machine state: check that the Motion Base application is");
                    text.AppendLine("  running, and that this program has an inbound firewall Allow rule for");
                    text.AppendLine("  UDP (rules are per executable path, so a rebuild to a new path needs a");
                    text.AppendLine("  new rule).");
                }
                else
                {
                    text.AppendLine("  Datagrams arrived but none was a Status Response. Compare the bytes");
                    text.AppendLine("  above against CDS7330 chapter 4: message id is the fourth word.");
                }

                return text.ToString();
            }

            int commands = sent.Count(r => !r.IsDuplicate);

            text.AppendLine($"  responses            {received.Count} for {commands} commands");

            // The words after the fixed header depend on the feedback requested,
            // so one reply of each size is printed whole, as 32-bit words.
            foreach (var size in received.GroupBy(r => r.Length))
            {
                text.AppendLine($"  {size.Count()} replies of {size.Key} bytes, the first as words:");
                text.AppendLine("    " + Words(size.First().Bytes));
            }

            var actuatorData = received
                .Select(r => StatusResponse.TryReadBlock(r.Bytes, FeedbackBlock.ActuatorData, out var v) ? v : null)
                .Where(v => v != null).ToList();

            if (actuatorData.Count > 0)
            {
                var largest = Enumerable.Range(0, 10).Select(word =>
                    actuatorData.Max(v => Enumerable.Range(0, 6).Max(a => Math.Abs(v![10 * a + word]))));
                text.AppendLine("  actuator data, largest |value| of each of the ten floats over all actuators:");
                text.AppendLine("    " + string.Join(" ", largest.Select(x => x.ToString("G4", CultureInfo.InvariantCulture))));
            }

            var states = received.GroupBy(r => r.Response.State).Select(g => $"{g.Key} x{g.Count()}");
            text.AppendLine($"  states seen          {string.Join(", ", states)}");

            var io = received.GroupBy(r => r.Response.Io).Select(g => $"{g.Key} x{g.Count()}");
            text.AppendLine($"  discrete io          {string.Join(", ", io)}");

            var echoes = received.Select(r => r.Response.DeclaredRateEcho).Distinct().OrderBy(v => v);
            text.AppendLine($"  declared rate echo   {string.Join(", ", echoes)}");

            var faulted = received.Where(r => r.Response.HasFault).ToList();
            text.AppendLine($"  responses in fault   {faulted.Count}");

            if (faulted.Count > 0)
            {
                var first = faulted[0].Response;
                text.AppendLine($"    first fault words  0x{first.FaultWord1:X8} 0x{first.FaultWord2:X8} 0x{first.FaultWord3:X8}");
            }

            // --- the MBC's own packet counter --------------------------------
            // Offset 4 of the reply is the MBC's counter, not an echo of ours.
            // Measured on 2026-11-02 it does not free-run: across two seconds of
            // silence with two packets sent it advanced by exactly 1, so it
            // counts packets the MBC accepted and says nothing about the MBC's
            // internal frame rate.
            //
            // It is still worth printing, because it is a loss detector at the
            // far end of the link: a step of 1 means every packet we sent was
            // taken, a step of 2 means one never got counted, a step of 0 means
            // one was counted twice.
            var steps = new List<double>(received.Count);
            for (int i = 1; i < received.Count; i++)
                steps.Add((long)received[i].Response.MbcPacketCount - received[i - 1].Response.MbcPacketCount);

            if (steps.Count > 0)
            {
                var histogram = steps.GroupBy(s => s).OrderByDescending(g => g.Count()).Take(5)
                    .Select(g => $"{g.Key:F0} x{g.Count()}");

                text.AppendLine();
                text.AppendLine("mbc packet counter (1 per reply = nothing lost at the far end)");
                text.AppendLine($"  step per reply       median {Median(steps):F0}, mean {steps.Average():F3}");
                text.AppendLine($"  commonest steps      {string.Join(", ", histogram)}");
                text.AppendLine($"  not counted          {steps.Count(s => s > 1):F0} packets" +
                    $",  counted twice {steps.Count(s => s < 1)}");
            }

            // --- round trip ---------------------------------------------------
            // Matched by order of arrival, not by sequence: the reply carries the
            // MBC's counter, so there is nothing to match on. Sound only while
            // nothing was lost, hence the count check above.
            if (received.Count == commands)
            {
                var commandTimes = sent.Where(r => !r.IsDuplicate).Select(r => r.Sent.Nanoseconds).ToList();
                var trips = new List<double>(received.Count);

                for (int i = 0; i < received.Count; i++)
                    trips.Add(Micros(received[i].Received.Nanoseconds - commandTimes[i]));

                text.AppendLine();
                text.AppendLine($"round trip (paired by order) median {Median(trips):F1} us, p99 {Percentile(trips, 0.99):F1} us, max {trips.Max():F1} us");
            }

            text.AppendLine();
            return text.ToString();
        }

        private static double Seconds(long nanoseconds) => nanoseconds / 1e9;
        private static double Micros(long nanoseconds) => nanoseconds / 1e3;

        /// <summary>
        /// Each big-endian 32-bit word as hex and, when it reads as a plausible
        /// float, that float too: a leg length or a pose is easier to recognise
        /// as 0.4401 than as 3EE15768.
        /// </summary>
        private static string Words(byte[] bytes)
        {
            var words = new List<string>();

            for (int offset = 0; offset + 4 <= bytes.Length; offset += 4)
            {
                uint word = System.Buffers.Binary.BinaryPrimitives.ReadUInt32BigEndian(bytes.AsSpan(offset, 4));
                float asFloat = BitConverter.Int32BitsToSingle((int)word);
                bool plausible = word != 0 && Math.Abs(asFloat) > 1e-6 && Math.Abs(asFloat) < 1e4;

                words.Add($"{offset}:{word:X8}" + (plausible ? $"({asFloat.ToString("G5", CultureInfo.InvariantCulture)})" : ""));
            }

            return string.Join(" ", words);
        }

        private static double Median(List<double> values) => Percentile(values, 0.5);

        private static double Percentile(List<double> values, double fraction)
        {
            if (values.Count == 0) return 0;

            var sorted = values.OrderBy(v => v).ToList();
            int index = (int)Math.Round(fraction * (sorted.Count - 1));
            return sorted[Math.Clamp(index, 0, sorted.Count - 1)];
        }

        private static string N(long value) => value.ToString(CultureInfo.InvariantCulture);

        private static string F(double value, int decimals) =>
            value.ToString("F" + decimals, CultureInfo.InvariantCulture);
    }
}
