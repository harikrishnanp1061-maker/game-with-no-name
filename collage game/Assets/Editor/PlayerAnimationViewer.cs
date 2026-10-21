using UnityEngine;
using UnityEditor;

public class PlayerAnimationViewer : EditorWindow
{
    private GameObject player;
    private AnimationClip idleClip;
    private AnimationClip walkClip;
    private AnimationClip runClip;
    private AnimationClip attackClip;

    private AnimationClip currentClip;
    private float previewTime = 0f;
    private bool isPlaying = false;
    private double lastTime = 0;

    [MenuItem("Tools/Player Character/Animation Previewer")]
    public static void ShowWindow()
    {
        var win = GetWindow<PlayerAnimationViewer>("Player Animations");
        win.minSize = new Vector2(360, 480);
        win.LoadClips();
        win.Show();
    }

    void OnEnable()
    {
        LoadClips();
        EditorApplication.update += OnEditorUpdate;
    }

    void OnDisable()
    {
        EditorApplication.update -= OnEditorUpdate;
        StopPreview();
    }

    [InitializeOnLoadMethod]
    public static void ExtractClipsOnLoad()
    {
        string dir = "Assets/Characters/Player/Animations";
        if (!AssetDatabase.IsValidFolder(dir))
        {
            AssetDatabase.CreateFolder("Assets/Characters/Player", "Animations");
        }
        string fbxPath = "Assets/Characters/Player/Player_Character.fbx";
        foreach (var obj in AssetDatabase.LoadAllAssetsAtPath(fbxPath))
        {
            if (obj is AnimationClip clip && !clip.name.StartsWith("__preview__"))
            {
                string targetPath = $"{dir}/{clip.name}.anim";
                if (!System.IO.File.Exists(targetPath))
                {
                    AnimationClip copy = Object.Instantiate(clip);
                    AssetDatabase.CreateAsset(copy, targetPath);
                    Debug.Log($"[PlayerAnimationViewer] Extracted animation clip: {targetPath}");
                }
            }
        }
        AssetDatabase.SaveAssets();
    }

    public void LoadClips()
    {
        player = GameObject.Find("Player_Character_Model");

        string fbxPath = "Assets/Characters/Player/Player_Character.fbx";
        foreach (var obj in AssetDatabase.LoadAllAssetsAtPath(fbxPath))
        {
            if (obj is AnimationClip clip && !clip.name.StartsWith("__preview__"))
            {
                if (clip.name.Contains("Idle")) idleClip = clip;
                else if (clip.name.Contains("Walk")) walkClip = clip;
                else if (clip.name.Contains("Run")) runClip = clip;
                else if (clip.name.Contains("Attack")) attackClip = clip;
            }
        }
    }

