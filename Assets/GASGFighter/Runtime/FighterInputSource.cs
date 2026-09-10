using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;

namespace GASG.Fighting
{
    public sealed class FighterInputSource : MonoBehaviour
    {
        [SerializeField] [Range(1, 2)] private int playerIndex = 1;
        [Tooltip("0=P1用1台目、1=P2用2台目。負数ならゲームパッドを使用しません。")]
        [SerializeField] private int gamepadIndex;
        [SerializeField] [Range(0.1f, 0.95f)] private float stickDeadZone = 0.35f;

        private Vector2 currentMove;
        private bool jumpBuffered;
        private bool lightBuffered;
        private bool mediumBuffered;
        private bool heavyBuffered;
        private bool specialBuffered;
        private bool throwBuffered;

        public Vector2 CurrentMove => currentMove;

        // 技判定の履歴とは独立し、画面上の左右方向で入力状態を保存する。
        public readonly FightDisplayHistory DisplayHistory = new FightDisplayHistory();
        private int heldButtons;
        private int assignedGamepadDeviceId = -1;

        public int GamepadIndex => gamepadIndex;
        public string AssignedDeviceName
        {
            get
            {
                Gamepad gamepad = GetAssignedGamepad();
                return gamepad != null ? gamepad.displayName : "NOT CONNECTED";
            }
        }

        private void Awake()
        {
            CacheAssignedDeviceId();
        }

        private void Update()
        {
            Vector2 keyboardMove = ReadKeyboardMove();
            Vector2 gamepadMove = ReadGamepadMove();
            currentMove = gamepadMove.sqrMagnitude > keyboardMove.sqrMagnitude ? gamepadMove : keyboardMove;

            Keyboard keyboard = Keyboard.current;
            Gamepad gamepad = GetAssignedGamepad();

            if (playerIndex == 1)
            {
                jumpBuffered |= WasPressed(keyboard?.wKey) || WasPressed(keyboard?.spaceKey);
                lightBuffered |= WasPressed(keyboard?.jKey);
                mediumBuffered |= WasPressed(keyboard?.kKey);
                heavyBuffered |= WasPressed(keyboard?.lKey);
                specialBuffered |= WasPressed(keyboard?.iKey);
                throwBuffered |= WasPressed(keyboard?.uKey);
            }
            else
            {
                jumpBuffered |= WasPressed(keyboard?.upArrowKey);
                lightBuffered |= WasPressed(keyboard?.numpad1Key) || WasPressed(keyboard?.rightCtrlKey);
                mediumBuffered |= WasPressed(keyboard?.numpad2Key) || WasPressed(keyboard?.rightShiftKey);
                heavyBuffered |= WasPressed(keyboard?.numpad3Key);
                specialBuffered |= WasPressed(keyboard?.numpad5Key);
                throwBuffered |= WasPressed(keyboard?.numpad0Key) || WasPressed(keyboard?.enterKey);
            }

            if (gamepad != null)
            {
                jumpBuffered |= gamepad.dpad.up.wasPressedThisFrame || gamepad.leftStick.up.wasPressedThisFrame;
                // PS5: □=弱、✕=中、○=強、△=必殺技、L1=投げ。
                lightBuffered |= gamepad.buttonWest.wasPressedThisFrame;
                mediumBuffered |= gamepad.buttonSouth.wasPressedThisFrame;
                heavyBuffered |= gamepad.buttonEast.wasPressedThisFrame;
                specialBuffered |= gamepad.buttonNorth.wasPressedThisFrame;
                throwBuffered |= gamepad.leftShoulder.wasPressedThisFrame;
            }

            heldButtons = 0;
            bool p1 = playerIndex == 1;
            SetHeld(0, (p1 ? Held(keyboard?.jKey) : Held(keyboard?.numpad1Key) || Held(keyboard?.rightCtrlKey)) || Held(gamepad?.buttonWest));
            SetHeld(1, (p1 ? Held(keyboard?.kKey) : Held(keyboard?.numpad2Key) || Held(keyboard?.rightShiftKey)) || Held(gamepad?.buttonSouth));
            SetHeld(2, (p1 ? Held(keyboard?.lKey) : Held(keyboard?.numpad3Key)) || Held(gamepad?.buttonEast));
            SetHeld(3, (p1 ? Held(keyboard?.iKey) : Held(keyboard?.numpad5Key)) || Held(gamepad?.buttonNorth));
            SetHeld(4, (p1 ? Held(keyboard?.uKey) : Held(keyboard?.numpad0Key) || Held(keyboard?.enterKey)) || Held(gamepad?.leftShoulder));
        }

        private static bool Held(ButtonControl control) => control != null && control.isPressed;

        private void SetHeld(int bit, bool held)
        {
            if (held) heldButtons |= 1 << bit;
        }

