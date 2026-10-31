# MirrorTwoIntervalExperiment

Two-interval distance discrimination in MirrorExperiment's car. The trial structure
comes from `RaceExperiment` and everything seen comes from `MirrorExperiment`: the
star torus, the screen in front of the participant, the fixation point.

The environment classes are used as they are, aliased in one by one (`StarField`,
`RearView`, `FixationPoint`, `KeyboardHead`, and the settings classes that go with
them). Everything else is separate: its own namespace, config file and data folder.
The one change this experiment needed in MirrorExperiment is a third screen view,
`InsideView.Forward` (see *Center and AntiCenter*).

**Which experiment runs** is the config's to say. One exe runs either car experiment;
at startup `ExperimentChoice` reads the config given with `-config`. It looks at its
`"Experiment"` field (`TwoInterval` here, `Heading` for MirrorExperiment), or, in an
older file, its shape. With no `-config`, it runs this one. Changing experiment means
starting the exe again. See the top of MirrorExperiment's README.

---

## A trial

```
start    platform at MoogStartPosition (surge -0.12), empty screen with its fixation point
         participant presses Start
1st      1 s forward: e.g. Vestibular, Reference, 8 cm
pause    1 s: empty screen with its fixation point; camera back to the centre of the torus
2nd      1 s forward from where the platform stopped: e.g. Combined, Test, 5.4 cm
answer   which went further? Down = first, Up = second; Left and Right do nothing
         taken from 0.5 s before the 2nd ends, until 2 s after; the 2nd runs to its end anyway
return   0.5 s after the 2nd ended at the earliest: platform back to MoogStartPosition, 4 s
```

- **Both intervals go straight forward and last the same time.** Only the distance
  differs, and the speed with it.
- **Vestibular:** the platform makes one movement forward, waits, then makes another,
  continuing from where it stopped. It backs up to `MoogStartPosition` first so both
  fit. The longest trial is the Reference plus the longest Test, `0.08 + 0.16 = 0.24 m`
  with the defaults, so from `-0.12` it ends at `+0.12`. Start logs the range.
  For a pause and at the end of the run it comes forward to `MoogNeutralPosition`, so
  Park always starts from there. After Park, a trial won't start until Engage.
- **Visual:** both intervals start from the centre of the torus. The camera jumps back
  there during the pause, while no stars are shown, and every interval gets a freshly
  made torus, so the second can't be compared star by star with the first.
