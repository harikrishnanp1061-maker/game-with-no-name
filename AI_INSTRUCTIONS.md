# FLIP//FALL — AI Assistant & Agent Onboarding Guide

> **Notice for AI Agents (Antigravity, Claude, ChatGPT, Cursor, Copilot, etc.):**  
> Read this document completely before attempting to modify, rebuild, or test this project. It contains essential architectural invariants, scene setup procedures, coordinate references, and pitfalls discovered during production.

---

## 1. Project Specifications & Environment

- **Project Name:** FLIP//FALL (Tutorial / Co-Op Escape Map)
- **Engine Version:** **Unity 6 (6000.0.5f1 / 6000.6.5f1)**
- **Render Pipeline:** Universal Render Pipeline (URP) / Built-in Shader Graph Hybrid
- **Unity Project Root:** `./collage game/` *(Note: The repository root contains docs and user raw assets; the actual Unity project is inside `./collage game/`)*.
- **Primary Scene:** `collage game/Assets/Scenes/TutorialScene.unity`
- **Core Input System:** Unity New Input System (`InputSystem_Actions.inputactions`)

---

## 2. Quickstart & Engine Setup

When setting up this project in Unity:
1. **Open Project in Unity Hub:**
   - Point the Hub to the subdirectory: `<repo-root>/collage game`
   - Use **Unity 6000.0.5f1** or compatible 6000.x.
2. **Initial Package Import:**
   - Allow Unity to restore packages listed in `Packages/manifest.json` (ProBuilder, Input System, TextMeshPro, Universal RP).
3. **Open the Active Scene:**
   - Navigate to `Assets/Scenes/TutorialScene.unity` and open it.
4. **Tag & Layer Integrity:**
   - Verify that `ProjectSettings/TagManager.asset` has the required tags:
     - `Player` (applied to all 4 player characters)
     - `Fuse` (used by fuse switches and elevator power sockets)
     - `FuseCell` (used by fuse cell pickups)
     - `MainCamera`
5. **Procedural Meshes:**
   - All level geometry meshes are serialized in `Assets/Meshes/LevelGeometryMeshes.asset`.
   - **CRITICAL:** Do NOT call `AssetDatabase.DeleteAsset("Assets/Meshes/LevelGeometryMeshes.asset")`. If you need to rebuild or adjust procedural meshes, append to or update sub-assets within this file.

---

## 3. Architecture & Key Game Systems

### A. 4-Player Asymmetric Co-Op System
- **Manager:** `MultiPlayerCharacterManager.cs` orchestrates 4 distinct co-op characters:
  - `Player_1` (Default local / Leader)
  - `Player_2` (Scout)
  - `Player_3` (Engineer)
  - `Player_4` (Support / Torchbearer)
- **Torchbearer Role:** Only `Player_4` possesses the `PlayerTorchAbility.cs`.
  - In Area 7 (The Dark Room), natural illumination is completely zero.
  - Players cannot see or navigate the dark room without `Player_4`'s volumetric torch.
- **FirstPersonController Physics:**
  - Movement includes grounded sprinting and airborne momentum preservation.
  - Jumping while sprinting preserves forward velocity via `airControlAcceleration = 12.0f` and `airDragDeceleration = 1.5f` even if navigation keys (`W/A/S/D`) are released mid-air.

### B. Enemy AI (`Entity38EnemyAI.cs`)
- **Creature Identity:** Entity 38 ("Needlelimbs").
- **Finite State Machine:**
  - `Idle` / `Stalk` (patrolling and listening)
  - `Chase` (navmesh sprint toward player)
  - `Roar` (intimidates and triggers camera shake)
  - `Attack` (melee strike within 2.2m)
- **Horror Screen Effect:** `MonsterTerrorScreenEffect.cs` creates distance-based chromatic aberration, vignetting, and radial distortion when the monster draws near.
- **Pit Monster:** Normal pits 1, 2, 3 have zero monsters. Only the Area 3 Hidden Pit contains `Monster8_PitStalker` (`PitMonsterJumpscare.cs`).

