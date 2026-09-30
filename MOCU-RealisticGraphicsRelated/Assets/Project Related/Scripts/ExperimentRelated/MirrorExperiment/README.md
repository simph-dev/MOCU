# MirrorExperiment

Heading discrimination with a mirrored vestibular cue: the optic flow simulates
driving forward while the platform physically travels backward.

Based on Yakubovich, Israeli-Korn, Halperin, Yahalom, Hassin-Baer & Zaidel (2020),
*Visual self-motion cues are impaired yet overweighted during visual-vestibular
integration in Parkinson's disease*, Brain Communications 2(1): fcaa035.

Built on the same infrastructure as `RaceExperiment` but with a different trial
structure. Nothing here touches Race — separate namespace, separate classes,
separate data folder. `TemporalSound` is the one type genuinely shared: it lives in
`RaceExperiment`, is aliased in, and is used read-only.

Both experiments have a class called `ExperimentHandler`, distinguished only by
namespace. Exactly one may be active at a time, since they compete for the same UI
buttons and `InputHandler` events. **Two files decide which one runs and they must
agree:**

- `Bootstrap.cs` — uncomment one `EnsureComponent<…ExperimentHandler>()` line, comment
  the other. The names are fully qualified there because both are in scope.
- `GeneralScript.cs` — its `using` at the top is what resolves the unqualified
  `ExperimentHandler` it calls `CalibrateHeadRotation()` on. If it points at the
  namespace whose handler is *not* registered, `GetComponent` returns null and head
  calibration throws on startup.

---

## What the experiment actually measures

The participant always does the same thing: experiences one second of self-motion
and reports whether it was to the **left or right of straight ahead**. They are
never told which cue to rely on, and they do not know which condition they are in.

Three kinds of condition, interleaved:

| Condition | What happens | What it yields |
|---|---|---|
| Vestibular | Dark, platform moves | vestibular threshold `σ_ves` |
| Visual | Platform still, stars simulate motion | visual threshold `σ_vis` |
| Combined | Both, deliberately disagreeing by Δ | how the two are weighted |

The point is the comparison. From the two unisensory thresholds, Bayes predicts
what the *optimal* weighting would be — a cue deserves weight in proportion to its
reliability, `R = 1/σ²`:

```
w_vis_predicted = R_vis / (R_vis + R_ves)
```

The combined conditions then measure the weighting the participant *actually* used.
Predicted versus observed is the result. The paper's finding, in these terms:
Parkinson's participants had *worse* vision than controls yet gave it *more* weight
than was optimal.

This is why all conditions must run in one session with one participant: the
unisensory measurements are the prediction, the combined ones are the observation,
and thresholds drift between days.

---

## Threshold

Sweep the heading angle from large to small and plot the proportion of "rightward"
responses:

```
 1.0 |                    ,---------
     |                  ,'
 0.5 |- - - - - - - - -+- - - - - -
     |              ,'
 0.0 |____---------'______________
      -16    -4   0   +4      +16   heading, degrees
```

Fit a cumulative Gaussian. Two parameters come out:

- **σ — the threshold.** How shallow the curve is. Small σ means the participant
  resolves small angles. Numerically it is the angle at which they are right about
  84% of the time. Lower is better.
- **μ — the bias.** Where the curve crosses 50%, i.e. the heading that subjectively
  feels like straight ahead. Rarely exactly zero for a real person.

Degrading the visual cue does not make the participant guess outright — it widens
the band of angles over which they are guessing. σ grows.

---

## Coherence, and why it is the noise knob

On every noise tick, a fraction of stars stays put and the rest are teleported to
fresh random positions anywhere in the cloud. That fraction is the coherence.

Two things about this that are easy to get wrong:

1. **The stars never move under their own power.** They are static in world space,
   and all optic flow comes from the camera travelling through them. "Moving
   coherently" just means "not relocated on this frame".
2. **Relocation is permanent.** A star that gets picked is overwritten with a new
   position and stays there. There is no home position to return to. This matches
   `ModifyStarField()` in the original MoogDots OpenGL source, and it is what makes
   the paper's survival argument hold: at 65% coherence the chance a given star
   lives 12 frames is `0.65^12 < 1%`, so the task cannot be solved by tracking one
   star.

