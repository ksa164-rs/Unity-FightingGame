using System;
using GASG.Fighting;
using UnityEditor;
using UnityEngine;

namespace GASG.Fighting.Editor
{
    public static class FightAttackDatabaseMigration
    {
        public const string DatabasePath = "Assets/GASGFighter/Data/AttackMotionDatabase_Prototype.asset";

        private const string ConfigPath = "Assets/GASGFighter/Data/FighterConfig_Prototype.asset";
        private const string LightPath = "Assets/GASGFighter/Data/Attack_Light.asset";
        private const string MiddlePath = "Assets/GASGFighter/Data/Attack_Medium.asset";
        private const string HeavyPath = "Assets/GASGFighter/Data/Attack_Heavy.asset";
        private const string SpecialPath = "Assets/GASGFighter/Data/Attack_Special.asset";
        private const string ThrowPath = "Assets/GASGFighter/Data/Attack_Throw.asset";
        private const string LightClipPath = "Assets/GASGFighter/Graphics/3D/Chara/animations/attack_001.anim";

        [MenuItem("GASG/Fighting Game/Open Attack Motion Database")]
        public static void OpenDatabase()
        {
            FighterAttackDatabase database = AssetDatabase.LoadAssetAtPath<FighterAttackDatabase>(DatabasePath);
            if (database == null)
            {
                database = CreateOrUpdateDatabase();
            }

            Selection.activeObject = database;
            EditorGUIUtility.PingObject(database);
        }

        [MenuItem("GASG/Fighting Game/Create or Update Attack Motion Database")]
        public static void CreateOrUpdateFromMenu()
        {
            FighterAttackDatabase database = CreateOrUpdateDatabase();
            Selection.activeObject = database;
            EditorGUIUtility.PingObject(database);
            Debug.Log($"[GASG Fighter][成功] 攻撃モーションを一括管理へ移行しました: {DatabasePath}");
        }

        public static FighterAttackDatabase CreateOrUpdateDatabase()
        {
            FighterAttackDefinition sourceLight = LoadRequiredAttack(LightPath);
            FighterAttackDefinition sourceMiddle = LoadRequiredAttack(MiddlePath);
            FighterAttackDefinition sourceHeavy = LoadRequiredAttack(HeavyPath);
            FighterAttackDefinition sourceSpecial = LoadRequiredAttack(SpecialPath);
            FighterAttackDefinition sourceThrow = LoadRequiredAttack(ThrowPath);

            FighterAttackDatabase database = AssetDatabase.LoadAssetAtPath<FighterAttackDatabase>(DatabasePath);
            if (database == null)
            {
                database = ScriptableObject.CreateInstance<FighterAttackDatabase>();
                database.name = "AttackMotionDatabase_Prototype";
                AssetDatabase.CreateAsset(database, DatabasePath);
            }

            FighterAttackDefinition light = GetOrCreateSubAsset(database, AttackMotionId.LightAttack, "LightAttack", sourceLight);
            FighterAttackDefinition middle = GetOrCreateSubAsset(database, AttackMotionId.MiddleAttack, "MiddleAttack", sourceMiddle);
            FighterAttackDefinition heavy = GetOrCreateSubAsset(database, AttackMotionId.HeavyAttack, "HeavyAttack", sourceHeavy);
            FighterAttackDefinition special = GetOrCreateSubAsset(database, AttackMotionId.SpecialAttack, "SpecialAttack", sourceSpecial);
            FighterAttackDefinition throwAttack = GetOrCreateSubAsset(database, AttackMotionId.Throw, "Throw", sourceThrow);

            AnimationClip lightClip = AssetDatabase.LoadAssetAtPath<AnimationClip>(LightClipPath);
            if (lightClip != null && light.AnimationClip == null)
            {
                SerializedObject serializedLight = new SerializedObject(light);
                serializedLight.FindProperty("animationClip").objectReferenceValue = lightClip;
                serializedLight.ApplyModifiedPropertiesWithoutUndo();
                EditorUtility.SetDirty(light);
            }

            database.EditorConfigure(light, middle, heavy, special, throwAttack);
            EditorUtility.SetDirty(database);

            FighterConfig config = AssetDatabase.LoadAssetAtPath<FighterConfig>(ConfigPath);
            if (config != null)
            {
                config.EditorSetAttackDatabase(database);
                EditorUtility.SetDirty(config);
            }
            AssetDatabase.SaveAssets();
            AssetDatabase.ImportAsset(DatabasePath, ImportAssetOptions.ForceUpdate);
            FightAttackAnimatorSynchronizer.Sync(database);
            return database;
        }

        private static FighterAttackDefinition GetOrCreateSubAsset(
            FighterAttackDatabase database,
            AttackMotionId motionId,
            string subAssetName,
            FighterAttackDefinition source)
        {
            FighterAttackDefinition existing = database.GetAttack(motionId);
            if (existing != null)
            {
                return existing;
            }

            UnityEngine.Object[] subAssets = AssetDatabase.LoadAllAssetsAtPath(DatabasePath);
            for (int i = 0; i < subAssets.Length; i++)
            {
                if (subAssets[i] is FighterAttackDefinition found && found.name == subAssetName)
                {
                    return found;
                }
            }

            FighterAttackDefinition created = UnityEngine.Object.Instantiate(source);
            created.name = subAssetName;
            created.hideFlags = HideFlags.HideInHierarchy;
            AssetDatabase.AddObjectToAsset(created, database);
            EditorUtility.SetDirty(created);
            Debug.Log($"[GASG Fighter][成功] Database内に攻撃データを作成しました: {subAssetName}");
            return created;
        }

        private static FighterAttackDefinition LoadRequiredAttack(string path)
        {
            FighterAttackDefinition attack = AssetDatabase.LoadAssetAtPath<FighterAttackDefinition>(path);
            if (attack == null)
            {
                throw new InvalidOperationException($"移行元の攻撃データが見つかりません: {path}");
            }

            return attack;
        }
    }
}