- **The screen** is the taller mirror, `ScreenRearMirrorTall` (2:1, the same frame
  as MirrorExperiment's 3:1 mirror). `RearView.ScreenWidth` is 0.70 m at 0.66 m, so the
  glass reaches 27° either side and the whole image 15° up and down. Its height (0.35
  m) follows from the image: `RearView.ScreenHeight` 0. A height of its own would
  stretch the image, and so make the frame thicker on two sides than on the other two.
  For other proportions, draw an image in those proportions.
- **The torus's hole** is `StarField.HoleRadius`, 1 m, the same as in the heading
  experiment. It must also keep every star beyond the screen: the screen's furthest
  corner, plus the longest single interval, plus one star, `0.77 + 0.16 + 0.01 ≈ 0.94
  m` with the defaults, within the 1 m. Should more be needed, the hole is made that
  large and Start warns, but the run goes ahead. Before 31 Oct 2026 the hole was
  exactly that sum: 0.94–0.97 m in the pilots. `StarField.TorusWidth` sets how far the
  stars reach beyond the hole.
- **During the pause** the screen and its frame stay up with the fixation point, and
  there are no stars anywhere.
- **Answers** are taken from `AnswerWindowBeforeEnd` (0.5 s) before the second interval
  ends, so the participant needn't wait out a movement already told apart. The first
  press counts. The movement still runs to its end, and `TimeToAnswer` (2 s) still runs
  from that end. The platform goes back no sooner than `PauseBeforeReturn` (0.5 s) after
  the end, however early the answer came.

## Head calibration

"Look straight ahead and keep still" is the zero. Calibration takes the head's whole
current rotation as straight ahead and level: **yaw**, **pitch** and **roll** (tilt to
the side). MirrorExperiment takes yaw alone. Before any calibration, level is the
headset's own: gravity.

- **When the program starts** (`GeneralScript`, once the headset is ready), and with
  the rotation hotkey, only the angles matter: until Start, everything shown follows
  the head (see *Before Start*), so the eye's position makes no difference.
- **On Start:** angles and position both. The eye is brought to its place in the car,
  and its gaze becomes the car's straight ahead.

If the program was started with the headset lying on the table, its startup angles are
the table's. Recalibrate with the rotation hotkey once the headset is on, or the
fixation point before Start won't be straight ahead.

Whatever angle the head is held at during calibration becomes straight ahead for the
whole run. If it is held a few degrees off, the visual motion goes that many degrees
off the platform's. For a task about distance rather than direction that doesn't
matter, but calibrate with the head held straight all the same. The info panel's
`calibration:` line shows what was taken as zero.

## Before Start

Until Start, and again once the run is over or stopped, only the fixation point is
shown. It hangs straight ahead of the head and follows it wherever the body goes
(`FixationAnchor.Head`), with the config's distance and size. Start brings on the
screen and moves the dot to where `FixationPoint.Anchor` puts it (`Car` by default).
A pause in the middle of the run keeps the run's look.

## Pause

Pause takes effect at once, wherever the trial is:

- **Before the participant's start signal:** nothing has moved yet, so the pause
  begins straight away.
- **During the intervals, or before an answer has come:** the trial is dropped. It is
  not recorded, the staircase doesn't move, and it runs again after Resume. The stars
  go at once. The platform finishes the move it is making, since it can't be stopped
  halfway, and then goes straight to neutral.
- **Once the answer is in:** the trial is recorded as usual, and the platform goes
  straight to neutral rather than back to the start.

While paused, the platform waits at neutral, and Park and Engage work.

## The three experiments

Every trial pairs a **Vestibular** interval with a **Combined** one. `Parameters.Design`
says what the two are made of; each design is a run of its own.

| `Design` | Vestibular | Combined |
|---|---|---|
| `Center` (default) | ves | ves + cen: the screen looks forward |
| `AntiCenter` | ves | ves + anti: the screen looks back |
| `PeripheralAntiCenter` | ves + per: the stars seen directly round the screen | ves + per + anti: the same, and the screen looks back |
| `Custom` | `CustomVestibular` | `CustomCombined` |

`Custom` takes any two `CueSet`s. Its defaults are ves + per against ves + per + cen:
the third experiment with the screen looking forward instead.

### One shortcut per experiment

The exe takes one optional argument, `-config <path>`: the config file to read instead
of the default one. So each experiment can have a config file and a Windows shortcut of
its own, and nobody has to edit `Design` between runs. Put it in the shortcut's
**Target** after the exe's path:

```
"C:\…\SIMPH MOCU.exe" -config Center.json
"C:\…\SIMPH MOCU.exe" -config AntiCenter.json
"C:\…\SIMPH MOCU.exe" -config PeripheralAntiCenter.json
```

- A relative path is taken from the shortcut's **Start in** folder. With Start in set
  to the shortcut's own folder, these read files lying next to the shortcuts. An
  absolute path works too.
- If the file doesn't exist yet, or is empty, it is written there with the defaults.
  Then set its `Design`, once.
- `-config` with no file after it stops Start with a message, rather than running on
  the default file.
- The info panel shows the config file actually used. Each session's `config.json`
  records the settings, the design included.
- In the Unity Editor there is no argument, so the default file is read.

## Counterbalancing

Every block of 8 trials holds each combination exactly once, shuffled:

- which interval comes first, Vestibular or Combined
- which of the two is the Test, and which the Reference
- whether the Test is longer or shorter than the Reference

`TrialsNumber` must be a multiple of 8.

## Staircase

The Reference is always `ReferenceDistance` (8 cm). The Test is the Reference times
`1 + (BaseDiff − 1) · Ratio^level`, or divided by that, depending on the trial's
polarity: `[2, 1.7, 1.49, 1.34, 1.24, …]` for 2 and 0.7. This is the same shifted
exponential as Race.

A correct answer makes the next trial harder with probability `ChanceToMakeTaskHarder`,
a wrong one makes it easier with `ChanceToMakeTaskEasier`, and a timeout leaves it as
it is. The correct answer is whichever interval physically went further.

`StaircasePerTestInterval`: false (the default, and the design's choice) runs one
staircase for the whole run, as Race does. True runs two, interleaved: one for trials
where the Vestibular interval is the Test, and one for trials where the Combined
interval is.

## Center and AntiCenter

Both are a single flat picture on the screen, the same for both eyes, from a camera at
the eye's calibrated place:

| | Camera looks | Reversed | `InsideView` |
|---|---|---|---|
| Center | forward, at the screen's centre | no | `Forward` |
| AntiCenter | straight back | yes, as a car's mirror or parking display | `Screen` |

### Is Center the same as looking ahead directly?

Geometrically, yes, from the calibrated place. The camera sits at the eye, looks at
the screen's centre (which faces the eye square on), and its field of view is exactly
the angle the screen subtends. So every point of the screen shows the very star that
lies behind it on the same line of sight, at the same angular size. Nothing extra is
added to the distance: the screen is 0.66 m away, but the picture on it is taken from
the eye, not from the screen. Only the Mirror view adds distance, twice the mirror's.

It differs from looking directly in three ways:

1. **No stereo.** Both eyes get the same picture, so the stars look painted on a
   surface 0.66 m away. Seen directly, they sit at their real depth, from 1 m
   outwards.
2. **No parallax.** The camera is fixed to the car, not the head. When the head
   moves, the picture stays put, and the window is no longer exact.
3. **Through a texture.** At `TexturePixelsPerDegree` (30), stars on the screen are a
   little softer than stars seen directly.

So with the stars seen directly round it as well (`Custom`), Center's stars would carry
on seamlessly into the ones round the screen. Only the frame, the flatness and the lack
of parallax would set them apart.

## Config and data

- Config: `Documents/MocuM2I/MirrorTwoIntervalConfig.json`, written with the defaults
  on first run and re-read on every Start. `StarField`, `RearView` and `FixationPoint`
  are MirrorExperiment's settings classes; see its README.
- Data folder: `DataFolder` in the config. Empty means `Documents/MocuM2I`. A relative
  path is taken from the config file's own folder, so `"Data"` keeps the results,
  and the `Snapshots` inside it, next to the config and the `.bat` that runs it. Start
  makes the folder, or refuses with the reason, before the first trial. The info
  panel's `data:` line shows where it is.
- Data: `<data folder>/<session>/config.json` plus `trials.jsonl`, one line per
  trial, mirrored into a hidden `MocuM2I_Vault` in the user profile. Each trial records
  both intervals: kind, role, cues, distance and measured duration. It also records
  polarity, staircase, level, multiplier, both answers and the response time.
  `ResponseTime` is in seconds from the end of the second interval: negative for an
  answer given before the end, null on a timeout. (Sessions from before 25 Oct 2026
  used −1 for a timeout and had no early answers.)

## Snapshots for figures

With `Snapshots.Enabled` true, the middle of each interval with stars is saved by
`SnapshotModule` (`Scripts/VrRelated/Snapshot`, see its README) to
`Documents/MocuM2I/Snapshots/`, as `<time>_<design>_<cues>_center.png`, `_left.png` and
`_right.png`. Only the first of each kind of interval in a run is saved, so one trial
gives the pictures of both its intervals.

- **Rendered by a camera of its own**, not taken off the monitor or the headset. It sits
  at the eye's place in the car and looks straight ahead and level, whatever the real
  head is doing:
  - `center` (on): the figure, from between the eyes. With `HeadsetFieldOfView` (on)
    it covers exactly the field the headset shows, both eyes together, read from the
    headset itself. The log prints it in degrees. `Height` pixels high (2160), as wide
    as that field makes it. Off, it takes `VerticalFieldOfView` at `Width` ×
    `Height`. It is the same perspective an eye has: every object sits at the same
    angle from straight ahead, and has the same angular size. Only the eyes' 3 cm to
    either side differ.
  - `left`, `right` (off, `LeftEye`/`RightEye`): each eye with its own place and its
    own off-centre field of view from the headset. Straight ahead is not in the middle,
    as it isn't in what that eye is shown. Useful to check what an eye is given.
- **For a figure set:** one trial of `PeripheralAntiCenter` gives `ves+per` (periphery
  only) and `ves+per+anti` (both). One trial of `AntiCenter` gives `ves+anti` (screen
  only). `Center` gives `ves+cen`, the screen looking forward.
- **Not for real runs.** Rendering these can cost the headset frames. The view on the
  screen is drawn at `RearView.TexturePixelsPerDegree` (30), so at 4K it is a little
  softer than the stars round it. Raise that setting for the snapshot run if it shows.

## The operator's view

The "Unity view" panel shows one eye of the headset: `OperatorViewEye` at startup
(`Left` by default, or `Right`). Then the **test1** button on the Debug tab switches
between the two while the app runs; Start doesn't reset it. The info panel's
`operator view:` line says which eye it is.

Both eyes are rendered in one pass, into the two layers of one texture. The copy used to
let both write the panel's flat texture, so whichever came last showed. It now picks
one. Either way it is one eye's picture: off-centre, so the fixation point isn't in the
middle. In the left eye it sits right of the middle, in the right eye left of it. If it
moves the other way, or by much more than the gap between the eyes accounts for, look
for something else.

## Files

| File | What |
|---|---|
| `ExperimentHandler.cs` | Scene, platform, trial loop, UI |
| `Experiment.cs` | Trial list, counterbalancing, staircases |
| `Trial.cs` | One trial and its two intervals |
| `CueSet.cs` | What one interval is made of |
| `Design.cs` | The three experiments and their cue sets |
| `Enums.cs` | Interval kind and role, polarity, answers, states |
| `Parameters.cs` / `ParametersFile.cs` | Config and its file |
| `SessionWriter.cs` | Saved data |
