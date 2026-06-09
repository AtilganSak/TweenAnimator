using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using DG.Tweening;
using UnityEngine;
using UnityEngine.Events;

[assembly: InternalsVisibleTo("TweenAnimator.Editor")]

namespace TweenAnimator
{
    [Serializable] public class TweenLoopUnityEvent : UnityEvent<int>
    {
    }

    [DisallowMultipleComponent]
    [AddComponentMenu("TweenAnimator/Tween Animator Controller")]
    public class TweenAnimatorController : MonoBehaviour
    {
        [SerializeField] private TweenAnimatorClip clip;

        [Header("Events")]
        [SerializeField] private UnityEvent _onPlay = new UnityEvent();

        [SerializeField] private UnityEvent _onPause = new UnityEvent();
        [SerializeField] private UnityEvent _onStop = new UnityEvent();
        [SerializeField] private UnityEvent _onComplete = new UnityEvent();
        [SerializeField] private TweenLoopUnityEvent _onLoop = new TweenLoopUnityEvent();

        // C# events for code subscribers
        public event Action OnPlay;
        public event Action OnPause;
        public event Action OnStop;
        public event Action OnComplete;
        public event Action<int> OnLoop;

        private Sequence _builtSequence;
        private bool _isComplete;
        private Dictionary<string, TweenEntryData> _entryCache;
        private Dictionary<string, EventMarkerData> _markerCache;

        // ── Clip ─────────────────────────────────────────────────────────────
        public TweenAnimatorClip Clip => clip;
        public TweenSequenceData Sequence => clip != null ? clip.Data : null;

        // ── State ─────────────────────────────────────────────────────────────
        /// <summary>Sequence is built and actively playing.</summary>
        public bool IsPlaying => _builtSequence != null && _builtSequence.IsActive() && _builtSequence.IsPlaying();

        /// <summary>Sequence is built but paused mid-playback.</summary>
        public bool IsPaused => _builtSequence != null && _builtSequence.IsActive() && !_builtSequence.IsPlaying();

        /// <summary>True after the sequence played to its end (reset on next Play).</summary>
        public bool IsComplete => _isComplete;

        /// <summary>Total duration in seconds (read from clip data).</summary>
        public float Duration => Sequence?.TotalDuration ?? 0f;

        /// <summary>Elapsed playback time in seconds.</summary>
        public float CurrentTime => (_builtSequence != null && _builtSequence.IsActive())
            ? _builtSequence.Elapsed(false)
            : 0f;

        /// <summary>Elapsed time as 0–1 normalized value.</summary>
        public float NormalizedTime => Duration > 0f ? Mathf.Clamp01(CurrentTime / Duration) : 0f;

        /// <summary>Playback speed multiplier. Syncs to the running sequence immediately.</summary>
        public float TimeScale
        {
            get => Sequence?.timeScale ?? 1f;
            set
            {
                if (Sequence != null) Sequence.timeScale = value;
                if (_builtSequence != null && _builtSequence.IsActive())
                    _builtSequence.timeScale = value;
            }
        }

        // Inspector-assigned event accessors (can also be used from code via AddListener)
        // ── Entry lookup ──────────────────────────────────────────────────────

        /// <summary>
        /// Access an entry by its display name. Returns null if not found.
        /// Example: ctrl["Cube - Fade"].OnComplete += OnFadeDone;
        /// </summary>
        public TweenEntryData this[string entryName]
        {
            get
            {
                EnsureEntryCache();
                return _entryCache.TryGetValue(entryName, out var e) ? e : null;
            }
        }

        /// <summary>Rebuild the name→entry lookup table. Call after modifying entries at runtime.</summary>
        public void RebuildEntryCache()
        {
            _entryCache = new Dictionary<string, TweenEntryData>();
            if (Sequence == null) return;
            foreach (var entry in Sequence.entries)
            {
                // primary key: displayName (user-set label)
                if (!string.IsNullOrEmpty(entry.displayName) && !_entryCache.ContainsKey(entry.displayName))
                    _entryCache[entry.displayName] = entry;
                // fallback key: stable entryId (GUID)
                if (!_entryCache.ContainsKey(entry.entryId))
                    _entryCache[entry.entryId] = entry;
            }
        }

        /// <summary>Same as the indexer. Returns null if not found.</summary>
        public TweenEntryData GetEntry(string entryName)
        {
            EnsureEntryCache();
            return _entryCache.TryGetValue(entryName, out var e) ? e : null;
        }