Positions are sampled uniformly per axis, with **no minimum-spacing constraint, on
purpose**. An even layout would hand the participant a positional cue and let them
read local geometry instead of integrating motion across many stars.

Coherence was chosen over the alternatives (contrast, density, blur, duration)
because it degrades *only* the direction-of-motion signal. Star count, mean
luminance, contrast and depth structure all stay put. Contrast in particular would
have been a bad choice here: Parkinson's involves known contrast-sensitivity
deficits, so a patient effect could not be separated from a motion-integration one.

### The 65% figure does not transfer

It was not derived; the authors piloted it in their own rig until the visual
threshold roughly matched the vestibular one. Equal reliabilities are wanted because
that is where the measurement is most sensitive — if one cue is far more reliable,
the optimal weight sits near 0 or 1 and deviations from optimality become
undetectable. The target zone is wide: anywhere the weights land between roughly
0.35 and 0.65 is fine.

So the number to preserve is not 65 — it is the *condition* `σ_vis ≈ σ_ves`.
Re-pilot on this rig: measure vestibular once, measure visual at three or four
coherence levels, and interpolate to the crossing. About 60 trials per condition is
enough, since the crossing point is what matters and not each threshold's precision.
Three to five pilot participants, spanning the target age range, then take the
median.

---

## Why Δ has two signs

In combined conditions the cues are split symmetrically: **each is offset by Δ/2**,
the vestibular one way and the visual the other. The nominal heading stays the
midpoint, and it is what the staircase varies and what the correct answer is scored
against by default.

Where the participant's subjective straight-ahead lands reveals the weighting:

| Weighting | PSE at Δ=+6 | PSE at Δ=−6 | Difference |
|---|---|---|---|
| Vision only | +3 | −3 | +6 |
| Equal | 0 | 0 | 0 |
| Vestibular only | −3 | +3 | −6 |

```
w_vis = [μ(+6) − μ(−6)] / (2Δ) + 0.5
```

The two signs exist to cancel the participant's personal bias. With one sign you
measure "weighting effect + bias" and cannot separate them. With both, the weighting
effect flips sign and the bias does not, so the difference removes it — and the mean
of the two hands you the bias for free.

Worked example, a participant with +1° bias and 0.75 weight on vision:

```
μ(+6) = +0.5    μ(−6) = −2.5
difference = 3.0   ->   3/12 + 0.5 = 0.75   (weight recovered)
mean = −1.0        ->   the bias
```

Note also that the PSE can only ever sit between −Δ/2 and +Δ/2, so the diagnostic
region is the band where the two cues straddle zero. The staircase concentrates
trials there by itself; the larger headings are still needed, as they constrain the
slope, which is the combined threshold.

---

## How a heading becomes motion

Three levels, and they are distinct fields on `Trial`:

```
Heading                     nominal, from the staircase - "which trial is this"
  |
  |  split by Delta (combined conditions only)
  v
VisualHeading               = Heading - Delta/2      still forward-referenced
VestibularHeading           = Heading + Delta/2
  |
  |  HeadingTransform, per channel
  v
PhysicalVisualHeading       where the optic flow actually goes
PhysicalVestibularHeading   where the platform actually carries you
```

Worked example — `Heading = +16`, `Δ = +6`, transforms without the swap (visual 0°,
vestibular 180°):

```
Heading                     = +16     staircase
VisualHeading               = +13     16 - 6/2
VestibularHeading           = +19     16 + 6/2
PhysicalVisualHeading       = +13     rotation 0, unchanged
PhysicalVestibularHeading   =  199    19 + 180
```

Used by:

- `PhysicalVisualHeading` → camera trajectory
- `PhysicalVestibularHeading` → platform trajectory
- `Heading` → correct answer, under the default scoring

Angle convention: `Surge = D·cos(θ)`, `Sway = D·sin(θ)`. So 0° is straight ahead,
+90° is straight right, 180° is straight back.

### The transforms

`HeadingTransform` is a rotation plus an optional left/right flip, one per channel.

