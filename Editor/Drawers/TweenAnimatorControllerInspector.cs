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

            // Clip field
            EditorGUI.BeginChangeCheck();
            var newClip = (TweenAnimatorClip)EditorGUILayout.ObjectField(
                "Clip", ctrl.Clip, typeof(TweenAnimatorClip), false);
            if (EditorGUI.EndChangeCheck())
            {
                Undo.RecordObject(ctrl, "Assign Tween Clip");
                ctrl.SetClip(newClip);
                EditorUtility.SetDirty(ctrl);
            }

            if (ctrl.Clip == null)
            {
                GUILayout.Space(4);
                if (GUILayout.Button("Create New Clip", GUILayout.Height(24)))
                {
                    string path = EditorUtility.SaveFilePanelInProject(
                        "Create Tween Clip",
                        ctrl.gameObject.name + "_TweenClip",
                        "asset",
                        "Save Tween Clip asset");
                    if (!string.IsNullOrEmpty(path))
                    {
                        var clip = ScriptableObject.CreateInstance<TweenAnimatorClip>();
                        AssetDatabase.CreateAsset(clip, path);
                        AssetDatabase.SaveAssets();
                        Undo.RecordObject(ctrl, "Assign Tween Clip");
                        ctrl.SetClip(clip);
                        EditorUtility.SetDirty(ctrl);
                    }
                }
            }
            else
            {
                var seq = ctrl.Sequence;
                GUILayout.Space(6);

                // Sequence settings
                EditorGUI.BeginChangeCheck();
                float newTimeScale = EditorGUILayout.FloatField("Time Scale", seq.timeScale);
                bool newPlayOnAwake = EditorGUILayout.Toggle("Play On Awake", seq.playOnAwake);
                if (EditorGUI.EndChangeCheck())
                {
                    Undo.RecordObject(ctrl.Clip, "Edit Sequence Settings");
                    seq.timeScale = newTimeScale;
                    seq.playOnAwake = newPlayOnAwake;
                    EditorUtility.SetDirty(ctrl.Clip);
                }

                GUILayout.Space(4);

                // Read-only summary
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
    }
}