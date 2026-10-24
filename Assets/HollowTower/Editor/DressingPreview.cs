using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering.Universal;
using Object = UnityEngine.Object;

namespace HollowTower.EditorTools
{
    /// <summary>Renders review shots of the dressed map from temporary cameras (nothing is left in the scene).</summary>
    public static class DressingPreview
    {
        /// <summary>
        /// Renders one shot. fill > 0 adds a temporary point light at the camera so placement can be judged
        /// in rooms whose own lamps are dim; fill = 0 shows the in-game lighting.
        /// </summary>
        public static void Shot(string png, Vector3 pos, Vector3 lookAt, float fov = 70f, float fill = 0f, int w = 960, int h = 540)
        {
            var go = new GameObject("TmpReviewCam");
            Light light = null;
            try
            {
                var cam = go.AddComponent<Camera>();
                cam.fieldOfView = fov;
                cam.nearClipPlane = 0.05f;
                cam.farClipPlane = 60f;
                cam.allowHDR = true;
                go.AddComponent<UniversalAdditionalCameraData>().renderPostProcessing = true;
                go.transform.position = pos;
                go.transform.LookAt(lookAt);
                if (fill > 0)
                {
                    light = new GameObject("TmpReviewLight").AddComponent<Light>();
                    light.type = LightType.Point;
                    light.range = 30f;
                    light.intensity = fill;
                    light.shadows = LightShadows.Soft;
                    light.transform.position = pos + Vector3.up * 0.3f;
                }
                var rt = new RenderTexture(w, h, 24, RenderTextureFormat.ARGB32);
                cam.targetTexture = rt;
                cam.Render();
                var prev = RenderTexture.active;
                RenderTexture.active = rt;
                var tex = new Texture2D(w, h, TextureFormat.RGB24, false);
                tex.ReadPixels(new Rect(0, 0, w, h), 0, 0);
                tex.Apply();
                RenderTexture.active = prev;
                cam.targetTexture = null;
                File.WriteAllBytes(png, tex.EncodeToPNG());
                Object.DestroyImmediate(tex);
                Object.DestroyImmediate(rt);
            }
            finally
            {
                Object.DestroyImmediate(go);
                if (light != null) Object.DestroyImmediate(light.gameObject);
            }
        }

        /// <summary>What a player sees stepping through the room's first door: eye height, looking at the room centre.</summary>
        public static string EyeShot(string png, int floor, string roomName, float fill)
        {
            var plan = LevelLayout.Plans[floor];
            var room = plan.Find(roomName);
            if (room == null) return "no room " + roomName;
            float c = CollegeDresser.Cell, y0 = floor * CollegeDresser.Story;
            var center = new Vector3((room.rect.x + room.rect.width * 0.5f) * c, y0 + 0.9f, (LevelLayout.Rows - room.rect.y - room.rect.height * 0.5f) * c);
            Vector3 eye = center + new Vector3(0, 0.75f, 0);
            foreach (var cell in room.cells)
            {
                int col = cell.x, row = cell.y;
                float x0 = col * c, z1 = (LevelLayout.Rows - row) * c;
                int[] keys = { LevelLayout.EdgeE(row, col), col > 0 ? LevelLayout.EdgeE(row, col - 1) : -1, row > 0 ? LevelLayout.EdgeS(row - 1, col) : -1, LevelLayout.EdgeS(row, col) };
                Vector3[] mids = { new Vector3(x0 + c - 0.6f, 0, z1 - c * 0.5f), new Vector3(x0 + 0.6f, 0, z1 - c * 0.5f), new Vector3(x0 + c * 0.5f, 0, z1 - 0.6f), new Vector3(x0 + c * 0.5f, 0, z1 - c + 0.6f) };
                for (int i = 0; i < 4; i++)
                    if (keys[i] >= 0 && plan.doors.Contains(keys[i]))
                    {
                        eye = new Vector3(mids[i].x, y0 + 1.65f, mids[i].z);
                        Shot(png, eye, center, 75f, fill);
                        return png;
                    }
            }
            Shot(png, eye, center + Vector3.forward, 75f, fill);
            return png;
        }

        /// <summary>A shot from a room's doorway-ish corner looking across it, by room name.</summary>
        public static string RoomShot(string png, int floor, string roomName, float fill, int corner = 0)
        {
            var plan = LevelLayout.Plans[floor];
            var room = plan.Find(roomName);
            if (room == null) return "no room " + roomName;
            var R = room.rect;
            float c = CollegeDresser.Cell, y0 = floor * CollegeDresser.Story;
            float x0 = R.x * c + 0.6f, x1 = (R.x + R.width) * c - 0.6f;
            float z1 = (LevelLayout.Rows - R.y) * c - 0.6f, z0 = (LevelLayout.Rows - R.y - R.height) * c + 0.6f;
            Vector3 pos;
            switch (corner % 4)
            {
                case 0: pos = new Vector3(x0, y0 + 3.2f, z0); break;
                case 1: pos = new Vector3(x1, y0 + 3.2f, z0); break;
                case 2: pos = new Vector3(x1, y0 + 3.2f, z1); break;
                default: pos = new Vector3(x0, y0 + 3.2f, z1); break;
            }
            var center = new Vector3((x0 + x1) * 0.5f, y0 + 0.4f, (z0 + z1) * 0.5f);
            Shot(png, pos, center, 85f, fill);
            return png;
        }
    }
}
