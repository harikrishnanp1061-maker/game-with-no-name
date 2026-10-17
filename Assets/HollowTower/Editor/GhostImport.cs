using System.IO;
using UnityEditor;
using UnityEngine;

namespace HollowTower.EditorTools
{
    /// <summary>
    /// Import settings for the ghost model (Resources/Models/Ghost, "DOE Ghost" FBX from the user's ghost.zip).
    /// Keeps its rig and any clips (Legacy, so Ghost.cs can play them without an Animator), and imports the
    /// three texture sets (body, cloth, claws) at 1K with proper normal maps.
    /// </summary>
    public class GhostImport : AssetPostprocessor
    {
        const string Folder = "Assets/HollowTower/Resources/Models/Ghost/";

        // After ModelTextureCap (HDRPSetup.cs), which turns animation off for everything under Models/.
        public override int GetPostprocessOrder() { return 10; }

        void OnPreprocessTexture()
        {
            if (!assetPath.StartsWith(Folder)) return;
            var ti = (TextureImporter)assetImporter;
            ti.maxTextureSize = 1024;
            ti.mipmapEnabled = true;
            ti.textureCompression = TextureImporterCompression.CompressedHQ;
            if (assetPath.EndsWith("_Normal.png")) ti.textureType = TextureImporterType.NormalMap;
            // HDRP mask maps (R metal, G occlusion, A smoothness) built from the zip's roughness / metallic maps: linear data.
            if (assetPath.EndsWith("_Mask.png")) ti.sRGBTexture = false;
        }

        void OnPreprocessModel()
        {
            if (!assetPath.StartsWith(Folder)) return;
            var mi = (ModelImporter)assetImporter;
            mi.materialImportMode = ModelImporterMaterialImportMode.None;
            mi.animationType = ModelImporterAnimationType.Legacy;
            mi.importAnimation = true;
            mi.importCameras = false;
            mi.importLights = false;
        }

        /// <summary>Batch entry point: logs the ghost FBX (nodes, meshes, clips) and writes Shots/ghost_uv_*.png UV layouts.</summary>
        public static void Inspect()
        {
            string path = Folder + "Ghost.fbx";
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
            var go = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (go == null) { Debug.Log("GInspect missing " + path); EditorApplication.Exit(1); return; }
            foreach (var t in go.GetComponentsInChildren<Transform>(true))
            {
                string c = "";
                foreach (var comp in t.GetComponents<Component>()) c += comp.GetType().Name + " ";
                Debug.Log("GInspect node " + t.name + " parent=" + (t.parent ? t.parent.name : "-") + " p=" + t.position.ToString("F3") + " s=" + t.lossyScale.ToString("F3") + " comps=" + c);
            }
            Directory.CreateDirectory("Shots");
            int k = 0;
            foreach (var r in go.GetComponentsInChildren<Renderer>(true))
            {
                var smr = r as SkinnedMeshRenderer;
                var mf = r.GetComponent<MeshFilter>();
                var mesh = smr != null ? smr.sharedMesh : mf != null ? mf.sharedMesh : null;
                if (mesh == null) continue;
                Debug.Log("GInspect mesh " + r.name + " skinned=" + (smr != null) + " verts=" + mesh.vertexCount + " tris=" + mesh.triangles.Length / 3 +
                          " sub=" + mesh.subMeshCount + " bones=" + (smr != null ? smr.bones.Length : 0) + " bounds=" + r.bounds.center.ToString("F3") + " " + r.bounds.size.ToString("F3"));
                var uv = mesh.uv;
                for (int s = 0; s < mesh.subMeshCount; s++)
                {
                    var tex = new Texture2D(256, 256);
                    var px = new Color32[256 * 256];
                    var tris = mesh.GetTriangles(s);
                    Vector2 mn = Vector2.one * 1e9f, mx = -mn;
                    for (int i = 0; i < tris.Length; i += 3)
                        for (int e = 0; e < 3; e++)
                        {
                            Vector2 a = uv[tris[i + e]], b = uv[tris[i + (e + 1) % 3]];
                            mn = Vector2.Min(mn, a); mx = Vector2.Max(mx, a);
                            for (int q = 0; q <= 16; q++)
                            {
                                Vector2 p = Vector2.Lerp(a, b, q / 16f);
                                int x = Mathf.FloorToInt(Mathf.Repeat(p.x, 1f) * 255f), y = Mathf.FloorToInt(Mathf.Repeat(p.y, 1f) * 255f);
                                px[y * 256 + x] = new Color32(255, 255, 255, 255);
                            }
                        }
                    tex.SetPixels32(px);
                    File.WriteAllBytes("Shots/ghost_uv_" + k + "_" + r.name.Replace(' ', '_') + "_" + s + ".png", tex.EncodeToPNG());
                    Debug.Log("GInspect uv " + k + " " + r.name + " sub" + s + " tris=" + tris.Length / 3 + " uvmin=" + mn.ToString("F3") + " uvmax=" + mx.ToString("F3"));
                }
                k++;
            }
            foreach (var o in AssetDatabase.LoadAllAssetsAtPath(path))
                if (o is AnimationClip) Debug.Log("GInspect clip " + o.name + " len=" + ((AnimationClip)o).length + " legacy=" + ((AnimationClip)o).legacy);
            EditorApplication.Exit(0);
        }

