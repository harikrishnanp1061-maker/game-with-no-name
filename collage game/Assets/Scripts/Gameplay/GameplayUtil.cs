using System;
using System.Collections.Generic;
using UnityEngine;
using System.Reflection;

// =========================================================================
// GameplayUtil - shared helpers for the FLIP//FALL gameplay scripts.
//
// Everything in here is deliberately dependency-free: no events, no state.
// It exists so each gameplay script does not re-implement player discovery,
// tag filtering, safe name matching and reflection lookups.
// =========================================================================
public static class GameplayUtil
{
    // -----------------------------------------------------------------
    // PLAYER DISCOVERY
    // -----------------------------------------------------------------
    /// <summary>
    /// Finds the local <see cref="PlayerSession"/>. Prefers the one already
    /// registered, and creates one lazily on the FirstPersonController if
    /// needed - so gameplay scripts keep working when a builder attaches
    /// FuseSwitch to a greybox cube and forgets about PlayerSession.
    /// </summary>
    public static PlayerSession FindPlayer()
    {
        PlayerSession session = PlayerSession.Local;
        if (session != null) return session;

        FirstPersonController fps = UnityEngine.Object.FindAnyObjectByType<FirstPersonController>();
        if (fps == null) return null;

        session = fps.GetComponent<PlayerSession>();
        if (session == null)
        {
            session = fps.gameObject.AddComponent<PlayerSession>();
        }
        return session;
    }

    /// <summary>
    /// True when the given game object is the local player or part of it.
    /// Handles the untagged-greybox case by falling back to the
    /// FirstPersonController ancestor search, so a missing "Player" tag does
    /// not silently break pit and reset detection.
    /// </summary>
    public static bool IsLocalPlayer(GameObject go)
    {
        if (go == null) return false;

        PlayerSession session = FindPlayer();
        if (session != null && session.Controller != null)
        {
            if (go == session.Controller.gameObject) return true;
            if (IsDescendantOf(go.transform, session.Controller.transform)) return true;
        }

        return go.GetComponentInParent<FirstPersonController>() != null;
    }

    /// <summary>True when <paramref name="child"/> is the same as or below <paramref name="parent"/>.</summary>
    public static bool IsDescendantOf(Transform child, Transform parent)
    {
        if (child == null || parent == null) return false;

        Transform t = child;
        int guard = 0;
        while (t != null && guard < 64)
        {
            if (t == parent) return true;
            t = t.parent;
            guard++;
        }
        return false;
    }

    // -----------------------------------------------------------------
    // INPUT
    // -----------------------------------------------------------------
    /// <summary>
    /// Was the interaction key pressed this frame? New Input System when
    /// ENABLE_INPUT_SYSTEM is defined, legacy Input otherwise, so the scripts
    /// keep working if the project setting is ever flipped back.
    /// </summary>
    public static bool WasInteractPressed()
    {
#if ENABLE_INPUT_SYSTEM
        UnityEngine.InputSystem.Keyboard kb = UnityEngine.InputSystem.Keyboard.current;
        UnityEngine.InputSystem.Mouse mouse = UnityEngine.InputSystem.Mouse.current;
        bool kbPressed = kb != null && kb.eKey.wasPressedThisFrame;
        bool mousePressed = mouse != null && mouse.leftButton.wasPressedThisFrame;
        return kbPressed || mousePressed;
#else
        return Input.GetKeyDown(KeyCode.E) || Input.GetMouseButtonDown(0);
#endif
    }

