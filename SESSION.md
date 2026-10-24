# Session log: making HollowTower coexist with collage game

Last updated 2026-10-22 by Claude (Projects thread "game design level 2"), then by Claude Code (URP conversion session, following `HANDOFF.md`).

## The two projects

- **collage game** (main game, contains the tutorial map `Assets/Scenes/TutorialScene.unity`)
  `C:\Users\Harikrishnan\Documents\trash programs\collage game\collage game`
  URP 17.6 (`Assets/Settings/PC_RPAsset.asset`), Input System only (activeInputHandler=1), Linear, D3D11+D3D12, Unity 6000.6.5f1.
  **Do not change anything in this project.**
- **HollowTower** (this folder, level being brought into line)
  Scene `Assets/HollowTowerRemastered/Scenes/HollowTower_Remastered.unity`
  Was HDRP 17.6 (`Assets/HollowTower/Settings/HollowTowerHDRP.asset`), old Input Manager, Linear, D3D11, same Unity version.

The full comparison is in `level-compatibility-report.md` in this folder.

## Unity MCP

Both editors run the CoplayDev `unity-mcp` package. Instances: `collage game@0ba8f70f` and `HollowTower@a1973c70`. Call `set_active_instance` with `HollowTower@a1973c70` before using any tool.

## Done so far

1. **Backup** of every .mat/.unity/.asset/.cs/.json/.prefab/.meta file (Library, Temp, Logs, .git, M4A1Source and Shots excluded), taken before any change:
   `C:\Users\HARIKR~1\AppData\Local\Temp\claude\C--Users-Harikrishnan-Documents\bcd8a6fa-8970-44e7-96ef-6d3934d729ff\scratchpad\hollowtower-backup-pre-urp` (45 MB).
   Git also has one commit (`d9b0a43`). A lot of uncommitted work was already present beforehand and nothing has been committed.
2. **Input fix (code):** `Assets/HollowTower/Scripts/InputWrap.cs` was extended with a generic KeyCode→Key map (A–Z, 0–9, F1–F12, arrow keys, right Shift/Ctrl, Alt), Mouse2 and a `MousePosition` property, and the arrow keys now count in `Move`. All direct `Input.*` calls in InteractiveDoor, RemasteredMapHUD, RemasteredPlayerController, TowerElevator and TowerExitElevator now go through `InputWrap.Down/Held/MouseDelta/Move/MousePosition`. No direct `Input.*` calls remain outside InputWrap.
3. **Packages installed** (user approved): `com.unity.render-pipelines.universal` 17.6.0 and `com.unity.inputsystem` 1.20.0. Both have imported, and the assemblies are loaded. HDRP is **still installed and is still the active pipeline**.
4. Active Input Handling is now **Both** (2). InputWrap uses its legacy path under Both and its new path under new-only. Either setting works.

## URP conversion (following HANDOFF.md)

MCP access note: this Claude Code session started in the parent folder, so the project-scoped `UnityMCP` server did not load as tools. The same server (`uvx --from mcpforunityserver==10.3.0 mcp-for-unity`) was driven over stdio by a small client script, always calling `set_active_instance HollowTower@a1973c70` first. Bulk work goes through the server's `execute_code` tool (CodeDom, C# 6).

Before screenshot (HDRP, from `Player_Camera`): `Assets/Screenshots/screenshot-20261022-205608.png`.

5. **Step 1 – URP assets created** in `Assets/HollowTowerRemastered/Settings/URP/`:
   - `HollowTower_URPAsset.asset` (UniversalRenderPipelineAsset): settings copied from collage game's `PC_RPAsset` (HDR on, 32-bit HDR precision default, depth + opaque textures, MSAA off, render scale 1, main light + additional lights per-pixel, shadows 2048 / 50 m / 4 cascades with the same splits, soft shadows high, SRP batcher, light layers, cookies, GPU Resident Drawer = Instanced, LDR grading, LUT 32). Only change: per-object light limit 8 (the URP maximum) instead of 4.
   - `HollowTower_Renderer.asset` (UniversalRendererData): **Forward+**, default URP PostProcessData assigned (post-processing enabled), depth priming off, intermediate texture Auto. No renderer features yet (SSAO is added in step 6). `m_UseNativeRenderPass` no longer exists in this URP version (renderer asset version 3), so there was nothing to set.

