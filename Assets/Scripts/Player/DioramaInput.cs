using UnityEngine;
using UnityEngine.InputSystem;

namespace BrumaPx.Player
{
    /// <summary>
    /// Keyboard reads for the overworld. Reads the Input System keyboard directly, so it does not depend on
    /// (or touch) the project's InputActionAsset. The project has the legacy input manager disabled.
    /// Movement is on the arrow keys; A, S, D, F are kept free for the quick slots.
    /// </summary>
    public static class DioramaInput
    {
        public static Vector2 Move()
        {
            var k = Keyboard.current;
            if (k == null) return DebugMove;
            float x = (k.rightArrowKey.isPressed ? 1f : 0f) - (k.leftArrowKey.isPressed ? 1f : 0f);
            float y = (k.upArrowKey.isPressed ? 1f : 0f) - (k.downArrowKey.isPressed ? 1f : 0f);
            return new Vector2(x, y) + DebugMove;
        }

        /// <summary>Test hook: added to the keyboard input (zero in normal play). Lets automated checks walk the hero.</summary>
        public static Vector2 DebugMove;

        /// <summary>Test hooks for the combat keys. The press ones are consumed on read; DebugGuard stays until cleared.</summary>
        public static bool DebugLight, DebugHeavy, DebugDodge, DebugGuard;

        static bool Consume(ref bool flag)
        {
            bool v = flag;
            flag = false;
            return v;
        }

        public static bool Run()
        {
            var k = Keyboard.current;
            return k != null && (k.leftShiftKey.isPressed || k.rightShiftKey.isPressed);
        }

        /// <summary>Z: light attack.</summary>
        public static bool LightPressed()
        {
            var k = Keyboard.current;
            bool real = k != null && k.zKey.wasPressedThisFrame;
            return Consume(ref DebugLight) || real;
        }

        /// <summary>X: heavy attack.</summary>
        public static bool HeavyPressed()
        {
            var k = Keyboard.current;
            bool real = k != null && k.xKey.wasPressedThisFrame;
            return Consume(ref DebugHeavy) || real;
        }

        /// <summary>C: dodge roll.</summary>
        public static bool DodgePressed()
        {
            var k = Keyboard.current;
            bool real = k != null && k.cKey.wasPressedThisFrame;
            return Consume(ref DebugDodge) || real;
        }

        /// <summary>V: guard (held). A press right before a hit is a parry.</summary>
        public static bool GuardHeld()
        {
            var k = Keyboard.current;
            return DebugGuard || (k != null && k.vKey.isPressed);
        }

        public static bool InteractPressed()
        {
            var k = Keyboard.current;
            return k != null && k.eKey.wasPressedThisFrame;
        }

        public static bool AdvancePressed()
        {
            var k = Keyboard.current;
            return k != null && (k.eKey.wasPressedThisFrame || k.spaceKey.wasPressedThisFrame || k.enterKey.wasPressedThisFrame);
        }

        /// <summary>Debug: F5 switches to the next weather.</summary>
        public static bool WeatherPressed() { var k = Keyboard.current; return k != null && k.f5Key.wasPressedThisFrame; }

        /// <summary>Debug: F6 jumps one hour ahead.</summary>
        public static bool HourPressed() { var k = Keyboard.current; return k != null && k.f6Key.wasPressedThisFrame; }

        /// <summary>Debug: F7 pauses or resumes the clock.</summary>
        public static bool PausePressed() { var k = Keyboard.current; return k != null && k.f7Key.wasPressedThisFrame; }

        public static bool CancelPressed()
        {
            var k = Keyboard.current;
            return k != null && k.escapeKey.wasPressedThisFrame;
        }
    }
}
