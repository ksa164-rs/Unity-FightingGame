using System;
using System.Collections.Generic;
using GASG.Fighting;
using UnityEditor;
using UnityEngine;

namespace GASG.Fighting.Editor
{
    /// <summary>技データを変更せず、調整前に確認すべき矛盾を報告します。</summary>
    public static class FightCombatDataValidator
    {
        [MenuItem("GASG/対戦プロトタイプ/04. 確認（変更なし）/対戦データを検証")]
        public static void ValidateAll()
        {
            string[] guids = AssetDatabase.FindAssets("t:FighterAttackDatabase", new[] { "Assets" });
            if (guids.Length == 0)
            {
                Debug.Log("[GASG Fighter][スキップ] Attack Databaseがありません。");
                return;
            }
            try
            {
                for (int i = 0; i < guids.Length; i++)
                {
                    string path = AssetDatabase.GUIDToAssetPath(guids[i]);
                    EditorUtility.DisplayProgressBar("対戦データの検証（変更なし）", path, (float)i / guids.Length);
                    LogReport(AssetDatabase.LoadAssetAtPath<FighterAttackDatabase>(path));
                }
            }
            finally
            {
                EditorUtility.ClearProgressBar();
            }
        }

        public static void LogReport(FighterAttackDatabase database)
        {
            if (database == null)
            {
                Debug.LogWarning("[GASG Fighter][スキップ] Databaseが未設定です。");
                return;
            }
            List<string> issues = FindIssues(database);
            foreach (string issue in issues)
            {
                Debug.LogWarning($"[GASG Fighter][要確認] {database.name}: {issue}", database);
            }
            Debug.Log($"[GASG Fighter][検証完了] {database.name}: 要確認 {issues.Count}件。データ変更なし。", database);
        }

        public static List<string> FindIssues(FighterAttackDatabase database)
        {
            var issues = new List<string>();
            if (database == null)
            {
                issues.Add("Databaseが未設定です。");
                return issues;
            }
            var attacks = new HashSet<FighterAttackDefinition>();
            var ids = new HashSet<string>(StringComparer.Ordinal);
            var actions = new Dictionary<string, FighterAttackDefinition>(StringComparer.Ordinal);
            if (database.HasMoveBindings)
            {
                for (int i = 0; i < database.MoveBindings.Count; i++)
                {
                    FighterMoveBinding binding = database.MoveBindings[i];
                    if (binding == null) { issues.Add($"Binding {i}: 空です。"); continue; }
                    if (!binding.IsValid(out string reason)) issues.Add(reason);
                    if (!ids.Add(binding.MoveId ?? string.Empty)) issues.Add($"技ID重複: {binding.MoveId}");
                    if (binding.Attack != null) attacks.Add(binding.Attack);
                    for (int j = 0; j < i; j++)
                    {
                        FighterMoveBinding other = database.MoveBindings[j];
                        if (other != null && binding.InputButton == other.InputButton &&
                            binding.Priority == other.Priority && ContextsOverlap(binding, other) &&
                            SameDirections(binding.CommandDirections, other.CommandDirections))
                        {
                            issues.Add($"入力競合: {other.MoveId} / {binding.MoveId}。同じボタン・方向列・優先度で状態が重なります。リストの先頭が選ばれるため、状態または優先度を分けてください。");
                        }
                    }
                }
            }
            else
            {
                issues.Add("Bindingが空のため旧5技を使用中です。新旧の入力設定を混在させないでください。");
                foreach (AttackMotionId motion in Enum.GetValues(typeof(AttackMotionId)))
                {
                    FighterAttackDefinition attack = database.GetAttack(motion);
                    if (attack != null) attacks.Add(attack);
                }
            }
            foreach (FighterAttackDefinition attack in attacks)
            {
                ValidateAttack(attack, issues);
                if (!string.IsNullOrWhiteSpace(attack.ActionId))
                {
                    if (actions.TryGetValue(attack.ActionId, out FighterAttackDefinition other) &&
                        (attack.TotalFrames != other.TotalFrames || attack.AnimationClip != other.AnimationClip))
                    {
                        issues.Add($"{attack.DisplayName} / {other.DisplayName}: 同じAction IDで尺またはClipが異なります。Animator同期で後の技が上書きするため、別Actionを割り当ててください。");
                    }
                    else actions[attack.ActionId] = attack;
                }
            }
            return issues;
        }

        private static void ValidateAttack(FighterAttackDefinition attack, List<string> issues)
        {
            string label = attack.DisplayName;
            if (attack.AnimationClip == null) issues.Add($"{label}: Animation Clipが未設定です。");
            if (attack.IsProjectile)
            {
                if (attack.ProjectilePrefab == null) issues.Add($"{label}: 発射体Prefabが未設定です。");
                if (attack.ProjectileSpawnFrame >= attack.TotalFrames)
                    issues.Add($"{label}: 発射フレームが攻撃終了以降です。0〜{attack.TotalFrames - 1}に設定してください。");
                if (attack.IsThrow) issues.Add($"{label}: 飛び道具と投げが同時に有効です。");
            }
            else if (attack.HasFrameHitboxes)
            {
                int first = int.MaxValue;
                int last = -1;
                foreach (AttackHitboxFrame hitbox in attack.Hitboxes)
                {
                    if (hitbox == null) { issues.Add($"{label}: 空のHitboxがあります。"); continue; }
                    first = Mathf.Min(first, hitbox.StartFrame);
                    last = Mathf.Max(last, hitbox.EndFrame);
                    if (hitbox.StartFrame < 0 || hitbox.EndFrame < hitbox.StartFrame || hitbox.EndFrame >= attack.TotalFrames)
                        issues.Add($"{label}/{hitbox.Label}: Hitboxが攻撃フレーム範囲外です。");
                    if (hitbox.Size.x <= 0f || hitbox.Size.y <= 0f || hitbox.Size.z <= 0f)
                        issues.Add($"{label}/{hitbox.Label}: Hitboxのサイズは全軸で正数が必要です。");
                }
                if (first != attack.StartupFrames || last != attack.StartupFrames + attack.ActiveFrames - 1)
                    issues.Add($"{label}: 発生・持続とHitboxの開始・終了が一致しません。実判定はHitbox側が優先されるため、表示硬直差も確認してください。");
            }
            if (attack.EnableAttackCancel && (attack.CancelTargets == AttackCancelTarget.None ||
                attack.CancelStartFrame < 0 || attack.CancelEndFrame < attack.CancelStartFrame ||
                attack.CancelEndFrame >= attack.TotalFrames))
                issues.Add($"{label}: キャンセル先または受付範囲が不正です。");
        }

        private static bool ContextsOverlap(FighterMoveBinding first, FighterMoveBinding second)
        {
            return (first.IsAvailableIn(FighterMoveContext.Standing) && second.IsAvailableIn(FighterMoveContext.Standing)) ||
                   (first.IsAvailableIn(FighterMoveContext.Crouching) && second.IsAvailableIn(FighterMoveContext.Crouching)) ||
                   (first.IsAvailableIn(FighterMoveContext.Airborne) && second.IsAvailableIn(FighterMoveContext.Airborne));
        }

        private static bool SameDirections(IReadOnlyList<FighterInputDirection> first, IReadOnlyList<FighterInputDirection> second)
        {
            int count = first?.Count ?? 0;
            if (count != (second?.Count ?? 0)) return false;
            for (int i = 0; i < count; i++) if (first[i] != second[i]) return false;
            return true;
        }
    }
}
