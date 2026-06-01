using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace TweenAnimator.Editor
{
    public class TweenAnimatorWindow : EditorWindow
    {
        // ─── Layout constants ──────────────────────────────────────────────────
        private const float LabelWidth       = 220f;
        private const float TimelineHeight   = 24f;
        private const float HeaderHeight     = 22f;
        private float _inspectorActualHeight = 180f; // measured each Repaint, used next frame
        private const float HandleWidth      = 8f;
        private const float MinBlockWidth    = 4f;
        private const float TimelineMinSecs  = 5f;
        private const float TimeRulerHeight  = 20f;

        // ─── State ─────────────────────────────────────────────────────────────
        private TweenAnimatorWindowState _state = new TweenAnimatorWindowState();
        private Vector2 _scrollPos;

        // Timeline view
        private float _viewDuration    = 5f;
        private float _pixelsPerSec    = 100f;
        private float _timelineScrollX = 0f;   // seconds offset (pan)

        // Pan state
        private bool  _panDragging;
        private float _panStartMouseX;
        private float _panStartScrollX;

        // Drag state
        private enum DragMode { None, MoveBlock, ResizeLeft, ResizeRight }
        private DragMode       _dragMode;
        private TweenEntryData _dragEntry;
        private float          _dragStartMouseX;
        private float          _dragStartDelay;
        private float          _dragStartDuration;
        private bool           _scrubDragging;
        private float          _dragAccumulatedY;
        private List<List<TweenEntryData>> _cachedTracks    = new List<List<TweenEntryData>>();
        private HashSet<string>            _missingEntryIds = new HashSet<string>();

        // ─── Styles (lazy) ─────────────────────────────────────────────────────
        private static GUIStyle _blockStyle;
        private static GUIStyle _labelStyle;
        private static GUIStyle _headerStyle;
        private static GUIStyle _whiteMiniLabel;
        private static Color    _blockColorOff   = new Color(0.4f,  0.4f,  0.4f, 0.6f);
        private static Color    _tickColor       = new Color(0.5f,  0.5f,  0.5f, 1f);

        private static readonly Color[] _palette = new[]
        {
            new Color(0.28f, 0.56f, 0.90f, 0.9f),
            new Color(0.90f, 0.45f, 0.28f, 0.9f),
            new Color(0.35f, 0.80f, 0.45f, 0.9f),
            new Color(0.80f, 0.35f, 0.75f, 0.9f),
            new Color(0.88f, 0.78f, 0.22f, 0.9f),
            new Color(0.30f, 0.78f, 0.78f, 0.9f),
            new Color(0.88f, 0.32f, 0.48f, 0.9f),
            new Color(0.58f, 0.44f, 0.26f, 0.9f),
        };

        // ─── Menu ──────────────────────────────────────────────────────────────
        [MenuItem("Tools/Tween Animator")]
        public static void ShowWindow()
        {
            var w = GetWindow<TweenAnimatorWindow>("Tween Animator");
            w.minSize = new Vector2(600, 400);
        }

        // ─── Lifecycle ─────────────────────────────────────────────────────────
        private void OnEnable()
        {
            Selection.selectionChanged             += OnSelectionChanged;
            EditorApplication.playModeStateChanged += OnPlayModeChanged;
            _state.Evaluate();
        }

        private void OnDisable()
        {
            Selection.selectionChanged             -= OnSelectionChanged;
            EditorApplication.playModeStateChanged -= OnPlayModeChanged;
            _state.ExitPreviewMode();
        }

        private void OnFocus()
        {
            ValidateBindings();
            Repaint();
        }

        private void OnSelectionChanged()
        {
            _state.Evaluate();
            ValidateBindings();
            Repaint();
        }

        private void ValidateBindings()
        {
            _missingEntryIds.Clear();
            if (_state.Controller?.Sequence == null) return;
            foreach (var entry in _state.Controller.Sequence.entries)
            {
                if (entry.binding == null) { _missingEntryIds.Add(entry.entryId); continue; }
                var comp = TweenAnimatorWindowState.ResolveComponent(_state.Controller, entry.binding);
                if (comp == null) _missingEntryIds.Add(entry.entryId);
            }
        }

        private void OnPlayModeChanged(PlayModeStateChange change)
        {
            if (change == PlayModeStateChange.ExitingEditMode)
                _state.ExitPreviewMode();
        }

        // ─── Main GUI ──────────────────────────────────────────────────────────
        private void OnGUI()
        {
            InitStyles();

            // Guard: controller destroyed externally (deleted from hierarchy/component removed).
            // Cannot change state mid-frame — Layout and Repaint must draw identical control sequences.
            // Draw a consistent fallback this frame and defer Evaluate() to next frame via delayCall.
            if (_state.Mode == WindowMode.HasController && !_state.Controller)
            {
                if (Event.current.type == EventType.Layout)
                    EditorApplication.delayCall += () => { _state.Evaluate(); Repaint(); };
                DrawNoSelection();
                return;
            }

            // Delete key removes selected entry
            if (_state.SelectedEntry != null && _state.Controller?.Clip != null)
            {
                Event e = Event.current;
                if (e.type == EventType.KeyDown && e.keyCode == KeyCode.Delete)
                {
                    Undo.RecordObject(_state.Controller.Clip, "Remove Tween Entry");
                    _state.Controller.Sequence.entries.Remove(_state.SelectedEntry);
                    _state.SelectedEntry = null;
                    EditorUtility.SetDirty(_state.Controller.Clip);
                    e.Use();
                    Repaint();
                }
            }

            switch (_state.Mode)
            {
                case WindowMode.NoSelection:   DrawNoSelection();   break;
                case WindowMode.NoComponent:   DrawNoComponent();   break;
                case WindowMode.NoClip:        DrawNoClip();        break;
                case WindowMode.HasController: DrawMainUI();        break;
            }

            if (_state.IsPreviewPlaying)
                Repaint();
        }

        // ─── State panels ──────────────────────────────────────────────────────
        private void DrawNoSelection()
        {
            GUILayout.FlexibleSpace();
            GUILayout.BeginHorizontal();
            GUILayout.FlexibleSpace();
            GUILayout.Label("Select a GameObject to begin.", EditorStyles.largeLabel);
            GUILayout.FlexibleSpace();
            GUILayout.EndHorizontal();
            GUILayout.FlexibleSpace();
        }

        private void DrawNoComponent()
        {
            GUILayout.FlexibleSpace();
            GUILayout.BeginHorizontal();
            GUILayout.FlexibleSpace();
            GUILayout.BeginVertical();

            GUILayout.Label($"\"{Selection.activeGameObject?.name}\" has no TweenAnimatorController.", EditorStyles.wordWrappedLabel);
            GUILayout.Space(8);

            if (GUILayout.Button("Create Tween Animation", GUILayout.Height(30)))
            {
                Undo.AddComponent<TweenAnimatorController>(Selection.activeGameObject);
                _state.Evaluate();
            }

            GUILayout.EndVertical();
            GUILayout.FlexibleSpace();
            GUILayout.EndHorizontal();
            GUILayout.FlexibleSpace();
        }

        private void DrawNoClip()
        {
            var ctrl = _state.Controller;

            GUILayout.FlexibleSpace();
            GUILayout.BeginHorizontal();
            GUILayout.FlexibleSpace();
            GUILayout.BeginVertical(GUILayout.Width(320));

            GUILayout.Label($"\"{ctrl.gameObject.name}\" has no Tween Clip assigned.", EditorStyles.wordWrappedLabel);
            GUILayout.Space(8);

            GUILayout.BeginHorizontal();
            GUILayout.Label("Assign Clip:", GUILayout.Width(80));
            var assigned = (TweenAnimatorClip)EditorGUILayout.ObjectField(null, typeof(TweenAnimatorClip), false);
            if (assigned != null)
            {
                Undo.RecordObject(ctrl, "Assign Tween Clip");
                ctrl.SetClip(assigned);
                EditorUtility.SetDirty(ctrl);
                _state.Evaluate();
                Repaint();
            }
            GUILayout.EndHorizontal();

            GUILayout.Space(4);

            if (GUILayout.Button("Create New Clip", GUILayout.Height(28)))
            {
                var newClip = CreateNewClip(ctrl.gameObject.name);
                if (newClip != null)
                {
                    Undo.RecordObject(ctrl, "Assign Tween Clip");
                    ctrl.SetClip(newClip);
                    EditorUtility.SetDirty(ctrl);
                    _state.Evaluate();
                    Repaint();
                }
            }

            GUILayout.EndVertical();
            GUILayout.FlexibleSpace();
            GUILayout.EndHorizontal();
            GUILayout.FlexibleSpace();
        }

        private void DrawMainUI()
        {
            var ctrl = _state.Controller;
            var seq  = ctrl.Sequence;

            _viewDuration = (position.width - LabelWidth) / _pixelsPerSec;

            DrawToolbar(ctrl, seq);
            DrawTrackArea(ctrl, seq);

            if (_state.SelectedEntry != null)
            {
                DrawEntryInspector(_state.SelectedEntry, ctrl);
                if (Event.current.type == EventType.Repaint)
                {
                    float h = GUILayoutUtility.GetLastRect().height;
                    if (h > 10f) _inspectorActualHeight = h;
                }
            }
        }

        // ─── Toolbar ───────────────────────────────────────────────────────────
        private void DrawToolbar(TweenAnimatorController ctrl, TweenSequenceData seq)
        {
            GUILayout.BeginHorizontal(EditorStyles.toolbar);

            if (GUILayout.Button(ctrl.gameObject.name, EditorStyles.toolbarButton, GUILayout.Width(100)))
            {
                Selection.activeGameObject = ctrl.gameObject;
                EditorGUIUtility.PingObject(ctrl.gameObject);
                SceneView.lastActiveSceneView.FrameSelected();
            }

            var newClip = (TweenAnimatorClip)EditorGUILayout.ObjectField(
                ctrl.Clip, typeof(TweenAnimatorClip), false, GUILayout.Width(130));
            if (newClip != ctrl.Clip)
            {
                Undo.RecordObject(ctrl, "Assign Tween Clip");
                ctrl.SetClip(newClip);
                EditorUtility.SetDirty(ctrl);
                _state.Evaluate();
                Repaint();
            }

            if (GUILayout.Button("New Clip", EditorStyles.toolbarButton, GUILayout.Width(58)))
            {
                var created = CreateNewClip(ctrl.gameObject.name);
                if (created != null)
                {
                    Undo.RecordObject(ctrl, "Assign Tween Clip");
                    ctrl.SetClip(created);
                    EditorUtility.SetDirty(ctrl);
                    _state.Evaluate();
                    Repaint();
                }
            }

            GUILayout.FlexibleSpace();

            // Preview toggle
            EditorGUI.BeginDisabledGroup(EditorApplication.isPlaying);
            bool previewNow = GUILayout.Toggle(
                _state.IsPreviewEnabled,
                "Preview",
                EditorStyles.toolbarButton,
                GUILayout.Width(55));
            if (previewNow != _state.IsPreviewEnabled)
            {
                if (previewNow) _state.EnterPreviewMode();
                else            _state.ExitPreviewMode();
            }
            EditorGUI.EndDisabledGroup();

            GUILayout.Space(4);

            EditorGUI.BeginDisabledGroup(EditorApplication.isPlaying);

            if (GUILayout.Button(new GUIContent(EditorGUIUtility.IconContent("PlayButton").image, "Play preview"), EditorStyles.toolbarButton, GUILayout.Width(30)))
            {
                if (!_state.IsPreviewEnabled) _state.EnterPreviewMode();
                _state.StartPreview();
            }

            if (GUILayout.Button(new GUIContent(EditorGUIUtility.IconContent("PauseButton").image, "Pause preview"), EditorStyles.toolbarButton, GUILayout.Width(30)))
                _state.PausePreview();

            if (GUILayout.Button(new GUIContent(EditorGUIUtility.IconContent("PreMatQuad").image, "Stop preview"), EditorStyles.toolbarButton, GUILayout.Width(30)))
                _state.StopPreview();

            if (GUILayout.Button(new GUIContent(EditorGUIUtility.IconContent("Animation.FirstKey").image, "Rewind to start"), EditorStyles.toolbarButton, GUILayout.Width(30)))
                _state.RewindPreview();

            EditorGUI.EndDisabledGroup();

            GUILayout.Space(10);

            if (GUILayout.Button(new GUIContent("⟲ View", "Reset timeline zoom and scroll"), EditorStyles.toolbarButton, GUILayout.Width(48)))
            {
                _pixelsPerSec    = 100f;
                _timelineScrollX = 0f;
                Repaint();
            }

            GUILayout.Space(4);

            if (GUILayout.Button("+ Add Property", EditorStyles.toolbarButton))
                ShowAddPropertyMenu(ctrl, seq);

            GUILayout.EndHorizontal();
        }

        // ─── Track area ────────────────────────────────────────────────────────
        private void DrawTrackArea(TweenAnimatorController ctrl, TweenSequenceData seq)
        {
            // Consume zoom + pan events BEFORE scroll view steals them
            Event ev = Event.current;

            if (ev.type == EventType.ScrollWheel)
            {
                float factor = ev.delta.y > 0f ? 0.85f : 1.15f;
                _pixelsPerSec = Mathf.Clamp(_pixelsPerSec * factor, 100f, 500f);
                ev.Use();
                Repaint();
            }
            else if (ev.type == EventType.MouseDown && ev.button == 2)
            {
                _panDragging     = true;
                _panStartMouseX  = ev.mousePosition.x;
                _panStartScrollX = _timelineScrollX;
                ev.Use();
            }
            else if (_panDragging)
            {
                if (ev.type == EventType.MouseDrag)
                {
                    float delta = (ev.mousePosition.x - _panStartMouseX) / _pixelsPerSec;
                    _timelineScrollX = Mathf.Max(0f, _panStartScrollX - delta);
                    ev.Use();
                    Repaint();
                }
                else if (ev.type == EventType.MouseUp)
                {
                    _panDragging = false;
                    ev.Use();
                }
            }

            float inspectorH = _state.SelectedEntry == null ? 0f : _inspectorActualHeight;
            float availableHeight = position.height
                - HeaderHeight
                - TimeRulerHeight
                - inspectorH;

            _scrollPos = GUILayout.BeginScrollView(_scrollPos, false, false, GUIStyle.none, GUI.skin.verticalScrollbar, GUILayout.Height(availableHeight));

            DrawTimeRuler();

            _cachedTracks = GetTrackGroups(seq.entries);
            var tracks = _cachedTracks;

            if (tracks.Count == 0)
            {
                GUILayout.Space(20);
                GUILayout.Label("  No properties. Click '+ Add Property'.", EditorStyles.miniLabel);
            }
            else
            {
                for (int ti = 0; ti < tracks.Count; ti++)
                    DrawTrackRow(tracks[ti], ti, ctrl, seq);
            }

            GUILayout.EndScrollView();

            // Label / timeline separator
            EditorGUI.DrawRect(new Rect(LabelWidth - 1, HeaderHeight, 1f, availableHeight + TimeRulerHeight), new Color(0.1f, 0.1f, 0.1f, 1f));

            // Pan cursor over timeline area
            Rect timelineArea = new Rect(LabelWidth, HeaderHeight, position.width - LabelWidth, availableHeight + TimeRulerHeight);
            EditorGUIUtility.AddCursorRect(timelineArea, _panDragging ? MouseCursor.Pan : MouseCursor.Arrow);

            // Click on empty space below tracks → unselect
            Event evEmpty = Event.current;
            if (evEmpty.type == EventType.MouseDown && evEmpty.button == 0)
            {
                Rect fullArea = new Rect(0, HeaderHeight, position.width, availableHeight);
                if (fullArea.Contains(evEmpty.mousePosition))
                {
                    _state.SelectedEntry = null;
                    evEmpty.Use();
                    Repaint();
                }
            }

            HandleDrag();
        }

        private static List<List<TweenEntryData>> GetTrackGroups(List<TweenEntryData> entries)
        {
            var groups = new List<List<TweenEntryData>>();
            var seen   = new Dictionary<string, List<TweenEntryData>>();

            foreach (var entry in entries)
            {
                string id = string.IsNullOrEmpty(entry.trackId) ? entry.entryId : entry.trackId;
                if (!seen.TryGetValue(id, out var group))
                {
                    group = new List<TweenEntryData>();
                    seen[id] = group;
                    groups.Add(group);
                }
                group.Add(entry);
            }
            return groups;
        }

        // Tick interval candidates (seconds)
        private static readonly float[] _tickCandidates = { 0.05f, 0.1f, 0.25f, 0.5f, 1f, 2f, 5f, 10f, 30f, 60f };

        private const float SnapInterval = 0.01f;

        private static float SnapValue(float value, bool free = false)
        {
            if (free) return value;
            return Mathf.Round(value / SnapInterval) * SnapInterval;
        }

        private static float PickInterval(float pixelsPerSec, float minPx)
        {
            foreach (var c in _tickCandidates)
                if (c * pixelsPerSec >= minPx) return c;
            return _tickCandidates[_tickCandidates.Length - 1];
        }

        private static string FormatTick(float t)
        {
            float rounded = Mathf.Round(t * 1000f) / 1000f;
            if (Mathf.Approximately(rounded, Mathf.Round(rounded)))
                return $"{Mathf.RoundToInt(rounded)}s";
            string s = $"{rounded:F2}".TrimEnd('0');
            return s + "s";
        }

        private void DrawTimeRuler()
        {
            Rect rulerRect = GUILayoutUtility.GetRect(position.width, TimeRulerHeight);
            EditorGUI.DrawRect(rulerRect, rulerColor);

            float minorInterval = PickInterval(_pixelsPerSec, minPx: 12f);
            float majorInterval = PickInterval(_pixelsPerSec, minPx: 55f);

            float startTime = _timelineScrollX;
            float endTime   = _timelineScrollX + _viewDuration + minorInterval;

            // Minor ticks
            int firstMinor = Mathf.FloorToInt(startTime / minorInterval);
            int lastMinor  = Mathf.CeilToInt(endTime   / minorInterval);
            for (int i = firstMinor; i <= lastMinor; i++)
            {
                float t = i * minorInterval;
                float x = LabelWidth + (t - _timelineScrollX) * _pixelsPerSec;
                if (x < LabelWidth) continue;
                EditorGUI.DrawRect(new Rect(x, rulerRect.yMax - TimeRulerHeight * 0.35f, 1f, TimeRulerHeight * 0.35f),
                    new Color(0.5f, 0.5f, 0.5f, 0.6f));
            }

            // Major ticks (with labels)
            int firstMajor = Mathf.FloorToInt(startTime / majorInterval);
            int lastMajor  = Mathf.CeilToInt(endTime   / majorInterval);
            for (int i = firstMajor; i <= lastMajor; i++)
            {
                float t = i * majorInterval;
                float x = LabelWidth + (t - _timelineScrollX) * _pixelsPerSec;
                if (x < LabelWidth) continue;
                EditorGUI.DrawRect(new Rect(x, rulerRect.y, 1f, TimeRulerHeight), _tickColor);
                GUI.Label(new Rect(x + 2f, rulerRect.y, 48f, TimeRulerHeight), FormatTick(t), EditorStyles.miniLabel);
            }

            // Scrub input
            Event e = Event.current;
            Rect scrubZone = new Rect(LabelWidth, rulerRect.y, rulerRect.width - LabelWidth, rulerRect.height);
            if (e.button == 0 && e.type == EventType.MouseDown && scrubZone.Contains(e.mousePosition))
            {
                if (!_state.IsPreviewEnabled) _state.EnterPreviewMode();
                _scrubDragging = true;
                float t = (e.mousePosition.x - LabelWidth) / _pixelsPerSec + _timelineScrollX;
                _state.GotoTime(t);
                e.Use();
                Repaint();
            }

            // Playhead
            float phX = LabelWidth + (_state.CurrentTime - _timelineScrollX) * _pixelsPerSec;
            if (phX >= LabelWidth)
            {
                EditorGUI.DrawRect(new Rect(phX - 1, rulerRect.y, 2, rulerRect.height), new Color(1f, 0.3f, 0.3f, 1f));
                if (_state.CurrentTime > 0.001f)
                    GUI.Label(new Rect(phX + 3, rulerRect.y, 50f, rulerRect.height), $"{_state.CurrentTime:F2}s", EditorStyles.miniLabel);
            }
        }

        private void DrawTrackRow(List<TweenEntryData> track, int trackIndex, TweenAnimatorController ctrl, TweenSequenceData seq)
        {
            bool anySelected = track.Contains(_state.SelectedEntry);

            GUILayout.BeginHorizontal(GUILayout.Height(TimelineHeight));

            // ── Label column ──────────────────────────────────────────────────
            var labelRect = GUILayoutUtility.GetRect(LabelWidth, TimelineHeight,
                GUILayout.Width(LabelWidth), GUILayout.Height(TimelineHeight));

            EditorGUI.DrawRect(labelRect, anySelected
                ? new Color(0.25f, 0.35f, 0.25f, 0.5f)
                : (trackIndex % 2 == 0 ? new Color(0.18f, 0.18f, 0.18f, 0.3f) : new Color(0.22f, 0.22f, 0.22f, 0.3f)));

            // Enable toggle — toggles all entries in track
            Rect toggleRect = new Rect(labelRect.x + 2, labelRect.y + 3, 16, 16);
            bool allEnabled = track.TrueForAll(e => e.isEnabled);
            bool enabled = GUI.Toggle(toggleRect, allEnabled, new GUIContent("", "Enable / disable track"));
            if (enabled != allEnabled)
            {
                Undo.RecordObject(ctrl.Clip, "Toggle Track");
                foreach (var te in track) te.isEnabled = enabled;
                EditorUtility.SetDirty(ctrl.Clip);
            }

            // Property label — shows first entry; click selects first entry
            var firstEntry = track[0];
            bool trackMissing = track.Exists(e => _missingEntryIds.Contains(e.entryId));
            float warnWidth = trackMissing ? 18f : 0f;
            Rect textRect = new Rect(labelRect.x + 20, labelRect.y, labelRect.width - 58 - warnWidth, labelRect.height);
            if (GUI.Button(textRect, TrackLabel(firstEntry, ctrl.transform), EditorStyles.label))
                _state.SelectedEntry = anySelected ? null : firstEntry;

            if (trackMissing)
            {
                Rect warnRect = new Rect(textRect.xMax + 2, labelRect.y + 2, 16, 16);
                var warnContent = new GUIContent(
                    EditorGUIUtility.IconContent("console.warnicon.sml").image,
                    "Missing: bound object or component no longer exists.");
                GUI.Label(warnRect, warnContent, GUIStyle.none);
            }

            // "+" button — opens chain context menu
            Rect addRect = new Rect(labelRect.xMax - 38, labelRect.y + 2, 18, 18);
            if (GUI.Button(addRect, new GUIContent("+", "Add chained tween to this track"), EditorStyles.miniButton))
                ShowChainMenu(ctrl, seq, track);

            // "×" button — deletes entire track
            Rect delRect = new Rect(labelRect.xMax - 18, labelRect.y + 2, 18, 18);
            if (GUI.Button(delRect, new GUIContent("×", "Delete track"), EditorStyles.miniButton))
            {
                Undo.RecordObject(ctrl.Clip, "Remove Track");
                foreach (var te in track) seq.entries.Remove(te);
                if (track.Contains(_state.SelectedEntry)) _state.SelectedEntry = null;
                EditorUtility.SetDirty(ctrl.Clip);
                GUILayout.EndHorizontal();
                return;
            }

            // ── Timeline column ───────────────────────────────────────────────
            Rect trackRect = GUILayoutUtility.GetRect(
                position.width - LabelWidth, TimelineHeight,
                GUILayout.ExpandWidth(true), GUILayout.Height(TimelineHeight));

            EditorGUI.DrawRect(trackRect, new Color(0.12f, 0.12f, 0.12f, 0.4f));

            // Draw all blocks for all entries in this track
            Color tColor = track[0].trackColor;
            foreach (var entry in track)
                DrawBlock(entry, trackRect, ctrl, seq, tColor);

            // Track background click → scrub (fires only when no block consumed the event)
            Event evTrack = Event.current;
            if (evTrack.button == 0 && evTrack.type == EventType.MouseDown
                && trackRect.Contains(evTrack.mousePosition))
            {
                if (!_state.IsPreviewEnabled) _state.EnterPreviewMode();
                _scrubDragging = true;
                float t = (evTrack.mousePosition.x - LabelWidth) / _pixelsPerSec + _timelineScrollX;
                _state.GotoTime(t);
                evTrack.Use();
                Repaint();
            }

            // Playhead line
            float phX = trackRect.x + (_state.CurrentTime - _timelineScrollX) * _pixelsPerSec;
            if (phX >= trackRect.x)
                EditorGUI.DrawRect(new Rect(phX - 1, trackRect.y, 2, trackRect.height), new Color(1f, 0.3f, 0.3f, 0.85f));

            GUILayout.EndHorizontal();

        }

        private void DrawBlock(TweenEntryData entry, Rect trackRect, TweenAnimatorController ctrl, TweenSequenceData seq, Color trackColor)
        {
            bool isSelected = _state.SelectedEntry == entry;

            float blockX = trackRect.x + (entry.delay - _timelineScrollX) * _pixelsPerSec;
            float blockW = Mathf.Max(MinBlockWidth, entry.EffectiveDuration * _pixelsPerSec);

            // Clip to timeline area — don't bleed over label column
            float clippedX = Mathf.Max(blockX, trackRect.x);
            float clippedW = blockX + blockW - clippedX;
            if (clippedW <= 0f) return;

            Rect blockRect = new Rect(blockX,    trackRect.y + 2, blockW,    trackRect.height - 4);
            Rect drawRect  = new Rect(clippedX,  trackRect.y + 2, clippedW,  trackRect.height - 4);

            Color blockCol = !entry.isEnabled ? _blockColorOff
                           : isSelected       ? Color.Lerp(trackColor, Color.white, 0.35f)
                                              : trackColor;
            EditorGUI.DrawRect(drawRect, blockCol);

            if (clippedW > 40)
            {
                if (_whiteMiniLabel == null)
                {
                    _whiteMiniLabel = new GUIStyle(EditorStyles.miniLabel);
                    _whiteMiniLabel.normal.textColor = Color.white;
                }
                GUI.Label(new Rect(clippedX + 4, drawRect.y, clippedW - 8, drawRect.height),
                    BlockLabel(entry, ctrl.transform), _whiteMiniLabel);
            }

            Event e = Event.current;

            // Right-click → delete context menu
            if (e.type == EventType.ContextClick && blockRect.Contains(e.mousePosition))
            {
                var menu = new GenericMenu();
                var cap  = entry;
                menu.AddItem(new GUIContent("Delete"), false, () =>
                {
                    Undo.RecordObject(ctrl.Clip, "Remove Tween Entry");
                    seq.entries.Remove(cap);
                    if (_state.SelectedEntry == cap) _state.SelectedEntry = null;
                    EditorUtility.SetDirty(ctrl.Clip);
                    Repaint();
                });
                menu.ShowAsContext();
                e.Use();
                return;
            }

            // Left-click / drag
            if (e.type == EventType.MouseDown && e.button == 0)
            {
                Rect leftHandle  = new Rect(blockRect.x, blockRect.y, HandleWidth, blockRect.height);
                Rect rightHandle = new Rect(blockRect.xMax - HandleWidth, blockRect.y, HandleWidth, blockRect.height);

                if (leftHandle.Contains(e.mousePosition))
                    BeginDrag(DragMode.ResizeLeft, entry);
                else if (rightHandle.Contains(e.mousePosition))
                    BeginDrag(DragMode.ResizeRight, entry);
                else if (blockRect.Contains(e.mousePosition))
                {
                    _state.SelectedEntry = entry;
                    BeginDrag(DragMode.MoveBlock, entry);
                }
            }

            EditorGUIUtility.AddCursorRect(new Rect(blockRect.x, blockRect.y, HandleWidth, blockRect.height), MouseCursor.ResizeHorizontal);
            EditorGUIUtility.AddCursorRect(new Rect(blockRect.xMax - HandleWidth, blockRect.y, HandleWidth, blockRect.height), MouseCursor.ResizeHorizontal);
            EditorGUIUtility.AddCursorRect(blockRect, MouseCursor.Pan);

            // Floating duration label while resizing
            bool isResizing = _dragEntry == entry &&
                (_dragMode == DragMode.ResizeLeft || _dragMode == DragMode.ResizeRight);
            if (isResizing && Event.current.type == EventType.Repaint)
            {
                string dLabel  = $"{entry.EffectiveDuration:F2}s";
                Vector2 dSize  = EditorStyles.miniLabel.CalcSize(new GUIContent(dLabel));
                float   dX     = Mathf.Clamp(blockRect.x + blockW * 0.5f - dSize.x * 0.5f,
                                     trackRect.x, trackRect.xMax - dSize.x);
                Rect    bgRect = new Rect(dX - 2, blockRect.y - dSize.y - 2, dSize.x + 4, dSize.y + 2);
                EditorGUI.DrawRect(bgRect, new Color(0f, 0f, 0f, 0.75f));
                GUI.Label(new Rect(dX, bgRect.y + 1, dSize.x, dSize.y), dLabel, EditorStyles.miniLabel);
            }
        }

        // ─── Add property context menu ─────────────────────────────────────────
        private void ShowAddPropertyMenu(TweenAnimatorController ctrl, TweenSequenceData seq)
        {
            var props = ComponentPropertyScanner.Scan(ctrl.transform);
            var menu  = new GenericMenu();

            foreach (var prop in props)
            {
                var capturedProp = prop;
                string label = string.IsNullOrEmpty(prop.HierarchyPath)
                    ? $"{prop.ComponentShortName}/{prop.DisplayName}"
                    : $"{prop.HierarchyPath}/{prop.ComponentShortName}/{prop.DisplayName}";

                menu.AddItem(new GUIContent(label), false, () =>
                {
                    var binding = new TweenPropertyBinding
                    {
                        hierarchyPath     = capturedProp.HierarchyPath,
                        componentTypeName = capturedProp.ComponentTypeName,
                        propertyName      = capturedProp.PropertyName,
                        axis              = PropertyAxis.None
                    };
                    AddEntry(ctrl, seq, binding);
                });
            }

            if (menu.GetItemCount() == 0)
                menu.AddDisabledItem(new GUIContent("No supported properties found"));

            menu.ShowAsContext();
        }

        // ─── Chain context menu ────────────────────────────────────────────────
        private void ShowChainMenu(TweenAnimatorController ctrl, TweenSequenceData seq, List<TweenEntryData> track)
        {
            var props = ComponentPropertyScanner.Scan(ctrl.transform);
            var menu  = new GenericMenu();

            foreach (var prop in props)
            {
                var capturedProp  = prop;
                var capturedTrack = track;
                string label = string.IsNullOrEmpty(prop.HierarchyPath)
                    ? $"{prop.ComponentShortName}/{prop.DisplayName}"
                    : $"{prop.HierarchyPath}/{prop.ComponentShortName}/{prop.DisplayName}";

                menu.AddItem(new GUIContent(label), false, () =>
                    AddChainEntry(ctrl, seq, capturedTrack, capturedProp));
            }

            if (menu.GetItemCount() == 0)
                menu.AddDisabledItem(new GUIContent("No supported properties found"));

            menu.ShowAsContext();
        }

        private void AddChainEntry(TweenAnimatorController ctrl, TweenSequenceData seq,
                                   List<TweenEntryData> track, DiscoveredProperty prop)
        {
            float chainDelay = 0f;
            foreach (var e in track)
                if (e.EndTime > chainDelay) chainDelay = e.EndTime;

            var binding = new TweenPropertyBinding
            {
                hierarchyPath     = prop.HierarchyPath,
                componentTypeName = prop.ComponentTypeName,
                propertyName      = prop.PropertyName,
            };

            var entry = new TweenEntryData
            {
                binding    = binding,
                delay      = chainDelay,
                trackId    = track[0].trackId,
                trackColor = track[0].trackColor,
                startValue = PropertyValueUnion.DefaultForType(prop.ValueType),
                endValue   = PropertyValueUnion.DefaultForType(prop.ValueType),
            };

            Undo.RecordObject(ctrl.Clip, "Add Chain Tween Entry");
            seq.entries.Add(entry);
            _state.SelectedEntry = entry;
            EditorUtility.SetDirty(ctrl.Clip);
            Repaint();
        }

        // ─── Drag processing ───────────────────────────────────────────────────
        private void BeginDrag(DragMode mode, TweenEntryData entry)
        {
            _dragMode          = mode;
            _dragEntry         = entry;
            _dragStartMouseX   = Event.current.mousePosition.x;
            _dragStartDelay    = entry.delay;
            _dragStartDuration = entry.EffectiveDuration;
            _dragAccumulatedY  = 0f;
            Event.current.Use();
        }

        private void HandleDrag()
        {
            Event e = Event.current;

            // Scrub drag
            if (_scrubDragging)
            {
                if (e.type == EventType.MouseDrag)
                {
                    float t = (e.mousePosition.x - LabelWidth) / _pixelsPerSec + _timelineScrollX;
                    _state.GotoTime(t);
                    e.Use();
                    Repaint();
                }
                else if (e.type == EventType.MouseUp)
                {
                    _scrubDragging = false;
                    e.Use();
                }
                return;
            }

            if (_dragMode == DragMode.None || _dragEntry == null) return;

            if (e.type == EventType.MouseDrag)
            {
                float deltaSecs = (e.mousePosition.x - _dragStartMouseX) / _pixelsPerSec;
                Undo.RecordObject(_state.Controller.Clip, "Edit Tween Timing");

                switch (_dragMode)
                {
                    case DragMode.MoveBlock:
                    {
                        float desiredDelay = SnapValue(Mathf.Max(0f, _dragStartDelay + deltaSecs), e.control);
                        float dur          = _dragStartDuration;
                        FindTrackGap(_dragEntry, desiredDelay, dur, out float prevEnd, out float nextStart);
                        _dragEntry.delay = Mathf.Clamp(desiredDelay, prevEnd, Mathf.Max(prevEnd, nextStart - dur));

                        _dragAccumulatedY += e.delta.y;
                        if (Mathf.Abs(_dragAccumulatedY) >= TimelineHeight)
                        {
                            int dir        = _dragAccumulatedY > 0f ? 1 : -1;
                            int currentIdx = _cachedTracks.FindIndex(t => t.Contains(_dragEntry));
                            if (currentIdx >= 0)
                            {
                                int    targetIdx  = currentIdx + dir;
                                string newTrackId = (targetIdx < 0 || targetIdx >= _cachedTracks.Count)
                                    ? System.Guid.NewGuid().ToString()
                                    : _cachedTracks[targetIdx][0].trackId;

                                _dragEntry.trackId = newTrackId;
                                _dragAccumulatedY -= dir * TimelineHeight;
                            }
                        }
                        break;
                    }
                    case DragMode.ResizeLeft:
                    {
                        float newDelay  = SnapValue(Mathf.Clamp(_dragStartDelay + deltaSecs,
                            0f, _dragStartDelay + _dragStartDuration - 0.05f), e.control);
                        float newEffDur = _dragStartDuration - (newDelay - _dragStartDelay);
                        FindTrackGap(_dragEntry, newDelay, newEffDur, out float prevEnd, out _);
                        newDelay        = Mathf.Max(newDelay, prevEnd);
                        newEffDur       = _dragStartDuration - (newDelay - _dragStartDelay);
                        _dragEntry.duration = newEffDur * Mathf.Max(0.001f, _dragEntry.speed);
                        _dragEntry.delay    = newDelay;
                        break;
                    }
                    case DragMode.ResizeRight:
                    {
                        float newEffDur = SnapValue(Mathf.Max(0.05f, _dragStartDuration + deltaSecs), e.control);
                        FindTrackGap(_dragEntry, _dragEntry.delay, newEffDur, out _, out float nextStart);
                        newEffDur = Mathf.Min(newEffDur, Mathf.Max(0.05f, nextStart - _dragEntry.delay));
                        _dragEntry.duration = newEffDur * Mathf.Max(0.001f, _dragEntry.speed);
                        break;
                    }
                }

                EditorUtility.SetDirty(_state.Controller.Clip);
                e.Use();
                Repaint();
            }
            else if (e.type == EventType.MouseUp)
            {
                _dragMode  = DragMode.None;
                _dragEntry = null;
                e.Use();
            }
        }

        private void FindTrackGap(TweenEntryData dragged, float desiredDelay, float duration,
                                   out float prevEnd, out float nextStart)
        {
            prevEnd   = 0f;
            nextStart = float.MaxValue;

            foreach (var track in _cachedTracks)
            {
                if (!track.Contains(dragged)) continue;
                foreach (var e in track)
                {
                    if (e == dragged) continue;
                    if (e.delay < desiredDelay)
                        prevEnd   = Mathf.Max(prevEnd,   e.EndTime);
                    else
                        nextStart = Mathf.Min(nextStart, e.delay);
                }
                break;
            }
        }

        // ─── Entry inspector ───────────────────────────────────────────────────
        private void DrawEntryInspector(TweenEntryData entry, TweenAnimatorController ctrl)
        {
            GUILayout.FlexibleSpace();
            GUILayout.BeginVertical(EditorStyles.helpBox);
            
            GUILayout.Space(5);

            bool isMissing = _missingEntryIds.Contains(entry.entryId);
            if (isMissing)
            {
                var prevColor = GUI.backgroundColor;
                GUI.backgroundColor = new Color(0.9f, 0.3f, 0.3f, 1f);
                GUILayout.BeginHorizontal(EditorStyles.helpBox);
                GUI.backgroundColor = prevColor;
                GUILayout.Label(EditorGUIUtility.IconContent("console.warnicon.sml"), GUILayout.Width(20));
                GUILayout.Label(
                    $"Missing: \"{entry.binding?.hierarchyPath}\" not found. Object may have been deleted.",
                    EditorStyles.wordWrappedMiniLabel);
                GUILayout.EndHorizontal();
            }

            GUILayout.BeginHorizontal();
            GUILayout.BeginHorizontal();
            GUILayout.Label("Name", GUILayout.Width(40));
            string defaultName   = BlockLabel(entry, ctrl.transform);
            string displayedName = string.IsNullOrEmpty(entry.displayName) ? defaultName : entry.displayName;
            string newName = EditorGUILayout.DelayedTextField(displayedName, GUILayout.Width(200));
            if (newName != displayedName)
            {
                Undo.RecordObject(ctrl.Clip, "Rename Tween Entry");
                entry.displayName = (newName == defaultName) ? string.Empty : newName;
                EditorUtility.SetDirty(ctrl.Clip);
            }
            if (!string.IsNullOrEmpty(entry.displayName) && GUILayout.Button(new GUIContent("↺", "Reset name to default"), EditorStyles.miniButton, GUILayout.Width(20)))
            {
                Undo.RecordObject(ctrl.Clip, "Reset Tween Entry Name");
                entry.displayName = string.Empty;
                EditorUtility.SetDirty(ctrl.Clip);
            }
            GUILayout.EndHorizontal();

            // Track color — applies to all entries sharing the same trackId
            var entryTrack = _cachedTracks.Find(t => t.Contains(entry));
            if (entryTrack != null)
            {
                GUILayout.BeginHorizontal();
                GUILayout.Label("Color", GUILayout.Width(40));
                Color newTrackColor = EditorGUILayout.ColorField(entryTrack[0].trackColor,GUILayout.Width(200));
                if (newTrackColor != entryTrack[0].trackColor)
                {
                    Undo.RecordObject(ctrl.Clip, "Change Track Color");
                    foreach (var te in entryTrack) te.trackColor = newTrackColor;
                    EditorUtility.SetDirty(ctrl.Clip);
                }
                GUILayout.EndHorizontal();
            }
            GUILayout.FlexibleSpace();
            GUILayout.EndHorizontal();
            
            EditorGUI.BeginChangeCheck();
            EditorGUI.BeginDisabledGroup(isMissing);

            // ── Timing row ────────────────────────────────────────────────────
            GUILayout.BeginHorizontal();
            GUILayout.Label("Delay", GUILayout.Width(38));
            float newDelay = EditorGUILayout.DelayedFloatField(entry.delay, GUILayout.Width(52));
            if (newDelay != entry.delay) entry.delay = SnapValue(Mathf.Max(0f, newDelay));
            GUILayout.Space(8);
            GUILayout.Label("Duration", GUILayout.Width(55));
            float newDur = EditorGUILayout.DelayedFloatField(entry.duration, GUILayout.Width(52));
            if (newDur != entry.duration) entry.duration = SnapValue(Mathf.Max(0.01f, newDur));
            if (Mathf.Abs(entry.speed - 1f) > 0.001f)
                GUILayout.Label($"= {entry.EffectiveDuration:F2}s", EditorStyles.miniLabel);
            GUILayout.EndHorizontal();

            GUILayout.BeginHorizontal();

            GUILayout.BeginVertical(GUILayout.Width(position.width * 0.5f));
            entry.ease     = (DG.Tweening.Ease)    EditorGUILayout.EnumPopup ("Ease",       entry.ease);
            entry.loopType = (DG.Tweening.LoopType) EditorGUILayout.EnumPopup("Loop Type",  entry.loopType);
            entry.loops    = EditorGUILayout.IntField("Loops",  entry.loops);
            entry.speed    = Mathf.Max(0.001f, EditorGUILayout.FloatField("Speed", entry.speed));
            entry.useCurrentAsStart = EditorGUILayout.Toggle("Use Current As Start", entry.useCurrentAsStart);

            var desc = entry.binding != null
                ? PropertyAccessorRegistry.GetDescriptor(entry.binding.componentTypeName, entry.binding.propertyName)
                : null;
            if (desc != null && desc.ExtraParam == ExtraParamType.RotateMode)
                entry.rotateMode = (DG.Tweening.RotateMode) EditorGUILayout.EnumPopup("Rotate Mode", entry.rotateMode);

            GUILayout.EndVertical();

            GUILayout.BeginVertical();
            EditorGUI.BeginDisabledGroup(entry.useCurrentAsStart);
            DrawStartValueField(entry, ctrl);
            EditorGUI.EndDisabledGroup();
            GUILayout.BeginHorizontal();
            DrawCaptureButton(ctrl, entry, isStart: false);
            GUILayout.Label("End", GUILayout.Width(35));
            DrawValueFieldNoLabel(ref entry.endValue);
            GUILayout.EndHorizontal();
            GUILayout.EndVertical();

            GUILayout.EndHorizontal();

            EditorGUI.EndDisabledGroup();

            if (EditorGUI.EndChangeCheck())
            {
                Undo.RecordObject(ctrl.Clip, "Edit Tween Entry");
                EditorUtility.SetDirty(ctrl.Clip);
            }

            GUILayout.EndVertical();
        }

        private static string TrackLabel(TweenEntryData entry, Transform root) =>
            string.IsNullOrEmpty(entry.displayName)
                ? ObjectName(entry.binding, root)
                : entry.displayName;

        private static string BlockLabel(TweenEntryData entry, Transform root)
        {
            if (!string.IsNullOrEmpty(entry.displayName)) return entry.displayName;
            if (entry.binding == null) return "?";
            string objName = ObjectName(entry.binding, root);
            string propDisplay = entry.binding.propertyName;
            foreach (var d in PropertyAccessorRegistry.GetSupportedProperties(entry.binding.componentTypeName))
                if (d.PropertyName == entry.binding.propertyName) { propDisplay = d.DisplayName; break; }
            return $"{objName} - {propDisplay}";
        }

        private static string ObjectName(TweenPropertyBinding binding, Transform root)
        {
            if (binding == null) return "?";
            if (string.IsNullOrEmpty(binding.hierarchyPath)) return root.name;
            int slash = binding.hierarchyPath.LastIndexOf('/');
            return slash >= 0 ? binding.hierarchyPath.Substring(slash + 1) : binding.hierarchyPath;
        }

        private void DrawStartValueField(TweenEntryData entry, TweenAnimatorController ctrl)
        {
            if (!string.IsNullOrEmpty(entry.linkedStartEntryId))
            {
                var linked = ctrl.Sequence?.entries.Find(e => e.entryId == entry.linkedStartEntryId);
                string linkLabel = linked != null ? TrackLabel(linked, ctrl.transform) : "Missing Link";

                GUILayout.BeginHorizontal();
                DrawCaptureButton(ctrl, entry, isStart: true);
                GUILayout.Label("Start", GUILayout.Width(35));
                GUILayout.Label($"→ {linkLabel}", EditorStyles.miniLabel);
                GUILayout.FlexibleSpace();
                if (GUILayout.Button(new GUIContent("×", "Unlink start value"), EditorStyles.miniButton, GUILayout.Width(20)))
                {
                    Undo.RecordObject(ctrl.Clip, "Unlink Start Value");
                    entry.linkedStartEntryId = null;
                    EditorUtility.SetDirty(ctrl.Clip);
                }
                GUILayout.EndHorizontal();
            }
            else
            {
                GUILayout.BeginHorizontal();
                DrawCaptureButton(ctrl, entry, isStart: true);
                GUILayout.Label("Start", GUILayout.Width(35));
                DrawValueFieldNoLabel(ref entry.startValue);
                if (GUILayout.Button(new GUIContent("🔗", "Link start value to another entry's end value"), EditorStyles.miniButton, GUILayout.Width(24)))
                    ShowLinkMenu(ctrl, entry);
                GUILayout.EndHorizontal();
            }
        }

        private void DrawCaptureButton(TweenAnimatorController ctrl, TweenEntryData entry, bool isStart)
        {
            var prev = GUI.backgroundColor;
            GUI.backgroundColor = new Color(0.85f, 0.18f, 0.18f, 1f);
            if (GUILayout.Button(new GUIContent("●", isStart ? "Capture current value as Start" : "Capture current value as End"), EditorStyles.miniButton, GUILayout.Width(22)))
                CaptureValue(ctrl, entry, isStart);
            GUI.backgroundColor = prev;
        }

        private static void DrawValueFieldNoLabel(ref PropertyValueUnion value)
        {
            switch (value.type)
            {
                case PropertyType.Float:
                    value.floatValue   = EditorGUILayout.FloatField(value.floatValue);                        break;
                case PropertyType.Vector2:
                    value.vector2Value = EditorGUILayout.Vector2Field(GUIContent.none, value.vector2Value);   break;
                case PropertyType.Vector3:
                    value.vector3Value = EditorGUILayout.Vector3Field(GUIContent.none, value.vector3Value);   break;
                case PropertyType.Color:
                    value.colorValue   = EditorGUILayout.ColorField(GUIContent.none, value.colorValue);       break;
            }
        }

        private void ShowLinkMenu(TweenAnimatorController ctrl, TweenEntryData entry)
        {
            var menu = new GenericMenu();
            foreach (var other in ctrl.Sequence.entries)
            {
                if (other.entryId == entry.entryId) continue;
                if (other.endValue.type != entry.startValue.type) continue;
                var cap = other;
                string label = TrackLabel(other, ctrl.transform);
                menu.AddItem(new GUIContent(label), false, () =>
                {
                    Undo.RecordObject(ctrl.Clip, "Link Start Value");
                    entry.linkedStartEntryId = cap.entryId;
                    EditorUtility.SetDirty(ctrl.Clip);
                    Repaint();
                });
            }
            if (menu.GetItemCount() == 0)
                menu.AddDisabledItem(new GUIContent("No compatible entries"));
            menu.ShowAsContext();
        }

        private void DrawValueField(string label, ref PropertyValueUnion value)
        {
            switch (value.type)
            {
                case PropertyType.Float:
                    value.floatValue   = EditorGUILayout.FloatField(label, value.floatValue);   break;
                case PropertyType.Vector2:
                    value.vector2Value = EditorGUILayout.Vector2Field(label, value.vector2Value); break;
                case PropertyType.Vector3:
                    value.vector3Value = EditorGUILayout.Vector3Field(label, value.vector3Value); break;
                case PropertyType.Color:
                    value.colorValue   = EditorGUILayout.ColorField(label, value.colorValue);    break;
            }
        }

        // ─── Actions ───────────────────────────────────────────────────────────
        private void AddEntry(TweenAnimatorController ctrl, TweenSequenceData seq, TweenPropertyBinding binding)
        {
            Undo.RecordObject(ctrl.Clip, "Add Tween Entry");

            var entry = new TweenEntryData
            {
                binding    = binding,
                trackColor = PickTrackColor(seq),
            };

            var descriptors = PropertyAccessorRegistry.GetSupportedProperties(binding.componentTypeName);
            foreach (var d in descriptors)
            {
                if (d.PropertyName == binding.propertyName)
                {
                    entry.startValue = PropertyValueUnion.DefaultForType(d.ValueType);
                    entry.endValue   = PropertyValueUnion.DefaultForType(d.ValueType);
                    break;
                }
            }

            seq.entries.Add(entry);
            _state.SelectedEntry = entry;
            EditorUtility.SetDirty(ctrl.Clip);
        }

        private void CaptureValue(TweenAnimatorController ctrl, TweenEntryData entry, bool isStart)
        {
            if (entry.binding == null) return;

            var accessor = PropertyAccessorRegistry.Get(entry.binding.componentTypeName, entry.binding.propertyName);
            if (accessor == null) return;

            Transform t = string.IsNullOrEmpty(entry.binding.hierarchyPath)
                ? ctrl.transform
                : ctrl.transform.Find(entry.binding.hierarchyPath);
            if (t == null) return;

            var type = System.Type.GetType(entry.binding.componentTypeName);
            if (type == null)
                foreach (var asm in System.AppDomain.CurrentDomain.GetAssemblies())
                {
                    type = asm.GetType(entry.binding.componentTypeName);
                    if (type != null) break;
                }
            if (type == null) return;

            var component = t.GetComponent(type);
            if (component == null) return;

            Undo.RecordObject(ctrl.Clip, isStart ? "Capture Start" : "Capture End");
            var captured = accessor.ReadValue(component);
            if (isStart) entry.startValue = captured;
            else         entry.endValue   = captured;
            EditorUtility.SetDirty(ctrl.Clip);
        }

        private static Color PickTrackColor(TweenSequenceData seq)
        {
            var tracks = GetTrackGroups(seq.entries);
            var used   = new System.Collections.Generic.HashSet<Color>();
            foreach (var t in tracks)
                used.Add(t[0].trackColor);
            foreach (var c in _palette)
                if (!used.Contains(c)) return c;
            return _palette[tracks.Count % _palette.Length];
        }

        // ─── Asset creation ────────────────────────────────────────────────────
        private static TweenAnimatorClip CreateNewClip(string baseName)
        {
            string path = EditorUtility.SaveFilePanelInProject(
                "Create Tween Clip", baseName + "_TweenClip", "asset", "Save Tween Clip asset");
            if (string.IsNullOrEmpty(path)) return null;

            var clip = CreateInstance<TweenAnimatorClip>();
            AssetDatabase.CreateAsset(clip, path);
            AssetDatabase.SaveAssets();
            return clip;
        }

        // ─── Styles ────────────────────────────────────────────────────────────
        private static void InitStyles()
        {
            if (_blockStyle != null) return;

            _blockStyle = new GUIStyle(GUI.skin.box)
            {
                alignment = TextAnchor.MiddleCenter,
                fontSize  = 10,
                normal    = { textColor = Color.white }
            };

            _labelStyle = new GUIStyle(EditorStyles.label) { fontSize = 11 };

            _headerStyle = new GUIStyle(EditorStyles.boldLabel) { fontSize = 11 };
        }

        private static Color rulerColor => new Color(0.15f, 0.15f, 0.15f, 1f);
    }
}
