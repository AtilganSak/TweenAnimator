using System;
using DG.Tweening;
using UnityEngine;

namespace TweenAnimator
{
    public abstract class PropertyAccessor
    {
        public abstract Tween BuildTween(Component target, TweenEntryData entry);
        public abstract Tween BuildTweenFrom(Component target, TweenEntryData entry, PropertyValueUnion from);
        public abstract void ApplyValue(Component target, PropertyValueUnion value);
        public abstract PropertyValueUnion ReadValue(Component target);
    }

    public sealed class Vector3PropertyAccessor : PropertyAccessor
    {
        private readonly Func<Component, Vector3> _getter;
        private readonly Action<Component, Vector3> _setter;

        public Vector3PropertyAccessor(Func<Component, Vector3> getter, Action<Component, Vector3> setter)
        {
            _getter = getter;
            _setter = setter;
        }

        public override Tween BuildTween(Component target, TweenEntryData entry)
        {
            Vector3 end = entry.endValue.vector3Value;
            return DOTween.To(() => _getter(target), v => _setter(target, v), end, entry.EffectiveDuration)
                .SetEase(entry.ease)
                .SetLoops(entry.loops, entry.loopType);
        }

        public override Tween BuildTweenFrom(Component target, TweenEntryData entry, PropertyValueUnion from)
        {
            Vector3 start = from.vector3Value;
            Vector3 end = entry.endValue.vector3Value;
            return DOTween.To(() => start, v => _setter(target, v), end, entry.EffectiveDuration)
                .SetEase(entry.ease)
                .SetLoops(entry.loops, entry.loopType);
        }

        public override void ApplyValue(Component target, PropertyValueUnion value) =>
            _setter(target, value.vector3Value);

        public override PropertyValueUnion ReadValue(Component target) =>
            PropertyValueUnion.FromVector3(_getter(target));
    }

    public sealed class FloatPropertyAccessor : PropertyAccessor
    {
        private readonly Func<Component, float> _getter;
        private readonly Action<Component, float> _setter;

        public FloatPropertyAccessor(Func<Component, float> getter, Action<Component, float> setter)
        {
            _getter = getter;
            _setter = setter;
        }

        public override Tween BuildTween(Component target, TweenEntryData entry)
        {
            float end = entry.endValue.floatValue;
            return DOTween.To(() => _getter(target), v => _setter(target, v), end, entry.EffectiveDuration)
                .SetEase(entry.ease)
                .SetLoops(entry.loops, entry.loopType);
        }

        public override Tween BuildTweenFrom(Component target, TweenEntryData entry, PropertyValueUnion from)
        {
            float start = from.floatValue;
            float end = entry.endValue.floatValue;
            return DOTween.To(() => start, v => _setter(target, v), end, entry.EffectiveDuration)
                .SetEase(entry.ease)
                .SetLoops(entry.loops, entry.loopType);
        }

        public override void ApplyValue(Component target, PropertyValueUnion value) =>
            _setter(target, value.floatValue);

        public override PropertyValueUnion ReadValue(Component target) =>
            PropertyValueUnion.FromFloat(_getter(target));
    }

    public sealed class Vector2PropertyAccessor : PropertyAccessor
    {
        private readonly Func<Component, Vector2> _getter;
        private readonly Action<Component, Vector2> _setter;

        public Vector2PropertyAccessor(Func<Component, Vector2> getter, Action<Component, Vector2> setter)
        {
            _getter = getter;
            _setter = setter;
        }

        public override Tween BuildTween(Component target, TweenEntryData entry)
        {
            Vector2 end = entry.endValue.vector2Value;
            return DOTween.To(() => _getter(target), v => _setter(target, v), end, entry.EffectiveDuration)
                .SetEase(entry.ease)
                .SetLoops(entry.loops, entry.loopType);
        }

        public override Tween BuildTweenFrom(Component target, TweenEntryData entry, PropertyValueUnion from)
        {
            Vector2 start = from.vector2Value;
            Vector2 end = entry.endValue.vector2Value;
            return DOTween.To(() => start, v => _setter(target, v), end, entry.EffectiveDuration)
                .SetEase(entry.ease)
                .SetLoops(entry.loops, entry.loopType);
        }

        public override void ApplyValue(Component target, PropertyValueUnion value) =>
            _setter(target, value.vector2Value);

        public override PropertyValueUnion ReadValue(Component target) =>
            PropertyValueUnion.FromVector2(_getter(target));
    }

    public sealed class RotationPropertyAccessor : PropertyAccessor
    {
        private readonly bool _isLocal;

        public RotationPropertyAccessor(bool isLocal) { _isLocal = isLocal; }

        public override Tween BuildTween(Component target, TweenEntryData entry)
        {
            var t = (Transform)target;
            Vector3 end = entry.endValue.vector3Value;
            Tween tw = _isLocal
                ? t.DOLocalRotate(end, entry.EffectiveDuration, entry.rotateMode)
                : t.DORotate(end, entry.EffectiveDuration, entry.rotateMode);
            return tw.SetEase(entry.ease).SetLoops(entry.loops, entry.loopType);
        }

