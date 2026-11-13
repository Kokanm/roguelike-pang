using System.Collections.Generic;
using UnityEngine;
#if BW_INPUT_SYSTEM && ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;
#endif

namespace Bubblewright
{
    /// <summary>
    /// One place that merges touch, keyboard and gamepad into the three things the game needs:
    /// a move direction, fire held, and confirm (restart).
    ///
    /// Touch (from the sheet): left half of the screen is a RELATIVE drag pad. Where your thumb lands
    /// is the centre; drag left or right of it to run. Right half is the fire button; hold to keep firing.
    /// The round jump button sits left of the fire button: a touch that begins inside it jumps and never fires.
    /// The mouse acts as one finger, so the touch controls can be tried on desktop too.
    ///
    /// Uses the Input System package when it is the active input backend, otherwise the legacy Input Manager.
    /// </summary>
    public class GameInput
    {
        public float Move { get; private set; }
        public float Climb { get; private set; }
        public float MoveY { get; private set; }
        public bool FireHeld { get; private set; }
        public bool FirePressed { get; private set; }
        /// <summary>Jump pressed this frame: L or Shift, pad B / Circle or Y / Triangle, or the touch jump button.</summary>
        public bool JumpPressed { get; private set; }
        public bool ConfirmPressed { get; private set; }
        public bool AnyInput { get; private set; }
        /// <summary>Confirm from keys or pad only, for menus where a tap should hit a button instead.</summary>
        public bool KeyConfirmPressed { get; private set; }
        /// <summary>Screen position (pixels, origin bottom left) of a finger or click that began this frame.</summary>
        public Vector2? TapPosition { get; private set; }

        // Desktop debug helpers: 1-4 spawn an XL bubble, F1 toggles hitboxes, F3 toggles the test layout.
        public int DebugSpawn { get; private set; }
        public bool DebugToggleHitboxes { get; private set; }
        public bool DebugTestLayoutPressed { get; private set; }
        /// <summary>Esc, P or pad Start. F2 opens the debug panel (it pauses the game too).</summary>
        public bool PausePressed { get; private set; }
        public bool DebugPanelPressed { get; private set; }
        /// <summary>Esc or pad B: go back one menu.</summary>
        public bool BackPressed { get; private set; }

        // Touch pad state, read by the on-screen view.
        public bool TouchUsed { get; private set; }
        public bool PadActive => padPointer != NoPointer;
        public Vector2 PadOrigin { get; private set; }
        public Vector2 PadCurrent { get; private set; }
        public bool TouchFireHeld { get; private set; }
        public bool TouchJumpHeld { get; private set; }

        /// <summary>Where the jump button is on screen (pixels, origin bottom left). Set every frame by the touch view.</summary>
        Vector2 jumpButtonCentre;
        float jumpButtonRadius;
        /// <summary>A touch counts as a jump this much further out than the drawn button.</summary>
        const float JumpTouchSlack = 1.2f;

        public void SetJumpButton(Vector2 screenCentre, float screenRadius)
        {
            jumpButtonCentre = screenCentre;
            jumpButtonRadius = screenRadius;
        }

        public bool InJumpButton(Vector2 screenPosition) =>
            jumpButtonRadius > 0f && (screenPosition - jumpButtonCentre).sqrMagnitude <= Sq(jumpButtonRadius * JumpTouchSlack);

        static float Sq(float v) => v * v;

        const int NoPointer = int.MinValue;
        const int MousePointer = -1000;
        int padPointer = NoPointer;
        readonly HashSet<int> firePointers = new HashSet<int>();
        readonly HashSet<int> jumpPointers = new HashSet<int>();
        readonly List<PointerSample> pointers = new List<PointerSample>(8);

        struct PointerSample
        {
            public int id;
            public Vector2 position;
            public bool began;
        }

        /// <summary>Screen scale for the relative drag pad (1 at 270 px height or 90 dpi).</summary>
        float PadScale => Screen.dpi > 0 ? Screen.dpi / 90f : Screen.height / (float)Tuning.I.referenceResolution.y;
        /// <summary>Pixels of drag before Wick starts running, scaled with screen density.</summary>
        float DeadZone => 5.4f * PadScale;
        float MaxDrag => DeadZone * 3f;
        float ClimbDeadZone => 18f * PadScale;

