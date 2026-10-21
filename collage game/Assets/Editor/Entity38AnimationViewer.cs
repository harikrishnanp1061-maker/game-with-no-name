using UnityEngine;
using UnityEditor;

public class Entity38AnimationViewer : EditorWindow
{
    private GameObject entity;
    private AnimationClip idleClip;
    private AnimationClip walkClip;
    private AnimationClip chaseClip;
    private AnimationClip attackClip;

    private AnimationClip currentPreviewClip;
    private float previewTime = 0f;
    private bool isPlaying = false;
    private double lastTime = 0;

    [MenuItem("Tools/Entity 38/Animation Previewer")]
    public static void ShowWindow()
    {
        var win = GetWindow<Entity38AnimationViewer>("Entity 38 Animations");
        win.minSize = new Vector2(360, 440);
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

    public void LoadClips()
    {
        entity = GameObject.Find("Entity38_Needlelimbs");
        
        idleClip = AssetDatabase.LoadAssetAtPath<AnimationClip>("Assets/Characters/Entity38/Animations/Entity38_Idle.anim");
        walkClip = AssetDatabase.LoadAssetAtPath<AnimationClip>("Assets/Characters/Entity38/Animations/Entity38_Walk.anim");
        chaseClip = AssetDatabase.LoadAssetAtPath<AnimationClip>("Assets/Characters/Entity38/Animations/Entity38_Chase.anim");
        attackClip = AssetDatabase.LoadAssetAtPath<AnimationClip>("Assets/Characters/Entity38/Animations/Entity38_Attack.anim");

        // Fallback to FBX sub-assets if standalone not yet refreshed
        if (idleClip == null || walkClip == null || attackClip == null)
        {
            string fbxPath = "Assets/Characters/Entity38/Entity38_Needlelimbs.fbx";
            foreach (var obj in AssetDatabase.LoadAllAssetsAtPath(fbxPath))
            {
                if (obj is AnimationClip clip)
                {
                    if (clip.name.Contains("Idle")) idleClip = clip;
                    else if (clip.name.Contains("Walk")) walkClip = clip;
                    else if (clip.name.Contains("Chase")) chaseClip = clip;
                    else if (clip.name.Contains("Attack")) attackClip = clip;
                }
            }
        }
    }

    void OnGUI()
    {
        EditorGUILayout.Space(10);
        EditorGUILayout.LabelField("Entity 38 Live Animation Player", EditorStyles.boldLabel);
        EditorGUILayout.HelpBox("Click any button below to watch Entity 38 perform the animation live in your Scene View and Game View!", MessageType.Info);

        if (entity == null)
        {
            entity = GameObject.Find("Entity38_Needlelimbs");
            if (entity == null)
            {
                EditorGUILayout.HelpBox("Entity38_Needlelimbs GameObject not found in active scene!", MessageType.Warning);
                return;
            }
        }

        EditorGUILayout.Space(10);
        GUI.backgroundColor = new Color(0.3f, 0.8f, 0.4f);
        if (GUILayout.Button("► PLAY IDLE (Deep Breathing)", GUILayout.Height(38)))
        {
            PlayAnimation(idleClip, "Entity38_Idle");
        }

        GUI.backgroundColor = new Color(0.4f, 0.7f, 1.0f);
        if (GUILayout.Button("► PLAY NORMAL WALK (Creepy Stalk)", GUILayout.Height(38)))
        {
            PlayAnimation(walkClip, "Entity38_Walk");
        }

        GUI.backgroundColor = new Color(1.0f, 0.65f, 0.2f);
        if (GUILayout.Button("► PLAY CHASE (Predatory Sprint)", GUILayout.Height(38)))
        {
            PlayAnimation(chaseClip, "Entity38_Chase");
        }

        GUI.backgroundColor = new Color(1.0f, 0.3f, 0.3f);
        if (GUILayout.Button("► PLAY ATTACK (Windup & Slash)", GUILayout.Height(38)))
        {
            PlayAnimation(attackClip, "Entity38_Attack");
        }

        GUI.backgroundColor = Color.white;
        EditorGUILayout.Space(15);

        if (currentPreviewClip != null && !Application.isPlaying)
        {
            EditorGUILayout.LabelField("Active Clip: " + currentPreviewClip.name, EditorStyles.boldLabel);
            EditorGUILayout.LabelField($"Time: {previewTime:F2}s / {currentPreviewClip.length:F2}s");

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

            float newTime = EditorGUILayout.Slider("Scrub Timeline", previewTime, 0f, currentPreviewClip.length);
            if (Mathf.Abs(newTime - previewTime) > 0.001f)
            {
                previewTime = newTime;
                SampleCurrent();
            }
        }
    }

    private void PlayAnimation(AnimationClip clip, string stateName)
    {
        LoadClips();

        // 1. In Play Mode: Trigger via Animator / AI component
        if (Application.isPlaying && entity != null)
        {
            var anim = entity.GetComponent<Animator>();
            if (anim != null)
            {
                if (!anim.enabled) anim.enabled = true;
                anim.CrossFade(stateName, 0.1f);
            }
            var ai = entity.GetComponent<Entity38EnemyAI>();
            if (ai != null)
            {
                ai.loopInPlace = true;
                ai.manualControl = true;
                if (stateName == "Entity38_Idle") ai.PlayAnimLoop("Entity38_Idle");
                else if (stateName == "Entity38_Walk") ai.PlayAnimLoop("Entity38_Walk");
                else if (stateName == "Entity38_Chase") ai.PlayAnimLoop("Entity38_Chase");
                else if (stateName == "Entity38_Attack") ai.TriggerAttackExternal();
            }
            return;
        }

        // 2. In Editor Mode: Use Unity's native AnimationMode API
        if (clip == null || entity == null)
        {
            Debug.LogError($"[Entity38AnimationViewer] Cannot play {stateName}: Clip or Entity is null!");
            return;
        }

        currentPreviewClip = clip;
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
        currentPreviewClip = null;
        previewTime = 0f;

        if (AnimationMode.InAnimationMode())
        {
            AnimationMode.StopAnimationMode();
        }

        if (idleClip != null && entity != null)
        {
            idleClip.SampleAnimation(entity, 0f);
        }
        SceneView.RepaintAll();
    }

    private void OnEditorUpdate()
    {
        if (Application.isPlaying) return;
        if (!isPlaying || currentPreviewClip == null || entity == null) return;

        double now = EditorApplication.timeSinceStartup;
        float dt = (float)(now - lastTime);
        lastTime = now;

        previewTime += dt;

        if (previewTime >= currentPreviewClip.length)
        {
            if (currentPreviewClip != attackClip)
            {
                previewTime = previewTime % currentPreviewClip.length;
            }
            else
            {
                previewTime = currentPreviewClip.length;
                isPlaying = false;
            }
        }

        SampleCurrent();
    }

    private void SampleCurrent()
    {
        if (currentPreviewClip == null || entity == null) return;

        if (!AnimationMode.InAnimationMode())
        {
            AnimationMode.StartAnimationMode();
        }

        AnimationMode.SampleAnimationClip(entity, currentPreviewClip, previewTime);
        SceneView.RepaintAll();
        UnityEditorInternal.InternalEditorUtility.RepaintAllViews();
        this.Repaint();
    }
}
