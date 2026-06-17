using UnityEngine;

namespace _Game.Analytics
{
    [CreateAssetMenu(fileName = "AnalyticsConfig", menuName = "Arrows/Analytics Config", order = 1)]
    public class AnalyticsConfigSO : ScriptableObject
    {
        [Header("Firebase Settings")]
        [Tooltip("Enable/disable all analytics logging")]
        public bool analyticsEnabled = true;
        
        [Tooltip("Log events to Unity Console without sending to Firebase (for testing)")]
        public bool debugMode = false;
        
        [Header("Event Batching")]
        [Tooltip("Number of events to batch before sending to Firebase")]
        [Range(1, 50)]
        public int eventBatchSize = 10;
        
        [Tooltip("Time interval (seconds) between event batches")]
        [Range(1f, 60f)]
        public float eventBatchIntervalSec = 5f;
        
        [Header("Performance")]
        [Tooltip("Log granular collision events (disable if performance impact detected)")]
        public bool logCollisionEvents = true;
        
        [Tooltip("Maximum events per session (safety cap to prevent excessive logging)")]
        [Range(100, 5000)]
        public int maxEventsPerSession = 1000;
        
        [Header("Session Tracking")]
        [Tooltip("Minimum time (seconds) between sessions to count as new session")]
        [Range(30f, 1800f)]
        public float sessionTimeoutSec = 300f; // 5 minutes
    }
}