        public GameInput()
        {
#if !(BW_INPUT_SYSTEM && ENABLE_INPUT_SYSTEM)
            Input.simulateMouseWithTouches = false; // otherwise touch 0 is reported twice
            Input.multiTouchEnabled = true;
#endif
        }

        public void Update()
        {
            pointers.Clear();
            float keyMove = 0f, keyMoveY = 0f, keyClimb = 0f;
            bool keyFire = false, keyFirePressed = false, keyJump = false, confirm = false, any = false;
            DebugSpawn = 0;
            DebugToggleHitboxes = false;
            DebugTestLayoutPressed = false;
            PausePressed = false;
            BackPressed = false;
            DebugPanelPressed = false;

#if BW_INPUT_SYSTEM && ENABLE_INPUT_SYSTEM
            var kb = Keyboard.current;
            if (kb != null)
            {
                if (kb.aKey.isPressed || kb.leftArrowKey.isPressed) keyMove -= 1f;
                if (kb.dKey.isPressed || kb.rightArrowKey.isPressed) keyMove += 1f;
                if (kb.sKey.isPressed || kb.downArrowKey.isPressed) { keyMoveY -= 1f; keyClimb -= 1f; }
                if (kb.wKey.isPressed || kb.upArrowKey.isPressed) { keyMoveY += 1f; keyClimb += 1f; }
                keyFire |= kb.spaceKey.isPressed || kb.jKey.isPressed || kb.kKey.isPressed;
                keyFirePressed |= kb.spaceKey.wasPressedThisFrame || kb.jKey.wasPressedThisFrame || kb.kKey.wasPressedThisFrame;
                keyJump |= kb.lKey.wasPressedThisFrame || kb.leftShiftKey.wasPressedThisFrame || kb.rightShiftKey.wasPressedThisFrame;
                confirm |= kb.rKey.wasPressedThisFrame || kb.enterKey.wasPressedThisFrame || kb.numpadEnterKey.wasPressedThisFrame || kb.spaceKey.wasPressedThisFrame;
                any |= kb.anyKey.wasPressedThisFrame;
                if (kb.digit1Key.wasPressedThisFrame) DebugSpawn = 1;
                if (kb.digit2Key.wasPressedThisFrame) DebugSpawn = 2;
                if (kb.digit3Key.wasPressedThisFrame) DebugSpawn = 3;
                if (kb.digit4Key.wasPressedThisFrame) DebugSpawn = 4;
                if (kb.digit5Key.wasPressedThisFrame) DebugSpawn = 5; // a rolling balloon
                DebugToggleHitboxes = kb.f1Key.wasPressedThisFrame;
                DebugTestLayoutPressed = kb.f3Key.wasPressedThisFrame;
                PausePressed |= kb.escapeKey.wasPressedThisFrame || kb.pKey.wasPressedThisFrame;
                BackPressed |= kb.escapeKey.wasPressedThisFrame;
                DebugPanelPressed = kb.f2Key.wasPressedThisFrame;
            }
            var pad = Gamepad.current;
            if (pad != null)
            {
                float stickX = pad.leftStick.ReadValue().x;
                float stickY = pad.leftStick.ReadValue().y;
                float x = stickX + pad.dpad.ReadValue().x;
                float y = stickY + pad.dpad.ReadValue().y;
                if (Mathf.Abs(x) > 0.35f) keyMove += Mathf.Sign(x);
                if (Mathf.Abs(y) > 0.35f) keyMoveY += Mathf.Sign(y);
                if (pad.dpad.up.isPressed || stickY > 0.5f) keyClimb += 1f;
                if (pad.dpad.down.isPressed || stickY < -0.5f) keyClimb -= 1f;
                keyFire |= pad.buttonSouth.isPressed || pad.buttonWest.isPressed || pad.rightTrigger.isPressed || pad.rightShoulder.isPressed;
                keyFirePressed |= pad.buttonSouth.wasPressedThisFrame || pad.buttonWest.wasPressedThisFrame || pad.rightTrigger.wasPressedThisFrame || pad.rightShoulder.wasPressedThisFrame;
                bool padJump = pad.buttonEast.wasPressedThisFrame || pad.buttonNorth.wasPressedThisFrame;
                keyJump |= padJump;
                confirm |= pad.startButton.wasPressedThisFrame || pad.buttonSouth.wasPressedThisFrame;
                PausePressed |= pad.startButton.wasPressedThisFrame;
                BackPressed |= pad.buttonEast.wasPressedThisFrame;
                any |= keyFirePressed || padJump || Mathf.Abs(x) > 0.35f || keyClimb != 0f;
            }
            var ts = Touchscreen.current;
            if (ts != null)
            {
                foreach (TouchControl t in ts.touches)
                {
                    if (!t.press.isPressed) continue;
                    pointers.Add(new PointerSample { id = t.touchId.ReadValue(), position = t.position.ReadValue(), began = t.press.wasPressedThisFrame });
                }
            }
            var mouse = Mouse.current;
            if (mouse != null && mouse.leftButton.isPressed && pointers.Count == 0)
                pointers.Add(new PointerSample { id = MousePointer, position = mouse.position.ReadValue(), began = mouse.leftButton.wasPressedThisFrame });
#else
            if (Input.GetKey(KeyCode.A) || Input.GetKey(KeyCode.LeftArrow)) keyMove -= 1f;
            if (Input.GetKey(KeyCode.D) || Input.GetKey(KeyCode.RightArrow)) keyMove += 1f;
            if (Input.GetKey(KeyCode.S) || Input.GetKey(KeyCode.DownArrow)) { keyMoveY -= 1f; keyClimb -= 1f; }
            if (Input.GetKey(KeyCode.W) || Input.GetKey(KeyCode.UpArrow)) { keyMoveY += 1f; keyClimb += 1f; }
            float axis = 0f, axisY = 0f;
            try { axis = Input.GetAxisRaw("Horizontal"); } catch (System.ArgumentException) { }
            try { axisY = Input.GetAxisRaw("Vertical"); } catch (System.ArgumentException) { }
            if (keyMove == 0f && Mathf.Abs(axis) > 0.35f) keyMove = Mathf.Sign(axis);
            if (keyMoveY == 0f && Mathf.Abs(axisY) > 0.35f) keyMoveY = Mathf.Sign(axisY);
            if (keyClimb == 0f && Mathf.Abs(axisY) > 0.5f) keyClimb = Mathf.Sign(axisY);
            KeyCode[] fireKeys = { KeyCode.Space, KeyCode.J, KeyCode.K, KeyCode.JoystickButton0, KeyCode.JoystickButton2, KeyCode.JoystickButton5 };
            foreach (var k in fireKeys)
            {
                keyFire |= Input.GetKey(k);
                keyFirePressed |= Input.GetKeyDown(k);
            }
            // Jump: L, either Shift, pad B / Circle (button 1) or Y / Triangle (button 3).
            KeyCode[] jumpKeys = { KeyCode.L, KeyCode.LeftShift, KeyCode.RightShift, KeyCode.JoystickButton1, KeyCode.JoystickButton3 };
            foreach (var k in jumpKeys) keyJump |= Input.GetKeyDown(k);
            confirm |= Input.GetKeyDown(KeyCode.R) || Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter) || Input.GetKeyDown(KeyCode.Space)
                       || Input.GetKeyDown(KeyCode.JoystickButton7) || Input.GetKeyDown(KeyCode.JoystickButton0);
            any |= Input.anyKeyDown;
            for (int d = 1; d <= 5; d++) if (Input.GetKeyDown(KeyCode.Alpha0 + d)) DebugSpawn = d; // 5: a rolling balloon
            DebugToggleHitboxes = Input.GetKeyDown(KeyCode.F1);
            DebugTestLayoutPressed = Input.GetKeyDown(KeyCode.F3);
            PausePressed = Input.GetKeyDown(KeyCode.Escape) || Input.GetKeyDown(KeyCode.P) || Input.GetKeyDown(KeyCode.JoystickButton7);
            BackPressed = Input.GetKeyDown(KeyCode.Escape) || Input.GetKeyDown(KeyCode.JoystickButton1);
            DebugPanelPressed = Input.GetKeyDown(KeyCode.F2);
            for (int i = 0; i < Input.touchCount; i++)
            {
                var t = Input.GetTouch(i);
                if (t.phase == TouchPhase.Ended || t.phase == TouchPhase.Canceled) continue;
                pointers.Add(new PointerSample { id = t.fingerId, position = t.position, began = t.phase == TouchPhase.Began });
            }
            if (Input.touchCount == 0 && Input.GetMouseButton(0))
                pointers.Add(new PointerSample { id = MousePointer, position = Input.mousePosition, began = Input.GetMouseButtonDown(0) });
#endif

