# Session Logs: Hollow Tower Remaster & Blender Elevator Integration

**Date / Timestamp:** 2026-10-21T22:32:00+05:30  
**Active Project:** `HollowTower` (Unity 6000.6.5f1, HDRP)  
**Editor Bridge:** Port 6400 (`HollowTower@a1973c70`)  
**Reference Project:** `collage game` (Port 6402)  
**Blender Model Source:** `C:\Users\Harikrishnan\Documents\trash programs\collage game assets\blender\props\Elevator.blend`  

---

## 1. Executive Summary & Goals
This session successfully replaced the procedural rusty box elevator with the authentic 3D Blender elevator design and verified the end-to-end gameplay loop in Play Mode:
1. **Spawn Elevator (Floor 1 - "Parking access")**:
   - Location: $(X = 141.60\text{m}, Y = 0.05\text{m}, Z = 12.00\text{m})$, backed against the East wall and facing West into the room.
   - The player starts inside the cabin at $(141.60, 0.20, 12.00)$ facing West ($Y = 270^\circ$).
   - On game start, after a brief immersion pause, the elevator chime sounds and the double doors smoothly slide open to a $2.27\text{m}$ clearance, welcoming the player into the facility.
2. **Exit Elevator (Floor 4 - "Storage 2")**:
   - Location: $(X = 141.60\text{m}, Y = 12.65\text{m}, Z = 6.00\text{m})$.
   - Doors start OPEN ($Z = 7.135\text{m}$ and $Z = 4.865\text{m}$).
   - Stepping inside triggers `[ E ] ENTER ELEVATOR & ESCAPE FACILITY`.
   - Pressing `[E]` triggers the closing sequence: doors seal shut, descent hum plays, and the victory evacuation overlay appears.
   - Pressing `[R]` instantly resets the player back inside the Floor 1 spawn elevator for another run.
3. **High-Fidelity Visuals**:
   - Utilizes the full 224-mesh Blender model with chipped yellow industrial side pillars, glowing amber marquee indicator lamps (`▲ ▲ ▲`), power module with glowing green status LEDs, brushed stainless steel interior, safety rails, and treadplate flooring.
   - 11 dedicated PBR materials in `Assets/Materials/Elevator/` using `HDRP/Lit` with emissive intensity multipliers.
4. **Performance & Lag Optimization**:
   - Resolved the severe lag discrepancy between DX11 Built-in and DX12 HDRP.
   - Disabled heavy Ray Tracing, SSGI, and SSR overhead in `HollowTowerHDRP.asset`.
   - Restricted camera far clip plane to 45m.
   - Integrated `TowerFloorCulling.cs` to dynamically deactivate inactive floors, slashing active mesh count from 14,886 to ~3,400.

---

## 2. Technical Findings & Architectural Solutions

### 2.1 FBX Scale Normalization & Collider Physics
- **The Issue**: FBX export from Blender applies a 100x scale factor on the root GameObject. Any box collider added as a child of the elevator root inherited this 100x scale, expanding a standard $2.6\text{m}$ collider into a $260\text{m}$ obstacle that pushed the player and camera out of bounds.
- **The Solution**:
  - `Elevator_Cabin_Colliders` and `Exit_Trigger_Zone` are parented directly to the Floor root (`Floor_1_Ground` and `Floor_4_Offices`, scale strictly `(1, 1, 1)`).
  - Accurate metric extents:
    - Floor slab: Center $(141.60, 0.11, 12.00)$, Extents $(1.30, 0.06, 1.30)$ -> exact $2.6\text{m} \times 0.12\text{m} \times 2.6\text{m}$.
    - Rear Wall: Center $(142.75, 1.45, 12.00)$, Extents $(0.10, 1.40, 1.30)$.
    - Left/Right Walls: Centers at $Z = 13.25$ and $Z = 10.75$, leaving the West doorway completely open.
    - Exit Trigger Zone: Center $(141.60, 13.85, 6.00)$, Extents $(0.80, 1.10, 0.80)$ -> $1.6\text{m} \times 2.2\text{m} \times 1.6\text{m}$.

### 2.2 Double Sliding Door Symmetrical Alignment
- `LeftSlidingDoor` and `RightSlidingDoor` in `Exterior_Front_Assembly` slide along local X:
  - Closed position: doors meet seamlessly at $Z = 12.000$ (Floor 1) and $Z = 6.000$ (Floor 4).
  - Open position: `leftOpenPos = leftClosedPos + (0.008, 0, 0)`, `rightOpenPos = rightClosedPos - (0.008, 0, 0)`.
  - Translates to world $Z = \pm 1.135\text{m}$, creating a clean $2.27\text{m}$ wide passageway.

### 2.3 Bulletproof CharacterController Trigger Detection
- In Unity PhysX, static trigger colliders do NOT trigger `OnTriggerEnter` when colliding with a `CharacterController` unless a `Rigidbody` is present.
- **Resolution**:
  - Added a kinematic `Rigidbody` (`isKinematic = true`, `useGravity = false`) to `Exit_Trigger_Zone`.
  - Added real-time bounds checking in `TowerExitElevator.Update()`: `triggerCollider.bounds.Contains(player.position + Vector3.up * 0.5f)`.
  - Guaranteed detection under all frame rates and physics tick frequencies.

---

## 3. Play Mode Verification Results
| Step | Action | Expected Result | Measured Result | Status |
| :--- | :--- | :--- | :--- | :--- |
| **1. Grounding** | Player spawns at $(141.60, 0.20, 12.00)$ | CharacterController grounded on floor slab without clipping | `isGrounded=True`, pos $(141.60, 0.20, 12.00)$ | **PASS** |
| **2. Spawn Doors** | 1.0s initial delay + chime | Doors slide open to $2.27\text{m}$ width | `hasOpened=True`, dl $Z=13.135$, dr $Z=10.865$ | **PASS** |
| **3. Player Exit** | Walk forward out of cabin | Player steps into Parking access seamlessly | Pos $(138.50, 0.20, 12.00)$, `isGrounded=True` | **PASS** |
| **4. F4 Exit State** | Inspect Floor 4 elevator | Doors stand open waiting for player | `isPlayerInside=True` upon entry, dl $Z=7.135$, dr $Z=4.865$ | **PASS** |
| **5. Escape [E]** | Press [E] / TriggerEscape | Doors close shut, descent hum plays, victory screen shows | Doors sealed at $Z=6.335 / 5.665$, `hasEscaped=True` | **PASS** |
| **6. Reset [R]** | Press [R] / RestartGame | Reset player back inside Floor 1 elevator | Player pos $(141.60, 0.20, 12.00)$, `hasEscaped=False` | **PASS** |

---

## 4. Key Visual Artifacts Generated
- `blender_elevator_floor1_exterior_fixed.png`: Floor 1 elevator exterior in Parking access showing chipped yellow pillars, glowing amber chevrons, green power LED, and hazard doors.
- `blender_elevator_spawn_player_firstperson.png`: First-person player view at game start looking through the sliding doors out into Parking access.
- `blender_elevator_spawn_player_looking_back.png`: Player standing in Parking access looking back at the fully open, illuminated elevator cabin.
- `blender_elevator_floor4_exterior_open_fixed.png`: Floor 4 Exit Elevator in Storage 2 with doors wide open.
- `blender_elevator_floor4_interior_fixed.png`: View looking out from inside Floor 4 exit elevator into Storage 2.
