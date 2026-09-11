using GASG.Fighting;
using UnityEditor;
using UnityEngine;

namespace GASG.Fighting.Editor
{
    public sealed class FightCameraTuningWindow : EditorWindow
    {
        private FightCameraController cameraController;
        private FightCameraSettings cameraSettings;
        private Vector2 scrollPosition;

        [MenuItem("GASG/対戦プロトタイプ/03. 調整/カメラを調整")]
        public static void OpenWindow()
        {
            FightCameraTuningWindow window = GetWindow<FightCameraTuningWindow>("格闘カメラ調整");
            window.minSize = new Vector2(390f, 520f);
            window.FindSceneCamera();
            window.Show();
        }

        private void OnEnable()
        {
            FindSceneCamera();
        }

        private void OnGUI()
        {
            EditorGUILayout.Space(8f);
            EditorGUILayout.LabelField("格闘カメラ調整", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox(
                "プリセットから構図を選び、必要な項目だけ調整してください。変更は設定アセットへ保存され、Play中も即時反映されます。すべての操作はUndoできます。",
                MessageType.Info);

            using (new EditorGUILayout.HorizontalScope())
            {
                FightCameraController selectedController = (FightCameraController)EditorGUILayout.ObjectField(
                    "対象カメラ",
                    cameraController,
                    typeof(FightCameraController),
                    true);
                if (selectedController != cameraController)
                {
                    cameraController = selectedController;
                    cameraSettings = cameraController != null ? cameraController.Settings : cameraSettings;
                }

                if (GUILayout.Button("シーンから検索", GUILayout.Width(100f)))
                {
                    FindSceneCamera();
                }
            }

            cameraSettings = (FightCameraSettings)EditorGUILayout.ObjectField(
                "設定アセット",
                cameraSettings,
                typeof(FightCameraSettings),
                false);

            if (cameraController == null)
            {
                EditorGUILayout.HelpBox("FightCameraControllerが見つかりません。対戦シーンを開いてから「シーンから検索」を押してください。", MessageType.Warning);
                return;
            }

            if (cameraSettings == null)
            {
                EditorGUILayout.HelpBox("カメラ設定アセットが未指定です。", MessageType.Error);
                return;
            }

            if (cameraController.Settings != cameraSettings)
            {
                if (GUILayout.Button("この設定を対象カメラへ割り当てる"))
                {
                    AssignSettingsToController();
                }
            }

            EditorGUILayout.Space(8f);
            EditorGUILayout.LabelField("構図プリセット", EditorStyles.boldLabel);
            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("寄り"))
                {
                    ApplyPreset(FightCameraPreset.Close);
                }

                if (GUILayout.Button("標準"))
                {
                    ApplyPreset(FightCameraPreset.Standard);
                }

                if (GUILayout.Button("引き"))
                {
                    ApplyPreset(FightCameraPreset.Wide);
                }
            }

            EditorGUILayout.Space(8f);
            scrollPosition = EditorGUILayout.BeginScrollView(scrollPosition);
            SerializedObject serializedSettings = new SerializedObject(cameraSettings);
            serializedSettings.Update();

            EditorGUI.BeginChangeCheck();
            DrawSection(serializedSettings, "レンズ", "fieldOfView");
            DrawSection(
                serializedSettings,
                "構図",
                "cameraHeight",
                "lookAtHeight",
                "horizontalOffset",
                "baseDistance",
                "maximumDistance",
                "distancePerSeparation",
                "airborneFollow");
            DrawSection(serializedSettings, "追従", "positionSmoothness", "rotationSmoothness");

            if (EditorGUI.EndChangeCheck())
            {
                serializedSettings.ApplyModifiedProperties();
                EditorUtility.SetDirty(cameraSettings);
                ApplyPreview();
            }
            else
            {
                serializedSettings.ApplyModifiedProperties();
            }

            EditorGUILayout.EndScrollView();

            EditorGUILayout.Space(6f);
            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("現在の構図をSceneへ反映"))
                {
                    ApplyPreview();
                }

                if (GUILayout.Button("設定アセットを選択"))
                {
                    Selection.activeObject = cameraSettings;
                    EditorGUIUtility.PingObject(cameraSettings);
                }
            }

            string modeMessage = EditorApplication.isPlaying
                ? "Play中：スライダー変更がGameビューへ即時反映されます。"
                : "Edit中：Sceneビューで構図を確認できます。Playすると同じ設定が使用されます。";
            EditorGUILayout.HelpBox(modeMessage, MessageType.None);
        }

        private static void DrawSection(SerializedObject serializedObject, string title, params string[] propertyNames)
        {
            EditorGUILayout.LabelField(title, EditorStyles.boldLabel);
            EditorGUI.indentLevel++;
            for (int i = 0; i < propertyNames.Length; i++)
            {
                SerializedProperty property = serializedObject.FindProperty(propertyNames[i]);
                if (property != null)
                {
                    EditorGUILayout.PropertyField(property);
                }
            }
            EditorGUI.indentLevel--;
            EditorGUILayout.Space(5f);
        }

        private void FindSceneCamera()
        {
            cameraController = Object.FindFirstObjectByType<FightCameraController>();
            cameraSettings = cameraController != null ? cameraController.Settings : cameraSettings;
            Repaint();
        }

        private void ApplyPreset(FightCameraPreset preset)
        {
            Undo.RecordObject(cameraSettings, "格闘カメラのプリセット変更");
            cameraSettings.EditorApplyPreset(preset);
            EditorUtility.SetDirty(cameraSettings);
            ApplyPreview();
        }

        private void AssignSettingsToController()
        {
            SerializedObject serializedController = new SerializedObject(cameraController);
            serializedController.Update();
            SerializedProperty settingsProperty = serializedController.FindProperty("settings");
            settingsProperty.objectReferenceValue = cameraSettings;
            serializedController.ApplyModifiedProperties();
            EditorUtility.SetDirty(cameraController);
            ApplyPreview();
        }

        private void ApplyPreview()
        {
            if (cameraController == null)
            {
                return;
            }

            cameraController.EditorApplyImmediatePreview();
            SceneView.RepaintAll();
            Repaint();
        }
    }

    [CustomEditor(typeof(FightCameraController))]
    public sealed class FightCameraControllerInspector : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();
            EditorGUILayout.Space(8f);
            if (GUILayout.Button("カメラ調整ウィンドウを開く"))
            {
                FightCameraTuningWindow.OpenWindow();
            }

            FightCameraController controller = (FightCameraController)target;
            if (controller.Settings == null)
            {
                EditorGUILayout.HelpBox("Camera Settingsが未設定です。カメラは追従しません。", MessageType.Error);
            }
        }
    }
}