            float touchMove = UpdateTouch(out float touchClimb, out bool touchFire, out bool touchFirePressed, out bool touchJumpPressed, out bool touchBegan);

            if (touchClimb != 0f)
            {
                Move = 0f;
                Climb = Mathf.Clamp(touchClimb, -1f, 1f);
            }
            else if (touchMove != 0f)
            {
                Move = Mathf.Clamp(touchMove, -1f, 1f);
                Climb = 0f;
            }
            else
            {
                Move = Mathf.Clamp(keyMove, -1f, 1f);
                Climb = Mathf.Clamp(keyClimb, -1f, 1f);
            }
            MoveY = Mathf.Clamp(touchClimb != 0f ? touchClimb : keyMoveY, -1f, 1f);
            FireHeld = keyFire || touchFire;
            FirePressed = keyFirePressed || touchFirePressed;
            JumpPressed = keyJump || touchJumpPressed;
            ConfirmPressed = confirm || touchBegan;
            KeyConfirmPressed = confirm;
            AnyInput = any || touchBegan;
        }

        float UpdateTouch(out float climb, out bool fire, out bool firePressed, out bool jumpPressed, out bool anyBegan)
        {
            climb = 0f;
            fire = false;
            firePressed = false;
            jumpPressed = false;
            anyBegan = false;
            TapPosition = null;
            float half = Screen.width * 0.5f;

            // Drop pointers that lifted.
            if (padPointer != NoPointer && !Contains(padPointer)) padPointer = NoPointer;
            firePointers.RemoveWhere(id => !Contains(id));
            jumpPointers.RemoveWhere(id => !Contains(id));

            foreach (var p in pointers)
            {
                if (!p.began) continue;
                anyBegan = true;
                TouchUsed = true;
                TapPosition ??= p.position;
                if (InJumpButton(p.position))
                {
                    jumpPointers.Add(p.id); // a jump touch never fires or moves, wherever it slides to
                    jumpPressed = true;
                }
                else if (p.position.x < half && padPointer == NoPointer)
                {
                    padPointer = p.id;
                    PadOrigin = p.position;
                    PadCurrent = p.position;
                }
                else if (p.position.x >= half)
                {
                    firePointers.Add(p.id);
                    firePressed = true;
                }
            }

            float move = 0f;
            foreach (var p in pointers)
            {
                if (p.id == padPointer)
                {
                    PadCurrent = p.position;
                    float dx = p.position.x - PadOrigin.x;
                    float dy = p.position.y - PadOrigin.y;
                    if (Mathf.Abs(dy) > ClimbDeadZone && Mathf.Abs(dy) > Mathf.Abs(dx))
                    {
                        climb = Mathf.Sign(dy);
                    }
                    else
                    {
                        // The origin trails the thumb, so reversing never needs a long drag back.
                        if (Mathf.Abs(dx) > MaxDrag)
                        {
                            PadOrigin = new Vector2(p.position.x - Mathf.Sign(dx) * MaxDrag, PadOrigin.y);
                            dx = Mathf.Sign(dx) * MaxDrag;
                        }
                        if (Mathf.Abs(dx) > DeadZone) move = Mathf.Sign(dx);
                    }
                }
                else if (firePointers.Contains(p.id))
                {
                    fire = true;
                }
            }
            TouchFireHeld = fire;
            bool jumpHeld = false;
            foreach (var p in pointers) jumpHeld |= jumpPointers.Contains(p.id);
            TouchJumpHeld = jumpHeld;
            return move;
        }

        bool Contains(int id)
        {
            foreach (var p in pointers) if (p.id == id) return true;
            return false;
        }
    }
}
