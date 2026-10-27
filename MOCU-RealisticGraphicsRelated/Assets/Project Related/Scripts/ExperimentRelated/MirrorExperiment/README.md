# MirrorExperiment

Heading discrimination in a car: the platform and the optic flow both drive forward,
and a screen in front of the participant shows the same motion from the back of the
car, as a parking display does.

Based on Yakubovich, Israeli-Korn, Halperin, Yahalom, Hassin-Baer & Zaidel (2020),
*Visual self-motion cues are impaired yet overweighted during visual-vestibular
integration in Parkinson's disease*, Brain Communications 2(1): fcaa035.

Built on the same infrastructure as `RaceExperiment` but with a different trial
structure. Nothing here touches Race — separate namespace, separate classes,
separate data folder. `TemporalSound` is the one type genuinely shared: it lives in
`RaceExperiment`, is aliased in, and is used read-only.

Race, Mirror and MirrorTwoInterval each have a class called `ExperimentHandler`,
distinguished only by namespace. Exactly one may be active at a time, since they
compete for the same UI buttons and `InputHandler` events. **Two files decide which
one runs and they must agree:**

- `Bootstrap.cs` — uncomment one `EnsureComponent<…ExperimentHandler>()` line, comment
  the others. The names are fully qualified there because all are in scope.
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

With the rear-view screen the visual cue comes in two fields, each switched on or off
per condition next to `HasVestibular`: `HasVisualInside` (cen), the stars on the screen
in the centre, and `HasVisualOutside` (per), the stars seen directly round it, in the
periphery. That makes three cues — three unimodal, three bimodal and one trimodal
combination — and the default conditions are exactly those seven:

```
vis per    vis cen    vis both    ves    comb per    comb cen    comb both
```

All at 100% coherence and Δ = 0 by default; set each condition's `Coherence` and `Delta`
in the config. See *Rear view*.

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

Worked example — `Heading = +16`, `Δ = +6`, the default transforms (both left alone):

```
Heading                     = +16     staircase
VisualHeading               = +13     16 - 6/2
VestibularHeading           = +19     16 + 6/2
PhysicalVisualHeading       = +13     the camera, and so both visual fields
PhysicalVestibularHeading   = +19     the platform
```

Used by:

- `PhysicalVisualHeading` → camera trajectory. There is one camera trajectory, and the
  screen's cameras ride with the car, so the field outside and the field on the
  screen both go at +13 — outside the flow comes out of a point 13° to the right, on
  the screen it sinks into one 13° to the left (see *Which way is left*).
- `PhysicalVestibularHeading` → platform trajectory
- `Heading` → correct answer, under the default scoring

Δ is applied only where there are two cues to split, a visual field and the platform.
In a visual-only or vestibular-only condition it is ignored, whatever the condition
says: otherwise it would shift the one cue there is, while the answer is still graded
against the nominal heading.

Angle convention: `Surge = D·cos(θ)`, `Sway = D·sin(θ)`. So 0° is straight ahead,
+90° is straight right, 180° is straight back.

### The transforms

`HeadingTransform` is a rotation plus an optional left/right flip, one per channel.

| Visual | Vestibular | Result |
|---|---|---|
| 0 | 0 | **the default:** camera and platform forward together, as a car drives; the screen shows the same motion from the back of the car |
| 0 | 180 | flow forward, platform back |
| 180 | 0 | visually backward, physically forward |
| 180 + swap | 180 + swap | camera and platform backward together — the earlier, reversing design |

The flip exists because reversing is ambiguous. A plain 180° rotation reverses the
whole velocity vector, so a nominal heading to the right carries you back **and to
the left**. `SwapLeftRight` gets you back-and-to-the-right instead. Both readings
are defensible; the default design reverses nothing and needs neither.

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

## Rear view

`Parameters.RearView`, on by default. The car — the platform, and the camera with it —
drives forward along the heading: `VisualTransform` and `VestibularTransform` are both
`{ Rotation: 0, SwapLeftRight: false }`. The stars seen directly flow as driving
forward does. A flat screen hangs in front of the participant, and what is behind the
car is rendered into it, as a car's parking display shows it: the same motion seen
from the back of the car, so there the scene recedes. No transform has to supply that;
the screen looks back.

There are three ways to fill the screen, chosen per condition (`InsideView`), so one run
can interleave them, each with its own staircase:

