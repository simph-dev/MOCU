# MirrorTwoIntervalExperiment

Two-interval distance discrimination in MirrorExperiment's car. The trial structure
comes from `RaceExperiment` and everything seen comes from `MirrorExperiment`: the
star torus, the screen in front of the participant, the fixation point.

The environment classes are used as they are, aliased in one by one (`StarField`,
`RearView`, `FixationPoint`, `KeyboardHead`, and the settings classes that go with
them). Everything else is separate: its own namespace, config file and data folder.
The one change this experiment needed in MirrorExperiment is a third screen view,
`InsideView.Forward` (see *Center and AntiCenter*).

**Which experiment runs** is decided in two files, which must agree, as for the
others: the `EnsureComponent<…ExperimentHandler>()` line in `Bootstrap.cs`, and the
`using` at the top of `GeneralScript.cs`.

---

## A trial

```
start    platform at MoogStartPosition (surge -0.12), empty screen with its fixation point
         participant presses Start
1st      1 s forward: e.g. Vestibular, Reference, 8 cm
pause    1 s: empty screen with its fixation point; camera back to the centre of the torus
2nd      1 s forward from where the platform stopped: e.g. Combined, Test, 5.4 cm
answer   which went further? Down = first, Up = second; Left and Right do nothing
return   platform back to MoogStartPosition, 4 s
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
- **The torus's hole** isn't a setting. It is worked out from the furthest corner of
  the screen, plus the longest single interval, plus one star: `0.67 + 0.16 + 0.01 ≈
  0.84 m` with the defaults. So no star ever comes between the eye and the screen.
  `StarField.TorusWidth` sets how far the stars reach beyond the hole.
- **During the pause** the screen and its frame stay up with the fixation point, and
  there are no stars anywhere.

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
   surface 0.66 m away. Seen directly, they sit at their real depth, from 0.8 m
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
- Data: `Documents/MocuM2I/<session>/config.json` plus `trials.jsonl`, one line per
  trial, mirrored into a hidden `MocuM2I_Vault` in the user profile. Each trial records
  both intervals: kind, role, cues, distance and measured duration. It also records
  polarity, staircase, level, multiplier, both answers and the response time.

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
