# HANDOFF — FLIP//FALL Tutorial Scene & Gameplay AI Architecture

**Target Engine:** Unity 6000.6.5f1 (Universal Render Pipeline)  
**Project Root:** `c:\Users\Harikrishnan\Documents\trash programs\collage game\collage game`  
**Active Saved Scene:** `Assets/Scenes/TutorialScene.unity` (0 compile errors, fully rebuilt, playtested, and saved)  
**Bridge / MCP Client:** `unity_mcp_client.py` & `run_csharp.py` (Local HTTP JSON-RPC endpoint at `http://127.0.0.1:8080/mcp`)  
**Project Log History:** `PROJECT_LOGS.md` (30 complete chronological architectural entries)  
**Last Updated:** October 21, 2026 (Post-compaction master update)

---

## 1. Executive Summary & Current State

All requested gameplay features, AI systems, animations, and bug fixes are **100% implemented, compiled without errors, and verified in Play Mode**:
1. **Monster 38 AI (Area 5 Boss Arena):**
   - Standing detection range increased to $30.0\,\text{m}$ (room corners are $\sim 21.5\,\text{m}$ from the creature). Standing players can no longer hug perimeter walls undetected.
   - Added crouch detection range ($14.5\,\text{m}$) allowing players to sneak along outer walls ($16.2\,\text{m} - 21.5\,\text{m}$) and behind the 4 support pillars.
   - Implemented dynamic closest-player retargeting: continuously tracks all players and switches pursuit to the closest player with $0.5\,\text{m}$ hysteresis.
   - Added opportunistic melee strikes: immediately executes a claw attack if any valid player enters reach ($d \le 2.7\,\text{m}$), even if previously chasing someone else.
   - Fixed north wall spawn boundary bug ($X \ge 97.5\,\text{f}, Z \ge 23.2\,\text{f}$) so players along the northern wall of Room 5 are no longer falsely flagged as outside the arena.
   - **NavMesh Corner & Bend Wall Pathfinding:** Implemented automatic runtime NavMesh generation (`EnsureLevelNavMesh()`) and multi-corner pathfinding (`NavMesh.CalculatePath()`). When a player stands behind a bend wall or pillar, Monster 38 steers towards clearance waypoints around the obstacle rather than running straight into the wall and getting stuck.
2. **Pit Monster (`Monster8_PitStalker` in Area 2 Pit Corridor):**
   - Actively rotates to face the player that falls into the pit corridor across all states: initial stare lock-on, forward jumpscare lunge, peak hold frame, reverse return, and continued idle breathing.
   - Uses `UpdateFacingFallenPlayer()` inside `LateUpdate()` (evaluating post-animator) with dynamic angular speed (`Mathf.Max(lockOnRotationSpeed * 15f, angle * lockOnRotationSpeed)` via `Quaternion.RotateTowards`), eliminating any lag, angle stall, or animator override.
   - Detection bounds cover the entire pit floor ($X \in [12, 68], Z \in [42, 58], Y < -1.5\,\text{m}$).
   - Movement and mouse look remain active inside the pit floor per user requirement (`CanMove = true`, `CanLook = true`).
   - **Pit Fall Entrance SFX (`chiiri-monster-15-337349.mp3`):** Boosted volume by $+5.1\,\text{dB}$ and added dedicated `_pitFallAudioSource` on `PitMonsterJumpscare.cs` (`volume = 1.0f`, `spatialBlend = 0.35f`). Plays immediately when a player drops into the pit chamber as Monster 8 stares down at them. Attack screech remains excluded (pit monster uses deep roar, lurking breathing, and chiiri pit entrance voice).
3. **Player 4 Torch Full Screen/Pitch Tracking:**
   - Handheld torch is parented to active pitching camera (`Camera.main.transform`).
   - The light beam accurately follows both vertical pitch (looking up/down at ceiling and floor) and horizontal yaw.
