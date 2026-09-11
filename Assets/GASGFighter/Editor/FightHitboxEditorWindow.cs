using GASG.Fighting;
using UnityEditor;
using UnityEditorInternal;
using UnityEngine;

namespace GASG.Fighting.Editor
{
    public sealed class FightHitboxEditorWindow : EditorWindow
    {
        private FighterAttackDefinition attack;
        private FighterController previewFighter;
        private SerializedObject serializedAttack;
        private ReorderableList hitboxList;
        private int previewFrame;
        private Vector2 scrollPosition;

        [MenuItem("GASG/対戦プロトタイプ/03. 調整/ヒットボックスを編集")]
        public static void OpenWindow()
        {
            FightHitboxEditorWindow window = GetWindow<FightHitboxEditorWindow>("ヒットボックス編集");
            window.minSize = new Vector2(440f, 560f);
            window.TryUseCurrentSelection();
            window.FindPreviewFighter();
            window.Show();
        }

        public static void OpenWithAttack(FighterAttackDefinition targetAttack)
        {
            FightHitboxEditorWindow window = GetWindow<FightHitboxEditorWindow>("ヒットボックス編集");
            window.minSize = new Vector2(440f, 560f);
            window.SetAttack(targetAttack);
            window.FindPreviewFighter();
            window.Show();
        }

        private void OnEnable()
        {
            SceneView.duringSceneGui += OnSceneGUI;
            Selection.selectionChanged += OnSelectionChanged;
            TryUseCurrentSelection();
        }

        private void OnDisable()
        {
            SceneView.duringSceneGui -= OnSceneGUI;
            Selection.selectionChanged -= OnSelectionChanged;
        }

