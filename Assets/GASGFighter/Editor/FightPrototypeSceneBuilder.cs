using System.Collections.Generic;
using System.IO;
using GASG.Fighting;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace GASG.Fighting.Editor
{
    public static class FightPrototypeSceneBuilder
    {
        private const string RootFolder = "Assets/GASGFighter";
        private const string DataFolder = RootFolder + "/Data";
        private const string MaterialFolder = RootFolder + "/Materials";
        private const string SceneFolder = RootFolder + "/Scenes";
        private const string ScenePath = SceneFolder + "/LocalVersusPrototype.unity";

        [MenuItem("GASG/Fighting Game/Create Local Versus Prototype Scene")]
        public static void CreatePrototypeScene()
        {
            // 未保存の作業を破棄しないよう、シーン切り替え前に必ず確認する。
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
            {
                Debug.Log("[GASG Fighter] 未保存シーンがあるため、シーン生成をキャンセルしました。");
                return;
            }

            if (AssetDatabase.LoadAssetAtPath<SceneAsset>(ScenePath) != null &&
                !EditorUtility.DisplayDialog(
                    "既存シーンの確認",
                    "LocalVersusPrototype.unity は既に存在します。シーンだけを再生成しますか？\n\nDataとMaterialアセットは上書きしません。",
                    "再生成",
                    "キャンセル"))
            {
                Debug.Log("[GASG Fighter] シーン生成をキャンセルしました。");
                return;
            }

            EnsureFolder(RootFolder);
            EnsureFolder(DataFolder);
            EnsureFolder(MaterialFolder);
            EnsureFolder(SceneFolder);

            FighterAttackDefinition lightAttack = GetOrCreateAttack(
                DataFolder + "/Attack_Light.asset",
                "Light Attack",
                "attack_001",
                5, 3, 11,
                60, 14, 9, 5, 2.5f,
                GuardHeight.Mid,
                new Vector3(0.85f, 1.15f, 0f),
                new Vector3(1.05f, 0.75f, 0.9f),
                false, 0);

            FighterAttackDefinition mediumAttack = GetOrCreateAttack(
                DataFolder + "/Attack_Medium.asset",
                "Medium Attack",
                "MediumAttack",
                7, 3, 14,
                85, 18, 11, 6, 3.2f,
                GuardHeight.Mid,
                new Vector3(0.95f, 1.2f, 0f),
                new Vector3(1.2f, 0.85f, 0.95f),
                false, 0);

            FighterAttackDefinition heavyAttack = GetOrCreateAttack(
                DataFolder + "/Attack_Heavy.asset",
                "Heavy Attack",
                "HeavyAttack",
                10, 4, 19,
                110, 22, 14, 8, 4.5f,
                GuardHeight.Mid,
                new Vector3(1.05f, 1.25f, 0f),
                new Vector3(1.35f, 0.95f, 1.0f),
                false, 0);

            FighterAttackDefinition specialAttack = GetOrCreateAttack(
                DataFolder + "/Attack_Special.asset",
                "Special Attack",
                "SpecialAttack",
                14, 6, 26,
                170, 28, 18, 10, 7f,
                GuardHeight.Mid,
                new Vector3(1.25f, 1.15f, 0f),
                new Vector3(1.8f, 1.2f, 1.1f),
                false, 55);

            FighterAttackDefinition throwAttack = GetOrCreateAttack(
                DataFolder + "/Attack_Throw.asset",
                "Throw",
                "Throw",
                4, 2, 22,
                130, 1, 1, 8, 6.5f,
                GuardHeight.Unblockable,
                new Vector3(0.65f, 1.0f, 0f),
                new Vector3(0.7f, 1.5f, 0.9f),
                true, 55);

            FighterAttackDatabase attackDatabase = FightAttackDatabaseMigration.CreateOrUpdateDatabase();
            FighterConfig fighterConfig = GetOrCreateConfig(
                lightAttack,
                mediumAttack,
                heavyAttack,
                specialAttack,
                throwAttack);
            fighterConfig.EditorSetAttackDatabase(attackDatabase);
            EditorUtility.SetDirty(fighterConfig);
            FightCameraSettings cameraSettings = GetOrCreateCameraSettings();

            Material player1Material = GetOrCreateMaterial(
                MaterialFolder + "/MAT_Player1.mat",
                new Color(0.06f, 0.42f, 1f));
            Material player2Material = GetOrCreateMaterial(
                MaterialFolder + "/MAT_Player2.mat",
                new Color(1f, 0.12f, 0.08f));
            Material stageMaterial = GetOrCreateMaterial(
                MaterialFolder + "/MAT_Stage.mat",
                new Color(0.12f, 0.14f, 0.18f));
            Material backdropMaterial = GetOrCreateMaterial(
                MaterialFolder + "/MAT_Backdrop.mat",
                new Color(0.025f, 0.035f, 0.065f));

            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            scene.name = "LocalVersusPrototype";

            CreateLighting();
            CreateStage(stageMaterial, backdropMaterial);

            FighterController player1 = CreateFighter(
                "Player_1",
                1,
                0,
                new Vector3(-2.5f, 0f, 0f),
                fighterConfig,
                player1Material);
            FighterController player2 = CreateFighter(
                "Player_2",
                2,
                1,
                new Vector3(2.5f, 0f, 0f),
                fighterConfig,
                player2Material);

            GameObject systems = new GameObject("Fight_Systems");
            FightMatchManager matchManager = systems.AddComponent<FightMatchManager>();
            matchManager.EditorConfigure(player1, player2);
            FightHud hud = systems.AddComponent<FightHud>();
            hud.EditorConfigure(matchManager);
            FightOptionsMenu optionsMenu = systems.AddComponent<FightOptionsMenu>();
            optionsMenu.EditorConfigure(matchManager);

            CreateCamera(player1.transform, player2.transform, cameraSettings);

            EditorSceneManager.MarkSceneDirty(scene);
            bool saved = EditorSceneManager.SaveScene(scene, ScenePath);
            if (!saved)
            {
                Debug.LogError($"[GASG Fighter] シーンを保存できませんでした: {ScenePath}");
                return;
            }

            AddSceneToBuildSettings();
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Selection.activeObject = AssetDatabase.LoadAssetAtPath<SceneAsset>(ScenePath);
            EditorGUIUtility.PingObject(Selection.activeObject);
            Debug.Log($"[GASG Fighter][成功] ローカル対戦シーンを作成しました: {ScenePath}");
        }

        [MenuItem("GASG/Fighting Game/Validate Local Versus Prototype Scene")]
        public static void ValidatePrototypeScene()
        {
            SceneAsset sceneAsset = AssetDatabase.LoadAssetAtPath<SceneAsset>(ScenePath);
            if (sceneAsset == null)
            {
                throw new System.InvalidOperationException($"対戦シーンが見つかりません: {ScenePath}");
            }

            Scene scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            if (!scene.IsValid() || !scene.isLoaded)
            {
                throw new System.InvalidOperationException("対戦シーンを読み込めませんでした。");
            }

            FighterController[] fighters = Object.FindObjectsByType<FighterController>(FindObjectsSortMode.None);
            FightMatchManager matchManager = Object.FindFirstObjectByType<FightMatchManager>();
            FightCameraController fightCamera = Object.FindFirstObjectByType<FightCameraController>();
            FightHud hud = Object.FindFirstObjectByType<FightHud>();
            FightOptionsMenu optionsMenu = Object.FindFirstObjectByType<FightOptionsMenu>();

            if (fighters.Length != 2)
            {
                throw new System.InvalidOperationException($"FighterControllerは2体必要です。現在: {fighters.Length}");
            }

            if (matchManager == null || matchManager.Player1 == null || matchManager.Player2 == null)
            {
                throw new System.InvalidOperationException("FightMatchManagerのPlayer参照が不足しています。");
            }

            if (fightCamera == null || fightCamera.Settings == null || hud == null || optionsMenu == null)
            {
                throw new System.InvalidOperationException("Camera Controller、Camera Settings、HUDまたはOptions Menuが不足しています。");
            }

            for (int i = 0; i < fighters.Length; i++)
            {
                FighterController fighter = fighters[i];
                if (fighter.Config == null ||
                    fighter.Config.LightAttack == null ||
                    fighter.Config.MediumAttack == null ||
                    fighter.Config.HeavyAttack == null ||
                    fighter.Config.SpecialAttack == null ||
                    fighter.Config.ThrowAttack == null)
                {
                    throw new System.InvalidOperationException($"{fighter.name}のConfigまたは攻撃データ参照が不足しています。");
                }

                if (fighter.InputSource == null || fighter.GetComponentInChildren<BoxCollider2D>() == null)
                {
                    throw new System.InvalidOperationException($"{fighter.name}のInputまたはHurtboxが不足しています。");
                }
            }

            Debug.Log("[GASG Fighter][検証成功] Player 2体、入力、攻撃データ、投げ、MatchManager、HUD、Camera、Hurtboxの参照を確認しました。");
        }

        private static FighterAttackDefinition GetOrCreateAttack(
            string path,
            string displayName,
            string animatorTrigger,
            int startup,
            int active,
            int recovery,
            int damage,
            int hitStun,
            int blockStun,
            int hitStop,
            float knockback,
            GuardHeight guardHeight,
            Vector3 hitboxCenter,
            Vector3 hitboxSize,
            bool isThrow,
            int knockdownFrames)
        {
            FighterAttackDefinition existing = AssetDatabase.LoadAssetAtPath<FighterAttackDefinition>(path);
            if (existing != null)
            {
                Debug.Log($"[GASG Fighter][スキップ] 既存の攻撃データを保持します: {path}");
                return existing;
            }

            FighterAttackDefinition created = ScriptableObject.CreateInstance<FighterAttackDefinition>();
            created.EditorConfigure(
                displayName,
                animatorTrigger,
                startup,
                active,
                recovery,
                damage,
                hitStun,
                blockStun,
                hitStop,
                knockback,
                guardHeight,
                hitboxCenter,
                hitboxSize,
                isThrow,
                knockdownFrames);
            AssetDatabase.CreateAsset(created, path);
            Debug.Log($"[GASG Fighter][成功] 攻撃データを作成しました: {path}");
            return created;
        }

        private static FighterConfig GetOrCreateConfig(
            FighterAttackDefinition lightAttack,
            FighterAttackDefinition mediumAttack,
            FighterAttackDefinition heavyAttack,
            FighterAttackDefinition specialAttack,
            FighterAttackDefinition throwAttack)
        {
            string path = DataFolder + "/FighterConfig_Prototype.asset";
            FighterConfig existing = AssetDatabase.LoadAssetAtPath<FighterConfig>(path);
            if (existing != null)
            {
                Debug.Log($"[GASG Fighter][スキップ] 既存のキャラクター設定を保持します: {path}");
                return existing;
            }

            FighterConfig created = ScriptableObject.CreateInstance<FighterConfig>();
            created.EditorConfigure(
                "Prototype Fighter",
                1000,
                4.5f,
                3.5f,
                8.5f,
                24f,
                8f,
                0.55f,
                12,
                10,
                2.2f,
                1.8f,
                8,
                lightAttack,
                mediumAttack,
                heavyAttack,
                specialAttack,
                throwAttack);
            AssetDatabase.CreateAsset(created, path);
            Debug.Log($"[GASG Fighter][成功] キャラクター設定を作成しました: {path}");
            return created;
        }

        private static Material GetOrCreateMaterial(string path, Color color)
        {
            Material existing = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (existing != null)
            {
                return existing;
            }

            Shader shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null)
            {
                shader = Shader.Find("Standard");
            }

            if (shader == null)
            {
                Debug.LogError("[GASG Fighter] 使用可能なLit Shaderが見つかりません。");
                return null;
            }

            Material material = new Material(shader) { color = color };
            if (material.HasProperty("_BaseColor"))
            {
                material.SetColor("_BaseColor", color);
            }

            AssetDatabase.CreateAsset(material, path);
            return material;
        }

        private static FightCameraSettings GetOrCreateCameraSettings()
        {
            string path = DataFolder + "/FightCameraSettings_Prototype.asset";
            FightCameraSettings existing = AssetDatabase.LoadAssetAtPath<FightCameraSettings>(path);
            if (existing != null)
            {
                Debug.Log($"[GASG Fighter][スキップ] 既存のカメラ設定を保持します: {path}");
                return existing;
            }

            FightCameraSettings created = ScriptableObject.CreateInstance<FightCameraSettings>();
            created.EditorApplyPreset(FightCameraPreset.Standard);
            AssetDatabase.CreateAsset(created, path);
            Debug.Log($"[GASG Fighter][成功] カメラ設定を作成しました: {path}");
            return created;
        }

        private static FighterController CreateFighter(
            string objectName,
            int playerIndex,
            int gamepadIndex,
            Vector3 position,
            FighterConfig config,
            Material material)
        {
            GameObject root = new GameObject(objectName);
            root.transform.position = position;

            FighterInputSource inputSource = root.AddComponent<FighterInputSource>();
            inputSource.EditorConfigure(playerIndex, gamepadIndex);

            Rigidbody rigidbody = root.AddComponent<Rigidbody>();
            rigidbody.isKinematic = true;
            rigidbody.useGravity = false;
            rigidbody.interpolation = RigidbodyInterpolation.Interpolate;

            GameObject hurtbox = new GameObject("Hurtbox");
            hurtbox.transform.SetParent(root.transform, false);
            BoxCollider2D hurtboxCollider = hurtbox.AddComponent<BoxCollider2D>();
            hurtboxCollider.isTrigger = true;
            hurtboxCollider.offset = new Vector3(0f, 1f, 0f);
            hurtboxCollider.size = new Vector2(0.9f, 2f);

            Transform visualRoot;
            Animator animator;
            if (!FightCharacterVisualBinder.TryInstantiatePlayer001Visual(root.transform, out visualRoot, out animator))
            {
                GameObject visual = GameObject.CreatePrimitive(PrimitiveType.Capsule);
                visual.name = "Visual_Placeholder_REPLACE_ME";
                visual.transform.SetParent(root.transform, false);
                visual.transform.localPosition = new Vector3(0f, 1f, 0f);
                visual.transform.localScale = new Vector3(0.8f, 1f, 0.65f);
                Collider primitiveCollider = visual.GetComponent<Collider>();
                if (primitiveCollider != null)
                {
                    Object.DestroyImmediate(primitiveCollider);
                }

                Renderer renderer = visual.GetComponent<Renderer>();
                if (renderer != null && material != null)
                {
                    renderer.sharedMaterial = material;
                }

                CreateFacingMarker(visual.transform, material);
                visualRoot = visual.transform;
                animator = null;
            }

            FighterController controller = root.AddComponent<FighterController>();
            controller.EditorConfigure(playerIndex, config, inputSource, visualRoot, animator);
            return controller;
        }

        private static void CreateFacingMarker(Transform parent, Material material)
        {
            GameObject marker = GameObject.CreatePrimitive(PrimitiveType.Cube);
            marker.name = "Facing_Marker";
            marker.transform.SetParent(parent, false);
            marker.transform.localPosition = new Vector3(0f, 0.25f, 0.62f);
            marker.transform.localScale = new Vector3(0.22f, 0.22f, 0.22f);
            Collider markerCollider = marker.GetComponent<Collider>();
            if (markerCollider != null)
            {
                Object.DestroyImmediate(markerCollider);
            }

            Renderer renderer = marker.GetComponent<Renderer>();
            if (renderer != null && material != null)
            {
                renderer.sharedMaterial = material;
            }
        }

        private static void CreateStage(Material stageMaterial, Material backdropMaterial)
        {
            GameObject stageRoot = new GameObject("Stage_Prototype");

            GameObject floor = GameObject.CreatePrimitive(PrimitiveType.Cube);
            floor.name = "Floor";
            floor.transform.SetParent(stageRoot.transform, false);
            floor.transform.position = new Vector3(0f, -0.3f, 0f);
            floor.transform.localScale = new Vector3(18f, 0.6f, 5f);
            AssignMaterial(floor, stageMaterial);

            GameObject backdrop = GameObject.CreatePrimitive(PrimitiveType.Cube);
            backdrop.name = "Backdrop";
            backdrop.transform.SetParent(stageRoot.transform, false);
            backdrop.transform.position = new Vector3(0f, 3.2f, 2.7f);
            backdrop.transform.localScale = new Vector3(20f, 7f, 0.3f);
            AssignMaterial(backdrop, backdropMaterial);

            for (int i = -8; i <= 8; i += 2)
            {
                GameObject line = GameObject.CreatePrimitive(PrimitiveType.Cube);
                line.name = $"FloorMarker_{i:+00;-00;00}";
                line.transform.SetParent(stageRoot.transform, false);
                line.transform.position = new Vector3(i, 0.015f, 0f);
                line.transform.localScale = new Vector3(0.035f, 0.02f, 4.8f);
                AssignMaterial(line, backdropMaterial);
                Collider collider = line.GetComponent<Collider>();
                if (collider != null)
                {
                    Object.DestroyImmediate(collider);
                }
            }
        }

        private static void CreateLighting()
        {
            GameObject lightObject = new GameObject("Directional Light");
            Light light = lightObject.AddComponent<Light>();
            light.type = LightType.Directional;
            light.intensity = 1.25f;
            light.color = new Color(0.92f, 0.95f, 1f);
            lightObject.transform.rotation = Quaternion.Euler(45f, -35f, 0f);

            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = new Color(0.28f, 0.32f, 0.42f);
            RenderSettings.ambientEquatorColor = new Color(0.12f, 0.14f, 0.2f);
            RenderSettings.ambientGroundColor = new Color(0.04f, 0.04f, 0.055f);
        }

        private static void CreateCamera(
            Transform player1,
            Transform player2,
            FightCameraSettings cameraSettings)
        {
            GameObject cameraObject = new GameObject("Fight Camera");
            Camera camera = cameraObject.AddComponent<Camera>();
            cameraObject.tag = "MainCamera";
            cameraObject.transform.position = new Vector3(0f, 3.8f, -11f);
            cameraObject.transform.rotation = Quaternion.Euler(9f, 0f, 0f);
            camera.fieldOfView = 42f;
            camera.nearClipPlane = 0.1f;
            camera.farClipPlane = 100f;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.015f, 0.02f, 0.04f);

            FightCameraController cameraController = cameraObject.AddComponent<FightCameraController>();
            cameraController.EditorConfigure(player1, player2, cameraSettings);
            cameraController.EditorApplyImmediatePreview();
            cameraObject.AddComponent<AudioListener>();
        }

        private static void AssignMaterial(GameObject target, Material material)
        {
            Renderer renderer = target.GetComponent<Renderer>();
            if (renderer != null && material != null)
            {
                renderer.sharedMaterial = material;
            }
        }

        private static void AddSceneToBuildSettings()
        {
            List<EditorBuildSettingsScene> scenes = new List<EditorBuildSettingsScene>(EditorBuildSettings.scenes);
            for (int i = 0; i < scenes.Count; i++)
            {
                if (scenes[i].path == ScenePath)
                {
                    return;
                }
            }

            scenes.Add(new EditorBuildSettingsScene(ScenePath, true));
            EditorBuildSettings.scenes = scenes.ToArray();
        }

        private static void EnsureFolder(string folderPath)
        {
            if (AssetDatabase.IsValidFolder(folderPath))
            {
                return;
            }

            string parent = Path.GetDirectoryName(folderPath)?.Replace('\\', '/');
            string folderName = Path.GetFileName(folderPath);
            if (string.IsNullOrEmpty(parent) || string.IsNullOrEmpty(folderName))
            {
                throw new IOException($"フォルダーを作成できません: {folderPath}");
            }

            EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, folderName);
        }
    }
}
