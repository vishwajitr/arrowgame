# Arrows Monetization & Analytics Implementation Design

**Date:** June 18, 2026  
**Version:** 1.0  
**Status:** Design Review

---

## Executive Summary

This design document specifies how to integrate Google AdMob monetization and Firebase Analytics into the existing Arrows Unity puzzle game. The implementation adds banner, interstitial, and rewarded ad formats alongside comprehensive event tracking, while maintaining the core gameplay experience and 60fps performance target.

**Goal:** Transform the existing prototype into a monetizable, measurable mobile release ready for soft launch.

**Approach:** Manager-First Architecture using event-driven integration with the existing `StateManager`, ensuring zero changes to core gameplay code (Line system, collision detection, animation).

**Phases:** 4-phase rollout over 6 weeks (Analytics Foundation → AdMob Integration → Tuning & Hardening → Soft Launch Preparation)

**Platforms:** Android primary (Phase 1-2), iOS secondary (Phase 3)

---

## Table of Contents

1. [Architecture Overview](#1-architecture-overview)
2. [Analytics System Design](#2-analytics-system-design)
3. [AdMob Integration Design](#3-admob-integration-design)
4. [UI & Gameplay Flow Changes](#4-ui--gameplay-flow-changes)
5. [SDK Integration & Dependencies](#5-sdk-integration--dependencies)
6. [Testing Strategy & Error Handling](#6-testing-strategy--error-handling)
7. [Data Flow & Implementation Phases](#7-data-flow--implementation-phases)
8. [Success Metrics](#8-success-metrics)
9. [Appendix](#9-appendix)

---

## 1. Architecture Overview

### 1.1 System Design Principles

**Event-Driven Integration:**  
Both `AdsManager` and `AnalyticsManager` subscribe to `StateManager` state transitions rather than being called directly from gameplay code. This creates a hard boundary between monetization and core gameplay systems.

**Configuration via ScriptableObjects:**  
Following the existing `SerapKeremGameKit` pattern, all ad unit IDs, frequency caps, and analytics settings live in ScriptableObject assets, never hardcoded in scripts.

**Fail-Safe Design:**  
If SDK initialization fails or ads don't load, the game proceeds normally without blocking. Monetization is additive, never mandatory for gameplay.

### 1.2 New Managers

#### AdsManager
- **Responsibility:** All AdMob SDK interactions (banner, interstitial, rewarded ads)
- **Type:** `MonoSingleton<AdsManager>` (follows SerapKeremGameKit pattern)
- **Lifecycle:** Initialize at app startup (non-blocking, 3s timeout), persist across scenes
- **Integration:** Hooks into `StateManager.OnStateChanged` event

#### AnalyticsManager
- **Responsibility:** All Firebase Analytics event logging
- **Type:** `MonoSingleton<AnalyticsManager>` (follows SerapKeremGameKit pattern)
- **Lifecycle:** Initialize at app startup (non-blocking, 3s timeout), persist across scenes
- **Integration:** Hooks into `StateManager` events, `LivesManager.OnLifeLost`, `LineHeadCollisionDetector.OnHeadCollision`

#### SessionManager
- **Responsibility:** Track session count, days since install, current level progression
- **Type:** Static utility class
- **Storage:** PlayerPrefs for local persistence

### 1.3 File Structure

```
Assets/
├── _Game/
│   ├── Scripts/
│   │   ├── Monetization/
│   │   │   ├── AdsManager.cs
│   │   │   ├── AdsConfigSO.cs
│   │   │   └── AdPlacement.cs (enum)
│   │   ├── Analytics/
│   │   │   ├── AnalyticsManager.cs
│   │   │   ├── AnalyticsConfigSO.cs
│   │   │   └── AnalyticsEvents.cs (event name constants)
│   │   ├── Session/
│   │   │   └── SessionManager.cs
│   │   └── UI/
│   │       ├── LevelSelectPanel.cs
│   │       ├── ContinuePanel.cs
│   │       └── DebugPanel.cs (dev builds only)
│   └── Resources/
│       └── Config/
│           ├── AdsConfig.asset
│           └── AnalyticsConfig.asset
└── SerapKeremGameKit/ (existing, minimal modifications)
    └── Scripts/
        └── LevelSystem/
            └── StateManager.cs (add OnStateChanged event)
```

### 1.4 Integration Points (Non-Breaking)

**Modified Existing Files:**
- `StateManager.cs`: Add `public event Action<GameState, GameState> OnStateChanged;`
- `LivesManager.cs`: Expose `public event Action<int> OnLifeLost;`
- `WinPanel.cs`: Add interstitial check before loading next level
- `SettingsPanel.cs`: Add Privacy Policy and Ad Consent buttons

**No Changes Required:**
- Line system (`LineAnimation`, `LineClick`, `LineMaterialHandler`, etc.)
- Collision detection (`LineHeadCollisionDetector`)
- Camera system (`CameraManager`)
- Audio/Haptics systems

---

## 2. Analytics System Design

### 2.1 AnalyticsManager API

```csharp
public class AnalyticsManager : MonoSingleton<AnalyticsManager>
{
    // === Initialization ===
    public void Initialize(Action onComplete);
    
    // === Lifecycle Events ===
    public void LogAppOpen(int sessionCount, int daysSinceInstall);
    public void LogFirstSessionStart(string platform, string appVersion);
    public void LogSessionEnd(float sessionLengthSec);
    
    // === Gameplay Events ===
    public void LogLevelStart(string levelId, int attemptNumber, int livesRemaining);
    public void LogLevelComplete(string levelId, float timeToCompleteSec, 
                                  int livesRemaining, int linesCount);
    public void LogLevelFail(string levelId, int attemptNumber, int failLineIndex);
    public void LogLineCollision(string levelId, string lineId, int livesRemainingAfter);
    public void LogLifeLost(string levelId, int livesRemaining);
    public void LogContinueOffered(string levelId, string offerType);
    public void LogContinueAccepted(string levelId, string offerType);
    
    // === Monetization Events (called by AdsManager) ===
    public void LogAdImpression(string adFormat, string placement, string adUnitId);
    public void LogInterstitialRequested(string placement, string levelId);
    public void LogInterstitialShown(string placement, string levelId);
    public void LogRewardedRequested(string placement, string rewardType);
    public void LogRewardedCompleted(string placement, string rewardType);
    public void LogRewardedSkipped(string placement, string rewardType);
    public void LogAdLoadFailed(string adFormat, string errorCode);
    
    // === Settings Events ===
    public void LogConsentStatus(string consentType, string status);
    public void LogSettingsChanged(string settingName, string newValue);
}
```

### 2.2 Event Taxonomy

**Lifecycle Events:**
| Event | Parameters | Trigger Point |
|-------|-----------|---------------|
| `app_open` | session_count, days_since_install | App foreground after background |
| `first_session_start` | platform, app_version | First app launch ever |
| `session_end` | session_length_sec | App background |

**Gameplay Events:**
| Event | Parameters | Trigger Point |
|-------|-----------|---------------|
| `level_start` | level_id, attempt_number, lives_remaining | StateManager.SetOnStart() |
| `level_complete` | level_id, time_to_complete_sec, lives_remaining, lines_count | StateManager.SetOnWin() |
| `level_fail` | level_id, attempt_number, fail_line_index | StateManager.SetOnLose() (0 lives) |
| `line_collision` | level_id, line_id, lives_remaining_after | LineHeadCollisionDetector.OnHeadCollision |
| `life_lost` | level_id, lives_remaining | LivesManager.OnLifeLost |
| `continue_offered` | level_id, offer_type | ContinuePanel.Show() |
| `continue_accepted` | level_id, offer_type | User taps "Watch Ad" or "Retry" |

**Monetization Events:**
| Event | Parameters | Trigger Point |
|-------|-----------|---------------|
| `ad_impression` | ad_format, placement, ad_unit_id | Auto-logged via Firebase/AdMob link |
| `interstitial_requested` | placement, level_id | AdsManager.LoadInterstitial() |
| `interstitial_shown` | placement, level_id | AdMob OnAdOpened callback |
| `rewarded_requested` | placement, reward_type | AdsManager.LoadRewarded() |
| `rewarded_completed` | placement, reward_type | AdMob OnUserEarnedReward callback |
| `rewarded_skipped` | placement, reward_type | AdMob OnAdClosed (no reward) |
| `ad_load_failed` | ad_format, error_code | AdMob OnAdFailedToLoad callback |

### 2.3 Event Hook Implementation

**StateManager Integration:**
```csharp
// In AnalyticsManager.Initialize()
StateManager.Instance.OnStateChanged += OnGameStateChanged;

private void OnGameStateChanged(GameState oldState, GameState newState)
{
    switch (newState)
    {
        case GameState.OnStart:
            LogLevelStart(GetCurrentLevelId(), GetAttemptNumber(), 
                          LivesManager.Instance.CurrentLives);
            break;
            
        case GameState.OnWin:
            LogLevelComplete(GetCurrentLevelId(), 
                             StateManager.Instance.GetLevelTime(),
                             LivesManager.Instance.CurrentLives,
                             GetLinesCount());
            break;
            
        case GameState.OnLose:
            if (LivesManager.Instance.CurrentLives == 0)
            {
                LogLevelFail(GetCurrentLevelId(), GetAttemptNumber(), -1);
            }
            break;
    }
}
```

**Collision Tracking:**
```csharp
// In AnalyticsManager.Initialize()
foreach (var line in FindObjectsOfType<LineHeadCollisionDetector>())
{
    line.OnHeadCollision += OnLineCollision;
}

private void OnLineCollision(Collider2D other)
{
    LogLineCollision(GetCurrentLevelId(), other.name, 
                     LivesManager.Instance.CurrentLives);
}
```

### 2.4 SessionManager Implementation

```csharp
public static class SessionManager
{
    private const string KEY_SESSION_COUNT = "arrows_session_count";
    private const string KEY_INSTALL_DATE = "arrows_install_date";
    private const string KEY_CURRENT_LEVEL = "arrows_current_level";
    private const string KEY_HIGHEST_UNLOCKED = "arrows_highest_unlocked";
    
    public static void IncrementSessionCount()
    {
        int count = PlayerPrefs.GetInt(KEY_SESSION_COUNT, 0);
        PlayerPrefs.SetInt(KEY_SESSION_COUNT, count + 1);
        
        if (count == 0) // First session
        {
            PlayerPrefs.SetString(KEY_INSTALL_DATE, DateTime.UtcNow.ToString("o"));
        }
        
        PlayerPrefs.Save();
    }
    
    public static int GetSessionCount()
    {
        return PlayerPrefs.GetInt(KEY_SESSION_COUNT, 0);
    }
    
    public static int GetDaysSinceInstall()
    {
        string installDateStr = PlayerPrefs.GetString(KEY_INSTALL_DATE, "");
        if (string.IsNullOrEmpty(installDateStr)) return 0;
        
        DateTime installDate = DateTime.Parse(installDateStr);
        return (DateTime.UtcNow - installDate).Days;
    }
    
    public static bool IsFirstSession()
    {
        return GetSessionCount() <= 1;
    }
    
    public static int GetCurrentLevel()
    {
        return PlayerPrefs.GetInt(KEY_CURRENT_LEVEL, 1);
    }
    
    public static void SetCurrentLevel(int levelId)
    {
        PlayerPrefs.SetInt(KEY_CURRENT_LEVEL, levelId);
        
        int highest = PlayerPrefs.GetInt(KEY_HIGHEST_UNLOCKED, 1);
        if (levelId > highest)
        {
            PlayerPrefs.SetInt(KEY_HIGHEST_UNLOCKED, levelId);
        }
        
        PlayerPrefs.Save();
    }
}
```

### 2.5 AnalyticsConfigSO

```csharp
[CreateAssetMenu(fileName = "AnalyticsConfig", menuName = "Arrows/Analytics Config")]
public class AnalyticsConfigSO : ScriptableObject
{
    [Header("Firebase Settings")]
    public bool analyticsEnabled = true;
    public bool debugMode = false; // Logs to console without sending to Firebase
    
    [Header("Event Batching")]
    public int eventBatchSize = 10; // Send events in batches
    public float eventBatchIntervalSec = 5f;
    
    [Header("Performance")]
    public bool logCollisionEvents = true; // Can disable for performance if needed
    public int maxEventsPerSession = 1000; // Safety cap
}
```

---

## 3. AdMob Integration Design

### 3.1 AdsManager API

```csharp
public class AdsManager : MonoSingleton<AdsManager>
{
    // === Initialization ===
    public void Initialize(Action onComplete);
    
    // === Banner Ads ===
    public void ShowBanner();
    public void HideBanner();
    public bool IsBannerShowing();
    
    // === Interstitial Ads ===
    public void LoadInterstitial();
    public void ShowInterstitial(string placement, Action onClosed = null);
    public bool IsInterstitialReady();
    public bool ShouldShowInterstitial(); // Checks frequency caps
    
    // === Rewarded Ads ===
    public void LoadRewarded();
    public void ShowRewarded(string placement, Action<bool> onComplete);
    public bool IsRewardedReady();
    
    // === Consent (UMP SDK) ===
    public void ShowConsentForm(Action<bool> onComplete);
    public bool HasConsent();
}
```

### 3.2 Ad Placement Logic

#### Banner Ads
- **Placement:** Bottom of LevelSelectPanel only
- **Format:** Adaptive banner (320x50 default, scales to device width)
- **Lifecycle:**
  - Show: On LevelSelectPanel.OnEnable()
  - Hide: On LevelSelectPanel.OnDisable()
- **Positioning:** Anchored to bottom, safe area aware
- **Auto-Refresh:** Handled by AdMob SDK (default 60s)

#### Interstitial Ads
- **Placement:** Between levels, on return to LevelSelectPanel after win
- **Trigger Logic:**
  ```csharp
  public bool ShouldShowInterstitial()
  {
      // Grace period check
      if (SessionManager.GetSessionCount() <= _config.gracePeriodSessionCount)
          return false;
      
      // Frequency check
      if (_levelCompletionsSinceLastAd < _config.interstitialEveryNLevels)
          return false;
      
      // Time gap check
      if (Time.realtimeSinceStartup - _lastInterstitialTime < _config.interstitialMinGapSeconds)
          return false;
      
      return IsInterstitialReady();
  }
  ```
- **Pre-Loading:** Load on level start, show on win
- **Frequency:** Every N levels (default N=3), min 60s between shows
- **Grace Period:** No interstitials in first 2 sessions (Day-0 protection)

#### Rewarded Ads
- **Placement 1:** ContinuePanel - "Watch Ad to Continue" (+1 life)
- **Placement 2:** LevelSelectPanel - "Unlock Hint" button (future feature)
- **Reward Granting:**
  ```csharp
  public void ShowRewarded(string placement, Action<bool> onComplete)
  {
      if (!IsRewardedReady())
      {
          onComplete?.Invoke(false);
          return;
      }
      
      _rewardGranted = false;
      
      _rewardedAd.OnUserEarnedReward += (sender, args) =>
      {
          _rewardGranted = true;
          AnalyticsManager.Instance.LogRewardedCompleted(placement, "extra_life");
      };
      
      _rewardedAd.OnAdClosed += (sender, args) =>
      {
          if (!_rewardGranted)
          {
              AnalyticsManager.Instance.LogRewardedSkipped(placement, "extra_life");
          }
          onComplete?.Invoke(_rewardGranted);
          LoadRewarded(); // Pre-load next
      };
      
      _rewardedAd.Show();
  }
  ```
- **Cap:** Max 1 per level attempt for continue offers

### 3.3 AdsConfigSO

```csharp
[CreateAssetMenu(fileName = "AdsConfig", menuName = "Arrows/Ads Config")]
public class AdsConfigSO : ScriptableObject
{
    [Header("Ad Unit IDs - Android")]
    public string androidBannerAdUnitId = "ca-app-pub-3940256099942544/6300978111"; // Test ID
    public string androidInterstitialAdUnitId = "ca-app-pub-3940256099942544/1033173712";
    public string androidRewardedAdUnitId = "ca-app-pub-3940256099942544/5224354917";
    
    [Header("Ad Unit IDs - iOS")]
    public string iosBannerAdUnitId = "ca-app-pub-3940256099942544/2934735716";
    public string iosInterstitialAdUnitId = "ca-app-pub-3940256099942544/4411468910";
    public string iosRewardedAdUnitId = "ca-app-pub-3940256099942544/1712485313";
    
    [Header("AdMob App IDs")]
    public string androidAdMobAppId = "ca-app-pub-3940256099942544~3347511713"; // Test app ID
    public string iosAdMobAppId = "ca-app-pub-3940256099942544~1458002511";
    
    [Header("Frequency Settings")]
    [Range(1, 10)] public int interstitialEveryNLevels = 3;
    [Range(30f, 300f)] public float interstitialMinGapSeconds = 60f;
    [Range(0, 5)] public int gracePeriodSessionCount = 2; // No ads first N sessions
    
    [Header("Test Mode")]
    public bool useTestAdUnits = true; // Override with test IDs
    public bool useMockAds = false; // Use mock implementation (for editor)
    
    [Header("Banner Settings")]
    public bool bannerEnabled = true;
    public BannerPosition bannerPosition = BannerPosition.Bottom;
    
    public enum BannerPosition { Top, Bottom }
}
```

### 3.4 UMP Consent Flow

```csharp
private void InitializeConsent(Action onComplete)
{
    var debugSettings = new ConsentDebugSettings
    {
        DebugGeography = DebugGeography.Disabled // Use real location in production
    };
    
    var requestParameters = new ConsentRequestParameters
    {
        ConsentDebugSettings = debugSettings
    };
    
    // Check if consent form is required
    ConsentInformation.Update(requestParameters, (FormError error) =>
    {
        if (error != null)
        {
            TraceLogger.LogWarning($"Consent form error: {error.Message}");
            onComplete?.Invoke();
            return;
        }
        
        // Load consent form if required
        if (ConsentInformation.IsConsentFormAvailable())
        {
            ConsentForm.Load((ConsentForm form, FormError loadError) =>
            {
                if (loadError != null)
                {
                    onComplete?.Invoke();
                    return;
                }
                
                _consentForm = form;
                
                // Show form if consent required
                if (ConsentInformation.ConsentStatus == ConsentStatus.Required)
                {
                    _consentForm.Show((FormError showError) =>
                    {
                        LogConsentStatus();
                        onComplete?.Invoke();
                    });
                }
                else
                {
                    onComplete?.Invoke();
                }
            });
        }
        else
        {
            onComplete?.Invoke();
        }
    });
}

private void LogConsentStatus()
{
    string status = ConsentInformation.ConsentStatus.ToString();
    AnalyticsManager.Instance.LogConsentStatus("gdpr", status);
}
```

### 3.5 iOS ATT Prompt (Phase 3)

```csharp
#if UNITY_IOS
private IEnumerator RequestATTPermission()
{
    if (ATTrackingStatusBinding.GetAuthorizationTrackingStatus() == 
        ATTrackingStatusBinding.AuthorizationTrackingStatus.NOT_DETERMINED)
    {
        ATTrackingStatusBinding.RequestAuthorizationTracking();
        
        // Wait for user response
        while (ATTrackingStatusBinding.GetAuthorizationTrackingStatus() == 
               ATTrackingStatusBinding.AuthorizationTrackingStatus.NOT_DETERMINED)
        {
            yield return new WaitForSeconds(0.5f);
        }
    }
    
    string status = ATTrackingStatusBinding.GetAuthorizationTrackingStatus().ToString();
    AnalyticsManager.Instance.LogConsentStatus("att", status);
}
#endif
```

---

## 4. UI & Gameplay Flow Changes

### 4.1 New Panel: LevelSelectPanel

**Purpose:** Explicit level selection screen replacing implicit progression.

**Layout:**
```
┌─────────────────────────────┐
│     ➡️ Select Level         │ ← Title
├─────────────────────────────┤
│                             │
│  ┌───┐ ┌───┐               │
│  │ 1 │ │ 2 │               │ ← Level grid (2 columns)
│  └───┘ └───┘               │   Shows locked/unlocked state
│  ┌───┐ ┌───┐               │   Shows stars/completion
│  │ 3 │ │🔒│               │
│  └───┘ └───┘               │
│   ...                       │
│                             │
├─────────────────────────────┤
│   [AdMob Banner Here]       │ ← Banner ad (adaptive)
└─────────────────────────────┘
│  [⚙️ Settings]              │ ← Bottom toolbar
└─────────────────────────────┘
```

**Implementation:**
```csharp
public class LevelSelectPanel : UIScreen
{
    [SerializeField] private LevelButton[] _levelButtons;
    [SerializeField] private GameObject _bannerContainer;
    
    protected override void OnEnable()
    {
        base.OnEnable();
        RefreshLevelButtons();
        AdsManager.Instance.ShowBanner();
    }
    
    protected override void OnDisable()
    {
        base.OnDisable();
        AdsManager.Instance.HideBanner();
    }
    
    private void RefreshLevelButtons()
    {
        int highestUnlocked = SessionManager.GetHighestUnlockedLevel();
        
        for (int i = 0; i < _levelButtons.Length; i++)
        {
            int levelId = i + 1;
            bool isUnlocked = levelId <= highestUnlocked;
            
            _levelButtons[i].SetLocked(!isUnlocked);
            _levelButtons[i].onClick.RemoveAllListeners();
            
            if (isUnlocked)
            {
                _levelButtons[i].onClick.AddListener(() => OnLevelSelected(levelId));
            }
        }
    }
    
    private void OnLevelSelected(int levelId)
    {
        SessionManager.SetCurrentLevel(levelId);
        StateManager.Instance.SetLoading();
        LevelManager.Instance.LoadLevel(levelId);
    }
}
```

### 4.2 New Panel: ContinuePanel

**Purpose:** Offer rewarded ad continue when player loses all lives.

**Layout:**
```
┌─────────────────────────────┐
│    💔 Out of Lives!         │
├─────────────────────────────┤
│                             │
│   ❤️ ❤️ ❤️ ❤️ ❤️         │ ← Shows 0 lives
│                             │
│  You can watch an ad to     │
│  get +1 life and continue!  │
│                             │
├─────────────────────────────┤
│ [📺 Watch Ad & Continue]    │ ← Primary (only if ad ready)
├─────────────────────────────┤
│ [🔄 Retry Level]            │ ← Secondary
└─────────────────────────────┘
```

**Implementation:**
```csharp
public class ContinuePanel : UIScreen
{
    [SerializeField] private Button _watchAdButton;
    [SerializeField] private Button _retryButton;
    
    protected override void OnEnable()
    {
        base.OnEnable();
        
        string levelId = LevelManager.Instance.GetCurrentLevelId();
        AnalyticsManager.Instance.LogContinueOffered(levelId, "rewarded_ad");
        
        // Show/hide watch ad button based on ad availability
        bool adReady = AdsManager.Instance.IsRewardedReady();
        _watchAdButton.gameObject.SetActive(adReady);
        
        _watchAdButton.onClick.AddListener(OnWatchAdClicked);
        _retryButton.onClick.AddListener(OnRetryClicked);
    }
    
    private void OnWatchAdClicked()
    {
        string levelId = LevelManager.Instance.GetCurrentLevelId();
        AnalyticsManager.Instance.LogContinueAccepted(levelId, "rewarded_ad");
        
        AdsManager.Instance.ShowRewarded("continue", (bool rewardGranted) =>
        {
            if (rewardGranted)
            {
                LivesManager.Instance.AddLife(1);
                Close();
                StateManager.Instance.SetOnStart(); // Resume level
            }
            else
            {
                // Ad skipped or failed
                ReturnToLevelSelect();
            }
        });
    }
    
    private void OnRetryClicked()
    {
        string levelId = LevelManager.Instance.GetCurrentLevelId();
        AnalyticsManager.Instance.LogContinueAccepted(levelId, "retry");
        
        Close();
        LevelManager.Instance.RestartLevel();
    }
    
    private void ReturnToLevelSelect()
    {
        Close();
        UIRootController.Instance.Show<LevelSelectPanel>();
    }
}
```

### 4.3 Modified Panel: WinPanel

**Changes:** Add interstitial check before loading next level.

```csharp
// Existing WinPanel code
public class WinPanel : UIScreen
{
    [SerializeField] private Button _nextLevelButton;
    
    protected override void OnEnable()
    {
        base.OnEnable();
        _nextLevelButton.onClick.AddListener(OnNextLevelClicked);
        
        // Pre-load interstitial for next transition
        AdsManager.Instance.LoadInterstitial();
    }
    
    private void OnNextLevelClicked()
    {
        Close();
        
        // Check if interstitial should show
        if (AdsManager.Instance.ShouldShowInterstitial())
        {
            string levelId = LevelManager.Instance.GetCurrentLevelId();
            
            AdsManager.Instance.ShowInterstitial("level_complete", () =>
            {
                // Ad closed, proceed to next level
                LoadNextLevel();
            });
        }
        else
        {
            // No ad, proceed immediately
            LoadNextLevel();
        }
    }
    
    private void LoadNextLevel()
    {
        SessionManager.UnlockNextLevel();
        UIRootController.Instance.Show<LevelSelectPanel>();
    }
}
```

### 4.4 Modified Panel: SettingsPanel

**Changes:** Add Privacy Policy and Ad Consent buttons.

```csharp
public class SettingsPanel : UIScreen
{
    [SerializeField] private Button _privacyPolicyButton;
    [SerializeField] private Button _adConsentButton;
    // ... existing buttons (mute, haptics, restart)
    
    protected override void OnEnable()
    {
        base.OnEnable();
        _privacyPolicyButton.onClick.AddListener(OnPrivacyPolicyClicked);
        _adConsentButton.onClick.AddListener(OnAdConsentClicked);
    }
    
    private void OnPrivacyPolicyClicked()
    {
        Application.OpenURL("https://yourwebsite.com/privacy-policy");
    }
    
    private void OnAdConsentClicked()
    {
        AdsManager.Instance.ShowConsentForm((bool success) =>
        {
            if (success)
            {
                ShowToast("Consent preferences updated");
            }
        });
    }
}
```

### 4.5 Level Progression Flow

**Old Flow:**
```
App Start → GameScene (Level 1) → Win → Level 2 → Win → Level 3...
```

**New Flow:**
```
App Start
  ↓
SessionManager.IncrementSessionCount()
  ↓
AdsManager.Initialize() (async, 3s timeout)
AnalyticsManager.Initialize() (async, 3s timeout)
  ↓
LevelSelectPanel
  ↓
User selects Level N
  ↓
Load Level N → Play → Outcome:
  ├─ Win → WinPanel → Check interstitial → Next Level or LevelSelectPanel
  └─ Lose → ContinuePanel → Watch Ad or Retry
```

---

## 5. SDK Integration & Dependencies

### 5.1 Required Unity Packages

**1. Google Mobile Ads Unity Plugin**
- **Version:** 9.1.0+ (latest stable)
- **Installation:** Via Unity Package Manager
  - Add package from git URL: `https://github.com/googleads/googleads-mobile-unity.git`
  - Or download `.unitypackage` from [GitHub Releases](https://github.com/googleads/googleads-mobile-unity/releases)
- **Includes:** AdMob SDK, UMP SDK (consent), External Dependency Manager (EDM4U)
- **Size Impact:** ~3-4MB

**2. Firebase Unity SDK**
- **Version:** 11.x+ (latest stable)
- **Installation:** Download from [Firebase Unity SDK](https://firebase.google.com/download/unity)
- **Required Packages:**
  - `FirebaseAnalytics.unitypackage`
  - `FirebaseRemoteConfig.unitypackage` (Phase 3)
  - `FirebaseCrashlytics.unitypackage` (Phase 3)
- **Size Impact:** ~4-5MB

**3. External Dependency Manager for Unity (EDM4U)**
- **Version:** Bundled with Google Mobile Ads plugin
- **Purpose:** Resolves Android/iOS native dependencies automatically
- **Configuration:** Auto-resolves on build

### 5.2 Firebase Project Setup

**Step 1: Create Firebase Project**
1. Go to [Firebase Console](https://console.firebase.google.com/)
2. Create new project: "Arrows Game"
3. Disable Google Analytics for Firebase if using separate Analytics instance (optional)

**Step 2: Add Android App**
1. Click "Add app" → Android
2. Package name: `com.serapkerem.arrows` (or your chosen package)
3. Download `google-services.json`
4. Place in: `Assets/Plugins/Android/google-services.json`
5. Enable Firebase Analytics in Firebase Console

**Step 3: Add iOS App (Phase 3)**
1. Click "Add app" → iOS
2. Bundle ID: `com.serapkerem.arrows`
3. Download `GoogleService-Info.plist`
4. Place in: `Assets/GoogleService-Info.plist` (root Assets folder)

### 5.3 AdMob App Setup

**Step 1: Create AdMob Account**
1. Go to [AdMob Console](https://apps.admob.com/)
2. Create new AdMob account (or link existing Google account)

**Step 2: Create AdMob App**
1. Apps → Add App → Select platform (Android)
2. App name: "Arrows"
3. Note the **AdMob App ID** (format: `ca-app-pub-XXXXXXXXXXXXXXXX~YYYYYYYYYY`)

**Step 3: Create Ad Units**
Create 3 ad units per platform:

**Android Ad Units:**
- Banner: "Arrows - Level Select Banner"
- Interstitial: "Arrows - Level Complete Interstitial"
- Rewarded: "Arrows - Continue Rewarded"

Note each **Ad Unit ID** (format: `ca-app-pub-XXXXXXXXXXXXXXXX/YYYYYYYYYY`)

**Step 4: Configure in Unity**
Update `AdsConfig.asset`:
- Android AdMob App ID
- Android Banner/Interstitial/Rewarded Ad Unit IDs
- Set `useTestAdUnits = true` for development builds

### 5.4 Platform-Specific Configuration

#### Android Configuration

**File:** `Assets/Plugins/Android/AndroidManifest.xml`

```xml
<?xml version="1.0" encoding="utf-8"?>
<manifest xmlns:android="http://schemas.android.com/apk/res/android">
    <application>
        <!-- AdMob App ID -->
        <meta-data
            android:name="com.google.android.gms.ads.APPLICATION_ID"
            android:value="ca-app-pub-XXXXXXXXXXXXXXXX~YYYYYYYYYY"/>
        
        <!-- Optional: Delay app measurement until MobileAds.Initialize() -->
        <meta-data
            android:name="com.google.android.gms.ads.DELAY_APP_MEASUREMENT_INIT"
            android:value="true"/>
    </application>
    
    <!-- Required permissions -->
    <uses-permission android:name="android.permission.INTERNET"/>
    <uses-permission android:name="android.permission.ACCESS_NETWORK_STATE"/>
</manifest>
```

**Build Settings:**
- Target SDK: Android 13 (API 33) or higher
- Minimum SDK: Android 8.0 (API 26)
- Scripting Backend: IL2CPP (required for 64-bit)
- Target Architectures: ARM64 (required), ARMv7 (optional)

#### iOS Configuration (Phase 3)

**File:** `Assets/Plugins/iOS/Info.plist` (created by build post-processor)

```xml
<key>GADApplicationIdentifier</key>
<string>ca-app-pub-XXXXXXXXXXXXXXXX~YYYYYYYYYY</string>

<!-- ATT Permission String -->
<key>NSUserTrackingUsageDescription</key>
<string>We use your data to provide personalized ads and improve your experience.</string>

<!-- SKAdNetwork IDs (for attribution) -->
<key>SKAdNetworkItems</key>
<array>
    <dict>
        <key>SKAdNetworkIdentifier</key>
        <string>cstr6suwn9.skadnetwork</string>
    </dict>
    <!-- Add all AdMob partner SKAdNetwork IDs from Google documentation -->
</array>
```

**Build Settings:**
- Target iOS: 14.0+
- Xcode Version: 14.0+
- Deployment Target: iOS 14.0
- Bitcode: Disabled (not supported by Unity 2022+)

### 5.5 Preprocessor Directives

**Ad Unit ID Selection:**
```csharp
public string GetBannerAdUnitId()
{
    #if UNITY_ANDROID
        return _config.useTestAdUnits 
            ? "ca-app-pub-3940256099942544/6300978111" // Test ID
            : _config.androidBannerAdUnitId; // Production ID
    #elif UNITY_IOS
        return _config.useTestAdUnits
            ? "ca-app-pub-3940256099942544/2934735716"
            : _config.iosBannerAdUnitId;
    #else
        return "unused"; // Editor
    #endif
}
```

**Mock Implementation (Editor):**
```csharp
#if UNITY_EDITOR
    if (_config.useMockAds)
    {
        return new MockAdsManager(); // Fake delays and callbacks
    }
#endif
```

### 5.6 Initialization Sequence

**App Startup (Bootstrap Scene):**
```csharp
public class AppInitializer : MonoBehaviour
{
    private void Start()
    {
        StartCoroutine(InitializeApp());
    }
    
    private IEnumerator InitializeApp()
    {
        // Step 1: Session tracking (sync)
        SessionManager.IncrementSessionCount();
        
        // Step 2: Initialize managers (async, parallel)
        bool adsReady = false;
        bool analyticsReady = false;
        
        AdsManager.Instance.Initialize(() => adsReady = true);
        AnalyticsManager.Instance.Initialize(() => analyticsReady = true);
        
        // Step 3: Wait max 3 seconds for both
        float timeout = 3f;
        float elapsed = 0f;
        
        while ((!adsReady || !analyticsReady) && elapsed < timeout)
        {
            elapsed += Time.deltaTime;
            yield return null;
        }
        
        if (!adsReady)
            TraceLogger.LogWarning("Ads initialization timed out");
        if (!analyticsReady)
            TraceLogger.LogWarning("Analytics initialization timed out");
        
        // Step 4: Proceed to game regardless of SDK status
        UIRootController.Instance.Show<LevelSelectPanel>();
    }
}
```

---

## 6. Testing Strategy & Error Handling

### 6.1 Testing Phases

#### Phase 1: Editor Testing (Mock Implementation)
- **Setup:** `AdsConfig.useMockAds = true`, `AnalyticsConfig.debugMode = true`
- **Mock AdsManager:**
  - Simulates 2-3 second load delays
  - 10% failure rate for ad loads
  - Logs all ad events to console with timestamps
- **Mock AnalyticsManager:**
  - Logs events to console with color coding
  - No Firebase SDK calls
- **Test Coverage:**
  - Full gameplay loop without SDK dependencies
  - UI flow verification (panels show/hide correctly)
  - Event firing validation (check console logs)

#### Phase 2: Device Testing (Test Ad Units)
- **Setup:** `AdsConfig.useTestAdUnits = true`, real SDKs
- **Android Test Device:** Mid-tier device (e.g., Samsung Galaxy A52, 2021)
- **Test Scenarios:**
  - Install → first session → verify no ads show (grace period)
  - Play 3 levels → verify interstitial shows on 4th level win
  - Lose all lives → verify ContinuePanel shows rewarded option
  - Background app during ad → verify graceful resume
  - Force close app mid-ad → verify no progression lost
- **Performance Testing:**
  - Frame rate monitoring during ads (target: maintain 60fps)
  - Memory usage before/after ad shows
  - Battery drain comparison (with vs without ads over 30min)

#### Phase 3: Firebase Integration Testing
- **Setup:** Real Firebase project, DebugView enabled
- **Validation:**
  - Open Firebase Console → DebugView
  - Trigger events in app → verify they appear in DebugView within 10s
  - Check event parameters match PRD schema exactly
  - Verify no duplicate events on scene reload
  - Test offline → online: events should batch and send when connected

#### Phase 4: Full Integration (Test Mode)
- **Setup:** Test ad units + real analytics
- **Duration:** 1 week internal testing with team
- **Metrics to Monitor:**
  - Ad fill rate (should be 100% with test ads)
  - Ad show rate (requested vs shown)
  - Crash rate (Firebase Crashlytics)
  - Frame drops during ad transitions

#### Phase 5: Production Testing (Soft Launch)
- **Setup:** Production ad units, limited region (e.g., Canada)
- **Sample Size:** 100-500 installs
- **Duration:** 2 weeks
- **Dashboards:** Daily monitoring of D1 retention, ARPDAU, crash rate

### 6.2 Ad Integration Test Checklist

**Critical Requirements (Per PRD):**

| Test Case | Expected Behavior | Status |
|-----------|------------------|--------|
| **FR-AD-04:** No ad during gameplay | Interstitial/Rewarded never show while StateManager is OnStart | ⬜ |
| **FR-AD-04:** Banner hidden during gameplay | Banner only visible on LevelSelectPanel | ⬜ |
| **FR-AD-05:** Reward on verified callback | Life only added when OnUserEarnedReward fires | ⬜ |
| **FR-AD-06:** Graceful ad failure | Interstitial failure = skip, Rewarded failure = hide button | ⬜ |
| **FR-AD-08:** Frequency cap respected | Min 60s gap between interstitials enforced | ⬜ |
| **FR-AD-08:** Grace period works | No interstitials in first 2 sessions | ⬜ |
| **Interstitial cadence** | Shows every 3rd level completion | ⬜ |
| **Rapid level completion** | Multiple rapid wins respect 60s min gap | ⬜ |
| **App background during ad** | Ad closes cleanly, game resumes correctly | ⬜ |
| **Consent flow (GDPR region)** | UMP form shows before first ad request | ⬜ |

### 6.3 Error Handling Strategies

#### SDK Initialization Failures

**Scenario:** AdMob or Firebase SDK fails to initialize (no internet, firewall, etc.)

**Handling:**
```csharp
private IEnumerator InitializeWithTimeout(float timeoutSeconds, Action onComplete)
{
    bool initialized = false;
    
    try
    {
        InitializeSDK(() => initialized = true);
    }
    catch (Exception e)
    {
        TraceLogger.LogError($"SDK init exception: {e.Message}");
        _sdkEnabled = false;
        onComplete?.Invoke();
        yield break;
    }
    
    float elapsed = 0f;
    while (!initialized && elapsed < timeoutSeconds)
    {
        elapsed += Time.deltaTime;
        yield return null;
    }
    
    if (!initialized)
    {
        TraceLogger.LogWarning("SDK init timed out, disabling");
        _sdkEnabled = false;
    }
    
    onComplete?.Invoke();
}
```

**Result:** Game proceeds to LevelSelectPanel regardless. Ads simply don't show.

---

#### Ad Load Failures

**1. Interstitial Fails to Load:**
```csharp
_interstitialAd.OnAdFailedToLoad += (sender, args) =>
{
    TraceLogger.LogWarning($"Interstitial load failed: {args.LoadAdError.GetMessage()}");
    AnalyticsManager.Instance.LogAdLoadFailed("interstitial", args.LoadAdError.GetCode().ToString());
    
    _interstitialReady = false;
    
    // Retry load after delay
    Invoke(nameof(LoadInterstitial), 30f);
};
```
**Result:** Next level loads immediately without ad, user progression not blocked.

**2. Rewarded Fails to Load:**
```csharp
_rewardedAd.OnAdFailedToLoad += (sender, args) =>
{
    TraceLogger.LogWarning($"Rewarded load failed: {args.LoadAdError.GetMessage()}");
    AnalyticsManager.Instance.LogAdLoadFailed("rewarded", args.LoadAdError.GetCode().ToString());
    
    _rewardedReady = false;
};

// In ContinuePanel
bool adReady = AdsManager.Instance.IsRewardedReady();
_watchAdButton.gameObject.SetActive(adReady); // Hide button if ad not ready
```
**Result:** "Watch Ad" button hidden, user sees only "Retry" option.

**3. Banner Fails to Load:**
```csharp
_bannerView.OnAdFailedToLoad += (sender, args) =>
{
    TraceLogger.LogWarning($"Banner load failed: {args.LoadAdError.GetMessage()}");
    _bannerContainer.SetActive(false); // Hide container to avoid blank space
    
    // Retry after delay
    Invoke(nameof(ShowBanner), 30f);
};
```
**Result:** Banner space hidden, no visual glitch.

---

#### Ad Show Failures

**Interstitial/Rewarded Closes Immediately:**
```csharp
_interstitialAd.OnAdOpened += (sender, args) =>
{
    _adShowStartTime = Time.realtimeSinceStartup;
};

_interstitialAd.OnAdClosed += (sender, args) =>
{
    float adDuration = Time.realtimeSinceStartup - _adShowStartTime;
    
    if (adDuration < 1f) // Ad closed too quickly, likely didn't render
    {
        TraceLogger.LogWarning("Interstitial closed immediately, treating as not shown");
        _levelCompletionsSinceLastAd--; // Don't count this against frequency cap
    }
    else
    {
        _lastInterstitialTime = Time.realtimeSinceStartup;
        _levelCompletionsSinceLastAd = 0;
    }
    
    DestroyInterstitial();
    LoadInterstitial(); // Pre-load next
};
```

---

#### Consent Flow Failures

**UMP SDK Fails to Load:**
```csharp
ConsentInformation.Update(requestParameters, (FormError error) =>
{
    if (error != null)
    {
        TraceLogger.LogWarning($"Consent update failed: {error.Message}");
        _consentStatus = "unknown";
        AnalyticsManager.Instance.LogConsentStatus("gdpr", "error");
        
        // Proceed with non-personalized ads
        RequestConfiguration requestConfig = new RequestConfiguration.Builder()
            .SetTagForUnderAgeOfConsent(TagForUnderAgeOfConsent.False)
            .build();
        MobileAds.SetRequestConfiguration(requestConfig);
        
        onComplete?.Invoke();
        return;
    }
    
    // Continue normal flow...
});
```

**Result:** App proceeds with non-personalized ads (GDPR-compliant fallback).

---

### 6.4 Performance Safeguards

#### Frame Rate Monitoring

```csharp
private float _lastFrameTime;
private int _frameDropCount;

private void Update()
{
    if (!_adCurrentlyShowing) return;
    
    float deltaTime = Time.unscaledDeltaTime;
    
    // Detect frame drops (>33ms = <30fps)
    if (deltaTime > 0.033f)
    {
        _frameDropCount++;
        
        if (_frameDropCount > 5) // 5 consecutive drops
        {
            TraceLogger.LogWarning("Sustained frame drops during ad display");
            AnalyticsManager.Instance.LogSettingsChanged("ad_performance", "frame_drops_detected");
            
            // Consider disabling future ads in this session (extreme case)
        }
    }
    else
    {
        _frameDropCount = 0;
    }
}
```

#### Memory Management

```csharp
private void DestroyBanner()
{
    if (_bannerView != null)
    {
        _bannerView.Destroy();
        _bannerView = null;
        TraceLogger.Log("Banner destroyed to free memory");
    }
}

private void DestroyInterstitial()
{
    if (_interstitialAd != null)
    {
        _interstitialAd.Destroy();
        _interstitialAd = null;
        TraceLogger.Log("Interstitial destroyed to free memory");
    }
}

// Destroy banner when leaving LevelSelectPanel
private void OnLevelSelectClosed()
{
    DestroyBanner();
}
```

**Memory Targets:**
- Banner: ~5MB persistent (only on LevelSelectPanel)
- Interstitial: ~10MB peak (destroyed after show)
- Rewarded: ~15MB peak (destroyed after show)
- Total SDK overhead: <50MB including Firebase

---

### 6.5 Debug Tools

#### In-Game Debug Panel (Development Builds Only)

```csharp
public class DebugPanel : MonoBehaviour
{
    #if DEVELOPMENT_BUILD || UNITY_EDITOR
    
    private void OnGUI()
    {
        GUILayout.BeginArea(new Rect(10, 10, 300, 500));
        GUILayout.Label($"Session: {SessionManager.GetSessionCount()}");
        GUILayout.Label($"Days Since Install: {SessionManager.GetDaysSinceInstall()}");
        GUILayout.Label($"Current Level: {SessionManager.GetCurrentLevel()}");
        
        GUILayout.Space(10);
        GUILayout.Label("=== Ads ===");
        GUILayout.Label($"Interstitial Ready: {AdsManager.Instance.IsInterstitialReady()}");
        GUILayout.Label($"Rewarded Ready: {AdsManager.Instance.IsRewardedReady()}");
        GUILayout.Label($"Levels Since Ad: {AdsManager.Instance.GetLevelsSinceLastAd()}");
        
        if (GUILayout.Button("Force Show Interstitial"))
        {
            AdsManager.Instance.ShowInterstitial("debug", null);
        }
        
        if (GUILayout.Button("Force Show Rewarded"))
        {
            AdsManager.Instance.ShowRewarded("debug", (granted) =>
            {
                Debug.Log($"Reward granted: {granted}");
            });
        }
        
        GUILayout.Space(10);
        if (GUILayout.Button("Reset Session Data"))
        {
            PlayerPrefs.DeleteAll();
            PlayerPrefs.Save();
            Debug.Log("Session data reset!");
        }
        
        if (GUILayout.Button("Toggle Ads"))
        {
            bool enabled = !AdsManager.Instance.IsEnabled();
            AdsManager.Instance.SetEnabled(enabled);
            Debug.Log($"Ads {(enabled ? "enabled" : "disabled")}");
        }
        
        GUILayout.EndArea();
    }
    
    #endif
}
```

#### Console Logging Levels

```csharp
public enum LogLevel
{
    None,      // Production: No logs
    Error,     // Production: Errors only
    Warning,   // Development: Warnings + Errors
    Info,      // Development: Key events (ads shown, analytics sent)
    Debug      // Debug: Verbose (every SDK call)
}

public static class TraceLogger
{
    private static LogLevel _currentLevel = LogLevel.Info;
    
    public static void SetLogLevel(LogLevel level)
    {
        _currentLevel = level;
    }
    
    public static void LogDebug(string message)
    {
        if (_currentLevel >= LogLevel.Debug)
            Debug.Log($"[DEBUG] {message}");
    }
    
    public static void Log(string message)
    {
        if (_currentLevel >= LogLevel.Info)
            Debug.Log($"[INFO] {message}");
    }
    
    public static void LogWarning(string message)
    {
        if (_currentLevel >= LogLevel.Warning)
            Debug.LogWarning($"[WARN] {message}");
    }
    
    public static void LogError(string message)
    {
        if (_currentLevel >= LogLevel.Error)
            Debug.LogError($"[ERROR] {message}");
    }
}
```

**Build Configuration:**
- **Development builds:** `LogLevel.Debug` (verbose)
- **Release builds:** `LogLevel.Error` (errors only)
- **Editor play mode:** `LogLevel.Info` (key events)

---

## 7. Data Flow & Implementation Phases

### 7.1 Complete Data Flow Diagrams

#### Level Completion Flow (with Interstitial)

```
User clears last line on level
  ↓
StateManager.SetOnWin()
  ↓
AnalyticsManager.LogLevelComplete(levelId, timeToComplete, lives, lineCount)
  ↓
WinPanel.Show()
  ├─ Display: Stars, time, "Next Level" button
  └─ AdsManager.LoadInterstitial() (pre-load for next transition)
  ↓
User taps "Next Level"
  ↓
AdsManager.ShouldShowInterstitial() check:
  ├─ Session count > gracePeriodSessionCount? (default: >2)
  ├─ Level completions % interstitialEveryNLevels == 0? (default: every 3)
  ├─ Time since last interstitial >= minGapSeconds? (default: >=60s)
  └─ IsInterstitialReady()?
  ↓
If ALL checks pass:
  ├─ AnalyticsManager.LogInterstitialRequested("level_complete", levelId)
  ├─ AdsManager.ShowInterstitial("level_complete", onClosed)
  ├─ AnalyticsManager.LogInterstitialShown("level_complete", levelId)
  ├─ Wait for ad close callback
  └─ On ad closed: LoadNextLevel()
  ↓
If ANY check fails:
  └─ LoadNextLevel() immediately
  ↓
LoadNextLevel():
  ├─ SessionManager.UnlockNextLevel()
  ├─ WinPanel.Close()
  └─ UIRootController.Show<LevelSelectPanel>()
```

---

#### Level Fail Flow (with Rewarded Continue)

```
User loses last life (collision or any failure)
  ↓
LivesManager.OnLifeLost event fires
  ↓
Check: CurrentLives == 0?
  ↓
If YES:
  ├─ StateManager.SetOnLose()
  ├─ AnalyticsManager.LogLevelFail(levelId, attemptNumber, failLineIndex)
  ├─ ContinuePanel.Show()
  └─ AnalyticsManager.LogContinueOffered(levelId, "rewarded_ad")
  ↓
ContinuePanel checks: AdsManager.IsRewardedReady()?
  ├─ If TRUE: Show "Watch Ad & Continue" button
  └─ If FALSE: Hide button, show only "Retry Level"
  ↓
User choice #1: "Watch Ad & Continue"
  ├─ AnalyticsManager.LogContinueAccepted(levelId, "rewarded_ad")
  ├─ AdsManager.ShowRewarded("continue", onComplete)
  ├─ Wait for reward callback
  ├─ OnUserEarnedReward:
  │   ├─ AnalyticsManager.LogRewardedCompleted("continue", "extra_life")
  │   ├─ LivesManager.AddLife(1)
  │   ├─ ContinuePanel.Close()
  │   └─ StateManager.SetOnStart() → Resume level mid-play
  └─ OnAdClosed without reward:
      ├─ AnalyticsManager.LogRewardedSkipped("continue", "extra_life")
      ├─ ContinuePanel.Close()
      └─ Return to LevelSelectPanel
  ↓
User choice #2: "Retry Level"
  ├─ AnalyticsManager.LogContinueAccepted(levelId, "retry")
  ├─ ContinuePanel.Close()
  ├─ LivesManager.ResetLives(5)
  └─ LevelManager.RestartLevel()
```

---

#### App Startup Flow

```
Unity Application Start
  ↓
[Bootstrap Scene or AppInitializer GameObject]
  ↓
SessionManager.IncrementSessionCount() (sync)
  ├─ Read session_count from PlayerPrefs
  ├─ Increment by 1
  ├─ If session_count == 1: Store install_date
  └─ Save to PlayerPrefs
  ↓
Start parallel initialization (both async, max 3s each):
  ├─ AdsManager.Initialize()
  │   ├─ Initialize UMP SDK (consent flow)
  │   │   ├─ Check ConsentInformation.ConsentStatus
  │   │   ├─ If Required: Show consent form
  │   │   └─ Log consent status to Analytics
  │   ├─ Initialize AdMob SDK
  │   │   ├─ MobileAds.Initialize()
  │   │   ├─ Pre-load interstitial
  │   │   └─ Pre-load rewarded
  │   └─ On complete/timeout: Set _adsReady flag
  │
  └─ AnalyticsManager.Initialize()
      ├─ Initialize Firebase SDK
      │   ├─ FirebaseApp.CheckAndFixDependenciesAsync()
      │   └─ FirebaseAnalytics.SetAnalyticsCollectionEnabled(true)
      ├─ Log lifecycle events:
      │   ├─ If session_count == 1: LogFirstSessionStart()
      │   └─ Else: LogAppOpen(session_count, days_since_install)
      └─ On complete/timeout: Set _analyticsReady flag
  ↓
Wait for both _adsReady AND _analyticsReady OR 3 seconds (whichever first)
  ↓
If timeout:
  ├─ TraceLogger.LogWarning("SDK initialization timed out")
  └─ Game proceeds anyway (fail-safe)
  ↓
UIRootController.Show<LevelSelectPanel>()
  ├─ AdsManager.ShowBanner() (if ads initialized)
  └─ Display level selection UI
```

---

### 7.2 Phased Implementation Timeline

#### **Phase 1: Analytics Foundation (Week 1-2)**

**Deliverables:**
- ✅ Firebase SDK integration (Analytics only, no AdMob yet)
- ✅ `AnalyticsManager` singleton with all event methods
- ✅ `AnalyticsConfigSO` ScriptableObject asset
- ✅ `SessionManager` utility class for session/cohort tracking
- ✅ Event hooks:
  - `StateManager.OnStateChanged` event (new)
  - `LivesManager.OnLifeLost` event (new)
  - `LineHeadCollisionDetector.OnHeadCollision` hookup
- ✅ `LevelSelectPanel` UI (no ads, just level selection)
- ✅ Enhanced `SettingsPanel` with placeholder buttons
- ✅ All lifecycle and gameplay analytics events firing

**Testing:**
- Firebase DebugView validation (all events appear with correct parameters)
- Session counting logic (install date, session increment)
- No duplicate events on scene reload/pause-resume
- No impact on frame rate (measure with Unity Profiler)

**Success Criteria:**
- All PRD Section 5.3.1–5.3.2 events logging correctly
- Firebase Console shows event data within 24 hours
- Game performance unchanged (60fps maintained)

**Files Created:**
```
Assets/_Game/Scripts/Analytics/
  ├─ AnalyticsManager.cs (~300 lines)
  ├─ AnalyticsConfigSO.cs (~30 lines)
  └─ AnalyticsEvents.cs (~100 lines)

Assets/_Game/Scripts/Session/
  └─ SessionManager.cs (~150 lines)

Assets/_Game/Scripts/UI/
  └─ LevelSelectPanel.cs (~200 lines)

Assets/_Game/Resources/Config/
  └─ AnalyticsConfig.asset

External:
  └─ Firebase Unity SDK (~4MB)
```

---

#### **Phase 2: AdMob Integration (Week 3-4)**

**Deliverables:**
- ✅ Google Mobile Ads SDK integration
- ✅ `AdsManager` singleton with banner/interstitial/rewarded support
- ✅ `AdsConfigSO` ScriptableObject asset (test ad unit IDs)
- ✅ UMP consent flow (GDPR/CCPA) integrated
- ✅ `ContinuePanel` UI with rewarded ad integration
- ✅ Banner ad integration in `LevelSelectPanel`
- ✅ Interstitial logic in `WinPanel`
- ✅ All monetization analytics events (PRD Section 5.3.3)
- ✅ Frequency cap logic (every N levels, min gap, grace period)

**Testing:**
- Test ad units only (never production IDs in dev builds)
- Banner shows/hides correctly on panel transitions
- Interstitial respects frequency caps and grace period
- Rewarded ad grants life only on verified callback
- Ad load failures don't block progression
- Frame rate monitoring during ad transitions
- Memory leak testing (play 20+ levels continuously)

**Success Criteria:**
- All three ad formats working on Android test device
- FR-AD-04 compliance verified (no ads during gameplay)
- Consent flow completes without blocking app startup
- 60fps maintained during ad transitions
- Ad-related analytics events logging correctly

**Files Created:**
```
Assets/_Game/Scripts/Monetization/
  ├─ AdsManager.cs (~400 lines)
  ├─ AdsConfigSO.cs (~50 lines)
  └─ AdPlacement.cs (~20 lines)

Assets/_Game/Scripts/UI/
  └─ ContinuePanel.cs (~150 lines)

Assets/_Game/Resources/Config/
  └─ AdsConfig.asset

Assets/Plugins/Android/
  ├─ google-services.json
  └─ AndroidManifest.xml (modified)

External:
  └─ Google Mobile Ads Unity Plugin (~3MB)
```

---

#### **Phase 3: Tuning & Hardening (Week 5)**

**Deliverables:**
- ✅ Firebase Remote Config integration
  - Ad frequency tuning without app update
  - Grace period adjustment remotely
- ✅ Firebase Crashlytics integration
  - Automatic crash reporting
  - Custom crash keys (level_id, session_count)
- ✅ iOS build setup
  - ATT prompt integration
  - iOS ad unit IDs configured
  - `GoogleService-Info.plist` added
- ✅ Debug panel for internal testing (dev builds only)
- ✅ Performance optimizations based on profiler results
- ✅ Edge case fixes from QA testing

**Testing:**
- Stress testing: Rapid level completion, app backgrounding during ads
- Memory leak testing: 1-hour continuous play session
- iOS-specific testing: ATT prompt, ad rendering on iPhone
- Remote Config: Change values in Firebase Console, verify app updates
- Crashlytics: Force crash, verify report appears in Firebase Console
- Cross-device testing: Low-end Android (2GB RAM), High-end iOS (iPhone 12+)

**Success Criteria:**
- Crash-free session rate ≥ 99.5%
- No memory leaks detected (heap snapshot comparison)
- Remote Config values update within 1 minute of app restart
- iOS ATT prompt shows before personalized ads
- All edge cases from QA resolved

**Files Created:**
```
Assets/_Game/Scripts/UI/
  └─ DebugPanel.cs (~100 lines)

Assets/_Game/Scripts/Monetization/
  └─ RemoteConfigManager.cs (~150 lines)

Assets/Plugins/iOS/
  └─ Info.plist (build post-processor)

Assets/GoogleService-Info.plist (iOS Firebase config)

External:
  ├─ FirebaseRemoteConfig.unitypackage (~1MB)
  └─ FirebaseCrashlytics.unitypackage (~1MB)
```

---

#### **Phase 4: Soft Launch Preparation (Week 6)**

**Deliverables:**
- ✅ Production ad unit IDs configured (not test IDs)
  - Separate ScriptableObject: `AdsConfig_Production.asset`
  - Build script switches config based on build type
- ✅ Privacy Policy hosted and linked
  - Host at: `https://yourwebsite.com/arrows-privacy-policy`
  - Link from `SettingsPanel`
- ✅ Google Play Store listing prepared
  - Screenshots, app description, privacy policy
  - Age rating questionnaire (PEGI/ESRB)
- ✅ Firebase Analytics dashboards configured
  - Retention dashboard (D1/D7/D30)
  - ARPDAU dashboard
  - Level funnel dashboard
  - Ad health dashboard (fill rate, eCPM)
- ✅ Automated alerts setup
  - Crash rate > 1%
  - D1 retention < 25%
  - Ad fill rate < 80%
- ✅ Soft launch build deployed to limited region (e.g., Canada)

**Monitoring (30 days post soft-launch):**
- **Daily:** Dashboard review (retention, ARPDAU, crash rate)
- **Weekly:** Level funnel analysis (identify drop-off points)
- **Weekly:** Ad health check (fill rate, eCPM by placement)
- **Ad-hoc:** User feedback review (Play Store reviews, support tickets)

**Success Criteria (per PRD Section 7):**
- D1 retention ≥ 30%
- D7 retention ≥ 10%
- ARPDAU baseline established (track for 30 days)
- Rewarded ad opt-in rate ≥ 25% on level fail
- Crash-free sessions ≥ 99.5%
- Level 10 completion rate measured (baseline for future levels)

**If success criteria met:** Proceed to global launch.  
**If not met:** Iterate on level difficulty, ad frequency, or onboarding flow based on data.

---

### 7.3 File Summary (Complete Implementation)

**New Files Created (Total: ~1,500 lines):**

```
Assets/_Game/Scripts/
├── Monetization/
│   ├── AdsManager.cs (~400 lines)
│   ├── AdsConfigSO.cs (~50 lines)
│   ├── AdPlacement.cs (~20 lines)
│   └── RemoteConfigManager.cs (~150 lines, Phase 3)
├── Analytics/
│   ├── AnalyticsManager.cs (~300 lines)
│   ├── AnalyticsConfigSO.cs (~30 lines)
│   └── AnalyticsEvents.cs (~100 lines)
├── Session/
│   └── SessionManager.cs (~150 lines)
├── UI/
│   ├── LevelSelectPanel.cs (~200 lines)
│   ├── ContinuePanel.cs (~150 lines)
│   └── DebugPanel.cs (~100 lines, dev builds only)
└── _Enums/
    └── AdType.cs (~10 lines)
```

**Modified Existing Files (~100 lines of changes):**

```
Assets/SerapKeremGameKit/Scripts/
├── LevelSystem/
│   └── StateManager.cs (+20 lines: OnStateChanged event)
├── UI/
│   ├── LivesManager.cs (+15 lines: OnLifeLost event)
│   ├── Screens/
│   │   ├── WinPanel.cs (+30 lines: interstitial check)
│   │   └── SettingsPanel.cs (+20 lines: Privacy/Consent buttons)
│   └── Core/
│       └── UIRootController.cs (+15 lines: LevelSelectPanel registration)
```

**Configuration Assets:**

```
Assets/_Game/Resources/Config/
├── AdsConfig.asset (development)
├── AdsConfig_Production.asset (soft launch/production)
└── AnalyticsConfig.asset
```

**External SDKs (~8-10MB total):**
- Firebase Analytics (~2MB)
- Firebase Remote Config (~1MB)
- Firebase Crashlytics (~1MB)
- Google Mobile Ads Unity Plugin (~3MB)
- External Dependency Manager (~1MB)

**Platform-Specific Files:**

```
Assets/Plugins/Android/
├── google-services.json (Firebase config)
└── AndroidManifest.xml (AdMob App ID)

Assets/
└── GoogleService-Info.plist (iOS Firebase config)

Assets/Plugins/iOS/
└── (Build post-processor generates Info.plist with ATT prompt)
```

---

## 8. Success Metrics

### 8.1 Soft Launch Targets (30 days)

| Metric | Target | Measurement Source |
|--------|--------|-------------------|
| D1 Retention | ≥ 30% | Firebase Analytics cohorts |
| D7 Retention | ≥ 10% | Firebase Analytics cohorts |
| D30 Retention | ≥ 5% | Firebase Analytics cohorts |
| ARPDAU | Baseline TBD | AdMob revenue / Firebase DAU |
| Rewarded Ad Opt-In Rate | ≥ 25% | `continue_accepted` / `continue_offered` |
| Crash-Free Sessions | ≥ 99.5% | Firebase Crashlytics |
| Ad Fill Rate (Interstitial) | ≥ 90% | AdMob console |
| Ad Fill Rate (Rewarded) | ≥ 90% | AdMob console |
| Level 10 Completion Rate | Measure baseline | `level_complete(level_10)` / `first_session_start` |
| Average Session Length | ≥ 5 minutes | `session_end` / `app_open` |

### 8.2 Performance Targets

| Metric | Target | Measurement Method |
|--------|--------|-------------------|
| Frame Rate (Gameplay) | 60 FPS | Unity Profiler, on mid-tier device |
| Frame Rate (Ad Transition) | ≥ 30 FPS | Unity Profiler during ad show |
| App Launch Time | < 5 seconds | Time to LevelSelectPanel visible |
| Memory Usage (Peak) | < 200MB | Android Memory Profiler |
| APK Size (Android) | < 80MB | Build output |
| IPA Size (iOS) | < 100MB | Build output |

### 8.3 Funnel Metrics to Monitor

**Level Progression Funnel:**
```
Level 1 Start (100% of installs)
  ↓
Level 1 Complete (target: ≥80%)
  ↓
Level 3 Complete (target: ≥60%)
  ↓
Level 5 Complete (target: ≥40%)
  ↓
Level 10 Complete (target: ≥20%)
```

**Monetization Funnel:**
```
Level Fail (0 lives)
  ↓
Continue Offer Shown (100% of fails)
  ↓
Rewarded Ad Requested (target: ≥30%)
  ↓
Rewarded Ad Completed (target: ≥90% of requested)
  ↓
Player Continues (100% of completed)
```

### 8.4 Ad Health Dashboards

**Daily Monitoring (Firebase Console + AdMob):**

1. **Ad Fill Rate Dashboard:**
   - Interstitial fill rate by day
   - Rewarded fill rate by day
   - Banner impressions per session
   - Alert if any < 80%

2. **eCPM Dashboard:**
   - Interstitial eCPM trend
   - Rewarded eCPM trend
   - Banner eCPM trend
   - Compare by region/platform

3. **Ad Frequency Dashboard:**
   - Avg interstitials per user per session
   - % of users who see rewarded ads
   - Interstitial show rate (shown / requested)
   - Rewarded completion rate (completed / requested)

4. **User Experience Dashboard:**
   - D1 retention segmented by "saw ad" vs "no ad"
   - Session length for users with ads vs without
   - Level fail rate after seeing interstitial vs without

### 8.5 Decision Points

**If D1 retention < 30%:**
- Check level funnel: Which level has highest drop-off?
- Review interstitial frequency: Too aggressive?
- A/B test: Increase grace period to 3 sessions

**If rewarded opt-in < 25%:**
- Check rewarded fill rate: Are ads failing to load?
- Review UI: Is "Watch Ad" button clear enough?
- Consider: Increase reward (2 lives instead of 1)

**If crash rate > 1%:**
- Review Crashlytics top crashes
- Identify: Ad SDK issue vs gameplay issue?
- Hotfix within 48 hours if critical

**If ARPDAU below expectations:**
- Check eCPM trends: Declining over time?
- Experiment with interstitial frequency (increase from 3 to 2 levels)
- Test rewarded placement on LevelSelectPanel (hint unlock)

---

## 9. Appendix

### 9.1 Key PRD Requirements Mapping

| PRD Requirement | Implementation | File(s) |
|----------------|---------------|---------|
| FR-AD-01: Config-based ad unit IDs | `AdsConfigSO` | `AdsConfigSO.cs`, `AdsConfig.asset` |
| FR-AD-02: Platform-agnostic AdsManager | Singleton pattern | `AdsManager.cs` |
| FR-AD-03: Pre-load ads | Load on win, show on next screen | `AdsManager.cs`, `WinPanel.cs` |
| FR-AD-04: No ads during gameplay | StateManager checks | `AdsManager.ShouldShowInterstitial()` |
| FR-AD-05: Verified reward callback | OnUserEarnedReward only | `AdsManager.ShowRewarded()` |
| FR-AD-06: Graceful ad failure | Hide button or skip | `ContinuePanel.cs`, `AdsManager.cs` |
| FR-AD-07: Consent (UMP/ATT) | UMP SDK integration | `AdsManager.InitializeConsent()` |
| FR-AD-08: Remote tunable frequency | ScriptableObject → Remote Config | `AdsConfigSO.cs`, `RemoteConfigManager.cs` |
| FR-AN-01: No duplicate events | Idempotency guards | `AnalyticsManager.cs` |
| FR-AN-02: Decoupled analytics | Manager pattern | `AnalyticsManager.cs` |
| FR-AN-03: No PII logged | Event parameter validation | `AnalyticsManager.cs` |
| FR-AN-04: Respect consent | Check UMP status | `AnalyticsManager.Initialize()` |
| FR-AN-05: Stable level_id | "level_01" format | `LevelManager.GetCurrentLevelId()` |

### 9.2 Glossary

| Term | Definition |
|------|-----------|
| **ARPDAU** | Average Revenue Per Daily Active User (total revenue / DAU) |
| **ATT** | App Tracking Transparency (iOS consent framework) |
| **DAU** | Daily Active Users |
| **eCPM** | Effective Cost Per Mille (revenue per 1000 ad impressions) |
| **EDM4U** | External Dependency Manager for Unity (resolves native dependencies) |
| **Fill Rate** | % of ad requests that return an ad (requested / filled) |
| **GDPR** | General Data Protection Regulation (EU privacy law) |
| **CCPA** | California Consumer Privacy Act (California privacy law) |
| **UMP** | User Messaging Platform (Google's consent SDK) |
| **Soft Launch** | Limited-region release for testing before global launch |
| **Grace Period** | Time/sessions during which no ads are shown (retention optimization) |

### 9.3 External Resources

**Documentation:**
- [Google Mobile Ads Unity Plugin Docs](https://developers.google.com/admob/unity/start)
- [Firebase Unity SDK Setup](https://firebase.google.com/docs/unity/setup)
- [UMP SDK Integration Guide](https://developers.google.com/admob/unity/privacy)
- [AdMob Best Practices](https://support.google.com/admob/answer/6128877)

**Tools:**
- [Firebase Console](https://console.firebase.google.com/)
- [AdMob Console](https://apps.admob.com/)
- [Unity Profiler](https://docs.unity3d.com/Manual/Profiler.html)
- [Android Memory Profiler](https://developer.android.com/studio/profile/memory-profiler)

**Privacy Policy Templates:**
- [App Privacy Policy Generator](https://app-privacy-policy-generator.nisrulz.com/)
- [AdMob Privacy Requirements](https://support.google.com/admob/answer/9449105)

---

## Change Log

| Version | Date | Author | Changes |
|---------|------|--------|---------|
| 1.0 | 2026-06-18 | AI Assistant | Initial design document based on PRD |

---

**End of Design Document**

This design is ready for review and implementation planning. Upon approval, the next step is to create a detailed implementation plan using the `writing-plans` skill.
