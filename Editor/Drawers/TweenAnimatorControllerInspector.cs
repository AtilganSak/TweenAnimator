using UnityEditor;
using UnityEngine;

namespace TweenAnimator.Editor
{
    [CustomEditor(typeof(TweenAnimatorController))]
    public class TweenAnimatorControllerInspector : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            var ctrl = (TweenAnimatorController)target;

            if (GUILayout.Button("Open Tween Animator", GUILayout.Height(28)))
                TweenAnimatorWindow.ShowWindow();

            GUILayout.Space(6);

            DrawClipList(ctrl);

            GUILayout.Space(6);

            DrawPlayOnAwake(ctrl);

            if (ctrl.Clip != null)
            {
                var seq = ctrl.Sequence;
                GUILayout.Space(6);

                EditorGUI.BeginChangeCheck();
                float newTimeScale = EditorGUILayout.FloatField("Time Scale", seq.timeScale);
                if (EditorGUI.EndChangeCheck())
                {
                    Undo.RecordObject(ctrl.Clip, "Edit Sequence Settings");
                    seq.timeScale = newTimeScale;
                    EditorUtility.SetDirty(ctrl.Clip);
                }

                GUILayout.Space(4);

                EditorGUI.BeginDisabledGroup(true);
                EditorGUILayout.LabelField("Entries", seq.entries.Count.ToString());
                EditorGUILayout.LabelField("Duration", $"{seq.TotalDuration:F2}s");
                EditorGUI.EndDisabledGroup();

                GUILayout.Space(4);

                // Runtime-only controls
                EditorGUI.BeginDisabledGroup(!EditorApplication.isPlaying);
                GUILayout.BeginHorizontal();
                if (GUILayout.Button("Play")) ctrl.Play();
                if (GUILayout.Button("Pause")) ctrl.Pause();
                if (GUILayout.Button("Stop")) ctrl.Stop();
                if (GUILayout.Button("Rewind")) ctrl.Rewind();
                GUILayout.EndHorizontal();
                EditorGUI.EndDisabledGroup();
            }
        }

        private static void DrawClipList(TweenAnimatorController ctrl)
        {
            EditorGUILayout.LabelField("Clips", EditorStyles.boldLabel);

            var clips = ctrl.Clips;
            int removeIndex = -1;

            for (int i = 0; i < clips.Count; i++)
            {
                GUILayout.BeginHorizontal();

                EditorGUI.BeginChangeCheck();
                string newName = EditorGUILayout.TextField(clips[i].name, GUILayout.MinWidth(60));
                var newAsset = (TweenAnimatorClip)EditorGUILayout.ObjectField(
                    clips[i].clip, typeof(TweenAnimatorClip), false, GUILayout.MinWidth(80));
                if (EditorGUI.EndChangeCheck())
                {
                    Undo.RecordObject(ctrl, "Edit Clip Slot");
                    clips[i].name = newName;
                    clips[i].clip = newAsset;
                    EditorUtility.SetDirty(ctrl);
                }

                bool isActive = i == ctrl.ActiveClipIndex;
                var prevColor = GUI.backgroundColor;
                if (isActive) GUI.backgroundColor = Color.green;
                EditorGUI.BeginDisabledGroup(isActive);
                if (GUILayout.Button(isActive ? "Active" : "Set Active", GUILayout.Width(70)))
                {
                    Undo.RecordObject(ctrl, "Set Active Clip");
                    ctrl.SetActiveClip(i);
                    EditorUtility.SetDirty(ctrl);
                }
                EditorGUI.EndDisabledGroup();
                GUI.backgroundColor = prevColor;

                if (GUILayout.Button("×", GUILayout.Width(20)))
                    removeIndex = i;

                GUILayout.EndHorizontal();
            }

            if (removeIndex >= 0)
            {
                Undo.RecordObject(ctrl, "Remove Clip");
                ctrl.RemoveClipAt(removeIndex);
                EditorUtility.SetDirty(ctrl);
            }

            if (GUILayout.Button("+ Add Clip Slot"))
            {
                Undo.RecordObject(ctrl, "Add Clip");
                ctrl.AddClip("Clip " + (clips.Count + 1), null);
                EditorUtility.SetDirty(ctrl);
            }
        }

        private static void DrawPlayOnAwake(TweenAnimatorController ctrl)
        {
            EditorGUI.BeginChangeCheck();
            bool newPlayOnAwake = EditorGUILayout.Toggle("Play On Awake", ctrl.PlayOnAwake);
            if (EditorGUI.EndChangeCheck())
            {
                Undo.RecordObject(ctrl, "Edit Play On Awake");
                ctrl.PlayOnAwake = newPlayOnAwake;
                EditorUtility.SetDirty(ctrl);
            }
        }
    }
}
