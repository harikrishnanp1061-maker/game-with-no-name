# FLIP // FALL

An atmospheric first-person co-operative psychological horror and escape game built in **Unity 6**.

[![Unity 6](https://img.shields.io/badge/Unity-6000.0.5f1-black.svg?style=flat&logo=unity)](https://unity.com/)
[![Render Pipeline](https://img.shields.io/badge/Pipeline-Universal%20RP-blue.svg)](https://unity.com/srp/universal-render-pipeline)
[![Status](https://img.shields.io/badge/Status-Playable%20Tutorial-brightgreen.svg)]()

> 🤖 **Working with an AI Coding Assistant?**  
> Please see [**AI_INSTRUCTIONS.md**](AI_INSTRUCTIONS.md) for detailed architectural rules, coordinate maps, and engine setup instructions tailored for AI agents.

---

## 🎮 Overview

Players awaken trapped in the depths of a brutalist subterranean industrial facility following an elevator crash. Working together as a 4-player team with asymmetric abilities, they must navigate crumbling corridors, cross deceptive pits, evade the predatory **Entity 38 ("Needlelimbs")**, solve light-dependent power puzzles, and restore power to an escape elevator.

### Key Features
- **4-Player Asymmetric Co-Op:** Four specialized player roles. Player 4 operates a high-intensity volumetric torch required to penetrate absolute darkness in Area 7.
- **Predatory Enemy AI:** Entity 38 patrols, hunts, roars, and attacks players with dynamic terror screen distortions (chromatic aberration, lens vignetting, heartbeats).
- **Perception Illusions:** One-way hidden doorways and deceptive two-way pitfalls that challenge spatial awareness.
- **Momentum Physics:** Grounded sprint and airborne momentum preservation tuned for satisfying parkour and reflex dodging.
- **Full PBR & Dynamic Lighting:** Realistic physical lighting, emergency strobes, shattered concrete debris, and fluorescent tube troffers.

---

## 🚀 Quickstart for Developers

### Prerequisites
- **Unity Hub**
- **Unity 6 (version `6000.0.5f1` or later)**
- **Git**

### Installation
1. Clone the repository:
   ```bash
   git clone https://github.com/harikrishnanp1061-maker/game-with-no-name.git
   cd game-with-no-name
   ```
2. Open **Unity Hub**.
3. Click **Add > Add project from disk** and select the inner **`collage game`** directory:
   ```text
   <repo-root>/collage game
   ```
4. Choose **Unity 6000.0.5f1** to launch the project.
5. In the Unity Project window, open:
   ```text
   Assets/Scenes/TutorialScene.unity
   ```
6. Press the **Play** button in the Editor to start the tutorial.

---

## 🕹️ Controls

| Action | Keyboard / Mouse |
| :--- | :--- |
| **Move** | `W` / `A` / `S` / `D` |
| **Look** | Mouse |
| **Sprint** | Left `Shift` |
| **Jump** | `Space` |
| **Crouch** | Left `Ctrl` / `C` |
| **Interact / Pick Up** | `E` |
| **Toggle Torch (Player 4)** | `F` |
| **Switch Player (Dev Mode)** | `1` / `2` / `3` / `4` |

---

## 📁 Repository Structure

```text
├── collage game/               # Unity 6 Project root
│   ├── Assets/                 # Game assets, scripts, scenes, materials, audio
│   │   ├── Characters/         # Player models and Entity 38 rigs
│   │   ├── Materials/          # PBR Master materials & custom shaders
│   │   ├── Meshes/             # Serialized level geometry meshes
│   │   ├── Scenes/             # TutorialScene.unity (Primary Scene)
│   │   ├── Scripts/            # C# Gameplay, AI, and Player controllers
│   │   └── Textures/           # High-resolution 2K PBR textures
│   └── Packages/               # Package manifest (ProBuilder, URP, Input System)
├── 3d assts by user/           # Original 3D source models
├── naration voice/             # Raw narration voiceover audio files
├── AI_INSTRUCTIONS.md          # Setup & architectural guide for AI assistants
├── PROJECT_LOGS.md             # Complete 28-entry chronological evolution logs
├── HANDOFF.md                  # Development handoff & state overview
└── .gitignore                  # Optimized Unity ignore configuration
```

---

## 📜 Development Documentation
- Detailed implementation logs and bug resolution history can be found in [**PROJECT_LOGS.md**](PROJECT_LOGS.md).
- AI agent workflow details are in [**AI_INSTRUCTIONS.md**](AI_INSTRUCTIONS.md).