        private void OnGUI()
        {
            EditorGUILayout.Space(8f);
            EditorGUILayout.LabelField("モーション別ヒットボックス編集", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox(
                "Attack Motion Databaseで選択した攻撃を1モーションとして扱います。フレーム範囲の異なる判定を複数追加できます。赤=Hitbox、緑=Hurtbox、黄=編集中の判定です。",
                MessageType.Info);

            FighterAttackDefinition selectedAttack = (FighterAttackDefinition)EditorGUILayout.ObjectField(
                "攻撃モーション",
                attack,
                typeof(FighterAttackDefinition),
                false);
            if (selectedAttack != attack)
            {
                SetAttack(selectedAttack);
            }

            using (new EditorGUILayout.HorizontalScope())
            {
                previewFighter = (FighterController)EditorGUILayout.ObjectField(
                    "プレビューキャラクター",
                    previewFighter,
                    typeof(FighterController),
                    true);
                if (GUILayout.Button("シーンから検索", GUILayout.Width(100f)))
                {
                    FindPreviewFighter();
                }
            }

            if (attack == null || serializedAttack == null)
            {
                EditorGUILayout.HelpBox("Attack_*.assetを指定してください。", MessageType.Warning);
                return;
            }

            serializedAttack.Update();

            EditorGUILayout.Space(5f);
            EditorGUILayout.LabelField("モーションフレーム・後隙", EditorStyles.boldLabel);
            EditorGUI.indentLevel++;
            SerializedProperty startupFrames = serializedAttack.FindProperty("startupFrames");
            SerializedProperty activeFrames = serializedAttack.FindProperty("activeFrames");
            SerializedProperty recoveryFrames = serializedAttack.FindProperty("recoveryFrames");
            EditorGUILayout.PropertyField(startupFrames, new GUIContent("発生フレーム"));
            EditorGUILayout.PropertyField(activeFrames, new GUIContent("持続フレーム"));
            EditorGUILayout.PropertyField(recoveryFrames, new GUIContent("後隙フレーム"));
            int editedTotalFrames = Mathf.Max(1, startupFrames.intValue) +
                                    Mathf.Max(1, activeFrames.intValue) +
                                    Mathf.Max(1, recoveryFrames.intValue);
            EditorGUILayout.LabelField("攻撃全体フレーム", $"{editedTotalFrames} F");
            EditorGUILayout.LabelField(
                "通常行動へ戻れるタイミング",
                $"{editedTotalFrames} F目以降");
            EditorGUI.indentLevel--;

            EditorGUILayout.Space(5f);
            EditorGUILayout.LabelField("攻撃キャンセル", EditorStyles.boldLabel);
            SerializedProperty enableCancel = serializedAttack.FindProperty("enableAttackCancel");
            EditorGUILayout.PropertyField(enableCancel, new GUIContent("攻撃キャンセルを有効化"));
            using (new EditorGUI.DisabledScope(!enableCancel.boolValue))
            {
                EditorGUI.indentLevel++;
                SerializedProperty cancelStart = serializedAttack.FindProperty("cancelStartFrame");
                SerializedProperty cancelEnd = serializedAttack.FindProperty("cancelEndFrame");
                EditorGUILayout.PropertyField(cancelStart, new GUIContent("受付開始フレーム"));
                EditorGUILayout.PropertyField(cancelEnd, new GUIContent("受付終了フレーム"));
                EditorGUILayout.PropertyField(
                    serializedAttack.FindProperty("cancelOnHitOrBlockOnly"),
                    new GUIContent("ヒット／ガード時のみ"));
                EditorGUILayout.PropertyField(
                    serializedAttack.FindProperty("cancelTargets"),
                    new GUIContent("キャンセル可能な攻撃"));
                EditorGUI.indentLevel--;

                if (enableCancel.boolValue &&
                    (cancelStart.intValue < 0 || cancelEnd.intValue < cancelStart.intValue || cancelEnd.intValue >= editedTotalFrames))
                {
                    EditorGUILayout.HelpBox(
                        $"キャンセル範囲は0～{editedTotalFrames - 1}の中で、開始≦終了に設定してください。",
                        MessageType.Warning);
                }
            }

            EditorGUILayout.HelpBox(
                "後隙は、攻撃後に移動や次の通常行動ができない時間です。攻撃キャンセルを有効にすると、指定範囲だけ選択した別攻撃へ移行できます。",
                MessageType.None);

            int maximumFrame = Mathf.Max(0, editedTotalFrames - 1);
            previewFrame = EditorGUILayout.IntSlider("プレビューフレーム", previewFrame, 0, maximumFrame);
            EditorGUILayout.LabelField("現在の区間", GetFramePhaseLabel(attack, previewFrame));

            EditorGUILayout.Space(5f);
            EditorGUILayout.LabelField("ヒット時の効果", EditorStyles.boldLabel);
            EditorGUI.indentLevel++;
            EditorGUILayout.PropertyField(serializedAttack.FindProperty("damage"));
            EditorGUILayout.PropertyField(serializedAttack.FindProperty("hitStopFrames"), new GUIContent("ヒットストップ（フレーム）"));
            EditorGUILayout.PropertyField(serializedAttack.FindProperty("hitStunFrames"));
            EditorGUILayout.PropertyField(serializedAttack.FindProperty("blockStunFrames"));
            EditorGUILayout.PropertyField(serializedAttack.FindProperty("knockback"));
            EditorGUI.indentLevel--;

            EditorGUILayout.Space(5f);
            EditorGUILayout.LabelField("カメラシェイク", EditorStyles.boldLabel);
            EditorGUI.indentLevel++;
            EditorGUILayout.PropertyField(serializedAttack.FindProperty("cameraShakePositionAmplitude"));
            EditorGUILayout.PropertyField(serializedAttack.FindProperty("cameraShakeRotationAmplitude"));
            EditorGUILayout.PropertyField(serializedAttack.FindProperty("cameraShakeDurationFrames"));
            EditorGUILayout.PropertyField(serializedAttack.FindProperty("cameraShakeFrequency"));
            EditorGUI.indentLevel--;

            EditorGUILayout.Space(8f);
            scrollPosition = EditorGUILayout.BeginScrollView(scrollPosition);
            hitboxList?.DoLayoutList();
            EditorGUILayout.EndScrollView();

            if (serializedAttack.ApplyModifiedProperties())
            {
                EditorUtility.SetDirty(attack);
                SceneView.RepaintAll();
            }

            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("現在フレームを開始に設定"))
                {
                    SetSelectedFrameProperty("startFrame", previewFrame);
                }

                if (GUILayout.Button("現在フレームを終了に設定"))
                {
                    SetSelectedFrameProperty("endFrame", previewFrame);
                }
            }

            EditorGUILayout.HelpBox(
                "Play中はFighterControllerの「判定表示」で、GameビューにもHitbox/Hurtboxを表示できます。",
                MessageType.None);
        }

