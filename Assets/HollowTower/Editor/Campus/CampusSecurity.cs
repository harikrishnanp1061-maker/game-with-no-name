using System.Collections.Generic;
using HollowTower.Campus;
using Unity.Netcode;
using Unity.Netcode.Components;
using UnityEditor;
using UnityEngine;
using UnityEngine.AI;
using M = HollowTower.EditorTools.CampusMats;

namespace HollowTower.EditorTools
{
    /// <summary>
    /// The campus's security layer, built after the administration building and the grounds:
    ///   - the security control room (admin upper floor, west): keypad door whose code is hidden in the finance office,
    ///     the surveillance console, wall monitors, the campus map, a charging rack of four walkie-talkies, and a P1
    ///     false wall out the back for when something comes in the front;
    ///   - the CCTV network (twelve cameras inside on the battery, nine outside on the generator), the phones it can ring
    ///     (office phones inside, payphones outside), the light banks it can throw on (the building's wings, and new
    ///     floodlights on the grounds);
    ///   - P1 false walls in the building (records/offices, boardroom/secretary): loops to lose a pursuer;
    ///   - the navmesh, the Nailed (one inside, one roaming the grounds), the radio network and the narration hooks.
    /// </summary>
    public static class CampusSecurity
    {
        const float H = CampusAdmin.H, CeilU = 8.25f, CeilG = 3.85f;
        public static SurveillanceConsole Console;
        public static Transform ControlRoom;
        static Transform root, lights;
        static System.Random rnd;

