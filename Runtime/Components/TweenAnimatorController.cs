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

    [Serializable]
    public class NamedTweenClip
    {
        public string name = "Clip";
        public TweenAnimatorClip clip;
    }

    [DisallowMultipleComponent]
    [AddComponentMenu("TweenAnimator/Tween Animator Controller")]
    public class TweenAnimatorController : MonoBehaviour
    {
        [SerializeField] private List<NamedTweenClip> clips = new List<NamedTweenClip>();
        [SerializeField] private int activeClipIndex = -1;

        [SerializeField] private bool playOnAwake;

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

        // ── Clips ────────────────────────────────────────────────────────────
        public IReadOnlyList<NamedTweenClip> Clips => clips;
        public int ActiveClipIndex => activeClipIndex;
        public bool PlayOnAwake { get => playOnAwake; set => playOnAwake = value; }

        /// <summary>The currently active clip (selected by index/name via Play or SetActiveClip).</summary>
        public TweenAnimatorClip Clip => IsValidIndex(activeClipIndex) ? clips[activeClipIndex].clip : null;
        public TweenSequenceData Sequence => Clip != null ? Clip.Data : null;

        private bool IsValidIndex(int index) => index >= 0 && index < clips.Count;

        /// <summary>Find a clip's index by its display name. Returns -1 if not found.</summary>
        public int GetClipIndex(string clipName)
        {
            for (int i = 0; i < clips.Count; i++)
                if (clips[i].name == clipName) return i;
            return -1;
        }

        /// <summary>Get a clip asset by its display name. Returns null if not found.</summary>
        public TweenAnimatorClip GetClip(string clipName)
        {
            int index = GetClipIndex(clipName);
            return index >= 0 ? clips[index].clip : null;
        }

        /// <summary>Switch the active clip without playing it. Stops any running playback. Returns false if the index is out of range.</summary>
        public bool SetActiveClip(int index)
        {
            if (!IsValidIndex(index))
            {
                Debug.LogWarning($"[TweenAnimator] Clip index {index} out of range (clips: {clips.Count}) on \"{gameObject.name}\".", this);
                return false;
            }

            Stop();
            activeClipIndex = index;
            _entryCache = null;
            _markerCache = null;
            return true;
        }

        /// <summary>Switch the active clip by name without playing it. Returns false if not found.</summary>
        public bool SetActiveClip(string clipName)
        {
            int index = GetClipIndex(clipName);
            if (index < 0)
            {
                Debug.LogWarning($"[TweenAnimator] No clip named \"{clipName}\" on \"{gameObject.name}\".", this);
                return false;
            }

            return SetActiveClip(index);
        }

        /// <summary>Add a new clip slot. If no clip is active yet, it becomes the active one.</summary>
        public int AddClip(string clipName, TweenAnimatorClip clipAsset)
        {
            clips.Add(new NamedTweenClip { name = clipName, clip = clipAsset });
            int index = clips.Count - 1;
            if (!IsValidIndex(activeClipIndex)) SetActiveClip(index);
            return index;
        }

        /// <summary>Remove a clip slot by index.</summary>
        public void RemoveClipAt(int index)
        {
            if (!IsValidIndex(index)) return;
            if (index == activeClipIndex) Stop();
            clips.RemoveAt(index);
            if (activeClipIndex == index) activeClipIndex = -1;
            else if (activeClipIndex > index) activeClipIndex--;
            _entryCache = null;
            _markerCache = null;
        }

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
        public EventMarkerData GetMarker(string name)
        {
            EnsureMarkerCache();
            return _markerCache.TryGetValue(name, out var m) ? m : null;
        }

        /// <summary>Find a marker on the clip at the given index, without switching the active clip.</summary>
        public EventMarkerData GetMarker(int clipIndex, string name) =>
            FindMarker(GetClipSequence(clipIndex), name);

        /// <summary>Find a marker on the clip with the given display name, without switching the active clip.</summary>
        public EventMarkerData GetMarker(string clipName, string name) =>
            FindMarker(GetClipSequence(clipName), name);

        private static EventMarkerData FindMarker(TweenSequenceData seq, string name)
        {
            if (seq?.markers == null) return null;
            foreach (var m in seq.markers)
                if (m.displayName == name || m.markerId == name)
                    return m;
            return null;
        }

        private TweenSequenceData GetClipSequence(int clipIndex) =>
            IsValidIndex(clipIndex) ? clips[clipIndex].clip?.Data : null;

        private TweenSequenceData GetClipSequence(string clipName)
        {
            int index = GetClipIndex(clipName);
            return index >= 0 ? GetClipSequence(index) : null;
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

        /// <summary>Add a marker to the clip at the given index, without switching the active clip.</summary>
        public EventMarkerData AddMarker(int clipIndex, string displayName, float time)
        {
            var seq = GetClipSequence(clipIndex);
            if (seq == null) return null;
            if (seq.markers == null) seq.markers = new List<EventMarkerData>();

            var marker = new EventMarkerData { displayName = displayName, time = time };
            seq.markers.Add(marker);
            if (clipIndex == activeClipIndex) _markerCache = null;
            return marker;
        }

        /// <summary>Add a marker to the clip with the given display name, without switching the active clip.</summary>
        public EventMarkerData AddMarker(string clipName, string displayName, float time)
        {
            int index = GetClipIndex(clipName);
            return index >= 0 ? AddMarker(index, displayName, time) : null;
        }

        /// <summary>
        /// Remove a marker by display name or markerId. Returns true if found and removed.
        /// </summary>
        public bool RemoveMarker(string name)
        {
            if (Sequence?.markers == null) return false;

            EnsureMarkerCache();
            if (!_markerCache.TryGetValue(name, out var marker)) return false;

            Sequence.markers.Remove(marker);
            _markerCache = null;
            return true;
        }

        /// <summary>Remove a marker from the clip at the given index, without switching the active clip.</summary>
        public bool RemoveMarker(int clipIndex, string name)
        {
            var seq = GetClipSequence(clipIndex);
            var marker = FindMarker(seq, name);
            if (marker == null) return false;

            seq.markers.Remove(marker);
            if (clipIndex == activeClipIndex) _markerCache = null;
            return true;
        }

        /// <summary>Remove a marker from the clip with the given display name, without switching the active clip.</summary>
        public bool RemoveMarker(string clipName, string name)
        {
            int index = GetClipIndex(clipName);
            return index >= 0 && RemoveMarker(index, name);
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
            if (playOnAwake)
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

        /// <summary>Switch to the clip at the given index and play it, returning a Task that completes when it finishes.</summary>
        public Task PlayAsync(int clipIndex, CancellationToken cancellationToken = default) =>
            SetActiveClip(clipIndex) ? PlayAsync(cancellationToken) : Task.CompletedTask;

        /// <summary>Switch to the clip with the given display name and play it, returning a Task that completes when it finishes.</summary>
        public Task PlayAsync(string clipName, CancellationToken cancellationToken = default) =>
            SetActiveClip(clipName) ? PlayAsync(cancellationToken) : Task.CompletedTask;

        /// <summary>Switch to the clip at the given index and play it from the beginning.</summary>
        public void Play(int clipIndex)
        {
            if (SetActiveClip(clipIndex)) Play();
        }

        /// <summary>Switch to the clip with the given display name and play it from the beginning.</summary>
        public void Play(string clipName)
        {
            if (SetActiveClip(clipName)) Play();
        }

        /// <summary>Play from a specific time in seconds.</summary>
        public void PlayFromTime(float time)
        {
            Play();
            _builtSequence?.Goto(Mathf.Clamp(time, 0f, Duration), andPlay: true);
        }

        /// <summary>Switch to the clip at the given index and play it from a specific time in seconds.</summary>
        public void PlayFromTime(int clipIndex, float time)
        {
            if (SetActiveClip(clipIndex)) PlayFromTime(time);
        }

        /// <summary>Switch to the clip with the given display name and play it from a specific time in seconds.</summary>
        public void PlayFromTime(string clipName, float time)
        {
            if (SetActiveClip(clipName)) PlayFromTime(time);
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

        /// <summary>Switch to the clip at the given index and play it backwards from the end.</summary>
        public void PlayBackward(int clipIndex)
        {
            if (SetActiveClip(clipIndex)) PlayBackward();
        }

        /// <summary>Switch to the clip with the given display name and play it backwards from the end.</summary>
        public void PlayBackward(string clipName)
        {
            if (SetActiveClip(clipName)) PlayBackward();
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

        /// <summary>Switch to the clip at the given index and play it backwards from a specific time in seconds.</summary>
        public void PlayBackwardFromTime(int clipIndex, float time)
        {
            if (SetActiveClip(clipIndex)) PlayBackwardFromTime(time);
        }

        /// <summary>Switch to the clip with the given display name and play it backwards from a specific time in seconds.</summary>
        public void PlayBackwardFromTime(string clipName, float time)
        {
            if (SetActiveClip(clipName)) PlayBackwardFromTime(time);
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
            AddClip(newClip.name, newClip);
            UnityEditor.EditorUtility.SetDirty(this);
        }
#endif
    }
}