using System;
using DG.Tweening;
using UnityEngine;

namespace TweenAnimator
{
    public static class SequenceBuilder
    {
        public static Sequence Build(TweenAnimatorController controller, TweenSequenceData data)
        {
            var seq = DOTween.Sequence();
            seq.SetAutoKill(data.autoKillOnComplete);
            seq.timeScale = data.timeScale;

            var entryById = new System.Collections.Generic.Dictionary<string, TweenEntryData>();
            foreach (var e in data.entries)
                entryById[e.entryId] = e;

            foreach (var entry in data.entries)
            {
                if (!entry.isEnabled || entry.binding == null) continue;

                var component = ResolveComponent(controller, entry.binding);
                if (component == null)
                {
                    string target = string.IsNullOrEmpty(entry.binding.hierarchyPath)
                        ? controller.gameObject.name
                        : $"{controller.gameObject.name}/{entry.binding.hierarchyPath}";
                    Debug.LogError(
                        $"[TweenAnimator] Skipping entry \"{entry.displayName}\": " +
                        $"object or component not found (path: \"{target}\", type: {entry.binding.componentTypeName}). " +
                        $"Check if the object was deleted or renamed.",
                        controller);
                    continue;
                }

                var accessor = PropertyAccessorRegistry.Get(entry.binding.componentTypeName, entry.binding.propertyName);
                if (accessor == null)
                {
                    Debug.LogError(
                        $"[TweenAnimator] Skipping entry \"{entry.displayName}\": " +
                        $"no accessor registered for {entry.binding.componentTypeName}.{entry.binding.propertyName}.",
                        controller);
                    continue;
                }

                var capturedEntry = entry;
                seq.InsertCallback(entry.delay, () => capturedEntry.InvokeOnStart());

                Tween tween;
                if (entry.useCurrentAsStart)
                {
                    // Capture from-value lazily at runtime so it reflects current state when tween starts.
                    tween = accessor.BuildTween(component, entry);
                }
                else
                {
                    // Explicit from-value baked into the tween — guarantees correct FROM for Rewind/PlayBackward.
                    // Avoids the InsertCallback race condition where the getter fired before the callback at delay=0.
                    PropertyValueUnion startVal = entry.startValue;
                    if (!string.IsNullOrEmpty(entry.linkedStartEntryId) &&
                        entryById.TryGetValue(entry.linkedStartEntryId, out var linked))
                        startVal = linked.endValue;
                    tween = accessor.BuildTweenFrom(component, entry, startVal);
                }

                seq.Insert(entry.delay, tween);

                if (entry.loops >= 0)
                {
                    float completeAt = entry.delay + entry.EffectiveDuration * Mathf.Max(1, entry.loops);
                    seq.InsertCallback(completeAt, () => capturedEntry.InvokeOnComplete());
                }
            }

            if (data.markers != null)
            {
                foreach (var marker in data.markers)
                {
                    if (!marker.isEnabled || marker.time < 0f) continue;
                    var capturedMarker = marker;
                    seq.InsertCallback(marker.time, () => capturedMarker.InvokeTrigger());
                }
            }

            // DOTween requires at least one tween or callback for the sequence to have duration.
            // If the sequence is empty, append a zero-duration placeholder.
            if (data.entries.Count == 0)
                seq.AppendInterval(0f);

            return seq;
        }

        private static Component ResolveComponent(TweenAnimatorController ctrl, TweenPropertyBinding binding)
        {
            if (binding == null) return null;

            Transform target = string.IsNullOrEmpty(binding.hierarchyPath)
                ? ctrl.transform
                : ctrl.transform.Find(binding.hierarchyPath);

            if (target == null) return null;

            var type = ResolveType(binding.componentTypeName);
            return type != null ? target.GetComponent(type) : null;
        }

        private static Type ResolveType(string typeName)
        {
            if (string.IsNullOrEmpty(typeName)) return null;

            var t = Type.GetType(typeName);
            if (t != null) return t;

            // Fallback: search loaded assemblies (needed for UnityEngine types in some contexts)
            foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                t = assembly.GetType(typeName);
                if (t != null) return t;
            }

            return null;
        }
    }
}