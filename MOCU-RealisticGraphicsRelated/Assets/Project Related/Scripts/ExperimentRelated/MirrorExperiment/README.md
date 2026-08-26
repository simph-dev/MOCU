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

Worked example — `Heading = +16`, `Δ = +6`, default transforms (visual 0°,
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
| 0 | 180 | **the mirror run — the default here** |
| 180 | 0 | reversed mirror: visually backward, physically forward |

The flip exists because reversing is ambiguous. A plain 180° rotation reverses the
whole velocity vector, so a nominal heading to the right carries you back **and to
the left**. `SwapLeftRight` gets you back-and-to-the-right instead. Both readings
are defensible; the default is the plain rotation.

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
| `SessionWriter.cs` | One folder per session, one JSON line per trial |
| `TrialAnswer.cs`, `TrialState.cs` | Enums |

---

## Scene setup

1. One empty GameObject with the `StarField` component. The 2112 triangles are **not**
   GameObjects — the mesh is built in code and drawn with `Graphics.DrawMeshInstanced`
   in batches of 1023 (its per-call limit). No star prefab exists or is needed.
   Its geometry fields in the Inspector only feed the preview: pressing Start
   overwrites them from `Parameters.StarField` in the config.
2. Assign `StarMaterial`: HDRP Unlit, white, **Double-Sided on**, **GPU Instancing on**.
   Double-sided matters — the original disables face culling entirely, and the
   triangles are one-sided.
3. Set the GameObject's layer so the VR camera's culling mask includes it;
   `gameObject.layer` is what gets passed to the draw call.
4. Camera near clip **0.05** (the paper clips at 5 cm).
5. In `Bootstrap.cs`, exactly one experiment handler active.

The GameObject's own transform barely matters: it is only the initial cloud anchor
before the first trial. From then on the cloud is re-anchored to the calibrated
camera rig — not to the live head pose, since the participant is head-supported and
"straight ahead" is a single direction pinned at calibration.

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
  backward through a cloud that is centred *ahead* of the participant. It works
  geometrically over 13 cm, but the centring was chosen for forward motion. Worth
  asking whether that mode is a real case at all before designing around it.
- **Fresnel glare.** White triangles on black produce glare streaks on the current
  headset. Lowering the white level is the safer fix than lifting the black, but any
  luminance change alters visual reliability and therefore needs re-piloting.
