using System;
using System.Collections.Generic;
using GASG.Fighting;
using UnityEditor;
using UnityEngine;

namespace GASG.Fighting.Editor
{
    public static class FightActionCatalogMigration
    {
        public const string CatalogPath = "Assets/GASGFighter/Data/AnimationActionCatalog_Prototype.asset";
        public const string ProfilePath = "Assets/GASGFighter/Data/AnimationProfile_Prototype.asset";

        private const string DatabasePath = "Assets/GASGFighter/Data/AttackMotionDatabase_Prototype.asset";
        private const string ConfigPath = "Assets/GASGFighter/Data/FighterConfig_Prototype.asset";
        private const string IdleClipPath = "Assets/GASGFighter/Graphics/3D/Chara/animations/wait.anim";
        private const string CrouchClipPath = "Assets/GASGFighter/Graphics/3D/Chara/animations/wait_shagami.anim";

        [MenuItem("GASG/Fighting Game/Create or Update Animation Action Catalog")]
        public static void CreateOrUpdateFromMenu()
        {
            FighterActionCatalog catalog = CreateOrUpdate();
            Selection.activeObject = catalog;
            EditorGUIUtility.PingObject(catalog);
        }

        public static FighterActionCatalog CreateOrUpdate()
        {
            FighterActionCatalog catalog = AssetDatabase.LoadAssetAtPath<FighterActionCatalog>(CatalogPath);
            if (catalog == null)
            {
                catalog = ScriptableObject.CreateInstance<FighterActionCatalog>();
                catalog.name = "AnimationActionCatalog_Prototype";
                AssetDatabase.CreateAsset(catalog, CatalogPath);
                Debug.Log($"[GASG Fighter][成功] Action Catalogを作成しました: {CatalogPath}");
            }

            FighterActionDefinition idle = GetOrCreateAction(
                catalog, "Idle", "待機", AssetDatabase.LoadAssetAtPath<AnimationClip>(IdleClipPath));
            FighterActionDefinition crouchIdle = GetOrCreateAction(
                catalog, "CrouchIdle", "しゃがみ待機", AssetDatabase.LoadAssetAtPath<AnimationClip>(CrouchClipPath));
            FighterActionDefinition jump = GetOrCreateAction(catalog, "Jump", "ジャンプ", null);
            FighterActionDefinition land = GetOrCreateAction(catalog, "Land", "着地", null);
            FighterActionDefinition forwardStep = GetOrCreateAction(catalog, "ForwardStep", "前ステップ", null);
            FighterActionDefinition backwardStep = GetOrCreateAction(catalog, "BackwardStep", "後ろステップ", null);
            FighterActionDefinition block = GetOrCreateAction(catalog, "Block", "ガード", null);
            FighterActionDefinition hit = GetOrCreateAction(catalog, "Hit", "被弾", null);
            FighterActionDefinition knockdown = GetOrCreateAction(catalog, "Knockdown", "ダウン", null);
            FighterActionDefinition thrown = GetOrCreateAction(catalog, "Thrown", "投げられ", null);
            FighterActionDefinition reset = GetOrCreateAction(catalog, "Reset", "ラウンドリセット", null);

            FighterAnimationProfile profile = AssetDatabase.LoadAssetAtPath<FighterAnimationProfile>(ProfilePath);
            bool profileWasCreated = false;
            if (profile == null)
            {
                profile = ScriptableObject.CreateInstance<FighterAnimationProfile>();
                profile.name = "AnimationProfile_Prototype";
                AssetDatabase.CreateAsset(profile, ProfilePath);
                profileWasCreated = true;
                Debug.Log($"[GASG Fighter][成功] Animation Profileを作成しました: {ProfilePath}");
            }

            if (profileWasCreated)
            {
                profile.EditorConfigure(
                    idle, crouchIdle, jump, land, forwardStep, backwardStep,
                    block, hit, knockdown, thrown, reset);
            }
            else
            {
                // 再実行時はユーザーが差し替えた参照を保持し、空欄だけを補完する。
                profile.EditorFillMissing(
                    idle, crouchIdle, jump, land, forwardStep, backwardStep,
                    block, hit, knockdown, thrown, reset);
            }
            EditorUtility.SetDirty(profile);

            FighterAttackDatabase database = AssetDatabase.LoadAssetAtPath<FighterAttackDatabase>(DatabasePath);
            if (database != null)
            {
                MigrateAttackActions(catalog, database);
            }

            FighterConfig config = AssetDatabase.LoadAssetAtPath<FighterConfig>(ConfigPath);
            if (config != null && config.AnimationProfile == null)
            {
                config.EditorSetAnimationProfile(profile);
                EditorUtility.SetDirty(config);
            }

            EditorUtility.SetDirty(catalog);
            AssetDatabase.SaveAssets();
            bool synced = database != null && FightAttackAnimatorSynchronizer.Sync(database);
            Debug.Log($"[GASG Fighter][移行完了] 既存モーションをAction ID管理へ移行しました。Animator同期: {(synced ? "成功" : "スキップ（DatabaseとConsoleを確認）")}");
            return catalog;
        }