        private void SetAttack(FighterAttackDefinition newAttack)
        {
            attack = newAttack;
            previewFrame = attack != null ? attack.StartupFrames : 0;
            serializedAttack = attack != null ? new SerializedObject(attack) : null;
            BuildHitboxList();
            SceneView.RepaintAll();
            Repaint();
        }

        private void BuildHitboxList()
        {
            if (serializedAttack == null)
            {
                hitboxList = null;
                return;
            }

            SerializedProperty hitboxesProperty = serializedAttack.FindProperty("hitboxes");
            hitboxList = new ReorderableList(serializedAttack, hitboxesProperty, true, true, true, true)
            {
                elementHeight = (EditorGUIUtility.singleLineHeight + 3f) * 4f + 5f,
                drawHeaderCallback = rect => EditorGUI.LabelField(rect, "フレーム別Hitbox（複数追加可能）"),
                drawElementCallback = DrawHitboxElement,
                onAddCallback = AddHitbox
            };
        }

        private void DrawHitboxElement(Rect rect, int index, bool isActive, bool isFocused)
        {
            SerializedProperty element = hitboxList.serializedProperty.GetArrayElementAtIndex(index);
            float line = EditorGUIUtility.singleLineHeight;
            float spacing = 3f;
            rect.y += 2f;

            EditorGUI.PropertyField(new Rect(rect.x, rect.y, rect.width, line), element.FindPropertyRelative("label"));
            rect.y += line + spacing;

            float halfWidth = (rect.width - 6f) * 0.5f;
            EditorGUI.PropertyField(
                new Rect(rect.x, rect.y, halfWidth, line),
                element.FindPropertyRelative("startFrame"),
                new GUIContent("開始"));
            EditorGUI.PropertyField(
                new Rect(rect.x + halfWidth + 6f, rect.y, halfWidth, line),
                element.FindPropertyRelative("endFrame"),
                new GUIContent("終了"));
            rect.y += line + spacing;

            EditorGUI.PropertyField(new Rect(rect.x, rect.y, rect.width, line), element.FindPropertyRelative("center"));
            rect.y += line + spacing;
            EditorGUI.PropertyField(new Rect(rect.x, rect.y, rect.width, line), element.FindPropertyRelative("size"));
        }

        private void AddHitbox(ReorderableList list)
        {
            serializedAttack.Update();
            SerializedProperty array = list.serializedProperty;
            int newIndex = array.arraySize;
            array.InsertArrayElementAtIndex(newIndex);
            SerializedProperty element = array.GetArrayElementAtIndex(newIndex);
            element.FindPropertyRelative("label").stringValue = $"Hitbox {newIndex + 1}";
            element.FindPropertyRelative("startFrame").intValue = previewFrame;
            element.FindPropertyRelative("endFrame").intValue = previewFrame;
            element.FindPropertyRelative("center").vector3Value = new Vector3(1f, 1.1f, 0f);
            element.FindPropertyRelative("size").vector3Value = new Vector3(1f, 1f, 1f);
            serializedAttack.ApplyModifiedProperties();
            list.index = newIndex;
            EditorUtility.SetDirty(attack);
            SceneView.RepaintAll();
        }

        private void SetSelectedFrameProperty(string propertyName, int value)
        {
            if (hitboxList == null || hitboxList.index < 0 || hitboxList.index >= hitboxList.count)
            {
                return;
            }

            serializedAttack.Update();
            SerializedProperty element = hitboxList.serializedProperty.GetArrayElementAtIndex(hitboxList.index);
            SerializedProperty targetProperty = element.FindPropertyRelative(propertyName);
            targetProperty.intValue = value;

            int start = element.FindPropertyRelative("startFrame").intValue;
            int end = element.FindPropertyRelative("endFrame").intValue;
            if (end < start)
            {
                if (propertyName == "startFrame")
                {
                    element.FindPropertyRelative("endFrame").intValue = start;
                }
                else
                {
                    element.FindPropertyRelative("startFrame").intValue = end;
                }
            }

            serializedAttack.ApplyModifiedProperties();
            EditorUtility.SetDirty(attack);
            SceneView.RepaintAll();
        }

