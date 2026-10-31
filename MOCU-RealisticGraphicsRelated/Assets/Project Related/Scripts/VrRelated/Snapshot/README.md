# SnapshotModule

Pictures of what the participant is shown, for figures. Not tied to any experiment.
`MirrorTwoIntervalExperiment` uses it; any other experiment can do the same.

Each snapshot can hold three pictures, all with the head held exactly straight ahead
and level, wherever the real head is:

| Picture | Camera | What it shows |
|---|---|---|
| `_center` | between the eyes, field of view of its own (`VerticalFieldOfView`, `Width` × `Height`) | the figure: straight ahead in the middle of the picture |
| `_left`, `_right` (off by default) | at that eye's place, with that eye's own projection, at the headset's resolution × `EyeResolutionScale` | what that eye would see |

## How it works

Nothing is copied off the headset, and its tracking is left alone, so the participant
sees nothing happen. Each picture is rendered by a camera of its own, kept out of the
headset and otherwise set up as the headset camera is: same layers, volumes and
post-processing. These cameras draw after every other camera, so a screen showing
another camera's view shows this frame's.

The eye cameras take each eye's place relative to the head, and its projection, from
the XR display (`XRDisplaySubsystem`). These are the same figures the render pipeline
uses for the headset. A headset eye's field of view is off-centre: wider toward the
temple than toward the nose. So in an eye picture, straight ahead is **not** in the
middle, just as it isn't in what that eye is shown. With no XR display running, the
eye pictures are skipped and the log says so.

The cameras are made when `Take` is called and read after the delay. They copy the
headset camera's layers at that moment, and whatever adapts over frames (automatic
exposure, temporal anti-aliasing) has settled by the time they are read.

## Using it

```csharp
_snapshot = someObject.AddComponent<SnapshotModule.HeadsetSnapshot>();

// Every frame, with whatever else is fixed to the head's place:
_snapshot.SetAnchor(eyePlace, Quaternion.identity);   // straight ahead and level

// When pictures are wanted:
_snapshot.Take(settings, headsetCamera, folder, "name", delaySeconds, path => Debug.Log(path));
```

`SnapshotSettings` belongs in the experiment's own config. While one snapshot is
waiting, another `Take` is skipped with a warning.

Not for real runs: rendering these pictures can cost the headset frames.
