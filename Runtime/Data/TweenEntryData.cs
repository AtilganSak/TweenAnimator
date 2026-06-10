using System;
using DG.Tweening;
using UnityEngine;

namespace TweenAnimator
{
    [Serializable]
    public class TweenEntryData
    {
        public string entryId = Guid.NewGuid().ToString();
        public string trackId = Guid.NewGuid().ToString();
        public string displayName;
        public TweenPropertyBinding binding = new TweenPropertyBinding();
        public PropertyValueUnion startValue;
        public PropertyValueUnion endValue;
        public bool useCurrentAsStart;
        public string linkedStartEntryId;
        public float duration = 1f;
        public float delay;
        public float speed = 1f;
        public Ease ease = Ease.Linear;
        public bool useCustomCurve;
        public AnimationCurve customEaseCurve = AnimationCurve.Linear(0, 0, 1, 1);
        public LoopType loopType = LoopType.Restart;
        public int loops = 1;
        public bool isEnabled = true;
        public Color trackColor = new Color(0.28f, 0.56f, 0.90f, 0.9f);
        public RotateMode rotateMode = RotateMode.Fast;

        public event Action OnStart;
        public event Action OnComplete;

        internal void InvokeOnStart() => OnStart?.Invoke();
        internal void InvokeOnComplete() => OnComplete?.Invoke();

        // Editor-only fold state — fine to serialize here, has no runtime cost
        public bool isExpanded;

        public float EffectiveDuration => duration / Mathf.Max(0.001f, speed);
        public float EndTime => delay + EffectiveDuration;
    }
}