        private void OnSceneGUI(SceneView sceneView)
        {
            if (attack == null || serializedAttack == null || previewFighter == null)
            {
                return;
            }

            BoxCollider2D hurtbox = previewFighter.GetComponentInChildren<BoxCollider2D>();
            if (hurtbox != null)
            {
                Handles.color = new Color(0.15f, 1f, 0.25f, 0.9f);
                Handles.DrawWireCube(hurtbox.bounds.center, hurtbox.bounds.size);
            }

            serializedAttack.Update();
            SerializedProperty hitboxes = serializedAttack.FindProperty("hitboxes");
            float facing = previewFighter.FacingDirection;
            for (int i = 0; i < hitboxes.arraySize; i++)
            {
                SerializedProperty element = hitboxes.GetArrayElementAtIndex(i);
                int start = element.FindPropertyRelative("startFrame").intValue;
                int end = element.FindPropertyRelative("endFrame").intValue;
                if (previewFrame < start || previewFrame > end)
                {
                    continue;
                }

                Vector3 center = element.FindPropertyRelative("center").vector3Value;
                Vector3 size = element.FindPropertyRelative("size").vector3Value;
                Vector3 worldCenter = previewFighter.transform.position + new Vector3(center.x * facing, center.y, center.z);
                Handles.color = i == hitboxList.index ? Color.yellow : new Color(1f, 0.12f, 0.08f, 0.95f);
                Handles.DrawWireCube(worldCenter, size);
            }

            DrawSelectedHandles(facing);
        }

        private void DrawSelectedHandles(float facing)
        {
            if (hitboxList == null || hitboxList.index < 0 || hitboxList.index >= hitboxList.count)
            {
                return;
            }

            SerializedProperty element = hitboxList.serializedProperty.GetArrayElementAtIndex(hitboxList.index);
            Vector3 center = element.FindPropertyRelative("center").vector3Value;
            Vector3 size = element.FindPropertyRelative("size").vector3Value;
            Vector3 worldCenter = previewFighter.transform.position + new Vector3(center.x * facing, center.y, center.z);

            EditorGUI.BeginChangeCheck();
            Vector3 movedCenter = Handles.PositionHandle(worldCenter, Quaternion.identity);
            Vector3 scaledSize = Handles.ScaleHandle(size, movedCenter, Quaternion.identity, HandleUtility.GetHandleSize(movedCenter));
            if (!EditorGUI.EndChangeCheck())
            {
                return;
            }

            Undo.RecordObject(attack, "ヒットボックスをSceneビューで調整");
            Vector3 worldDelta = movedCenter - previewFighter.transform.position;
            element.FindPropertyRelative("center").vector3Value = new Vector3(worldDelta.x * facing, worldDelta.y, worldDelta.z);
            element.FindPropertyRelative("size").vector3Value = new Vector3(
                Mathf.Max(0.01f, Mathf.Abs(scaledSize.x)),
                Mathf.Max(0.01f, Mathf.Abs(scaledSize.y)),
                Mathf.Max(0.01f, Mathf.Abs(scaledSize.z)));
            serializedAttack.ApplyModifiedProperties();
            EditorUtility.SetDirty(attack);
            Repaint();
        }

        private void FindPreviewFighter()
        {
            previewFighter = Object.FindFirstObjectByType<FighterController>();
            SceneView.RepaintAll();
            Repaint();
        }

        private void TryUseCurrentSelection()
        {
            if (Selection.activeObject is FighterAttackDefinition selectedAttack)
            {
                SetAttack(selectedAttack);
            }
        }

        private void OnSelectionChanged()
        {
            TryUseCurrentSelection();
            Repaint();
        }

        private static string GetFramePhaseLabel(FighterAttackDefinition definition, int frame)
        {
            string phase;
            if (frame < definition.StartupFrames)
            {
                phase = "発生前（Startup）";
            }
            else if (frame < definition.StartupFrames + definition.ActiveFrames)
            {
                phase = "攻撃判定中（Active）";
            }
            else
            {
                phase = "硬直中（Recovery）";
            }

            if (definition.EnableAttackCancel && frame >= definition.CancelStartFrame && frame <= definition.CancelEndFrame)
            {
                phase += definition.CancelOnHitOrBlockOnly
                    ? " / ヒット・ガード時キャンセル可"
                    : " / キャンセル可";
            }

            return phase;
        }
    }

    [CustomEditor(typeof(FighterAttackDefinition))]
    public sealed class FighterAttackDefinitionInspector : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();
            EditorGUILayout.Space(8f);
            if (GUILayout.Button("専用ヒットボックスエディタを開く"))
            {
                FightHitboxEditorWindow.OpenWithAttack((FighterAttackDefinition)target);
            }
        }
    }
}
