# Collage Game + HollowTower: compatibility report

Checked live through Unity MCP on 2026-10-22. Nothing in either project was changed.

## Unity MCP

Already set up. Both open editors run the CoplayDev `unity-mcp` package and are connected:

- **collage game** (main): `trash programs/collage game/collage game`
- **HollowTower** (the parallel level): `trash programs/collage game assets/paralel working/HollowTower`

## What's open

**collage game, `Assets/Scenes/TutorialScene.unity`** (15 root objects). A `TutorialScene` environment root, six URP point lights for the pit warnings and depth fill, two pit "victim" dummies, `MultiPlayer_Manager` (character switching, wake-up sequence, suffocation effect, ambience audio, crosshair, interaction system), four players (`Player_1` to `Player_4`) each with a CharacterController and FirstPersonController (Player_4 also has the torch ability), and a teammate picture-in-picture preview. No skybox, flat ambient light, no fog. Build list: SampleScene, TutorialScene.

**HollowTower, `Assets/HollowTowerRemastered/Scenes/HollowTower_Remastered.unity`** (2 root objects). `HollowTower_Remastered_Environment` (7 child groups) and `Player_Character_Controlled` (RemasteredPlayerController, map HUD, light optimizer, floor culling). Cubemap skybox, skybox ambient light.

## Side-by-side

| Setting | collage game | HollowTower | Clash? |
|---|---|---|---|
| Unity version | 6000.6.5f1 | 6000.6.5f1 | No |
| Render pipeline | **URP 17.6** (PC_RPAsset via quality level) | **HDRP 17.6** (HollowTowerHDRP.asset) | **Blocker** |
| Materials | 131 × URP/Lit | 49 × HDRP/Lit, 1 × HDRP 3ds Max physical | **Blocker** |
| Post-processing | URP volume profiles (Horror_VolumeProfile) | HDRP volume (HorrorVolumeProfile) | **Blocker** |
| Input handling | Input System only (new) | Old Input Manager only | **Blocker** |
| Graphics APIs (Win64) | D3D11, D3D12 | D3D11 | Minor |
| Color space | Linear | Linear | No |
| Quality levels | 1 (PC) | 6 (Very Low to Ultra) | Minor |
| Tags | Fuse, FuseCell | (none custom) | No |
| Layers / physics | defaults, gravity −9.81, fixed step 0.02 | same | No |
| glTFast | 6.20.0 | 6.20.0 | No |
| Other packages | Cinemachine, Input System, AI Navigation, ProBuilder, Timeline, VFX Graph, uGUI | HDRP, NVIDIA module | See below |

## Blockers and fixes

1. **URP vs HDRP.** One game can only run one render pipeline. Every HDRP/Lit material in HollowTower will render pink in collage game, and its HDRP volume, sky and light settings won't carry over.
   *Fix (recommended):* keep URP, since the main game and its 131 materials already use it. Move HollowTower's assets in, then convert its materials to URP/Lit (by script or by hand, remapping HDRP mask maps: metallic from R, AO from G, smoothness from A), rebuild its horror look as a URP volume profile, and swap the HDRP cubemap sky for a URP skybox material. The glTF Shader Graph materials keep working on both pipelines because glTFast ships variants for each.

2. **Input System mismatch.** collage game is set to the new Input System only. HollowTower calls the old `Input.GetKeyDown`, `Input.GetAxisRaw` and similar directly (for example `InteractiveDoor.cs:99` and `RemasteredMapHUD.cs:188-212`). Under the new-only setting those calls throw an exception at runtime. The copies already sitting in collage game's `Assets/Levels/HollowTower/Scripts` (InteractiveDoor, RemasteredMapHUD, TowerElevator) have the same problem. collage game's own scripts are fine: they put their old-input fallbacks behind `#if` guards.
   *Fix:* port those calls to `Keyboard.current` and `Mouse.current`, as HollowTower's own `InputWrap.cs` partly does already. The quick stopgap is Player Settings > Active Input Handling > **Both**.

3. **Duplicate script names.** collage game already has partial copies of HollowTower code under `Assets/Levels/HollowTower` (namespace `HollowTower`): InteractiveDoor, LevelLayout (with FloorPlan, Room, RoomType, StairSpec), RemasteredLightOptimizer, RemasteredMapHUD, TowerElevator, TowerPitHazard and TrackedLamp. Importing HollowTower's `Scripts` folder on top of them gives duplicate-type compile errors, because they share the namespace.
   *Fix:* pick one copy of each file. Diff the two versions and keep the newer one, rather than importing the folder twice.

4. **Two player controllers.** HollowTower's scene uses its own `RemasteredPlayerController`, while collage game uses `FirstPersonController` and `MultiPlayerCharacterManager` with four players. If HollowTower comes in as a level of the game, its player object should be replaced by collage game's player setup.

## Minor items

- Add D3D12 to HollowTower's API list, or just use collage game's setting once merged.
- HollowTower's quality tiers don't assign a pipeline asset. After the merge, use collage game's PC tier with the URP asset, and add Low/Medium URP tiers if you want them.
- HDRP-only extras (the NVIDIA DLSS module, HDRP sky/fog, area lights, volumetrics) have no direct URP equivalent and need a URP substitute or have to be dropped.
- Existing warning in collage game: several tower meshes aren't marked Read/Write, so the runtime NavMesh bake works in the editor but **not in a build**. Turn on Read/Write in those models' import settings.

## Suggested order

Settle duplicate scripts (3) → port input calls (2) → copy HollowTower assets into `Assets/Levels/HollowTower` → convert materials and the volume to URP (1) → swap in collage game's player (4) → add the scene to Build Settings.
