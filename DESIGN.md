# Hollow Tower: design notes

## Pitch
A single-player, first-person stealth horror shooter. One survivor, one MP5, one tower full of the
dead. Fighting is possible but loud; hiding is how you live.

## Story
Night 41 of the outbreak. The Halvorsen Tower was the city's last safe zone, held by soldiers floor
by floor, and it fell. You wake on the 4th floor among the bodies. The street doors are welded shut.
Old maintenance logs mention a service elevator on the ground floor that was walled over years ago
and still runs on its own power line. Its 3 fuses were pulled and scattered during the fighting.

## Goal and flow
1. Start on floor 4. Each of floors 4, 3 and 2 hides one glowing fuse (any order).
2. Stairs alternate sides (north-west shaft for 1-2 and 3-4, south-east shaft for 2-3), so every
   floor must be crossed.
3. Grabbing a fuse sparks loudly and draws nearby enemies.
4. With all 3 fuses, the fake wall in the ground floor's north-east corner grinds into the floor and
   the lift powers up. The building "wakes": 3 relentless ghosts spawn on your floor and a brute,
   two runners and a walker gather on the ground floor.
5. Step into the lift. Doors close. You escape through the old service tunnels.

## Enemies
| Enemy | Speed (chase) | Health | Notes |
| --- | --- | --- | --- |
| Walker | 3.1 m/s | 100 | Common; 16 m sight; slower than your walk when wandering |
| Runner | 6.0 m/s | 65 | As fast as your sprint, wide vision, gives up slowly |
| Brute | 4.3 m/s | 700 | 40 damage hits, hears 1.8x further, red glowing eyes |
| Ghost | 3.7 m/s (4.3 relentless) | n/a | Passes through walls, senses you within ~9 m without sight, 110 HP (about 5 hits), burned away by 1.4 s of flashlight |

## Stealth rules
- Visibility = light at your position (real lamps, line-of-sight checked) x stance (crouch 0.55x,
  sprint 1.25x); flashlight on sets a floor of 1.05; firing sets 1.4 for 1.5 s.
- Zombie sight range = base sight x your visibility, within its field of view, with line of sight.
  Within 2.2 m they notice you regardless.
- Awareness fills while seen: Suspicious (stops, stares, creeps closer) then Chase. Lost targets are
  searched for, then forgotten.
- Noise: footsteps 5 m (sprint 14 m, crouch 1.2 m), landing 8 m, gunshot 30 m, fuse 24 m. Each floor
  of concrete halves the reach.
- Lockers make you invisible, unless an enemy was watching when you got in; zombies will rip you out.

## Weapon: MP5
800 rpm, 30-round magazine, 150 spare (ammo boxes add 60, dead zombies sometimes drop 20), 22 damage (x2.5 headshot), 2.1 s reload,
tight spread when aiming, recoil you have to pull down.

## Hint system
- Press H (12 s cooldown): context aware. If you're spotted it tells you how to break contact and
  points to the nearest locker; if a ghost hunts you it explains the flashlight; otherwise it gives
  the next objective, distance and compass direction.
- A guiding light then floats along the real walking route (NavMesh path), waiting if you fall behind.
- One-time tips appear the first time you meet a locker, ghost, brute, or stand in bright light.
- Red writing on walls on each floor hints at the rules and the hidden elevator.

## Technical choices (Claude's calls)
- Built-in render pipeline so it opens on any Unity version; HDRP + ray tracing is a documented upgrade.
- All geometry, materials, textures (procedural grime), sounds (synthesized) and the NavMesh are
  generated at runtime from ASCII floor plans. No imported assets, nothing to break on import.
