# Hollow Tower

A first-person stealth horror shooter for Unity. You wake on the 4th floor of a tower that fell
to the dead. Zombies and ghosts own every floor. Find the 3 fuses, power the walled-up
service elevator on the ground floor, and escape.

Everything (the building, lighting, enemies, the MP5, sounds) is generated from C# when you
press Play, so there are no art assets to import.

## Open and play

1. Open **Unity Hub**.
2. **Add > Add project from disk**, and pick this `HollowTower` folder.
3. If Hub asks which editor version to use, pick the Unity version you have installed
   (2022.3 LTS or Unity 6 both work). Click **Open** / **Continue** if it warns about upgrading.
4. The first import takes a few minutes. When it finishes, the scene
   `Assets/HollowTower/Scenes/HollowTower.unity` is created and opened for you.
   (If it isn't, use the menu **Hollow Tower > Create or Reset Game Scene**.)
5. Press **Play** (or menu **Hollow Tower > Play**). Click inside the Game view to capture the mouse.
   Tip: in the Game view toolbar choose **Play Maximized** for the full-screen experience.

## Controls

| Key | Action |
| --- | --- |
| WASD / mouse | Move / look |
| Shift | Sprint (loud, drains stamina) |
| C (toggle) or Ctrl (hold) | Crouch: quieter and much harder to see |
| Space | Jump |
| Left click | Fire the MP5 (full auto, very loud) |
| Right click | Aim down sights |
| R | Reload |
| F | Flashlight (makes you more visible, but burns ghosts) |
| E | Hide in / leave a locker |
| H | Hint: tells you what to do next and sends a guiding light along the route |
| Esc | Pause and show controls |

## How to survive

- **Zombies see and hear.** The bar at the top of the screen is how visible you are: standing under
  lamps, sprinting, and the flashlight push it up; darkness and crouching push it down.
- **Every gunshot is heard about 30 metres away**, and picking up a fuse makes a loud spark.
- **Lockers hide you completely**, unless an enemy watched you climb in. Then it will tear the door open.
- **Ghosts drift through walls.** Shoot one (about 5 hits) or hold the flashlight beam on it
  for about a second and a half to drive it away (it comes back later), or hide.
- **Brutes** (big, red eyes) take a whole magazine. Avoid them.
- When the last fuse is in, the building wakes up. Relentless ghosts come for you and a pack
  gathers on the ground floor. Run for the north-east corner of the ground floor.
- Wall writing in red is a clue. Press **H** whenever you're stuck.
- **Checkpoints:** reaching floor 3 and floor 2 saves your progress. If you die, press Enter to carry on
  from the last checkpoint (N starts a new game).

## Making it yours

- Floor plans are the ASCII maps at the top of `Assets/HollowTower/Scripts/LevelBuilder.cs`.
  Change letters to move zombies (`Z` `R` `B`), ghosts (`G`), lockers (`L`), lamps (`l`), fuses (`F`).
- Enemy stats (speed, health, sight range, hearing) are in `Zombie.cs` (`For(EnemyKind k)`).
- Darkness and fog are on the **Hollow Tower (press Play)** object in the scene (`GameBootstrap`).

## Graphics and ray tracing

The game runs on Unity's built-in render pipeline so that it opens and plays on any Unity
version and any GPU. It uses deferred lighting, soft real-time shadows from the flashlight and
lamps, HDR, fog, and a custom film grade (tonemapping, cold shadows, vignette, grain, damage
effects).

Real-time **ray tracing needs HDRP** plus a ray-tracing GPU (NVIDIA RTX 20-series or newer, or
AMD RX 6000 or newer) and DirectX 12. To upgrade:

1. Menu **Hollow Tower > Graphics > Install HDRP**.
2. **Window > Rendering > HDRP Wizard**: press **Fix All** on the HDRP tab, then on the
   **HDRP + DXR** tab press **Fix All** again. Unity switches to DirectX 12 and restarts.
3. Add a **Volume** to the scene with Ray-Traced Shadows, Reflections and Global Illumination overrides.

Because this project builds its materials and lights in code for the built-in pipeline, after the
HDRP switch lights and materials need re-tuning (HDRP uses physical light units). That step is
best done together with Claude once the base game runs on your laptop.

## Troubleshooting

- **"Enter Safe Mode?" on first open:** choose **Ignore**. A helper script then fixes the input
  setting and asks to restart Unity once.
- **Pink/magenta objects:** the project was switched to URP or HDRP. Use
  **Edit > Project Settings > Graphics** and clear the render pipeline asset to go back to built-in.
- **Mouse doesn't turn the camera:** click inside the Game view first.
- **Too dark / too bright:** select **Hollow Tower (press Play)** in the Hierarchy and change
  `Ambient` and `Fog Density`.