        public override Tween BuildTweenFrom(Component target, TweenEntryData entry, PropertyValueUnion from)
        {
            var t = (Transform)target;
            Vector3 start = from.vector3Value;
            Vector3 end = entry.endValue.vector3Value;
            // Explicit start: use DOTween.To for linear euler interpolation from baked start value
            return DOTween.To(() => start, v =>
                {
                    if (_isLocal) t.localEulerAngles = v;
                    else t.eulerAngles = v;
                }, end, entry.EffectiveDuration)
                .SetEase(entry.ease)
                .SetLoops(entry.loops, entry.loopType);
        }

        public override void ApplyValue(Component target, PropertyValueUnion value)
        {
            var t = (Transform)target;
            if (_isLocal) t.localEulerAngles = value.vector3Value;
            else t.eulerAngles = value.vector3Value;
        }

        public override PropertyValueUnion ReadValue(Component target)
        {
            var t = (Transform)target;
            return PropertyValueUnion.FromVector3(_isLocal ? t.localEulerAngles : t.eulerAngles);
        }
    }

    public sealed class ColorPropertyAccessor : PropertyAccessor
    {
        private readonly Func<Component, Color> _getter;
        private readonly Action<Component, Color> _setter;

        public ColorPropertyAccessor(Func<Component, Color> getter, Action<Component, Color> setter)
        {
            _getter = getter;
            _setter = setter;
        }

        public override Tween BuildTween(Component target, TweenEntryData entry)
        {
            Color end = entry.endValue.colorValue;
            return DOTween.To(() => _getter(target), v => _setter(target, v), end, entry.EffectiveDuration)
                .SetEase(entry.ease)
                .SetLoops(entry.loops, entry.loopType);
        }

        public override Tween BuildTweenFrom(Component target, TweenEntryData entry, PropertyValueUnion from)
        {
            Color start = from.colorValue;
            Color end = entry.endValue.colorValue;
            return DOTween.To(() => start, v => _setter(target, v), end, entry.EffectiveDuration)
                .SetEase(entry.ease)
                .SetLoops(entry.loops, entry.loopType);
        }

        public override void ApplyValue(Component target, PropertyValueUnion value) =>
            _setter(target, value.colorValue);

        public override PropertyValueUnion ReadValue(Component target) =>
            PropertyValueUnion.FromColor(_getter(target));
    }

    // Property name convention: "mat_float:<shaderProp>" or "mat_color:<shaderProp>"
    public sealed class MaterialFloatPropertyAccessor : PropertyAccessor
    {
        private readonly string _shaderProp;

        public MaterialFloatPropertyAccessor(string shaderProp) { _shaderProp = shaderProp; }

        private Material Mat(Component target)
        {
            var r = (Renderer)target;
            return Application.isPlaying ? r.material : r.sharedMaterial;
        }

        public override Tween BuildTween(Component target, TweenEntryData entry)
        {
            var mat = Mat(target);
            float end = entry.endValue.floatValue;
            return DOTween.To(() => mat.GetFloat(_shaderProp), v => mat.SetFloat(_shaderProp, v), end, entry.EffectiveDuration)
                .SetEase(entry.ease)
                .SetLoops(entry.loops, entry.loopType);
        }

        public override Tween BuildTweenFrom(Component target, TweenEntryData entry, PropertyValueUnion from)
        {
            var mat = Mat(target);
            float start = from.floatValue;
            float end = entry.endValue.floatValue;
            return DOTween.To(() => start, v => mat.SetFloat(_shaderProp, v), end, entry.EffectiveDuration)
                .SetEase(entry.ease)
                .SetLoops(entry.loops, entry.loopType);
        }

        public override void ApplyValue(Component target, PropertyValueUnion value) =>
            Mat(target).SetFloat(_shaderProp, value.floatValue);

        public override PropertyValueUnion ReadValue(Component target) =>
            PropertyValueUnion.FromFloat(Mat(target).GetFloat(_shaderProp));
    }

    public sealed class MaterialColorPropertyAccessor : PropertyAccessor
    {
        private readonly string _shaderProp;

        public MaterialColorPropertyAccessor(string shaderProp) { _shaderProp = shaderProp; }

        private Material Mat(Component target)
        {
            var r = (Renderer)target;
            return Application.isPlaying ? r.material : r.sharedMaterial;
        }

        public override Tween BuildTween(Component target, TweenEntryData entry)
        {
            var mat = Mat(target);
            Color end = entry.endValue.colorValue;
            return DOTween.To(() => mat.GetColor(_shaderProp), v => mat.SetColor(_shaderProp, v), end, entry.EffectiveDuration)
                .SetEase(entry.ease)
                .SetLoops(entry.loops, entry.loopType);
        }

        public override Tween BuildTweenFrom(Component target, TweenEntryData entry, PropertyValueUnion from)
        {
            var mat = Mat(target);
            Color start = from.colorValue;
            Color end = entry.endValue.colorValue;
            return DOTween.To(() => start, v => mat.SetColor(_shaderProp, v), end, entry.EffectiveDuration)
                .SetEase(entry.ease)
                .SetLoops(entry.loops, entry.loopType);
        }

        public override void ApplyValue(Component target, PropertyValueUnion value) =>
            Mat(target).SetColor(_shaderProp, value.colorValue);

        public override PropertyValueUnion ReadValue(Component target) =>
            PropertyValueUnion.FromColor(Mat(target).GetColor(_shaderProp));
    }
}