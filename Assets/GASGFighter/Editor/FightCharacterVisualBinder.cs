using System;
using GASG.Fighting;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace GASG.Fighting.Editor
{
    public static class FightCharacterVisualBinder
    {
        private const string ScenePath = "Assets/GASGFighter/Scenes/LocalVersusPrototype.unity";
        private const string ModelPath = "Assets/GASGFighter/Graphics/3D/Chara/fbx/Player_001.fbx";
        private const string ControllerPath = "Assets/GASGFighter/Graphics/3D/Chara/animations/Player_001.controller";
        private const string AttackDatabasePath = "Assets/GASGFighter/Data/AttackMotionDatabase_Prototype.asset";

        [MenuItem("GASG/対戦プロトタイプ/01. 初期セットアップ/Player_001モデルを対戦シーンへ適用")]
        public static void ApplyToPrototypeScene()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
            {
                Debug.Log("[GASG Fighter] 未保存シーンがあるため、モデル適用をキャンセルしました。");
                return;
            }

            ConfigureAnimatorController();
            Scene scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            if (!scene.IsValid() || !scene.isLoaded)
            {
                throw new InvalidOperationException($"対戦シーンを開けません: {ScenePath}");
            }

            FighterController[] fighters = UnityEngine.Object.FindObjectsByType<FighterController>(FindObjectsSortMode.None);
            if (fighters.Length != 2)
            {
                throw new InvalidOperationException($"FighterControllerは2体必要です。現在: {fighters.Length}");
            }

            Array.Sort(fighters, (a, b) => a.PlayerIndex.CompareTo(b.PlayerIndex));
            for (int i = 0; i < fighters.Length; i++)
            {
                BindFighter(fighters[i]);
            }

            EditorSceneManager.MarkSceneDirty(scene);
            if (!EditorSceneManager.SaveScene(scene, ScenePath))
            {
                throw new InvalidOperationException($"対戦シーンを保存できません: {ScenePath}");
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("[GASG Fighter][成功] Player_001とStandingLightPunchをPlayer 1 / 2へ適用しました。");
        }

        public static bool TryInstantiatePlayer001Visual(
            Transform parent,
            out Transform visualRoot,
            out Animator animator)
        {
            visualRoot = null;
            animator = null;
            GameObject modelPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(ModelPath);
            AnimatorController controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(ControllerPath);
            if (modelPrefab == null || controller == null)
            {
                return false;
            }

            if (parent == null) return false;
            // 名前変更やPrefab Variantでも同じ元モデルなら再利用し、二重生成しない。
            foreach (Transform child in parent)
            {
                if (!IsPlayer001Visual(child) || !child.gameObject.activeSelf) continue;
                visualRoot = child;
                animator = child.GetComponent<Animator>();
                if (animator == null) animator = Undo.AddComponent<Animator>(child.gameObject);
                ConfigureVisualAnimator(animator, controller);
                return true;
            }

            ConfigureAnimatorController();
            GameObject visual = PrefabUtility.InstantiatePrefab(modelPrefab, parent) as GameObject;
            if (visual == null)
            {
                return false;
            }

            visual.name = "Visual_Player_001";
            Undo.RegisterCreatedObjectUndo(visual, "Create Fighter Visual");
            visual.transform.localPosition = Vector3.zero;
            visual.transform.localRotation = Quaternion.identity;
            visual.transform.localScale = Vector3.one;

            animator = visual.GetComponent<Animator>();
            if (animator == null)
            {
                animator = visual.AddComponent<Animator>();
            }

            animator.runtimeAnimatorController = controller;
            animator.applyRootMotion = false;
            animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
            visualRoot = visual.transform;
            return true;
        }

        private static void BindFighter(FighterController fighter)
        {
            if (fighter == null) throw new ArgumentNullException(nameof(fighter));
            SerializedObject serializedFighter = new SerializedObject(fighter);
            Transform existingVisual = serializedFighter.FindProperty("visualRoot").objectReferenceValue as Transform;
            if (existingVisual == null || existingVisual == fighter.transform || !existingVisual.IsChildOf(fighter.transform))
                existingVisual = fighter.transform.Find("Visual_Player_001");
            Animator animator;
            Transform visualRoot;

            if (existingVisual != null)
            {
                visualRoot = existingVisual;
                animator = existingVisual.GetComponent<Animator>();
                if (animator == null)
                {
                    animator = Undo.AddComponent<Animator>(existingVisual.gameObject);
                }

                ConfigureVisualAnimator(animator, AssetDatabase.LoadAssetAtPath<AnimatorController>(ControllerPath));
            }
            else if (!TryInstantiatePlayer001Visual(fighter.transform, out visualRoot, out animator))
            {
                throw new InvalidOperationException("Player_001モデルまたはAnimator Controllerを読み込めませんでした。");
            }

            Undo.RecordObject(visualRoot.gameObject, "Enable Fighter Visual");
            visualRoot.gameObject.SetActive(true);
            // 選択済みモデル以外の、同じ元FBX由来の表示モデルだけを無効化する。
            // 武器・VFX・Hurtboxなど他の子オブジェクトは対象にしない。
            foreach (Transform child in fighter.transform)
            {
                if (child == visualRoot || visualRoot.IsChildOf(child)) continue;
                if (!IsPlayer001Visual(child) && child.name != "Visual_Placeholder_REPLACE_ME") continue;
                if (!child.gameObject.activeSelf) continue;
                Undo.RecordObject(child.gameObject, "Disable Duplicate Fighter Visual");
                child.gameObject.SetActive(false);
                PrefabUtility.RecordPrefabInstancePropertyModifications(child.gameObject);
                Debug.Log($"[GASG Fighter][成功] 重複表示を無効化: {fighter.name}/{child.name}", child);
            }

            serializedFighter.Update();
            serializedFighter.FindProperty("visualRoot").objectReferenceValue = visualRoot;
            serializedFighter.FindProperty("animator").objectReferenceValue = animator;
            serializedFighter.ApplyModifiedProperties();
            EditorUtility.SetDirty(fighter);
        }

        private static bool IsPlayer001Visual(Transform candidate)
        {
            GameObject source = PrefabUtility.GetCorrespondingObjectFromOriginalSource(candidate.gameObject);
            return source != null && AssetDatabase.GetAssetPath(source) == ModelPath &&
                   candidate.GetComponentInChildren<SkinnedMeshRenderer>(true) != null;
        }

        private static void ConfigureVisualAnimator(Animator animator, AnimatorController controller)
        {
            if (controller == null) throw new InvalidOperationException("Animator Controllerが見つかりません。");
            Undo.RecordObject(animator, "Configure Fighter Animator");
            animator.runtimeAnimatorController = controller;
            animator.applyRootMotion = false;
            animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
            PrefabUtility.RecordPrefabInstancePropertyModifications(animator);
        }

        private static void ConfigureAnimatorController()
        {
            AnimatorController controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(ControllerPath);
            FighterAttackDatabase database = AssetDatabase.LoadAssetAtPath<FighterAttackDatabase>(AttackDatabasePath);
            if (controller == null || database == null)
            {
                throw new InvalidOperationException("Animator ControllerまたはAttack Databaseが見つかりません。");
            }

            FightAttackAnimatorSynchronizer.Sync(database);
        }
    }
}
