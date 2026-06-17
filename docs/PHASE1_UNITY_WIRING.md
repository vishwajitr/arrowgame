# Phase 1 — Unity editor wiring (temp_arrows)

The runnable Unity project is **`temp_arrows/`**. Analytics bootstrap runs at runtime (`AppBootstrap`); no `AnalyticsManager` GameObject is strictly required in the scene.

## Optional: `AnalyticsConfig` asset

- A default config is loaded from **`Resources/DefaultAnalyticsConfig`**.
- To tune values in the Editor: duplicate `Assets/Resources/DefaultAnalyticsConfig.asset` or edit it directly.

## Level select UI (Task 13)

1. In **GameScene** (or your menu canvas), add an empty child under the main UI canvas, e.g. `LevelSelectPanel`.
2. Add **`CanvasGroup`** (required by `UIPanel`), **`LevelSelectPanel`** component.
3. Wire **canvasGroup** on `UIPanel` (drag the same object’s `CanvasGroup`).
4. Create a **horizontal or grid layout** under the panel; assign its `RectTransform` to **`_levelButtonContainer`**.
5. Create one **Button** with a **Text** on the **same GameObject** as the `Button` (script uses `GetComponent<Text>()`).
6. Save that button as a **prefab** and assign it to **`_levelButtonPrefab`**.
7. Set **`_totalLevels`** to match your `LevelManager` gameplay level count (default 20).
8. Hook a menu control to **`LevelSelectPanel.Show()`** as needed.

## Privacy policy button (Task 20)

1. Open the **Settings** prefab / hierarchy used by `SettingsPanel`.
2. Add a **Button**; assign it to **`_privacyPolicyButton`** on `SettingsPanel`.
3. Set **`_privacyPolicyUrl`** to your real policy URL (placeholder is `https://example.com/privacy`).

## Play mode checks

- With **debug mode** on in `DefaultAnalyticsConfig`, watch the **Console** for `[Analytics]` lines.
- First launch should emit **`first_session_start`** once (after `IncrementSessionCount`).
