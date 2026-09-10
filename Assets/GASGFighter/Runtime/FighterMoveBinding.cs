using System;
using System.Collections.Generic;
using UnityEngine;

namespace GASG.Fighting
{
    public enum FighterAttackButton
    {
        Light,
        Medium,
        Heavy,
        Special,
        Throw
    }

    public enum FighterMoveContext
    {
        Any,
        Standing,
        Crouching,
        Airborne,
        Grounded
    }

    // テンキー表記と同じ値を使用する。キャラクターが向いている方向を6（前）として記録する。
    public enum FighterInputDirection
    {
        DownBack = 1,
        Down = 2,
        DownForward = 3,
        Back = 4,
        Neutral = 5,
        Forward = 6,
        UpBack = 7,
        Up = 8,
        UpForward = 9
    }

    [Serializable]
    public sealed class FighterMoveBinding
    {
        [InspectorName("技ID")]
        [Tooltip("保存データや将来のネットワーク同期で使用する固定IDです。作成後は変更しないでください。")]
        [SerializeField] private string moveId = "fighter.move.new";

        [InspectorName("表示名")]
        [SerializeField] private string displayName = "New Move";

        [InspectorName("攻撃データ")]
        [SerializeField] private FighterAttackDefinition attack;

        [InspectorName("攻撃ボタン")]
        [SerializeField] private FighterAttackButton inputButton = FighterAttackButton.Light;

        [InspectorName("使用可能な状態")]
        [SerializeField] private FighterMoveContext context = FighterMoveContext.Grounded;

        [InspectorName("キャンセル分類")]
        [Tooltip("キャンセル元の許可先と照合する分類です。コマンド＋弱で出す必殺技もSpecialを指定します。")]
        [SerializeField] private AttackCancelTarget cancelCategory = AttackCancelTarget.Light;

        [InspectorName("方向コマンド")]
        [Tooltip("古い入力から順に指定します。例: 波動拳=2,3,6 / 昇龍拳=6,2,3。空ならボタン単押しです。")]
        [SerializeField] private List<FighterInputDirection> commandDirections = new List<FighterInputDirection>();

        [InspectorName("コマンド受付フレーム")]
        [Range(1, FighterInputHistory.Capacity)]
        [SerializeField] private int commandWindowFrames = 12;

        [InspectorName("解決優先度")]
        [Tooltip("同じボタンで複数の技が成立した場合、大きい値の技を選びます。コマンド技を高くしてください。")]
        [Range(0, 1000)]
        [SerializeField] private int priority = 10;

        public string MoveId => moveId;
        public string DisplayName => displayName;
        public FighterAttackDefinition Attack => attack;
        public FighterAttackButton InputButton => inputButton;
        public FighterMoveContext Context => context;
        public AttackCancelTarget CancelCategory => cancelCategory;
        public IReadOnlyList<FighterInputDirection> CommandDirections => commandDirections;
        public int CommandWindowFrames => commandWindowFrames;
        public int Priority => priority;
        public bool HasDirectionalCommand => commandDirections != null && commandDirections.Count > 0;

        public bool IsAvailableIn(FighterMoveContext currentContext)
        {
            if (context == FighterMoveContext.Any || context == currentContext)
            {
                return true;
            }

            return context == FighterMoveContext.Grounded &&
                   (currentContext == FighterMoveContext.Standing || currentContext == FighterMoveContext.Crouching);
        }

        public bool IsValid(out string reason)
        {
            if (string.IsNullOrWhiteSpace(moveId))
            {
                reason = "技IDが空です。";
                return false;
            }

            if (attack == null)
            {
                reason = $"{moveId}: 攻撃データが未設定です。";
                return false;
            }

            reason = string.Empty;
            return true;
        }
    }

    /// <summary>
    /// 描画fpsから独立した戦闘フレーム単位の方向入力履歴です。
    /// </summary>
    public sealed class FighterInputHistory
    {
        public const int Capacity = 30;
        private const float DirectionThreshold = 0.45f;

        private readonly FighterInputDirection[] directions = new FighterInputDirection[Capacity];
        private readonly FighterInputFrame[] inputs = new FighterInputFrame[Capacity];
        private int count;

        public int Count => count;
        public FighterInputDirection CurrentDirection =>
            count > 0 ? directions[0] : FighterInputDirection.Neutral;

        public void Advance(float worldX, float worldY, float facingDirection)
        {
            Advance(new FighterInputFrame { moveX = worldX, moveY = worldY }, facingDirection);
        }

        public void Advance(FighterInputFrame input, float facingDirection)
        {
            FighterInputDirection direction = Quantize(input.moveX, input.moveY, facingDirection);
            int copyCount = Mathf.Min(count, Capacity - 1);
            if (copyCount > 0)
            {
                Array.Copy(directions, 0, directions, 1, copyCount);
                Array.Copy(inputs, 0, inputs, 1, copyCount);
            }

            directions[0] = direction;
            inputs[0] = input;
            count = Mathf.Min(count + 1, Capacity);
        }

        public FighterInputDirection GetDirectionFromNewest(int index)
        {
            return index >= 0 && index < count
                ? directions[index]
                : FighterInputDirection.Neutral;
        }

        public FighterInputFrame GetInputFromNewest(int index)
        {
            return index >= 0 && index < count
                ? inputs[index]
                : default;
        }

        public bool Matches(IReadOnlyList<FighterInputDirection> command, int windowFrames)
        {
            if (command == null || command.Count == 0)
            {
                return true;
            }

            int commandIndex = command.Count - 1;
            int searchableFrames = Mathf.Min(count, Mathf.Clamp(windowFrames, 1, Capacity));
            FighterInputDirection previousSample = (FighterInputDirection)0;

            for (int frameIndex = 0; frameIndex < searchableFrames; frameIndex++)
            {
                FighterInputDirection sample = directions[frameIndex];
                if (frameIndex > 0 && sample == previousSample)
                {
                    continue;
                }

                previousSample = sample;
                if (sample != command[commandIndex])
                {
                    continue;
                }

                commandIndex--;
                if (commandIndex < 0)
                {
                    return true;
                }
            }

            return false;
        }

        public void ConsumeDirectionalCommand()
        {
            FighterInputDirection current = CurrentDirection;
            directions[0] = current;
            count = count > 0 ? 1 : 0;
        }

        public void Clear()
        {
            Array.Clear(directions, 0, directions.Length);
            Array.Clear(inputs, 0, inputs.Length);
            count = 0;
        }

        public static FighterInputDirection Quantize(float worldX, float worldY, float facingDirection)
        {
            float relativeX = worldX * (facingDirection >= 0f ? 1f : -1f);
            int horizontal = Mathf.Abs(relativeX) >= DirectionThreshold ? (int)Mathf.Sign(relativeX) : 0;
            int vertical = Mathf.Abs(worldY) >= DirectionThreshold ? (int)Mathf.Sign(worldY) : 0;

            if (vertical < 0)
            {
                return horizontal < 0
                    ? FighterInputDirection.DownBack
                    : horizontal > 0
                        ? FighterInputDirection.DownForward
                        : FighterInputDirection.Down;
            }

            if (vertical > 0)
            {
                return horizontal < 0
                    ? FighterInputDirection.UpBack
                    : horizontal > 0
                        ? FighterInputDirection.UpForward
                        : FighterInputDirection.Up;
            }

            return horizontal < 0
                ? FighterInputDirection.Back
                : horizontal > 0
                    ? FighterInputDirection.Forward
                    : FighterInputDirection.Neutral;
        }
    }
}
