using GASG.Fighting;
using UnityEditor;
using UnityEngine;

namespace GASG.Fighting.Editor
{
    public sealed class FightActionManagerWindow : EditorWindow
    {
        private FighterActionCatalog catalog;
        private FighterAnimationProfile profile;
        private FighterAttackDatabase attackDatabase;
        private Vector2 scrollPosition;
        private string newActionId = "AS_p01_000_001";
        private string newDisplayName = "新規モーション";
        private int characterNumber = 1;
        private int categoryIndex;
        private bool showInternalIds;
        private static readonly int[] Categories = { 0, 100, 200, 300, 400, 900 };
        private static readonly string[] CategoryLabels = { "000 基本移動", "100 通常攻撃", "200 必殺技", "300 投げ", "400 防御・被弾", "900 システム" };
        private AnimationClip newAnimationClip;

        [MenuItem("GASG/Fighting Game/Open Animation Action Manager")]
        public static void Open()
        {
            FightActionManagerWindow window = GetWindow<FightActionManagerWindow>("モーション管理");
            window.minSize = new Vector2(560f, 520f);
            window.Show();
        }

        private void OnEnable()
        {
            ReloadAssets();
        }

        private void OnGUI()
        {
            EditorGUILayout.Space(6f);
            EditorGUILayout.LabelField("モーション管理", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox(
                "管理番号 / 表示名 / Animation Clipを一元管理します。AS_p01_000_001 = モーション / キャラクター01 / 分類000 / 連番001。番号は登録後に固定し、表示名は自由に変更できます。",
                MessageType.Info);

            if (catalog == null || profile == null)
            {
                EditorGUILayout.HelpBox("Action Catalogの初期設定がまだ作成されていません。", MessageType.Warning);
                if (GUILayout.Button("既存モーションから初期設定を作成"))
                {
                    FightActionCatalogMigration.CreateOrUpdate();
                    ReloadAssets();
                }

                return;
            }

            scrollPosition = EditorGUILayout.BeginScrollView(scrollPosition);
            showInternalIds = EditorGUILayout.Toggle("内部接続IDも表示（保守用）", showInternalIds);
            DrawProfile();
            DrawActions();
            DrawNewAction();
            EditorGUILayout.EndScrollView();

            EditorGUILayout.Space(6f);
            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("検証（変更なし）"))
                {
                    ValidateCatalog();
                }

                if (GUILayout.Button("Animatorへ反映・保存"))
                {
                    ApplyAndSave();
                }
            }
        }

