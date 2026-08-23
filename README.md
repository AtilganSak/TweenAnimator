# Tween Animator

A visual timeline-based tween animation editor for Unity, powered by **DOTween**. Design multi-property animations in a custom editor window, preview them by scrubbing in Edit Mode, and trigger playback at runtime with a single API call.

---

## Features

- **Visual timeline editor** — drag, resize, and chain animation blocks on a zoomable timeline
- **Edit Mode preview** — scrub the red playhead to preview animations without entering Play Mode
- **Property picker** — auto-scans your hierarchy and lists all animatable properties
- **Multi-property sequences** — animate position, rotation, scale, color, alpha, font size, and more in one clip
- **Linked start values** — chain entries so one entry's end value feeds the next entry's start value
- **Event markers** — place named time markers on the timeline; fire a C# callback at any exact moment
- **Per-entry events** — `OnStart` / `OnComplete` C# callbacks on individual tween entries
- **Full playback API** — play, pause, resume, stop, rewind, seek, play backward
- **DOTween under the hood** — all eases, loop types, and RotateMode options exposed
- **AnimationClip export** — bake any TweenAnimatorClip into a standard Unity `.anim` file with configurable sample rate and automatic key reduction

---

## Requirements

| Dependency | Version |
|---|---|
| Unity | 2021.3 LTS or later |
| [DOTween](http://dotween.demigiant.com/) | 1.2.745 or later (free or Pro) |
| TextMeshPro | 3.0.6 or later (included with Unity) |

> **DOTween must be installed manually** from the [Asset Store](https://assetstore.unity.com/packages/tools/animation/dotween-hotween-v2-27676) or the [official site](http://dotween.demigiant.com/) before importing this package.

---

## Installation

### Option A — Unity Package Manager (recommended)

1. Open **Window → Package Manager**
2. Click the **+** button → **Add package from git URL…**
3. Enter:
   ```
   https://github.com/AtilganSak/TweenAnimator.git
   ```

### Option B — Manual

1. Clone or download this repository
2. Copy the `Assets/TweenAnimator` folder into your project's `Assets` folder

---

## Quick Start

### 1. Add the Controller

Add a `TweenAnimatorController` component to any GameObject via **Add Component → TweenAnimator → Tween Animator Controller**.

When added, Unity prompts you to save a new **TweenAnimatorClip** asset — this ScriptableObject holds all animation data.

### 2. Open the Editor Window

**Window → Tween Animator** opens the timeline editor.

Select the GameObject with your controller to see its clip in the editor.

### 3. Add Properties

Click **+ Add Property** to open the property picker. It scans the entire hierarchy under the selected GameObject and lists every animatable property. Click any property to add it as a track.

### 4. Create Animation Blocks

Each track shows one animation entry as a colored block on the timeline.

- **Drag** the block body to change delay
- **Drag** left/right edges to resize duration
- **Click** a block to select it and edit its parameters in the inspector panel on the right

### 5. Preview

Click **▶ Preview** (or press the play button in the editor window) to enter preview mode. Drag the red playhead to scrub through the animation in Edit Mode — no need to enter Play Mode.

### 6. Manage Clips (Inspector)

The `TweenAnimatorController` Inspector shows a **Clips** list — each row is a name + `TweenAnimatorClip` asset. Use **+ Add Clip Slot** to add more, **×** to remove, and **Set Active** to pick which one is the *active clip* (its row turns green). The active clip is what the Tween Animator window edits/previews, and what `Play()` plays by default.

Enable **Play On Awake** to auto-play whichever clip is Active as soon as the scene starts.

### 7. Play at Runtime

```csharp
using TweenAnimator;
using UnityEngine;

public class Example : MonoBehaviour
{
    [SerializeField] private TweenAnimatorController _animator;

    void Start()
    {
        _animator.Play();
    }

    void OnButtonClick()
    {
        _animator.PlayBackward();
    }
}
```

---

## Supported Properties

| Component | Properties |
|---|---|
| `Transform` | Local Position, Local Rotation, Local Scale, Position (World) |
| `RectTransform` | Local Position, Local Rotation, Local Scale, Anchored Position, Size Delta |
| `CanvasGroup` | Alpha |
| `Image` | Color, Fill Amount |
| `SpriteRenderer` | Color |
| `TextMeshPro` | Color, Alpha, Font Size, Character Spacing, Word Spacing, Line Spacing |
| `TextMeshProUGUI` | Color, Alpha, Font Size, Character Spacing, Word Spacing, Line Spacing |
| `Light` | Intensity |
| `AudioSource` | Volume, Pitch |

---

## Export to AnimationClip

Any `TweenAnimatorClip` can be baked into a standard Unity `AnimationClip` (`.anim`) for use with the Animator, Timeline, or any tool that consumes Unity animation assets.

### How to export

**Option A — Inspector**

1. Select a `TweenAnimatorClip` asset in the Project window
2. Configure the **Export Settings** at the bottom of the Inspector
3. Click **Convert to AnimationClip** — a Save File dialog opens so you can choose the output folder and file name

**Option B — Context menu**

Right-click a `TweenAnimatorClip` asset → **TweenAnimator → Convert to AnimationClip**

Uses default settings and opens the same Save File dialog.

### Export Settings

| Setting | Default | Description |
|---|---|---|
| **Animation Name** | Clip asset name | Default file name pre-filled in the Save dialog |
| **Samples / Second** | 30 | Keyframes sampled per second of animation. Lower = fewer keys, coarser curves |
| **Reduce Keys** | On | Runs Ramer-Douglas-Peucker simplification after sampling — removes keyframes whose deviation from linear interpolation is within tolerance |
| **Tolerance** | 0.001 | Max allowed value error when removing a keyframe. Invisible at 0.001 for most properties. Raise slightly for color/alpha (0–1 range); lower for large world-space values |

> **Tip — Linear eases:** with Reduce Keys on, a perfectly linear tween collapses to just 2 keyframes regardless of sample rate.

### Limitations

| Case | Behaviour |
|---|---|
| `Use Current As Start = true` | Entry skipped — the live runtime value cannot be baked at editor time. Disable the option and set an explicit start value to include the entry. |
| Infinite loops (`Loops = -1`) | Only the first iteration is baked |
| Material shader properties (`mat_float:` / `mat_color:`) | Not supported — Unity's `AnimationClip.SetCurve` API does not map to shader properties |
| Overlapping entries on the same channel | Both sets of keyframes are written; the later entry's keys win |

### Scripting API

```csharp
using TweenAnimator.Editor;

// Default settings
TweenClipToAnimationClipConverter.ConvertAndSave(tweenClip, ConvertSettings.Default);

// Custom settings — no Save dialog, provide explicit path
var settings = new ConvertSettings
{
    SamplesPerSecond   = 15,
    ReduceKeys         = true,
    ReductionTolerance = 0.005f,
};
AnimationClip anim = TweenClipToAnimationClipConverter.ConvertToAnimationClip(tweenClip, settings);
```

---

## Per-Entry Inspector Options

| Option | Description |
|---|---|
| **Ease** | DOTween ease curve |
| **Loop Type** | Restart, Yoyo, Incremental |
| **Loops** | Number of loops (-1 = infinite) |
| **Speed** | Multiplier applied to duration |
| **Use Current As Start** | Captures the property's live value when the tween starts instead of using the baked start value |
| **Rotate Mode** | *(Rotation properties only)* Fast, FastBeyond360, LocalAxisAdd, WorldAxisAdd |

---

## Runtime API

```csharp
TweenAnimatorController ctrl = GetComponent<TweenAnimatorController>();

// Playback (always plays the active clip)
ctrl.Play();
ctrl.PlayFromTime(0.5f);
ctrl.PlayFromNormalizedTime(0.5f);
ctrl.PlayBackward();
ctrl.Pause();
ctrl.Resume();
ctrl.Stop();
ctrl.Rewind();

// Seek
ctrl.GotoTime(1.2f);
ctrl.GotoNormalizedTime(0.75f);

// State
bool playing   = ctrl.IsPlaying;
bool paused    = ctrl.IsPaused;
bool complete  = ctrl.IsComplete;
float elapsed  = ctrl.CurrentTime;
float duration = ctrl.Duration;
float norm     = ctrl.NormalizedTime;
ctrl.TimeScale = 2f;            // double speed
```

### Multiple Clips

A controller can hold a list of named clips (see [Manage Clips](#6-manage-clips-inspector)). There is no API to hand it a raw `TweenAnimatorClip` at runtime — playback always goes through this list, by index or by name. Every playback method has index/name overloads that switch the active clip first, then play:

```csharp
ctrl.Play(0);                       // play clip at index 0
ctrl.Play("Intro");                 // play clip named "Intro"
ctrl.PlayBackward("Intro");
ctrl.PlayFromTime("Intro", 0.5f);
ctrl.PlayBackwardFromTime(0, 0.5f);
await ctrl.PlayAsync("Intro");

ctrl.SetActiveClip(1);              // switch active clip without playing
ctrl.SetActiveClip("Outro");

int index = ctrl.GetClipIndex("Intro");
TweenAnimatorClip clip = ctrl.GetClip("Intro");
IReadOnlyList<NamedTweenClip> clips = ctrl.Clips;
```

### Per-Entry Events

```csharp
// By display name set in the editor
ctrl["Cube - Fade"].OnComplete += () => Debug.Log("Fade done!");
ctrl["Logo - Scale"].OnStart   += () => Debug.Log("Scale started!");

// By entry ID (GUID, always stable)
ctrl["some-guid-string"].OnComplete += HandleDone;
```

### Event Markers

Place named markers on the timeline via the **Event Markers** track (use the **+** button or right-click the time ruler). Each marker fires a C# event when the playhead passes its time.

```csharp
// Subscribe by name (set in the editor)
ctrl.GetMarker("OnJump").OnTrigger += () => PlayJumpEffect();
ctrl.GetMarker("OnLand").OnTrigger += HandleLand;

// Add a marker at runtime, subscribe, then play
ctrl.AddMarker("OnBoom", 1.5f).OnTrigger += () => Explode();
ctrl.Play();

// Remove a marker by name or markerId
ctrl.RemoveMarker("OnBoom");

// Target a specific clip by index/name without switching the active clip
ctrl.GetMarker("Intro", "OnJump");
ctrl.AddMarker(1, "OnBoom", 1.5f);
ctrl.RemoveMarker("Outro", "OnBoom");
```

> Markers added or removed at runtime take effect on the next `Play()` call.

### Global Events

```csharp
ctrl.OnPlay     += () => Debug.Log("Playing");
ctrl.OnPause    += () => Debug.Log("Paused");
ctrl.OnStop     += () => Debug.Log("Stopped");
ctrl.OnComplete += () => Debug.Log("Finished");
ctrl.OnLoop     += loopIndex => Debug.Log($"Loop {loopIndex}");
```

UnityEvent versions (`OnPlayEvent`, `OnCompleteEvent`, etc.) are also wired up in the Inspector.

---

## Extending — Adding Custom Properties

Register any `Component` property in `PropertyAccessorRegistry.cs`:

```csharp
// Float property
RegisterFloat<MyComponent>("myFloat", "My Float",
    c => c.myFloat, (c, v) => c.myFloat = v);

// Color property
RegisterColor<MyComponent>("myColor", "My Color",
    c => c.myColor, (c, v) => c.myColor = v);

// Vector3 property
RegisterVector3<MyComponent>("myVector", "My Vector",
    c => c.myVector, (c, v) => c.myVector = v);
```

The property picker and timeline editor pick up the new entry automatically.

---

## License

Licensed under the [Functional Source License, Version 1.1, MIT Future License](LICENSE.md) (FSL-1.1-MIT) — https://fsl.software/

Free to use, modify and redistribute for any purpose except offering a competing product or service. Each version converts to the MIT license two years after its release.
