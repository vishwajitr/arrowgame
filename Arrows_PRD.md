# 🎯➡️ Arrows — Product Requirements Document

**Mobile Puzzle Game — Unity (URP)**
Repository: github.com/SERAP-KEREM/Arrows
Version 1.0 | Status: Draft for Review
Date: June 18, 2026

> Scope of this revision: Adds monetization (Google AdMob) and analytics/telemetry requirements on top of the existing core gameplay loop.

---

## Table of Contents

1. [Overview](#1-overview)
2. [Target Players & Use Cases](#2-target-players--use-cases)
3. [Core Gameplay (Existing Foundation)](#3-core-gameplay-existing-foundation)
4. [Monetization — Google AdMob Integration](#4-monetization--google-admob-integration)
5. [Analytics & Telemetry](#5-analytics--telemetry)
6. [Architecture & Integration Notes](#6-architecture--integration-notes)
7. [Success Metrics](#7-success-metrics)
8. [Phased Release Plan](#8-phased-release-plan)

---

## 1. Overview

### 1.1 Summary

Arrows is a single-screen, session-based line puzzle game built in Unity 6 (URP). Players tap animated lines to send them moving forward along a fixed path; lines shrink from the tail as they travel and disappear once fully traveled. The central skill is sequencing taps correctly so that no two moving line-heads collide. A collision costs one life; the level is won once every line on the board has fully cleared.

The existing build (per the public repository) implements the core mechanic, 10 hand-built levels, a 5-life system, win/lose states, camera auto-framing, audio/haptics, and a custom internal framework (SerapKeremGameKit) covering pooling, state management, and UI panels. This PRD defines the requirements to take that prototype to a monetizable, measurable mobile release by adding a Google AdMob ad stack and a first-party analytics/telemetry layer, alongside the supporting product surfaces (level select, settings, IAP-ready economy hooks) those systems depend on.

### 1.2 Goals

- **Ship a monetizable build:** integrate AdMob banner, interstitial, and rewarded ad formats without degrading the core puzzle-solving experience.
- **Instrument the funnel:** capture acquisition-to-retention and monetization events end-to-end so the team can make data-backed decisions on level difficulty, pacing, and ad placement.
- **Preserve game feel:** all monetization and tracking must be additive — no regression to frame pacing, input latency, or the animation/collision systems already built.
- **Establish a scalable level pipeline:** move from 10 fixed levels to a structure that supports ongoing content drops, gated in part by ad-supported continues.

### 1.3 Non-Goals

- Redesigning core line/collision mechanics — out of scope; this PRD treats the existing gameplay system as the stable foundation.
- Hard-currency IAP store, battle pass, or live-ops events — flagged as a fast-follow, not part of this release.
- Cross-platform account sync / cloud save — not required for v1; local persistence only.
- Multiplayer or asynchronous PvP features.

### 1.4 Target Platforms

| Platform | Minimum OS | Notes |
|---|---|---|
| Android | Android 8.0 (API 26)+ | Primary launch platform; Google Play Console + AdMob native fit |
| iOS | iOS 14+ | Requires App Tracking Transparency (ATT) prompt before personalized ads |

---

## 2. Target Players & Use Cases

### 2.1 Player Profile

Casual mobile puzzle players who play in short, frequent sessions (commute, waiting, pre-sleep). They are drawn to games with low setup cost, immediate legibility (clear win/lose state), and a tactile, satisfying core action — similar audiences to games like Unblock Me, Block Puzzle, or Tangle Tower-style line puzzles.

### 2.2 Core Use Cases

- **Quick session play:** open the app, complete one or two levels in under 3 minutes, close the app.
- **Stuck-and-retry:** fail a level, watch a rewarded ad for an extra life or hint rather than restarting from scratch.
- **Progression binge:** longer sessions where a player clears a block of levels in sequence, encountering interstitials at natural breakpoints.
- **Return visit:** player reopens the app after a gap; the game should resume exactly where they left off (current level, lives, settings).

---

## 3. Core Gameplay (Existing Foundation)

This section documents the current implementation as the baseline this PRD builds on top of. It is descriptive, not new scope, except where marked **[NEW]**.

### 3.1 Core Loop

- Each level presents a fixed arrangement of straight lines (Line Renderer–based) on a board framed automatically by the CameraManager.
- Tapping/clicking an inactive line activates it; the LineAnimation system moves it forward using a pooled Vector3 array (zero-allocation) with a DOTween-driven tween.
- As a line moves, it visually erases from its tail, simulating a shrinking trail, and is destroyed by LineDestroyer once it fully exits.
- A LineRendererHead object tracks the visual tip of each moving line for legibility.
- LineHeadCollisionDetector checks for head-to-line collisions in real time. On collision, LineMaterialHandler swaps materials/colors to flag the error, and the player loses one life via LivesManager.
- Once activated, a line cannot be reactivated or paused — commitment is immediate, which is the core tension of the puzzle.
- Level outcome: clearing every line = win (StateManager → OnWin); exhausting all 5 lives = lose (StateManager → OnLose).

### 3.2 Existing Systems Inventory

| System | Responsibility |
|---|---|
| StateManager | Centralized game states: Loading, OnStart, OnWin, OnLose |
| LivesManager | Singleton life count tracking (starts at 5), heart-based UI |
| CameraManager | Auto-frames camera to fit all lines in the current level |
| Level System | Prefab-based level loading; 10 levels with increasing difficulty |
| SerapKeremGameKit | Logging, pooled audio, cross-platform haptics, particle pooling, panel-based UI framework, currency/wallet primitives, guarded MonoSingleton base |

### 3.3 [NEW] Required Gameplay-Adjacent Additions

The following surfaces do not exist in the current build but are required to support monetization and analytics meaningfully, and are therefore in scope for this PRD:

- **Level Select / Map screen:** replacing implicit linear progression with an explicit screen, since ad placement and analytics funnels need discrete, nameable checkpoints (level_id) rather than an undifferentiated sequence.
- **Pause / Settings panel:** exposing mute, haptics toggle, restart, and (per store policy) a privacy/ad-consent entry point.
- **Continue-on-fail prompt:** the OnLose state must branch into an offer screen ("Watch ad to continue" vs. "Retry level") rather than dropping straight to retry.
- **Daily session counter / first-session flag:** lightweight local state needed to drive analytics cohorting (Day 0/1/7/30) and ad frequency capping logic.

---

## 4. Monetization — Google AdMob Integration

### 4.1 Objective

Introduce a non-intrusive ad layer using the Google Mobile Ads (AdMob) Unity plugin that monetizes natural break points in the core loop (level transitions, failure recovery) without interrupting active puzzle-solving, which must remain ad-free.

### 4.2 Ad Formats & Placement

| Format | Placement | Trigger | Frequency Cap |
|---|---|---|---|
| Banner | Level Select screen only | Persistent while screen is open; auto-refresh per AdMob default | N/A (static) |
| Interstitial | Between levels, on return to Level Select | Every Nth level win (default N=3, tunable remotely) | Min. 60s between interstitials; never on first 2 sessions (Day-0 grace) |
| Rewarded Video | Fail screen, Level Select | Player-initiated only: "+1 Life & Continue" or "Unlock Hint" | Max 1 per attempt for continue; max 3/level for hints |
| Rewarded Interstitial | App resume after long absence (optional, fast-follow) | Soft offer for bonus currency on relaunch | Max 1/day |

### 4.3 Functional Requirements

- **FR-AD-01:** Integrate the Google Mobile Ads Unity plugin (latest stable) with separate ad unit IDs configured per platform (Android/iOS) and per environment (test vs. production), loaded via a config asset — never hardcoded inline in scene scripts.
- **FR-AD-02:** All ad calls must be wrapped in an AdsManager singleton (consistent with the existing guarded MonoSingleton pattern in SerapKeremGameKit) exposing platform-agnostic methods: `ShowBanner()`, `HideBanner()`, `LoadInterstitial()`, `ShowInterstitial()`, `LoadRewarded()`, `ShowRewarded(onReward, onFail)`.
- **FR-AD-03:** Interstitial and rewarded ads must be pre-loaded ahead of the moment they're needed (load-on-win, show-on-next-screen) to avoid a visible loading stall.
- **FR-AD-04:** No ad may be shown while a line is actively animating or mid-collision-resolution — the game must reach a stable StateManager state (OnWin/OnLose/Loading) before any ad call fires.
- **FR-AD-05:** Rewarded ad rewards are only granted on the SDK's verified reward callback, never optimistically on ad start.
- **FR-AD-06:** If an ad fails to load, the game must fail gracefully — hide the offer button (for rewarded) or silently skip (for interstitial) rather than blocking progression.
- **FR-AD-07:** Respect platform consent requirements: implement Google's User Messaging Platform (UMP) SDK for GDPR/CCPA consent collection, and the iOS App Tracking Transparency (ATT) prompt prior to requesting personalized ads.
- **FR-AD-08:** Ad frequency, the interstitial "every Nth level" value, and grace-period session count must be exposed as remotely tunable values (e.g., via Firebase Remote Config or an equivalent) rather than compiled constants.

### 4.4 Non-Functional Requirements

- Ad SDK initialization must not block the splash/loading screen beyond a 3-second timeout; the game proceeds to OnStart regardless of ad-load completion.
- Memory and battery overhead from the ad SDK must not measurably affect line-animation frame pacing (target: maintain 60fps on mid-tier devices, e.g., a 2021-era Android device).
- All test builds must use AdMob's official test ad unit IDs; production ad unit IDs are only enabled in release/store builds, gated by build configuration.

### 4.5 Open Questions

- Should hint-unlock rewarded ads be capped per level or per day globally?
- Is a "remove ads" one-time IAP in scope for v1, or deferred to the fast-follow IAP economy work?

---

## 5. Analytics & Telemetry

### 5.1 Objective

Instrument the full player journey — install through level completion through ad interaction — so the team can measure retention, level difficulty/drop-off, and ad revenue performance, and iterate on level design and monetization pacing with real data rather than guesswork.

### 5.2 Proposed Stack

- Primary analytics SDK: Firebase Analytics (Google Analytics for Firebase) — free tier, integrates natively alongside AdMob and supports Remote Config + Crashlytics in the same console.
- Ad revenue measurement: Firebase/AdMob linked reporting for impression-level revenue (eCPM, ARPDAU) cross-referenced with gameplay events.
- Crash & stability: Firebase Crashlytics, since stability issues directly confound retention metrics.

### 5.3 Event Taxonomy

All events funnel through a single AnalyticsManager singleton (mirroring the AdsManager pattern) so the SDK can be swapped or dual-logged without touching gameplay code.

#### 5.3.1 Lifecycle Events

| Event | Parameters | Purpose |
|---|---|---|
| app_open | session_count, days_since_install | Session cadence, DAU |
| first_session_start | platform, app_version | New install tracking / D0 cohort |
| session_end | session_length_sec | Engagement depth |

#### 5.3.2 Gameplay Events

| Event | Parameters | Purpose |
|---|---|---|
| level_start | level_id, attempt_number, lives_remaining | Funnel entry, retry rate |
| level_complete | level_id, time_to_complete_sec, lives_remaining, lines_count | Difficulty tuning, pacing |
| level_fail | level_id, attempt_number, fail_line_index | Drop-off / difficulty spikes |
| line_collision | level_id, line_id, lives_remaining_after | Granular difficulty heatmaps per line |
| life_lost | level_id, lives_remaining | Pacing of failure pressure |
| continue_offered | level_id, offer_type (ad/retry) | Recovery funnel top |
| continue_accepted | level_id, offer_type | Recovery funnel conversion |

#### 5.3.3 Monetization Events

| Event | Parameters | Purpose |
|---|---|---|
| ad_impression | ad_format, placement, ad_unit_id | Linked with AdMob for revenue-by-placement (auto-logged via Firebase/AdMob link) |
| interstitial_requested | placement, level_id | Fill-rate diagnostics |
| interstitial_shown | placement, level_id | Show-rate vs. request-rate |
| rewarded_requested | placement, reward_type | Demand signal for rewarded inventory |
| rewarded_completed | placement, reward_type | Verified reward grants, ARPDAU input |
| rewarded_skipped | placement, reward_type | Offer rejection rate |
| ad_load_failed | ad_format, error_code | Fill-rate / SDK health monitoring |

#### 5.3.4 Settings & Consent Events

| Event | Parameters | Purpose |
|---|---|---|
| consent_status_set | consent_type (gdpr/att), status | Compliance audit trail, personalized-ad eligibility |
| settings_changed | setting_name, new_value | Feature usage (mute/haptics adoption) |

### 5.4 Funnels & Dashboards to Build

- **Level funnel:** level_start → level_complete / level_fail per level_id, to surface the exact level where drop-off spikes (key input to rebalancing the existing 10 levels and future ones).
- **Retention curves:** D1/D7/D30 retention from first_session_start, segmented by whether the player engaged with rewarded ads (continue_accepted) in their first session.
- **ARPDAU / eCPM by placement:** ad_impression revenue joined with placement, to validate whether the interstitial cadence (every Nth level) is set correctly versus rewarded engagement.
- **Ad health:** request-to-show ratio and ad_load_failed rate by format and platform, to catch SDK or fill-rate regressions quickly.

### 5.5 Functional Requirements

- **FR-AN-01:** Every event in Section 5.3 must fire exactly once per logical occurrence (no duplicate firing on scene reload or pause/resume).
- **FR-AN-02:** Event logging must be decoupled from gameplay logic via the AnalyticsManager interface so gameplay code never references the Firebase SDK directly.
- **FR-AN-03:** No personally identifiable information (PII) is logged in any event payload.
- **FR-AN-04:** Analytics collection must respect the consent state captured via the UMP flow (Section 4.3, FR-AD-07) — non-essential/advertising-linked analytics must be suppressible per regional consent law.
- **FR-AN-05:** level_id must use a stable, human-readable identifier (e.g., level_01) that persists across content updates, so historical funnel data remains comparable as new levels are added.

---

## 6. Architecture & Integration Notes

### 6.1 Where New Systems Sit

Both AdsManager and AnalyticsManager should be implemented as guarded MonoSingletons, consistent with SerapKeremGameKit's existing architecture pattern, and should listen to StateManager transitions rather than being called ad hoc from gameplay scripts. This keeps the line/collision/animation code completely unaware that ads or analytics exist — a regression-safety boundary, since that code is the most performance- and correctness-sensitive part of the existing build.

### 6.2 Suggested Event Hook Points

| StateManager Transition | Triggers |
|---|---|
| Loading → OnStart | level_start; pre-load next interstitial/rewarded if not already cached |
| OnStart → OnWin | level_complete; check interstitial cadence counter; show banner on return to Level Select |
| OnStart → OnLose | level_fail, life_lost; present continue_offered (rewarded) vs. retry |
| Collision event (LineHeadCollisionDetector) | line_collision (fired pre-state-transition, granular) |

### 6.3 Dependencies & Risks

| Risk | Impact | Mitigation |
|---|---|---|
| Ad SDK init delay on cold start | Perceived launch slowness | Async init with 3s timeout; never block first frame (FR-AD covers this) |
| Over-frequent interstitials hurting retention | Lower D1/D7, App Store ratings | Remote-config cadence + Day-0/1 grace period; monitor via retention dashboard |
| Consent flow blocking EU/Brazil/California users | Compliance risk, store rejection | Implement Google UMP SDK before any ad request in those regions |
| Duplicate analytics events on scene reload | Corrupted funnel data | Centralize firing in AnalyticsManager with idempotency guard (FR-AN-01) |

---

## 7. Success Metrics

| Metric | Target (90 days post-launch) | Source |
|---|---|---|
| D1 Retention | ≥ 30% | Firebase Analytics cohorts |
| D7 Retention | ≥ 10% | Firebase Analytics cohorts |
| ARPDAU | Baseline established in first 30 days, then +X% QoQ (TBD with finance) | AdMob + Firebase link |
| Rewarded ad opt-in rate on fail | ≥ 25% of continue_offered events | continue_accepted / continue_offered |
| Crash-free sessions | ≥ 99.5% | Crashlytics |
| Level 10 completion rate (from installs) | Baseline measurement; informs future level pacing | Level funnel (Section 5.4) |

---

## 8. Phased Release Plan

### Phase 1 — Analytics Foundation
- Integrate Firebase SDK, AnalyticsManager singleton, lifecycle + gameplay events (Sections 5.3.1–5.3.2).
- Build Level Select screen and continue-on-fail prompt (Section 3.3) — required scaffolding for both ads and analytics.

### Phase 2 — AdMob Integration
- Integrate Google Mobile Ads SDK + UMP consent flow.
- Implement AdsManager with banner, interstitial, rewarded flows per Section 4.
- Wire monetization events (Section 5.3.3) end-to-end.

### Phase 3 — Tuning & Hardening
- Remote Config rollout for ad cadence and grace-period values.
- QA pass specifically on ad-timing-vs-gameplay-state edge cases (FR-AD-04).
- Crashlytics integration and stability bake.

### Phase 4 — Soft Launch
- Limited-region release to validate D1/D7 retention and ARPDAU targets before global launch.
- Dashboard review cadence: weekly funnel and ad-health review during soft launch.
