using System;
using System.Collections.Generic;
using System.IO;
using DG.Tweening;
using DG.Tweening.Core.Easing;
using UnityEditor;
using UnityEngine;

namespace TweenAnimator.Editor
{
    public struct ConvertSettings
    {
        public int   SamplesPerSecond;   // keyframes sampled per second (2–120)
        public bool  ReduceKeys;         // remove redundant keyframes via Douglas-Peucker
        public float ReductionTolerance; // max value error allowed when removing a key

        public static ConvertSettings Default => new ConvertSettings
        {
            SamplesPerSecond   = 30,
            ReduceKeys         = true,
            ReductionTolerance = 0.001f,
        };
    }

    public static class TweenClipToAnimationClipConverter
    {
        private struct EntrySegment
        {
            public float StartTime;
            public float EndTime;
            public float StartVal;
            public float EndVal;
            public Ease Ease;
            public AnimationCurve CustomCurve; // non-null when useCustomCurve
            public int Loops;
            public LoopType LoopType;
        }

        // (componentTypeName, tweenPropertyName) → Unity AnimationClip sub-property names
        private static readonly Dictionary<(string, string), string[]> s_PropNameMap =
            new Dictionary<(string, string), string[]>
        {
            // Transform
            { ("UnityEngine.Transform", "localPosition"),    new[] { "m_LocalPosition.x",      "m_LocalPosition.y",      "m_LocalPosition.z"      } },
            { ("UnityEngine.Transform", "localEulerAngles"), new[] { "localEulerAnglesRaw.x",   "localEulerAnglesRaw.y",   "localEulerAnglesRaw.z"   } },
            { ("UnityEngine.Transform", "localScale"),       new[] { "m_LocalScale.x",          "m_LocalScale.y",          "m_LocalScale.z"          } },
            // RectTransform
            { ("UnityEngine.RectTransform", "localPosition"),    new[] { "m_LocalPosition.x",    "m_LocalPosition.y",    "m_LocalPosition.z"    } },
            { ("UnityEngine.RectTransform", "localEulerAngles"), new[] { "localEulerAnglesRaw.x", "localEulerAnglesRaw.y", "localEulerAnglesRaw.z" } },
            { ("UnityEngine.RectTransform", "localScale"),       new[] { "m_LocalScale.x",        "m_LocalScale.y",        "m_LocalScale.z"        } },
            { ("UnityEngine.RectTransform", "anchoredPosition"), new[] { "m_AnchoredPosition.x",  "m_AnchoredPosition.y"                           } },
            { ("UnityEngine.RectTransform", "sizeDelta"),        new[] { "m_SizeDelta.x",         "m_SizeDelta.y"                                  } },
            // CanvasGroup
            { ("UnityEngine.CanvasGroup", "alpha"), new[] { "m_Alpha" } },
            // Image
            { ("UnityEngine.UI.Image", "color"),      new[] { "m_Color.r", "m_Color.g", "m_Color.b", "m_Color.a" } },
            { ("UnityEngine.UI.Image", "fillAmount"), new[] { "m_FillAmount" } },
            // SpriteRenderer
            { ("UnityEngine.SpriteRenderer", "color"), new[] { "m_Color.r", "m_Color.g", "m_Color.b", "m_Color.a" } },
            // Light
            { ("UnityEngine.Light", "intensity"), new[] { "m_Intensity" } },
            // AudioSource
            { ("UnityEngine.AudioSource", "volume"), new[] { "m_Volume" } },
            { ("UnityEngine.AudioSource", "pitch"),  new[] { "m_Pitch"  } },
            // TextMeshPro (world space)
            { ("TMPro.TextMeshPro", "color"),           new[] { "m_fontColor.r", "m_fontColor.g", "m_fontColor.b", "m_fontColor.a" } },
            { ("TMPro.TextMeshPro", "alpha"),            new[] { "m_fontColor.a"       } },
            { ("TMPro.TextMeshPro", "fontSize"),         new[] { "m_fontSize"          } },
            { ("TMPro.TextMeshPro", "characterSpacing"), new[] { "m_characterSpacing"  } },
            { ("TMPro.TextMeshPro", "wordSpacing"),      new[] { "m_wordSpacing"       } },
            { ("TMPro.TextMeshPro", "lineSpacing"),      new[] { "m_lineSpacing"       } },
            // TextMeshProUGUI
            { ("TMPro.TextMeshProUGUI", "color"),           new[] { "m_fontColor.r", "m_fontColor.g", "m_fontColor.b", "m_fontColor.a" } },
            { ("TMPro.TextMeshProUGUI", "alpha"),            new[] { "m_fontColor.a"       } },
            { ("TMPro.TextMeshProUGUI", "fontSize"),         new[] { "m_fontSize"          } },
            { ("TMPro.TextMeshProUGUI", "characterSpacing"), new[] { "m_characterSpacing"  } },
            { ("TMPro.TextMeshProUGUI", "wordSpacing"),      new[] { "m_wordSpacing"       } },
            { ("TMPro.TextMeshProUGUI", "lineSpacing"),      new[] { "m_lineSpacing"       } },
        };