        private void DrawProfile()
        {
            EditorGUILayout.Space(4f);
            EditorGUILayout.LabelField("基本動作に使うモーション", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox(
                "待機・ジャンプなどの役割ごとに、管理番号と表示名でモーションを選びます。",
                MessageType.None);

            SerializedObject serializedProfile = new SerializedObject(profile);
            serializedProfile.Update();
            DrawActionPopup(serializedProfile.FindProperty("idle"), "待機");
            DrawActionPopup(serializedProfile.FindProperty("crouchIdle"), "しゃがみ待機");
            DrawActionPopup(serializedProfile.FindProperty("jump"), "ジャンプ");
            DrawActionPopup(serializedProfile.FindProperty("land"), "着地");
            DrawActionPopup(serializedProfile.FindProperty("forwardStep"), "前ステップ");
            DrawActionPopup(serializedProfile.FindProperty("backwardStep"), "後ろステップ");
            DrawActionPopup(serializedProfile.FindProperty("block"), "ガード");
            DrawActionPopup(serializedProfile.FindProperty("hit"), "被弾");
            DrawActionPopup(serializedProfile.FindProperty("knockdown"), "ダウン");
            DrawActionPopup(serializedProfile.FindProperty("thrown"), "投げられ");
            DrawActionPopup(serializedProfile.FindProperty("reset"), "リセット");
            if (serializedProfile.ApplyModifiedProperties())
            {
                EditorUtility.SetDirty(profile);
            }
        }

        private void DrawActionPopup(SerializedProperty property, string label)
        {
            int selectedIndex = 0;
            string[] options = new string[catalog.Actions.Count + 1];
            options[0] = "（未設定）";
            for (int i = 0; i < catalog.Actions.Count; i++)
            {
                FighterActionDefinition action = catalog.Actions[i];
                options[i + 1] = action != null
                    ? action.DisplayLabel
                    : "（参照切れ）";
                if (action == property.objectReferenceValue)
                {
                    selectedIndex = i + 1;
                }
            }

            int newIndex = EditorGUILayout.Popup(label, selectedIndex, options);
            // 一覧外の参照は、ユーザーが選び直すまで保持する。
            if (newIndex != selectedIndex)
                property.objectReferenceValue = newIndex > 0 ? catalog.Actions[newIndex - 1] : null;
        }

        private void DrawActions()
        {
            EditorGUILayout.Space(10f);
            EditorGUILayout.LabelField($"登録モーション（{catalog.Actions.Count}）", EditorStyles.boldLabel);

            for (int i = 0; i < catalog.Actions.Count; i++)
            {
                FighterActionDefinition action = catalog.Actions[i];
                if (action == null)
                {
                    EditorGUILayout.HelpBox($"{i}番目のAction参照が空です。", MessageType.Error);
                    continue;
                }

                SerializedObject serializedAction = new SerializedObject(action);
                serializedAction.Update();
                using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
                {
                    using (new EditorGUI.DisabledScope(true))
                    {
                        EditorGUILayout.TextField("管理番号", string.IsNullOrEmpty(action.MotionId) ? "未採番" : action.MotionId);
                        if (showInternalIds) EditorGUILayout.TextField("内部接続ID", action.ActionId);
                    }

                    EditorGUILayout.PropertyField(serializedAction.FindProperty("displayName"), new GUIContent("表示名"));
                    EditorGUILayout.PropertyField(serializedAction.FindProperty("animationClip"), new GUIContent("Animation Clip"));
                }

                if (serializedAction.ApplyModifiedProperties())
                {
                    EditorUtility.SetDirty(action);
                }
            }
        }

        private void DrawNewAction()
        {
            EditorGUILayout.Space(10f);
            EditorGUILayout.LabelField("モーションを追加", EditorStyles.boldLabel);
            EditorGUI.BeginChangeCheck();
            characterNumber = Mathf.Clamp(EditorGUILayout.IntField("キャラクター番号", characterNumber), 1, 99);
            categoryIndex = EditorGUILayout.Popup("分類", categoryIndex, CategoryLabels);
            bool categoryChanged = EditorGUI.EndChangeCheck();
            bool suggestNumber = GUILayout.Button("次の管理番号を提案");
            if (categoryChanged || suggestNumber) SuggestNextId();
            newActionId = EditorGUILayout.TextField("管理番号", newActionId);
            newDisplayName = EditorGUILayout.TextField("表示名", newDisplayName);
            newAnimationClip = (AnimationClip)EditorGUILayout.ObjectField(
                "Animation Clip", newAnimationClip, typeof(AnimationClip), false);

            if (GUILayout.Button("新規モーションを追加"))
            {
                try
                {
                    if (!FighterMotionNaming.IsValid(newActionId))
                        throw new System.InvalidOperationException("管理番号はAS_p01_000_001形式で入力してください。全角文字は使えません。");
                    FightActionCatalogMigration.CreateAction(
                        catalog,
                        newActionId,
                        newDisplayName,
                        newAnimationClip);
                    SuggestNextId();
                    newDisplayName = "新規モーション";
                    newAnimationClip = null;
                    Repaint();
                }
                catch (System.Exception exception)
                {
                    Debug.LogError($"[GASG Fighter][失敗] Actionを作成できませんでした: {exception.Message}");
                    EditorUtility.DisplayDialog("Action作成エラー", exception.Message, "OK");
                }
            }
        }

        private void SuggestNextId()
        {
            try
            {
                newActionId = FighterMotionNaming.SuggestNext(catalog, characterNumber, Categories[categoryIndex]);
            }
            catch (System.ArgumentOutOfRangeException)
            {
                newActionId = string.Empty;
                Debug.LogWarning("[GASG Fighter][スキップ] この分類の連番は999件までです。別の分類を使用してください。");
            }
        }

        private void ApplyAndSave()
        {
            if (!catalog.ValidateCatalog(out string report))
            {
                Debug.LogError($"[GASG Fighter][失敗] Action Catalogを保存できません。\n{report}", catalog);
                EditorUtility.DisplayDialog("Action Catalog検証エラー", report, "OK");
                return;
            }

            FighterConfig config = AssetDatabase.LoadAssetAtPath<FighterConfig>(
                "Assets/GASGFighter/Data/FighterConfig_Prototype.asset");
            if (config != null && config.AnimationProfile != profile)
            {
                Undo.RecordObject(config, "Assign Animation Profile");
                config.EditorSetAnimationProfile(profile);
                EditorUtility.SetDirty(config);
            }

            AssetDatabase.SaveAssets();
            bool synced = attackDatabase != null && FightAttackAnimatorSynchronizer.Sync(attackDatabase);
            Debug.Log($"[GASG Fighter][保存完了] Action Catalogを保存しました。Animator同期: {(synced ? "成功" : "スキップ（DatabaseとConsoleを確認）")}");
        }

        private void ValidateCatalog()
        {
            bool valid = catalog.ValidateCatalog(out string report);
            Debug.Log($"[GASG Fighter][{(valid ? "成功" : "失敗")}] Action Catalog検証\n{report}", catalog);
            EditorUtility.DisplayDialog("Action Catalog検証", report, "OK");
        }

        private void ReloadAssets()
        {
            catalog = AssetDatabase.LoadAssetAtPath<FighterActionCatalog>(FightActionCatalogMigration.CatalogPath);
            profile = AssetDatabase.LoadAssetAtPath<FighterAnimationProfile>(FightActionCatalogMigration.ProfilePath);
            attackDatabase = AssetDatabase.LoadAssetAtPath<FighterAttackDatabase>(
                "Assets/GASGFighter/Data/AttackMotionDatabase_Prototype.asset");
            SuggestNextId();
        }
    }
}