6. **Step 2 – pipeline switched:** `GraphicsSettings.defaultRenderPipeline` = `HollowTower_URPAsset`. The six quality levels (Very Low … Ultra) were trimmed to a single **"PC"** tier (collage game's PC values: pixel lights 2, aniso forced, AA off, LOD bias 2, vSync off, shadowmask mode Distance), which uses `HollowTower_URPAsset` and is the default for every platform. No script depended on the old quality levels. `GraphicsSettings.currentRenderPipeline` now reports `UniversalRenderPipelineAsset`. HDRP is still installed (removed in step 8). Console: no errors after the switch.

7. **Step 3 – materials converted** with a new editor script, `Assets/HollowTower/Editor/URPConversion.cs` (menu *Hollow Tower > URP*; also `URPConversion.ConvertMaterials()`). It reads values straight from each material's serialized data, so it would still work with HDRP removed. **45 HDRP/Lit `.mat` files** → `Universal Render Pipeline/Lit` (a fresh scan found 45, not the 49 in the inventory below; that is every HDRP `.mat` in the project):
   - `_BaseColorMap`/`_BaseColor` (+ tiling/offset) → `_BaseMap`/`_BaseColor`; `_NormalMap`/`_NormalScale` → `_BumpMap`/`_BumpScale`; `_MaskMap` → `_MetallicGlossMap` (R metal, A smoothness) and `_OcclusionMap` (G); `_Smoothness` = `_SmoothnessRemapMax` with a mask map, else `_Smoothness`; `_Metallic` copied; occlusion strength from the AO remap range. Keywords set by URP's own `BaseShaderGUI.SetMaterialKeywords` + `LitGUI.SetMaterialKeywords` (`_NORMALMAP`, `_METALLICSPECGLOSSMAP`, `_OCCLUSIONMAP`, `_EMISSION`).
   - Emission: a render test showed Unity uploads `[HDR]` colours unchanged, so HDRP's `_EmissiveColor` is linear nits. Converted value = what HDRP fed the tonemapper at EV 5 (typical auto-exposure in a lit corridor), honouring `_EmissiveExposureWeight`, then capped (6 for lamp bulbs, 2.5 for emission-mapped ceiling panels). Results: LampCool/Warm/Red 6, ceilings F1–F4 2.5, elevator amber lamp 3.5, green LED 3.8, LampFixture 0.19.
   - Surface/alpha/double-sided mapping is in the script (none of these materials are transparent, alpha-clipped or double-sided).
   - **3ds Max physical material** (55 sub-assets in `Resources/Models/Egypt/Egypt.FBX`): these are embedded in the FBX, so they can't be edited in place. They already resolve to **URP's own** `Shader Graphs/PhysicalMaterial3DsMax` (URP ships one and regenerates FBX materials with it), so they were left on that rather than extracted to new `.mat` files. The Egypt model isn't used by the remastered scene.
   - glTF Shader Graph materials (262) left alone as planned; all report as supported under URP.
   - Check afterwards: 0 materials in `Assets` reference an HDRP shader or an unsupported shader.

8. **Step 4 – lights converted** (`URPConversion.ConvertLights()`): all **210** lights now have `UniversalAdditionalLightData`; HDAdditionalLightData removed from the 206 that had it (the 4 exit-elevator lights on floor 4 had no HDRP data but did have HDRP-scale candela values, so the converter treats any light without URP data as HDRP-authored). Colour, range, spot/inner angles and shadow modes kept. Intensities: HDRP value converted to candela per its unit (lumen spots with reflector: lm / 2π(1−cos(½·angle)); lumen points: lm / 4π; candela as is; directional lux as is), × 0.045, capped at 5. Collage game's TutorialScene uses roughly 2–9.5, with its torch at 20. Results:
   - 128 cool ceiling spots 312 lm → **3.40**, 55 warm 288 lm → 3.14, 6 at 264 lm → 2.87, 11 red emergency 240 lm → 2.78
   - Player `Tactical_Flashlight` 2600 lm → **20** (matches collage game's `Torch_SpotLight`)
   - Floor 1 spawn elevator: interior spot 2.05, door/marquee points 0.17. Floor 4 exit elevator: interior spot, door lights and marquee all hit the cap of 5 (they were 450–1800 cd in HDRP, roughly 100× the spawn elevator's).
   - Directional moonlight 0.5 lux → 0.023
   - `RemasteredFlicker` reads its base intensity at runtime, so it follows the new values automatically.
9. **Step 5 – camera:** `Player_Camera` lost HDAdditionalCameraData and got UniversalAdditionalCameraData with **post-processing on**, AA None (HDRP was None), dithering/stopNaN off (as in HDRP), volume mask Default (as in HDRP). `allowHDR` set to true (HDRP ignored this flag; URP needs it for HDR + bloom).
10. **Step 6 – volume, SSAO, fog:**
   - New profile `Assets/HollowTowerRemastered/Settings/URP/HollowTower_URPVolumeProfile.asset`, assigned to the scene's `Global_Horror_Volume` (the old HDRP `HorrorVolumeProfile.asset` is left in `Materials/` and no longer referenced). Overrides: **Tonemapping ACES**; **Bloom** threshold 1.3 (URP's threshold is gamma-space; HDRP's linear 1.8 ≈ 1.3), intensity 0.4, scatter 0.7 (as HDRP), high-quality filtering; **Motion Blur** intensity 0 (as in HDRP, i.e. off) at Medium quality; **Color Adjustments** post-exposure **+1 EV** in place of HDRP auto exposure (tuned visually: at 0 EV, ACES + SSAO made corridors too dark).
   - **Screen Space Ambient Occlusion renderer feature** added to `HollowTower_Renderer`: intensity 0.6 (HDRP value), radius 0.3 (collage game's value; HDRP's 2 m radius doesn't translate to URP), direct lighting strength 0.25, depth-normals source, medium normal and sample quality, high blur, falloff 100.
   - **Fog:** `RenderSettings` fog on, Exponential, density 1/70 ≈ 0.0143 (HDRP mean free path 70 m), colour sRGB (0.07, 0.08, 0.10) dark blue-grey.
   - Scene saved after each of steps 4–6.

11. **Step 7 – skybox:** the "cubemap skybox with an empty asset path" in the inventory turned out to be **HDRP's internal `Skybox/Cubemap` material**, which HDRP's sky manager assigns to `RenderSettings.skybox` at runtime. The scene file never saved a skybox (`m_SkyboxMaterial: {fileID: 0}` in the pre-change backup too), so under URP there was no skybox at all. HDRP's actual sky was its default **HDRI Sky** (`DefaultHDRISky.exr` from the HDRP package, exposure 11, set in `HDRPDefaultResources/DefaultSettingsVolumeProfile.asset`). Fix:
   - Copied that cubemap to `Assets/HollowTowerRemastered/Settings/URP/HollowTower_SkyCubemap.exr` (so it survives HDRP removal) and made `HollowTower_Skybox.mat` (**`Skybox/Cubemap`**, which URP supports). Exposure 0.047 and a blue-grey tint bring the sky to about the fog colour's brightness (measured with a sky-only render), so wherever the 45 m far clip exposes the sky it blends into the fog rather than showing daylight.
   - `RenderSettings.skybox` = that material; default reflections from the skybox; sun source = `Directional_Moonlight`.
   - Ambient made explicit: **Flat**, colour sRGB (0.212, 0.227, 0.259). That is exactly what the scene had been lit with since the switch (Unity's ambient-sky-colour fallback when skybox ambient has no skybox), so the tuned look is unchanged but no longer depends on that fallback.

12. **Step 8 – HDRP code guarded/retired, HDRP removed.** HDRP-only code now sits behind a scripting define, **`HOLLOWTOWER_HDRP`**, which is **not set**. (To restore the original HDRP code you'd reinstall HDRP and add that define.)
   - **Remastered-scene scripts ported (no HDRP left):** `RemasteredLightOptimizer` (dropped the HDAdditionalLightData shadow-resolution/volumetric/cached-shadow tweaks; floor/distance culling and the 2-lamp soft-shadow budget unchanged) and `RemasteredPlayerController` (an unused `using` removed).
   - **Retired:** `Editor/HDRPSetup.cs` (whole file guarded). In `Editor/MapRemasterBuilder.cs`, Remaster steps 1–4, 6 and 7 are guarded: they build the map with HDRP materials/lights/volume, and re-running them would regenerate HDRP content over the converted scene. "Open Remastered Scene" and "Remaster/5. Bake Tower NavMesh" still work.
   - **Old procedural game** (`GameBootstrap` → `LevelBuilder`, `Gfx`, `HDRPLook`, etc.; its scene was already deleted and it isn't used by the remastered map): kept compiling with a URP fallback rather than deleted. `Gfx` now builds URP Lit/Unlit materials, URP emission/transparency/decals (URP decals need a Decal renderer feature to draw, which isn't added), uses glTFast materials as they are, and works at URP light scale (`Gfx.Lumens` = 1.86, the remastered map's 0.045-per-candela scale; `Gfx.Nits` = 1; new `Gfx.SpotLumens` = 520 for spot lights given in lumens). New `Gfx.Validate()` / `Gfx.LightData()` replace the direct `HDMaterial.ValidateMaterial` / `HDAdditionalLightData` calls in Annex, BossRig, DogRig, GhostBody, MP5, Wing, LevelBuilder and GameManager. `HDRPLook` has a URP stand-in with the same API (ACES, grading, vignette, grain, bloom, a dim trilight ambient, SMAA; ray tracing and DLSS report off); `ScreenFX` works unchanged on URP's Vignette/ChromaticAberration/ColorAdjustments. HDRP-only extras (triplanar mapping, ray tracing, DLSS, volumetric fog, normal/mask maps that callers set by HDRP property names) are simply absent under URP. This fallback compiles but **has not been play-tested**.
   - **Packages removed:** `com.unity.render-pipelines.high-definition` (its `-config` dependency went with it) and `com.unity.modules.nvidia` (nothing else used it). The stale HDRP entry in GraphicsSettings' global-settings map was removed (only URP is listed now, as in collage game).
   - **Editor hiccup after the removal, fixed:** the Rendering Debugger's in-memory state (`DebugDisplaySerializer`, which survives domain reloads) came back with null entries where HDRP's debug settings had been. That made URP's pipeline constructor throw (a NullReferenceException, then "Blitter is already initialized" on every retry), and nothing rendered. Clearing it didn't stick across reloads, so a small editor script, `Editor/URPDebugStateCleanup.cs` (`[InitializeOnLoad]`), drops those nulls after each reload; URP now creates cleanly. One harmless message per domain reload is left: "A scripted class has a different serialization layout when loading (Read 240 bytes but expected 3468 bytes)". It comes from the same stale in-memory state and should disappear after an editor restart, at which point the cleanup script has nothing to do (it can be deleted then).
   - Left in place, now unreferenced (safe to delete once you're happy): `Assets/HDRPDefaultResources/` (HDRP global settings + default volume profile), `Assets/HollowTower/Settings/HollowTowerHDRP.asset`, `Assets/HollowTowerRemastered/Materials/HorrorVolumeProfile.asset`. The scene has no missing-script components.
   - Console after all this: 0 compile errors. Pre-existing warnings only (DogImport CS0162; HollowTowerSetup UAC0005).

13. **Step 9 – graphics APIs:** StandaloneWindows64 now uses **Direct3D11, Direct3D12** (manual list, D3D11 first). This matches the effective list in collage game, which leaves Windows on Automatic. The editor keeps running on D3D11, so no restart was needed. The `Hollow Tower/Graphics/Set Graphics API to DirectX 11` menu in `HollowTowerSetup.cs` would put it back to D3D11-only, but it only runs when clicked.

14. **Step 10 – verified:** 0 compile errors; 13,780 renderers / 13,780 material slots, **0 pink** (all URP Lit), skybox supported, `RenderPipelineManager.currentPipeline` = UniversalRenderPipeline. Play mode entered and stopped cleanly (spawn elevator opens, map HUD works, no runtime exceptions). Scene saved. Screenshots: before `Assets/Screenshots/screenshot-20261022-205608.png` (HDRP, Player_Camera); after `Assets/Screenshots/after_urp_playmode.png`; extra corridor/elevator renders were saved outside the project in the session scratchpad.
15. **Step 11 – report:** delivered to the user on 2026-10-23 (the editor had been restarted, PID 9952, scene loaded on DX11). Things to mention: the floor-4 exit elevator lights sit at the cap of 5 and look hot; the spawn elevator cab ceiling is bright; the remaining one-per-reload "different serialization layout" message should go away after an editor restart; the old procedural game's URP fallback hasn't been play-tested.

## Scene inventory (HollowTower_Remastered)

14,643 objects: 13,779 MeshRenderers, 210 Lights each with HDAdditionalLightData (mostly realtime spots, HDRP intensity about 288–312, no shadows), 1 Camera (`Player_Camera`, FOV 75, far clip 45) with HDAdditionalCameraData, 1 Volume using `Assets/HollowTowerRemastered/Materials/HorrorVolumeProfile.asset` (Tonemapping, SSAO, Fog, Bloom, MotionBlur, Exposure), 164 InteractiveDoor, 38 RemasteredFlicker. Baked GI is on, but there are no lightmaps. The skybox is a cubemap skybox, and the material asset path came back empty.
Materials in Assets: 49 HDRP/Lit, 9 glTF pbrMetallicRoughness Shader Graph, 3 glTF unlit, 1 HDRP 3DSMax PhysicalMaterial.

## Not done yet

See `HANDOFF.md`.

## Player model swap (2026-10-23, Claude Code)

User asked to replace the player model with collage game's tutorial character (the faceless mannequin) and delete the old backpack model.
- Copied from collage game (read only), keeping its paths and GUIDs: `Assets/Art/Characters/SK_Web_Player_Animated.fbx`, `Assets/Materials/Mat_Character_Body.mat`, `Assets/Materials/Mat_Player1_Joints.mat`, `Assets/Characters/Player/Anim_Death_Collapse.anim`, and the current `Player_LocomotionController.controller` (HollowTower's older copy pointed at the missing FBX).
- `Player_Character_Controlled/Player_Model` replaced by `Model_Animated` (the FBX, set up as in TutorialScene's Player_1: scale 1.3, body + Player 1 cyan joint materials, Generic animator with the locomotion controller, shadows-only body for first person like the tutorial's controlled player). `RemasteredPlayerController.animator/characterModel` point at it.
- `RemasteredPlayerController.Start` now plays `Idle` directly: the collage controller starts in `Unconscious` and waits for a `WakeUp` trigger from the tutorial intro.
- Old backpack model (`Player_Character.fbx/.prefab`, `Player_AnimatorController`, `Mat_Player`, its textures and the 4 unused `Player_*.anim`) moved to the Windows Recycle Bin; nothing else referenced them. Originals remain in collage game.
- `MapRemasterBuilder` step 3 (retired, behind `HOLLOWTOWER_HDRP`) updated to build the mannequin instead.
- Checked: 0 compile errors, 0 missing scripts/prefabs, play mode starts in Idle with no console errors. Scene saved. Not committed (user: don't commit yet).

## Abandoned-college dressing + player fixes (2026-10-23, Claude Code)

**Player:** model scale 1.3 → 1.0 (the mannequin is 1.79 m at 1.0). CharacterController 1.8 m tall, radius 0.35 (was 2.15 / 0.42, too tall for the 1.94 m elevator door and 2.18 m cab, which is why the player got stuck). Camera 1.66 m, 0.14 m forward. Body now renders in first person; `RemasteredPlayerController.LateUpdate` shrinks the head bone in first person (`showBodyInFirstPerson`, `firstPersonForward`). Walk-out test with the doors open: the player leaves the spawn cab freely.

**Props:** `Assets/HollowTowerRemastered/Props/`
- `WebPack/` – 55 glTF props from `collage game assets from web/BackgroundProps` (tree trunk, street lamp, rocks left out). `PolyHaven/` – 79 more CC0 models downloaded from polyhaven.com (1k textures). All CC0.
- `Prefabs/` – one normalised wrapper prefab per model (`Editor/PropLibrary.cs`, menu *Hollow Tower > College Dressing > 1. Build Prop Prefabs*): pivot = bottom centre (wall props: back face, ceiling props: top), front = +Z, bad scales fixed (`steel_frame_shelves_01` ×0.1, `WoodenChair_01` ×0.42, rubble ×0.3), box collider + `PropInfo` with the measured size.
- `ProcPrefabs/`, `Generated/`, `Proc/` – box-built props (lockers, stalls, sinks, radiators, lab benches, notice boards, vending, bunks, cubicles, server racks...) baked as reusable variants, merged room litter meshes, and their materials. All regenerated by the dresser.

**Dresser:** `Editor/CollegeDresser.cs` (placement rules), `CollegeRecipes.cs` (room recipes, bays, decay, corridors), `ProcKit.cs` (box-built props), `ProcVariants.cs`, `MeshMerge.cs`, `DressingPreview.cs` (review renders). Menu *Hollow Tower > College Dressing > 2. Dress All Floors* (seed 20261023) / *3. Clear Dressing*. Output goes under `College_Dressing` in each floor root (so floor culling still applies).
- Every prop is measured after rotation and its lowest point snapped to the floor; things on desks/shelves are placed by raycasting the host's real mesh. Check after the final build: 13,166 placed objects, 0 below the floor, 0 floating (917 wall/ceiling mounts excluded).
- Each room type has several layouts (classroom: rows / desks shoved aside with chairs upside down / ransacked / barricade / group islands, etc.) chosen per room and never the same as the previous room of that type; big rooms are filled bay by bay (~5 m) with type-weighted clusters (lounge corner, desk pair, shelf row, squatter's camp, collapsed ceiling, leak with bucket, junk heap...). Per-room mess level drives knocked-over furniture, paper, fallen tiles, ceiling holes with dangling cables, puddles, rubbish, rats.
- Doorways, a walkway from each door to the room centre, the elevator cabs/landings, stair openings and pits are kept clear.
- Cost: ~28.8k dressing renderers (≈7k per floor, floor culling keeps only nearby floors on). Scene file 43 MB → 81 MB; generated meshes ≈45 MB (text-serialised).
- Not verified: frame rate in a real play session (the editor was in the background, so play mode barely ticked).

## Dressing rework after feedback (2026-10-23, later)

User feedback: too much small litter ("feels like trash"), corridors too cluttered, abandoned should be subtle (a bit off, tilted), rooms need meaning (no random junk), more wall objects, more kinds of educational spaces (library, botany, labs...), Backrooms-like liminal reference, and pixelation in dark-to-bright areas.

- **Graphics:** camera AA None → SMAA High, dithering on (removes banding in dark gradients), URP HDR buffer 32-bit → 64-bit, torch shadow resolution → High tier (2048, additional-light atlas 4096, high soft-shadow quality).
- **Room identities:** new `Scripts/CollegeRoles.cs` gives every room a college label + role (Entrance Hall, Botanical Atrium, Natural History Gallery, Trophy Hall, Lecture Halls, Chemistry/Biology/Physics labs, Engineering Workshop, Exam Hall, Geography Room, Library, Rare Books Archive, Dorms, Greenhouse, Herbarium, Reading Room, Study Hall, Senate Chamber, Professor's offices, Bursar's/Registrar's...). `Room.label`/`Room.role` added to LevelLayout (names unchanged because the old LevelBuilder looks rooms up by name); `RemasteredMapHUD` shows the label.
- **Recipes rewritten** (`Editor/CollegeRecipes.cs`, old version kept in the session scratchpad): role-based, calm. Mess odds now 5:4:1.2:0.25 (most rooms "left behind"); furniture drifts/turns/pushed back, rarely tipped; very little paper; ceiling damage only in the messiest rooms. Big rooms repeat a meaningful unit (desk pods + bookcase dividers, dormitory bed spaces, easel circles, lounge groups, stall blocks) instead of litter. Corridors: sparse benches/plants/fountains/lockers, notices, windows; almost no floor litter.
- **New props:** 49 more Poly Haven CC0 models (planters, plants (sets split into single plants by `PropLibrary`), busts/statues, chess set, ukulele, grandfather clock, tools, benches, tea set, chandelier...). New box-built pieces in `Editor/ProcKitEdu.cs`: windows with blinds (outer walls, radiators under some), posters, periodic table, world maps, banners, pegboards, exit signs, wall shelves, trophy cases, glass vitrines, globes, easels, lectern, workbench, planter beds, telescope, fume hood, lecture tiers, specimen jars.
- **Fixes found on the way:** prefab builder now keeps a model's own root scale (Chandelier_01 had come out 75 m wide); wall-mounted items sit on the plaster (were 7.5 cm off the wall) and their scene test ignores the wall they hang on (windows were all being rejected); chairs round tables sit clear of the table (were all rejected).
- Final build: 10,064 placed objects, 0 below floor, 0 floating (checker false positives explained in chat), 1,624 generated meshes all valid, ~24.8k dressing renderers. Scene saved. Not committed.

## Pipes / big landmarks / chair pixelation (2026-10-23, finished)

User feedback: the wall piping looks bad, big rooms need pieces large enough to block the view across them, and chairs look slightly pixelated.

- **Chair pixelation, root cause:** the glTF props' normal maps were decoded wrongly. Unity had imported them as sRGB colour textures (gamma-skewed normals, DXT1 blocks), which made the shading noisy. `Editor/PropTextureFix.cs` (menu *Hollow Tower > College Dressing > 0. Fix Prop Texture Imports*) reads every prop .gltf and sets each image's importer: normal and metal/roughness/AO maps → **Default type, linear**; base colour/emissive → sRGB; all BC7 (CompressedHQ), trilinear, aniso 8. The normal maps must **not** use Unity's NormalMap texture type: glTFast's URP shader graph decodes the raw RGB itself, and with NormalMap packing desk tops render black and metal legs chalk white (A/B tested on SchoolDesk_01). Result: chairs and desks shade cleanly, upside-down chairs no longer smear white, and table cloths and wood grain show.
- **Pipes/cables:** `modular_industrial_pipes_01` and `modular_electric_cables` are both Poly Haven kit sheets (49 loose pieces laid side by side), so on a wall they read as a catalogue board. Both are set to `skip` in `PropLibrary` and nothing places them now. The plant room's identity is now the procedural `Boiler` (drum, risers, header, valves, flue) and `Tank`.
- **Big view blockers** (`Editor/ProcKitBig.cs`, the `Landmarks` pass at the start of `CollegeRecipes.Dress`):
  - Pieces: dust sheet over stacked furniture (now pale white/linen, since dark sheets read as dirt mounds), stacked desks and chairs, zig-zag exhibition panels, a scaffold tower with tarp, crate stacks, boiler, tank, and a new **`Hoarding`**: a half-built partition stud wall, 2.5 m tall and 3.7–7.3 m long, plywood on a timber frame, painted on the front half the time, sometimes with a short return, a sheet off and leant against the back, stapled notices and leftover paint tins/offcuts.
  - `Screens`: rooms ≥220 m² get 1 long screen (≥400 m²: 2; ≥600 m²: 3), a hoarding or panels by room type, standing square across the view from the main door. The first stands part-way in, right beside the walkway; the second is deeper in on the other side; the third (very big rooms only) faces the second door. Then 0–4 other pieces by area, square to the walls, ≥1.8 m from walls and ≥5 m apart. Doorways and walkways stay clear.
  - Caps: private rooms (offices, dorms, meeting rooms...) get one dust-sheeted pile, two only above 300 m², instead of a field of them. Botany rooms get at most 2 scaffolds.
- Final build (seed 20261023): 9,659 dressing objects, 56+ partition walls, 0 below the floor, 0 console errors. Scene saved. **Not committed** (user's instruction).
- Eye-level checks (1.66 m) from the doors of the Entrance Hall (both doors), Dining Hall (both doors), Boiler Room, Mathematics Room, Exam Hall, Library, Staff Common Room, Botanical Atrium, Dorm 320 and Professor's Office 401: every big room now has at least one 2.5 m piece across the line of sight.

### Still worth a look
- The plywood and paint are flat colours (no texture). Fine in the dark rooms; a wood-grain texture would help up close.
- Puddle decals in classrooms show as dark octagons under the torch.
- Generator and pump rooms each get 2–3 boilers. A pump/generator builder would make them distinct.
- Frame rate in a real play session is still unmeasured.

## Four players + floor 1 co-op obstacles (2026-10-23)

**Four players (from the collage game tutorial):** `Player_1`..`Player_4` (mannequin with the tutorial's cyan/amber/mint/violet joint materials, copied with their GUIDs) start in the spawn elevator cab. `Scripts/PlayerParty.cs` on `Player_Party` is the tutorial's MultiPlayerCharacterManager:
- **F1-F4** move control and the camera into that body; the others stay standing where they were left.
- **T** brings the other three behind you, checking each spot is clear.
- Only the steered body has the `Player` tag, so floor culling, the lamp optimiser and the exit elevator follow it.
- The map HUD, lamp optimiser and floor culling moved from the player onto `Player_Party`. The map's scene swap moved from F1 to **F9**.
- Menu: *Hollow Tower > Players > Set Up Four Players* (`Editor/PlayerPartySetup.cs`, safe to re-run).

**Abilities** (collage game design):
- P1 sees false walls.
- P2 sees hidden pits.
- P3 sees invisible enemies. Not used on floor 1 yet.
- P4 has the only torch, a compass, and 4 inventory slots (the others have 2).

**New runtime pieces** (`Assets/HollowTower/Scripts/`):
- `PartyPerception`: the tutorial's PerceptionCover; hides or shows renderers by who is steered.
- `PartyInteractor` + `IPartyInteractable`: E prompts. It also shows ordinary doors' prompts and locked messages.
- `PlayerInventory` (per body) and `PartyItem` (pick up with E, drop with G, mouse wheel selects a slot). With `needsTorch` set, an item is invisible and can't be taken until a party torch beam is on it.
- `HiddenWallDoor`: false wall. It is the original wall until opened; P1 sees a cyan outline and gets the prompt; anyone can push it.
- `PitHazards`: holes are tested by position, so fake floors and narrow strips work exactly as drawn. A fall fades out and puts the body back on the last solid floor it stood on (checked for floor under it), with a fallback to its spawn spot and a kill plane.
- `FuseSocket`: powers and unlocks the stair door.
- `FloorObjective`: objective line, plus P4's compass, which points to the fuse, then the socket, then the stairs.
- `Floor1Design`: every coordinate.
- `Floor1BuildRecord`: what the builder changed, so a rebuild can undo it.

**Floor 1 route:** spawn (Parking, SE), then the fuse (Cold Storage), then the stairs (NW).
1. South corridor, cols 23-26: **hidden-pit minefield**, 12 x 3 tiles of 1.33 m. The others see the normal floor; P2 sees stepping slabs over a shaft.
2. Cols 12-13: **8 m chasm** (too wide to sprint-jump), with a **girder only P2 sees**. It is solid for everyone.
3. Cold Storage's real doors are **frozen shut**. The way in is a **false wall from Food Prep** (P1). The room's lamp is out and the **fuse cell only appears in P4's torch beam**. Whoever is standing there can take it while P4 holds the light.
4. West corridor (6,15): **hidden pit with a 1.2 m ledge** (P2). The collapse at (6,10) blocks the corridor, so the way north is a **false wall from the Staff Locker Room into the Washrooms** (P1).
5. A visible pit with a ledge at (6,5). The **stair door stays locked ("no power")** until the fuse goes into the socket beside it.

**Route shaping:**
- Rubble barricades at (29,16), (17,16) and (6,10).
- Jammed doors: Pump station S and Dishwashing S (bypasses round the minefield and chasm), and both Cold Storage doors.
- The four outer south doorways (they opened onto the void, including the one in the spawn room) are chained shut.

**Builder and checks:**
- Builder: *Hollow Tower > Floor 1 > Build Co-op Obstacles* (`Editor/Floor1GameplayBuilder.cs`). It re-dresses floor 1 (the dresser now skips `Floor1Design.DressingExclusions`) and builds under `Floor_1_Ground/Floor_1_Gameplay`. *Remove Co-op Obstacles* undoes it.
- Verified in Play mode with test scripts: switching and T; falling into a hidden hole and respawning on the solid side; no fuse prompt in the dark; the fuse revealed by P4's torch; P1 taking it (2/2 slots), dropping it, P4 picking it up; the socket refusing P1 and accepting P4; the stair door unlocking and opening; P3 opening the false wall with no prompt.
- Renders checked:
  - Minefield and ledge pit for P1 vs P2.
  - Chasm girder for P1 vs P2.
  - False-wall outline for P1 vs P3.
  - Barricade.
  - The P4 HUD with the compass.
- Note for render checks: GPU-driven static renderers (the floor slabs) pick up enable/disable on the next frame, so switch players in one call and render in the next.
- Scene saved. **Not committed.**

**Not done yet:**
- P3's invisible enemies.
- Floors 2-4.
- Sounds for the false wall, socket and falls.
- A "floor cleared" check when everyone is upstairs.

## Spray paint cans (2026-10-23)

Every player carries a spray can in their own colour (P1 cyan, P2 amber, P3 mint, P4 violet). **Hold the left mouse button** to paint where you're aiming, up to about 3.6 m away. The paint does not take an inventory slot.

**Code:** `Scripts/SprayPaint.cs` on `Player_Party`. It uses the `Mat_SprayPaint` material (URP Particles/Unlit, transparent, double-sided).

**How the paint is drawn:**
- Dots are filled in along each swipe so lines stay continuous.
- All the dots go into a few shared vertex-coloured meshes (16 chunks of 1,200 dots); the oldest paint is reused when the budget runs out.
- The paint is unlit, like fluorescent marking paint, so it reads in the dark.
- Paint is runtime-only and is not saved with the scene.

**Where paint lands:**
- On a **hidden pit**, the paint lands on the fake floor (which has no collider) at floor level. To the others it is paint on the floor; to P2 it hangs exactly over the hole.
- Paint on a **door leaf** moves with the door.
- Paint on a **false wall** is kept on the wall and slides into the floor with the panel when the wall opens (`HiddenWallDoor.PaintAnchor` / `ForCollider`).

**Can models and sound:**
- A can sits in each mannequin's right hand (`mixamorig:RightHand`).
- A view-model can appears at the bottom right of the screen while spraying, with a coloured mist and a generated hiss.
- Set up by `PlayerPartySetup` (re-runnable).

**Tested in Play mode with the play-test input bot:**
- P2 marked the minefield's hidden holes. P1 sees amber lines on plain floor; P2 sees them over the holes.
- P1 painted an X on the false wall. It went down with the panel when P3 opened the wall.
- Paint on the Staff Locker Room door moved with the door when it opened.

**Rendering note:** when the editor window isn't focused, Play mode stops advancing frames between MCP calls. Use `EditorApplication.Step()` (paused) inside an `EditorApplication.update` callback to drive frames for tests.

## Death, spectating, P4's torch blessing, easier minefield (2026-10-23)

**Death is permanent.** Falling into a pit (hidden or visible) kills the player; there is no respawn.
- The body drops to the bottom of the shaft and plays the controller's `Death` state (`RemasteredPlayerController.Die`).
- Whatever they carried is dropped on the last solid floor they stood on (`PlayerInventory.DropAllAt`), so the fuse cell can't be lost.
- A 0.12 m grace margin forgives a stumble at a hole's lip (`PitHazards.grace`).

**Spectating** (`Scripts/PlayerPartySpectate.cs`, partial of `PlayerParty`). Steering a dead player (F1-F4) puts the camera inside the nearest living teammate's head; their head is hidden like in first person.
- **Mouse:** looks around.
- **Mouse wheel:** watch another living teammate.
- **Left click:** drop a ping (beam + pulsing ring in the spectator's colour, 9 s, a beep, at most 3 each) that everyone sees.
- **Abilities still work:** perception goes by the steered player, so a dead P2 still sees hidden pits from wherever they watch, and a dead P1 sees false walls.
- **Tracking:** the watched body gets the `Player` tag, so floor culling, lights and the map follow the view.
- **Not allowed for the dead:** doors, interaction, spraying and T.
- **Everyone dead:** "THE TOWER TOOK EVERYONE", [R] restarts.

**P4's blessing:** when P4 is dead, the teammate P4 watches carries a torch (violet-tinted copy of P4's spot light).
- Only P4 aims it: it points where P4's spectator view looks, relative to that teammate's head, and keeps that aim while the local player steers someone else.
- It counts as a torch for revealing the fuse cell (`PlayerParty.Torches()`).

**Minefield made easier** (death is now final and enemies are coming): 9 x 3 tiles (x 96-108) with 8 holes, down from 12 x 3 with 18. It has full-width solid rows between the hazards where a lost player can stop and get directions. The other pits are unchanged.

Tested in Play mode:
- P2 and P4 died in the minefield; both spectated P1.
- P1 got the blessing torch, lighting the floor ahead.
- P4 pinged.
- P3 died carrying the fuse, which was left at the ledge-pit edge.
- The last death showed the game-over screen.
- No console errors.

## Monsters, SFX, P4's compass model (2026-10-23)

Built by *Hollow Tower > Floor 1 > Build Monsters, Sound and Compass* (`Editor/Floor1MonstersBuilder.cs`, re-runnable).

**Monster:** Needlelimbs (Entity 38). The model, the 4 clips and the animator controller were copied from collage game with their GUIDs (`Assets/Characters/Entity38`, minus the prefab, which used the tutorial's AI script). AI is in `Scripts/NeedleMonster.cs`:
- States: Patrol, Investigate (noises), Alert (procedural rear-up and screech), Chase, Attack (kills), Search (procedural head sweep), Feed.
- Sight: field of view plus line of sight; 13 m normally, 6.5 m against a crouching player.
- Hearing: sprinting, landing, doors and false walls.
- It bashes unlocked doors open.

**Two monsters on floor 1:**
- A visible one hunting the south-west (Atrium, Food Prep, Generator room, south corridor, west corridor, locker room).
- An invisible one in the north (lobby, washrooms, west corridor up to the stairs). Only P3 sees it until it is hunting.

**NavMesh:** baked from floor 1's colliders into `Navigation/Floor1_NavMesh.asset` with the built-in NavMeshBuilder (no package needed), loaded by `NavMeshDataLoader`. Unlocked door leaves were left out of the bake; jammed doors, barricades, closed false walls and pit holes block it.

**Sound** (`Scripts/SfxBank.cs` on Player_Party, `PlayerSfx` on each body). Clips were copied from collage game's `Assets/Audio/SFX`; door, false-wall, socket and needle-step sounds were synthesised into `Audio/SFX/Generated`.
- Players: footsteps by pace, jump, land, torch click.
- Deaths: fall scream and impact, death gasp.
- World: door open, close and locked, false-wall grind, socket power-up, item pickup, ambient drone.
- Monster: needle steps, screech, roar, attack.
- Proximity dread: a lurking loop and then a heartbeat as any monster nears the camera's body. It is heard for the invisible monster too.

**Compass:** a 3D brass compass in P4's left hand plus a view copy while P4 is steered (`CompassModel`). The needle points to the fuse, then the socket, then the stairs; the glass glows as the target gets near. The HUD dial was removed.

**Tested in Play mode:**
- Both monsters patrol on the NavMesh.
- The visible one spotted P1 in the corridor, screeched, chased, attacked and killed.
- The invisible one renders for P3 only.
- The compass view render works.


## Human Deer (2026-10-23)

The Human Deer from Consolidated_Monsters is copied to `Assets/Characters/HumanDeer`. It had a skeleton (84 joints) but no animation.

`Editor/HumanDeerAnimator.cs` (*Hollow Tower > Monsters > Build Human Deer Animations*) builds URP materials (base colour, normal, faint emissive) and five clips from per-bone swing functions applied about rest-pose world axes:
- Deer_Idle
- Deer_Walk (a hunched stalk)
- Deer_Run (a low lope)
- Deer_Attack (rear back, then lunge and rake)
- Deer_Scream (head thrown up, jaw gaping, arms out)

It also builds `HumanDeer_Animator.controller`. The fur cards are hidden because their Arnold material does not port.

The deer replaces the visible south-west monster on floor 1: about 2.5 m tall, walks at 1.5, runs at 5.6, plays its own screech on Alert. `NeedleMonster` now takes the state names per creature. The invisible north monster is still a Needlelimbs. Tested in Play mode: patrol, alert/screech, chase, attack, kill.

Note: edit-mode `SampleAnimation` previews do not redraw the big skinned body mesh before a camera render. Check poses in Play mode.
