using GASG.Fighting;
using UnityEditor;
using UnityEngine;

namespace GASG.Fighting.Editor
{
    [CustomEditor(typeof(FighterAttackDatabase))]
    public sealed class FightAttackDatabaseEditor : UnityEditor.Editor
    {
        private int selectedAttackIndex;
        private Vector2 scrollPosition;
        private bool showInputSettings;

        public override void OnInspectorGUI()
        {
            FighterAttackDatabase database = (FighterAttackDatabase)target;

            EditorGUILayout.Space(4f);
            EditorGUILayout.LabelField("攻撃モーション一括管理", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox(
                "上部で攻撃を選択すると、そのモーションに必要なアニメーション・フレーム・ダメージ・HitStop・キャンセル・Hitboxをまとめて調整できます。",
                MessageType.Info);
            EditorGUILayout.HelpBox(
                "モーション管理で管理番号・表示名・Clipを登録 → 技の再生モーションを選択 → ゲームへ反映・保存 → Play。",
                MessageType.None);

            DrawMoveBindings();
            if (database.HasMoveBindings)
            {
                EditorGUILayout.LabelField("コマンド一覧（右向き基準）", EditorStyles.boldLabel);
                foreach (FighterMoveBinding binding in database.MoveBindings)
                    FightCommandIconDrawer.DrawCommand(binding);
            }

            if (GUILayout.Button("このDatabaseの矛盾をチェック（変更なし）"))
            {
                FightCombatDataValidator.LogReport(database);
            }

            var attacks = new System.Collections.Generic.List<FighterAttackDefinition>();
            var labels = new System.Collections.Generic.List<string>();
            if (database.HasMoveBindings)
            {
                foreach (FighterMoveBinding binding in database.MoveBindings)
                {
                    if (binding == null) continue;
                    attacks.Add(binding.Attack);
                    labels.Add($"{FightCommandIconDrawer.DisplayName(binding)} [{binding.MoveId}]");
                }
            }
            else
            {
                foreach (AttackMotionId motion in System.Enum.GetValues(typeof(AttackMotionId)))
                {
                    attacks.Add(database.GetAttack(motion));
                    labels.Add(motion.ToString());
                }
            }
            selectedAttackIndex = Mathf.Clamp(selectedAttackIndex, 0, Mathf.Max(0, attacks.Count - 1));
            if (attacks.Count > 0)
            {
                selectedAttackIndex = EditorGUILayout.Popup("編集する技", selectedAttackIndex, labels.ToArray());
            }
            FighterAttackDefinition attack = attacks.Count > 0 ? attacks[selectedAttackIndex] : null;
            if (attack == null)
            {
                EditorGUILayout.HelpBox(
                    "選択した攻撃データが登録されていません。移行メニューを実行するか、Databaseへ攻撃データを登録してください。",
                    MessageType.Warning);
                DrawDatabaseReferences();
                return;
            }

            SerializedObject serializedAttack = new SerializedObject(attack);
            serializedAttack.Update();

            scrollPosition = EditorGUILayout.BeginScrollView(scrollPosition);
            EditorGUI.BeginChangeCheck();
            DrawMotionSection(serializedAttack);
            bool motionBindingChanged = EditorGUI.EndChangeCheck();
            DrawFrameSection(serializedAttack);
            DrawCancelSection(serializedAttack);
            DrawHitSection(serializedAttack);
            DrawProjectileSection(serializedAttack);
            DrawHitboxSection(serializedAttack);
            DrawThrowSection(serializedAttack);
            EditorGUILayout.EndScrollView();

            bool propertiesChanged = serializedAttack.ApplyModifiedProperties();
            if (propertiesChanged)
            {
                EditorUtility.SetDirty(attack);
                SceneView.RepaintAll();

                if (motionBindingChanged)
                {
                    FighterAttackDatabase databaseToSync = database;
                    EditorApplication.delayCall += () =>
                    {
                        if (databaseToSync != null)
                        {
                            if (FightAttackAnimatorSynchronizer.Sync(databaseToSync))
                            {
                                Debug.Log($"[GASG Fighter][成功] 攻撃Actionをゲームへ同期しました: {databaseToSync.name}");
                            }
                        }
                    };
                }
            }

            EditorGUILayout.Space(6f);
            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("Hitbox Editorで詳細調整"))
                {
                    FightHitboxEditorWindow.OpenWithAttack(attack);
                }

                if (GUILayout.Button("ゲームへ反映・保存", GUILayout.Width(140f)))
                {
                    AssetDatabase.SaveAssets();
                    bool synced = FightAttackAnimatorSynchronizer.Sync(database);
                    Debug.Log($"[GASG Fighter][保存完了] {database.name} / {attack.DisplayName}。Animator同期: {(synced ? "成功" : "スキップ（Consoleを確認）")}");
                }
            }
        }

