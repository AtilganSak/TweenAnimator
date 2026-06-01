# Tween Animator

A visual timeline-based tween animation editor for Unity, powered by **DOTween**. Design multi-property animations in a custom editor window, preview them by scrubbing in Edit Mode, and trigger playback at runtime with a single API call.

---

## Features

- **Visual timeline editor** — drag, resize, and chain animation blocks on a zoomable timeline
- **Edit Mode preview** — scrub the red playhead to preview animations without entering Play Mode
- **Property picker** — auto-scans your hierarchy and lists all animatable properties
- **Multi-property sequences** — animate position, rotation, scale, color, alpha, font size, and more in one clip
- **Linked start values** — chain entries so one entry's end value feeds the next entry's start value
- **Per-entry events** — `OnStart` / `OnComplete` C# callbacks on individual tween entries
- **Full playback API** — play, pause, resume, stop, rewind, seek, play backward
- **DOTween under the hood** — all eases, loop types, and RotateMode options exposed

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

### 6. Play at Runtime

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

// Playback
ctrl.Play();
ctrl.Play(otherClip);           // swap clip then play
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

// Clip
ctrl.SetClip(newClip);
```

### Per-Entry Events

```csharp
// By display name set in the editor
ctrl["Cube - Fade"].OnComplete += () => Debug.Log("Fade done!");
ctrl["Logo - Scale"].OnStart   += () => Debug.Log("Scale started!");

// By entry ID (GUID, always stable)
ctrl["some-guid-string"].OnComplete += HandleDone;
```

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
