using UnityEngine;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine.SceneManagement;
using UnityEditor.SceneManagement;

public static class SetupMonster8PitStalker
{
    private const string FbxPath = "Assets/Characters/Monster8/Monster8.fbx";
    private const string MatPath = "Assets/Characters/Monster8/Materials/Mat_Monster8.mat";
    private const string ControllerPath = "Assets/Characters/Monster8/Monster8_AnimatorController.controller";
    private const string PrefabPath = "Assets/Characters/Monster8/Monster8_PitStalker.prefab";

    [InitializeOnLoadMethod]
    private static void OnProjectLoaded()
    {
        EditorApplication.delayCall += () =>
        {
            SetupPitMonster();
        };
    }

    [MenuItem("FLIP-FALL/Setup Monster 8 Pit Stalker")]
    public static void SetupPitMonster()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) return;
        Debug.Log("[SetupMonster8] Setting up Monster 8 Pit Stalker...");

        // 1. Verify / Configure Model Importer
        ModelImporter importer = AssetImporter.GetAtPath(FbxPath) as ModelImporter;
        if (importer != null)
        {
            bool needReimport = false;
            if (importer.animationType != ModelImporterAnimationType.Generic)
            {
                importer.animationType = ModelImporterAnimationType.Generic;
                needReimport = true;
            }
            if (Mathf.Abs(importer.globalScale - 0.025f) > 0.001f)
            {
                importer.globalScale = 0.025f;
                needReimport = true;
            }

            ModelImporterClipAnimation[] currentClips = importer.clipAnimations;
            if (currentClips == null || currentClips.Length < 2)
            {
                var idleClip = new ModelImporterClipAnimation
                {
                    name = "Monster8_Idle",
                    firstFrame = 0,
                    lastFrame = 59,
                    loopTime = true,
                    wrapMode = WrapMode.Loop
                };
                var jumpClip = new ModelImporterClipAnimation
                {
                    name = "Monster8_Jumpscare",
                    firstFrame = 0,
                    lastFrame = 99,
                    loopTime = false,
                    wrapMode = WrapMode.ClampForever
                };
                importer.clipAnimations = new[] { idleClip, jumpClip };
                needReimport = true;
            }

            if (needReimport)
            {
                importer.SaveAndReimport();
                Debug.Log("[SetupMonster8] Reimported Monster 8 FBX with Generic clips.");
            }
        }

        // 2. Load or Build Material
        Material mat = AssetDatabase.LoadAssetAtPath<Material>(MatPath);
        if (mat == null)
        {
            Shader urpLit = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
            mat = new Material(urpLit);
            mat.name = "Mat_Monster8";

            Texture2D albedo = AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/Characters/Monster8/Textures/body.png");
            Texture2D normal = AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/Characters/Monster8/Textures/body_n.png");
            Texture2D spec = AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/Characters/Monster8/Textures/body_spec.png");

            if (albedo != null) mat.SetTexture("_BaseMap", albedo);
            if (normal != null)
            {
                mat.SetTexture("_BumpMap", normal);
                mat.EnableKeyword("_NORMALMAP");
            }
            if (spec != null)
            {
                mat.SetTexture("_MetallicSpecGlossMap", spec);
                mat.SetFloat("_Smoothness", 0.55f);
            }

            if (!AssetDatabase.IsValidFolder("Assets/Characters/Monster8/Materials"))
            {
                AssetDatabase.CreateFolder("Assets/Characters/Monster8", "Materials");
            }
            AssetDatabase.CreateAsset(mat, MatPath);
            AssetDatabase.SaveAssets();
        }

        // 3. Load or Build Animator Controller
        AnimatorController controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(ControllerPath);
        AnimationClip idleAnimClip = null;
        AnimationClip jumpAnimClip = null;

        Object[] assets = AssetDatabase.LoadAllAssetsAtPath(FbxPath);
        foreach (var a in assets)
        {
            if (a is AnimationClip c)
            {
                if (c.name == "Monster8_Idle" || (idleAnimClip == null && c.name.Contains("Idle"))) idleAnimClip = c;
                else if (c.name == "Monster8_Jumpscare" || (jumpAnimClip == null && (c.name.Contains("Jumpscare") || c.name.Contains("Take")))) jumpAnimClip = c;
            }
        }

        if (controller == null)
        {
            controller = AnimatorController.CreateAnimatorControllerAtPath(ControllerPath);
            controller.AddParameter("Jumpscare", AnimatorControllerParameterType.Trigger);
            controller.AddParameter("AnimSpeed", AnimatorControllerParameterType.Float);
            controller.AddParameter("IsJumpscaring", AnimatorControllerParameterType.Bool);

            var rootSm = controller.layers[0].stateMachine;
            var idleState = rootSm.AddState("Idle");
            idleState.motion = idleAnimClip;

            var jumpState = rootSm.AddState("Jumpscare");
            jumpState.motion = jumpAnimClip;
            jumpState.speedParameterActive = true;
            jumpState.speedParameter = "AnimSpeed";

            var toJump = idleState.AddTransition(jumpState);
            toJump.AddCondition(AnimatorConditionMode.If, 0, "IsJumpscaring");
            toJump.duration = 0.15f;
            toJump.hasExitTime = false;

            var toIdle = jumpState.AddTransition(idleState);
            toIdle.AddCondition(AnimatorConditionMode.IfNot, 0, "IsJumpscaring");
            toIdle.duration = 0.25f;
            toIdle.hasExitTime = false;

            rootSm.defaultState = idleState;
            EditorUtility.SetDirty(controller);
            AssetDatabase.SaveAssets();
        }

        // 4. Find or Instantiate Monster8 in the scene
        GameObject existing = GameObject.Find("Monster8_PitStalker");
        GameObject fbxPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(FbxPath);
        if (fbxPrefab == null)
        {
            Debug.LogError($"[SetupMonster8] FBX not found at {FbxPath}");
            return;
        }

        GameObject monsterObj;
        if (existing != null)
        {
            monsterObj = existing;
            Debug.Log("[SetupMonster8] Updating existing Monster8_PitStalker instance in scene.");
        }
        else
        {
            monsterObj = (GameObject)PrefabUtility.InstantiatePrefab(fbxPrefab);
            monsterObj.name = "Monster8_PitStalker";
            Undo.RegisterCreatedObjectUndo(monsterObj, "Create Monster8_PitStalker");
        }

        // Position in Pit 4 (Signature Hidden Pit at X: 55 to 60.5, Center: 57.75)
        // Set home position at X: 58.8f, Y: -9.75f, Z: 50.0f
        monsterObj.transform.position = new Vector3(58.8f, -9.75f, 50.0f);
        monsterObj.transform.rotation = Quaternion.Euler(0f, -90f, 0f); // Facing West along the corridor towards approaching players
        monsterObj.transform.localScale = Vector3.one;

        // Try parenting to 02_PitCorridor if it exists
        GameObject pitCorridor = GameObject.Find("02_PitCorridor");
        if (pitCorridor != null)
        {
            monsterObj.transform.SetParent(pitCorridor.transform, true);
        }

        // Assign Material to SkinnedMeshRenderers
        var renderers = monsterObj.GetComponentsInChildren<SkinnedMeshRenderer>(true);
        foreach (var smr in renderers)
        {
            smr.sharedMaterial = mat;
            smr.receiveShadows = true;
            smr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;
        }

        // Assign Animator & Controller
        Animator anim = monsterObj.GetComponent<Animator>();
        if (anim == null) anim = monsterObj.AddComponent<Animator>();
        anim.runtimeAnimatorController = controller;
        anim.applyRootMotion = false;

        // Attach & Configure PitMonsterJumpscare
        PitMonsterJumpscare pitComp = monsterObj.GetComponent<PitMonsterJumpscare>();
        if (pitComp == null) pitComp = monsterObj.AddComponent<PitMonsterJumpscare>();
        pitComp.stareDelayBeforeJumpscare = 2.0f;
        pitComp.holdEndFrameDuration = 1.0f;
        pitComp.forwardJumpscareSpeed = 1.0f;
        pitComp.reverseJumpscareSpeed = -1.1f;
        pitComp.lockOnRotationSpeed = 6.0f;

        // Ensure Monster Illumination Light
        Transform existingLight = monsterObj.transform.Find("MonsterPitIlluminationLight");
        GameObject lightObj = existingLight != null ? existingLight.gameObject : new GameObject("MonsterPitIlluminationLight");
        lightObj.transform.SetParent(monsterObj.transform, false);
        lightObj.transform.localPosition = new Vector3(0f, 2.2f, 1.6f);
        Light mLight = lightObj.GetComponent<Light>();
        if (mLight == null) mLight = lightObj.AddComponent<Light>();
        mLight.type = LightType.Point;
        mLight.range = 10.0f;
        mLight.intensity = 3.6f;
        mLight.color = new Color(1.0f, 0.94f, 0.82f);
        mLight.shadows = LightShadows.None;
        mLight.renderMode = LightRenderMode.ForcePixel;

        // Audio Clips (attack screech excluded specifically for pit monster per user instructions)
        AudioClip roarClip = AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Audio/SFX/Monster/Monster_Chase_Roar.mp3");
        AudioClip growlClip = AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Audio/SFX/Monster/Monster_Lurking_Proximity.mp3");
        AudioClip pitFallClip = AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Audio/SFX/Monster/chiiri-monster-15-337349.mp3");
        if (pitFallClip == null)
            pitFallClip = AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Audio/SFX/Monster/Monster_Pit_Chiiri_Fall.mp3");

        pitComp.pitFallSfxClip = pitFallClip;

        AudioSource[] sources = monsterObj.GetComponents<AudioSource>();
        AudioSource actionSource = sources.Length > 0 ? sources[0] : monsterObj.AddComponent<AudioSource>();
        AudioSource growlSource = sources.Length > 1 ? sources[1] : monsterObj.AddComponent<AudioSource>();
        AudioSource pitFallSource = sources.Length > 2 ? sources[2] : monsterObj.AddComponent<AudioSource>();

        actionSource.playOnAwake = false;
        actionSource.loop = false;
        actionSource.spatialBlend = 0.5f;
        actionSource.minDistance = 3.0f;
        actionSource.maxDistance = 40.0f;
        actionSource.volume = 1.0f;

        growlSource.playOnAwake = true;
        growlSource.loop = true;
        growlSource.spatialBlend = 1.0f;
        growlSource.minDistance = 2.0f;
        growlSource.maxDistance = 9.0f; // Localized to hidden pit
        growlSource.volume = 0.45f;
        if (growlClip != null) growlSource.clip = growlClip;

        pitFallSource.playOnAwake = false;
        pitFallSource.loop = false;
        pitFallSource.spatialBlend = 0.35f;
        pitFallSource.minDistance = 3.5f;
        pitFallSource.maxDistance = 16.0f;
        pitFallSource.volume = 1.0f;
        if (pitFallClip != null) pitFallSource.clip = pitFallClip;

        // Save as Prefab
        PrefabUtility.SaveAsPrefabAssetAndConnect(monsterObj, PrefabPath, InteractionMode.AutomatedAction);
        Debug.Log($"[SetupMonster8] Saved prefab to {PrefabPath}");

        // Mark scene dirty and save
        var activeScene = SceneManager.GetActiveScene();
        if (activeScene.IsValid())
        {
            EditorSceneManager.MarkSceneDirty(activeScene);
            EditorSceneManager.SaveScene(activeScene);
            Debug.Log($"[SetupMonster8] Saved scene {activeScene.name}");
        }

        Debug.Log("[SetupMonster8] Monster 8 Pit Stalker successfully configured and placed in pit!");
    }
}