        private void EnsureEntryCache()
        {
            if (_entryCache == null) RebuildEntryCache();
        }

        // ── Marker lookup ─────────────────────────────────────────────────────

        /// <summary>
        /// Find a marker by display name or markerId. Returns null if not found.
        /// Example: ctrl.GetMarker("OnJump").OnTrigger += HandleJump;
        /// </summary>
        public EventMarkerData GetMarker(string nameOrId)
        {
            EnsureMarkerCache();
            return _markerCache.TryGetValue(nameOrId, out var m) ? m : null;
        }

        private void RebuildMarkerCache()
        {
            _markerCache = new Dictionary<string, EventMarkerData>();
            if (Sequence?.markers == null) return;
            foreach (var marker in Sequence.markers)
            {
                if (!string.IsNullOrEmpty(marker.displayName) && !_markerCache.ContainsKey(marker.displayName))
                    _markerCache[marker.displayName] = marker;
                if (!_markerCache.ContainsKey(marker.markerId))
                    _markerCache[marker.markerId] = marker;
            }
        }

        private void EnsureMarkerCache()
        {
            if (_markerCache == null) RebuildMarkerCache();
        }

        /// <summary>
        /// Add a marker at runtime. Call Play() after to include it in the running sequence.
        /// Returns the new marker so you can subscribe: ctrl.AddMarker("Boom", 1.5f).OnTrigger += OnBoom;
        /// </summary>
        public EventMarkerData AddMarker(string displayName, float time)
        {
            if (Sequence == null) return null;
            if (Sequence.markers == null) Sequence.markers = new List<EventMarkerData>();

            var marker = new EventMarkerData { displayName = displayName, time = time };
            Sequence.markers.Add(marker);
            _markerCache = null;
            return marker;
        }

        /// <summary>
        /// Remove a marker by display name or markerId. Returns true if found and removed.
        /// </summary>
        public bool RemoveMarker(string nameOrId)
        {
            if (Sequence?.markers == null) return false;

            EnsureMarkerCache();
            if (!_markerCache.TryGetValue(nameOrId, out var marker)) return false;

            Sequence.markers.Remove(marker);
            _markerCache = null;
            return true;
        }

        // ── Inspector event accessors ─────────────────────────────────────────
        public UnityEvent OnPlayEvent => _onPlay;
        public UnityEvent OnPauseEvent => _onPause;
        public UnityEvent OnStopEvent => _onStop;
        public UnityEvent OnCompleteEvent => _onComplete;
        public TweenLoopUnityEvent OnLoopEvent => _onLoop;

        // ── Lifecycle ─────────────────────────────────────────────────────────
        private void Awake()
        {
            DOTween.Init(recycleAllByDefault: true, useSafeMode: true);
            if (Sequence != null && Sequence.playOnAwake)
                Play();
        }

        private void OnDestroy() => _builtSequence?.Kill();

        // ── Playback ──────────────────────────────────────────────────────────

        /// <summary>Build and play the current clip from the beginning.</summary>
        public void Play()
        {
            if (Sequence == null) return;
            _isComplete = false;
            _builtSequence?.Kill();
            _builtSequence = Build();
            _builtSequence.Play();
            FirePlay();
        }

        /// <summary>Play and return a Task that completes when the sequence finishes.</summary>
        public Task PlayAsync(CancellationToken cancellationToken = default)
        {
            if (Sequence == null) return Task.CompletedTask;

            var tcs = new TaskCompletionSource<bool>();

            void OnDone()
            {
                OnComplete -= OnDone;
                tcs.TrySetResult(true);
            }

            OnComplete += OnDone;
            Play();

            if (cancellationToken.CanBeCanceled)
                cancellationToken.Register(() =>
                {
                    OnComplete -= OnDone;
                    Stop();
                    tcs.TrySetCanceled(cancellationToken);
                });

            return tcs.Task;
        }

        /// <summary>Assign a new clip then play it from the beginning.</summary>
        public void Play(TweenAnimatorClip newClip)
        {
            SetClip(newClip);
            Play();
        }

        /// <summary>Play from a specific time in seconds.</summary>
        public void PlayFromTime(float time)
        {
            Play();
            _builtSequence?.Goto(Mathf.Clamp(time, 0f, Duration), andPlay: true);
        }

