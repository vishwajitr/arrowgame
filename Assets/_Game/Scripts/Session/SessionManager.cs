using System;
using UnityEngine;

namespace _Game.Session
{
    /// <summary>
    /// Static utility for tracking user sessions, progression, and cohort data.
    /// Uses PlayerPrefs for local persistence.
    /// </summary>
    public static class SessionManager
    {
        private const string KEY_SESSION_COUNT = "arrows_session_count";
        private const string KEY_INSTALL_DATE = "arrows_install_date";
        private const string KEY_CURRENT_LEVEL = "arrows_current_level";
        private const string KEY_HIGHEST_UNLOCKED = "arrows_highest_unlocked";
        private const string KEY_LAST_SESSION_TIME = "arrows_last_session_time";
        
        /// <summary>
        /// Increments session count and stores install date on first session.
        /// Call this on app start.
        /// </summary>
        public static void IncrementSessionCount()
        {
            int count = PlayerPrefs.GetInt(KEY_SESSION_COUNT, 0);
            PlayerPrefs.SetInt(KEY_SESSION_COUNT, count + 1);
            
            // Store install date on first session if not already set
            if (count == 0 && !PlayerPrefs.HasKey(KEY_INSTALL_DATE))
            {
                PlayerPrefs.SetString(KEY_INSTALL_DATE, DateTime.UtcNow.ToString("o"));
            }
            
            // Update last session time
            PlayerPrefs.SetString(KEY_LAST_SESSION_TIME, DateTime.UtcNow.ToString("o"));
            
            PlayerPrefs.Save();
        }
        
        /// <summary>
        /// Gets the total number of sessions (app launches).
        /// </summary>
        public static int GetSessionCount()
        {
            return PlayerPrefs.GetInt(KEY_SESSION_COUNT, 0);
        }
        
        /// <summary>
        /// Gets the number of days since the app was first installed.
        /// </summary>
        public static int GetDaysSinceInstall()
        {
            string installDateStr = PlayerPrefs.GetString(KEY_INSTALL_DATE, "");
            if (string.IsNullOrEmpty(installDateStr)) return 0;
            
            try
            {
                DateTime installDate = DateTime.Parse(installDateStr, null, System.Globalization.DateTimeStyles.RoundtripKind);
                return (DateTime.UtcNow - installDate).Days;
            }
            catch (Exception)
            {
                return 0;
            }
        }
        
        /// <summary>
        /// Returns true if this is the first session ever.
        /// NOTE: Call this AFTER IncrementSessionCount() - checks if count <= 1.
        /// </summary>
        public static bool IsFirstSession()
        {
            return GetSessionCount() <= 1;
        }
        
        /// <summary>
        /// Gets the install date in ISO8601 format.
        /// Returns empty string if not set.
        /// </summary>
        public static string GetInstallDate()
        {
            return PlayerPrefs.GetString(KEY_INSTALL_DATE, "");
        }
        
        /// <summary>
        /// Gets the current level the player is on.
        /// </summary>
        public static int GetCurrentLevel()
        {
            return PlayerPrefs.GetInt(KEY_CURRENT_LEVEL, 1);
        }
        
        /// <summary>
        /// Sets the current level the player is attempting.
        /// </summary>
        public static void SetCurrentLevel(int levelId)
        {
            PlayerPrefs.SetInt(KEY_CURRENT_LEVEL, levelId);
            PlayerPrefs.Save();
        }
        
        /// <summary>
        /// Gets the highest level the player has unlocked.
        /// </summary>
        public static int GetHighestUnlockedLevel()
        {
            return PlayerPrefs.GetInt(KEY_HIGHEST_UNLOCKED, 1);
        }

        /// <summary>Alias for plan/spec wording.</summary>
        public static int GetMaxUnlockedLevel() => GetHighestUnlockedLevel();

        /// <summary>
        /// Unlocks the next level if current level was completed.
        /// Call this on level win.
        /// </summary>
        public static void UnlockNextLevel()
        {
            int currentLevel = GetCurrentLevel();
            int highestUnlocked = GetHighestUnlockedLevel();
            
            // Unlock next level if current was the highest
            if (currentLevel >= highestUnlocked)
            {
                PlayerPrefs.SetInt(KEY_HIGHEST_UNLOCKED, currentLevel + 1);
                PlayerPrefs.Save();
            }
        }
        
        /// <summary>
        /// Resets all session data (for testing/debug).
        /// </summary>
        public static void ResetAllData()
        {
            PlayerPrefs.DeleteKey(KEY_SESSION_COUNT);
            PlayerPrefs.DeleteKey(KEY_INSTALL_DATE);
            PlayerPrefs.DeleteKey(KEY_CURRENT_LEVEL);
            PlayerPrefs.DeleteKey(KEY_HIGHEST_UNLOCKED);
            PlayerPrefs.DeleteKey(KEY_LAST_SESSION_TIME);
            PlayerPrefs.Save();
        }
    }
}