        public FighterInputFrame ConsumeFrame()
        {
            FighterInputFrame frame = new FighterInputFrame
            {
                moveX = currentMove.x,
                moveY = currentMove.y,
                jumpPressed = jumpBuffered,
                lightPressed = lightBuffered,
                mediumPressed = mediumBuffered,
                heavyPressed = heavyBuffered,
                specialPressed = specialBuffered,
                throwPressed = throwBuffered
            };

            // 短い押下も、次のシミュレーションフレームで履歴に残す。
            int buttons = heldButtons | (lightBuffered ? 1 : 0) | (mediumBuffered ? 2 : 0) |
                (heavyBuffered ? 4 : 0) | (specialBuffered ? 8 : 0) | (throwBuffered ? 16 : 0);
            DisplayHistory.Advance(currentMove, buttons);

            jumpBuffered = false;
            lightBuffered = false;
            mediumBuffered = false;
            heavyBuffered = false;
            specialBuffered = false;
            throwBuffered = false;
            return frame;
        }

        public bool IsRestartPressed()
        {
            Keyboard keyboard = Keyboard.current;
            if (keyboard != null && (keyboard.enterKey.wasPressedThisFrame || keyboard.spaceKey.wasPressedThisFrame))
            {
                return true;
            }

            Gamepad gamepad = GetAssignedGamepad();
            return gamepad != null && gamepad.startButton.wasPressedThisFrame;
        }

        private Vector2 ReadKeyboardMove()
        {
            Keyboard keyboard = Keyboard.current;
            if (keyboard == null)
            {
                return Vector2.zero;
            }

            float x;
            float y;
            if (playerIndex == 1)
            {
                x = ReadAxis(keyboard.aKey.isPressed, keyboard.dKey.isPressed);
                y = ReadAxis(keyboard.sKey.isPressed, keyboard.wKey.isPressed);
            }
            else
            {
                x = ReadAxis(keyboard.leftArrowKey.isPressed, keyboard.rightArrowKey.isPressed);
                y = ReadAxis(keyboard.downArrowKey.isPressed, keyboard.upArrowKey.isPressed);
            }

            return new Vector2(x, y);
        }

        private Vector2 ReadGamepadMove()
        {
            Gamepad gamepad = GetAssignedGamepad();
            if (gamepad == null)
            {
                return Vector2.zero;
            }

            Vector2 stick = gamepad.leftStick.ReadValue();
            Vector2 dpad = gamepad.dpad.ReadValue();
            Vector2 selected = dpad.sqrMagnitude > stick.sqrMagnitude ? dpad : stick;
            return selected.sqrMagnitude >= stickDeadZone * stickDeadZone ? selected : Vector2.zero;
        }

        private Gamepad GetAssignedGamepad()
        {
            if (assignedGamepadDeviceId >= 0)
            {
                for (int i = 0; i < Gamepad.all.Count; i++)
                {
                    if (Gamepad.all[i].deviceId == assignedGamepadDeviceId)
                    {
                        return Gamepad.all[i];
                    }
                }

                // 切断されたデバイスの枠へ、別の機器を自動で割り当てない。
                return null;
            }

            return gamepadIndex >= 0 && gamepadIndex < Gamepad.all.Count ? Gamepad.all[gamepadIndex] : null;
        }

        public void AssignGamepadIndex(int newGamepadIndex)
        {
            gamepadIndex = newGamepadIndex;
            CacheAssignedDeviceId();
            ClearBufferedInput();
            Debug.Log($"[GASG Fighter][成功] P{playerIndex}の入力を {AssignedDeviceName} (Gamepad {gamepadIndex}) に割り当てました。", this);
        }

        private void CacheAssignedDeviceId()
        {
            assignedGamepadDeviceId = gamepadIndex >= 0 && gamepadIndex < Gamepad.all.Count
                ? Gamepad.all[gamepadIndex].deviceId
                : -1;
        }

        private void ClearBufferedInput()
        {
            currentMove = Vector2.zero;
            jumpBuffered = false;
            lightBuffered = false;
            mediumBuffered = false;
            heavyBuffered = false;
            specialBuffered = false;
            throwBuffered = false;
            heldButtons = 0;
        }

        private static float ReadAxis(bool negative, bool positive)
        {
            return (positive ? 1f : 0f) - (negative ? 1f : 0f);
        }

        private static bool WasPressed(ButtonControl control)
        {
            return control != null && control.wasPressedThisFrame;
        }

#if UNITY_EDITOR
        public void EditorConfigure(int newPlayerIndex, int newGamepadIndex)
        {
            playerIndex = Mathf.Clamp(newPlayerIndex, 1, 2);
            gamepadIndex = newGamepadIndex;
        }
#endif
    }
}
