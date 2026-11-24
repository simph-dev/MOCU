# MOCU / Simph

Software for Prof. Adam Zaidel's lab (Bar-Ilan): a Moog 6-DOF motion platform with a
person in the chair, a VR headset, a response pad, run from Unity. The old system
(MOCU v1: Unity plus standalone daemons) runs the lab's experiments today. A new core
is being grown beside it on branch `NewCore`. Architecture decisions and their status,
if present locally: `Documentation/Internal (not tracked)/architecture-review-brief.md`.

The repository is public and serves as the developer's portfolio. Keep personal notes,
local paths, participant data and vendor documents out of tracked files; working notes
go to `Documentation/Internal (not tracked)/`.

## Working with the developer

- Reply in Russian. Code, comments, commit messages, READMEs and docs stay in English.
- Stimulus properties (durations, coherence, cue conflict, head tracking, what the
  participant sees) are decided by Adam, the PI. Present them as questions for him;
  do not change them silently.
- Commit only when asked, in logically split commits. Never push unless asked.
- The live experiments are built from another branch. Nothing on `NewCore` may break
  `MOCU-RealisticGraphicsRelated` or the v1 daemons it uses.

## Layout and commands

- `src/Simph.Core` - shared logic, no Unity, no I/O, no threads. Compiled twice: by
  Unity as the local package `com.simph.core` (asmdef with `noEngineReferences`) and by
  the .NET SDK for the probe and tests.
- `src/Simph.MoogProbe` - console host that talks to the platform and measures it. Run
  order and safety: `src/Simph.MoogProbe/README.md`.
- `tests/Simph.Core.Tests` - xunit. Build and test from the repo root:
  `dotnet build Simph.sln`, `dotnet test Simph.sln`.
- `MOCU-RealisticGraphicsRelated` - the live Unity project (HDRP, Unity 6000.6.10f1 on
  `NewCore`).
  `MOCU-UnityCore` is an outdated copy; do not edit it.
- `MOCU-MoogControl`, `-AudioControl`, `-ChartViewer`, `-PortFinder` - v1 daemons.
- Old system (C++ and MATLAB): `S:\CODE\OLD SYSTEM - MOOGDOTS`.

## Constraints that fail late

- Code under `src/Simph.Core` must compile as C# 9 in both the .NET SDK (netstandard2.1)
  and Unity (its .NET Framework API profile): no records, no `init`, no file-scoped
  namespaces, no `UnityEngine`. The SDK accepts these; Unity rejects them only when it
  next compiles, which happens when its window gets focus. Unity's own compile is the
  real check.
- Never time anything with `Stopwatch` in Unity: its Mono counts from process start.
  Use `Simph.Core.Time.SystemClock`.
- `src/Directory.Build.props` sends bin/obj to `build/`. A built DLL under `src/` would
  be imported by Unity as a second copy of the assembly.
- Do not edit `Packages/manifest.json` while the Unity Editor is open.
- To check that Unity scripts compile without Unity: copy
  `MOCU-RealisticGraphicsRelated/Assembly-CSharp.csproj` to a temporary name in the same
  folder, `dotnet build` it with OutputPath in the scratchpad, then delete the copy and
  its own `Temp/obj/<name>`. Never delete `Temp/bin` or `Temp/obj` themselves. Unity's
  own errors are in `MOCU-RealisticGraphicsRelated/Logs/Editor.log`.
- Moog's manuals stay out of git (`Documentation/Moog (vendor, not tracked)`).

## Writing code

One developer maintains all of this and should be able to hold it in his head. Code is
written to be read first.

- Prefer plain, explicit code over clever code. Add an abstraction only when a second
  real use exists.
- Small classes with one job; names that say what a thing is, with units where it
  matters (`durationSeconds`, `peakAccelerationMps2`).
- Comments in the code explain why: the constraint, the measurement or the decision
  behind a line. Public types get a short doc comment. Do not restate what the code
  says.
- When you write or change code, say in the reply which design choices you made and
  why, so the developer can learn from them and push back.

## The platform

- A person may be in the chair. Anything that can move the platform (probe `hold` or
  `move`, an engaged daemon) runs only after the developer confirms in the current
  conversation that the MBC is up and the chair and room are empty. Probe `listen` is
  read-only and safe.
- Test a safety claim before stating it.

## Evidence

- Name a cause only after changing that one variable against a control, and measure the
  effect at the platform, not at an intermediate channel.
- Say "all" only after checking all. Keep "measured", "predicted by the model" and
  "assumed" apart in reports.

## Communicating

Write for an intelligent but low-context reader: the developer has not seen your tool
calls or the files you opened and may be coming back from other work. Put the
conclusion first, then the details. Use plain words and full sentences; do not use
terms or abbreviations you made up during the work. Keep a closing summary
self-contained: what you found, what you changed, how it was verified, and what is
left.