        static float R(float a, float b) { return a + (float)rnd.NextDouble() * (b - a); }
        static GameObject Box(string n, Transform p, Vector3 world, Vector3 size, Material m, bool col = true)
        {
            return CampusKit.Box(n, p, p.InverseTransformPoint(world), size, m, col);
        }
        /// <summary>A screen face (a quad: its picture fills it 0..1) at a world point, facing +x (into the control room).</summary>
        static GameObject Screen(string n, Transform p, Vector3 world, float width, float height, Material m, bool collider)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Quad); go.name = n;
            if (!collider) Object.DestroyImmediate(go.GetComponent<Collider>());
            go.transform.SetParent(p, false); go.transform.position = world; go.transform.rotation = Quaternion.Euler(0, -90, 0);
            go.transform.localScale = new Vector3(width, height, 1f);
            go.GetComponent<Renderer>().sharedMaterial = m;
            go.GetComponent<Renderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            return go;
        }

        static GameObject BoxR(string n, Transform p, Vector3 world, Vector3 size, Quaternion rot, Material m, bool col = true)
        {
            return CampusKit.Box(n, p, p.InverseTransformPoint(world), size, rot, m, col);
        }

        public static void Build(Transform env)
        {
            rnd = new System.Random(4417);
            root = new GameObject("Campus_Security").transform; root.SetParent(env, false);
            lights = new GameObject("Security_Lights").transform; lights.SetParent(root, false);
            ControlRoomBuild();
            FalseWalls();
            Cameras();
            Phones();
            LightZones();
            Floodlights();
            Sinkholes();
        }

        // ------------------------------------------------------------------ the control room
        public static TextMesh ShiftBoard;

        static void ControlRoomBuild()
        {
            var room = new GameObject("Security_Control_Room").transform; room.SetParent(root, false);
            ControlRoom = room;
            var walls = CampusAdmin.Rooms;
            CampusKit.WallX(walls, "Wall_ControlEast", -25, -24, -13, H, CeilU, 0.25f, M.PlasterCool);
            CampusKit.WallZ(walls, "Wall_ControlNorth", -13, -36, -25, H, CeilU, 0.25f, M.PlasterCool, new CampusKit.Hole(-30.6f, -29.0f, H, H + 2.3f));

            // the door: steel, a card reader - the security keycard is in the head administrator's office
            var hd = new GameObject("Control_Room_Door"); hd.transform.SetParent(room, false);
            hd.transform.position = new Vector3(-30.6f, H, -13f);
            var leaf = new GameObject("Leaf").transform; leaf.SetParent(hd.transform, false);
            CampusKit.Box("Door_Leaf", leaf, new Vector3(0.8f, 1.15f, 0), new Vector3(1.56f, 2.28f, 0.06f), M.Metal);
            CampusKit.Box("Door_Handle", leaf, new Vector3(1.4f, 1.0f, 0.06f), new Vector3(0.12f, 0.04f, 0.12f), M.MetalLight, false);
            CampusKit.Box("Door_Window", leaf, new Vector3(0.8f, 1.6f, 0.0f), new Vector3(0.3f, 0.4f, 0.07f), M.GlassDark, false);
            CampusKit.Box("Card_Reader", hd.transform, new Vector3(1.85f, 1.25f, 0.15f), new Vector3(0.1f, 0.16f, 0.03f), M.Black, false);
            CampusKit.Box("Card_Reader_LED", hd.transform, new Vector3(1.85f, 1.31f, 0.17f), new Vector3(0.03f, 0.02f, 0.01f), M.GlowRed, false);
            CampusKit.Text(hd.transform, "SECURITY CONTROL", new Vector3(0.8f, 2.55f, 0.16f), Vector3.forward, 0.14f, new Color(0.75f, 0.72f, 0.6f));
            CampusKit.Text(hd.transform, "AUTHORISED STAFF ONLY - CARD ACCESS", new Vector3(0.8f, 2.38f, 0.16f), Vector3.forward, 0.07f, new Color(0.7f, 0.3f, 0.25f));
            var door = hd.AddComponent<CampusKeyDoor>();
            door.doorName = "security control room"; door.requiredItem = "security_keycard";
            door.lockedText = "Card reader - it needs a security keycard (the head administrator keeps one)";
            door.leaves = new[] { leaf }; door.openAngles = new[] { 95f };
            var dc = hd.AddComponent<BoxCollider>(); dc.center = new Vector3(0.8f, 1.15f, 0); dc.size = new Vector3(1.6f, 2.3f, 0.3f);
            var obst = hd.AddComponent<NavMeshObstacle>(); obst.carving = true; obst.center = dc.center; obst.size = dc.size;

            var console = new GameObject("Surveillance_Console"); console.transform.SetParent(room, false);
            console.transform.position = new Vector3(-31.4f, H + 1.0f, -18.5f);
            var cc = console.AddComponent<BoxCollider>(); cc.size = new Vector3(1.0f, 0.5f, 4.0f);
            Console = console.AddComponent<SurveillanceConsole>();
            Console.adminRoot = CampusAdmin.Root;

            // the video wall: the whole west wall, six monitors across and three high, the live map in the middle
            const float wx = -35.6f, z0 = -23.5f, z1 = -13.5f, y0 = H + 0.55f, y1 = H + 3.85f;
            float cw = (z1 - z0) / 6f, rh = (y1 - y0) / 3f;
            Box("VideoWall_Frame", room, new Vector3(wx - 0.08f, (y0 + y1) * 0.5f, (z0 + z1) * 0.5f), new Vector3(0.12f, y1 - y0 + 0.2f, z1 - z0 + 0.2f), M.Black, false);
            var screens = new List<Renderer>(); var labels = new List<TextMesh>();
            var screenMat = CampusKit.Flat("CM_ScreenOff", new Color(0.02f, 0.04f, 0.03f), 0.6f);
            for (int row = 0; row < 3; row++)
                for (int col = 0; col < 6; col++)
                {
                    if ((col == 2 || col == 3) && row < 2) continue;   // the map takes the middle
                    float zc = z0 + cw * (col + 0.5f), yc = y1 - rh * (row + 0.5f);
                    Box("Monitor_Bezel", room, new Vector3(wx, yc, zc), new Vector3(0.1f, rh - 0.05f, cw - 0.05f), M.Black, false);
                    var scr = Screen("Monitor_Screen", room, new Vector3(wx + 0.056f, yc + 0.03f, zc), cw - 0.16f, rh - 0.22f, screenMat, true);
                    screens.Add(scr.GetComponent<Renderer>());
                    var lt = CampusKit.Text(room, "CAM", new Vector3(wx + 0.07f, yc - rh * 0.5f + 0.08f, zc), Vector3.right, 0.05f, new Color(0.55f, 1f, 0.65f));
                    labels.Add(lt.GetComponent<TextMesh>());
                }
            float mz = z0 + cw * 3f, my = y1 - rh;
            Box("Map_Bezel", room, new Vector3(wx, my, mz), new Vector3(0.1f, rh * 2f - 0.05f, cw * 2f - 0.05f), M.Black, false);
            var map = Screen("Map_Screen", room, new Vector3(wx + 0.056f, my, mz), cw * 2f - 0.16f, rh * 2f - 0.2f, screenMat, true);
            Console.mapScreen = map.GetComponent<Renderer>();
            Console.mapSize = new Vector2(cw * 2f - 0.16f, rh * 2f - 0.2f);
            CampusKit.Text(room, "SITE SURVEILLANCE", new Vector3(wx + 0.07f, y1 + 0.12f, mz), Vector3.right, 0.1f, new Color(0.55f, 1f, 0.65f));
            Console.wallScreens = screens.ToArray(); Console.wallLabels = labels.ToArray();
            var glow = CampusKit.Lamp(lights, new Vector3(-32.6f, H + 1.2f, -18.5f), new Color(0.5f, 0.85f, 0.7f), 7f, 0.7f, null, 0.04f);
            glow.important = true;

            // the operator's desk facing the wall: two steel desks end to end, three monitors, the base-station radio
            CampusKit.Prop("metal_office_desk", room, new Vector3(-31.4f, H, -19.6f), 90);
            CampusKit.Prop("metal_office_desk", room, new Vector3(-31.4f, H, -17.4f), 90);
            CampusKit.Prop("chair_office", room, new Vector3(-29.5f, H, -18.5f), -90);
            var desk = new List<Renderer>();
            foreach (float z in new[] { -19.8f, -18.5f, -17.2f })
            {
                Box("Desk_Monitor", room, new Vector3(-31.65f, H + 1.07f, z), new Vector3(0.3f, 0.44f, 0.62f), M.Black, false);
                desk.Add(Screen("Desk_Screen", room, new Vector3(-31.495f, H + 1.09f, z), 0.52f, 0.34f, screenMat, false).GetComponent<Renderer>());
            }
            Console.deskScreens = desk.ToArray();
            Box("Keyboard", room, new Vector3(-31.1f, H + 0.81f, -18.5f), new Vector3(0.18f, 0.03f, 0.5f), M.Black, false);
            CampusKit.Prop("vintage_radio_transceiver", room, new Vector3(-31.5f, H + 0.79f, -16.6f), 100);
            CampusKit.Prop("clipboard", room, new Vector3(-31.2f, H + 0.79f, -20.2f), 20);
            Box("Mug", room, new Vector3(-31.0f, H + 0.84f, -17.6f), new Vector3(0.08f, 0.1f, 0.08f), M.Plaster, false);
            var vp = new GameObject("Console_ViewPoint").transform; vp.SetParent(room, false);
            vp.position = new Vector3(-30.35f, H + 1.6f, -18.5f);
            vp.rotation = Quaternion.LookRotation(new Vector3(wx, H + 2.2f, -18.5f) - vp.position);
            Console.viewPoint = vp;
            Console.room = new Bounds(new Vector3(-30.5f, H + 2f, -18.5f), new Vector3(11f, 4.5f, 11f));

            // the guards' things: racks, lockers, the shift board (its change-over time sets the clock tower)
            CampusKit.Prop("ServerRack_0", room, new Vector3(-26.4f, H, -23.4f), 0);
            CampusKit.Prop("ServerRack_1", room, new Vector3(-27.5f, H, -23.4f), 0);
            CampusKit.Prop("Lockers_4_0_1", room, new Vector3(-25.4f, H, -20.5f), -90);
            CampusKit.Prop("papers_scatter", room, new Vector3(-28f, H + 0.01f, -21f), 30);
            CampusKit.Prop("Blackboard_4.5_0", room, new Vector3(-31.2f, H + 0.9f, -23.82f), 0, 0.7f);
            CampusKit.Text(room, "NIGHT SHIFT  31/10", new Vector3(-31.2f, H + 2.0f, -23.78f), Vector3.forward, 0.12f, new Color(0.85f, 0.85f, 0.8f));
            ShiftBoard = CampusKit.Text(room, "SHIFT CHANGE  --:--", new Vector3(-31.2f, H + 1.75f, -23.78f), Vector3.forward, 0.1f, new Color(1f, 0.95f, 0.6f)).GetComponent<TextMesh>();
            CampusKit.Text(room, "PATROL 0300 - NO ANSWER", new Vector3(-31.2f, H + 1.52f, -23.78f), Vector3.forward, 0.07f, new Color(0.85f, 0.85f, 0.8f));
            CampusKit.Text(room, "DO NOT OPEN SERVICE GATE", new Vector3(-31.2f, H + 1.36f, -23.78f), Vector3.forward, 0.07f, new Color(0.9f, 0.45f, 0.4f));
            Box("Strip_Housing", lights, new Vector3(-28.5f, CeilU + 0.03f, -18.5f), new Vector3(1.3f, 0.08f, 0.3f), M.MetalLight, false);
            var tube = Box("Strip_Tube", lights, new Vector3(-28.5f, CeilU - 0.03f, -18.5f), new Vector3(1.2f, 0.04f, 0.18f), M.GlowCold, false);
            CampusKit.Lamp(lights, new Vector3(-28.5f, CeilU - 1f, -18.5f), new Color(0.78f, 0.86f, 1f), 10f, 1.4f, new[] { tube.GetComponent<Renderer>() }, 0.08f).important = true;

            // the walkie-talkies, standing in their chargers on a wall shelf beside the door
            Box("Charger_Shelf", room, new Vector3(-27.0f, H + 1.05f, -13.3f), new Vector3(1.5f, 0.04f, 0.26f), M.Metal, false);
            CampusKit.Text(room, "RADIOS - RETURN AFTER USE", new Vector3(-27.0f, H + 1.55f, -13.15f), Vector3.back, 0.07f, new Color(0.8f, 0.75f, 0.5f));
            for (int i = 0; i < 4; i++) Walkie(room, new Vector3(-27.55f + i * 0.36f, H + 1.07f, -13.32f));
        }

        static void Walkie(Transform parent, Vector3 at)
        {
            Box("Charger_Base", parent, at + new Vector3(0, 0.02f, 0), new Vector3(0.11f, 0.04f, 0.09f), M.Black, false);
            var w = new GameObject("Walkie_Talkie"); w.transform.SetParent(parent, false);
            w.transform.position = at + new Vector3(0, 0.04f, 0);
            w.transform.rotation = Quaternion.Euler(0, 180, 0);
            CampusKit.Box("Body", w.transform, new Vector3(0, 0.09f, 0), new Vector3(0.065f, 0.17f, 0.035f), M.Black, false);
            CampusKit.Box("Grille", w.transform, new Vector3(0, 0.07f, -0.019f), new Vector3(0.05f, 0.06f, 0.004f), M.Dark, false);
            CampusKit.Box("Screen", w.transform, new Vector3(0, 0.135f, -0.019f), new Vector3(0.04f, 0.02f, 0.004f), M.GlowScreen, false);
            var ant = CampusKit.Cyl("Antenna", w.transform, new Vector3(0.018f, 0.175f, 0), 0.006f, 0.13f, M.Black, false);
            CampusKit.Cyl("Knob", w.transform, new Vector3(-0.016f, 0.175f, 0), 0.008f, 0.02f, M.MetalLight, false);
            var led = CampusKit.Box("LED", w.transform, new Vector3(0.022f, 0.165f, -0.019f), new Vector3(0.008f, 0.008f, 0.004f), M.GlowGreen, false);
            var col = w.AddComponent<BoxCollider>(); col.center = new Vector3(0, 0.12f, 0); col.size = new Vector3(0.22f, 0.3f, 0.2f);
            var item = w.AddComponent<PartyItem>();
            item.itemId = HollowTower.Campus.Walkie.ItemId; item.displayName = "Walkie-talkie"; item.color = new Color(0.4f, 1f, 0.55f);
            item.needsTorch = false;
            item.glowRenderers = new[] { led.GetComponent<Renderer>() };
            item.glowIntensity = 1.2f;
        }

        // ------------------------------------------------------------------ P1 false walls in the building
        static void FalseWalls()
        {
            FalseWall.BoxFn box = (n, p, c, s, m, col) => Box(n, p, c, s, m, col);
            FalseWall.Make(root, "FalseWall_RecordsOffices", new Vector3(-32.5f, 0, 2f), true, 0.25f, 1.8f, 2.5f, M.Plaster, M.Metal, box);
            FalseWall.Make(root, "FalseWall_BoardroomSuite", new Vector3(18f, H, 4f), true, 0.25f, 1.8f, 2.5f, M.WoodPanel, M.Metal, box);
        }

        // ------------------------------------------------------------------ CCTV
        struct CamDef { public string label; public bool outdoor; public Vector3 pos, look; public float range; public bool pole; }
        static readonly CamDef[] CamDefs =
        {
            new CamDef { label = "Vestibule", pos = new Vector3(-12.6f, 3.55f, -23.4f), look = new Vector3(6, 0.4f, -14), range = 28 },
            new CamDef { label = "Atrium", pos = new Vector3(-13.4f, 7.6f, 23.2f), look = new Vector3(3, 0, -2), range = 40 },
            new CamDef { label = "Open Offices", pos = new Vector3(-35.4f, 3.5f, 23.4f), look = new Vector3(-19, 0.4f, 5), range = 30 },
            new CamDef { label = "Records", pos = new Vector3(-14.6f, 3.5f, -23.4f), look = new Vector3(-30, 0.4f, -5), range = 30 },
            new CamDef { label = "Seminar Hall", pos = new Vector3(14.6f, 3.5f, 23.4f), look = new Vector3(32, 0.4f, 3), range = 30 },
            new CamDef { label = "Staff Lounge", pos = new Vector3(35.4f, 3.5f, -23.4f), look = new Vector3(18, 0.4f, -5), range = 28 },
            new CamDef { label = "Upper Gallery", pos = new Vector3(13.4f, 7.9f, -23.4f), look = new Vector3(-9, 4.4f, 8), range = 36 },
            new CamDef { label = "Admissions", pos = new Vector3(-35.4f, 7.9f, 23.4f), look = new Vector3(-18, 4.4f, 2), range = 30 },
            new CamDef { label = "Finance", pos = new Vector3(-14.6f, 7.9f, -0.6f), look = new Vector3(-29, 4.6f, -13), range = 26 },
            new CamDef { label = "Boardroom", pos = new Vector3(35.4f, 7.9f, -23.4f), look = new Vector3(17, 4.4f, -5), range = 28 },
            new CamDef { label = "Head's Suite", pos = new Vector3(14.6f, 7.9f, 23.4f), look = new Vector3(25, 4.4f, 8), range = 26 },
            new CamDef { label = "Control Room", pos = new Vector3(-35.5f, 7.85f, -23.5f), look = new Vector3(-29.8f, 5.2f, -13), range = 16 },
            new CamDef { label = "Main Gate", outdoor = true, pos = new Vector3(14.6f, 3.4f, 228.4f), look = new Vector3(0, 1, 238), range = 50 },
            new CamDef { label = "Car Park", outdoor = true, pos = new Vector3(15.5f, 6.5f, 157f), look = new Vector3(70, 0, 196), range = 70, pole = true },
            new CamDef { label = "Quad", outdoor = true, pos = new Vector3(20f, 7.4f, 24.6f), look = new Vector3(0, 0, 80), range = 70 },
            new CamDef { label = "Library", outdoor = true, pos = new Vector3(-50f, 5.5f, 60f), look = new Vector3(-62, 1, 84), range = 50, pole = true },
            new CamDef { label = "Maintenance Yard", outdoor = true, pos = new Vector3(-146.6f, 5.2f, -22.6f), look = new Vector3(-157, 0, -36), range = 40, pole = true },
            new CamDef { label = "Sports Field", outdoor = true, pos = new Vector3(-40.5f, 8f, 160f), look = new Vector3(-90, 0, 194), range = 90, pole = true },
            new CamDef { label = "Cafe Terrace", outdoor = true, pos = new Vector3(103f, 5f, 21f), look = new Vector3(70, 0, -10), range = 55, pole = true },
            new CamDef { label = "East Road", outdoor = true, pos = new Vector3(119.5f, 6f, 30f), look = new Vector3(114, 0, 100), range = 80, pole = true },
            new CamDef { label = "Service Gate", outdoor = true, pos = new Vector3(141.6f, 2.7f, -45.6f), look = new Vector3(152, 1, -51), range = 40 },
        };

        static void Cameras()
        {
            var g = new GameObject("CCTV").transform; g.SetParent(root, false);
            foreach (var d in CamDefs)
            {
                var cam = new GameObject("CCTV_" + d.label.Replace(' ', '_').Replace("'", "")); cam.transform.SetParent(g, false);
                cam.transform.position = d.pos;
                Vector3 dir = d.look - d.pos;
                cam.transform.rotation = Quaternion.LookRotation(dir);
                var model = CampusKit.Prop("security_camera_01", cam.transform, Vector3.zero, 0, 1f, false);
                if (model != null) model.transform.localRotation = Quaternion.identity;
                if (d.pole)
                {
                    CampusKit.Cyl("CCTV_Pole", g, new Vector3(d.pos.x, 0, d.pos.z) - new Vector3(dir.x, 0, dir.z).normalized * 0.15f, 0.07f, d.pos.y, M.MetalLight);
                }
                var led = CampusKit.Box("LED", cam.transform, new Vector3(0, 0.08f, 0.12f), new Vector3(0.015f, 0.015f, 0.015f), M.GlowRed, false);
                var vgo = new GameObject("View"); vgo.transform.SetParent(cam.transform, false); vgo.transform.localPosition = new Vector3(0, 0, 0.55f);
                var view = vgo.AddComponent<Camera>();
                view.enabled = false; view.fieldOfView = d.outdoor ? 62f : 78f; view.nearClipPlane = 0.12f; view.farClipPlane = d.outdoor ? 260f : 60f;
                view.clearFlags = CameraClearFlags.Skybox;
                var data = vgo.AddComponent<UnityEngine.Rendering.Universal.UniversalAdditionalCameraData>();
                data.renderShadows = false; data.renderPostProcessing = false; data.antialiasing = UnityEngine.Rendering.Universal.AntialiasingMode.None;
                var cc = cam.AddComponent<CctvCamera>();
                cc.label = d.label; cc.outdoor = d.outdoor; cc.range = d.range; cc.view = view; cc.statusLed = led.GetComponent<Renderer>();
            }
        }

        // ------------------------------------------------------------------ phones
        static void Phones()
        {
            var g = new GameObject("Phones").transform; g.SetParent(root, false);
            DeskPhone(g, "Reception", new Vector3(0.9f, 0.96f, -2.3f), 180);
            DeskPhone(g, "Office Manager", new Vector3(-33.2f, 0.77f, 19.1f), 90);
            DeskPhone(g, "Records", new Vector3(-31.7f, 0.79f, -2.3f), 0);
            DeskPhone(g, "Staff Lounge", new Vector3(27.2f, 0.93f, -23.45f), 0);
            WallPhone(g, "Seminar Hall", new Vector3(35.78f, 1.45f, 2.4f), -90);
            DeskPhone(g, "Admissions", new Vector3(-32.3f, H + 0.77f, 8.2f), 180);
            DeskPhone(g, "Boardroom", new Vector3(24.2f, H + 0.79f, -10.2f), 90);
            DeskPhone(g, "Head's Secretary", new Vector3(18.4f, H + 0.77f, 10.3f), 180);
            DeskPhone(g, "Finance", new Vector3(-24.6f, H + 0.77f, -8.7f), 180);
            Payphone(g, "Bus Stop", new Vector3(-42.2f, 0.03f, 154.3f), 180);
            Payphone(g, "Library Porch", new Vector3(-53.6f, 0.03f, 100.5f), 90);
            Payphone(g, "Cafe Kiosk", new Vector3(85.4f, 0.04f, -6.6f), -90);
            Payphone(g, "Car Park", new Vector3(23.2f, 0.03f, 227.4f), 180);
            Payphone(g, "Sports Field", new Vector3(-93.2f, 0.03f, 157.4f), 0);
            Payphone(g, "Student Centre", new Vector3(130.6f, 0.03f, 6.2f), 90);
            Payphone(g, "Service Lane", new Vector3(138.5f, 0.03f, -43.2f), 180);
        }

        static CampusPhone PhoneComponent(GameObject go, string label, bool outdoor)
        {
            var p = go.AddComponent<CampusPhone>();
            p.label = label; p.outdoor = outdoor; if (!outdoor) p.nailedRange = 40f;
            var l = new GameObject("Bell_Light").AddComponent<Light>(); l.transform.SetParent(go.transform, false);
            l.transform.localPosition = new Vector3(0, 0.3f, 0); l.type = LightType.Point; l.color = new Color(1f, 0.3f, 0.2f);
            l.range = 2.5f; l.intensity = 2f * CampusKit.LampGain * 0.3f; l.enabled = false; l.shadows = LightShadows.None;
            p.bellLight = l;
            return p;
        }

        static void DeskPhone(Transform g, string label, Vector3 at, float yaw)
        {
            var ph = new GameObject("Phone_" + label.Replace(' ', '_').Replace("'", "")); ph.transform.SetParent(g, false);
            ph.transform.position = at; ph.transform.rotation = Quaternion.Euler(0, yaw, 0);
            CampusKit.Box("Base", ph.transform, new Vector3(0, 0.03f, 0), new Vector3(0.2f, 0.06f, 0.22f), M.Black, false);
            CampusKit.Box("Base_Top", ph.transform, new Vector3(0, 0.065f, 0.02f), new Vector3(0.17f, 0.02f, 0.12f), M.Black, false);
            CampusKit.Box("Keypad", ph.transform, new Vector3(0, 0.062f, -0.07f), new Vector3(0.1f, 0.012f, 0.06f), M.MetalLight, false);
            CampusKit.Box("Handset", ph.transform, new Vector3(0, 0.095f, 0.02f), new Vector3(0.05f, 0.03f, 0.22f), M.Black, false);
            CampusKit.Box("Earpiece", ph.transform, new Vector3(0, 0.09f, 0.115f), new Vector3(0.06f, 0.04f, 0.05f), M.Black, false);
            CampusKit.Box("Mouthpiece", ph.transform, new Vector3(0, 0.09f, -0.075f), new Vector3(0.06f, 0.04f, 0.05f), M.Black, false);
            PhoneComponent(ph, label, false);
        }

        static void WallPhone(Transform g, string label, Vector3 at, float yaw)
        {
            var ph = new GameObject("Phone_" + label.Replace(' ', '_')); ph.transform.SetParent(g, false);
            ph.transform.position = at; ph.transform.rotation = Quaternion.Euler(0, yaw, 0);
            CampusKit.Box("Box", ph.transform, new Vector3(0, 0, 0.04f), new Vector3(0.18f, 0.26f, 0.08f), M.Black, false);
            CampusKit.Box("Handset", ph.transform, new Vector3(-0.06f, 0, 0.1f), new Vector3(0.04f, 0.22f, 0.04f), M.Black, false);
            CampusKit.Box("Keypad", ph.transform, new Vector3(0.03f, 0, 0.082f), new Vector3(0.07f, 0.1f, 0.005f), M.MetalLight, false);
            PhoneComponent(ph, label, false);
        }

        static void Payphone(Transform g, string label, Vector3 at, float yaw)
        {
            var ph = new GameObject("Payphone_" + label.Replace(' ', '_')); ph.transform.SetParent(g, false);
            ph.transform.position = at; ph.transform.rotation = Quaternion.Euler(0, yaw, 0);
            CampusKit.Box("Post", ph.transform, new Vector3(0, 1.1f, -0.06f), new Vector3(0.14f, 2.2f, 0.12f), M.MetalLight);
            CampusKit.Box("Hood", ph.transform, new Vector3(0, 2.05f, 0.12f), new Vector3(0.62f, 0.06f, 0.42f), M.MetalLight, false);
            CampusKit.Box("Hood_Back", ph.transform, new Vector3(0, 1.45f, 0.0f), new Vector3(0.62f, 1.2f, 0.03f), M.Metal, false);
            CampusKit.Text(ph.transform, "TELEPHONE", new Vector3(0, 2.0f, 0.335f), Vector3.forward, 0.06f, new Color(0.85f, 0.85f, 0.75f));
            CampusKit.Prop("korean_public_payphone_01", ph.transform, new Vector3(0, 1.35f, 0.02f), 0, 1f, false);
            PhoneComponent(ph, label, true);
        }

        // ------------------------------------------------------------------ light zones
        struct ZoneDef { public string label; public Rect area; public bool upper; public bool on; }
        static readonly ZoneDef[] Zones =
        {
            new ZoneDef { label = "Records Archive", area = Rect.MinMaxRect(-36, -24, -14, 2) },
            new ZoneDef { label = "Open Offices", area = Rect.MinMaxRect(-36, 2, -14, 24), on = true },
            new ZoneDef { label = "Seminar Hall", area = Rect.MinMaxRect(14, -2, 36, 24) },
            new ZoneDef { label = "Staff Lounge", area = Rect.MinMaxRect(14, -24, 36, -2) },
            new ZoneDef { label = "Vestibule", area = Rect.MinMaxRect(-14, -24, 14, -12), on = true },
            new ZoneDef { label = "Admissions", area = Rect.MinMaxRect(-36, 0, -14, 24), upper = true, on = true },
            new ZoneDef { label = "Finance", area = Rect.MinMaxRect(-36, -24, -14, 0), upper = true },
            new ZoneDef { label = "Boardroom", area = Rect.MinMaxRect(14, -24, 36, 4), upper = true },
            new ZoneDef { label = "Head's Suite", area = Rect.MinMaxRect(14, 4, 36, 24), upper = true, on = true },
        };

        static void LightZones()
        {
            var g = new GameObject("Light_Zones").transform; g.SetParent(root, false);
            var adminLamps = CampusAdmin.LightsGroup.GetComponentsInChildren<CampusLamp>(true);
            foreach (var z in Zones)
            {
                var list = new List<CampusLamp>();
                foreach (var l in adminLamps)
                {
                    Vector3 p = l.transform.position;
                    bool upper = p.y > H + 0.5f;
                    if (upper != z.upper || !z.area.Contains(new Vector2(p.x, p.z))) continue;
                    if (l.important) continue;   // the entrance lamps stay as they are
                    list.Add(l);
                }
                if (list.Count == 0) continue;
                var go = new GameObject("Zone_" + z.label.Replace(' ', '_').Replace("'", "")); go.transform.SetParent(g, false);
                go.transform.position = new Vector3(z.area.center.x, z.upper ? H : 0, z.area.center.y);
                var lz = go.AddComponent<LightZone>();
                lz.label = z.label; lz.lamps = list.ToArray(); lz.area = z.area; lz.floorY = z.upper ? H : 0; lz.startOn = z.on;
            }
        }

        // ------------------------------------------------------------------ floodlights on the grounds (the generator's)
        static void Floodlights()
        {
            var g = new GameObject("Floodlight_Banks").transform; g.SetParent(root, false);
            Flood(g, "Sports Field Floods", Rect.MinMaxRect(-140, 162, -40, 226), new[] { new Vector3(-136, 0, 160), new Vector3(-44, 0, 228) });
            Flood(g, "Car Park Floods", Rect.MinMaxRect(14, 156, 120, 226), new[] { new Vector3(66, 0, 154.5f), new Vector3(118, 0, 228) });
            Flood(g, "Quad Floods", Rect.MinMaxRect(-45, 45, 45, 117), new[] { new Vector3(-46, 0, 44), new Vector3(46, 0, 118) });
            Flood(g, "West Gardens Floods", Rect.MinMaxRect(-170, -10, -112, 122), new[] { new Vector3(-113, 0, 0), new Vector3(-170, 0, 110) });
            Flood(g, "Student Centre Floods", Rect.MinMaxRect(120, -50, 170, 40), new[] { new Vector3(123, 0, 38), new Vector3(168, 0, -36) });
            Flood(g, "Library Lawn Floods", Rect.MinMaxRect(-104, 112, -52, 140), new[] { new Vector3(-104, 0, 140), new Vector3(-52, 0, 113) });
        }

        static void Flood(Transform g, string label, Rect area, Vector3[] poles)
        {
            var go = new GameObject("Zone_" + label.Replace(' ', '_')); go.transform.SetParent(g, false);
            go.transform.position = new Vector3(area.center.x, 0, area.center.y);
            var lamps = new List<CampusLamp>();
            Vector3 centre = new Vector3(area.center.x, 0, area.center.y);
            foreach (var p in poles)
            {
                CampusKit.Cyl("Flood_Pole", go.transform, go.transform.InverseTransformPoint(p), 0.14f, 11f, M.MetalLight);
                Vector3 dir = (centre - p); dir.y = 0; dir.Normalize();
                Vector3 side = Vector3.Cross(Vector3.up, dir);
                foreach (float s in new[] { -0.8f, 0.8f })
                {
                    Vector3 hp = p + Vector3.up * 11.2f + side * s;
                    var head = BoxR("Flood_Head", go.transform, hp, new Vector3(1.1f, 0.7f, 0.25f), Quaternion.LookRotation(dir) * Quaternion.Euler(35, 0, 0), M.Dark, false);
                    var l = CampusKit.Lamp(lights, hp + dir * 4f - Vector3.up * 2.5f, new Color(0.85f, 0.9f, 1f), 46f, 3.4f, new[] { head.GetComponent<Renderer>() }, 0f, false);
                    l.important = true; l.headGlow = 4f;
                    lamps.Add(l);
                }
            }
            var lz = go.AddComponent<LightZone>();
            lz.label = label; lz.outdoor = true; lz.lamps = lamps.ToArray(); lz.area = area; lz.floorY = 0; lz.startOn = false;
            lz.nailedRange = 45f; lz.giantRange = 170f;
        }

        // ------------------------------------------------------------------ P2's sinkholes
        /// <summary>Storm drains that have caved in under the paths: to everyone but P2 the ground looks whole. Step on
        /// one and you drop in (a scream the monsters hear), climbing out at the edge a few seconds later.</summary>
        static void Sinkholes()
        {
            var g = new GameObject("Sinkholes"); g.transform.SetParent(root, false);
            var pits = g.AddComponent<PitHazards>();
            pits.floorY = 0f; pits.respawn = true; pits.dropDepth = 3f; pits.shaftDepth = 3.4f;
            var dark = CampusKit.Flat("CM_SinkholeDark", new Color(0.01f, 0.01f, 0.01f), 0.05f);
            var rim = CampusKit.Flat("CM_SinkholeRim", new Color(0.18f, 0.14f, 0.1f), 0.1f);
            foreach (var c in new[] { new Vector2(-108, 62), new Vector2(-138, 12), new Vector2(40, -44), new Vector2(128, -45.5f), new Vector2(0, 101) })
            {
                var r = new Rect(c.x - 1.3f, c.y - 1.2f, 2.6f, 2.4f);
                pits.holes.Add(new PitHazards.Hole { rect = r, hidden = true });
                var vis = new GameObject("Sinkhole_P2"); vis.transform.SetParent(g.transform, false);
                CampusKit.Box("Hole", vis.transform, new Vector3(c.x, 0.045f, c.y), new Vector3(2.5f, 0.01f, 2.3f), dark, false);
                foreach (var e in new[] { new Vector3(0, 0, 1.25f), new Vector3(0, 0, -1.25f), new Vector3(1.35f, 0, 0), new Vector3(-1.35f, 0, 0) })
                    CampusKit.Box("Rim", vis.transform, new Vector3(c.x, 0.05f, c.y) + e, new Vector3(Mathf.Abs(e.x) > 0 ? 0.2f : 2.9f, 0.04f, Mathf.Abs(e.z) > 0 ? 0.2f : 2.7f), rim, false);
                var pp = vis.AddComponent<PartyPerception>(); pp.rule = PartyPerception.Rule.VisibleOnlyTo; pp.player = 1;
            }
        }

        // ------------------------------------------------------------------ navmesh, monsters, radio, narration
        public const string NavPath = "Assets/Campus/Generated/CampusNavMesh.asset";

        public static string BakeNavMesh(Transform env)
        {
            var markups = new List<NavMeshBuildMarkup>();
            foreach (var d in env.GetComponentsInChildren<InteractiveDoor>(true)) markups.Add(new NavMeshBuildMarkup { root = d.transform.parent != null ? d.transform.parent : d.transform, ignoreFromBuild = true });
            foreach (var d in env.GetComponentsInChildren<CampusKeyDoor>(true)) markups.Add(new NavMeshBuildMarkup { root = d.transform, ignoreFromBuild = true });
            foreach (var a in env.GetComponentsInChildren<PartyItem>(true)) markups.Add(new NavMeshBuildMarkup { root = a.transform, ignoreFromBuild = true });
            foreach (var p in Object.FindObjectsByType<RemasteredPlayerController>(FindObjectsSortMode.None)) markups.Add(new NavMeshBuildMarkup { root = p.transform, ignoreFromBuild = true });
            var giant = Object.FindAnyObjectByType<CampusGiant>();
            if (giant != null) markups.Add(new NavMeshBuildMarkup { root = giant.transform, ignoreFromBuild = true });
            // up to just above the admin upper floor's head room: roofs are not walkable
            var bounds = new Bounds(new Vector3(-2f, 2.6f, 110f), new Vector3(360f, 9.2f, 340f));
            var sources = new List<NavMeshBuildSource>();
            UnityEngine.AI.NavMeshBuilder.CollectSources(bounds, Physics.DefaultRaycastLayers, NavMeshCollectGeometry.PhysicsColliders, 0, markups, sources);
            var settings = NavMesh.GetSettingsByID(0);
            settings.agentRadius = 0.35f; settings.agentHeight = 1.9f; settings.agentClimb = 0.35f; settings.agentSlope = 42f;
            settings.overrideVoxelSize = true; settings.voxelSize = 0.14f;
            settings.overrideTileSize = true; settings.tileSize = 128;
            var data = UnityEngine.AI.NavMeshBuilder.BuildNavMeshData(settings, sources, bounds, Vector3.zero, Quaternion.identity);
            if (data == null) return "navmesh FAILED";
            AssetDatabase.DeleteAsset(NavPath);
            AssetDatabase.CreateAsset(data, NavPath);
            var holder = new GameObject("Campus_NavMesh");
            holder.AddComponent<NavMeshDataLoader>().data = data;
            return "navmesh from " + sources.Count + " colliders";
        }

        public static int SpawnNailed()
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(NailedImport.PrefabPath);
            if (prefab == null) { Debug.LogWarning("[Campus] Nailed prefab missing - run Hollow Tower/Campus/Import Nailed Monster"); return 0; }
            // inside: round both floors of the administration building (it starts upstairs in the boardroom, far from the lift)
            Spawn(prefab, "Nailed_Admin", new Vector3(26f, H, -12f), true, new[]
            {
                new Vector3(26, H, -12), new Vector3(20, H, 14), new Vector3(12.5f, H, 12), new Vector3(12.5f, H, -10.5f), new Vector3(0, H, -16),
                new Vector3(-25, H, -6), new Vector3(-25, H, 14), new Vector3(-12.5f, H, 10), new Vector3(9.2f, H, 3.5f), new Vector3(9.2f, 0, 17.5f),
                new Vector3(24, 0, 10), new Vector3(25, 0, -12), new Vector3(0, 0, 6), new Vector3(-25, 0, 12), new Vector3(-25, 0, -12),
                new Vector3(-6, 0, -14), new Vector3(9.2f, 0, 17.5f), new Vector3(9.2f, H, 3.5f),
            });
            // outside: a long round of the grounds - quad, library, gardens, the yard, the south road, the service lane, the
            // student centre, the cafe
            Spawn(prefab, "Nailed_Grounds", new Vector3(-80f, 0, 120f), false, new[]
            {
                new Vector3(0, 0, 60), new Vector3(-50, 0, 82), new Vector3(-80, 0, 120), new Vector3(-108, 0, 100), new Vector3(-138, 0, 60),
                new Vector3(-138, 0, -12), new Vector3(-150, 0, -30), new Vector3(-100, 0, -44), new Vector3(-20, 0, -44), new Vector3(60, 0, -44),
                new Vector3(120, 0, -45), new Vector3(146, 0, -40), new Vector3(126, 0, 0), new Vector3(114, 0, 40), new Vector3(76, 0, 0),
                new Vector3(45, 0, 30), new Vector3(20, 0, 40),
            });
            return 2;
        }

        static void Spawn(GameObject prefab, string name, Vector3 at, bool indoors, Vector3[] route)
        {
            var go = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
            go.name = name;
            go.transform.position = at;
            var agent = go.AddComponent<NavMeshAgent>();
            agent.radius = 0.35f; agent.height = 1.9f; agent.baseOffset = 0f; agent.speed = 1.5f;
            agent.angularSpeed = 420f; agent.acceleration = 22f; agent.stoppingDistance = 0.3f; agent.autoBraking = true;
            agent.obstacleAvoidanceType = ObstacleAvoidanceType.LowQualityObstacleAvoidance;
            var m = go.AddComponent<NailedMonster>();
            m.route = route; m.indoors = indoors;
            m.walkClipSpeed = NailedImport.WalkClipSpeed; m.runClipSpeed = NailedImport.RunClipSpeed;
            m.animator = go.GetComponentInChildren<Animator>();
            if (!indoors) { m.sightRange = 26f; m.walkSpeed = 1.6f; go.AddComponent<FarInvisible>().hideBeyond = 13f; }   // P3 sees it coming
            go.AddComponent<NetworkObject>();
            var nt = go.AddComponent<NetworkTransform>();
            nt.SyncRotAngleX = false; nt.SyncRotAngleZ = false; nt.SyncScaleX = nt.SyncScaleY = nt.SyncScaleZ = false; nt.Interpolate = true;
            go.AddComponent<Net.NetMonster>();
        }

        public static void RadioAndNarration()
        {
            var radio = new GameObject("Campus_Radio");
            radio.AddComponent<NetworkObject>();
            radio.AddComponent<HollowTower.Campus.Walkie>();
            var nm = Object.FindAnyObjectByType<NarrationManager>();
            if (nm == null) nm = new GameObject("Campus_Narration").AddComponent<NarrationManager>();
            var so = new SerializedObject(nm);
            var list = so.FindProperty("_narrationEntries");
            System.Action<string, string> add = (id, text) =>
            {
                for (int i = 0; i < list.arraySize; i++) if (list.GetArrayElementAtIndex(i).FindPropertyRelative("id").stringValue == id) return;
                list.InsertArrayElementAtIndex(list.arraySize);
                var e = list.GetArrayElementAtIndex(list.arraySize - 1);
                e.FindPropertyRelative("id").stringValue = id;
                e.FindPropertyRelative("subtitleText").stringValue = text;
                e.FindPropertyRelative("volume").floatValue = 1f;
                e.FindPropertyRelative("clip").objectReferenceValue = null;
            };
            // placeholders: the user records the real lines (drop an AudioClip into each entry)
            add("campus_gate_welded", "Welded. From the outside... somebody wanted us kept in. There was an old service gate - the far south-east corner, past the student centre. The control room upstairs could open it.");
            add("campus_service_gate_found", "That's it - the service gate. The lock's magnetic. Someone has to release it from the control room.");
            so.ApplyModifiedPropertiesWithoutUndo();
        }
    }
}