| `InsideView` | Cameras | Each eye sees | Head movement |
|---|---|---|---|
| `Screen` (default) | one, at the eye's place looking straight back | the same flat picture, stars the size they are seen directly | changes nothing: a parking display |
| `Mirror` | one per eye, at that eye reflected in the glass | its own picture: depth behind the glass | shifts the picture, as in a real mirror |
| `Forward` | one, at the eye's place looking forward at the screen's centre, not reversed | the same flat picture: a window onto the field ahead | changes nothing - so the window is exact only from the calibrated place. Used by MirrorTwoIntervalExperiment as "Center" |

Cameras, textures and screen are all built in code from the config
(`RearView.cs`). Nothing is added to the scene. Only the active view's cameras
render, so the other costs nothing.

### The stars: a torus

The stars fill a torus lying round the participant at eye height, its axis upright
through the eye's calibrated place: a ring all the way round — ahead for the field seen
directly, behind for the screen — so whatever the heading, the car moves through stars
on every side. Its cross-section is square: a thick-walled tube, as high as it is
thick. Round, the ring would thin out toward its inner edge, squeezing the nearest
stars — the fastest — toward eye level; square, every distance gets the same height of
stars. It has two settings: `StarField.TorusWidth`, the side of the square (1 m by
default), and `StarField.DensityPerCubicMeter` (1250, the paper's).

The hole in the middle is not a setting. It is worked out on every Start, so that no star
ever comes between the eye and the screen:

```
hole radius  =  distance to the screen's furthest corner, frame and all
             +  how far a trial carries the car (Distance × VisualDistanceMultiplier)
             +  one star
```

The torus is made where the car starts each trial, and the car can go that far in any
direction; so wherever it is on its way, every star is still beyond the screen's
corners, whatever the head does inside the car. The screen faces the eye, so its
corners are further than its centre: 0.70 m for the default mirror, 42 × 14 cm at 0.66 m, and
0.84 m with the 13 cm of travel and a 1 cm star.

```
default torus    hole 0.84 m, width 1 m: stars 0.84 to 1.84 m from the axis,
                 from 0.5 m below eye height to 0.5 m above
                 π × (1.84² − 0.84²) × 1 ≈ 8.4 m^3 at 1250 per m^3 ≈ 10500 stars
```

- Made at the start of every trial and left where it is while the car goes through it.
  Nothing is added or taken away on the way, so no star appears or vanishes as the car
  moves; only the noise moves stars about, anywhere within the torus.
- So the stars ahead and behind do change a little over a trial: the car comes closer
  to those ahead and draws away from those behind. That is the price of no star ever
  blinking at an edge.
- Every star faces straight ahead, as in the earlier experiments, and never turns.
  Those ahead and behind are seen face-on; those straight to the sides edge-on, and all
  but vanish. `StarField.StarsFaceCenter: true` turns each star to face the eye's place
  instead, once, where it is put — at the start of the trial, or wherever the noise
  moves it — and it does not turn after that either.
- The nearest stars have the fastest flow, so the hole — and with it the screen's size
  and the travel — is part of the stimulus.

### Inside and outside

One cloud and one camera motion feed both fields: the screen's cameras ride with the
car. So the two always show the same movement — from the back of the car on the
screen, ahead of it outside — exactly as a car's parking display and its windscreen do.
Δ applies to both the same way (−Δ/2), since they are one visual heading.

Each condition picks which of them has stars, through `HasVisualInside` and
`HasVisualOutside`. The stars are drawn on a layer of their own, `Stars` (layer 7 in
Tags and Layers), and the mirror's share of them a second time on another,
`MirrorStars` (layer 11; see *Stars in the mirror*). The headset camera renders only
the first, the mirror's cameras only the second, and each trial switches that one layer
on or off in their culling masks: the headset camera's for outside, the mirror's
cameras' for inside. Any other camera — the Scene view, one recording the scene from
the side — always sees both, the mirror's share on top of the rest.

The screen, its frame and the fixation point sit on another layer, `Cabin` (layer 10):
the inside of the car. The headset camera renders it; the mirror's cameras never do. In
the Mirror view they look straight at the screen, and would otherwise see the screen
they render into, and the fixation point a second time, reflected.

- The screen, its frame and the fixation point stay in every condition, stars in the
  mirror or not. Otherwise the mere presence of the mirror would tell the participant
  which condition this is.
- `HasVisualInside` needs `RearView.Enabled`; Start refuses a condition that asks for one
  without the other.
- `HasVisual`, the single visual flag from before, is refused too. It could mean either
  field, so the config has to say which.

### Stars in the mirror

In the Mirror view the eye sees the stars behind by way of the glass: eye to glass to
star, twice `RearView.Distance` further than a star at the same distance seen directly.
At 0.66 m and with the default torus, the stars seen directly are 0.84 to 1.84 m from
the eye and the ones in the mirror 2.16 to 3.16 m. So in the mirror about four times as
many fall in each degree, and each is about two times smaller. That is a real mirror.
The Screen view has none of it.

`RearView.MirrorViewStarFraction` and `RearView.MirrorViewStarScale` even it out: the
Mirror view shows only that share of the stars, that many times larger. The share is
the first stars of the cloud's array. Their positions are drawn uniformly and
independently and so is every relocation by the noise, so the share is a uniform cloud
of its own, sparser, and still the same stars — they move and flicker with the rest.

```
stars in the mirror as seen directly    MirrorViewStarFraction = 0.25
(roughly, at the geometry above)        MirrorViewStarScale    = 2
```

- Rough on purpose: the ratio depends on the torus and the mirror's distance, and
  changes toward the edges of the glass. The numbers are piloted, like the coherence.
- The flow in the mirror stays slower whatever the numbers: that comes from the
  distance, not from the stars. Stereo depth stays that of a real mirror too.
- The Screen view always shows all the stars at their own size: its camera is at the
  eye, so they already look as stars seen directly do.

### Which way is left

The task is a left/right judgement, so this is not a detail. The screen is always
mirror-reversed, in both views — as a car's rear-view mirror is, and its parking
display too, for the same reason: what is behind the car to its left shows on the left.
For a nominal heading of 16° **to the right**, with the defaults:

```
the car                goes forward-right
seen directly          the flow comes out of a point 16° to the right;
                       stars near the centre drift left, away from it
on the screen          the flow sinks into a point 16° to the left;
                       stars near the centre drift left, into it
```

- Reversed, the two fields agree: stars cross both the same way, the screen's only
  receding where the outside ones come on. Unreversed, they would cross the screen
  the other way — which is why there is no longer a switch for it. The
  `FlipHorizontally` of earlier configs is ignored.
- The point the flow centres on is on opposite sides: the heading's side outside, the
  other side on the screen. Driving forward-right, the scene behind recedes toward
  behind-left, and a mirror shows behind-left on the left.
- `Nominal` and `Visual` scoring both grade the side the car goes to — the right, here.

### Geometry

- **Mirror view: each eye camera is the eye reflected in the glass.** It looks back
  through the glass, and its frustum is cut to the glass exactly — an off-axis
  projection whose near plane is the glass itself, so nothing beyond the glass can
  show in it. A ray from the reflected eye through a point of the glass is the
  reflection of the ray from the real eye to that point, so every point of the
  picture lands where the real reflection would. Things behind look further away
  than through the Screen view — eye to glass to star, not eye to star — and so flow
  more slowly; the direction of travel is the same.
- The eyes are the headset's own, live (`MirrorFollowsHead: true`), or held at the
  calibrated place (`false`): stereo depth either way, parallax only when live.
  Without a headset reporting its eyes — on a laptop — `EyeSeparation` stands in, and
  the log says so once.
- **Screen view: the field of view is derived, not set.** It is the angle the screen
  subtends at the eye — 35.3° × 12.1° for the default mirror, 42 × 14 cm at 66 cm — so the
  screen acts as a window: a star the camera sees 10° off its axis appears 10° off the
  centre of the screen, and a 4° heading sits 4° off centre. Given Unity's default 60°
  lens instead, the same screen would hold a picture 120° wide squeezed into 35.3°: a
  16° heading would sit about 3° off centre, a 4° one under 1°, and the flow would
  crawl. `Magnification` sets that squeeze on purpose, like a convex mirror: at
  0.5 the lens covers about 62° across and every angle on the screen is halved —
  headings included.
- So the screen shows headings out to the edge of its glass, ±16.9° for the default: the point
  the flow on it sinks into sits just inside the edge at the staircase's starting 16°,
  and just off it beyond — 19° with Δ = −6. The flow still shows which way the car
  goes; only the point itself is off the glass. Glass 46 cm wide at 0.66 m would
  hold ±19°.
- The Screen view's camera can be moved (`ScreenCameraOffsetRight/Up/Back`) and tilted
  down (`ScreenCameraPitch`), like a parking camera on a bumper. All zero — at the eye,
  looking straight back — is this experiment's setup, and the only one where angles
  on the screen are true.
- **The screen is fixed to the car, not to the head.** It sits at the eye's calibrated
  place and moves with the stimulus trajectory — the 13 cm of a trial — and with
  nothing else. What the head does can change what the Mirror view shows, as with a
  real mirror, never where the screen is.
- The fixation point is fixed to the car as well, unless told otherwise:
  `FixationPoint.Anchor` is `Car` (straight ahead, the default), `Screen` (on the
  centre of the screen, where a heading straight back shows) or `Head` (follows the
  head's position, as the old scene object did). Only `Head` lets the dot and the
  screen drift apart when the head moves.
- The operator panel's `head:` line says where the eye is relative to its place in
  the car, in centimetres. It changes as the participant leans in the chair while the
  screen and the dot stay put; if it does not change, the headset is tracking its
  rotation only, and the whole scene follows the head about.
- **The Screen view is a picture on a flat surface.** Both eyes get the same image at
  the depth of the screen: no stereo depth behind the glass, no parallax through it.
  If the field inside is to be compared with the field outside — which is stereo, as
  everything in VR — the Mirror view keeps that difference out of the comparison.

### The screen's image

A screen in a car is rarely a plain rectangle, and it comes in a housing.
`RearView.ScreenImage` names a picture of the whole thing — a car's mirror, a parking
display, a photograph of a real one — in a `Resources` folder (no extension). Three
kinds of pixel in it:

```
transparent      nothing there at all: what is behind shows through
green            the glass: where the view behind the car shows
anything else    the thing itself — housing, bezel, frame — drawn as it is
```

Green is any colour clearly greener than it is red or blue: pure green, lime, salad. A
dark housing, a grey bezel or the odd greenish tint in a photograph are not. In
linear colour, a pixel whose green is above both red and blue by 0.7 or more is all
glass, by 0.1 or less all frame, and in between a share of each. That in-between is
there for the edge of the glass: filtering, and further off the texture's smaller
mipmaps, blend the green with the frame along it, and drawn as frame those pixels
made a green haze round the glass. For the same reason the frame's colour has any
green above its red and blue taken out, so a genuinely green part of a housing comes
out grey. Paint the glass a flat green with hard edges; the outline of the
transparent part can be as soft as you like, since it is cut at half.

The image is the screen. Its width is `RearView.ScreenWidth`, and it hangs where
`Distance`, `OffsetRight` and `OffsetUp` put it — those are all that change from one
image to another. Its height follows from its proportions, so it is never stretched,
unless `RearView.ScreenHeight` gives one. Then the image, frame and all, is stretched
to fit, but the view behind stays true, since the cameras take their field of view
from the screen's actual size. There is always an image; for a plain rectangle, use
`ScreenPlainGlass`.

| `ScreenImage` | What it is | Proportions | Glass, of the width | `ScreenWidth` for ±16.9° |
|---|---|---|---|---|
| `ScreenRearMirror` | **the default:** an interior rear-view mirror, long and rounded | 3 : 1 | 95% | 0.42 (the default) |
| `ScreenRearMirrorTall` | the same mirror, taller: same frame and rim, at 2 : 1 | 2 : 1 | 95% | 0.42 |
| `ScreenParkingDisplay` | a parking display: a tablet with a black bezel | 16 : 10 | 92% | 0.44 |
| `ScreenSideMirror` | a door mirror, lopsided | 1000 × 640 | 85% | 0.47 |
| `ScreenPlainFrame` | the old screen: 40 × 20 cm of glass in a 1.5 cm frame | 860 × 460 | 93% | 0.43 |
| `ScreenPlainGlass` | a plain rectangle, all glass | 2 : 1 | 100% | 0.40 |

- The view behind the car spans the whole image, frame included, and the frame covers
  part of it, as a real one does. So angles on the glass stay true right up to the
  frame, and the field of view and the textures go by the whole image's size.
- What decides how wide a heading still shows is the glass, not the image: at 0.66 m
  the glass has to reach 0.66 × tan θ either side of the centre for the point the flow
  sinks into to be on it at θ. The last column puts each image's glass out to ±16.9°,
  where the staircase's 16° just fits; the log says on every Start how far the whole
  image reaches.
- It is drawn the right way round as the participant sees it: only the view in the
  glass is mirror-reversed, not the image.
- The image stays still while the flow moves inside it, so its frame is a stationary
  visual reference — part of the stimulus, not decoration. It is there in every
  condition, stars on the glass or not.
- The hole in the stars goes by the whole image's corners, transparent ones included.
- The fixation point's `Screen` anchor is the image's centre, which need not be the
  glass's.
- An empty name, or one that is not found, refuses Start, like a malformed config.
- A new image: in its import settings, set **Non-Power of 2** to **None** and
  **Compression** to **None**. Left as they come, Unity rounds the image to a power of
  two — 1200 × 400 becomes 1024 × 512 — and blurs the edge between glass and frame. In
  the Editor the screen still gets the right proportions, read from the file itself;
  in a build only the rounded texture is left to go by. The images here are set so.

### It is a different visual stimulus

The flow now covers a 34° × 10° patch instead of the whole field (the default mirror's glass
), it passes through a texture on the way, and in the Screen view it carries no
stereo depth. Visual reliability will not be
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
| `RearView.cs` | The rear view: its screen, cameras and textures, built in code |
| `InsideView.cs` | Mirror or Screen: how the field inside the mirror is shown |
| `KeyboardHead.cs` | Debug: moving the head with the keyboard instead of walking |
| `Resources/RearViewScreen.shader` | The screen, both views: its image, and on the green the view behind for each eye |
| `RearViewSettings.cs` | Its config: the screen's image, size and place, stars in the Mirror view, magnification |
| `Resources/Screen*.png` | Example screen images: a plain frame, an interior mirror, a parking display, a door mirror |
| `FixationPoint.cs` | The fixation dot, built in code |
| `FixationPointSettings.cs`, `FixationAnchor.cs` | Its config: on/off, what it is fixed to, distance, size |
| `Resources/FixationPoint.mat` | Its material: the project's fixation shader, drawn on top of everything |
| `Resources/StarMaterial.mat` | The stars' material |
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
└── RearView          only once the config switches the screen on
```

The scene holds none of it, so none of it can be lost from the scene or disagree
with the config. Losing it is not hypothetical: a custom pass kept in a scene is
dropped if the scene is ever loaded while its class does not compile, and that has
happened once. (`RaceExperiment` still keeps its own objects in the scene.)

What is still needed:

1. `Resources/StarMaterial.mat`: HDRP Unlit, white, **Double-Sided on**, **GPU
   Instancing on**. Double-sided matters — the original disables face culling
   entirely, and the triangles are one-sided. The stars are **not**
   GameObjects: the mesh is built in code and drawn with `Graphics.DrawMeshInstanced`
   in batches of 1023 (its per-call limit).
2. Three layers in Tags and Layers: `Stars` (layer 7), which the stars are drawn on for
   the headset camera; `MirrorStars` (layer 11), which the mirror's share of them is
   drawn on for the mirror's cameras; and `Cabin` (layer 10), for the screen, its frame
   and the fixation point. Without `Stars` there are no stars, without `Cabin` or
   `MirrorStars` no mirror; the log and Start say so.
3. `Resources/RearViewScreen.shader`, the screen in both views. HDRP Unlit samples
   one texture for both eyes and has no second one to tell the glass from the frame,
   so the screen needs its own small shader.
4. Camera near clip **0.05** (the paper clips at 5 cm).
5. In `Bootstrap.cs`, exactly one experiment handler active.

While the app runs, the `Stars` object's Inspector still has the debug toggles —
`AlwaysVisible`, `PreviewCoherence`. They reset with every Play, so a preview cannot
be left switched on into a real session. Its geometry fields are overwritten from
`Parameters.StarField` when the app starts and on every Start, so the preview shows
the configured cloud.

The `MirrorExperiment` object itself has one more, `KeyboardHead.Active`: the head moves
with W/S/A/D (along the view, kept level) and E/Q (up, down), Shift five times
faster, and turns with the right mouse button held, for a laptop. The headset's own
position is ignored meanwhile, its rotation still counts. The car — mirror, clear
zone, a fixation point fixed to the body or the mirror — stays put, so this is the
same as a participant moving their head, without walking round the room to check
the mirror's parallax. It resets with every Play as well; switching it off puts the
head back at its calibrated place.

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
in the file but out of the run. JSON has no comments, so this is how the seven
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
- **Cloud shape.** The paper's cloud was a 130 × 130 × 100 cm box ahead of the
  participant, cut by far and near clip planes at 100 and 5 cm. Here it is a torus of
  square cross-section all the way round them, with no clip planes of its own (see
  *The stars: a torus*). Its stars face straight ahead, as the original's did.
- **Camera sampling rate.** The original stepped the camera one trajectory sample per
  60 Hz frame. Here it follows wall-clock time at the display rate, so the coherent
  flow is smoother. Same physical trajectory, finer sampling.

## Open questions

- **Cloud centring.** The torus is centred on the eye's calibrated place. Fixed for
  everyone, or derived per subject from the head and eye offsets? On the eye or on the
  cyclopean head centre?
- **Δ in the combined conditions.** The seven default conditions have Δ = 0, which
  measures the combined thresholds but not the weights: those need Δ of both signs
  (see *Why Δ has two signs*), so either pairs of combined conditions or a sign drawn
  per trial.
- **Fresnel glare.** White triangles on black produce glare streaks on the current
  headset. Lowering the white level is the safer fix than lifting the black, but any
  luminance change alters visual reliability and therefore needs re-piloting.