        private void DrawDatabaseReferences()
        {
            serializedObject.Update();
            EditorGUILayout.Space(6f);
            EditorGUILayout.LabelField("登録データ", EditorStyles.boldLabel);
            EditorGUILayout.PropertyField(serializedObject.FindProperty("lightAttack"));
            EditorGUILayout.PropertyField(serializedObject.FindProperty("middleAttack"));
            EditorGUILayout.PropertyField(serializedObject.FindProperty("heavyAttack"));
            EditorGUILayout.PropertyField(serializedObject.FindProperty("specialAttack"));
            EditorGUILayout.PropertyField(serializedObject.FindProperty("throwAttack"));
            serializedObject.ApplyModifiedProperties();
        }

        private void DrawMoveBindings()
        {
            serializedObject.Update();
            EditorGUILayout.Space(6f);
            showInputSettings = EditorGUILayout.Foldout(showInputSettings, "入力設定（技ID・コマンド・使用状態）", true);
            if (!showInputSettings) return;
            EditorGUILayout.HelpBox(
                "方向は右向き基準の矢印で表示します。青＝弱、黄＝中、赤＝強、SP＝必殺。解決優先度が高い技を優先します。",
                MessageType.None);
            EditorGUILayout.PropertyField(serializedObject.FindProperty("moveBindings"), true);
            if (serializedObject.ApplyModifiedProperties())
            {
                EditorUtility.SetDirty(target);
            }
        }

