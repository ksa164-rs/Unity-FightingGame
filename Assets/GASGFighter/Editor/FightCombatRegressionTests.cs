using System.Collections;
using System.Reflection;
using GASG.Fighting;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace GASG.Fighting.Editor.Tests
{
    public sealed class FightCombatRegressionTests
    {
        private const BindingFlags PrivateInstance = BindingFlags.Instance | BindingFlags.NonPublic;

        private static void SetField(object target, string name, object value)
        {
            target.GetType().GetField(name, PrivateInstance).SetValue(target, value);
        }

        private static object Call(object target, string name, params object[] args)
        {
            return target.GetType().GetMethod(name, PrivateInstance).Invoke(target, args);
        }

        // 実際の状態遷移へ攻撃を適用し、連続ガードと上下段の切替を確認する。
        [TestCase(GuardHeight.Mid, 0f, true)]
        [TestCase(GuardHeight.Low, -1f, true)]
        [TestCase(GuardHeight.Low, 0f, false)]
        [TestCase(GuardHeight.Overhead, 0f, true)]
        [TestCase(GuardHeight.Overhead, -1f, false)]
        [TestCase(GuardHeight.Unblockable, -1f, false)]
        public void BlockStun_AllowsContinuedGuardWithCurrentHeight(GuardHeight height, float moveY, bool expectedBlock)
        {
            var config = ScriptableObject.CreateInstance<FighterConfig>();
            var attack = ScriptableObject.CreateInstance<FighterAttackDefinition>();
            var firstObject = new GameObject("GuardTest_Attacker");
            var secondObject = new GameObject("GuardTest_Defender");
            try
            {
                var first = firstObject.AddComponent<FighterController>();
                var second = secondObject.AddComponent<FighterController>();
                first.EditorConfigure(1, config, null, null, null);
                second.EditorConfigure(2, config, null, null, null);
                first.Initialize(null, second);
                second.Initialize(null, first);
                first.ResetForRound(new Vector3(-2f, 0f, 0f));
                second.ResetForRound(new Vector3(2f, 0f, 0f));
                second.SetRoundControl(true);
                SetField(second, "lastInput", new FighterInputFrame { moveX = 1f });
                Assert.That(second.TryReceiveAttack(first, attack), Is.True);
                Assert.That(second.State, Is.EqualTo(FighterState.BlockStun));
                SetField(attack, "guardHeight", height);
                SetField(second, "lastInput", new FighterInputFrame { moveX = 1f, moveY = moveY });
                Assert.That(second.TryReceiveAttack(first, attack), Is.True);
                Assert.That(second.CurrentHealth, Is.EqualTo(expectedBlock ? config.MaxHealth : config.MaxHealth - attack.Damage));
                Assert.That(second.State, Is.EqualTo(expectedBlock ? FighterState.BlockStun : FighterState.HitStun));
            }
            finally
            {
                Object.DestroyImmediate(firstObject);
                Object.DestroyImmediate(secondObject);
                Object.DestroyImmediate(config);
                Object.DestroyImmediate(attack);
            }
        }

        [Test]
        public void Validator_DetectsMismatchedHitboxAndMissingProjectilePrefab()
        {
            var database = ScriptableObject.CreateInstance<FighterAttackDatabase>();
            var attack = ScriptableObject.CreateInstance<FighterAttackDefinition>();
            try
            {
                database.EditorConfigure(attack, null, null, null, null);
                SetField(attack, "hitboxes", new System.Collections.Generic.List<AttackHitboxFrame>
                {
                    new AttackHitboxFrame("Invalid", 1, 200, Vector3.zero, Vector3.one)
                });
                string report = string.Join("\n", FightCombatDataValidator.FindIssues(database));
                StringAssert.Contains("範囲外", report);
                StringAssert.Contains("一致しません", report);
                SetField(attack, "isProjectile", true);
                SetField(attack, "projectileSpawnFrame", attack.TotalFrames);
                report = string.Join("\n", FightCombatDataValidator.FindIssues(database));
                StringAssert.Contains("Prefabが未設定", report);
                StringAssert.Contains("攻撃終了以降", report);
            }
            finally
            {
                Object.DestroyImmediate(database);
                Object.DestroyImmediate(attack);
            }
        }

        [UnityTest]
        public IEnumerator Projectiles_UseMatchClockPauseAndOriginalAttackIdentity()
        {
            yield return new EnterPlayMode();
            var config = ScriptableObject.CreateInstance<FighterConfig>();
            var shot = ScriptableObject.CreateInstance<FighterAttackDefinition>();
            var normal = ScriptableObject.CreateInstance<FighterAttackDefinition>();
            var matchObject = new GameObject("ClockTest_Match");
            var firstObject = new GameObject("ClockTest_Attacker");
            var secondObject = new GameObject("ClockTest_Defender");
            var projectileObject = new GameObject("ClockTest_Projectile");
            var prefab = new GameObject("ClockTest_Prefab");
            prefab.SetActive(false);
            try
            {
                var match = matchObject.AddComponent<FightMatchManager>();
                // 自動Updateを止め、同じ実メソッドを決まった回数だけ呼ぶ。
                match.enabled = false;
                var first = firstObject.AddComponent<FighterController>();
                var second = secondObject.AddComponent<FighterController>();
                first.EditorConfigure(1, config, firstObject.AddComponent<FighterInputSource>(), null, null);
                second.EditorConfigure(2, config, secondObject.AddComponent<FighterInputSource>(), null, null);
                first.Initialize(match, second);
                second.Initialize(match, first);
                match.EditorConfigure(first, second);
                Call(match, "BeginMatch");
                Call(match, "StartRoundControl");
                SetField(shot, "isProjectile", true);
                SetField(shot, "projectilePrefab", prefab);
                var projectile = projectileObject.AddComponent<FighterProjectile>();
                projectileObject.transform.position = new Vector3(-4f, 2f, 0f);
                projectile.Initialize(first, shot, Vector3.right);
                Call(match, "SimulateMatchFrame");
                float expectedX = -4f + shot.ProjectileSpeed / 60f;
                Assert.That(projectile.transform.position.x, Is.EqualTo(expectedX).Within(0.00001f));
                match.RequestHitStop(2);
                Call(match, "SimulateMatchFrame");
                Call(match, "SimulateMatchFrame");
                Assert.That(projectile.transform.position.x, Is.EqualTo(expectedX).Within(0.00001f));
                match.SetOptionsPaused(true);
                Call(match, "SimulateMatchFrame");
                Assert.That(projectile.transform.position.x, Is.EqualTo(expectedX).Within(0.00001f));
                match.SetOptionsPaused(false);
                Call(match, "SimulateMatchFrame");
                Assert.That(projectile.transform.position.x, Is.EqualTo(expectedX + shot.ProjectileSpeed / 60f).Within(0.00001f));

                int oldInstanceId = first.AttackInstanceId;
                Call(first, "StartAttack", normal);
                Assert.That(first.TryApplyProjectileHit(shot, oldInstanceId, 1f), Is.True);
                Assert.That((bool)typeof(FighterController).GetField("attackConnected", PrivateInstance).GetValue(first), Is.False);

                // リセット後は古い弾を即時無効化し、次ラウンドへ持ち越さない。
                match.ResetMatch();
                Assert.That(projectileObject.activeSelf, Is.False);
            }
            finally
            {
                Object.DestroyImmediate(projectileObject);
                Object.DestroyImmediate(matchObject);
                Object.DestroyImmediate(firstObject);
                Object.DestroyImmediate(secondObject);
                Object.DestroyImmediate(prefab);
                Object.DestroyImmediate(config);
                Object.DestroyImmediate(shot);
                Object.DestroyImmediate(normal);
            }
            yield return new ExitPlayMode();
        }

        [Test]
        public void AnimatorSync_StopsBeforeWritingConflictingActionDurations()
        {
            var database = ScriptableObject.CreateInstance<FighterAttackDatabase>();
            var first = ScriptableObject.CreateInstance<FighterAttackDefinition>();
            var second = ScriptableObject.CreateInstance<FighterAttackDefinition>();
            try
            {
                SetField(second, "startupFrames", first.StartupFrames + 1);
                database.EditorConfigure(first, second, null, null, null);
                LogAssert.Expect(LogType.Warning, new System.Text.RegularExpressions.Regex("Animator同期を中止しました"));
                Assert.That(FightAttackAnimatorSynchronizer.Sync(database), Is.False);
            }
            finally
            {
                Object.DestroyImmediate(database);
                Object.DestroyImmediate(first);
                Object.DestroyImmediate(second);
            }
        }
    }
}
