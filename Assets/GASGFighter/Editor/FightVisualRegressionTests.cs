using System.Collections;
using System.Linq;
using System.Reflection;
using GASG.Fighting;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace GASG.Fighting.Editor.Tests
{
    public sealed class FightVisualRegressionTests
    {
        private const string ScenePath = "Assets/GASGFighter/Scenes/LocalVersusPrototype.unity";
        private const BindingFlags PrivateInstance = BindingFlags.NonPublic | BindingFlags.Instance;

        [Test]
        public void PrototypeScene_OnlySelectedModelIsActiveAndRebindingDoesNotDuplicate()
        {
            // 開いている作業シーンを変更せず、検証対象だけ追加ロードして保存せず閉じる。
            Scene scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Additive);
            try
            {
                FighterController[] fighters = scene.GetRootGameObjects()
                    .SelectMany(root => root.GetComponentsInChildren<FighterController>()).ToArray();
                Assert.That(fighters.Length, Is.EqualTo(2));
                foreach (FighterController fighter in fighters)
                {
                    int originalChildren = fighter.transform.childCount;
                    AssertSingleVisual(fighter);
                    // 名前に依存せず、登録済みのモデルを再利用することも確認する。
                    Transform selected = (Transform)new SerializedObject(fighter).FindProperty("visualRoot").objectReferenceValue;
                    selected.name = "RenamedVisualForTest";
                    bool duplicateEnabled = false;
                    foreach (Transform child in fighter.transform)
                    {
                        if (child == selected || child.GetComponentInChildren<SkinnedMeshRenderer>(true) == null) continue;
                        child.gameObject.SetActive(true);
                        duplicateEnabled = true;
                    }
                    Assert.That(duplicateEnabled, Is.True, "元の重複Prefabを残して非表示にしていることを確認します。");
                    MethodInfo bind = typeof(FightCharacterVisualBinder).GetMethod("BindFighter", BindingFlags.Static | BindingFlags.NonPublic);
                    bind.Invoke(null, new object[] { fighter });
                    bind.Invoke(null, new object[] { fighter });
                    Assert.That(fighter.transform.childCount, Is.EqualTo(originalChildren));
                    AssertSingleVisual(fighter);
                }
            }
            finally { EditorSceneManager.CloseScene(scene, true); }
        }

        private static void AssertSingleVisual(FighterController fighter)
        {
            Transform selected = (Transform)new SerializedObject(fighter).FindProperty("visualRoot").objectReferenceValue;
            Assert.That(selected, Is.Not.Null);
            int activeModels = 0;
            foreach (Transform child in fighter.transform)
            {
                if (child.gameObject.activeInHierarchy && child.GetComponentInChildren<SkinnedMeshRenderer>() != null)
                {
                    activeModels++;
                    Assert.That(child, Is.SameAs(selected));
                }
            }
            Assert.That(activeModels, Is.EqualTo(1), fighter.name);
        }

        [UnityTest]
        public IEnumerator LightButton_PlaysLightPunchClipInPrototypeScene()
        {
            yield return new EnterPlayMode();
            EditorSceneManager.LoadSceneInPlayMode(ScenePath, new LoadSceneParameters(LoadSceneMode.Single));
            yield return null;
            var match = Object.FindFirstObjectByType<FightMatchManager>();
            Assert.That(match, Is.Not.Null);
            match.enabled = false;
            typeof(FightMatchManager).GetMethod("StartRoundControl", PrivateInstance).Invoke(match, null);
            foreach (FighterController fighter in new[] { match.Player1, match.Player2 })
            {
                AssertSingleVisual(fighter);
                typeof(FighterInputSource).GetField("lightBuffered", PrivateInstance).SetValue(fighter.InputSource, true);
                fighter.SimulateFrame();
                Assert.That(fighter.CurrentAttack, Is.SameAs(fighter.Config.AttackDatabase.LightAttack));
                Assert.That(fighter.CurrentAttack.Action.MotionId, Is.EqualTo("AS_p01_200"));
                Assert.That(fighter.CurrentAttack.AnimatorTrigger, Is.EqualTo("StandingLightPunch"));
                var animator = (Animator)new SerializedObject(fighter).FindProperty("animator").objectReferenceValue;
                animator.Update(FightFrameTiming.FrameDuration);
                Assert.That(animator.GetCurrentAnimatorStateInfo(0).IsName("StandingLightPunch"), Is.True);
                Assert.That(animator.GetCurrentAnimatorClipInfo(0)[0].clip,
                    Is.SameAs(AssetDatabase.LoadAssetAtPath<AnimationClip>(
                        "Assets/GASGFighter/Graphics/3D/Chara/animations/AS_p01_200.anim")));
            }
            yield return new ExitPlayMode();
        }
    }
}