        // ─── Menu ──────────────────────────────────────────────────────────────

        [MenuItem("Assets/TweenAnimator/Convert to AnimationClip")]
        private static void MenuConvert()
        {
            var clip = Selection.activeObject as TweenAnimatorClip;
            ConvertAndSave(clip, ConvertSettings.Default, clip != null ? clip.name : null);
        }

        [MenuItem("Assets/TweenAnimator/Convert to AnimationClip", true)]
        private static bool MenuConvertValidate() => Selection.activeObject is TweenAnimatorClip;

        // ─── Public API ────────────────────────────────────────────────────────

        public static void ConvertAndSave(TweenAnimatorClip tweenClip, ConvertSettings settings,
                                           string animName = null)
        {
            if (tweenClip == null) return;

            string defaultName = string.IsNullOrWhiteSpace(animName) ? tweenClip.name : animName.Trim();
            string srcPath     = AssetDatabase.GetAssetPath(tweenClip);
            string defaultDir  = Path.GetDirectoryName(Path.GetFullPath(srcPath));

            string absPath = EditorUtility.SaveFilePanel(
                "Save AnimationClip", defaultDir, defaultName + ".anim", "anim");

            if (string.IsNullOrEmpty(absPath)) return; // user cancelled

            // Convert absolute path to project-relative asset path
            string projectRoot = Path.GetFullPath(Application.dataPath + "/..");
            string assetPath   = absPath.Replace('\\', '/');
            string rootPrefix  = projectRoot.Replace('\\', '/').TrimEnd('/') + '/';
            if (assetPath.StartsWith(rootPrefix, StringComparison.OrdinalIgnoreCase))
                assetPath = assetPath.Substring(rootPrefix.Length);

            if (!assetPath.StartsWith("Assets/", StringComparison.OrdinalIgnoreCase))
            {
                EditorUtility.DisplayDialog("TweenAnimator",
                    "Save location must be inside the project's Assets folder.", "OK");
                return;
            }

            var animClip = ConvertToAnimationClip(tweenClip, settings);
            animClip.name = Path.GetFileNameWithoutExtension(absPath);

            AssetDatabase.CreateAsset(animClip, assetPath);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            EditorUtility.FocusProjectWindow();
            Selection.activeObject = animClip;
            Debug.Log($"[TweenAnimator] AnimationClip saved: {assetPath}", animClip);
        }