        static readonly string[][] Chains =
        {
            new[] { "Hips", "Spine1", "Spine2", "Spine3", "Neck", "Head", "Head_end" },
            new[] { "Spine3", "Shoulder_L", "Arm_L", "ForeArm_L", "Hand_L", "Middle3_L_end" },
            new[] { "Spine3", "Shoulder_R", "Arm_R", "ForeArm_R", "Hand_R", "Middle3_R_end" },
            new[] { "Hips", "LegUp_L", "LegLow_L", "Foot_L", "Toe_L", "Toe_L_end" },
            new[] { "Hips", "LegUp_R", "LegLow_R", "Foot_R", "Toe_R", "Toe_R_end" },
        };

        /// <summary>
        /// Batch entry point: samples every clip at 8 times and draws stick figures (side and front views) into
        /// Shots/ghost_clips.png, one row per clip, and logs how far the hips travel (root motion) and how high the feet lift.
        /// </summary>
        public static void ClipSheet()
        {
            string path = Folder + "Ghost.fbx";
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            var clips = new System.Collections.Generic.List<AnimationClip>();
            foreach (var o in AssetDatabase.LoadAllAssetsAtPath(path))
                if (o is AnimationClip && !o.name.StartsWith("__preview")) clips.Add((AnimationClip)o);
            const int Frames = 8, Cell = 160;
            int w = Frames * Cell * 2, h = clips.Count * Cell;
            var tex = new Texture2D(w, h);
            var px = new Color32[w * h];
            for (int i = 0; i < px.Length; i++) px[i] = new Color32(16, 16, 24, 255);
            var go = Object.Instantiate(prefab);
            var bones = new System.Collections.Generic.Dictionary<string, Transform>();
            foreach (var t in go.GetComponentsInChildren<Transform>(true)) bones[t.name] = t;
            for (int c = 0; c < clips.Count; c++)
            {
                var clip = clips[c];
                Vector3 hips0 = Vector3.zero;
                float lMin = 9, lMax = -9, rMin = 9, rMax = -9;
                for (int f = 0; f < Frames; f++)
                {
                    float time = clip.length * f / Frames;
                    clip.SampleAnimation(go, time);
                    Vector3 hips = bones["Hips"].position;
                    if (f == 0) hips0 = hips;
                    float lf = bones["Foot_L"].position.y, rf = bones["Foot_R"].position.y;
                    lMin = Mathf.Min(lMin, lf); lMax = Mathf.Max(lMax, lf); rMin = Mathf.Min(rMin, rf); rMax = Mathf.Max(rMax, rf);
                    Debug.Log("GClip " + clip.name + " t=" + time.ToString("F2") + " hips=" + hips.ToString("F3") + " footL=" + bones["Foot_L"].position.ToString("F3") +
                              " footR=" + bones["Foot_R"].position.ToString("F3") + " head=" + bones["Head"].position.ToString("F3") + " handL=" + bones["Hand_L"].position.ToString("F3"));
                    for (int view = 0; view < 2; view++)
                    {
                        int ox = (view * Frames + f) * Cell, oy = (clips.Count - 1 - c) * Cell;
                        foreach (var chain in Chains)
                            for (int k = 0; k + 1 < chain.Length; k++)
                            {
                                Transform a, b;
                                if (!bones.TryGetValue(chain[k], out a) || !bones.TryGetValue(chain[k + 1], out b)) continue;
                                Color32 col = chain[1].EndsWith("_L") || chain[1].EndsWith("L") && chain[1].Contains("_L") ? new Color32(255, 120, 90, 255)
                                            : chain[1].Contains("_R") ? new Color32(90, 170, 255, 255) : new Color32(240, 240, 240, 255);
                                Line(px, w, h, Project(a.position - hips + Vector3.up * hips.y, view, ox, oy, Cell), Project(b.position - hips + Vector3.up * hips.y, view, ox, oy, Cell), col);
                            }
                    }
                }
                clip.SampleAnimation(go, clip.length * 0.999f);
                Debug.Log("GClip " + clip.name + " len=" + clip.length + " hipsTravel=" + (bones["Hips"].position - hips0).ToString("F3") +
                          " footL y " + lMin.ToString("F3") + ".." + lMax.ToString("F3") + " footR y " + rMin.ToString("F3") + ".." + rMax.ToString("F3"));
            }
            tex.SetPixels32(px);
            Directory.CreateDirectory("Shots");
            File.WriteAllBytes("Shots/ghost_clips.png", tex.EncodeToPNG());
            Object.DestroyImmediate(go);
            EditorApplication.Exit(0);
        }

        // view 0: side (z to the right), view 1: front (x to the right). Height 1.3 m fills the cell.
        static Vector2Int Project(Vector3 p, int view, int ox, int oy, int cell)
        {
            float s = cell / 1.4f;
            float x = view == 0 ? p.z : p.x;
            return new Vector2Int(ox + cell / 2 + Mathf.RoundToInt(x * s), oy + 6 + Mathf.RoundToInt(p.y * s));
        }

        static void Line(Color32[] px, int w, int h, Vector2Int a, Vector2Int b, Color32 c)
        {
            int n = Mathf.Max(Mathf.Abs(b.x - a.x), Mathf.Abs(b.y - a.y)) + 1;
            for (int i = 0; i <= n; i++)
            {
                int x = Mathf.RoundToInt(Mathf.Lerp(a.x, b.x, i / (float)n)), y = Mathf.RoundToInt(Mathf.Lerp(a.y, b.y, i / (float)n));
                for (int dx = 0; dx < 2; dx++) for (int dy = 0; dy < 2; dy++)
                {
                    int X = x + dx, Y = y + dy;
                    if (X >= 0 && X < w && Y >= 0 && Y < h) px[Y * w + X] = c;
                }
            }
        }
    }
}