    // -----------------------------------------------------------------
    // NAMING
    // -----------------------------------------------------------------
    /// <summary>
    /// Finds the first scene object whose name matches any of the given
    /// strings, most specific first: exact match, then case-insensitive
    /// substring. Names with a "(1)" duplicate suffix still match.
    /// </summary>
    public static GameObject FindByNameContains(params string[] nameSubstrings)
    {
        if (nameSubstrings == null || nameSubstrings.Length == 0) return null;

        GameObject[] all = AllSceneObjects();
        if (all == null) return null;

        foreach (string needle in nameSubstrings)
        {
            if (string.IsNullOrEmpty(needle)) continue;

            foreach (GameObject candidate in all)
            {
                if (candidate == null) continue;
                if (string.Equals(candidate.name, needle, StringComparison.Ordinal)) return candidate;
            }
        }

        foreach (string needle in nameSubstrings)
        {
            if (string.IsNullOrEmpty(needle)) continue;

            foreach (GameObject candidate in all)
            {
                if (candidate == null) continue;
                if (MatchesName(candidate.name, needle)) return candidate;
            }
        }

        return null;
    }

    /// <summary>Case-insensitive "contains" that also tolerates "Name (1)" suffixes.</summary>
    public static bool MatchesName(string name, string needle)
    {
        if (string.IsNullOrEmpty(name) || string.IsNullOrEmpty(needle)) return false;

        int paren = name.IndexOf('(');
        string stripped = paren > 0 ? name.Substring(0, paren).Trim() : name;

        return stripped.IndexOf(needle, StringComparison.OrdinalIgnoreCase) >= 0
            || name.IndexOf(needle, StringComparison.OrdinalIgnoreCase) >= 0;
    }

    /// <summary>All scene objects, including inactive ones.</summary>
    public static GameObject[] AllSceneObjects()
    {
        return UnityEngine.Object.FindObjectsByType<GameObject>();
    }

    // -----------------------------------------------------------------
    // TAGS
    // -----------------------------------------------------------------
    public static bool TagExists(string tagName)
    {
        if (string.IsNullOrEmpty(tagName)) return false;

        // GetExistingTag is the only runtime way to test tag registration
        // without touching UnityEditor.TagManager. An unregistered tag comes
        // back as an empty handle; anything registered returns its name.
        try
        {
            return TagHandle.GetExistingTag(tagName).ToString() != null;
        }
        catch (Exception)
        {
            return false;
        }
    }

    /// <summary>
    /// Applies the tag only when it already exists. Adding a new tag at
    /// runtime is not possible in a build, and in the editor it rewrites
    /// TagManager.asset - so creation is left to the editor menu.
    /// Returns true if the tag was applied or was already set.
    /// </summary>
    public static bool TrySetTag(GameObject go, string tagName)
    {
        if (go == null || string.IsNullOrEmpty(tagName)) return false;
        if (go.tag == tagName) return true;

        // Assigning an unregistered tag throws, which is exactly the case we
        // want to report instead of crash.
        try
        {
            go.tag = tagName;
            return true;
        }
        catch (Exception)
        {
            return false;
        }
    }

    // -----------------------------------------------------------------
    // REFLECTION
    // -----------------------------------------------------------------
    /// <summary>
    /// Sets a bool field on the first component that declares it (root
    /// first, then descendants), public or private. Used to toggle a plain
    /// "bool powered" on a greybox elevator that has no ElevatorObjective.
    /// </summary>
    public static bool SetBoolField(GameObject target, string fieldName, bool value)
    {
        if (target == null || string.IsNullOrEmpty(fieldName)) return false;

        if (SetBoolFieldOn(target.GetComponents<Component>(), fieldName, value)) return true;
        return SetBoolFieldOn(target.GetComponentsInChildren<Component>(), fieldName, value);
    }

    private static bool SetBoolFieldOn(Component[] components, string fieldName, bool value)
    {
        if (components == null) return false;

        foreach (Component component in components)
        {
            if (component == null) continue;

            FieldInfo field = component.GetType().GetField(fieldName,
                BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
            if (field == null || field.FieldType != typeof(bool)) continue;

            try
            {
                field.SetValue(component, value);
                return true;
            }
            catch (Exception)
            {
                continue;
            }
        }
        return false;
    }

    // -----------------------------------------------------------------
    // SMALL MISC
    // -----------------------------------------------------------------
    /// <summary>Stable display name for an action performed by a player.</summary>
    public static string SafeName(PlayerSession player)
    {
        return player != null ? player.Name : "player";
    }
}
