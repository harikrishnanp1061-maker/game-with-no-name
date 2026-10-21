using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.Rendering.Universal;

// =========================================================================
// SetupFourPlayerCharacters - Editor Builder for 4 Physical Characters
//
// Positions all 4 player characters (P1 - P4) at spawn in Area 1 Crash Room
// around the crumpled elevator cab wreckage.
// Configures animated 3D models, Animator controllers, FirstPersonControllers,
// identity joint materials, and manager scripts.
// =========================================================================
public static class SetupFourPlayerCharacters
{
    private struct PlayerConfig
    {
        public PlayerIdentity identity;
        public string name;
        public Vector3 position;
        public Quaternion rotation;
    }

    [MenuItem("Tools/FlipFall/Build 4-Player Characters & WakeUp")]
    public static void Build()
    {
        Debug.Log("[SetupFourPlayerCharacters] Starting 4-Player cooperative setup...");

        // 1. CLEAN UP PREVIOUS PLAYERS AND STAND-INS
        string[] oldNames = {
            "Player_FPS", "Player_Cylinder", "Player4_Teammate_StandIn",
            "Player_1", "Player_2", "Player_3", "Player_4",
            "MultiPlayer_Manager"
        };

        foreach (string name in oldNames)
        {
            GameObject old = GameObject.Find(name);
            while (old != null)
            {
                Undo.DestroyObjectImmediate(old);
                old = GameObject.Find(name);
            }
        }

        // 2. LOAD ASSETS
        GameObject modelAsset = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Art/Characters/SK_Web_Player_Animated.fbx");
        if (modelAsset == null)
        {
            modelAsset = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Art/Characters/SK_Player_Animated.fbx");
        }

        // Ensure player locomotion transitions are completely wired
        SetupPlayerLocomotionTransitions.ConfigureTransitions();

        RuntimeAnimatorController animCtrl = AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>(
            "Assets/Characters/Player/Player_LocomotionController.controller");

        Material matBody = AssetDatabase.LoadAssetAtPath<Material>("Assets/Materials/Mat_Character_Body.mat");
        Material[] matJoints = new Material[]
        {
            AssetDatabase.LoadAssetAtPath<Material>("Assets/Materials/Mat_Player1_Joints.mat"),
            AssetDatabase.LoadAssetAtPath<Material>("Assets/Materials/Mat_Player2_Joints.mat"),
            AssetDatabase.LoadAssetAtPath<Material>("Assets/Materials/Mat_Player3_Joints.mat"),
            AssetDatabase.LoadAssetAtPath<Material>("Assets/Materials/Mat_Player4_Joints.mat")
        };

        // 3. CREATE MULTIPLAYER MANAGER
        GameObject mgrObj = new GameObject("MultiPlayer_Manager");
        Undo.RegisterCreatedObjectUndo(mgrObj, "Create MultiPlayer_Manager");
        mgrObj.transform.position = Vector3.zero;

        MultiPlayerCharacterManager multiMgr = mgrObj.AddComponent<MultiPlayerCharacterManager>();
        PlayerWakeUpSequence wakeUpSeq = mgrObj.AddComponent<PlayerWakeUpSequence>();
        PlayerSuffocationPitEffect suffocationEffect = mgrObj.AddComponent<PlayerSuffocationPitEffect>();
        AtmosphereAudioManager atmoMgr = mgrObj.AddComponent<AtmosphereAudioManager>();
        DevPlayerSwitchingPanel devPanel = mgrObj.AddComponent<DevPlayerSwitchingPanel>();
        PlayerCrosshair crosshair = mgrObj.AddComponent<PlayerCrosshair>();
        InteractionManager interactMgr = mgrObj.AddComponent<InteractionManager>();
        PlayerSession playerSession = mgrObj.AddComponent<PlayerSession>();

        // Wire wake-up, suffocation, and atmosphere audio clips
        SerializedObject soWake = new SerializedObject(wakeUpSeq);
        SerializedProperty pWake = soWake.FindProperty("_wakeUpSound");
        if (pWake != null) pWake.objectReferenceValue = AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Audio/SFX/Player/Player_WakeUp_Breath.wav");
        SerializedProperty pWakeDur = soWake.FindProperty("_wakeUpDuration");
        if (pWakeDur != null) pWakeDur.floatValue = 4.4f;
        soWake.ApplyModifiedProperties();


        SerializedObject soSuff = new SerializedObject(suffocationEffect);
        SerializedProperty pImpact = soSuff.FindProperty("_fallImpactClip");
        if (pImpact != null) pImpact.objectReferenceValue = AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Audio/SFX/Player/Player_Pit_Fall_Impact.mp3");
        SerializedProperty pSuff = soSuff.FindProperty("_suffocationClip");
        if (pSuff != null) pSuff.objectReferenceValue = AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Audio/SFX/Player/Player_Pit_Suffocation_From25s.mp3");
        SerializedProperty pSuffStart = soSuff.FindProperty("_suffocationAudioStartTime");
        if (pSuffStart != null) pSuffStart.floatValue = 0f;
        soSuff.ApplyModifiedProperties();

        SerializedObject soAtmo = new SerializedObject(atmoMgr);
        SerializedProperty pDrone = soAtmo.FindProperty("_demonicDroneClip");
        if (pDrone != null) pDrone.objectReferenceValue = AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Audio/SFX/Atmosphere/Atmosphere_Demonic_Drone_5Min.mp3");
        soAtmo.ApplyModifiedProperties();

        // 4. FIND OR CREATE MAIN CAMERA
        Camera mainCam = Camera.main;
        if (mainCam == null)
        {
            Camera[] allCams = UnityEngine.Object.FindObjectsByType<Camera>(FindObjectsSortMode.None);
            if (allCams.Length > 0) mainCam = allCams[0];
        }

        if (mainCam == null)
        {
            GameObject camObj = new GameObject("Main Camera");
            mainCam = camObj.AddComponent<Camera>();
            camObj.AddComponent<AudioListener>();
            camObj.tag = "MainCamera";
        }

        mainCam.nearClipPlane = 0.05f;
        mainCam.farClipPlane = 150f;
        mainCam.fieldOfView = 75f;
        mainCam.clearFlags = CameraClearFlags.SolidColor;
        mainCam.backgroundColor = new Color(0.012f, 0.012f, 0.015f);

        UniversalAdditionalCameraData camData = mainCam.GetComponent<UniversalAdditionalCameraData>();
        if (camData == null) camData = mainCam.gameObject.AddComponent<UniversalAdditionalCameraData>();
        camData.renderPostProcessing = true;
        camData.renderShadows = true;

        AudioListener audioListener = mainCam.GetComponent<AudioListener>();
        if (audioListener == null) audioListener = mainCam.gameObject.AddComponent<AudioListener>();

        // 5. CONFIGURE 4 PLAYER SPAWN POSITIONS IN AREA 1 CRASH ROOM (Around Crumpled Elevator Cab at (0, 0.4, 50))

        PlayerConfig[] configs = new PlayerConfig[]
        {
            new PlayerConfig {
                identity = PlayerIdentity.Player1,
                name = "Player_1",
                position = new Vector3(-2.4f, 0.05f, 45.8f),
                rotation = Quaternion.Euler(0f, 32f, 0f)
            },
            new PlayerConfig {
                identity = PlayerIdentity.Player2,
                name = "Player_2",
                position = new Vector3(0.8f, 0.05f, 47.6f),
                rotation = Quaternion.Euler(0f, -75f, 0f)
            },
            new PlayerConfig {
                identity = PlayerIdentity.Player3,
                name = "Player_3",
                position = new Vector3(-1.1f, 0.05f, 51.2f),
                rotation = Quaternion.Euler(0f, 130f, 0f)
            },
            new PlayerConfig {
                identity = PlayerIdentity.Player4,
                name = "Player_4",
                position = new Vector3(3.2f, 0.05f, 49.5f),
                rotation = Quaternion.Euler(0f, -115f, 0f)
            }
        };

        PlayerCharacter[] createdCharacters = new PlayerCharacter[4];

        for (int i = 0; i < 4; i++)
        {
            PlayerConfig cfg = configs[i];

            GameObject pObj = new GameObject(cfg.name);
            Undo.RegisterCreatedObjectUndo(pObj, "Create " + cfg.name);
            pObj.transform.position = cfg.position;
            pObj.transform.rotation = cfg.rotation;
            pObj.tag = "Player";

            // CharacterController
            CharacterController cc = pObj.AddComponent<CharacterController>();
            cc.height = 2.15f;
            cc.radius = 0.40f;
            cc.center = new Vector3(0f, 1.08f, 0f);
            cc.slopeLimit = 45f;
            cc.stepOffset = 0.35f;
            cc.skinWidth = 0.05f;

            // Camera Holder at base of character body (Y=0)
            GameObject camHolder = new GameObject("CameraHolder");
            camHolder.transform.SetParent(pObj.transform, false);
            camHolder.transform.localPosition = Vector3.zero;
            camHolder.transform.localRotation = Quaternion.identity;

            // FirstPersonController
            FirstPersonController fpc = pObj.AddComponent<FirstPersonController>();
            fpc.walkSpeed = 4.5f;
            fpc.sprintSpeed = 7.5f;
            fpc.crouchSpeed = 2.2f;
            fpc.jumpHeight = 1.25f;
            fpc.gravity = -20f;
            fpc.mouseSensitivity = 2.0f;
            fpc.standingHeight = 2.15f;
            fpc.crouchHeight = 1.35f;
            fpc.standingCamY = 1.82f;
            fpc.crouchCamY = 1.15f;
            fpc.killPlaneY = -15f;
            fpc.respawnY = 0.1f;
            fpc.playerCamera = mainCam != null ? mainCam.transform : camHolder.transform;
            fpc.IsControlled = (i == 0); // Player 1 starts controlled

            // Instantiate Animated 3D Character Model (scaled 1.30x for tall adult proportions ~2.00m)
            GameObject modelInst = null;
            if (modelAsset != null)
            {
                modelInst = (GameObject)PrefabUtility.InstantiatePrefab(modelAsset, pObj.transform);
                modelInst.name = "Model_Animated";
                modelInst.transform.localPosition = Vector3.zero;
                modelInst.transform.localRotation = Quaternion.identity;
                modelInst.transform.localScale = Vector3.one * 1.30f;

                // Configure Animator
                Animator anim = modelInst.GetComponent<Animator>();
                if (anim == null) anim = modelInst.AddComponent<Animator>();
                if (animCtrl != null) anim.runtimeAnimatorController = animCtrl;
                fpc.characterAnimator = anim;

                // Initialize in Unconscious pose (lying knocked out flat on the concrete floor)
                anim.Play("Unconscious", 0, 0f);
                anim.Update(0f);

                // Assign Materials
                Renderer[] rends = modelInst.GetComponentsInChildren<Renderer>();
                foreach (Renderer r in rends)
                {
                    if (r.gameObject.name.Contains("Joints") && matJoints[i] != null)
                    {
                        r.sharedMaterial = matJoints[i];
                    }
                    else if (matBody != null)
                    {
                        r.sharedMaterial = matBody;
                    }
                }
            }

            // Configure AudioSources and Clips for FirstPersonController (Mild Stone/Concrete Footsteps)
            AudioClip[] concreteSteps = new AudioClip[]
            {
                AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Audio/SFX/Player/Player_Footstep_01.wav"),
                AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Audio/SFX/Player/Player_Footstep_02.wav"),
                AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Audio/SFX/Player/Player_Footstep_03.wav"),
                AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Audio/SFX/Player/Player_Footstep_04.wav"),
                AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Audio/SFX/Player/Player_Footstep_05.wav"),
                AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Audio/SFX/Player/Player_Footstep_06.wav")
            };
            AudioClip fastBreathing = AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Audio/SFX/Player/Player_Breathing_Exhausted.mp3");
            AudioClip jumpSfx = AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Audio/SFX/Player/Player_Jump.wav");
            AudioClip landSfx = AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Audio/SFX/Player/Player_Land_Concrete.wav");

            AudioClip walkingTrack = AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Audio/SFX/Player/freesound_community-concrete-footsteps-6752.mp3");
            AudioClip runningTrack = AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Audio/SFX/Player/freesound_community-running-sounds-6003.mp3");

            SerializedObject soFpc = new SerializedObject(fpc);
            SerializedProperty pBreath = soFpc.FindProperty("fastBreathingClip");
            if (pBreath != null) pBreath.objectReferenceValue = fastBreathing;
            SerializedProperty pJump = soFpc.FindProperty("jumpClip");
            if (pJump != null) pJump.objectReferenceValue = jumpSfx;
            SerializedProperty pLand = soFpc.FindProperty("landClip");
            if (pLand != null) pLand.objectReferenceValue = landSfx;
            SerializedProperty pWalk = soFpc.FindProperty("walkingAudioTrack");
            if (pWalk != null) pWalk.objectReferenceValue = walkingTrack;
            SerializedProperty pRun = soFpc.FindProperty("runningAudioTrack");
            if (pRun != null) pRun.objectReferenceValue = runningTrack;
            SerializedProperty stepsProp = soFpc.FindProperty("footstepClips");
            if (stepsProp != null)
            {
                stepsProp.arraySize = concreteSteps.Length;
                for (int s = 0; s < concreteSteps.Length; s++)
                {
                    stepsProp.GetArrayElementAtIndex(s).objectReferenceValue = concreteSteps[s];
                }
            }
            soFpc.ApplyModifiedProperties();

            // Player 4 Torch Ability
            PlayerTorchAbility torch = null;
            if (cfg.identity == PlayerIdentity.Player4)
            {
                torch = pObj.AddComponent<PlayerTorchAbility>();
                fpc.torchAbility = torch;

                SerializedObject soTorch = new SerializedObject(torch);
                SerializedProperty pOn = soTorch.FindProperty("_torchClickOnClip");
                if (pOn != null) pOn.objectReferenceValue = AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Audio/SFX/Player/Torch_Click_On.wav");
                SerializedProperty pOff = soTorch.FindProperty("_torchClickOffClip");
                if (pOff != null) pOff.objectReferenceValue = AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Audio/SFX/Player/Torch_Click_Off.wav");
                soTorch.ApplyModifiedProperties();
            }

            // PlayerCharacter component
            PlayerCharacter pc = pObj.AddComponent<PlayerCharacter>();
            pc.Initialize(cfg.identity);
            createdCharacters[i] = pc;
        }

        // 6. LINK CHARACTERS IN MULTIPLAYER MANAGER
        SerializedObject soMgr = new SerializedObject(multiMgr);
        soMgr.FindProperty("_player1").objectReferenceValue = createdCharacters[0];
        soMgr.FindProperty("_player2").objectReferenceValue = createdCharacters[1];
        soMgr.FindProperty("_player3").objectReferenceValue = createdCharacters[2];
        soMgr.FindProperty("_player4").objectReferenceValue = createdCharacters[3];
        soMgr.FindProperty("_mainCamera").objectReferenceValue = mainCam;
        soMgr.FindProperty("_audioListener").objectReferenceValue = audioListener;
        soMgr.ApplyModifiedProperties();

        // 7. PARENT MAIN CAMERA TO PLAYER 1 CAMERA HOLDER
        mainCam.transform.SetParent(createdCharacters[0].CameraHolder, false);
        mainCam.transform.localPosition = new Vector3(0f, 1.70f, 0.1f);
        mainCam.transform.localRotation = Quaternion.identity;
        audioListener.transform.SetParent(createdCharacters[0].CameraHolder, false);
        audioListener.transform.localPosition = Vector3.zero;
        audioListener.transform.localRotation = Quaternion.identity;

        createdCharacters[0].SetControlled(true, mainCam, audioListener);
        createdCharacters[1].SetControlled(false);
        createdCharacters[2].SetControlled(false);
        createdCharacters[3].SetControlled(false);

        PlayerIdentityManager.ResetToDefault();

        // 8. SETUP ENTITY 38 MONSTER IN AREA 5 CREATURE ROOM
        try
        {
            SetupEntity38Character.ExecuteSetup();
            Debug.Log("[SetupFourPlayerCharacters] Entity 38 Needlelimbs monster positioned in Area 5 Creature Room!");
        }
        catch (System.Exception ex)
        {
            Debug.LogWarning("[SetupFourPlayerCharacters] Entity 38 setup warning: " + ex.Message);
        }

        // 9. WIRE FUSE CELL PICKUP SFX
        FuseCellPickup fusePickup = Object.FindAnyObjectByType<FuseCellPickup>();
        if (fusePickup != null)
        {
            SerializedObject soFuse = new SerializedObject(fusePickup);
            SerializedProperty pFuseSfx = soFuse.FindProperty("_pickupSound");
            if (pFuseSfx != null)
            {
                pFuseSfx.objectReferenceValue = AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Audio/SFX/Items/Item_Pickup_Equip.mp3");
                soFuse.ApplyModifiedProperties();
                Debug.Log("[SetupFourPlayerCharacters] Wired Item_Pickup_Equip.mp3 to FuseCellPickup.");
            }
        }

        // Mark scene dirty and save
        EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
        EditorSceneManager.SaveOpenScenes();

        Debug.Log("[SetupFourPlayerCharacters] Successfully built all 4 Player Characters with Waking Up sequence, Jog/Run/Crouch/Jump actions, Pit Suffocation respawn, and Entity 38 monster!");
    }
}
