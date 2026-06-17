using System;
using System.Collections;
using UnityEngine;
// Firebase SDK imports commented out - will be enabled when SDK is integrated
// using Firebase;
// using Firebase.Analytics;
using SerapKeremGameKit._Singletons;
using SerapKeremGameKit._Logging;
using _Game.Session;

namespace _Game.Analytics
{
    /// <summary>
    /// Singleton manager for Firebase Analytics integration.
    /// Handles initialization, event logging, and session tracking.
    /// Currently operates in debug mode without Firebase SDK.
    /// </summary>
    public class AnalyticsManager : MonoSingleton<AnalyticsManager>
    {
        [SerializeField] private AnalyticsConfigSO _config;
        
        private bool _isInitialized = false;
        private int _eventsLoggedThisSession = 0;
        private float _sessionStartTime = 0f;
        
        /// <summary>
        /// Initializes Analytics (Firebase will be added later).
        /// Non-blocking with timeout.
        /// </summary>
        public void Initialize(Action onComplete = null)
        {
            // I1: Prevent re-initialization
            if (_isInitialized)
            {
                TraceLogger.LogWarning("AnalyticsManager already initialized");
                onComplete?.Invoke();
                return;
            }
            
            if (_config == null)
            {
                TraceLogger.LogError("AnalyticsConfig not assigned!");
                onComplete?.Invoke();
                return;
            }
            
            if (!_config.analyticsEnabled)
            {
                TraceLogger.LogWarning("Analytics disabled in config");
                onComplete?.Invoke();
                return;
            }
            
            StartCoroutine(InitializeAnalytics(onComplete));
        }
        
        private IEnumerator InitializeAnalytics(Action onComplete)
        {
            bool initialized = false;
            float timeout = 3f;
            float startTime = Time.realtimeSinceStartup;
            
            // TODO: Firebase SDK initialization will go here when SDK is integrated
            // For now, simulate initialization delay
            yield return new WaitForSeconds(0.5f);
            
            _isInitialized = true;
            _sessionStartTime = Time.realtimeSinceStartup;
            
            TraceLogger.Log("Analytics initialized successfully (debug mode)");
            initialized = true;
            
            /* Firebase initialization (to be enabled when SDK is added):
            FirebaseApp.CheckAndFixDependenciesAsync().ContinueWith(task =>
            {
                var dependencyStatus = task.Result;
                if (dependencyStatus == DependencyStatus.Available)
                {
                    FirebaseAnalytics.SetAnalyticsCollectionEnabled(true);
                    _isInitialized = true;
                    _sessionStartTime = Time.realtimeSinceStartup;
                    
                    TraceLogger.Log("Firebase Analytics initialized successfully");
                    initialized = true;
                }
                else
                {
                    TraceLogger.LogError($"Firebase dependencies not available: {dependencyStatus}");
                }
            });
            */
            
            // C2: Wait for initialization or timeout (works with or without Firebase)
            while (!initialized && (Time.realtimeSinceStartup - startTime) < timeout)
            {
                yield return null;
            }
            
            if (!initialized)
            {
                TraceLogger.LogWarning("Analytics initialization timed out");
            }
            
            onComplete?.Invoke();
        }
        
        /// <summary>
        /// Logs an event with parameters to analytics.
        /// Currently logs to console; Firebase integration pending.
        /// </summary>
        private void LogEvent(string eventName, params (string key, object value)[] parameters)
        {
            // C1: Null check for config
            if (_config == null)
            {
                TraceLogger.LogWarning("AnalyticsConfig is null, cannot log event");
                return;
            }
            
            // Safety cap check
            if (_eventsLoggedThisSession >= _config.maxEventsPerSession)
            {
                if (_eventsLoggedThisSession == _config.maxEventsPerSession)
                {
                    TraceLogger.LogWarning("Max events per session reached, suppressing further logs");
                }
                return;
            }
            
            _eventsLoggedThisSession++;
            
            // I3: Debug mode: log to console (respects debug mode setting)
            if (_config.debugMode)
            {
                string paramsStr = parameters.Length > 0 ? $" with {parameters.Length} params" : "";
                TraceLogger.Log($"[Analytics] {eventName}{paramsStr}");
                
                if (parameters.Length > 0)
                {
                    foreach (var param in parameters)
                    {
                        TraceLogger.Log($"  - {param.key}: {param.value}");
                    }
                }
            }
            
            /* Firebase logging (to be enabled when SDK is added):
            if (_config.debugMode)
            {
                // Debug mode output above
                return;
            }
            
            if (_isInitialized)
            {
                // Convert to Firebase Parameter array
                // I4: TODO - Type preservation needed: currently converts all values to strings,
                // but Firebase supports int, long, double. Will need type checking when enabled.
                var firebaseParams = new Parameter[parameters.Length];
                for (int i = 0; i < parameters.Length; i++)
                {
                    firebaseParams[i] = new Parameter(parameters[i].key, parameters[i].value.ToString());
                }
                
                if (firebaseParams.Length > 0)
                {
                    FirebaseAnalytics.LogEvent(eventName, firebaseParams);
                }
                else
                {
                    FirebaseAnalytics.LogEvent(eventName);
                }
            }
            */
        }
        
        private void OnApplicationPause(bool pauseStatus)
        {
            if (pauseStatus)
            {
                // App going to background
                LogSessionEnd();
            }
            else
            {
                // I2: App returning from background
                // NOTE: This logs app_open on every resume (including from multitasking),
                // not just cold starts. This is acceptable behavior but will inflate metrics
                // if cold-start vs resume distinction is needed later.
                LogAppOpen();
            }
        }
        
        private void OnApplicationQuit()
        {
            LogSessionEnd();
        }
        
        // Lifecycle event methods will be added in Task 7
        // Placeholder stubs for OnApplicationPause/Quit:
        private void LogSessionEnd() { }
        private void LogAppOpen() { }
        
        // Gameplay event methods will be added in Task 8
        // Settings event methods will be added in Task 9
    }
}
