# 🗡️ Greysword — Antigravity AI Brain Game

> **Hackathon Submission** | Unity 3D Zombie Survival with Real-Time Adaptive AI Difficulty Engine

![Unity](https://img.shields.io/badge/Engine-Unity%202022.3%2B-black?logo=unity)
![Python](https://img.shields.io/badge/Brain%20Server-Python%203.8%2B-blue?logo=python)
![Platform](https://img.shields.io/badge/Platform-PC%20%7C%20Android-green)
![License](https://img.shields.io/badge/License-Hackathon%20Entry-orange)

---

## 📖 Table of Contents

1. [Project Overview](#-project-overview)
2. [Solution Design](#-solution-design)
3. [Architecture](#-architecture)
4. [Agents Developed](#-agents-developed)
5. [APIs Used](#-apis-used)
6. [Integration Details](#-integration-details)
7. [Difficulty Profile System](#-difficulty-profile-system)
8. [Data Flow & Decision Loop](#-data-flow--decision-loop)
9. [Key Scripts Reference](#-key-scripts-reference)
10. [How to Run](#-how-to-run)

---

## 🎮 Project Overview

**Greysword** is a third-person zombie survival game where the game's AI engine — called **Antigravity Brain** — watches how you play in real-time and changes the difficulty dynamically to keep you in a perfect engagement state: not too bored, not too frustrated.

The core innovation is that difficulty is **not a slider set by the player**. It is an inference system that observes behavior signals, classifies the player into a psychological state, and applies targeted game parameter changes — every 5 seconds of gameplay.

---

## 🧠 Solution Design

### The Problem We Solve

Traditional games offer "Easy / Normal / Hard" difficulty modes set at the start. This creates two failure states:
- **Too easy** → boredom, disengagement, players quit
- **Too hard** → frustration, rage-quit, negative experience

### Our Approach: Agentic Adaptive Difficulty

The **Antigravity Brain** is an agentic AI loop running alongside the game. It follows a strict **Observe → Infer → Decide → Act → Evaluate** cycle:

| Phase | What Happens |
|---|---|
| **Observe** | Collect 8 behavioral signals from the player every 5 seconds |
| **Infer** | Classify player into one of 6 psychological states using weighted scoring |
| **Decide** | Select a targeted difficulty adjustment based on the inferred state |
| **Validate** | Run QC rules to prevent unfair or extreme adjustments |
| **Act** | Apply the new `DifficultyProfile` to all active game systems in real-time |
| **Evaluate** | Compare HP and kill count to previous cycle to measure decision effectiveness |

This loop repeats continuously throughout the game session, logging every decision for full transparency.

---

## 🏗️ Architecture

The system is split into two layers:

### Layer 1 — External AI Brain Server (Python)
`antigravity_brain_server.py` is a standalone Python HTTP server that acts as the remote reasoning engine.

```
┌──────────────────────────────────────────────────────────────────┐
│                    ANTIGRAVITY BRAIN SERVER                      │
│                    (Python · Port 8080)                          │
│                                                                  │
│  POST /session_start  → Clear buffer, start recording session    │
│  POST /evaluate       → Receive snapshot → Infer → Respond       │
│  POST /log            → Receive outcome log from game client      │
│  POST /session_end    → Flush session to disk + history log      │
│                                                                  │
│  All sessions saved to:                                          │
│    antigravity_server_log.txt   (current session)                │
│    antigravity_master_history.log (all-time history)             │
└──────────────────────────────────────────────────────────────────┘
```

### Layer 2 — Unity Game Client (C#)
Five coordinated MonoBehaviour components form the in-game AI pipeline:

```
┌──────────────────────────────────────────────────────────────────┐
│                       UNITY GAME CLIENT                          │
│                                                                  │
│  ┌─────────────────────┐    ┌──────────────────────────────┐    │
│  │ PlayerBehaviorTracker│───▶│   AntigravityGameDirector    │    │
│  │  (collects signals)  │    │   (master orchestrator)      │    │
│  └─────────────────────┘    └──────────────┬───────────────┘    │
│                                             │                    │
│              ┌──────────────────────────────┤                    │
│              │  HTTP POST /evaluate          │                    │
│              ▼                              ▼                    │
│  ┌─────────────────────┐    ┌──────────────────────────────┐    │
│  │  [Remote Brain Server│    │  AdaptiveDifficultyManager   │    │
│  │   or Local Fallback] │    │  (local fallback evaluator)  │    │
│  └──────────┬──────────┘    └──────────────┬───────────────┘    │
│             │ JSON Response                 │                    │
│             └──────────────┬────────────────┘                    │
│                            ▼                                     │
│              ┌─────────────────────────┐                         │
│              │  ApplyProfileToGame()   │                         │
│              └────────────┬────────────┘                         │
│                           │                                      │
│         ┌─────────────────┼──────────────────────┐              │
│         ▼                 ▼                       ▼              │
│  ZombieSpawnManager    ZombieAI[]          AgenticDecisionLogger │
│  (count/interval)  (speed/HP/range)       (log + on-screen UI)  │
└──────────────────────────────────────────────────────────────────┘
```

### Fallback Architecture

If the Python brain server is unreachable (e.g., offline mobile play), the game automatically falls back to the **local `AdaptiveDifficultyManager`**, which runs the same inference logic entirely on-device. No internet required for core gameplay.

```
AntigravityGameDirector
    └── tries POST /evaluate (2s timeout)
            ├── SUCCESS → use server response
            └── FAIL    → fallback to AdaptiveDifficultyManager.Evaluate()
```

---

## 🤖 Agents Developed

### Agent 1: AntigravityGameDirector
**File:** `Assets/scripts/AntigravityGameDirector.cs`

The **master orchestrator agent**. It owns the evaluation loop timer, coordinates all other components, sends data to the remote brain, and applies results to the game world.

- Runs evaluation every `evaluationInterval` seconds (default: 5s)
- Sends `BehaviorSnapshot` to remote server via HTTP POST
- Applies `DifficultyProfile` responses to all game systems
- Detects kiting/camping behavior and triggers ambush spawns
- Records outcomes between evaluation cycles
- Persists server URL via `PlayerPrefs` (survives device restarts)

**Agent Behavior Loop:**
```
Timer fires → GetSnapshot() → POST /evaluate → Parse JSON
    → ApplyProfileToGame() → Log Decision → Wait for next cycle
```

---

### Agent 2: AdaptiveDifficultyManager (Local Fallback Agent)
**File:** `Assets/scripts/AdaptiveDifficultyManager.cs`

The **on-device inference agent**. Mirrors the server logic in C# for offline operation. Uses a multi-signal weighted scoring model to classify the player.

**Signal Score Computation:**

| Score | Formula | Weight |
|---|---|---|
| `killScore` | `min(1, killsPerMin / 3.0)` | 40% of skillScore |
| `resistScore` | `min(1, 1 - dmgPerMin / 40)` | 30% of skillScore |
| `surviveScore` | `min(1, timeSurvived / 300)` | 20% of skillScore |
| `healthScore` | `avgHealthAfterFight` | 10% of skillScore |
| `deathScore` | `min(1, deaths / 3)` | 60% of frustrationScore |
| `damageScore` | `min(1, dmgPerMin / 40)` | 40% of frustrationScore |
| `kitingScore` | `1.0 if isKiting else dist/15` | Standalone |

**Classification Tree:**
```
frustrationScore > 0.7           → Frustrated
skillScore < 0.25 AND frust > 0.5 → Struggling
skillScore < 0.35                → Beginner
kitingScore > 0.7                → Kiter
skillScore > 0.65 AND bored > 0.6 → Bored
skillScore > 0.65                → Skilled
else                             → Balanced
```

---

### Agent 3: PlayerBehaviorTracker (Sensor Agent)
**File:** `Assets/scripts/PlayerBehaviorTracker.cs`

The **data collection agent**. Passively monitors gameplay and computes derived metrics. It is the only source of ground truth for player performance.

**Signals Collected:**

| Signal | Collection Method | Update Frequency |
|---|---|---|
| `killsPerMinute` | Event callback on zombie death | Per kill |
| `damagePerMinute` | Event callback on player damage | Per hit |
| `avgHealthAfterFight` | Sampled after each eval cycle | Per cycle |
| `accuracy` | Attack hit/miss counters | Per attack |
| `deathCount` | Event on player death | Per death |
| `timeSurvived` | `Time.time - sessionStartTime` | Every frame |
| `isKiting` | `avgDistanceFromZombies > 10f` | Every 2 seconds |
| `avgDistFromZombies` | Rolling lerp average, scans all ZombieAI | Every 2 seconds |

---

### Agent 4: AgenticDecisionLogger (Audit Agent)
**File:** `Assets/scripts/AgenticDecisionLogger.cs`

The **transparency and audit agent**. Records every AI decision with full trace and makes it visible in-game. Designed specifically for hackathon judges to inspect AI behavior live.

**Log Entry Format:**
```
[HH:MM:SS]
  OBSERVATION: Kills/min=2.1 Dmg/min=15 AvgHealth=78% Deaths=0 Survived=45s Kiting=False
  INFERENCE:   Skilled
  DECISION:    Increase challenge — skilled player.
  ACTION:      HP×1.10, MaxAlive+1=7
  OUTCOME:     Health +5%, 3 kills since last eval, current HP=83%
```

- Writes to `Application.persistentDataPath/antigravity_log.txt`
- Shows scrollable Brain Log panel in-game UI (toggle via "Brain Logs" button)
- Records QC rejections in yellow with reason
- Tracks outcome of previous decision on the next evaluation cycle

---

### Agent 5: Antigravity Brain Server (Remote Reasoning Agent)
**File:** `antigravity_brain_server.py`

The **remote AI inference agent**. A pure Python HTTP server, zero dependencies beyond stdlib. Stateless per evaluation (no memory between requests by design — metrics are re-sent each cycle).

**Endpoints:**

| Endpoint | Method | Purpose |
|---|---|---|
| `/session_start` | POST | Clear session buffer, mark session start time |
| `/evaluate` | POST | Receive snapshot + current profile → return new profile + label |
| `/log` | POST | Receive outcome log from game → append to session buffer |
| `/session_end` | POST | Flush session to disk with reason (Player Died / Game Exited) |

---

## 📡 APIs Used

### Real APIs

| API | Type | Usage |
|---|---|---|
| **Unity `UnityWebRequest`** | Unity Runtime API | HTTP client for all game → server communication |
| **Python `http.server.HTTPServer`** | Python stdlib | HTTP server (zero external dependencies) |
| **Unity `NavMeshAgent`** | Unity AI/Navigation API | Zombie pathfinding and movement |
| **Unity `Input System` (New)** | Unity Package | Player movement, combat, UI interaction |
| **Unity `JsonUtility`** | Unity Runtime API | JSON serialization of `BehaviorSnapshot` and `DifficultyProfile` |

### No Mock APIs
All communication is real HTTP over LAN/localhost. The server is a real running process. No mocking or simulation is used in the evaluation pipeline.

---

## 🔌 Integration Details

### Unity ↔ Python Server Integration

**Request payload** (Unity → Server, `POST /evaluate`):
```json
{
  "snapshot": {
    "killsPerMinute": 2.1,
    "damagePerMinute": 15.3,
    "avgHealthAfterFight": 0.78,
    "accuracy": 0.62,
    "deathCount": 0,
    "timeSurvived": 45.2,
    "isKiting": false,
    "avgDistFromZombies": 6.4
  },
  "currentProfile": {
    "zombieSpeedMultiplier": 1.0,
    "zombieHealthMultiplier": 1.0,
    "zombieDamageMultiplier": 1.0,
    "zombieDetectionRange": 12.0,
    "zombieChaseRange": 18.0,
    "zombieAttackCooldown": 1.5,
    "maxAliveZombies": 6,
    "spawnInterval": 15.0,
    "specialZombieChance": 0.1,
    "foodDropChance": 0.15
  }
}
```

**Response payload** (Server → Unity):
```json
{
  "profile": {
    "zombieSpeedMultiplier": 1.0,
    "zombieHealthMultiplier": 1.1,
    "zombieDamageMultiplier": 1.0,
    "zombieDetectionRange": 12.0,
    "zombieChaseRange": 18.0,
    "zombieAttackCooldown": 1.5,
    "maxAliveZombies": 7,
    "spawnInterval": 15.0,
    "specialZombieChance": 0.1,
    "foodDropChance": 0.15
  },
  "label": "Skilled",
  "observation": "Kills/Min=2.1 Dmg/Min=15 Health=78% Deaths=0",
  "decision": "Player exhibiting high efficiency. Skilled state inferred.",
  "action": "Increasing challenge parameters: HP=1.10, MaxAlive=7",
  "rejected": false,
  "rejectReason": ""
}
```

### Profile Application Integration

Once a new `DifficultyProfile` is received, `AntigravityGameDirector.ApplyProfileToGame()` broadcasts it to every game system:

```
ApplyProfileToGame(profile)
├── ZombieSpawnManager.ApplyDifficultySettings(maxAlive, spawnInterval)
├── ZombieSpawnManager.ApplyDifficultyToActiveZombies(speed, damage, health)
└── foreach ZombieAI in scene:
        z.detectionRange  = profile.zombieDetectionRange
        z.chaseRange      = profile.zombieChaseRange
        z.attackCooldown  = profile.zombieAttackCooldown
```

### Mobile Integration

For Android builds, the `DevConsole` allows setting the server IP at runtime:
```
/server http://192.168.1.X:8080
```
This updates `PlayerPrefs["AntigravityServerUrl"]` and all subsequent evaluations target that IP, enabling the mobile device to query a PC running the brain server over Wi-Fi.

---

## 📊 Difficulty Profile System

The `DifficultyProfile` is a serializable data class with 10 parameters:

| Parameter | Min | Max | Default | Effect |
|---|---|---|---|---|
| `zombieSpeedMultiplier` | 0.5× | 1.8× (QC cap) | 1.0× | Movement speed of all zombies |
| `zombieHealthMultiplier` | 0.5× | 2.5× | 1.0× | Hit points of all zombies |
| `zombieDamageMultiplier` | 0.5× | 2.0× (QC cap) | 1.0× | Damage dealt per hit |
| `zombieDetectionRange` | 6 units | 22 units | 12 units | How far zombie "sees" player |
| `zombieChaseRange` | 10 units | 30 units | 18 units | How far zombie pursues |
| `zombieAttackCooldown` | 0.5s | 4.0s | 1.5s | Time between zombie attacks |
| `maxAliveZombies` | 1 | 14 (QC cap) | 6 | Max simultaneous zombies |
| `spawnInterval` | 5s | 60s | 15s | Time between spawn waves |
| `specialZombieChance` | 0% | 50% | 10% | Chance of fast/special zombie |
| `foodDropChance` | 0% | 50% | 15% | Chance zombie drops healing |

### Preset Profiles

| Preset | Speed | HP | Damage | MaxAlive | Interval |
|---|---|---|---|---|---|
| **Easy** | 0.7× | 0.7× | 0.7× | 3 | 20s |
| **Normal** | 1.0× | 1.0× | 1.0× | 6 | 15s |
| **Hard** | 1.4× | 1.5× | 1.3× | 10 | 8s |
| **Bored** | 1.6× | 1.8× | 1.2× | 12 | 6s |

---

## 🔄 Data Flow & Decision Loop

```
                    ┌──────────────────────────────────┐
                    │         GAME SESSION              │
                    └────────────────┬─────────────────┘
                                     │ POST /session_start
                                     ▼
┌─────────────────────────────────────────────────────────────────┐
│   Every 5 seconds (evaluationInterval)                          │
│                                                                  │
│   1. OBSERVE                                                     │
│      PlayerBehaviorTracker.GetSnapshot()                         │
│      → BehaviorSnapshot { kills, damage, health, deaths, ... }  │
│                                                                  │
│   2. SEND TO BRAIN                                               │
│      POST /evaluate { snapshot + currentProfile }               │
│          ├── Server responds: new DifficultyProfile + label      │
│          └── Timeout/Error: local AdaptiveDifficultyManager      │
│                                                                  │
│   3. QC VALIDATION (on server AND local)                         │
│      • MaxAlive ≤ 14                                             │
│      • DamageMultiplier ≤ 2.0                                    │
│      • SpeedMultiplier ≤ 1.8                                     │
│      • HP < 30% → block all difficulty increases                 │
│                                                                  │
│   4. ACT                                                         │
│      ApplyProfileToGame(newProfile)                              │
│      → ZombieSpawnManager, ZombieAI[], BossSummonManager         │
│                                                                  │
│   5. LOG                                                         │
│      AgenticDecisionLogger.LogDecision(obs, label, dec, action)  │
│      → on-screen UI panel + antigravity_log.txt                  │
│                                                                  │
│   6. RECORD OUTCOME (next cycle)                                 │
│      Compare current HP / kills to last eval values              │
│      POST /log { outcome } → appended to server session log      │
└─────────────────────────────────────────────────────────────────┘
                                     │ POST /session_end
                                     ▼
                    ┌──────────────────────────────────┐
                    │  antigravity_master_history.log  │
                    │  (full session history on disk)  │
                    └──────────────────────────────────┘
```

---

## 📁 Key Scripts Reference

### Game AI Core

| Script | Role |
|---|---|
| `AntigravityGameDirector.cs` | Master orchestrator — owns the eval loop, HTTP client, profile application |
| `AdaptiveDifficultyManager.cs` | Local AI fallback — weighted signal scoring, player state classification |
| `PlayerBehaviorTracker.cs` | Sensor layer — collects and computes 8 behavioral signals |
| `AgenticDecisionLogger.cs` | Audit layer — logs every decision to file and in-game UI |
| `DifficultyProfile.cs` | Data model — 10-parameter difficulty profile with preset factories |
| `antigravity_brain_server.py` | Remote brain — Python HTTP server with inference, QC, and session logging |

### Game Systems

| Script | Role |
|---|---|
| `ZombieSpawnManager.cs` | Wave-based zombie spawning, ambush spawns, difficulty application |
| `ZombieAI.cs` | Enemy state machine (Idle → Chase → Attack) with NavMesh pathfinding |
| `ZombieHealth.cs` | Zombie HP, damage multiplier application, death/skull drop events |
| `PlayerMovement.cs` | Third-person controller with New Input System |
| `PlayerCombat.cs` | Melee attack system with hit detection and tracker integration |
| `PlayerHealth.cs` | Player HP, damage events, death broadcasting via C# Action event |
| `BossSummonManager.cs` | Boss encounter triggered by ritual/skull milestones |
| `BossArena.cs` | Boss arena lockdown and phase management |
| `GameUIManager.cs` | Full HUD — health bars, skull counter, XP bar, pause, game over |
| `GameModeManager.cs` | Mode switching between Antigravity AI and FixedRule modes |
| `DevConsole.cs` | In-game developer console with `/server` command for mobile IP config |
| `SkullCounter.cs` | Skull collection tracking and milestone triggering |
| `XPManager.cs` | XP and level progression system |
| `MobileControls.cs` | Virtual joystick and touch button overlay for Android builds |

### Editor Tools

| Script | Role |
|---|---|
| `AndroidAPKBuilder.cs` | One-click Android APK build from Unity Editor menu |
| `AutoRigAnimators.cs` | Batch applies animator controllers to humanoid characters |
| `AutoSetupBossAnimator.cs` | Auto-configures boss animator state machine |
| `SceneSetupWizard.cs` | One-click scene hierarchy setup for all game systems |
| `AutoSceneRebuilder.cs` | Rebuilds core GameManager objects if deleted/missing |

---

## 🚀 How to Run

### Prerequisites
- **Unity 2022.3+ LTS** (or newer)
- **Python 3.8+**
- **Android SDK** (only for APK builds)

### Step 1 — Start the Brain Server
```bash
# From the project root
python antigravity_brain_server.py
```

The server will print:
```
==================================================
   ANTIGRAVITY GAME BRAIN SERVER RUNNING
==================================================
 Local Address:   http://localhost:8080
 LAN IP Address:  http://192.168.X.X:8080
==================================================
```

### Step 2 — Run in Unity Editor (PC)
Open the project in Unity, open `Assets/Scenes/SampleScene.unity`, and press **Play**.

The game connects to `http://localhost:8080` automatically.

### Step 3 — Android Build
1. Build APK via **Editor → Antigravity → Build Android APK**
2. Install on phone, connect to same Wi-Fi as PC
3. In-game, open Dev Console and type:
   ```
   /server http://<YOUR_PC_LAN_IP>:8080
   ```

### Reviewing AI Decisions
- **In-game:** Press the **"Brain Logs"** button (top-right HUD)
- **On disk (PC):** `C:\Users\<user>\AppData\LocalLow\<company>\<product>\antigravity_log.txt`
- **Server terminal:** Full decision trace printed in real-time to the Python console
- **History:** `antigravity_master_history.log` in the project root

---

## 🏆 Hackathon Innovation Summary

| Innovation | Detail |
|---|---|
| **Agentic AI Loop** | Full Observe → Infer → Decide → Act → Evaluate cycle, 5s cadence |
| **Dual-Mode Architecture** | Remote server + identical local fallback, zero downtime |
| **Multi-Signal Classification** | 8 signals, 4 composite scores, 6 player states |
| **QC Safety Layer** | Hard caps + health-based veto prevent unfair difficulty spikes |
| **Full Audit Trail** | Every decision logged to file, screen, and remote server |
| **Cross-Platform** | PC editor + Android mobile, LAN server discovery |
| **Session Persistence** | All game sessions archived to `antigravity_master_history.log` |

---

*Built with ❤️ for the Hackathon. All custom code is original.*