        public static AnimationClip ConvertToAnimationClip(TweenAnimatorClip tweenClip, ConvertSettings settings)
        {
            settings.SamplesPerSecond = Mathf.Clamp(settings.SamplesPerSecond, 2, 120);

            var data     = tweenClip.Data;
            var animClip = new AnimationClip { name = tweenClip.name };

            var endById = new Dictionary<string, PropertyValueUnion>();
            foreach (var e in data.entries)
                endById[e.entryId] = e.endValue;

            var channels = new Dictionary<(string path, string typeName, string propName), List<EntrySegment>>();

            int skipped = 0;
            foreach (var entry in data.entries)
            {
                if (!entry.isEnabled || entry.binding == null) continue;

                if (entry.useCurrentAsStart)
                {
                    Debug.LogWarning($"[TweenAnimator → AnimationClip] \"{entry.displayName}\": useCurrentAsStart=true — explicit start required for baking. Entry skipped.");
                    skipped++;
                    continue;
                }

                var propKey = (entry.binding.componentTypeName, entry.binding.propertyName);
                if (!s_PropNameMap.TryGetValue(propKey, out var unityProps))
                {
                    string pn = entry.binding.propertyName;
                    if (pn.StartsWith("mat_float:") || pn.StartsWith("mat_color:"))
                        Debug.LogWarning($"[TweenAnimator → AnimationClip] \"{entry.displayName}\": material shader properties cannot be converted. Entry skipped.");
                    else
                        Debug.LogWarning($"[TweenAnimator → AnimationClip] \"{entry.displayName}\": no mapping for {entry.binding.componentTypeName}.{pn}. Entry skipped.");
                    skipped++;
                    continue;
                }

                PropertyValueUnion startVal = entry.startValue;
                if (!string.IsNullOrEmpty(entry.linkedStartEntryId) &&
                    endById.TryGetValue(entry.linkedStartEntryId, out var linked))
                    startVal = linked;

                int effectiveLoops = entry.loops < 0 ? 1 : Mathf.Max(1, entry.loops);
                if (entry.loops < 0)
                    Debug.LogWarning($"[TweenAnimator → AnimationClip] \"{entry.displayName}\": infinite loop — only first iteration baked.");

                float singleDuration = entry.EffectiveDuration;
                float[] startCh = ExtractChannels(startVal, unityProps.Length);
                float[] endCh   = ExtractChannels(entry.endValue, unityProps.Length);

                for (int ci = 0; ci < unityProps.Length; ci++)
                {
                    var key = (entry.binding.hierarchyPath ?? "", entry.binding.componentTypeName, unityProps[ci]);
                    if (!channels.TryGetValue(key, out var list))
                    {
                        list = new List<EntrySegment>();
                        channels[key] = list;
                    }
                    list.Add(new EntrySegment
                    {
                        StartTime = entry.delay,
                        EndTime   = entry.delay + singleDuration * effectiveLoops,
                        StartVal  = startCh[ci],
                        EndVal    = endCh[ci],
                        Ease        = entry.ease,
                        CustomCurve = entry.useCustomCurve ? entry.customEaseCurve : null,
                        Loops       = effectiveLoops,
                        LoopType  = entry.loopType,
                    });
                }
            }

            foreach (var kvp in channels)
            {
                var (path, typeName, propName) = kvp.Key;
                var type = ResolveType(typeName);
                if (type == null)
                {
                    Debug.LogWarning($"[TweenAnimator → AnimationClip] Cannot resolve type '{typeName}'. Curve skipped.");
                    continue;
                }
                animClip.SetCurve(path, type, propName, BuildCombinedCurve(kvp.Value, settings));
            }

            if (skipped > 0)
                Debug.LogWarning($"[TweenAnimator → AnimationClip] {skipped} entr(ies) skipped. See above for details.");

            return animClip;
        }

        // ─── Curve building ────────────────────────────────────────────────────

        private static AnimationCurve BuildCombinedCurve(List<EntrySegment> segments, ConvertSettings settings)
        {
            segments.Sort((a, b) => a.StartTime.CompareTo(b.StartTime));

            var keys = new List<Keyframe>();

            for (int si = 0; si < segments.Count; si++)
            {
                var   seg        = segments[si];
                float singleDur  = seg.Loops > 0
                    ? (seg.EndTime - seg.StartTime) / seg.Loops
                    :  seg.EndTime - seg.StartTime;
                int   spl        = Mathf.Max(2, Mathf.RoundToInt(singleDur * settings.SamplesPerSecond));

                if (singleDur <= 0f)
                {
                    keys.Add(new Keyframe(seg.StartTime, seg.EndVal));
                    continue;
                }

                for (int loop = 0; loop < seg.Loops; loop++)
                {
                    float loopStart = seg.StartTime + loop * singleDur;
                    LoopBounds(seg, loop, out float fromVal, out float toVal);

                    var loopKeys = new List<Keyframe>(spl + 1);
                    for (int s = 0; s <= spl; s++)
                    {
                        float nt    = (float)s / spl;
                        float eased = seg.CustomCurve != null
                            ? seg.CustomCurve.Evaluate(nt)
                            : EaseManager.Evaluate(seg.Ease, null, nt * singleDur, singleDur, 1.70158f, 0f);
                        loopKeys.Add(new Keyframe(loopStart + nt * singleDur,
                                                  Mathf.LerpUnclamped(fromVal, toVal, eased)));
                    }

                    if (settings.ReduceKeys)
                        loopKeys = ReduceKeys(loopKeys, settings.ReductionTolerance);

                    keys.AddRange(loopKeys);
                }

                // Hold keyframes across gaps to the next segment
                bool hasGap = si < segments.Count - 1 &&
                              segments[si + 1].StartTime > seg.EndTime + 0.0005f;
                if (hasGap)
                {
                    float holdVal = FinalValue(seg);
                    float gapEnd  = segments[si + 1].StartTime;
                    keys.Add(new Keyframe(seg.EndTime,      holdVal));
                    keys.Add(new Keyframe(gapEnd - 0.0001f, holdVal));
                }
            }

            // Sort and deduplicate
            keys.Sort((a, b) => a.time.CompareTo(b.time));

            var curve   = new AnimationCurve();
            float prevT = float.MinValue;
            foreach (var kf in keys)
            {
                if (kf.time - prevT < 0.00005f) continue;
                curve.AddKey(kf);
                prevT = kf.time;
            }

            for (int i = 0; i < curve.length; i++)
            {
                AnimationUtility.SetKeyLeftTangentMode(curve,  i, AnimationUtility.TangentMode.Linear);
                AnimationUtility.SetKeyRightTangentMode(curve, i, AnimationUtility.TangentMode.Linear);
            }
            return curve;
        }