        public static FighterActionDefinition CreateAction(
            FighterActionCatalog catalog,
            string actionId,
            string displayName,
            AnimationClip clip)
        {
            if (catalog == null)
            {
                throw new ArgumentNullException(nameof(catalog));
            }

            string normalizedId = actionId != null ? actionId.Trim() : string.Empty;
            if (string.IsNullOrWhiteSpace(normalizedId))
            {
                throw new InvalidOperationException("Action IDを入力してください。");
            }

            if (!FighterActionCatalog.IsValidActionId(normalizedId))
            {
                throw new InvalidOperationException(
                    "Action IDに使用できるのは英数字、アンダースコア、ハイフン、ピリオドです。");
            }

            FighterActionDefinition existing = catalog.FindById(normalizedId);
            if (existing != null || catalog.FindByMotionId(normalizedId) != null)
            {
                throw new InvalidOperationException($"Action IDは既に登録されています: {normalizedId}");
            }

            FighterActionDefinition created = ScriptableObject.CreateInstance<FighterActionDefinition>();
            created.name = normalizedId;
            created.hideFlags = HideFlags.HideInHierarchy;
            created.EditorConfigure(normalizedId, displayName, clip);
            if (FighterMotionNaming.IsValid(normalizedId))
            {
                created.EditorSetMotionIdentity(normalizedId, displayName);
            }
            Undo.RecordObject(catalog, "Add Motion");
            AssetDatabase.AddObjectToAsset(created, catalog);
            Undo.RegisterCreatedObjectUndo(created, "Add Motion");
            catalog.EditorAddAction(created);
            EditorUtility.SetDirty(created);
            EditorUtility.SetDirty(catalog);
            AssetDatabase.SaveAssets();
            Debug.Log($"[GASG Fighter][成功] Actionを作成しました: {normalizedId}");
            return created;
        }

        private static FighterActionDefinition GetOrCreateAction(
            FighterActionCatalog catalog,
            string actionId,
            string displayName,
            AnimationClip defaultClip)
        {
            FighterActionDefinition action = catalog.FindById(actionId);
            if (action != null)
            {
                if (action.AnimationClip == null && defaultClip != null)
                {
                    action.EditorConfigure(action.ActionId, action.DisplayName, defaultClip);
                    EditorUtility.SetDirty(action);
                }

                return action;
            }

            return CreateAction(catalog, actionId, displayName, defaultClip);
        }

        private static void MigrateAttackActions(FighterActionCatalog catalog, FighterAttackDatabase database)
        {
            HashSet<FighterAttackDefinition> attacks = new HashSet<FighterAttackDefinition>();
            foreach (AttackMotionId motionId in Enum.GetValues(typeof(AttackMotionId)))
            {
                FighterAttackDefinition attack = database.GetAttack(motionId);
                if (attack != null)
                {
                    attacks.Add(attack);
                }
            }

            IReadOnlyList<FighterMoveBinding> bindings = database.MoveBindings;
            for (int i = 0; i < bindings.Count; i++)
            {
                if (bindings[i]?.Attack != null)
                {
                    attacks.Add(bindings[i].Attack);
                }
            }

            foreach (FighterAttackDefinition attack in attacks)
            {
                if (attack.Action != null)
                {
                    continue;
                }

                string actionId = string.IsNullOrWhiteSpace(attack.ActionId) ? attack.name : attack.ActionId;
                FighterActionDefinition action = catalog.FindById(actionId) ??
                                                   CreateAction(catalog, actionId, attack.DisplayName, attack.AnimationClip);
                attack.EditorSetAction(action);
                EditorUtility.SetDirty(attack);
            }
        }
    }
}
