using UnityEngine;

namespace TweenAnimator
{
    [CreateAssetMenu(fileName = "NewTweenClip", menuName = "TweenAnimator/Tween Clip")]
    public class TweenAnimatorClip : ScriptableObject
    {
        [SerializeField] private TweenSequenceData data = new TweenSequenceData();
        public TweenSequenceData Data => data;
    }
}
