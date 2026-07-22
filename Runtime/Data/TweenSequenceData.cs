using System;
using System.Collections.Generic;
using UnityEngine;

namespace TweenAnimator
{
    [Serializable]
    public class TweenSequenceData
    {
        public string displayName = "Sequence";
        public float timeScale = 1f;
        public bool autoKillOnComplete = true;
        public List<TweenEntryData> entries = new List<TweenEntryData>();
        public List<EventMarkerData> markers = new List<EventMarkerData>();

        public float TotalDuration
        {
            get
            {
                float max = 0f;
                foreach (var e in entries)
                    if (e.isEnabled && e.EndTime > max)
                        max = e.EndTime;
                return max;
            }
        }
    }
}