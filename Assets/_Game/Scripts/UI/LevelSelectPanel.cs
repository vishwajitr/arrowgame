using UnityEngine;
using UnityEngine.UI;
using SerapKeremGameKit._UI;
using _Game.Session;
using _Game.Analytics;

namespace _Game.UI
{
    /// <summary>
    /// UI panel for level selection, dynamically generates buttons for all available levels.
    /// Integrates with SessionManager for progression tracking and AnalyticsManager for event logging.
    /// </summary>
    public class LevelSelectPanel : UIPanel
    {
        [Header("Level Button Setup")]
        [SerializeField] private Transform _levelButtonContainer;
        [SerializeField] private Button _levelButtonPrefab;
        
        [Header("Level Configuration")]
        [SerializeField] private int _totalLevels = 20;
        
        private int _maxUnlockedLevel;
        
        /// <summary>
        /// Initializes the panel.
        /// </summary>
        protected void Awake()
        {
            // Removed: _maxUnlockedLevel fetch moved to OnPanelShown() to avoid race conditions
        }
        
        /// <summary>
        /// Called when the panel is shown. Refreshes unlocked level data, generates level buttons, and logs panel opened event.
        /// </summary>
        public override void Show(bool playSound = true)
        {
            base.Show(playSound);
            // Fetch max unlocked level when panel is shown (fixes race condition)
            _maxUnlockedLevel = SessionManager.GetHighestUnlockedLevel();
            GenerateLevelButtons();
            LogPanelOpened();
        }
        
        /// <summary>
        /// Dynamically generates level buttons based on the total level count.
        /// Buttons are enabled/disabled based on the player's progression.
        /// </summary>
        private void GenerateLevelButtons()
        {
            if (_levelButtonContainer == null)
            {
                Debug.LogError("LevelSelectPanel: _levelButtonContainer is not assigned!");
                return;
            }
            
            if (_levelButtonPrefab == null)
            {
                Debug.LogError("LevelSelectPanel: _levelButtonPrefab is not assigned!");
                return;
            }
            
            // Clear existing buttons
            foreach (Transform child in _levelButtonContainer)
            {
                if (Application.isPlaying)
                    Destroy(child.gameObject);
                else
                    DestroyImmediate(child.gameObject);
            }
            
            // Generate buttons for all levels
            for (int i = 1; i <= _totalLevels; i++)
            {
                Button button = Instantiate(_levelButtonPrefab, _levelButtonContainer);
                int levelNumber = i;
                bool isUnlocked = levelNumber <= _maxUnlockedLevel;
                
                // Set button interactable state
                button.interactable = isUnlocked;
                
                // Set button text (optimized to avoid GetComponentInChildren)
                Text buttonText = button.GetComponent<Text>();
                if (buttonText != null)
                {
                    buttonText.text = levelNumber.ToString();
                }
                else
                {
                    Debug.LogWarning($"LevelSelectPanel: Button for level {levelNumber} has no Text component");
                }
                
                // Add click listener
                button.onClick.AddListener(() => OnLevelButtonClicked(levelNumber));
            }
        }
        
        /// <summary>
        /// Handles level button click events. Logs analytics and initiates level loading.
        /// </summary>
        /// <param name="levelNumber">The level number to load</param>
        private void OnLevelButtonClicked(int levelNumber)
        {
            // Log analytics event
            if (AnalyticsManager.Instance != null)
            {
                AnalyticsManager.Instance.LogLevelStart(levelNumber);
            }
            else
            {
                Debug.LogWarning("LevelSelectPanel: AnalyticsManager instance not found");
            }
            
            // Set the current level in SessionManager
            SessionManager.SetCurrentLevel(levelNumber);
            
            // TODO: Implement actual level loading logic
            // This will depend on the game's level loading system:
            // Option 1: Scene-based levels
            // SceneManager.LoadScene($"Level_{levelNumber}");
            // Option 2: Level manager system
            // if (LevelManager.Instance != null)
            // {
            //     LevelManager.Instance.LoadLevel(levelNumber);
            // }
            
            Debug.Log($"LevelSelectPanel: Level {levelNumber} selected (loading not yet implemented)");
            
            // Close the panel
            Hide();
        }
        
        /// <summary>
        /// Logs that the level select panel was opened using analytics.
        /// </summary>
        private void LogPanelOpened()
        {
            if (AnalyticsManager.Instance != null)
            {
                AnalyticsManager.Instance.LogSettingsChanged("panel_opened", "level_select");
            }
            else
            {
                Debug.LogWarning("LevelSelectPanel: AnalyticsManager instance not found");
            }
        }
        
        /// <summary>
        /// Cleanup method to remove all button listeners and prevent memory leaks.
        /// </summary>
        protected override void OnDestroy()
        {
            base.OnDestroy();
            // Clear all button listeners
            if (_levelButtonContainer != null)
            {
                foreach (Transform child in _levelButtonContainer)
                {
                    Button button = child.GetComponent<Button>();
                    if (button != null)
                    {
                        button.onClick.RemoveAllListeners();
                    }
                }
            }
        }
    }
}
