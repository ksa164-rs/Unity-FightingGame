using System;
using System.Collections.Generic;
using UnityEngine;

namespace GASG.Fighting
{
    public enum AttackMotionId
    {
        LightAttack,
        MiddleAttack,
        HeavyAttack,
        SpecialAttack,
        Throw
    }

    [CreateAssetMenu(
        fileName = "AttackMotionDatabase_New",
        menuName = "GASG/Fighting/Attack Motion Database")]
    public sealed class FighterAttackDatabase : ScriptableObject
    {
        [Header("攻撃モーション")]
        [SerializeField] private FighterAttackDefinition lightAttack;
        [SerializeField] private FighterAttackDefinition middleAttack;
        [SerializeField] private FighterAttackDefinition heavyAttack;
        [SerializeField] private FighterAttackDefinition specialAttack;
        [SerializeField] private FighterAttackDefinition throwAttack;

        [Header("技コマンド（優先度1の新形式）")]
        [Tooltip("技ID、状態、方向コマンド、攻撃ボタンをデータとして管理します。空の場合は旧5技参照を使用します。")]
        [SerializeField] private List<FighterMoveBinding> moveBindings = new List<FighterMoveBinding>();

        public FighterAttackDefinition LightAttack => lightAttack;
        public FighterAttackDefinition MiddleAttack => middleAttack;
        public FighterAttackDefinition HeavyAttack => heavyAttack;
        public FighterAttackDefinition SpecialAttack => specialAttack;
        public FighterAttackDefinition ThrowAttack => throwAttack;
        public IReadOnlyList<FighterMoveBinding> MoveBindings => moveBindings;
        public bool HasMoveBindings => moveBindings != null && moveBindings.Count > 0;

        public FighterMoveBinding FindMoveById(string moveId)
        {
            if (string.IsNullOrWhiteSpace(moveId) || moveBindings == null)
            {
                return null;
            }

            for (int i = 0; i < moveBindings.Count; i++)
            {
                FighterMoveBinding binding = moveBindings[i];
                if (binding != null && string.Equals(binding.MoveId, moveId, StringComparison.Ordinal))
                {
                    return binding;
                }
            }

            return null;
        }

        private void OnValidate()
        {
            if (moveBindings == null)
            {
                return;
            }

            HashSet<string> moveIds = new HashSet<string>(StringComparer.Ordinal);
            for (int i = 0; i < moveBindings.Count; i++)
            {
                FighterMoveBinding binding = moveBindings[i];
                if (binding == null)
                {
                    Debug.LogWarning($"[GASG Fighter][技データ] {i}番目のBindingが空です。", this);
                    continue;
                }

                if (!binding.IsValid(out string reason))
                {
                    Debug.LogWarning($"[GASG Fighter][技データ] {reason}", this);
                    continue;
                }

                if (!moveIds.Add(binding.MoveId))
                {
                    Debug.LogError($"[GASG Fighter][技データ] 技IDが重複しています: {binding.MoveId}", this);
                }
            }
        }

        public FighterAttackDefinition GetAttack(AttackMotionId motionId)
        {
            switch (motionId)
            {
                case AttackMotionId.LightAttack:
                    return lightAttack;
                case AttackMotionId.MiddleAttack:
                    return middleAttack;
                case AttackMotionId.HeavyAttack:
                    return heavyAttack;
                case AttackMotionId.SpecialAttack:
                    return specialAttack;
                case AttackMotionId.Throw:
                    return throwAttack;
                default:
                    return null;
            }
        }

#if UNITY_EDITOR
        public void EditorConfigure(
            FighterAttackDefinition newLightAttack,
            FighterAttackDefinition newMiddleAttack,
            FighterAttackDefinition newHeavyAttack,
            FighterAttackDefinition newSpecialAttack,
            FighterAttackDefinition newThrowAttack)
        {
            lightAttack = newLightAttack;
            middleAttack = newMiddleAttack;
            heavyAttack = newHeavyAttack;
            specialAttack = newSpecialAttack;
            throwAttack = newThrowAttack;
        }
#endif
    }
}