| Visual | Vestibular | Result |
|---|---|---|
| 0 | 0 | plain heading discrimination, as in the paper |
| 0 | 180 | the mirror run without a mirror: flow forward, platform back |
| 180 | 0 | reversed mirror: visually backward, physically forward |
| 180 + swap | 180 + swap | **the default:** camera and platform together, backward, for the rear-view mirror |

The flip exists because reversing is ambiguous. A plain 180° rotation reverses the
whole velocity vector, so a nominal heading to the right carries you back **and to
the left**. `SwapLeftRight` gets you back-and-to-the-right instead. Both readings
are defensible. The default is the swap, on both channels: a nominal heading to the
right is back-right, which a real mirror shows as right — see *Rear-view mirror*.

Rotations other than 0 and 180 are geometrically possible but probably meaningless:
Δ=±6 was chosen because it is well inside the range where cues still fuse, and a
90° discrepancy is nowhere near it — the percept would split rather than integrate.

### Scoring

`CorrectAnswerBasedOn` picks which heading the answer is graded against:

- `Nominal` (default) — the staircase heading. What the paper does.
- `Vestibular` / `Visual` — the *physical* direction of that channel, for runs where
  the participant is instructed to go by one cue alone.

Right versus left is the **sign of the lateral component**, not of the angle: a
rotated platform at 199° carries the participant to the left even though the angle
is positive. Hence `sin(θ) > 0` rather than `θ > 0` — one formula that is correct
for all three references.

Under `Vestibular` scoring the correct answer flips relative to nominal at headings
below Δ/2, since the vestibular heading crosses zero there. That is the point of the
mode, not a bug, but it looks startling in the logs.

---

## Rear-view mirror

`Parameters.Mirror`, off by default. Without it the "mirror" is only a relation
between the two cues: the flow goes forward, the platform goes backward. With it the
scene can be physically truthful. The camera travels exactly where the platform does
(`VisualTransform` equal to `VestibularTransform`), so whatever is seen directly moves
with the body; the mirror looks backward, and there the same motion shows as flow
expanding — forward, as in a car's mirror when reversing. The reversal comes from the
mirror itself. No transform has to supply it.

A second camera sits at the eye looking straight back and renders into a texture;
the texture is shown on a flat screen in front of the participant. Camera, texture
and screen are all built in code from the config (`RearViewMirror.cs`). Nothing is
added to the scene.

Switching it on changes nothing else. In particular **the cloud stays where
`Parameters.StarField` puts it**, and the mirror only shows the part of it that lies
behind the participant. So place it with the cloud settings:

```
behind only           DistanceToCloudCenter = -0.66
around the head       DistanceToCloudCenter = 0,  VolumeDepth = 2.4
                      seen receding directly and expanding in the mirror
```

Density is per cubic meter, so a bigger cloud means more stars, not sparser ones.

The screen is an object in the scene like any other, so stars closer to the eye than
the screen would be drawn in front of it. `StarField.ClearRadius` prevents that: no
star is drawn within that distance of the eye's calibrated place. The zone is the
inside of a car — the mirror hangs within it, everything that moves is outside — and,
like the mirror, it travels with the stimulus trajectory and not with the head. Keep
it larger than `Mirror.Distance`.

- It acts as a spherical near clip. The cloud stays uniform in the world and the zone
  is only a hole moving through it: a star disappears as the zone reaches it and
  reappears once the zone has passed. Stars in the zone still take part in the noise.
- The nearest stars have the fastest flow, so the radius changes the stimulus.
- The cloud has to reach well past the radius. The default cloud is only 1.3 m wide,
  and a 0.8 m zone round the head would swallow all of it except two caps, ahead
  and behind.

```
around the head, with a cabin    DistanceToCloudCenter = 0
                                 VolumeWidth = VolumeHeight = VolumeDepth = 3
                                 ClearRadius = 0.8
                                 3 x 3 x 3 m at 1250 per m^3 = 33750 stars
```

### Which way is left

A mirror swaps left and right, and the task is a left/right judgement, so this is
not a detail. For a nominal heading **to the right**, with the camera travelling
backward (`VisualTransform.Rotation` at 180):