4. **Developer Teammate Teleportation Feature:**
   - In `MultiPlayerCharacterManager.cs`: `TeleportOtherPlayersToActive()` teleports all inactive player bodies to the active player's position in an organic semi-circle behind/beside the player (`-forward * 1.5m ± right * 1.2m`).
   - Available via `[ ⚡ TELEPORT TEAMMATES HERE  [T] ]` button in the dev panel (`DevPlayerSwitchingPanel.cs`) and quick hotkey `T`.
5. **4-Player Crash Wake-Up Sequence:**
   - Blackout delay removed (`_initialBlackDelay = 0.0f`, start alpha $0.35$). Visuals render from frame 1.
   - Interactive key skip added (`Space`, `WASD`, `C`, or Click).
   - Real mocap animation remapped in `SK_Web_Player_Animated.fbx.meta` to `CH_Player_Armature|Anim_Mocap_Situp_To_Idle` with default `Unconscious` state on un-woken teammates.
   - Organic asymmetrical crash dispersion around the elevator cab in Player 1's forward FOV ($75^\circ$).
6. **Locomotion Animations & Dynamic Shadow Casting:**
   - **Root Cause of Gliding Shadow:** `Player_LocomotionController.controller` states were empty (`Motion: NULL`), and Blender 4.5's FBX exporter had exported static takes due to unassigned `ActionSlot`s in `bake_anim_use_all_actions`.
   - **Full Animation Restoration:** Exported all 14 actions from Blender via dedicated NLA tracks into `Assets/Art/Characters/SK_Web_Player_Animated.fbx`:
     - `Anim_Idle` (299f natural breathing loop)
     - `Anim_Jog` (26f forward stride with 35.5° leg swing)
     - `Anim_Run` (20f sprint cycle)
     - `Anim_Jog_Torch` & `Anim_Run_Torch` (forward-stabilized flashlight grip)
     - `Anim_Crouch_Idle` & `Anim_Crouch_Walk` (in-place crouch locomotion)
     - `Anim_Jump_Improved` (47f responsive jump launch)
     - `Anim_WakeUp_Stand` (133f mocap recovery)
   - **Controller Wiring:** Updated `SetupPlayerLocomotionTransitions.cs` to bind all motions and transitions (`Idle <-> Jog <-> Run`, Torch states, Crouch states, and sprint stops).
   - **Shadows & Culling:** Enforced `characterAnimator.cullingMode = AnimatorCullingMode.AlwaysAnimate` in `FirstPersonController.cs` and `PlayerCharacter.cs`. Active player mesh renderers use `ShadowCastingMode.ShadowsOnly`, keeping first-person view clear while casting full, fluidly animated walking and sprinting shadows on floors and walls under Player 4's flashlight beam.
7. **Hero Props & Architecture:**
   - Strict 4 Master Materials rule (`Concrete042C`, `Concrete044C`, `PavingStones115C`, `Metal062C`).
   - Dedicated PBR elevator cab textures propped by concrete support slabs.
   - Exit Elevator hero prop in Area 8 with procedural PBR materials and 3 fuse sockets.
   - Green Fuse Cell hero prop in Area 7 on the central workbench at `(36.0, 0.928, 11.0)`.
8. **Secondary Picture-in-Picture (PiP) Preview Window (`TeammatePiPPreview.cs`):**
   - Dedicated real-time secondary camera rendering to a $640 \times 360$ RenderTexture.
   - Allows viewing from the perspective of any inactive teammate (Player 1 to Player 4, auto-excluding controlled player) or from an over-the-shoulder 3rd-Person Chase Cam or Room Surveillance Cam.
   - Includes active player tracking mode (`_trackActivePlayer = true`), keeping the active player framed and centered as they walk, run, crouch, or jump around the room.
   - Seamless URP RenderPipeline hook temporarily sets all player meshes to `ShadowCastingMode.On` during preview camera rendering, so character bodies and mocap animations are 100% visible in the preview window with zero clipping in first person.
   - Hotkeys: `F6` (Toggle PiP window), `F7` (Cycle View), `F8` (Toggle Tracking), plus draggable HUD window and integration with `DevPlayerSwitchingPanel.cs`.
