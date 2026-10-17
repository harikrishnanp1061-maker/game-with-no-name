using UnityEngine;
#if ENABLE_INPUT_SYSTEM && !ENABLE_LEGACY_INPUT_MANAGER
using UnityEngine.InputSystem;
#endif

namespace HollowTower
{
    /// <summary>
    /// Works with both the old Input Manager and the new Input System package,
    /// so the project runs no matter which one the Unity install has switched on.
    /// </summary>
    public static class InputWrap
    {
#if ENABLE_LEGACY_INPUT_MANAGER || !ENABLE_INPUT_SYSTEM
        static bool PHeld(KeyCode k) { return Input.GetKey(k); }
        static bool PDown(KeyCode k) { return Input.GetKeyDown(k); }
        static Vector2 PMouse
        {
            get { return new Vector2(Input.GetAxisRaw("Mouse X"), Input.GetAxisRaw("Mouse Y")); }
        }
#else
        static bool Raw(KeyCode k)
        {
            var mouse = Mouse.current;
            if (k == KeyCode.Mouse0) return mouse != null && mouse.leftButton.isPressed;
            if (k == KeyCode.Mouse1) return mouse != null && mouse.rightButton.isPressed;
            var kb = Keyboard.current;
            if (kb == null) return false;
            Key key = Map(k);
            if (key == Key.None) return false;
            return kb[key].isPressed;
        }

        static bool WasPressed(KeyCode k)
        {
            var mouse = Mouse.current;
            if (k == KeyCode.Mouse0) return mouse != null && mouse.leftButton.wasPressedThisFrame;
            if (k == KeyCode.Mouse1) return mouse != null && mouse.rightButton.wasPressedThisFrame;
            var kb = Keyboard.current;
            if (kb == null) return false;
            Key key = Map(k);
            if (key == Key.None) return false;
            return kb[key].wasPressedThisFrame;
        }

        static Key Map(KeyCode k)
        {
            switch (k)
            {
                case KeyCode.W: return Key.W;
                case KeyCode.A: return Key.A;
                case KeyCode.S: return Key.S;
                case KeyCode.D: return Key.D;
                case KeyCode.E: return Key.E;
                case KeyCode.Q: return Key.Q;
                case KeyCode.R: return Key.R;
                case KeyCode.F: return Key.F;
                case KeyCode.H: return Key.H;
                case KeyCode.M: return Key.M;
                case KeyCode.N: return Key.N;
                case KeyCode.C: return Key.C;
                case KeyCode.Space: return Key.Space;
                case KeyCode.LeftShift: return Key.LeftShift;
                case KeyCode.LeftControl: return Key.LeftCtrl;
                case KeyCode.Escape: return Key.Escape;
                case KeyCode.Return: return Key.Enter;
                case KeyCode.Tab: return Key.Tab;
                case KeyCode.X: return Key.X;
                default: return Key.None;
            }
        }

        static bool PHeld(KeyCode k) { return Raw(k); }
        static bool PDown(KeyCode k) { return WasPressed(k); }
        static Vector2 PMouse
        {
            get
            {
                var mouse = Mouse.current;
                if (mouse == null) return Vector2.zero;
                return mouse.delta.ReadValue() * 0.1f;
            }
        }
#endif

        public static bool Held(KeyCode k) { return Bot.Active ? Bot.held.Contains(k) : PHeld(k); }
        public static bool Down(KeyCode k) { return Bot.Active ? Bot.down.Contains(k) : PDown(k); }
        public static Vector2 MouseDelta { get { return Bot.Active ? Bot.mouse : PMouse; } }

        /// <summary>True when the game owns the mouse (or the -playtest bot is driving), so clicks may fire the gun.</summary>
        public static bool GameHasMouse { get { return Bot.Active || (Application.isFocused && Cursor.lockState == CursorLockMode.Locked); } }

        /// <summary>Virtual keyboard and mouse for the automated play test (PlayTest.cs); replaces real input while Active.</summary>
        public static class Bot
        {
            public static bool Active;
            public static readonly System.Collections.Generic.HashSet<KeyCode> held = new System.Collections.Generic.HashSet<KeyCode>();
            public static readonly System.Collections.Generic.HashSet<KeyCode> down = new System.Collections.Generic.HashSet<KeyCode>();
            public static Vector2 mouse;
        }

        public static Vector2 Move
        {
            get
            {
                float x = (Held(KeyCode.D) ? 1f : 0f) - (Held(KeyCode.A) ? 1f : 0f);
                float y = (Held(KeyCode.W) ? 1f : 0f) - (Held(KeyCode.S) ? 1f : 0f);
                var v = new Vector2(x, y);
                return v.sqrMagnitude > 1f ? v.normalized : v;
            }
        }
    }
}
