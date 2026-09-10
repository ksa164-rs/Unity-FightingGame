using System;
using UnityEngine;

namespace GASG.Fighting
{
    public static class FightFrameTiming
    {
        public const int SimulationRate = 60;
        public const float FrameDuration = 1f / SimulationRate;

        public static int SecondsToFrames(float seconds)
        {
            return Mathf.Max(1, Mathf.RoundToInt(seconds * SimulationRate));
        }
    }

    public enum FighterState
    {
        RoundLocked,
        Neutral,
        Crouching,
        Jumping,
        Stepping,
        Attacking,
        HitStun,
        BlockStun,
        KnockedDown
    }

    public enum GuardHeight
    {
        Mid,
        Low,
        Overhead,
        Unblockable
    }

    [Flags]
    public enum AttackCancelTarget
    {
        None = 0,
        Light = 1 << 0,
        Medium = 1 << 1,
        Heavy = 1 << 2,
        Special = 1 << 3,
        Throw = 1 << 4
    }

    [Serializable]
    public struct FighterInputFrame
    {
        public float moveX;
        public float moveY;
        public bool jumpPressed;
        public bool lightPressed;
        public bool mediumPressed;
        public bool heavyPressed;
        public bool specialPressed;
        public bool throwPressed;
    }

    /// <summary>
    /// 1フレーム内で検出した攻撃結果です。検出と適用を分け、相打ちを成立させます。
    /// </summary>
    public readonly struct FighterAttackContact
    {
        public FighterAttackContact(
            FighterController attacker,
            FighterController defender,
            FighterAttackDefinition attack)
        {
            Attacker = attacker;
            Defender = defender;
            Attack = attack;
        }

        public FighterController Attacker { get; }
        public FighterController Defender { get; }
        public FighterAttackDefinition Attack { get; }
        public bool IsValid => Attacker != null && Defender != null && Attack != null;
    }

    internal enum FighterCommand
    {
        Jump,
        Light,
        Medium,
        Heavy,
        Special,
        Throw,
        Count
    }

    /// <summary>
    /// 描画フレームではなく戦闘フレーム単位で、ボタン入力を短時間だけ保持します。
    /// </summary>
    internal sealed class FighterCommandBuffer
    {
        private readonly int[] remainingFrames = new int[(int)FighterCommand.Count];

        public void Advance(FighterInputFrame input, int bufferFrames)
        {
            for (int i = 0; i < remainingFrames.Length; i++)
            {
                remainingFrames[i] = Mathf.Max(0, remainingFrames[i] - 1);
            }

            int lifetime = Mathf.Max(1, bufferFrames);
            Store(FighterCommand.Jump, input.jumpPressed, lifetime);
            Store(FighterCommand.Light, input.lightPressed, lifetime);
            Store(FighterCommand.Medium, input.mediumPressed, lifetime);
            Store(FighterCommand.Heavy, input.heavyPressed, lifetime);
            Store(FighterCommand.Special, input.specialPressed, lifetime);
            Store(FighterCommand.Throw, input.throwPressed, lifetime);
        }

        public bool Contains(FighterCommand command)
        {
            return remainingFrames[(int)command] > 0;
        }

        public bool Consume(FighterCommand command)
        {
            int index = (int)command;
            if (remainingFrames[index] <= 0)
            {
                return false;
            }

            remainingFrames[index] = 0;
            return true;
        }

        public void ClearAttackCommands()
        {
            remainingFrames[(int)FighterCommand.Light] = 0;
            remainingFrames[(int)FighterCommand.Medium] = 0;
            remainingFrames[(int)FighterCommand.Heavy] = 0;
            remainingFrames[(int)FighterCommand.Special] = 0;
            remainingFrames[(int)FighterCommand.Throw] = 0;
        }

        public void Clear()
        {
            Array.Clear(remainingFrames, 0, remainingFrames.Length);
        }

        private void Store(FighterCommand command, bool pressed, int lifetime)
        {
            if (pressed)
            {
                remainingFrames[(int)command] = lifetime;
            }
        }
    }
}