        // Ramer-Douglas-Peucker: removes keyframes whose deviation from linear interpolation
        // of their neighbours is within tolerance.
        private static List<Keyframe> ReduceKeys(List<Keyframe> keys, float tolerance)
        {
            if (keys.Count <= 2) return keys;

            float maxDev   = 0f;
            int   splitIdx = 0;

            Keyframe first = keys[0];
            Keyframe last  = keys[keys.Count - 1];
            float    dt    = last.time - first.time;

            for (int i = 1; i < keys.Count - 1; i++)
            {
                float t    = dt > 0f ? (keys[i].time - first.time) / dt : 0f;
                float lin  = Mathf.Lerp(first.value, last.value, t);
                float dev  = Mathf.Abs(keys[i].value - lin);
                if (dev > maxDev) { maxDev = dev; splitIdx = i; }
            }

            if (maxDev > tolerance)
            {
                var left  = ReduceKeys(keys.GetRange(0, splitIdx + 1), tolerance);
                var right = ReduceKeys(keys.GetRange(splitIdx, keys.Count - splitIdx), tolerance);
                left.RemoveAt(left.Count - 1); // avoid duplicate at split point
                left.AddRange(right);
                return left;
            }

            return new List<Keyframe> { first, last };
        }

        // ─── Loop helpers ──────────────────────────────────────────────────────

        private static void LoopBounds(EntrySegment seg, int loopIndex, out float fromVal, out float toVal)
        {
            switch (seg.LoopType)
            {
                case LoopType.Yoyo:
                    bool rev = loopIndex % 2 == 1;
                    fromVal = rev ? seg.EndVal : seg.StartVal;
                    toVal   = rev ? seg.StartVal : seg.EndVal;
                    break;
                case LoopType.Incremental:
                    float d = seg.EndVal - seg.StartVal;
                    fromVal = seg.StartVal + loopIndex * d;
                    toVal   = seg.EndVal   + loopIndex * d;
                    break;
                default:
                    fromVal = seg.StartVal;
                    toVal   = seg.EndVal;
                    break;
            }
        }

        private static float FinalValue(EntrySegment seg)
        {
            switch (seg.LoopType)
            {
                case LoopType.Yoyo:       return seg.Loops % 2 == 1 ? seg.StartVal : seg.EndVal;
                case LoopType.Incremental: return seg.EndVal + (seg.Loops - 1) * (seg.EndVal - seg.StartVal);
                default:                  return seg.EndVal;
            }
        }

        // ─── Helpers ───────────────────────────────────────────────────────────

        private static float[] ExtractChannels(PropertyValueUnion v, int channelCount)
        {
            switch (channelCount)
            {
                case 1:
                    float s = v.type switch
                    {
                        PropertyType.Float   => v.floatValue,
                        PropertyType.Vector2 => v.vector2Value.x,
                        PropertyType.Vector3 => v.vector3Value.x,
                        PropertyType.Color   => v.colorValue.r,
                        _                    => 0f,
                    };
                    return new[] { s };

                case 2:
                    return v.type switch
                    {
                        PropertyType.Vector2 => new[] { v.vector2Value.x, v.vector2Value.y },
                        PropertyType.Vector3 => new[] { v.vector3Value.x, v.vector3Value.y },
                        _                    => new[] { v.floatValue, v.floatValue },
                    };

                case 3:
                    return v.type == PropertyType.Vector3
                        ? new[] { v.vector3Value.x, v.vector3Value.y, v.vector3Value.z }
                        : new[] { 0f, 0f, 0f };

                case 4:
                    return v.type == PropertyType.Color
                        ? new[] { v.colorValue.r, v.colorValue.g, v.colorValue.b, v.colorValue.a }
                        : new[] { 0f, 0f, 0f, 1f };

                default:
                    return new float[channelCount];
            }
        }

        private static Type ResolveType(string typeName)
        {
            var t = Type.GetType(typeName);
            if (t != null) return t;
            foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
            {
                t = asm.GetType(typeName);
                if (t != null) return t;
            }
            return null;
        }
    }
}
