# Simph.MoogProbe

A standalone diagnostic host for the Moog MB-E-6DOF/12/1000. It streams a movement
to the platform, records exactly what went out and what came back, and reports
what the platform really did.

It is separate from `MOCU-MoogControl` on purpose. That daemon is linked into the
Unity build the experiments run from; nothing here touches it.

## What it measures

For every packet, the performance counter time it was *due* on the grid and the
time it actually *left*; for every reply, the time it arrived and the pose in it.
From that: the real send rate, the gap distribution, lateness against the grid,
dropped slots, the machine state over the run, and for each movement its bend,
peak speed and arrival times against the plan.

The pose in the replies is the recorder's `Feedback DOF` to within 0.03 um, row for
row, and it arrives on the host's clock. So a run needs neither the recorder nor
anything copied off the XP machine. The recorder is only needed to see inside the
MBC: `Command DOF Trajectory Filter` shows what went into the filter and what came
out.

## Build and run

From the repository root:

```bash
dotnet build Simph.sln
```

The executable lands outside the source tree, because Unity walks
`src/Simph.Core` as a local package and must not find a built DLL there:

```
build/Simph.MoogProbe/bin/Debug/net8.0/win-x64/Simph.MoogProbe.exe
```

Run it with no arguments for the full option list.

## Order to run things

### 1. Loopback, no machine

Proves the tool works and gives the host's own baseline on this PC. Safe
anywhere.

```bash
dotnet run --project src/Simph.MoogProbe -- --phase listen --seconds 5 --host 127.0.0.1:16386 --mbc 127.0.0.1:16384
```

Expect no responses — nothing is listening. What matters is the send loop:
median gap near 1000 µs and very few dropped slots.

### 2. Listen to the real MBC — cannot move the platform

The MBC answers every command message whatever state it is in, and ignores the
pose in the packet until it is ENGAGED. `--phase listen` never sends Engage, so
this is a read-only measurement of the link.

Three runs, twenty seconds each:

```bash
dotnet run --project src/Simph.MoogProbe -- --phase listen --rate 1000 --declare 1000 --seconds 20
```

```bash
dotnet run --project src/Simph.MoogProbe -- --phase listen --rate 1000 --declare 2000 --seconds 20
```

```bash
dotnet run --project src/Simph.MoogProbe -- --phase listen --rate 1000 --declare 2000 --duplicate --seconds 20
```

The first declares the rate we actually send. The second reproduces what
`MOCU-MoogControl` has always declared. The third adds the duplication CDS7330
Rev. F asks for.

Afterwards, look at `C:\Moog\Log Files\Log_File_<n>.txt` on the MBC for lines
reading `Invalid Host Sequence Count`. A count near zero in the first run and a
count near one per packet in the third would settle, for this firmware, which
scheme it actually wants.

### 3. Engage and hold — moves the platform

Needs `--arm` and a typed `MOVE`. Nobody in the chair.

```bash
dotnet run --project src/Simph.MoogProbe -- --phase hold --rate 1000 --declare 1000 --seconds 10 --arm
```

### 4. Movements

Nobody in the chair. Each of these prints the bend and the arrival times at the
end, measured from the replies.

The stimulus as the experiments send it today, then at its own duration:

```bash
dotnet run --project src/Simph.MoogProbe -- --phase move --duration 0.8 --repeats 3 --arm
```

```bash
dotnet run --project src/Simph.MoogProbe -- --phase move --duration 1.0 --repeats 3 --arm
```

With the trajectory filter inverted, which should take the bend out, and with the
drives led as well, which should put the specified profile on the platform:

```bash
dotnet run --project src/Simph.MoogProbe -- --phase move --duration 1.0 --precompensate --repeats 3 --arm
```

```bash
dotnet run --project src/Simph.MoogProbe -- --phase move --duration 1.0 --precompensate --drive-lag 96 --repeats 3 --arm
```

A compensated command is checked as well as the plan: what it asks of the filter
and how far it overshoots are printed before anything is sent, and it is refused
if the filter could not deliver it.

The difference between a clean stream and the daemon's has one untested cause.
These two separate the declared rate from the send rate:

```bash
dotnet run --project src/Simph.MoogProbe -- --phase move --duration 0.8 --declare 2000 --repeats 3 --arm
```

```bash
dotnet run --project src/Simph.MoogProbe -- --phase move --duration 0.8 --rate 989 --declare 1000 --repeats 3 --arm
```

To see inside the filter, switch on `Recorder_DOF_Trajectory_Filter` under
Recorder > Filter Chain in the MBC's configuration; it applies at once.

## Reading the output

Each run writes a folder under `probe-logs/`:

- `sent.csv` — one row per packet: slot, sequence, due, sent, lateness, gap, pose
- `received.csv` — one row per reply: MBC counter, arrival, gap, state, fault words, pose
- `summary.txt` — the same figures printed to the console, with the movement table

The first send is always a millisecond or two late: it carries the JIT of the
whole send path. It is in `sent.csv` like everything else, but the lateness
figures in the summary start at the second packet.

Two traps in the MBC's own data:

- The `Time` columns in recorder files run about 4% slow against real time, and
  they advance in steps of 0.1 of their own milliseconds. While a stream is
  locked, the row index is the better clock: one row per packet.
- The round trip in the summary pairs replies with commands by order of arrival,
  because replies do not echo our sequence count. It occasionally goes negative,
  so read its distribution, not individual values.

The channels of the DOF filters only fill while the platform is ENGAGED, and
`Command DOF` carries our set-points only then. Anything run from `--phase listen`
shows up in neither.

## Safety

- `--phase listen` cannot move the platform. It is the default.
- `hold` and `move` require `--arm` and a typed confirmation.
- A movement is run past `MotionValidator` before anything is sent, and a plan
  outside the envelope is refused. `--dither` and `--heave` have bounds of their
  own, because no plan describes them.
- Ctrl+C keeps the stream alive, holds the last pose and walks the platform down
  through Disengage. A second Ctrl+C drops the stream, and the MBC then throws a
  Class 1 alarm after `HostCommTimeoutDelay` packets (50, so about 50 ms) and
  disengages on its own terms.
- What the probe sends in each slot is decided by `RunSequencer`, which is covered
  by tests that need no hardware, including one that a listen run never sends
  anything but Null.
