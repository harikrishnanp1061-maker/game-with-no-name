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
        static Vector2 PMousePos { get { return Input.mousePosition; } }
        static float PScroll { get { return Input.mouseScrollDelta.y; } }
#else
        static bool Raw(KeyCode k)
        {
            var mouse = Mouse.current;
            if (k == KeyCode.Mouse0) return mouse != null && mouse.leftButton.isPressed;
            if (k == KeyCode.Mouse1) return mouse != null && mouse.rightButton.isPressed;
            if (k == KeyCode.Mouse2) return mouse != null && mouse.middleButton.isPressed;
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
            if (k == KeyCode.Mouse2) return mouse != null && mouse.middleButton.wasPressedThisFrame;
            var kb = Keyboard.current;
            if (kb == null) return false;
            Key key = Map(k);
            if (key == Key.None) return false;
            return kb[key].wasPressedThisFrame;
        }

        static Key Map(KeyCode k)
        {
            // Letters, digits and F-keys are laid out contiguously in both enums.
            if (k >= KeyCode.A && k <= KeyCode.Z) return Key.A + (k - KeyCode.A);
            if (k >= KeyCode.Alpha1 && k <= KeyCode.Alpha9) return Key.Digit1 + (k - KeyCode.Alpha1);
            if (k >= KeyCode.F1 && k <= KeyCode.F12) return Key.F1 + (k - KeyCode.F1);
            switch (k)
            {
                case KeyCode.Alpha0: return Key.Digit0;
                case KeyCode.Space: return Key.Space;
                case KeyCode.LeftShift: return Key.LeftShift;
                case KeyCode.RightShift: return Key.RightShift;
                case KeyCode.LeftControl: return Key.LeftCtrl;
                case KeyCode.RightControl: return Key.RightCtrl;
                case KeyCode.LeftAlt: return Key.LeftAlt;
                case KeyCode.Escape: return Key.Escape;
                case KeyCode.Return: return Key.Enter;
                case KeyCode.Tab: return Key.Tab;
                case KeyCode.UpArrow: return Key.UpArrow;
                case KeyCode.DownArrow: return Key.DownArrow;
                case KeyCode.LeftArrow: return Key.LeftArrow;
                case KeyCode.RightArrow: return Key.RightArrow;
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
        static Vector2 PMousePos
        {
            get
            {
                var mouse = Mouse.current;
                return mouse != null ? mouse.position.ReadValue() : Vector2.zero;
            }
        }
        static float PScroll
        {
            get
            {
                var mouse = Mouse.current;
                return mouse != null ? Mathf.Sign(mouse.scroll.ReadValue().y) * (mouse.scroll.ReadValue().y != 0f ? 1f : 0f) : 0f;
            }
        }
#endif

        public static bool Held(KeyCode k) { return Bot.Active ? Bot.held.Contains(k) : PHeld(k); }
        public static bool Down(KeyCode k) { return Bot.Active ? Bot.down.Contains(k) : PDown(k); }
        public static Vector2 MouseDelta { get { return Bot.Active ? Bot.mouse : PMouse; } }
        public static Vector2 MousePosition { get { return PMousePos; } }
        /// <summary>Mouse wheel this frame: positive up, negative down.</summary>
        public static float Scroll { get { return Bot.Active ? 0f : PScroll; } }

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
                float x = (Held(KeyCode.D) || Held(KeyCode.RightArrow) ? 1f : 0f) - (Held(KeyCode.A) || Held(KeyCode.LeftArrow) ? 1f : 0f);
                float y = (Held(KeyCode.W) || Held(KeyCode.UpArrow) ? 1f : 0f) - (Held(KeyCode.S) || Held(KeyCode.DownArrow) ? 1f : 0f);
                var v = new Vector2(x, y);
                return v.sqrMagnitude > 1f ? v.normalized : v;
            }
        }
    }
}