| `VisualTransform.SwapLeftRight` | Camera travels | `Mirror.FlipHorizontally` | Screen shows |
|---|---|---|---|
| false | back-left | true | forward-left |
| false | back-left | false | forward-right |
| true | back-right | true | forward-right |
| true | back-right | false | forward-left |

- **The screen shows the nominal visual heading when the two flags are equal.** Only
  then does `Nominal` scoring grade what the participant sees.
- `FlipHorizontally: true` is what glass does: reversing back-right, the scene in a
  real mirror expands around a point to the right. `false` is what you would see had
  you turned round.
- `Visual` scoring grades the side the *camera travels* to, which is the side shown
  on the screen only when the picture is flipped. Neither the scoring nor the trial
  record knows about the mirror yet.

So the two coherent setups are:

```
real mirror          Mirror.FlipHorizontally = true
(the defaults)
                     VisualTransform     = { Rotation: 180, SwapLeftRight: true }
                     VestibularTransform = { Rotation: 180, SwapLeftRight: true }
                     body goes back-right, screen shows forward-right

turned-round view    Mirror.FlipHorizontally = false
                     VisualTransform     = { Rotation: 180, SwapLeftRight: false }
                     VestibularTransform = { Rotation: 180, SwapLeftRight: false }
                     body goes back-left, screen shows forward-right
                     (the platform veers as it did before the mirror)
```

### Geometry

- **The field of view is derived, not set.** It is the angle the screen subtends at
  the eye — 33.7° × 17.2° for the default 40 × 20 cm at 66 cm — so the screen acts as
  a window: a star the camera sees 10° off its axis appears 10° off the centre of the
  screen, and a 4° heading sits 4° off centre. Given Unity's default 60° lens instead,
  the same screen would hold a picture about 98° wide squeezed into 33.7°: a 16°
  heading would sit about 4° off centre, a 4° one about 1°, and the flow would crawl.
  `Magnification` sets that squeeze on purpose, like a convex mirror: at 0.5 the lens
  covers about 62° across and every angle on the screen is halved — headings included.
- **It is fixed to the car, not to the head.** Screen and camera sit at the eye's
  calibrated place and move with the stimulus trajectory — the 13 cm of a trial —
  and with nothing else. Turning or moving the head changes neither where the screen
  is nor what it shows: a parking screen. A real mirror would stay put as well, but
  its picture would shift with the head.
