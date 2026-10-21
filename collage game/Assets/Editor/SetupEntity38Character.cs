using System.IO;
using UnityEngine;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine.Rendering.Universal;

public static class SetupEntity38Character
{
    private const string FbxPath = "Assets/Characters/Entity38/SK_Entity_38_Needlelimbs_Animated.fbx";
    private const string AlbedoPath = "Assets/Characters/Entity38/Textures/Entity38_Albedo.png";
    private const string MetallicRoughnessPath = "Assets/Characters/Entity38/Textures/Entity38_MetallicRoughness.png";
    private const string MatDir = "Assets/Characters/Entity38/Materials";
    private const string MatPath = "Assets/Characters/Entity38/Materials/Mat_Entity38_Needlelimbs.mat";
    private const string ControllerPath = "Assets/Characters/Entity38/Entity38_AnimatorController.controller";
    private const string PrefabPath1 = "Assets/Characters/Entity38/Entity38_Needlelimbs.prefab";
    private const string PrefabPath2 = "Assets/ReadyMadeAssets/Entity38/Prefabs/Entity38_Needlelimbs.prefab";

    [MenuItem("Tools/Setup Entity 38 Character")]
    public static void ExecuteSetup()
    {
        Debug.Log("[SetupEntity38] Setting up Upright Animated Entity 38 Needlelimbs character...");

        // 1. Ensure folders exist
        if (!AssetDatabase.IsValidFolder("Assets/Characters"))
            AssetDatabase.CreateFolder("Assets", "Characters");
        if (!AssetDatabase.IsValidFolder("Assets/Characters/Entity38"))
            AssetDatabase.CreateFolder("Assets/Characters", "Entity38");
        if (!AssetDatabase.IsValidFolder(MatDir))
            AssetDatabase.CreateFolder("Assets/Characters/Entity38", "Materials");

        // 2. Configure ModelImporter for the FBX
        ModelImporter importer = AssetImporter.GetAtPath(FbxPath) as ModelImporter;
        if (importer != null)
        {
            importer.animationType = ModelImporterAnimationType.Generic;
            var defaultClips = importer.defaultClipAnimations;
            var clipList = new System.Collections.Generic.List<ModelImporterClipAnimation>();

            foreach (var clip in defaultClips)
            {
                string takeName = clip.name;
                if (takeName.Contains("Idle"))
                {
                    clip.name = "Entity38_Idle";
                    clip.loopTime = true;
                }
                else if (takeName.Contains("Walk"))
                {
                    clip.name = "Entity38_Walk";
                    clip.loopTime = true;
                }
                else if (takeName.Contains("Run") || takeName.Contains("Chase"))
                {
                    clip.name = "Entity38_Chase";
                    clip.loopTime = true;
                }
                else if (takeName.Contains("Attack"))
                {
                    clip.name = "Entity38_Attack";
                    clip.loopTime = false;
                }
                clipList.Add(clip);
            }
            if (clipList.Count > 0)
            {
                importer.clipAnimations = clipList.ToArray();
            }
            importer.SaveAndReimport();
        }

        // 3. Create / Configure Material
        Shader urpLit = Shader.Find("Universal Render Pipeline/Lit");
        if (urpLit == null) urpLit = Shader.Find("Standard");

        Material mat = AssetDatabase.LoadAssetAtPath<Material>(MatPath);
        if (mat == null)
        {
            mat = new Material(urpLit);
            AssetDatabase.CreateAsset(mat, MatPath);
        }
        else
        {
            mat.shader = urpLit;
        }

        Texture2D albedoTex = AssetDatabase.LoadAssetAtPath<Texture2D>(AlbedoPath);
        Texture2D metallicTex = AssetDatabase.LoadAssetAtPath<Texture2D>(MetallicRoughnessPath);

        if (albedoTex != null)
        {
            if (mat.HasProperty("_BaseMap")) mat.SetTexture("_BaseMap", albedoTex);
            else if (mat.HasProperty("_MainTex")) mat.SetTexture("_MainTex", albedoTex);
        }
        if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", Color.white);
        else mat.color = Color.white;

        if (metallicTex != null)
        {
            if (mat.HasProperty("_MetallicGlossMap")) mat.SetTexture("_MetallicGlossMap", metallicTex);
        }
        if (mat.HasProperty("_Smoothness")) mat.SetFloat("_Smoothness", 0.55f);
        if (mat.HasProperty("_Metallic")) mat.SetFloat("_Metallic", 0.1f);

        EditorUtility.SetDirty(mat);
        AssetDatabase.SaveAssets();

        // 4. Create Animator Controller with all 4 animation states
        AnimatorController controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(ControllerPath);
        if (controller == null)
        {
            controller = AnimatorController.CreateAnimatorControllerAtPath(ControllerPath);
        }

        AnimationClip idleClip = null, walkClip = null, chaseClip = null, attackClip = null;
        Object[] assets = AssetDatabase.LoadAllAssetsAtPath(FbxPath);
        foreach (var obj in assets)
        {
            if (obj is AnimationClip clip && !clip.name.StartsWith("__preview__"))
            {
                if (clip.name == "Entity38_Idle" || (idleClip == null && clip.name.Contains("Idle"))) idleClip = clip;
                else if (clip.name == "Entity38_Walk" || (walkClip == null && clip.name.Contains("Walk"))) walkClip = clip;
                else if (clip.name == "Entity38_Chase" || (chaseClip == null && (clip.name.Contains("Chase") || clip.name.Contains("Run")))) chaseClip = clip;
                else if (clip.name == "Entity38_Attack" || (attackClip == null && clip.name.Contains("Attack"))) attackClip = clip;
            }
        }

        if (controller.layers.Length > 0)
        {
            var rootStateMachine = controller.layers[0].stateMachine;
            foreach (var state in rootStateMachine.states)
            {
                rootStateMachine.RemoveState(state.state);
            }

            var chaseState = rootStateMachine.AddState("Entity38_Chase");
            chaseState.motion = chaseClip != null ? chaseClip : walkClip;

            var walkState = rootStateMachine.AddState("Entity38_Walk");
            walkState.motion = walkClip != null ? walkClip : chaseClip;

            var idleState = rootStateMachine.AddState("Entity38_Idle");
            idleState.motion = idleClip != null ? idleClip : chaseClip;

            var attackState = rootStateMachine.AddState("Entity38_Attack");
            attackState.motion = attackClip != null ? attackClip : chaseClip;

            rootStateMachine.defaultState = idleState;
            EditorUtility.SetDirty(controller);
            AssetDatabase.SaveAssets();
        }

        // 5. Instantiate and place in active scene
        GameObject existing = GameObject.Find("Entity38_Needlelimbs");
        if (existing != null)
        {
            Undo.DestroyObjectImmediate(existing);
        }

        GameObject fbxPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(FbxPath);
        if (fbxPrefab == null)
        {
            Debug.LogError($"[SetupEntity38] Could not load FBX prefab at {FbxPath}");
            return;
        }

        GameObject characterInstance = (GameObject)PrefabUtility.InstantiatePrefab(fbxPrefab);
        characterInstance.name = "Entity38_Needlelimbs";
        Undo.RegisterCreatedObjectUndo(characterInstance, "Spawn Entity 38 Needlelimbs");

        // Scale: 2x scale (4.34m tall, towering over the player)
        characterInstance.transform.localScale = new Vector3(2.0f, 2.0f, 2.0f);

        // Position: In Area 5 Creature Room (X: 88.0f, Y: 0.05f, Z: 11.0f)
        GameObject creatureRoom = GameObject.Find("05_CreatureRoom");
        if (creatureRoom != null)
        {
            characterInstance.transform.SetParent(creatureRoom.transform, true);
        }
        characterInstance.transform.position = new Vector3(88.0f, 0.05f, 11.0f);
        characterInstance.transform.rotation = Quaternion.Euler(0f, -90f, 0f); // Prowling the chamber

        // Apply Material to all SkinnedMeshRenderers and MeshRenderers
        foreach (var smr in characterInstance.GetComponentsInChildren<SkinnedMeshRenderer>(true))
        {
            smr.sharedMaterial = mat;
            smr.updateWhenOffscreen = true;
        }
        foreach (var mr in characterInstance.GetComponentsInChildren<MeshRenderer>(true))
        {
            mr.sharedMaterial = mat;
        }

        // Hook up Animator
        Animator anim = characterInstance.GetComponent<Animator>();
        if (anim == null) anim = characterInstance.AddComponent<Animator>();
        anim.runtimeAnimatorController = controller;
        anim.applyRootMotion = false;

        // Hook up CharacterController (scaled for 4.34m upright monster)
        CharacterController cc = characterInstance.GetComponent<CharacterController>();
        if (cc == null) cc = characterInstance.AddComponent<CharacterController>();
        cc.height = 2.2f;
        cc.radius = 0.35f;
        cc.center = new Vector3(0f, 1.1f, 0f);
        cc.stepOffset = 0.45f;
        cc.slopeLimit = 65.0f;
        cc.skinWidth = 0.04f;

        // Hook up Entity38EnemyAI
        Entity38EnemyAI ai = characterInstance.GetComponent<Entity38EnemyAI>();
        if (ai == null) ai = characterInstance.AddComponent<Entity38EnemyAI>();
        ai.manualControl = false;
        ai.loopInPlace = false;
        ai.showDebugGUI = false;
        ai.canAttack = true;
        ai.verticalModelOffset = 0.0f; // Pivot is already at floor level
        ai.detectionRange = 50.0f;
        ai.chaseRange = 35.0f;
        ai.attackRange = 3.2f;
        ai.stalkSpeed = 2.2f;
        ai.chaseSpeed = 4.4f;
        ai.chaseAnimSpeed = 1.6f;
        ai.rotationSpeed = 10.0f;
        ai.attackCooldown = 1.4f;

        // Wire Monster SFX Audio Clips
        SerializedObject soAi = new SerializedObject(ai);
        SerializedProperty pScreech = soAi.FindProperty("noticeAttackScreechClip");
        if (pScreech != null) pScreech.objectReferenceValue = AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Audio/SFX/Monster/Monster_Screech_Notice_Attack.mp3");
        SerializedProperty pLurk = soAi.FindProperty("proximityLurkingClip");
        if (pLurk != null) pLurk.objectReferenceValue = AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Audio/SFX/Monster/Monster_Lurking_Proximity.mp3");
        SerializedProperty pBreath = soAi.FindProperty("closeUpGhostBreathClip");
        if (pBreath != null) pBreath.objectReferenceValue = AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Audio/SFX/Monster/Monster_Attack_GhostBreath.mp3");
        SerializedProperty pRoar = soAi.FindProperty("chaseRoarClip");
        if (pRoar != null) pRoar.objectReferenceValue = AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Audio/SFX/Monster/Monster_Chase_Roar.mp3");
        soAi.ApplyModifiedProperties();

        // Ensure Player_FPS has Player tag for robust AI targeting
        GameObject playerObj = GameObject.Find("Player_FPS");
        if (playerObj != null && playerObj.tag != "Player")
        {
            playerObj.tag = "Player";
        }

        // Save Prefabs
        try
        {
            PrefabUtility.SaveAsPrefabAssetAndConnect(characterInstance, PrefabPath1, InteractionMode.AutomatedAction);
            PrefabUtility.SaveAsPrefabAsset(characterInstance, PrefabPath2);
            Debug.Log($"[SetupEntity38] Saved prefabs to {PrefabPath1} and {PrefabPath2}");
        }
        catch (System.Exception ex)
        {
            Debug.LogWarning($"[SetupEntity38] Warning saving prefab: {ex.Message}");
        }

        if (walkClip != null)
        {
            Debug.Log($"[SetupEntity38] Animation confirmed: '{walkClip.name}' ({walkClip.length:F2}s, {walkClip.frameRate * walkClip.length:F0} frames) hooked up to Animator and Entity38EnemyAI.");
        }
        else
        {
            Debug.Log("[SetupEntity38] No animation clip found in model.");
        }

        // Frame and focus in Scene View
        Selection.activeGameObject = characterInstance;
        if (SceneView.lastActiveSceneView != null)
        {
            SceneView.lastActiveSceneView.FrameSelected();
            SceneView.lastActiveSceneView.Repaint();
        }

        // Mark scene dirty and save
        UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(characterInstance.scene);
        UnityEditor.SceneManagement.EditorSceneManager.SaveScene(characterInstance.scene);

        Debug.Log($"[SetupEntity38] Successfully spawned Upright Entity 38 in Creature Room at pos {characterInstance.transform.position}, scale {characterInstance.transform.localScale} (4.34m tall, towering over player)!");
    }
}
