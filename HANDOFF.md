# Handoff (current, 2026-10-23): four players + floor 1 co-op obstacles (DONE)

Four tutorial players with switching (F1-F4, T), the ability system (P1 false walls, P2 hidden pits, P4 torch, compass and 4 slots; P3's invisible enemies not yet), inventories, and floor 1's designed route (minefield, P2-only girder over a chasm, two false walls, dark Cold Storage with a torch-revealed fuse cell, stairwell fuse socket). Details are in the last section of `SESSION.md`; coordinates are in `Scripts/Floor1Design.cs`. Next is probably P3's invisible enemies and floor 2. Nothing is committed (user's instruction).

---

# Handoff (current, 2026-10-23): pipes, big view blockers, chair pixelation (DONE)

All three items are finished and the scene is saved. See the "Pipes / big landmarks / chair pixelation" section at the end of `SESSION.md` for what changed and what is still worth a look. Nothing has been committed (user's instruction).

Tools used: the stdio MCP client `mcp.py` + `capture.py` (render Player_Camera from poses) in the session scratchpad. Long editor jobs (Dress All Floors ≈ 10 min, texture reimport) outlast the MCP response timeout but keep running in Unity. Wait with `python mcp.py wait 600` and check the result afterwards. `EditorApplication.delayCall` does not fire while the editor window is unfocused, so don't rely on it.

---

# Handoff: finish converting HollowTower to URP

Read `SESSION.md` first, then do the steps below in order. Update `SESSION.md` as you finish each one.

## Rules from the user (shibin)

- Work **only in this HollowTower project**. Never change the collage game project, its settings or the tutorial map (`TutorialScene`). Reading it for reference is fine.
- Convert **in place**, not in a copy.
- **Don't ask questions.** Make reasonable calls, keep going, and stop only for things that need the user's hands (for example clicking a Unity dialog).
- A backup of the pre-change state is listed in `SESSION.md`. If something goes badly wrong, restore from it.

## Remaining steps

1. **Create the URP assets** in `Assets/HollowTowerRemastered/Settings/URP/`: a `UniversalRenderPipelineAsset` plus a `UniversalRendererData` (Forward+ suits 210 lights; set the per-object light limit high). Base the settings on collage game's `Assets/Settings/PC_RPAsset.asset` and `PC_Renderer.asset` (read only): HDR on, post-processing enabled.
2. **Switch the pipeline:** set `GraphicsSettings.defaultRenderPipeline` and every quality level's render pipeline asset to the new URP asset. Optionally trim the quality levels down to match collage game's single "PC" tier.
3. **Convert materials** (HDRP/Lit → `Universal Render Pipeline/Lit`) with an editor script:
   - `_BaseColorMap` → `_BaseMap`, `_BaseColor` → `_BaseColor`
   - `_NormalMap` → `_BumpMap` (+ `_NORMALMAP` keyword), `_NormalScale` → `_BumpScale`
   - `_MaskMap` → both `_MetallicGlossMap` (R = metallic, A = smoothness) and `_OcclusionMap` (G = AO), with the `_METALLICSPECGLOSSMAP` and `_OCCLUSIONMAP` keywords. Set `_Smoothness` from `_SmoothnessRemapMax` when a mask map is present, otherwise from `_Smoothness`. Set `_Metallic` from `_Metallic`.
   - `_EmissiveColorMap` → `_EmissionMap`, `_EmissiveColor` → `_EmissionColor` (+ `_EMISSION` keyword). HDRP emission is in physical units, so scale it down (start by dividing by about 2^exposure from the volume) and check it visually.
   - Surface/alpha: `_SurfaceType` 1 → transparent, `_AlphaCutoffEnable` → `_AlphaClip`/`_Cutoff`, `_DoubleSidedEnable` → `_Cull` 0.
   - The 3DSMax PhysicalMaterial goes to URP/Lit as well. The glTF Shader Graph materials can be left alone, since glTFast supports URP.
4. **Lights:** remove `HDAdditionalLightData` and add `UniversalAdditionalLightData`. HDRP intensities (about 300) are physical units and are far too bright in URP. Rescale them: for reference, read the light intensities in collage game's TutorialScene (read only) and aim for a similar range, roughly 1–5 for spots. Keep colour, range and angle.
5. **Camera:** remove `HDAdditionalCameraData`, add `UniversalAdditionalCameraData` and turn on `renderPostProcessing`.
6. **Volume:** create a URP `VolumeProfile` with Tonemapping (ACES), Bloom, Motion Blur, plus Color Adjustments to stand in for HDRP Exposure. URP has no SSAO volume override: add the Screen Space Ambient Occlusion renderer feature instead. HDRP volumetric fog goes to `RenderSettings` fog (dark blue-grey, exponential). Assign the new profile to the scene's Volume.
7. **Skybox:** check that the cubemap skybox material uses `Skybox/Cubemap`, which works in URP.
8. **HDRP-only scripts:** `HDRPLook.cs`, `Editor/HDRPSetup.cs` and any `using UnityEngine.Rendering.HighDefinition` code. Guard them or retire them so the project compiles once HDRP is removed. Then remove the `com.unity.render-pipelines.high-definition` package (and `com.unity.modules.nvidia` if nothing else uses it).
9. **Graphics APIs:** add Direct3D12 after D3D11 for StandaloneWindows64, to match collage game.
10. **Verify:** check the console for errors, look for pink materials (`find_gameobjects` plus a renderer shader check), take a screenshot through `manage_camera`, and save the scene.
11. **Report back** to the user with a before/after screenshot and anything that still looks off.

## Later, when merging into collage game (not part of this task)

collage game already holds partial copies of HollowTower scripts under `Assets/Levels/HollowTower` (InteractiveDoor, LevelLayout, RemasteredLightOptimizer, RemasteredMapHUD, TowerElevator, TowerPitHazard), all in namespace `HollowTower`. Keep one copy of each file to avoid duplicate-type errors. The HollowTower player controller should also be swapped for collage game's FirstPersonController/multi-player setup.