    void OnGUI()
    {
        EditorGUILayout.Space(10);
        EditorGUILayout.LabelField("Player Character Animation Player", EditorStyles.boldLabel);
        EditorGUILayout.HelpBox("Select any animation below to preview your newly rigged player character live in Scene & Game views!", MessageType.Info);

        if (player == null)
        {
            player = GameObject.Find("Player_Character_Model");
            if (player == null)
            {
                EditorGUILayout.HelpBox("Player_Character_Model not found in scene! Click below to spawn it.", MessageType.Warning);
                if (GUILayout.Button("Spawn Player Character in Scene", GUILayout.Height(30)))
                {
                    var prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Characters/Player/Player_Character.prefab");
                    if (prefab != null)
                    {
                        var inst = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
                        inst.name = "Player_Character_Model";
                        inst.transform.position = new Vector3(0, 0.13f, -4.2f);
                        inst.transform.rotation = Quaternion.Euler(0, 180, 0);
                        player = inst;
                    }
                }
                return;
            }
        }

        EditorGUILayout.Space(10);
        GUI.backgroundColor = new Color(0.35f, 0.85f, 0.45f);
        if (GUILayout.Button("► PLAY IDLE (Natural Breathing)", GUILayout.Height(36)))
        {
            PlayAnimation(idleClip, "Player_Idle");
        }

        GUI.backgroundColor = new Color(0.4f, 0.75f, 1.0f);
        if (GUILayout.Button("► PLAY WALK (College Stride)", GUILayout.Height(36)))
        {
            PlayAnimation(walkClip, "Player_Walk");
        }

        GUI.backgroundColor = new Color(1.0f, 0.65f, 0.25f);
        if (GUILayout.Button("► PLAY RUN (Athletic Sprint)", GUILayout.Height(36)))
        {
            PlayAnimation(runClip, "Player_Run");
        }

        GUI.backgroundColor = new Color(0.95f, 0.35f, 0.35f);
        if (GUILayout.Button("► PLAY ATTACK (Boxing Cross Punch)", GUILayout.Height(36)))
        {
            PlayAnimation(attackClip, "Player_Attack");
        }

        GUI.backgroundColor = Color.white;
        EditorGUILayout.Space(10);
        if (GUILayout.Button("⟲ Reset Player Position & Facing (In Front of Elevator)", GUILayout.Height(30)))
        {
            pinnedPosition = new Vector3(0f, 0.13f, -4.2f);
            pinnedRotation = Quaternion.Euler(0f, 180f, 0f);
            if (player != null)
            {
                player.transform.position = pinnedPosition;
                player.transform.rotation = pinnedRotation;
                SceneView.RepaintAll();
            }
        }
        EditorGUILayout.Space(10);

        if (currentClip != null && !Application.isPlaying)
        {
            EditorGUILayout.LabelField("Active Clip: " + currentClip.name, EditorStyles.boldLabel);
            EditorGUILayout.LabelField($"Time: {previewTime:F2}s / {currentClip.length:F2}s");

            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button(isPlaying ? "❚❚ Pause" : "► Resume", GUILayout.Height(28)))
            {
                isPlaying = !isPlaying;
            }
            if (GUILayout.Button("■ Stop & Reset", GUILayout.Height(28)))
            {
                StopPreview();
            }
            EditorGUILayout.EndHorizontal();

            float newTime = EditorGUILayout.Slider("Scrub Timeline", previewTime, 0f, currentClip.length);
            if (Mathf.Abs(newTime - previewTime) > 0.001f)
            {
                previewTime = newTime;
                SampleCurrent();
            }
        }
    }

    private Vector3 pinnedPosition = new Vector3(0f, 0.13f, -4.2f);
    private Quaternion pinnedRotation = Quaternion.Euler(0f, 180f, 0f);

    private void PlayAnimation(AnimationClip clip, string stateName)
    {
        LoadClips();

        if (player != null)
        {
            // If player was dragged or moved away from the staging area, re-anchor to front of elevator
            if (player.transform.position.z > -2.0f || player.transform.position.y < 0.05f)
            {
                pinnedPosition = new Vector3(0f, 0.13f, -4.2f);
                pinnedRotation = Quaternion.Euler(0f, 180f, 0f);
            }
            else
            {
                pinnedPosition = player.transform.position;
                pinnedRotation = player.transform.rotation;
            }
            player.transform.position = pinnedPosition;
            player.transform.rotation = pinnedRotation;
        }

        if (Application.isPlaying && player != null)
        {
            var anim = player.GetComponent<Animator>();
            if (anim != null)
            {
                if (!anim.enabled) anim.enabled = true;
                anim.applyRootMotion = false;
                anim.CrossFade(stateName, 0.15f);
            }
            return;
        }

        if (clip == null || player == null)
        {
            Debug.LogError($"[PlayerAnimationViewer] Cannot play {stateName}: Clip or Player is null!");
            return;
        }

        currentClip = clip;
        previewTime = 0f;
        isPlaying = true;
        lastTime = EditorApplication.timeSinceStartup;

        if (!AnimationMode.InAnimationMode())
        {
            AnimationMode.StartAnimationMode();
        }

        SampleCurrent();
    }

    private void StopPreview()
    {
        isPlaying = false;
        currentClip = null;
        previewTime = 0f;

        if (AnimationMode.InAnimationMode())
        {
            AnimationMode.StopAnimationMode();
        }

        if (idleClip != null && player != null)
        {
            idleClip.SampleAnimation(player, 0f);
        }

        if (player != null)
        {
            player.transform.position = pinnedPosition;
            player.transform.rotation = pinnedRotation;
        }

        SceneView.RepaintAll();
    }

    private void OnEditorUpdate()
    {
        if (Application.isPlaying) return;
        if (!isPlaying || currentClip == null || player == null) return;

        double now = EditorApplication.timeSinceStartup;
        float dt = (float)(now - lastTime);
        lastTime = now;

        previewTime += dt;
        if (previewTime >= currentClip.length)
        {
            previewTime = previewTime % currentClip.length;
        }

        SampleCurrent();
    }

    private void SampleCurrent()
    {
        if (currentClip == null || player == null) return;

        if (!AnimationMode.InAnimationMode())
        {
            AnimationMode.StartAnimationMode();
        }

        AnimationMode.SampleAnimationClip(player, currentClip, previewTime);

        // Lock root position & facing to prevent any animation root motion from snapping player into elevator
        player.transform.position = pinnedPosition;
        player.transform.rotation = pinnedRotation;

        SceneView.RepaintAll();
        UnityEditorInternal.InternalEditorUtility.RepaintAllViews();
        this.Repaint();
    }
}
