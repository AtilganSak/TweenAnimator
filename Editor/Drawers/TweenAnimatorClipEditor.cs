using UnityEditor;
using UnityEngine;

namespace TweenAnimator.Editor
{
    [CustomEditor(typeof(TweenAnimatorClip))]
    public class TweenAnimatorClipEditor : UnityEditor.Editor
    {
        private const string PrefSamples   = "TweenAnimator.Convert.SamplesPerSecond";
        private const string PrefReduce    = "TweenAnimator.Convert.ReduceKeys";
        private const string PrefTolerance = "TweenAnimator.Convert.ReductionTolerance";
        private const string PrefAnimName  = "TweenAnimator.Convert.AnimName";

        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();

            GUILayout.Space(12f);
            EditorGUILayout.LabelField("Export Settings", EditorStyles.boldLabel);

            var clip = (TweenAnimatorClip)target;

            // Load persisted settings
            int    samples   = EditorPrefs.GetInt(PrefSamples,    ConvertSettings.Default.SamplesPerSecond);
            bool   reduce    = EditorPrefs.GetBool(PrefReduce,    ConvertSettings.Default.ReduceKeys);
            float  tolerance = EditorPrefs.GetFloat(PrefTolerance, ConvertSettings.Default.ReductionTolerance);
            string animName  = EditorPrefs.GetString(PrefAnimName, "");
            if (string.IsNullOrWhiteSpace(animName)) animName = clip.name;

            EditorGUI.BeginChangeCheck();

            // Animation name
            string newName = EditorGUILayout.TextField(
                new GUIContent("Animation Name", "Name of the exported .anim file."),
                animName);

            // Samples per second
            int newSamples = EditorGUILayout.IntSlider(
                new GUIContent("Samples / Second",
                    "Keyframes sampled per second of animation. " +
                    "Linear ease with Reduce Keys on converges to 2 keys regardless."),
                samples, 2, 120);

            // Key reduction
            bool newReduce = EditorGUILayout.Toggle(
                new GUIContent("Reduce Keys",
                    "Remove keyframes whose deviation from linear interpolation is within tolerance. " +
                    "Dramatically cuts key count on linear / ease-in-out sections."),
                reduce);

            float newTolerance = tolerance;
            if (newReduce)
            {
                EditorGUI.indentLevel++;
                newTolerance = EditorGUILayout.Slider(
                    new GUIContent("Tolerance",
                        "Max allowed value error when removing a keyframe. " +
                        "0.001 is imperceptible for most properties."),
                    tolerance, 0.00001f, 0.1f);
                EditorGUI.indentLevel--;
            }

            if (EditorGUI.EndChangeCheck())
            {
                EditorPrefs.SetInt(PrefSamples,    newSamples);
                EditorPrefs.SetBool(PrefReduce,    newReduce);
                EditorPrefs.SetFloat(PrefTolerance, newTolerance);
                EditorPrefs.SetString(PrefAnimName, newName);
            }

            GUILayout.Space(6f);
            if (GUILayout.Button("Convert to AnimationClip", GUILayout.Height(28f)))
            {
                var settings = new ConvertSettings
                {
                    SamplesPerSecond   = newSamples,
                    ReduceKeys         = newReduce,
                    ReductionTolerance = newTolerance,
                };
                TweenClipToAnimationClipConverter.ConvertAndSave(clip, settings, newName);
            }
        }
    }
}