        /// <summary>Play from a normalized position (0 = start, 1 = end).</summary>
        public void PlayFromNormalizedTime(float normalizedTime) =>
            PlayFromTime(normalizedTime * Duration);

        /// <summary>Build and play the clip backwards from the end.</summary>
        public void PlayBackward()
        {
            if (Sequence == null) return;
            _isComplete = false;
            _builtSequence?.Kill();
            _builtSequence = Build();
            _builtSequence.Goto(Duration, andPlay: false);
            _builtSequence.PlayBackwards();
            FirePlay();
        }

        /// <summary>Play backwards from a specific time in seconds.</summary>
        public void PlayBackwardFromTime(float time)
        {
            if (Sequence == null) return;
            _isComplete = false;
            _builtSequence?.Kill();
            _builtSequence = Build();
            _builtSequence.Goto(Mathf.Clamp(time, 0f, Duration), andPlay: false);
            _builtSequence.PlayBackwards();
            FirePlay();
        }

        /// <summary>Play backwards from a normalized position (0 = start, 1 = end).</summary>
        public void PlayBackwardFromNormalizedTime(float normalizedTime) =>
            PlayBackwardFromTime(normalizedTime * Duration);

        /// <summary>Pause playback. Resume with <see cref="Resume"/> or <see cref="Play"/>.</summary>
        public void Pause()
        {
            if (!IsPlaying) return;
            _builtSequence.Pause();
            _onPause.Invoke();
            OnPause?.Invoke();
        }

        /// <summary>Resume a paused sequence.</summary>
        public void Resume()
        {
            if (!IsPaused) return;
            _builtSequence.Play();
            FirePlay();
        }

        /// <summary>Kill the running sequence without restoring values.</summary>
        public void Stop()
        {
            _builtSequence?.Kill();
            _builtSequence = null;
            _onStop.Invoke();
            OnStop?.Invoke();
        }

        /// <summary>Jump to the beginning and pause.</summary>
        public void Rewind()
        {
            EnsureBuilt();
            _builtSequence?.Rewind();
        }

        /// <summary>Seek to a time in seconds without changing play/pause state.</summary>
        public void GotoTime(float time)
        {
            EnsureBuilt();
            _builtSequence?.Goto(Mathf.Clamp(time, 0f, Duration), andPlay: IsPlaying);
        }

        /// <summary>Seek to a normalized position (0–1) without changing play/pause state.</summary>
        public void GotoNormalizedTime(float normalizedTime) =>
            GotoTime(normalizedTime * Duration);

        /// <summary>Replace the clip. Stops any running playback.</summary>
        public void SetClip(TweenAnimatorClip newClip)
        {
            Stop();
            clip = newClip;
            _entryCache = null;
            _markerCache = null;
        }

        // ── Internal helpers ─────────────────────────────────────────────────

        private Sequence Build()
        {
            var seq = SequenceBuilder.Build(this, Sequence);
            seq.OnComplete(FireComplete);
            seq.OnStepComplete(FireLoop);
            return seq;
        }

        private void EnsureBuilt()
        {
            if (_builtSequence != null && _builtSequence.IsActive()) return;
            if (Sequence == null) return;
            _isComplete = false;
            _builtSequence = Build();
            _builtSequence.Pause();
        }

        private void FirePlay()
        {
            _onPlay.Invoke();
            OnPlay?.Invoke();
        }

        private void FireComplete()
        {
            _isComplete = true;
            _onComplete.Invoke();
            OnComplete?.Invoke();
        }

        private void FireLoop()
        {
            int step = _builtSequence?.CompletedLoops() ?? 0;
            _onLoop.Invoke(step);
            OnLoop?.Invoke(step);
        }

        internal TweenSequenceData GetSequenceData() => Sequence;

#if UNITY_EDITOR
        private void Reset()
        {
            string assetName = gameObject.name + "_TweenClip";
            string path = UnityEditor.EditorUtility.SaveFilePanelInProject(
                "Create Tween Clip", assetName, "asset", "Choose where to save the Tween Clip asset");
            if (string.IsNullOrEmpty(path)) return;

            var newClip = ScriptableObject.CreateInstance<TweenAnimatorClip>();
            UnityEditor.AssetDatabase.CreateAsset(newClip, path);
            UnityEditor.AssetDatabase.SaveAssets();
            clip = newClip;
            UnityEditor.EditorUtility.SetDirty(this);
        }
#endif
    }
}