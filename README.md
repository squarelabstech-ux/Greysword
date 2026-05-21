# 🧠 Goohacka — Antigravity AI Brain Game

> **Hackathon Submission** | Unity 3D Zombie Survival with Real-Time Adaptive AI Difficulty Engine

---

## 🎮 What Is This?

**Goohacka** is a third-person zombie survival game built in Unity where the game actively **thinks about you** as you play. The **Antigravity Brain** — a Python-powered AI server — observes your behavior in real-time and dynamically adapts the game's difficulty to keep you in the perfect flow state: not bored, not frustrated.

No two playthroughs feel the same. The AI is always watching.

---

## 🤖 The Antigravity Brain — How It Works

The core innovation is the **Antigravity Brain Server** (`antigravity_brain_server.py`), a lightweight HTTP server that acts as a real-time game AI engine.

### Signal Collection (Unity → Server)
Every 60 seconds of gameplay, Unity sends a behavioral snapshot:

| Metric | What It Measures |
|---|---|
| `killsPerMinute` | Combat efficiency |
| `damagePerMinute` | How much punishment the player is taking |
| `avgHealthAfterFight` | Survivability score |
| `deathCount` | Frustration indicator |
| `timeSurvived` | Session endurance |
| `isKiting` | Whether the player is running away instead of fighting |
| `avgDistFromZombies` | Combat engagement range |

### Player State Classification
The Brain classifies the player into one of **6 states**:

| State | Condition | AI Response |
|---|---|---|
| 🔴 **Frustrated** | High deaths + high damage | Reduce zombie count, damage, add food drops |
| 🟠 **Struggling** | Low skill + high damage | Slow zombies, reduce damage, increase drops |
| 🟡 **Beginner** | Low kill efficiency | Gentle ramp — add 1 zombie |
| 🎯 **Kiter** | Player keeps running away | Extend zombie detection & chase range |
| 🟢 **Skilled** | High efficiency | Increase zombie HP and count |
| ⚡ **Bored** | Skilled + taking no damage | Faster zombies, more zombies, special types |

### Quality Control (QC) Layer
All decisions pass through a safety validator before being applied:
- Hard caps on zombie count (max 14), damage multiplier (max 2.0×), speed (max 1.8×)
- Critical health protection — refuses to increase difficulty if player HP < 30%

---

## 🏗️ Architecture

```
┌─────────────────────────────────────────────────┐
│                  UNITY GAME CLIENT               │
│                                                  │
│  PlayerBehaviorTracker → AntigravityGameDirector │
│         ↓ HTTP POST /evaluate (JSON)             │
│                                                  │
├─────────────────────────────────────────────────┤
│            ANTIGRAVITY BRAIN SERVER              │
│              (antigravity_brain_server.py)       │
│                                                  │
│  Signal Analysis → State Inference → QC Layer   │
│         ↓ JSON Response (new DifficultyProfile)  │
│                                                  │
├─────────────────────────────────────────────────┤
│              GAME SYSTEMS UPDATED                │
│                                                  │
│  ZombieSpawnManager → AdaptiveDifficultyManager  │
│  ZombieAI (speed/HP) → BossSummonManager        │
└─────────────────────────────────────────────────┘
```

---

## 🗂️ Key Scripts

| Script | Purpose |
|---|---|
| `antigravity_brain_server.py` | 🧠 The AI decision engine (Python HTTP server) |
| `AntigravityGameDirector.cs` | Master game orchestrator — calls the Brain |
| `AdaptiveDifficultyManager.cs` | Applies difficulty profile changes in-game |
| `PlayerBehaviorTracker.cs` | Collects and builds behavioral snapshots |
| `AgenticDecisionLogger.cs` | In-game Brain Log UI — shows AI decisions live |
| `ZombieSpawnManager.cs` | Dynamic zombie wave system |
| `ZombieAI.cs` | Enemy pathfinding and attack logic |
| `BossSummonManager.cs` | Boss encounter management |
| `DevConsole.cs` | In-game dev console (set server IP for mobile) |
| `GameUIManager.cs` | Full HUD, health bars, skull counter, game states |
| `MobileControls.cs` | Touch input for Android builds |

---

## 🚀 How to Run

### Prerequisites
- Unity **2022.3+** (LTS recommended)
- Python **3.8+**
- Android SDK (for mobile build)

### Step 1 — Start the Antigravity Brain Server
```bash
# Navigate to the project root
cd path/to/goohacka

# Start the AI server (runs on port 8080)
python antigravity_brain_server.py
```

The server will display your **local network IP**. Keep this running.

### Step 2 — Run the Game (PC)
Open the project in Unity and press **Play**.  
The game automatically connects to `http://localhost:8080`.

### Step 3 — Android / Mobile Build
1. Connect your phone to the **same Wi-Fi** as your PC
2. Build and install the APK via Unity (Android target)
3. Open the **Dev Console** in-game (`~` key or on-screen button)
4. Type: `/server http://<YOUR_PC_IP>:8080`
5. The game will now query your PC's Brain Server in real time

---

## 🎯 Game Features

- ⚔️ **Third-person combat** with melee weapons and sword attacks
- 🧟 **Zombie wave system** with configurable spawn intervals
- 💀 **Skull collection** mechanic — skulls drop from kills
- 🏆 **XP and leveling system**
- 👾 **Boss encounters** — summoned by the Ritual Manager
- 📊 **Live AI Brain Log** panel — watch the AI make decisions in-game
- 📱 **Mobile-ready** with touch controls and Android APK builder
- 🌿 **Stylized low-poly art** (nature, rocks, farm environment packs)

---

## 🧪 Hackathon Innovation Highlights

1. **Real-time behavioral AI** — no scripted difficulty curves, pure adaptive logic
2. **Player state machine** with 6 distinct profiles and weighted signal scoring
3. **QC safety layer** prevents runaway difficulty or unfair situations
4. **Decoupled server architecture** — Brain Server can run on a remote machine or cloud
5. **Session logging** — full decision history persisted to disk for analysis
6. **Mobile + PC cross-play** via LAN server architecture

---

## 📦 Project Structure

```
goohacka/
├── Assets/
│   ├── scripts/          # All C# game scripts
│   ├── Editor/           # Unity Editor automation tools
│   ├── Scenes/           # Game scenes
│   └── ...               # Art assets (models, animations)
├── Packages/             # Unity package manifest
├── ProjectSettings/      # Unity project config
└── antigravity_brain_server.py  # 🧠 AI Brain Server
```

---

## 👥 Team

Built for the Hackathon with ❤️ and a lot of zombie spawning.

---

## 📄 License

This project is submitted as a hackathon entry. All custom code is original. Third-party Unity Asset Store packages are used under their respective licenses.