### C. Perception Covers & Optical Illusions (`PerceptionCover.cs`)
- Used for hidden doors and false floors that change transparency based on player position:
  - `DoorCover_Area4_HiddenDoor`: One-way hidden doorway into the secret passage.
  - `DoorCover_CreatureRoomExit`: One-way exit from creature holding area.
  - `FloorCover_HiddenPit`: Standard Two-Way deceptive floor (`OneWay = false`). A solid crossing girder (`HiddenPit_CrossingGirder`, $1.2\,\text{m}$ wide) is suspended at $Y = -0.45\,\text{m}$ to allow cross-walking.

### D. Mission Objectives & Escape Flow
1. **Area 1 (Elevator Shaft Crash):** Starting wake-up cinematic and intro corridor.
2. **Area 3 (Hidden Pit):** Reflex dodge jump or careful girder crossing.
3. **Area 4 (Hidden Door):** Secret passage requiring environmental clue solving.
4. **Area 5 & 6 (Creature Territory):** Stealth and evasion past Entity 38.
5. **Area 7 (Dark Fuse Room):** Collect the unlit fuse cell (`FuseCellPickup.cs`). Must be illuminated by Player 4's torch to activate.
6. **Area 8 (Working Elevator):** Insert the charged fuse cell into `ElevatorPowerSocket.cs`, enter the cabin, and press `ElevatorInteriorCloseButton.cs` to trigger escape.

---

## 4. Crucial Programming Invariants for Future AI Developers

1. **Tag Safety (NEVER throw `UnityException`):**
   - Do NOT use `go.CompareTag("CustomTag")` without verifying the tag exists, as Unity throws fatal exceptions for undefined tags.
   - Always prefer `go.tag == "CustomTag"` or `GameplayUtil.TrySetTag(go, tagName)`.
2. **Unity 6 API Standards:**
   - Use `FindAnyObjectByType<T>()` or `FindFirstObjectByType<T>()` instead of the deprecated `FindObjectOfType<T>()`.
   - Use `FindObjectsByType<T>(FindObjectsSortMode.None)` instead of deprecated `FindObjectsOfType<T>()`.
3. **Asset Serialization:**
   - Avoid creating loose procedural meshes that only exist in scene memory; always serialize procedural assets into `.asset` files so `git clone` recipients do not load missing or pink meshes.
4. **Git & OS Hygiene:**
   - Never create files with reserved Windows names (e.g. `NUL`, `CON`, `PRN`, `AUX`) via redirection (`> NUL`).
   - Keep diagnostic dumps, scratch `.py` scripts, and debug PNG renders in `_archive_scripts/` (which is gitignored).

---

## 5. Automation & Direct Execution

If running an automated AI agent with socket execution:
- The project includes `unity_direct.py` in `_archive_scripts/` (or can connect via TCP port `6401`/`6402`) using an 8-byte big-endian length prefix framing Roslyn C# code execution commands.
- See [PROJECT_LOGS.md](file:///c:/Users/Harikrishnan/Documents/trash%20programs/collage%20game/PROJECT_LOGS.md) for full historical context on every bug fix and architectural decision made in this codebase.

---

## 6. Multi-Laptop AI Collaboration (Antigravity LAN Bridge)

To coordinate between two developers running Antigravity on the same local Wi-Fi:

1. **Start the LAN Bridge Daemon:**
   ```bash
   python antigravity_bridge.py start
   ```
   - Automatically detects local IP (e.g., `http://192.168.1.4:8765`).
   - Serves an interactive dashboard at `http://localhost:8765`.

2. **Send Handoff & Push Updates to Peer:**
   ```bash
   python antigravity_bridge.py push --peer <FRIEND_IP> --msg "Fixed monster prefab and elevator"
   ```
   - Commits all changes, pushes to GitHub `origin/main`, and sends an instant audio/visual notification to the peer laptop.

3. **Check Received Handoffs:**
   - Any agent can inspect `_sync/INCOMING_HANDOFF.md` to see what the other developer's agent built, which files changed, and what tasks need testing.

4. **Pulling Updates:**
   ```bash
   python antigravity_bridge.py pull
   ```