        private static void DrawMotionSection(SerializedObject serializedAttack)
        {
            EditorGUILayout.Space(5f);
            EditorGUILayout.LabelField("モーション", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox(
                "再生するモーションを管理番号 / 表示名で選びます。Clipの差し替えはモーション管理で行います。",
                MessageType.None);
            EditorGUI.indentLevel++;
            EditorGUILayout.PropertyField(serializedAttack.FindProperty("displayName"), new GUIContent("表示名"));
            SerializedProperty actionProperty = serializedAttack.FindProperty("action");
            DrawActionPopup(actionProperty, "再生モーション");
            FighterActionDefinition action = actionProperty.objectReferenceValue as FighterActionDefinition;
            if (action != null)
            {
                using (new EditorGUI.DisabledScope(true))
                {
                    EditorGUILayout.TextField("モーション管理番号", action.MotionId);
                    EditorGUILayout.ObjectField("Animation Clip", action.AnimationClip, typeof(AnimationClip), false);
                }
            }
            else
            {
                EditorGUILayout.HelpBox(
                    "再生モーションが未設定です。GASG > Fighting Game > Open Animation Action Managerから登録してください。",
                    MessageType.Warning);
            }
            EditorGUI.indentLevel--;
        }

        private static void DrawActionPopup(SerializedProperty actionProperty, string label)
        {
            FighterActionCatalog catalog = AssetDatabase.LoadAssetAtPath<FighterActionCatalog>(
                FightActionCatalogMigration.CatalogPath);
            if (catalog == null)
            {
                EditorGUILayout.PropertyField(actionProperty, new GUIContent(label));
                return;
            }

            int selectedIndex = 0;
            string[] options = new string[catalog.Actions.Count + 1];
            options[0] = "（未設定）";
            for (int i = 0; i < catalog.Actions.Count; i++)
            {
                FighterActionDefinition candidate = catalog.Actions[i];
                options[i + 1] = candidate != null
                    ? candidate.DisplayLabel
                    : "（参照切れ）";
                if (candidate == actionProperty.objectReferenceValue)
                {
                    selectedIndex = i + 1;
                }
            }

            int newIndex = EditorGUILayout.Popup(label, selectedIndex, options);
            if (newIndex != selectedIndex)
                actionProperty.objectReferenceValue = newIndex > 0 ? catalog.Actions[newIndex - 1] : null;
        }

        private static void DrawFrameSection(SerializedObject serializedAttack)
        {
            EditorGUILayout.Space(5f);
            EditorGUILayout.LabelField("フレーム管理（60fps基準）", EditorStyles.boldLabel);
            EditorGUI.indentLevel++;
            SerializedProperty startup = serializedAttack.FindProperty("startupFrames");
            SerializedProperty active = serializedAttack.FindProperty("activeFrames");
            SerializedProperty recovery = serializedAttack.FindProperty("recoveryFrames");
            EditorGUILayout.PropertyField(startup, new GUIContent("発生フレーム"));
            EditorGUILayout.PropertyField(active, new GUIContent("持続フレーム"));
            EditorGUILayout.PropertyField(recovery, new GUIContent("後隙フレーム"));
            int totalFrames = Mathf.Max(1, startup.intValue) + Mathf.Max(1, active.intValue) + Mathf.Max(1, recovery.intValue);
            EditorGUILayout.LabelField("攻撃全体", $"{totalFrames} F");
            EditorGUILayout.LabelField("通常行動へ戻れるタイミング", $"{totalFrames} F目以降");
            EditorGUI.indentLevel--;
        }

        private static void DrawCancelSection(SerializedObject serializedAttack)
        {
            EditorGUILayout.Space(5f);
            EditorGUILayout.LabelField("攻撃キャンセル", EditorStyles.boldLabel);
            SerializedProperty enabled = serializedAttack.FindProperty("enableAttackCancel");
            EditorGUILayout.PropertyField(enabled, new GUIContent("キャンセルを有効化"));
            using (new EditorGUI.DisabledScope(!enabled.boolValue))
            {
                EditorGUI.indentLevel++;
                EditorGUILayout.PropertyField(serializedAttack.FindProperty("cancelStartFrame"), new GUIContent("受付開始フレーム"));
                EditorGUILayout.PropertyField(serializedAttack.FindProperty("cancelEndFrame"), new GUIContent("受付終了フレーム"));
                EditorGUILayout.PropertyField(serializedAttack.FindProperty("cancelOnHitOrBlockOnly"), new GUIContent("ヒット／ガード時のみ"));
                EditorGUILayout.PropertyField(serializedAttack.FindProperty("cancelTargets"), new GUIContent("キャンセル可能な攻撃"));
                EditorGUI.indentLevel--;
            }
        }

        private static void DrawHitSection(SerializedObject serializedAttack)
        {
            EditorGUILayout.Space(5f);
            EditorGUILayout.LabelField("ヒット・ガード効果", EditorStyles.boldLabel);
            EditorGUI.indentLevel++;
            EditorGUILayout.PropertyField(serializedAttack.FindProperty("damage"), new GUIContent("ダメージ"));
            EditorGUILayout.PropertyField(serializedAttack.FindProperty("hitStopFrames"), new GUIContent("HitStopフレーム"));
            EditorGUILayout.PropertyField(serializedAttack.FindProperty("attackerKnockbackOnHit"), new GUIContent("攻撃側ノックバック（ヒット）"));
            EditorGUILayout.PropertyField(serializedAttack.FindProperty("attackerKnockbackOnBlock"), new GUIContent("攻撃側ノックバック（ガード）"));
            SerializedProperty hitStun = serializedAttack.FindProperty("hitStunFrames");
            SerializedProperty blockStun = serializedAttack.FindProperty("blockStunFrames");
            EditorGUILayout.PropertyField(hitStun, new GUIContent("ヒット硬直"));
            EditorGUILayout.PropertyField(blockStun, new GUIContent("ガード硬直"));

            int activeFrames = Mathf.Max(1, serializedAttack.FindProperty("activeFrames").intValue);
            int recoveryFrames = Mathf.Max(1, serializedAttack.FindProperty("recoveryFrames").intValue);
            bool causesKnockdown = serializedAttack.FindProperty("isThrow").boolValue ||
                                   serializedAttack.FindProperty("knockdownFrames").intValue > 0;
            string hitAdvantage = causesKnockdown
                ? "D（ダウン）"
                : FormatFrameAdvantage(hitStun.intValue - activeFrames - recoveryFrames);
            string blockAdvantage = FormatFrameAdvantage(blockStun.intValue - activeFrames - recoveryFrames);
            EditorGUILayout.LabelField("最速ヒット時の硬直差", hitAdvantage);
            EditorGUILayout.LabelField("最速ガード時の硬直差", blockAdvantage);
            EditorGUILayout.PropertyField(serializedAttack.FindProperty("knockback"), new GUIContent("ノックバック"));
            EditorGUILayout.PropertyField(serializedAttack.FindProperty("guardHeight"), new GUIContent("ガード属性"));
            EditorGUI.indentLevel--;

            EditorGUILayout.Space(3f);
            EditorGUILayout.LabelField("カメラシェイク", EditorStyles.boldLabel);
            EditorGUI.indentLevel++;
            EditorGUILayout.PropertyField(
                serializedAttack.FindProperty("cameraShakePositionAmplitude"),
                new GUIContent("位置の振幅"));
            EditorGUILayout.PropertyField(
                serializedAttack.FindProperty("cameraShakeRotationAmplitude"),
                new GUIContent("回転の振幅（度）"));
            EditorGUILayout.PropertyField(
                serializedAttack.FindProperty("cameraShakeDurationFrames"),
                new GUIContent("継続フレーム"));
            EditorGUILayout.PropertyField(
                serializedAttack.FindProperty("cameraShakeFrequency"),
                new GUIContent("周波数"));
            EditorGUI.indentLevel--;
        }

        private static string FormatFrameAdvantage(int frames)
        {
            return frames > 0 ? $"+{frames} F" : $"{frames} F";
        }

        private static void DrawProjectileSection(SerializedObject serializedAttack)
        {
            EditorGUILayout.Space(5f);
            EditorGUILayout.LabelField("飛び道具", EditorStyles.boldLabel);
            SerializedProperty enabled = serializedAttack.FindProperty("isProjectile");
            EditorGUILayout.PropertyField(enabled, new GUIContent("飛び道具として扱う"));
            using (new EditorGUI.DisabledScope(!enabled.boolValue))
            {
                EditorGUI.indentLevel++;
                EditorGUILayout.PropertyField(serializedAttack.FindProperty("projectilePrefab"), new GUIContent("発射体VFX Prefab"));
                EditorGUILayout.PropertyField(serializedAttack.FindProperty("projectileImpactPrefab"), new GUIContent("命中VFX Prefab"));
                EditorGUILayout.PropertyField(serializedAttack.FindProperty("projectileSpawnFrame"), new GUIContent("発射フレーム"));
                EditorGUILayout.PropertyField(serializedAttack.FindProperty("projectileSpawnOffset"), new GUIContent("発射位置"));
                EditorGUILayout.PropertyField(serializedAttack.FindProperty("projectileSpeed"), new GUIContent("飛翔速度"));
                EditorGUILayout.PropertyField(serializedAttack.FindProperty("projectileRadius"), new GUIContent("当たり判定半径"));
                EditorGUILayout.PropertyField(serializedAttack.FindProperty("projectileLifetime"), new GUIContent("最大生存時間"));
                EditorGUILayout.PropertyField(serializedAttack.FindProperty("impactVfxLifetime"), new GUIContent("命中VFX生存時間"));
                EditorGUI.indentLevel--;
            }
        }

        private static void DrawHitboxSection(SerializedObject serializedAttack)
        {
            EditorGUILayout.Space(5f);
            EditorGUILayout.LabelField("Hitbox", EditorStyles.boldLabel);
            EditorGUILayout.PropertyField(serializedAttack.FindProperty("hitboxes"), new GUIContent("フレーム別Hitbox"), true);
        }

        private static void DrawThrowSection(SerializedObject serializedAttack)
        {
            EditorGUILayout.Space(5f);
            EditorGUILayout.LabelField("投げ・ダウン", EditorStyles.boldLabel);
            EditorGUI.indentLevel++;
            EditorGUILayout.PropertyField(serializedAttack.FindProperty("isThrow"), new GUIContent("投げ判定"));
            EditorGUILayout.PropertyField(serializedAttack.FindProperty("knockdownFrames"), new GUIContent("ダウン時間"));
            EditorGUI.indentLevel--;
        }
    }
}
