using System.Collections.Generic;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

public static class SetupPlayerLocomotionTransitions
{
    private const string ControllerPath = "Assets/Characters/Player/Player_LocomotionController.controller";
    private const string FbxPath = "Assets/Art/Characters/SK_Web_Player_Animated.fbx";

    [MenuItem("Tools/FLIP//FALL/Configure Player Locomotion Transitions")]
    public static void ConfigureTransitions()
    {
        AnimatorController ctrl = AssetDatabase.LoadAssetAtPath<AnimatorController>(ControllerPath);
        if (ctrl == null)
        {
            Debug.LogError($"[SetupPlayerLocomotionTransitions] Controller not found at {ControllerPath}");
            return;
        }

        // 1. Load Animation Clips from SK_Web_Player_Animated.fbx
        Object[] subAssets = AssetDatabase.LoadAllAssetRepresentationsAtPath(FbxPath);
        Dictionary<string, AnimationClip> clips = new Dictionary<string, AnimationClip>();
        foreach (var sub in subAssets)
        {
            if (sub is AnimationClip clip)
            {
                clips[clip.name] = clip;
            }
        }
        Debug.Log($"[SetupPlayerLocomotionTransitions] Loaded {clips.Count} animation clips from {FbxPath}");

        AnimatorControllerLayer layer = ctrl.layers[0];
        AnimatorStateMachine sm = layer.stateMachine;

        // Map states
        Dictionary<string, AnimatorState> states = new Dictionary<string, AnimatorState>();
        foreach (ChildAnimatorState child in sm.states)
        {
            states[child.state.name] = child.state;
        }

        // Clear existing transitions
        foreach (var kvp in states)
        {
            kvp.Value.transitions = new AnimatorStateTransition[0];
        }
        sm.anyStateTransitions = new AnimatorStateTransition[0];

        // Ensure parameters exist
        EnsureParameter(ctrl, "Speed", AnimatorControllerParameterType.Float);
        EnsureParameter(ctrl, "IsCrouching", AnimatorControllerParameterType.Bool);
        EnsureParameter(ctrl, "IsGrounded", AnimatorControllerParameterType.Bool);
        EnsureParameter(ctrl, "HasTorch", AnimatorControllerParameterType.Bool);
        EnsureParameter(ctrl, "Jump", AnimatorControllerParameterType.Trigger);
        EnsureParameter(ctrl, "WakeUp", AnimatorControllerParameterType.Trigger);

        // =========================================================================
        // Assign Motion Clips to States
        // =========================================================================
        AssignMotion(states, "Idle", clips, "Anim_Idle", 1f);
        AssignMotion(states, "Jog", clips, "Anim_Jog", 1f);
        AssignMotion(states, "Run", clips, "Anim_Run", 1f);
        AssignMotion(states, "Crouch_Idle", clips, "Anim_Crouch_Idle", 1f);
        AssignMotion(states, "Crouch_Walk", clips, "Anim_Crouch_Walk", 1f);
        AssignMotion(states, "Idle_Torch", clips, "Anim_Idle_Torch", 1f);
        AssignMotion(states, "Jog_Torch", clips, "Anim_Jog_Torch", 1f);
        AssignMotion(states, "Run_Torch", clips, "Anim_Run_Torch", 1f);
        AssignMotion(states, "Crouch_Idle_Torch", clips, "Anim_Crouch_Idle_Torch", 1f);
        AssignMotion(states, "Crouch_Walk_Torch", clips, "Anim_Crouch_Walk_Torch", 1f);
        AssignMotion(states, "WakeUp", clips, "Anim_WakeUp_Stand", 1f);
        AssignMotion(states, "Jump", clips, "Anim_Jump_Improved", 1f);

        // =========================================================================
        // 0. Unconscious State (Frozen at frame 0, lying flat on the floor)
        // Default state so characters remain knocked out on the ground until triggered
        // =========================================================================
        AnimatorState stUnconscious = null;
        if (!states.TryGetValue("Unconscious", out stUnconscious))
        {
            stUnconscious = sm.AddState("Unconscious", new Vector3(50, 0, 0));
            states["Unconscious"] = stUnconscious;
        }

        if (clips.TryGetValue("Anim_WakeUp_Stand", out AnimationClip wakeClip))
        {
            stUnconscious.motion = wakeClip;
        }
        stUnconscious.speed = 0f;

        // Default state is Unconscious
        sm.defaultState = stUnconscious;

        // Unconscious -> WakeUp (Triggered by wakeup routine)
        if (states.ContainsKey("WakeUp"))
        {
            AnimatorStateTransition tWake = stUnconscious.AddTransition(states["WakeUp"]);
            tWake.hasExitTime = false;
            tWake.duration = 0.12f;
            tWake.AddCondition(AnimatorConditionMode.If, 0, "WakeUp");
        }

        // Unconscious emergency breakout if character actively moves (Speed > 0.1)
        if (states.ContainsKey("Jog"))
        {
            AnimatorStateTransition tBreak = stUnconscious.AddTransition(states["Jog"]);
            tBreak.hasExitTime = false;
            tBreak.duration = 0.15f;
            tBreak.AddCondition(AnimatorConditionMode.Greater, 0.1f, "Speed");
            tBreak.AddCondition(AnimatorConditionMode.IfNot, 0, "HasTorch");
        }
        if (states.ContainsKey("Jog_Torch"))
        {
            AnimatorStateTransition tBreakTorch = stUnconscious.AddTransition(states["Jog_Torch"]);
            tBreakTorch.hasExitTime = false;
            tBreakTorch.duration = 0.15f;
            tBreakTorch.AddCondition(AnimatorConditionMode.Greater, 0.1f, "Speed");
            tBreakTorch.AddCondition(AnimatorConditionMode.If, 0, "HasTorch");
        }

        // =========================================================================
        // 1. WakeUp -> Idle / Locomotion
        // =========================================================================
        if (states.ContainsKey("WakeUp") && states.ContainsKey("Idle"))
        {
            AnimatorStateTransition t = states["WakeUp"].AddTransition(states["Idle"]);
            t.hasExitTime = true;
            t.exitTime = 0.90f;
            t.duration = 0.30f;
            t.hasFixedDuration = false;
            t.AddCondition(AnimatorConditionMode.IfNot, 0, "HasTorch");
        }

        if (states.ContainsKey("WakeUp") && states.ContainsKey("Idle_Torch"))
        {
            AnimatorStateTransition t = states["WakeUp"].AddTransition(states["Idle_Torch"]);
            t.hasExitTime = true;
            t.exitTime = 0.90f;
            t.duration = 0.30f;
            t.hasFixedDuration = false;
            t.AddCondition(AnimatorConditionMode.If, 0, "HasTorch");
        }

        if (states.ContainsKey("WakeUp") && states.ContainsKey("Jog"))
        {
            AnimatorStateTransition t = states["WakeUp"].AddTransition(states["Jog"]);
            t.hasExitTime = false;
            t.duration = 0.20f;
            t.AddCondition(AnimatorConditionMode.Greater, 0.1f, "Speed");
            t.AddCondition(AnimatorConditionMode.IfNot, 0, "HasTorch");
        }
        if (states.ContainsKey("WakeUp") && states.ContainsKey("Jog_Torch"))
        {
            AnimatorStateTransition t = states["WakeUp"].AddTransition(states["Jog_Torch"]);
            t.hasExitTime = false;
            t.duration = 0.20f;
            t.AddCondition(AnimatorConditionMode.Greater, 0.1f, "Speed");
            t.AddCondition(AnimatorConditionMode.If, 0, "HasTorch");
        }

        // =========================================================================
        // 2. Normal Locomotion (Idle <-> Jog <-> Run)
        // =========================================================================
        if (states.ContainsKey("Idle") && states.ContainsKey("Jog"))
        {
            AnimatorStateTransition t = states["Idle"].AddTransition(states["Jog"]);
            t.hasExitTime = false;
            t.duration = 0.15f;
            t.AddCondition(AnimatorConditionMode.Greater, 0.1f, "Speed");
            t.AddCondition(AnimatorConditionMode.IfNot, 0, "IsCrouching");
            t.AddCondition(AnimatorConditionMode.IfNot, 0, "HasTorch");

            AnimatorStateTransition tBack = states["Jog"].AddTransition(states["Idle"]);
            tBack.hasExitTime = false;
            tBack.duration = 0.15f;
            tBack.AddCondition(AnimatorConditionMode.Less, 0.1f, "Speed");
        }

        if (states.ContainsKey("Jog") && states.ContainsKey("Run"))
        {
            AnimatorStateTransition t = states["Jog"].AddTransition(states["Run"]);
            t.hasExitTime = false;
            t.duration = 0.15f;
            t.AddCondition(AnimatorConditionMode.Greater, 1.5f, "Speed");
            t.AddCondition(AnimatorConditionMode.IfNot, 0, "IsCrouching");

            AnimatorStateTransition tBack = states["Run"].AddTransition(states["Jog"]);
            tBack.hasExitTime = false;
            tBack.duration = 0.15f;
            tBack.AddCondition(AnimatorConditionMode.Less, 1.5f, "Speed");
        }

        // Quick stop from Sprint to Idle
        if (states.ContainsKey("Run") && states.ContainsKey("Idle"))
        {
            AnimatorStateTransition tStop = states["Run"].AddTransition(states["Idle"]);
            tStop.hasExitTime = false;
            tStop.duration = 0.15f;
            tStop.AddCondition(AnimatorConditionMode.Less, 0.1f, "Speed");
        }

        // =========================================================================
        // 3. Torch Locomotion (Idle_Torch <-> Jog_Torch <-> Run_Torch)
        // =========================================================================
        if (states.ContainsKey("Idle_Torch") && states.ContainsKey("Jog_Torch"))
        {
            AnimatorStateTransition t = states["Idle_Torch"].AddTransition(states["Jog_Torch"]);
            t.hasExitTime = false;
            t.duration = 0.15f;
            t.AddCondition(AnimatorConditionMode.Greater, 0.1f, "Speed");
            t.AddCondition(AnimatorConditionMode.IfNot, 0, "IsCrouching");

            AnimatorStateTransition tBack = states["Jog_Torch"].AddTransition(states["Idle_Torch"]);
            tBack.hasExitTime = false;
            tBack.duration = 0.15f;
            tBack.AddCondition(AnimatorConditionMode.Less, 0.1f, "Speed");
        }

        if (states.ContainsKey("Jog_Torch") && states.ContainsKey("Run_Torch"))
        {
            AnimatorStateTransition t = states["Jog_Torch"].AddTransition(states["Run_Torch"]);
            t.hasExitTime = false;
            t.duration = 0.15f;
            t.AddCondition(AnimatorConditionMode.Greater, 1.5f, "Speed");
            t.AddCondition(AnimatorConditionMode.IfNot, 0, "IsCrouching");

            AnimatorStateTransition tBack = states["Run_Torch"].AddTransition(states["Jog_Torch"]);
            tBack.hasExitTime = false;
            tBack.duration = 0.15f;
            tBack.AddCondition(AnimatorConditionMode.Less, 1.5f, "Speed");
        }

        // Quick stop from Torch Sprint to Torch Idle
        if (states.ContainsKey("Run_Torch") && states.ContainsKey("Idle_Torch"))
        {
            AnimatorStateTransition tStop = states["Run_Torch"].AddTransition(states["Idle_Torch"]);
            tStop.hasExitTime = false;
            tStop.duration = 0.15f;
            tStop.AddCondition(AnimatorConditionMode.Less, 0.1f, "Speed");
        }

        // Cross Torch Toggling
        if (states.ContainsKey("Idle") && states.ContainsKey("Idle_Torch"))
        {
            AnimatorStateTransition t = states["Idle"].AddTransition(states["Idle_Torch"]);
            t.hasExitTime = false;
            t.duration = 0.20f;
            t.AddCondition(AnimatorConditionMode.If, 0, "HasTorch");

            AnimatorStateTransition tBack = states["Idle_Torch"].AddTransition(states["Idle"]);
            tBack.hasExitTime = false;
            tBack.duration = 0.20f;
            tBack.AddCondition(AnimatorConditionMode.IfNot, 0, "HasTorch");
        }

        if (states.ContainsKey("Jog") && states.ContainsKey("Jog_Torch"))
        {
            AnimatorStateTransition t = states["Jog"].AddTransition(states["Jog_Torch"]);
            t.hasExitTime = false;
            t.duration = 0.15f;
            t.AddCondition(AnimatorConditionMode.If, 0, "HasTorch");

            AnimatorStateTransition tBack = states["Jog_Torch"].AddTransition(states["Jog"]);
            tBack.hasExitTime = false;
            tBack.duration = 0.15f;
            tBack.AddCondition(AnimatorConditionMode.IfNot, 0, "HasTorch");
        }

        if (states.ContainsKey("Run") && states.ContainsKey("Run_Torch"))
        {
            AnimatorStateTransition t = states["Run"].AddTransition(states["Run_Torch"]);
            t.hasExitTime = false;
            t.duration = 0.15f;
            t.AddCondition(AnimatorConditionMode.If, 0, "HasTorch");

            AnimatorStateTransition tBack = states["Run_Torch"].AddTransition(states["Run"]);
            tBack.hasExitTime = false;
            tBack.duration = 0.15f;
            tBack.AddCondition(AnimatorConditionMode.IfNot, 0, "HasTorch");
        }

        // =========================================================================
        // 4. Normal Crouch Locomotion (Idle <-> Crouch_Idle <-> Crouch_Walk)
        // =========================================================================
        if (states.ContainsKey("Idle") && states.ContainsKey("Crouch_Idle"))
        {
            AnimatorStateTransition t = states["Idle"].AddTransition(states["Crouch_Idle"]);
            t.hasExitTime = false;
            t.duration = 0.20f;
            t.AddCondition(AnimatorConditionMode.If, 0, "IsCrouching");
            t.AddCondition(AnimatorConditionMode.IfNot, 0, "HasTorch");

            AnimatorStateTransition tBack = states["Crouch_Idle"].AddTransition(states["Idle"]);
            tBack.hasExitTime = false;
            tBack.duration = 0.20f;
            tBack.AddCondition(AnimatorConditionMode.IfNot, 0, "IsCrouching");
            tBack.AddCondition(AnimatorConditionMode.Less, 0.1f, "Speed");
        }

        if (states.ContainsKey("Crouch_Idle") && states.ContainsKey("Crouch_Walk"))
        {
            AnimatorStateTransition t = states["Crouch_Idle"].AddTransition(states["Crouch_Walk"]);
            t.hasExitTime = false;
            t.duration = 0.15f;
            t.AddCondition(AnimatorConditionMode.Greater, 0.1f, "Speed");

            AnimatorStateTransition tBack = states["Crouch_Walk"].AddTransition(states["Crouch_Idle"]);
            tBack.hasExitTime = false;
            tBack.duration = 0.15f;
            tBack.AddCondition(AnimatorConditionMode.Less, 0.1f, "Speed");
        }

        if (states.ContainsKey("Crouch_Walk") && states.ContainsKey("Idle"))
        {
            AnimatorStateTransition t = states["Crouch_Walk"].AddTransition(states["Idle"]);
            t.hasExitTime = false;
            t.duration = 0.20f;
            t.AddCondition(AnimatorConditionMode.IfNot, 0, "IsCrouching");
            t.AddCondition(AnimatorConditionMode.Less, 0.1f, "Speed");
        }

        if (states.ContainsKey("Jog") && states.ContainsKey("Crouch_Walk"))
        {
            AnimatorStateTransition t = states["Jog"].AddTransition(states["Crouch_Walk"]);
            t.hasExitTime = false;
            t.duration = 0.18f;
            t.AddCondition(AnimatorConditionMode.If, 0, "IsCrouching");

            AnimatorStateTransition tBack = states["Crouch_Walk"].AddTransition(states["Jog"]);
            tBack.hasExitTime = false;
            tBack.duration = 0.18f;
            tBack.AddCondition(AnimatorConditionMode.IfNot, 0, "IsCrouching");
            tBack.AddCondition(AnimatorConditionMode.Greater, 0.1f, "Speed");
        }

        if (states.ContainsKey("Run") && states.ContainsKey("Crouch_Walk"))
        {
            AnimatorStateTransition t = states["Run"].AddTransition(states["Crouch_Walk"]);
            t.hasExitTime = false;
            t.duration = 0.18f;
            t.AddCondition(AnimatorConditionMode.If, 0, "IsCrouching");
        }

        if (states.ContainsKey("Crouch_Idle") && states.ContainsKey("Jog"))
        {
            AnimatorStateTransition t = states["Crouch_Idle"].AddTransition(states["Jog"]);
            t.hasExitTime = false;
            t.duration = 0.20f;
            t.AddCondition(AnimatorConditionMode.IfNot, 0, "IsCrouching");
            t.AddCondition(AnimatorConditionMode.Greater, 0.1f, "Speed");
        }

        // =========================================================================
        // 5. Torch Crouch Locomotion
        // =========================================================================
        if (states.ContainsKey("Idle_Torch") && states.ContainsKey("Crouch_Idle_Torch"))
        {
            AnimatorStateTransition t = states["Idle_Torch"].AddTransition(states["Crouch_Idle_Torch"]);
            t.hasExitTime = false;
            t.duration = 0.20f;
            t.AddCondition(AnimatorConditionMode.If, 0, "IsCrouching");

            AnimatorStateTransition tBack = states["Crouch_Idle_Torch"].AddTransition(states["Idle_Torch"]);
            tBack.hasExitTime = false;
            tBack.duration = 0.20f;
            tBack.AddCondition(AnimatorConditionMode.IfNot, 0, "IsCrouching");
            tBack.AddCondition(AnimatorConditionMode.Less, 0.1f, "Speed");
        }

        if (states.ContainsKey("Crouch_Idle_Torch") && states.ContainsKey("Crouch_Walk_Torch"))
        {
            AnimatorStateTransition t = states["Crouch_Idle_Torch"].AddTransition(states["Crouch_Walk_Torch"]);
            t.hasExitTime = false;
            t.duration = 0.15f;
            t.AddCondition(AnimatorConditionMode.Greater, 0.1f, "Speed");

            AnimatorStateTransition tBack = states["Crouch_Walk_Torch"].AddTransition(states["Crouch_Idle_Torch"]);
            tBack.hasExitTime = false;
            tBack.duration = 0.15f;
            tBack.AddCondition(AnimatorConditionMode.Less, 0.1f, "Speed");
        }

        if (states.ContainsKey("Jog_Torch") && states.ContainsKey("Crouch_Walk_Torch"))
        {
            AnimatorStateTransition t = states["Jog_Torch"].AddTransition(states["Crouch_Walk_Torch"]);
            t.hasExitTime = false;
            t.duration = 0.18f;
            t.AddCondition(AnimatorConditionMode.If, 0, "IsCrouching");

            AnimatorStateTransition tBack = states["Crouch_Walk_Torch"].AddTransition(states["Jog_Torch"]);
            tBack.hasExitTime = false;
            tBack.duration = 0.18f;
            tBack.AddCondition(AnimatorConditionMode.IfNot, 0, "IsCrouching");
            tBack.AddCondition(AnimatorConditionMode.Greater, 0.1f, "Speed");
        }

        if (states.ContainsKey("Run_Torch") && states.ContainsKey("Crouch_Walk_Torch"))
        {
            AnimatorStateTransition t = states["Run_Torch"].AddTransition(states["Crouch_Walk_Torch"]);
            t.hasExitTime = false;
            t.duration = 0.18f;
            t.AddCondition(AnimatorConditionMode.If, 0, "IsCrouching");
        }

        if (states.ContainsKey("Crouch_Walk_Torch") && states.ContainsKey("Idle_Torch"))
        {
            AnimatorStateTransition t = states["Crouch_Walk_Torch"].AddTransition(states["Idle_Torch"]);
            t.hasExitTime = false;
            t.duration = 0.20f;
            t.AddCondition(AnimatorConditionMode.IfNot, 0, "IsCrouching");
            t.AddCondition(AnimatorConditionMode.Less, 0.1f, "Speed");
        }

        if (states.ContainsKey("Crouch_Idle") && states.ContainsKey("Crouch_Idle_Torch"))
        {
            AnimatorStateTransition t = states["Crouch_Idle"].AddTransition(states["Crouch_Idle_Torch"]);
            t.hasExitTime = false;
            t.duration = 0.20f;
            t.AddCondition(AnimatorConditionMode.If, 0, "HasTorch");

            AnimatorStateTransition tBack = states["Crouch_Idle_Torch"].AddTransition(states["Crouch_Idle"]);
            tBack.hasExitTime = false;
            tBack.duration = 0.20f;
            tBack.AddCondition(AnimatorConditionMode.IfNot, 0, "HasTorch");
        }

        if (states.ContainsKey("Crouch_Walk") && states.ContainsKey("Crouch_Walk_Torch"))
        {
            AnimatorStateTransition t = states["Crouch_Walk"].AddTransition(states["Crouch_Walk_Torch"]);
            t.hasExitTime = false;
            t.duration = 0.20f;
            t.AddCondition(AnimatorConditionMode.If, 0, "HasTorch");

            AnimatorStateTransition tBack = states["Crouch_Walk_Torch"].AddTransition(states["Crouch_Walk"]);
            tBack.hasExitTime = false;
            tBack.duration = 0.20f;
            tBack.AddCondition(AnimatorConditionMode.IfNot, 0, "HasTorch");
        }

        // =========================================================================
        // 6. AnyState Triggers (WakeUp & Jump)
        // =========================================================================
        if (states.ContainsKey("WakeUp"))
        {
            AnimatorStateTransition t = sm.AddAnyStateTransition(states["WakeUp"]);
            t.canTransitionToSelf = false;
            t.hasExitTime = false;
            t.duration = 0.10f;
            t.AddCondition(AnimatorConditionMode.If, 0, "WakeUp");
        }

        if (states.ContainsKey("Jump"))
        {
            AnimatorStateTransition t = sm.AddAnyStateTransition(states["Jump"]);
            t.canTransitionToSelf = false;
            t.hasExitTime = false;
            t.duration = 0.10f;
            t.AddCondition(AnimatorConditionMode.If, 0, "Jump");

            if (states.ContainsKey("Idle"))
            {
                AnimatorStateTransition tExit = states["Jump"].AddTransition(states["Idle"]);
                tExit.hasExitTime = true;
                tExit.exitTime = 0.85f;
                tExit.duration = 0.20f;
                tExit.AddCondition(AnimatorConditionMode.IfNot, 0, "HasTorch");
            }

            if (states.ContainsKey("Idle_Torch"))
            {
                AnimatorStateTransition tExit = states["Jump"].AddTransition(states["Idle_Torch"]);
                tExit.hasExitTime = true;
                tExit.exitTime = 0.85f;
                tExit.duration = 0.20f;
                tExit.AddCondition(AnimatorConditionMode.If, 0, "HasTorch");
            }
        }

        EditorUtility.SetDirty(ctrl);
        AssetDatabase.SaveAssets();
        Debug.Log("[SetupPlayerLocomotionTransitions] Successfully configured all player transitions and animation clips in Player_LocomotionController!");
    }

    private static void AssignMotion(Dictionary<string, AnimatorState> states, string stateName, Dictionary<string, AnimationClip> clips, string clipName, float speed)
    {
        if (states.TryGetValue(stateName, out AnimatorState st))
        {
            if (clips.TryGetValue(clipName, out AnimationClip clip))
            {
                st.motion = clip;
                st.speed = speed;
                Debug.Log($"[SetupPlayerLocomotionTransitions] Assigned clip '{clipName}' to state '{stateName}' (speed={speed})");
            }
            else
            {
                Debug.LogWarning($"[SetupPlayerLocomotionTransitions] Clip '{clipName}' not found for state '{stateName}'");
            }
        }
    }

    private static void EnsureParameter(AnimatorController ctrl, string name, AnimatorControllerParameterType type)
    {
        foreach (var p in ctrl.parameters)
        {
            if (p.name == name) return;
        }
        ctrl.AddParameter(name, type);
    }
}
