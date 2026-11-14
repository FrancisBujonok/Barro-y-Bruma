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

        /// <summary>Test hooks for the bag, the belt and the notebook (zero or false in normal play).</summary>
        public static bool DebugBag, DebugNotebook;
        public static int DebugBelt = -1;
        public static Vector2Int DebugStep;

        /// <summary>Tab held: the bag is laid out on the ground.</summary>
        public static bool BagHeld()
        {
            var k = Keyboard.current;
            return DebugBag || (k != null && k.tabKey.isPressed);
        }

        /// <summary>N: opens or closes the notebook.</summary>
        public static bool NotebookPressed()
        {
            var k = Keyboard.current;
            bool real = k != null && k.nKey.wasPressedThisFrame;
            return Consume(ref DebugNotebook) || real;
        }

        /// <summary>Test hook for the body window (consumed on read).</summary>
        public static bool DebugBody;

        /// <summary>B: opens or closes the body window.</summary>
        public static bool BodyPressed()
        {
            var k = Keyboard.current;
            bool real = k != null && k.bKey.wasPressedThisFrame;
            return Consume(ref DebugBody) || real;
        }

        /// <summary>Test hooks for the clothing window, its confirm key and the body-window treatment (consumed on read).</summary>
        public static bool DebugEquip, DebugConfirm;
        public static int DebugTreat = -1;

        /// <summary>P: opens or closes the clothing window.</summary>
        public static bool EquipPressed()
        {
            var k = Keyboard.current;
            bool real = k != null && k.pKey.wasPressedThisFrame;
            return Consume(ref DebugEquip) || real;
        }

        /// <summary>Enter, E or Space: confirms inside a window.</summary>
        public static bool ConfirmPressed()
        {
            return Consume(ref DebugConfirm) || AdvancePressed();
        }

        /// <summary>1, 2, 3 in the body window: 0 bandage, 1 salve, 2 cure, or -1.</summary>
        public static int TreatPressed()
        {
            if (DebugTreat >= 0)
            {
                int d = DebugTreat;
                DebugTreat = -1;
                return d;
            }
            var k = Keyboard.current;
            if (k == null) return -1;
            if (k.digit1Key.wasPressedThisFrame) return 0;
            if (k.digit2Key.wasPressedThisFrame) return 1;
            if (k.digit3Key.wasPressedThisFrame) return 2;
            return -1;
        }

        /// <summary>A, S, D, F: the belt pocket pressed this frame (0 to 3), or -1.</summary>
        public static int BeltPressed()
        {
            if (DebugBelt >= 0)
            {
                int d = DebugBelt;
                DebugBelt = -1;
                return d;
            }
            var k = Keyboard.current;
            if (k == null) return -1;
            if (k.aKey.wasPressedThisFrame) return 0;
            if (k.sKey.wasPressedThisFrame) return 1;
            if (k.dKey.wasPressedThisFrame) return 2;
            if (k.fKey.wasPressedThisFrame) return 3;
            return -1;
        }

        /// <summary>Test hook for PageStep (consumed on read).</summary>
        public static int DebugPage;

        /// <summary>Q and W: previous and next notebook page (-1, 0 or 1).</summary>
        public static int PageStep()
        {
            if (DebugPage != 0)
            {
                int d = DebugPage;
                DebugPage = 0;
                return d;
            }
            var k = Keyboard.current;
            if (k == null) return 0;
            return (k.wKey.wasPressedThisFrame ? 1 : 0) - (k.qKey.wasPressedThisFrame ? 1 : 0);
        }

        /// <summary>Arrow pressed this frame, for moving a cursor in the bag and the notebook (y up is positive).</summary>
        public static Vector2Int MenuStep()
        {
            if (DebugStep != Vector2Int.zero)
            {
                var d = DebugStep;
                DebugStep = Vector2Int.zero;
                return d;
            }
            var k = Keyboard.current;
            if (k == null) return Vector2Int.zero;
            int x = (k.rightArrowKey.wasPressedThisFrame ? 1 : 0) - (k.leftArrowKey.wasPressedThisFrame ? 1 : 0);
            int y = (k.upArrowKey.wasPressedThisFrame ? 1 : 0) - (k.downArrowKey.wasPressedThisFrame ? 1 : 0);
            return new Vector2Int(x, y);
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
