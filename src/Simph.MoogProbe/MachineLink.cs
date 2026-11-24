using System;
using System.Collections.Concurrent;
using System.Net;
using System.Net.Sockets;
using System.Threading;

using Simph.Core.Geometry;
using Simph.Core.Time;

using Simph.MoogProbe.Protocol;

namespace Simph.MoogProbe
{
    /// <summary>One packet as it left the host.</summary>
    public readonly struct SentRecord
    {
        public long Slot { get; }
        public uint Sequence { get; }
        public Timestamp Due { get; }
        public Timestamp Sent { get; }
        public Pose6 Pose { get; }
        public MotionCommand Command { get; }
        public bool IsDuplicate { get; }

        public SentRecord(long slot, uint sequence, Timestamp due, Timestamp sent,
            Pose6 pose, MotionCommand command, bool isDuplicate)
        {
            Slot = slot;
            Sequence = sequence;
            Due = due;
            Sent = sent;
            Pose = pose;
            Command = command;
            IsDuplicate = isDuplicate;
        }
    }


    /// <summary>One status response as it arrived.</summary>
    public readonly struct ReceivedRecord
    {
        public Timestamp Received { get; }
        public StatusResponse Response { get; }
        public Pose6 Feedback { get; }
        public bool HasFeedback { get; }
        public int Length { get; }

        public ReceivedRecord(Timestamp received, StatusResponse response,
            Pose6 feedback, bool hasFeedback, int length)
        {
            Received = received;
            Response = response;
            Feedback = feedback;
            HasFeedback = hasFeedback;
            Length = length;
        }
    }


    /// <summary>
    /// The UDP link to the MBC. Sends synchronously on the calling thread so the
    /// send timestamp is taken as close to the socket as we can get it, and
    /// receives on a thread of its own that does nothing but stamp and enqueue.
    ///
    /// Both directions record into unbounded queues rather than writing to disk
    /// inline. Writing a line of CSV inside a 1 ms loop would be measuring the
    /// file system, not the platform.
    /// </summary>
    public sealed class MachineLink : IDisposable
    {
        private readonly Socket _socket;
        private readonly EndPoint _mbc;
        private readonly byte[] _sendBuffer = new byte[DofCommandMessage.Size];
        private readonly ConcurrentQueue<SentRecord> _sent = new ConcurrentQueue<SentRecord>();
        private readonly ConcurrentQueue<ReceivedRecord> _received = new ConcurrentQueue<ReceivedRecord>();
        private readonly CancellationTokenSource _stopping = new CancellationTokenSource();
        private readonly Thread _receiver;

        private uint _sequence;
        private volatile int _latestState = -1;
        private volatile int _responses;

        // Counted separately from _responses: otherwise "nothing came back" and
        // "something came back that we did not understand" look the same in the
        // summary, and they need different fixes.
        private volatile int _datagrams;
        private byte[]? _firstUnparsed;
        private string? _firstUnparsedFrom;
        private readonly object _unparsedLock = new object();

        private readonly IClock _clock;

        public MachineLink(IPEndPoint host, IPEndPoint mbc, IClock clock)
        {
            _mbc = mbc;
            _clock = clock ?? throw new ArgumentNullException(nameof(clock));

            _socket = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);
            _socket.ReceiveBufferSize = 8 * 1024 * 1024;
            _socket.SendBufferSize = 1024 * 1024;
            _socket.Bind(host);

            _receiver = new Thread(ReceiveLoop)
            {
                IsBackground = true,
                Name = "moog-probe-receive",
                Priority = ThreadPriority.AboveNormal
            };
            _receiver.Start();
        }

        public ConcurrentQueue<SentRecord> Sent => _sent;
        public ConcurrentQueue<ReceivedRecord> Received => _received;

        /// <summary>The last sequence count handed to the MBC.</summary>
        public uint LastSequence => _sequence;

        /// <summary>
        /// The machine state from the most recent response, or null before the
        /// first one arrives. Read from the send loop to drive the engage and
        /// disengage handshakes, so it is deliberately a plain volatile read
        /// rather than anything that could block a 1 ms slot.
        /// </summary>
        public MachineState? LatestState
        {
            get
            {
                int state = _latestState;
                return state < 0 ? (MachineState?)null : (MachineState)state;
            }
        }

        public int ResponseCount => _responses;

        /// <summary>Every datagram that reached the socket, parsed or not.</summary>
        public int DatagramCount => _datagrams;

        /// <summary>
        /// The first datagram that arrived but was not a Status Response, as hex,
        /// with where it came from. Null when everything parsed.
        /// </summary>
        public string? FirstUnparsed
        {
            get
            {
                lock (_unparsedLock)
                {
                    if (_firstUnparsed == null)
                        return null;

                    return $"{_firstUnparsed.Length} bytes from {_firstUnparsedFrom}: " +
                        BitConverter.ToString(_firstUnparsed).Replace('-', ' ');
                }
            }
        }

        /// <summary>
        /// Sends one command. With <paramref name="copies"/> above one the same
        /// bytes go out that many times, sequence count included - the scheme
        /// CDS7330 Rev. F section 4.2.1 asks for. Each copy is recorded
        /// separately so the log shows what actually left the host.
        /// </summary>
        public void Send(long slot, Timestamp due, MotionCommand command,
            uint statusResponseWord, Pose6 pose, int copies = 1)
        {
            if (copies < 1) throw new ArgumentOutOfRangeException(nameof(copies));

            uint sequence = ++_sequence;

            DofCommandMessage.Write(_sendBuffer, sequence, command, statusResponseWord, pose);

            for (int copy = 0; copy < copies; copy++)
            {
                _socket.SendTo(_sendBuffer, _mbc);
                var sent = _clock.Now;

                _sent.Enqueue(new SentRecord(slot, sequence, due, sent, pose, command, copy > 0));
            }
        }

        private void ReceiveLoop()
        {
            var buffer = new byte[2048];
            EndPoint from = new IPEndPoint(IPAddress.Any, 0);

            while (!_stopping.IsCancellationRequested)
            {
                try
                {
                    int length = _socket.ReceiveFrom(buffer, ref from);
                    var at = _clock.Now;

                    _datagrams++;

                    if (!StatusResponse.TryParse(buffer.AsSpan(0, length), out var response))
                    {
                        lock (_unparsedLock)
                        {
                            if (_firstUnparsed == null)
                            {
                                _firstUnparsed = buffer.AsSpan(0, Math.Min(length, 96)).ToArray();
                                _firstUnparsedFrom = from.ToString();
                            }
                        }

                        continue;
                    }

                    bool hasFeedback = StatusResponse.TryReadDofFeedback(
                        buffer.AsSpan(0, length), out var feedback);

                    _latestState = (int)response.State;
                    _responses++;

                    _received.Enqueue(new ReceivedRecord(at, response, feedback, hasFeedback, length));
                }
                catch (SocketException)
                {
                    // The socket was closed under us, or nothing was listening.
                    // Either way the run is over; Dispose reports it.
                    return;
                }
                catch (ObjectDisposedException)
                {
                    return;
                }
            }
        }

        public void Dispose()
        {
            _stopping.Cancel();
            _socket.Close();
            _receiver.Join(TimeSpan.FromSeconds(1));
            _socket.Dispose();
            _stopping.Dispose();
        }
    }
}
