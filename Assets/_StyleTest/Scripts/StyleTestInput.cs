using UnityEngine;
using UnityEngine.InputSystem;

namespace BrumaPx.StyleTest
{
    /// <summary>
    /// Keyboard reads for the style test. Reads the Input System keyboard directly, so it does not depend on
    /// (or touch) the project's InputActionAsset. The project has the legacy input manager disabled.
    /// </summary>
    public static class StyleTestInput
    {
public static Vector2 Move()
        {
            var k = Keyboard.current;
            if (k == null) return DebugMove;
            float x = (k.dKey.isPressed || k.rightArrowKey.isPressed ? 1f : 0f) - (k.aKey.isPressed || k.leftArrowKey.isPressed ? 1f : 0f);
            float y = (k.wKey.isPressed || k.upArrowKey.isPressed ? 1f : 0f) - (k.sKey.isPressed || k.downArrowKey.isPressed ? 1f : 0f);
            return new Vector2(x, y) + DebugMove;
        }

        /// <summary>Test hook: added to the keyboard input (zero in normal play). Lets automated checks walk the hero.</summary>
        public static Vector2 DebugMove;

        public static bool Run()
        {
            var k = Keyboard.current;
            return k != null && (k.leftShiftKey.isPressed || k.rightShiftKey.isPressed);
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

        public static bool CancelPressed()
        {
            var k = Keyboard.current;
            return k != null && k.escapeKey.wasPressedThisFrame;
        }
    }
}