- The fixation point is fixed to the car as well, unless told otherwise:
  `FixationPoint.Anchor` is `Body` (straight ahead, the default), `Mirror` (on the
  centre of the screen, where a heading straight back shows) or `Head` (follows the
  head's position, as the old scene object did). Only `Head` lets the dot and the
  mirror drift apart when the head moves.
- **It is a picture on a flat surface, not an optical mirror.** Both eyes get the
  same image at the depth of the screen: no stereo depth behind the glass, no
  parallax through it.

### Frame

Without one the screen is black on black and can only be seen while there are stars
behind. `Mirror.FrameTexture` names an image in a `Resources` folder (no extension;
empty for none), drawn as a frame `Mirror.FrameWidth` meters wide round the screen.
`MirrorExperiment/Resources/MirrorFrame.png` is an example, drawn for the default
screen and frame width.

- The image is stretched over the screen plus the frame width on every side, and the
  screen covers its middle — only the outer border shows. Draw it at the proportions
  of that outer rectangle.
- It is opaque, but black is invisible against the black surround, so any outline is
  possible: paint black whatever should not be seen. The inner edge is always the
  screen's rectangle.
- A name that is not found refuses Start, like a malformed config.
- The frame stays still while the flow moves next to it. That makes it a stationary
  visual reference, which is part of the stimulus, not decoration.

### It is a different visual stimulus

The flow now covers a 34° × 17° patch instead of the whole field (at the default
size), it carries no stereo depth, and it passes through a texture on the way.
Visual reliability will not be
what it was full-field, so the coherence that balances `σ_vis` against `σ_ves` has to
be piloted again with the mirror on, at the screen size finally settled on.

---

## Staircase

One per condition, all interleaved, each converging independently.

```
heading magnitude = StartHeading * HeadingRatio ^ level
```

At `StartHeading = 16`, `HeadingRatio = 0.5` this reproduces the paper's logarithmic
series (16, 8, 4, 2, 1, 0.5, 0.25) and keeps going below it — same shape as
`RaceExperiment.Trial.CalculateMultiplier`, but with no lower bound and no table of
fixed steps.

The rule is the paper's: after a correct answer reduce the magnitude 30% of the time,
after an incorrect one increase it 80% of the time, and leave a timeout alone. This
converges at about 73% correct, which samples the informative part of the curve.

---

## Files

| File | Role |
|---|---|
| `ExperimentHandler.cs` | The orchestrator: trial loop, platform, camera, calibration |
| `Experiment.cs` | Trial generation, interleaving, the per-condition staircases |
| `Trial.cs` | One trial: headings, correct answer, response |
| `ConditionSettings.cs` | One condition: which cues, coherence, Δ |
| `HeadingTransform.cs` | Rotation + flip, applied per channel |
| `CorrectAnswerReference.cs` | What the answer is graded against |
| `Parameters.cs` | Everything configurable |
| `ParametersFile.cs` | Reads the config JSON from the data folder |
| `StarField.cs` | The star cloud: geometry, coherence, rendering |
| `RearViewMirror.cs` | The mirror: rear camera, texture and screen, built in code |
| `MirrorSettings.cs` | Its config: screen size and place, flip, magnification, frame |
| `Resources/MirrorFrame.png` | Example frame image for the default screen |
| `FixationPoint.cs` | The fixation dot, built in code |
| `FixationPointSettings.cs`, `FixationAnchor.cs` | Its config: on/off, what it is fixed to, distance, size |
| `Resources/FixationPoint.mat` | Its material: the project's fixation shader, drawn on top of everything |
| `Resources/StarMaterial.mat` | The stars' material, also the template for the mirror's screen and frame |
| `SessionWriter.cs` | One folder per session, one JSON line per trial |
| `TrialAnswer.cs`, `TrialState.cs` | Enums |

---

## Scene setup

Almost none. Everything the experiment shows is built in code at startup, under a
root object of its own that exists only while the app runs:

```
MirrorExperiment
├── Stars             the StarField
├── EyeMirrorVolume   copies every headset frame to the operator's "Unity view" panel
├── FixationPoint
└── RearViewMirror    only once the config switches the mirror on
```

The scene holds none of it, so none of it can be lost from the scene or disagree
with the config. Losing it is not hypothetical: a custom pass kept in a scene is
dropped if the scene is ever loaded while its class does not compile, and that has
happened once. (`RaceExperiment` still keeps its own objects in the scene.)

What is still needed:

1. `Resources/StarMaterial.mat`: HDRP Unlit, white, **Double-Sided on**, **GPU
   Instancing on**. Double-sided matters — the original disables face culling
   entirely, and the triangles are one-sided. The 2112 triangles are **not**
   GameObjects: the mesh is built in code and drawn with `Graphics.DrawMeshInstanced`
   in batches of 1023 (its per-call limit).
2. The stars are drawn on the headset camera's own layer, so the camera's culling mask
   has to include that layer. A warning in the log says so if it does not.
3. Camera near clip **0.05** (the paper clips at 5 cm).
4. In `Bootstrap.cs`, exactly one experiment handler active.

While the app runs, the `Stars` object's Inspector still has the debug toggles —
`AlwaysVisible`, `PreviewCoherence`. They reset with every Play, so a preview cannot
be left switched on into a real session. Its geometry fields are overwritten from
`Parameters.StarField` when the app starts and on every Start, so the preview shows
the configured cloud.

The cloud is anchored to the calibrated camera rig — not to the live head pose, since
the participant is head-supported and "straight ahead" is a single direction pinned
at calibration.

---

## Data

```
Documents/MocuME/
    MirrorExperimentConfig.json     <- edit this between runs
    2026-08-19_14-32-05/
        config.json                 <- snapshot of the settings used
        trials.jsonl                <- one line per trial, appended
```

Mirrored into a hidden `%UserProfile%/MocuME_Vault/` as an independent backup.

The config is re-read on **every** press of Start, so editing it does not require
restarting Unity. A malformed file refuses to start the experiment rather than
quietly falling back to defaults.

A condition can be parked without deleting it: set its `"Enabled": false` and it stays
in the file but out of the run. JSON has no comments, so this is how the paper's five
conditions stay in the file while piloting runs just one. Condition indices in the
data are counted over the whole list, disabled ones included, so a condition keeps
its index whether or not its neighbours ran.

Trials are appended, not rewritten. Each line is self-contained: it carries the
nested condition, all three levels of heading, the reference the answer was scored
against, and the response. Analysis is one line:

```python
pd.read_json('trials.jsonl', lines=True)
```

---

## Noise rate: the history, because it will come up again

The incoherent stars are relocated at `NoiseUpdateHz`, currently **60 Hz** — the
paper's 16.7 ms frame interval, confirmed with Adam.

The headset renders at 90 Hz and 90 does not divide into 60, so noise ticks land on
alternating 1- and 2-frame gaps (11.1 ms / 22.2 ms, averaging 16.7). `StarField`
accumulates time rather than counting frames, so the mean rate stays correct.

**The original did exactly the same thing.** `MoogCom.cpp` in the old MoogDots tree
alternates a hand-written 22 ms / 11 ms busy-wait for precisely this reason, with a
comment explaining that the Oculus runs at 90. The whole visual pipeline there —
noise, trajectory step, frame submit — hung off that one loop.

What is *not* recoverable: what the loop actually achieved on Windows 7, before the
migration that broke the timers. The upsampling constant was 16.67 (implying 60 Hz)
in the 2018, 2019 and 2023 versions of the file, so 60 Hz was always the intent, but
the detailed per-frame logs that would settle it are gone. If the 2020 data were
collected at 45 Hz, the published 65% was calibrated at a dot lifetime of 63 ms
rather than 47.6 — one more reason not to transplant the number on faith.

`StarField` therefore reports `MeasuredNoiseHz`, counted rather than assumed, and the
UI tab shows asked-versus-measured. This question does not need to be archaeology
twice.

On a 120 Hz headset the whole dilemma disappears: 120 divides into 60 exactly, so the
noise can tick every second frame with no jitter at all.

---

## Known deviations from the paper

- **Star size.** The paper uses 0.5 × 0.5 cm triangles. At 66 cm on this headset that
  is only a few pixels across, so the working default here is 1 cm. Bigger stars are
  easier to see, which shifts visual reliability — so the coherence has to be
  calibrated at whatever size is finally settled on, and the two must be reported
  together.
- **Camera sampling rate.** The original stepped the camera one trajectory sample per
  60 Hz frame. Here it follows wall-clock time at the display rate, so the coherent
  flow is smoother. Same physical trajectory, finer sampling.

## Open questions

- **Far clip.** The logs give `CLIP_PLANES 5 100`, but a 100 cm deep cloud centred at
  66 cm reaches 116 cm from the eye, so the far plane would cut its back off. Ask
  whether that was intentional and whether the planes are measured from the eye.
- **Cloud centring.** Was 66 cm fixed for everyone or derived per subject from the
  head and eye offsets? Centred on the eye or on the cyclopean head centre?
- **Design.** Five interleaved conditions as in the paper, or a single combined
  condition? Configurable either way via `Parameters.Conditions`.
- **Reverse visual.** If the visual transform is ever set to 180 the camera travels
  backward through a cloud that is centred *ahead* of the participant, unless
  `DistanceToCloudCenter` says otherwise. It works geometrically over 13 cm, but the
  centring was chosen for forward motion. With the mirror this is the normal case, so
  the cloud's place wants settling along with the mirror's.
- **Mirror: left and right.** Real mirror or turned-round view, and which way the
  platform veers — see *Which way is left*. Once settled, the on-screen heading
  belongs in the trial record and `Visual` scoring should grade against it.
- **Mirror: what is ahead.** Only the screen, or also stars seen directly and
  receding, as through a windscreen? The second gives a large-field cue that says
  "backward" next to a small one that says "forward". Set by where the cloud is put.
- **Fresnel glare.** White triangles on black produce glare streaks on the current
  headset. Lowering the white level is the safer fix than lifting the black, but any
  luminance change alters visual reliability and therefore needs re-piloting.
