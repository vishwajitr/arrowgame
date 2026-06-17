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
        
        /// <summary>
        /// Logs app open event with session count and days since install.
        /// Called when app starts or returns from background.
        /// </summary>
        public void LogAppOpen()
        {
            // I1: Check if initialized before logging
            if (!_isInitialized)
            {
                TraceLogger.LogWarning("Analytics not initialized, skipping LogAppOpen");
                return;
            }
            
            int sessionCount = SessionManager.GetSessionCount();
            int daysSinceInstall = SessionManager.GetDaysSinceInstall();
            
            LogEvent(
                AnalyticsEvents.APP_OPEN,
                (AnalyticsEvents.PARAM_SESSION_COUNT, sessionCount),
                (AnalyticsEvents.PARAM_DAYS_SINCE_INSTALL, daysSinceInstall)
            );
        }
        
        /// <summary>
        /// Logs first session start event with install timestamp.
        /// Should only be called if SessionManager.IsFirstSession() is true.
        /// </summary>
        public void LogFirstSessionStart()
        {
            // I1: Check if initialized before logging
            if (!_isInitialized)
            {
                TraceLogger.LogWarning("Analytics not initialized, skipping LogFirstSessionStart");
                return;
            }
            
            string installTimestamp = SessionManager.GetInstallDate();
            
            // I2: Handle empty install date
            if (string.IsNullOrEmpty(installTimestamp))
            {
                installTimestamp = DateTime.UtcNow.ToString("o");
            }
            
            LogEvent(
                AnalyticsEvents.FIRST_SESSION_START,
                (AnalyticsEvents.PARAM_INSTALL_TIMESTAMP, installTimestamp)
            );
        }
        
        /// <summary>
        /// Logs session end event with session duration in seconds.
        /// Called when app goes to background or quits.
        /// </summary>
        public void LogSessionEnd()
        {
            // I1: Check if initialized before logging
            if (!_isInitialized)
            {
                TraceLogger.LogWarning("Analytics not initialized, skipping LogSessionEnd");
                return;
            }
            
            // C1: Guard against negative session duration on first call
            if (_sessionStartTime <= 0f)
            {
                TraceLogger.LogWarning("Session start time not set, skipping LogSessionEnd");
                return;
            }
            
            // M1: Ensure duration is never negative
            int sessionDuration = Mathf.Max(0, (int)(Time.realtimeSinceStartup - _sessionStartTime));
            
            LogEvent(
                AnalyticsEvents.SESSION_END,
                (AnalyticsEvents.PARAM_SESSION_DURATION, sessionDuration)
            );
        }
        
        /// <summary>
        /// Logs level start event with level number and session count.
        /// </summary>
        /// <param name="levelNumber">The level number being started</param>
        public void LogLevelStart(int levelNumber)
        {
            if (!_isInitialized)
            {
                TraceLogger.LogWarning("Analytics not initialized, skipping LogLevelStart");
                return;
            }
            
            levelNumber = Mathf.Max(0, levelNumber);
            int sessionCount = SessionManager.GetSessionCount();
            
            LogEvent(
                AnalyticsEvents.LEVEL_START,
                (AnalyticsEvents.PARAM_LEVEL_NUMBER, levelNumber),
                (AnalyticsEvents.PARAM_SESSION_COUNT, sessionCount)
            );
        }
        
        /// <summary>
        /// Logs level complete event with level number, duration, lives remaining, and session count.
        /// </summary>
        /// <param name="levelNumber">The level number that was completed</param>
        /// <param name="duration">Duration in seconds to complete the level</param>
        /// <param name="livesRemaining">Number of lives remaining after completion</param>
        public void LogLevelComplete(int levelNumber, int duration, int livesRemaining)
        {
            if (!_isInitialized)
            {
                TraceLogger.LogWarning("Analytics not initialized, skipping LogLevelComplete");
                return;
            }
            
            levelNumber = Mathf.Max(0, levelNumber);
            duration = Mathf.Max(0, duration);
            livesRemaining = Mathf.Max(0, livesRemaining);
            int sessionCount = SessionManager.GetSessionCount();
            
            LogEvent(
                AnalyticsEvents.LEVEL_COMPLETE,
                (AnalyticsEvents.PARAM_LEVEL_NUMBER, levelNumber),
                (AnalyticsEvents.PARAM_LEVEL_DURATION, duration),
                (AnalyticsEvents.PARAM_LIVES_REMAINING, livesRemaining),
                (AnalyticsEvents.PARAM_SESSION_COUNT, sessionCount)
            );
        }
        
        /// <summary>
        /// Logs level failed event with level number, duration, failure reason, and session count.
        /// </summary>
        /// <param name="levelNumber">The level number that was failed</param>
        /// <param name="duration">Duration in seconds before failure</param>
        /// <param name="failureReason">Reason for failure (e.g., "out_of_lives", "timeout")</param>
        public void LogLevelFailed(int levelNumber, int duration, string failureReason)
        {
            if (!_isInitialized)
            {
                TraceLogger.LogWarning("Analytics not initialized, skipping LogLevelFailed");
                return;
            }
            
            if (string.IsNullOrEmpty(failureReason))
            {
                TraceLogger.LogWarning("LogLevelFailed called with null/empty failureReason, using 'unknown'");
                failureReason = "unknown";
            }
            
            levelNumber = Mathf.Max(0, levelNumber);
            duration = Mathf.Max(0, duration);
            int sessionCount = SessionManager.GetSessionCount();
            
            LogEvent(
                AnalyticsEvents.LEVEL_FAILED,
                (AnalyticsEvents.PARAM_LEVEL_NUMBER, levelNumber),
                (AnalyticsEvents.PARAM_LEVEL_DURATION, duration),
                (AnalyticsEvents.PARAM_FAILURE_REASON, failureReason),
                (AnalyticsEvents.PARAM_SESSION_COUNT, sessionCount)
            );
        }
        
        /// <summary>
        /// Logs collision event with collision type.
        /// </summary>
        /// <param name="collisionType">Type of collision (e.g., "wall", "obstacle", "enemy", "target")</param>
        public void LogCollision(string collisionType)
        {
            if (!_isInitialized)
            {
                TraceLogger.LogWarning("Analytics not initialized, skipping LogCollision");
                return;
            }
            
            if (string.IsNullOrEmpty(collisionType))
            {
                TraceLogger.LogWarning("LogCollision called with null/empty collisionType, using 'unknown'");
                collisionType = "unknown";
            }
            
            LogEvent(
                AnalyticsEvents.COLLISION,
                (AnalyticsEvents.PARAM_COLLISION_TYPE, collisionType)
            );
        }
        
        /// <summary>
        /// Logs life lost event with level number and remaining lives.
        /// </summary>
        /// <param name="levelNumber">The level number where life was lost</param>
        /// <param name="livesRemaining">Number of lives remaining after loss</param>
        public void LogLifeLost(int levelNumber, int livesRemaining)
        {
            if (!_isInitialized)
            {
                TraceLogger.LogWarning("Analytics not initialized, skipping LogLifeLost");
                return;
            }
            
            levelNumber = Mathf.Max(0, levelNumber);
            livesRemaining = Mathf.Max(0, livesRemaining);
            int sessionCount = SessionManager.GetSessionCount();
            
            LogEvent(
                AnalyticsEvents.LIFE_LOST,
                (AnalyticsEvents.PARAM_LEVEL_NUMBER, levelNumber),
                (AnalyticsEvents.PARAM_LIVES_REMAINING, livesRemaining),
                (AnalyticsEvents.PARAM_SESSION_COUNT, sessionCount)
            );
        }
        
        /// <summary>
        /// Logs consent status changed event with the new consent status.
        /// </summary>
        /// <param name="consentStatus">The new consent status (e.g., "granted", "denied", "not_required")</param>
        public void LogConsentStatusChanged(string consentStatus)
        {
            if (!_isInitialized)
            {
                TraceLogger.LogWarning("Analytics not initialized, skipping LogConsentStatusChanged");
                return;
            }
            
            if (string.IsNullOrEmpty(consentStatus))
            {
                TraceLogger.LogWarning("LogConsentStatusChanged called with null/empty consentStatus, using 'unknown'");
                consentStatus = "unknown";
            }
            
            LogEvent(
                AnalyticsEvents.CONSENT_STATUS_CHANGED,
                (AnalyticsEvents.PARAM_CONSENT_STATUS, consentStatus)
            );
        }
        
        /// <summary>
        /// Logs settings changed event with the setting name and new value.
        /// </summary>
        /// <param name="settingName">The name of the setting that changed (e.g., "sound", "music")</param>
        /// <param name="settingValue">The new value of the setting (e.g., "on", "off")</param>
        public void LogSettingsChanged(string settingName, string settingValue)
        {
            if (!_isInitialized)
            {
                TraceLogger.LogWarning("Analytics not initialized, skipping LogSettingsChanged");
                return;
            }
            
            if (string.IsNullOrEmpty(settingName))
            {
                TraceLogger.LogWarning("LogSettingsChanged called with null/empty settingName, using 'unknown'");
                settingName = "unknown";
            }
            
            if (string.IsNullOrEmpty(settingValue))
            {
                TraceLogger.LogWarning("LogSettingsChanged called with null/empty settingValue, using 'unknown'");
                settingValue = "unknown";
            }
            
            LogEvent(
                AnalyticsEvents.SETTINGS_CHANGED,
                (AnalyticsEvents.PARAM_SETTING_NAME, settingName),
                (AnalyticsEvents.PARAM_SETTING_VALUE, settingValue)
            );
        }
    }
}