9. **Area 4 Hidden Doorway False Wall (`DoorCover_Area4_HiddenDoor`):**
   - Located inside the industrial steel portal frame in Area 4 (`04_HiddenDoorSection`), directly following the Area 2/3 Hidden Pit corridor.
   - Seals the $3.2\text{m} \times 3.6\text{m}$ opening at $(98.95, 1.80, 50.00)$ with seamless `PBR_Master_Wall` concrete texture.
   - **Asymmetric Co-op Perception:** Attached `PerceptionCover` (`HiddenFromTarget`, target: `Player3`, `syncColliders = true`). Player 3 (Scout) sees the passage open and can walk straight through; Players 1, 2, and 4 perceive a solid concrete dead end.
   - **Interactive Secret Wall:** Implemented `IInteractable` on `HiddenDoor.cs`. When any player approaches, displays `[E] PUSH SECRET WALL`. Pressing `[E]` triggers a smooth vertical slide animation downwards by $3.8\text{m}$ into the floor slab, permanently revealing the passage for the entire team and firing the `"door"` beat in `TutorialGameTracker`.

---

## 2. Key File & Systems Reference

| System / Feature | Key Script(s) | Key Methods / Properties |
|---|---|---|
| **Area 4 Hidden Wall** | [`Assets/Scripts/Gameplay/HiddenDoor.cs`](file:///c:/Users/Harikrishnan/Documents/trash%20programs/collage%20game/collage%20game/Assets/Scripts/Gameplay/HiddenDoor.cs)<br>[`Assets/Scripts/Gameplay/PerceptionCover.cs`](file:///c:/Users/Harikrishnan/Documents/trash%20programs/collage%20game/collage%20game/Assets/Scripts/Gameplay/PerceptionCover.cs) | `DoorCover_Area4_HiddenDoor`, `PerceptionRule.HiddenFromTarget`, `PlayerIdentity.Player3`, `OpenMode.Slide`, `[E] PUSH SECRET WALL`, `OnOpened` |
| **Secondary PiP Preview** | [`Assets/Scripts/Gameplay/TeammatePiPPreview.cs`](file:///c:/Users/Harikrishnan/Documents/trash%20programs/collage%20game/collage%20game/Assets/Scripts/Gameplay/TeammatePiPPreview.cs) | `ShowPiP` (`F6`), `CycleNextView()` (`F7`), `TrackActivePlayer` (`F8`), `UpdateTeammateCamera()`, URP camera render mesh hook |
| **Monster 38 AI & Pathfinding** | [`Assets/Scripts/Entity38EnemyAI.cs`](file:///c:/Users/Harikrishnan/Documents/trash%20programs/collage%20game/collage%20game/Assets/Scripts/Entity38EnemyAI.cs) | `agitationRange` ($30\text{m}$), `crouchAgitationRange` ($14.5\text{m}$), `HasClearDirectPath()`, `GetNavMeshMoveDirection()`, `EnsureLevelNavMesh()`, dynamic retargeting & melee |
| **Pit Monster** | [`Assets/Scripts/Gameplay/PitMonsterJumpscare.cs`](file:///c:/Users/Harikrishnan/Documents/trash%20programs/collage%20game/collage%20game/Assets/Scripts/Gameplay/PitMonsterJumpscare.cs) | `IsInsidePitChamber()`, `FindFallenPlayerInPit()`, `UpdateFacingFallenPlayer()`, `LateUpdate()`, `TriggerPitFall()`, `ResetToPitHome()` |
| **Player 4 Torch Light** | [`Assets/Scripts/Gameplay/PlayerTorchAbility.cs`](file:///c:/Users/Harikrishnan/Documents/trash%20programs/collage%20game/collage%20game/Assets/Scripts/Gameplay/PlayerTorchAbility.cs) | Full vertical pitch tracking parented to `Camera.main`, shared 3D beam illumination, stand-in proxy pose sync |
| **Teammate Dev Teleport** | [`Assets/Scripts/Gameplay/MultiPlayerCharacterManager.cs`](file:///c:/Users/Harikrishnan/Documents/trash%20programs/collage%20game/collage%20game/Assets/Scripts/Gameplay/MultiPlayerCharacterManager.cs) | `TeleportOtherPlayersToActive()`, non-overlapping semi-circle offset, bound to `T` hotkey & dev GUI button |
| **Dev Switching Panel** | [`Assets/Scripts/Gameplay/DevPlayerSwitchingPanel.cs`](file:///c:/Users/Harikrishnan/Documents/trash%20programs/collage%20game/collage%20game/Assets/Scripts/Gameplay/DevPlayerSwitchingPanel.cs) | In-game IMGUI panel for F1–F4 identity switching, mouse release on `Tab`, teleport button, PiP monitor buttons |
| **Pit Suffocation & Respawn** | [`Assets/Scripts/Gameplay/PlayerSuffocationPitEffect.cs`](file:///c:/Users/Harikrishnan/Documents/trash%20programs/collage%20game/collage%20game/Assets/Scripts/Gameplay/PlayerSuffocationPitEffect.cs) | `SuffocationRoutine()`, `PrePitSafePosition`, `_pitFallYThreshold` ($-1.5\text{m}$), full movement & look retention |
| **Terror Post-Processing** | [`Assets/Scripts/Gameplay/MonsterTerrorScreenEffect.cs`](file:///c:/Users/Harikrishnan/Documents/trash%20programs/collage%20game/collage%20game/Assets/Scripts/Gameplay/MonsterTerrorScreenEffect.cs) | `SetPitTerror()`, `ResetTerrorImmediately()`, `OnActivePlayerSwitched()`, max vignette $0.32$, `maxTerrorRange` $28\text{m}$ |
| **Player Controller** | [`Assets/Scripts/FirstPersonController.cs`](file:///c:/Users/Harikrishnan/Documents/trash%20programs/collage%20game/collage%20game/Assets/Scripts/FirstPersonController.cs) | `CanMove`, `CanLook`, `isCrouching`, `toggleCrouchWithC`, `lastSafePosition`, `hasLastSafe`, `VerticalRotation` |
| **Multi-Player Manager** | [`Assets/Scripts/Gameplay/MultiPlayerCharacterManager.cs`](file:///c:/Users/Harikrishnan/Documents/trash%20programs/collage%20game/collage%20game/Assets/Scripts/Gameplay/MultiPlayerCharacterManager.cs) | `ActivePlayer`, `AllPlayers`, `SwitchToPlayer()`, `TriggerAllWakeUp()`, auto-resolving singleton getters |
| **Wake-Up Cinematic** | [`Assets/Scripts/Gameplay/PlayerWakeUpSequence.cs`](file:///c:/Users/Harikrishnan/Documents/trash%20programs/collage%20game/collage%20game/Assets/Scripts/Gameplay/PlayerWakeUpSequence.cs) | `WakeUpRoutine()`, `FinishWakeUpImmediately()`, `_initialBlackDelay = 0.0f` |
| **Locomotion Transitions** | [`Assets/Editor/SetupPlayerLocomotionTransitions.cs`](file:///c:/Users/Harikrishnan/Documents/trash%20programs/collage%20game/collage%20game/Assets/Editor/SetupPlayerLocomotionTransitions.cs) | Sets up `Unconscious` default state and all bidirectional crouch / jog / run transitions |
| **Exit Elevator Setup** | [`Assets/Editor/ExitElevatorMaterialSetup.cs`](file:///c:/Users/Harikrishnan/Documents/trash%20programs/collage%20game/collage%20game/Assets/Editor/ExitElevatorMaterialSetup.cs) | Generates procedural PBR textures for Area 8 exit elevator matching `Elevator.blend` |
| **Level Builder** | [`Assets/Editor/FlipFallLevelBuilder.cs`](file:///c:/Users/Harikrishnan/Documents/trash%20programs/collage%20game/collage%20game/Assets/Editor/FlipFallLevelBuilder.cs) | Complete procedural scene generation pipeline (`Tools > FLIP//FALL > Rebuild Tutorial Map`) |

---

## 3. Detailed Specifications & Implementation Notes

### A. Monster 38 Detection & AI Math
- **Room 5 Bounds:** Center monster at $(88.0, 0.05, 11.0)$. Arena dimensions: $X \in [71.25, 105.25]$, $Z \in [-1.75, 23.75]$ ($34\text{m} \times 25.5\text{m}$).
- **Standing Detection ($30.0\text{m}$):** Max distance from center to room corners is $\approx 21.5\text{m}$. With $30.0\text{m}$ range, a standing player is detected anywhere in Room 5.
- **Crouch Detection ($14.5\text{m}$):** Walking along the perimeter walls puts the player at distances of $16.2\text{m}$ (north/south) to $21.5\text{m}$ (corners). When crouching, `crouchAgitationRange = 14.5\text{m}` shields them from agitation, allowing stealth navigation behind pillars and walls.
- **North Corridor Filter:** Spawn entrance to Room 4 is located at $X \in [99.5, 104.5], Z \approx 23.15$. The spawn territory filter [`IsInSpawnPassage()`](file:///c:/Users/Harikrishnan/Documents/trash%20programs/collage%20game/collage%20game/Assets/Scripts/Entity38EnemyAI.cs#L453) strictly requires $X \ge 97.5\text{f}$ AND $Z \ge 23.2\text{f}$, preventing players walking along the north wall inside Room 5 from being ignored.
- **Opportunistic Melee:** In [`FindPlayerInMeleeRange()`](file:///c:/Users/Harikrishnan/Documents/trash%20programs/collage%20game/collage%20game/Assets/Scripts/Entity38EnemyAI.cs#L462), any player within $d \le \text{attackRange} + 0.5\text{m} = 2.7\text{m}$ immediately triggers `ExecuteAttackRoutine()` and becomes the primary target.

### B. Pit Monster (`Monster8_PitStalker`) Active Facing
- **Pit Corridor Geometry:** Pit floor spans $X \in [12, 68]$, $Z \in [42, 58]$ at $Y \approx -10.0\text{m}$.
- **Facing Mechanism:**
  ```csharp
  // Called in LateUpdate() to execute after animator evaluation:
  Vector3 targetDir = (_targetPlayer.position - transform.position);
  targetDir.y = 0;
  if (targetDir.sqrMagnitude > 0.001f)
  {
      Quaternion lookRot = Quaternion.LookRotation(targetDir.normalized);
      float angle = Quaternion.Angle(transform.rotation, lookRot);
      float turnSpeed = Mathf.Max(lockOnRotationSpeed * 15f, angle * lockOnRotationSpeed);
      transform.rotation = Quaternion.RotateTowards(transform.rotation, lookRot, turnSpeed * Time.deltaTime);
  }
  ```
- **Orientation Proof:** Initial rotation $(0, 270, 0)$ corresponds to world forward $(-1, 0, 0)$ (west, facing approaching players). Target West $\rightarrow 270^\circ$, Target North $\rightarrow 0^\circ$, Target East $\rightarrow 90^\circ$, Target South $\rightarrow 180^\circ$.
- **Animator Config:** `applyRootMotion = false` allows script code to authoritatively control root rotation without animator conflict.

### C. Locomotion & Crouch Controls
- **Input System Actions:** Both `<Keyboard>/c`, `<Keyboard>/leftCtrl`, `<Keyboard>/rightCtrl`, and gamepad bindings are configured under `Crouch`.
- **Toggle vs. Hold:**
  - `C` toggles crouch on/off.
  - `Ctrl` holds crouch while pressed.
  - Sprint (`Left Shift`) or Jump (`Space`) automatically cancels crouch toggle and returns player to standing locomotion.
- **Camera & Controller Lerp:** Smooth interpolation over $0.18\text{s}$ ensures no jarring camera pops when crouching or standing.

### D. Player 4 Torch Overhaul & Dynamic Surface Reflection Lighting
- **Purple Stand-In Cylinder Eradication:** Early prototypes used proxy cylinders (`Player4_Teammate_StandIn`, `P4_Body`) which accumulated and rendered around Player 4. All 175 legacy instances were permanently purged from the scene, and all proxy generation logic in [`PlayerTorchAbility.cs`](file:///c:/Users/Harikrishnan/Documents/trash%20programs/collage%20game/collage%20game/Assets/Scripts/Gameplay/PlayerTorchAbility.cs) was replaced with physical body mounting.
- **Physical Mounting Architecture:**
  - When Player 4 is controlled: Handheld torch parents to `Camera.main.transform` at `(0.24, -0.22, 0.40)` for mouse pitch/yaw tracking.
  - When switching away to P1–P3: Handheld torch dynamically mounts directly to `Player_4`'s physical humanoid character model (`Player_4/CameraHolder`) at eye level, illuminating the world in 3D without proxy meshes.
- **Wider Spread & Concentrated Core Beam:**
  - Primary Spotlight: $54.0^\circ$ outer angle ($24.0^\circ$ inner), $32.0\,\text{m}$ throw distance, $13.5\,\text{f}$ base intensity.
  - Secondary Core Hotspot: `Torch_CoreHotspot` ($26.0^\circ$ outer, $10.0^\circ$ inner, $18.0\,\text{m}$ range, $9.0\,\text{f}$ intensity) provides a focused high-lumen center beam.
- **Dynamic Close-Up Intensity Scaling:** Real-time forward raycasting modulates intensity inversely with surface distance. Surfaces within $5.5\,\text{m}$ amplify intensity up to $+8.0\,\text{lux}$ (main) and $+12.0\,\text{lux}$ (core), delivering high-intensity punch on close walls/objects while tapering off naturally at distance.
- **Subtle Surface Reflection Bounce:** `Torch_SurfaceBounce` point light dynamically hovers $0.15\,\text{m}$ off hit surfaces along the surface normal. It casts a soft, subtle specular bounce (ranging $0.15\,\text{f}$ to $2.2\,\text{f}$ intensity) simulating realistic surface reflectance without scene washout.

### E. Developer Teammate Teleportation Feature
- **Implementation:** Added [`TeleportOtherPlayersToActive()`](file:///c:/Users/Harikrishnan/Documents/trash%20programs/collage%20game/collage%20game/Assets/Scripts/Gameplay/MultiPlayerCharacterManager.cs#L257) in `MultiPlayerCharacterManager.cs`.
- **Organic Positioning:** Spaces teammates in a neat semi-circle behind/beside the active player (`-forward * 1.5m ± right * 1.2m`) to prevent character overlap or clipping.
- **State Sanitization:** Disables/re-enables `CharacterController`, sets `lastSafePosition` for pit fall safety, aligns yaw to active player, and resets animation to idle.
- **Triggers:** Available via dev GUI button `[ ⚡ TELEPORT TEAMMATES HERE  [T] ]` in [`DevPlayerSwitchingPanel.cs`](file:///c:/Users/Harikrishnan/Documents/trash%20programs/collage%20game/collage%20game/Assets/Scripts/Gameplay/DevPlayerSwitchingPanel.cs) and keyboard hotkey `T`.

### F. Entity 38 Aggravation & Line of Sight System
- **Input Conflict Purge:** Removed all legacy debug keys (`Space`, `1`–`4`, `M`) in [`Entity38EnemyAI.cs`](file:///c:/Users/Harikrishnan/Documents/trash%20programs/collage%20game/collage%20game/Assets/Scripts/Entity38EnemyAI.cs) that previously hijacked player jumps (`Space` toggled `manualControl`, freezing the AI in place) and player switching (`1`–`4` forced animations).
- **Portal Approach Detection:** Updated `IsInSpawnPassage(Vector3 pos)` from $Z > 23.2$ to $Z > 34.0$, enabling the creature to detect approaching players in the doorway/hallway, play its notice screech, and transition from `Lurking` to `Chasing`. Physical creature boundary clamp remains at $Z \le 23.2$ (`EnforceSpawnTerritoryBoundary()`).
- **Domain Reload Resilience:** [`MultiPlayerCharacterManager.ActivePlayer`](file:///c:/Users/Harikrishnan/Documents/trash%20programs/collage%20game/collage%20game/Assets/Scripts/Gameplay/MultiPlayerCharacterManager.cs) auto-resolves the active controlled body if references reset.

### G. Running Screen Sway & Cross-Player Sway Bleed Isolation
- **Subtle Running Screen Sway:** [`FirstPersonController.cs`](file:///c:/Users/Harikrishnan/Documents/trash%20programs/collage%20game/collage%20game/Assets/Scripts/FirstPersonController.cs) features gentle, natural screen sway roll ($\pm 0.95^\circ$) and yaw deflection ($\pm 0.35^\circ$) synchronized to sprint footsteps (`8.5\,\text{Hz}`). Smoothly decays to 0 when walking or stopping.
- **Strict Player Camera Isolation:**
  - De-controlling a character explicitly detaches its camera reference (`playerCamera = null`).
  - Active player switch explicitly zeroes `_mainCamera.transform.localRotation` roll.
  - Pit suffocation and terror sway routines verify active player ownership before applying rotation, guaranteeing 0 cross-player sway bleeding.

### H. Area 4 Hidden Doorway False Wall (`DoorCover_Area4_HiddenDoor`)
- **Location:** Inside the steel portal frame of `04_HiddenDoorSection` at $(98.95, 1.80, 50.00)$ leading to `Passage_To_CreatureRoom`. Size: $(0.40, 3.60, 3.20)$.
- **Material:** `Assets/Materials/PBR_Master_Wall.mat` (`matWall`), flush with surrounding walls with world planar UVs.
- **Asymmetric Perception (`PerceptionCover.cs`):**
  - Assigned to **Player 3 (Scout)** (`PerceptionRule.HiddenFromTarget`, `target = Player3`, `syncColliders = true`).
  - Player 3 sees the passage open and can walk straight through. Players 1, 2, and 4 perceive a solid concrete dead end.
- **Interactive Secret Wall (`HiddenDoor.cs` & `IInteractable`):**
  - Prompt: `[E] PUSH SECRET WALL`. Range: $3.5\text{m}$.
  - On interact: Slides down $3.8\text{m}$ ($Y = 1.80 \rightarrow -2.00$) into the floor slab, permanently opening the passage for all players and completing the `"door"` beat in `TutorialGameTracker`.

---

## 4. Inviolable Core Constraints for Any Next Agent

1. **NEVER replace elevator cab materials with generic metal.** The cab must retain its dedicated procedural multi-part textures (`PBR_Elevator*`).
2. **NEVER overwrite Exit Elevator PBR materials.** The standing exit elevator in Area 8 must strictly use `ExitElevatorMaterialSetup.cs` materials matching `Elevator.blend`.
3. **NEVER move the fuse cell off the Area 7 workbench.** It must remain centered on the workbench table at `(36.0, 0.928, 11.0)`.
4. **NEVER add floating 3D text in the scene.** All navigation cues must use physical architectural frames and lighting.
5. **NEVER disable player movement or look in the pit.** The player retains full `CanMove` and `CanLook` control while down on the pit floor.
6. **NEVER add attack screech audio to the pit monster.** Monster 8 uses deep monstrous roars and lurking breathing; the high-pitch attack screech is exclusive to Entity 38 in Area 5.
7. **Strict Architecture Materials:** All walls must use `PBR_Master_Wall.mat` (`Concrete042C`), room floors `PBR_Master_RoomFloor.mat` (`Concrete044C`), corridors `PBR_Master_PathwayFloor.mat` (`PavingStones115C`), and structural metals `PBR_Master_Metal.mat` (`Metal062C`).
8. **Asymmetric Co-op Role Assignment:**
   - **Player 1:** Discovers secret exit in Area 5 (`DoorCover_CreatureRoomExit`).
   - **Player 2:** Discovers hidden pit trap in Area 2 (`FloorCover_HiddenPit`).
   - **Player 3:** Discovers hidden passage in Area 4 (`DoorCover_Area4_HiddenDoor`).
   - **Player 4:** Possesses tactical torchlight ability (`PlayerTorchAbility.cs`).

---

## 5. How to Automate and Test via Unity MCP

The workspace includes a Python MCP bridge client `unity_mcp_client.py` allowing dynamic C# execution inside Unity.

### A. Quick Health Check (Play Mode & Compilation Status)
```python
import unity_mcp_client

code = """
return $"isCompiling: {UnityEditor.EditorApplication.isCompiling} | isPlaying: {UnityEngine.Application.isPlaying}";
"""
res = unity_mcp_client.call_tool("execute_code", {"action": "execute", "code": code})
print(res["result"]["structuredContent"]["data"]["result"])
```

### B. Trigger Script Compilation & Rebuild Check
```python
import unity_mcp_client

code = """
UnityEditor.AssetDatabase.Refresh(UnityEditor.ImportAssetOptions.ForceUpdate);
UnityEditor.Compilation.CompilationPipeline.RequestScriptCompilation();
return "Compilation requested.";
"""
res = unity_mcp_client.call_tool("execute_code", {"action": "execute", "code": code})
print(res)
```

### C. Verify Monster 38 Range Values at Runtime
```python
import unity_mcp_client

code = """
var ai = GameObject.FindAnyObjectByType<Entity38EnemyAI>();
if (ai == null) return "Entity38EnemyAI not found in scene!";
return $"agitationRange={ai.agitationRange}, crouchAgitationRange={ai.crouchAgitationRange}, chaseRange={ai.chaseRange}, detectionRange={ai.detectionRange}";
"""
res = unity_mcp_client.call_tool("execute_code", {"action": "execute", "code": code})
print(res["result"]["structuredContent"]["data"]["result"])
```

### D. Verify Pit Monster Tracking at Runtime
```python
import unity_mcp_client

code = """
var pmj = GameObject.FindAnyObjectByType<PitMonsterJumpscare>();
if (pmj == null) return "PitMonsterJumpscare not found!";
return $"CurrentState={pmj.CurrentState}, lockOnRotationSpeed={pmj.lockOnRotationSpeed}, MonsterPos={pmj.transform.position}, MonsterRot={pmj.transform.rotation.eulerAngles}";
"""
res = unity_mcp_client.call_tool("execute_code", {"action": "execute", "code": code})
print(res["result"]["structuredContent"]["data"]["result"])
```

---

## 6. Play Mode Controls Cheatsheet for Manual Testing

| Key / Action | In-Game Effect |
|---|---|
| **`W / A / S / D`** | Jog / Move character |
| **`Left Shift`** (Hold) | Sprint ($7.5\text{m/s}$) with heavy running footsteps and stamina depletion |
| **`C`** (Tap) | Toggle Crouch ($2.2\text{m/s}$, eye height $1.15\text{m}$) |
| **`Left Ctrl`** (Hold) | Hold Crouch |
| **`Space`** | Jump / Instantly skip wake-up sequence |
| **`F1` / `F2` / `F3` / `F4`** | Switch active character between Player 1, 2, 3, and 4 |
| **`E`** | Interact with Door Buttons, Fuse Cell Pickup, Elevator Sockets |
| **`F`** | Toggle Flashlight / Torch Ability |
