using System.Collections.Generic;
using System.Reflection;
using GASG.Fighting;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace GASG.Fighting.Editor.Tests
{
    /// <summary>
    /// Transformで確定した同一tickの座標を、接触収集が参照できることを確認します。
    /// Project SettingsのAuto Sync Transformsは無効のまま検証します。
    /// </summary>
    public sealed class FightPhysics2DSyncTests
    {
        private const BindingFlags PrivateInstance = BindingFlags.Instance | BindingFlags.NonPublic;

        private readonly List<Object> createdObjects = new List<Object>();
        private bool previousAutoSyncTransforms;
        private FightMatchManager match;
        private FighterController player1;
        private FighterController player2;
        private FighterConfig config;

        [SetUp]
        public void SetUp()
        {
            previousAutoSyncTransforms = Physics2D.autoSyncTransforms;
            Physics2D.autoSyncTransforms = false;

            config = Track(ScriptableObject.CreateInstance<FighterConfig>());
            config.EditorConfigure(
                "Physics2D Sync Test",
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
                null,
                null,
                null,
                null,
                null);

            GameObject matchObject = Track(new GameObject("Physics2DSyncTest_Match"));
            match = matchObject.AddComponent<FightMatchManager>();
            match.enabled = false;
            player1 = CreateFighter("Physics2DSyncTest_Player1", 1);
            player2 = CreateFighter("Physics2DSyncTest_Player2", 2);
            player1.Initialize(match, player2);
            player2.Initialize(match, player1);
            match.EditorConfigure(player1, player2);
            Call(match, "BeginMatch");
            Call(match, "StartRoundControl");
        }

        [TearDown]
        public void TearDown()
        {
            Physics2D.autoSyncTransforms = previousAutoSyncTransforms;
            for (int i = createdObjects.Count - 1; i >= 0; i--)
            {
                if (createdObjects[i] != null)
                {
                    Object.DestroyImmediate(createdObjects[i]);
                }
            }

            createdObjects.Clear();
            Physics2D.SyncTransforms();
        }

        [Test]
        public void MeleeContact_UsesPositionMovedImmediatelyBeforeTick()
        {
            FighterAttackDefinition attack = CreateAttack(new Vector3(0.8f, 1f, 0f), new Vector3(0.6f, 1f, 1f));
            SetPositionsAndSyncOldPhysics(0f, 4f);
            Call(player1, "StartAttack", attack);

            player2.SetPositionX(1.1f);
            SimulateMatchFrame();

            Assert.That(player2.CurrentHealth, Is.EqualTo(990),
                "移動直後のHurtboxが同じtickの近接判定へ反映されていません。");
        }

        [Test]
        public void DashContact_UsesPositionAfterStepMovement()
        {
            FighterAttackDefinition attack = CreateAttack(new Vector3(0.725f, 1f, 0f), new Vector3(0.15f, 1f, 1f));
            SetPositionsAndSyncOldPhysics(0f, 1.32f);
            Call(player1, "StartAttack", attack);
            Call(player2, "StartStep", -1);

            SimulateMatchFrame();

            Assert.That(player2.transform.position.x, Is.EqualTo(1.1f).Within(0.0001f));
            Assert.That(player2.CurrentHealth, Is.EqualTo(990),
                "ステップ移動後のHurtboxが同じtickの近接判定へ反映されていません。");
        }

        [Test]
        public void ScreenEdgeContact_UsesClampedPosition()
        {
            FighterAttackDefinition attack = CreateAttack(new Vector3(0.8f, 1f, 0f), new Vector3(0.6f, 1f, 1f));
            SetPositionsAndSyncOldPhysics(6.9f, 4f);
            Call(player1, "StartAttack", attack);

            player2.SetPositionX(20f);
            SimulateMatchFrame();

            Assert.That(player2.transform.position.x, Is.EqualTo(config.StageHalfWidth));
            Assert.That(player2.CurrentHealth, Is.EqualTo(990),
                "画面端へClampされたHurtboxが同じtickの判定へ反映されていません。");
        }

        [Test]
        public void PushboxContact_UsesBothResolvedPositions()
        {
            FighterAttackDefinition attack = CreateAttack(new Vector3(0.8f, 1f, 0f), new Vector3(0.6f, 1f, 1f));
            SetPositionsAndSyncOldPhysics(-4f, 4f);
            player1.SetPositionX(0f);
            player2.SetPositionX(0.5f);
            Call(player1, "StartAttack", attack);

            SimulateMatchFrame();

            Assert.That(player1.transform.position.x, Is.EqualTo(-0.3f).Within(0.0001f));
            Assert.That(player2.transform.position.x, Is.EqualTo(0.8f).Within(0.0001f));
            Assert.That(player2.CurrentHealth, Is.EqualTo(990),
                "PushBox解決後のHurtboxが同じtickの近接判定へ反映されていません。");
        }

        [Test]
        public void MultipleTicks_SynchronizeEveryProcessedTick()
        {
            FighterAttackDefinition attack = CreateAttack(new Vector3(0.8f, 1f, 0f), new Vector3(0.6f, 1f, 1f));
            SetPositionsAndSyncOldPhysics(0f, 4f);
            Call(player1, "StartAttack", attack);

            SimulateMatchFrame();
            Assert.That(player2.CurrentHealth, Is.EqualTo(1000));

            player2.SetPositionX(1.1f);
            SimulateMatchFrame();

            Assert.That(player2.CurrentHealth, Is.EqualTo(990),
                "追いつき処理の後続tickでHurtbox座標が更新されていません。");
        }

        [Test]
        public void ProjectileContact_UsesOpponentPositionMovedImmediatelyBeforeTick()
        {
            FighterAttackDefinition attack = CreateAttack(Vector3.zero, Vector3.one);
            SetField(attack, "isProjectile", true);
            SetPositionsAndSyncOldPhysics(-2f, 4f);
            player2.SetPositionX(0.1f);

            GameObject projectileObject = Track(new GameObject("Physics2DSyncTest_Projectile"));
            projectileObject.transform.position = Vector3.zero;
            FighterProjectile projectile = projectileObject.AddComponent<FighterProjectile>();
            projectile.Initialize(player1, attack, Vector3.right);

            // Runtimeでは次フレームに破棄される。EditModeで実メソッドを直接呼ぶため既知ログを受ける。
            LogAssert.Expect(
                LogType.Error,
                new System.Text.RegularExpressions.Regex("^Destroy may not be called from edit mode!"));
            SimulateMatchFrame();

            Assert.That(player2.CurrentHealth, Is.EqualTo(990),
                "移動直後のHurtboxが同じtickの飛び道具判定へ反映されていません。");
        }

        [Test]
        public void SimultaneousMeleeContacts_UseSameSynchronizedSnapshot()
        {
            FighterAttackDefinition player1Attack = CreateAttack(new Vector3(0.8f, 1f, 0f), new Vector3(0.6f, 1f, 1f));
            FighterAttackDefinition player2Attack = CreateAttack(new Vector3(0.8f, 1f, 0f), new Vector3(0.6f, 1f, 1f));
            SetPositionsAndSyncOldPhysics(-4f, 4f);
            player1.SetPositionX(0f);
            player2.SetPositionX(1.1f);
            Call(player1, "StartAttack", player1Attack);
            Call(player2, "StartAttack", player2Attack);

            SimulateMatchFrame();

            Assert.That(player1.CurrentHealth, Is.EqualTo(990));
            Assert.That(player2.CurrentHealth, Is.EqualTo(990),
                "両者の接触候補を同じPhysics2D座標スナップショットから収集できていません。");
        }

        private FighterController CreateFighter(string objectName, int playerIndex)
        {
            GameObject fighterObject = Track(new GameObject(objectName));
            GameObject hurtboxObject = new GameObject("Hurtbox");
            hurtboxObject.transform.SetParent(fighterObject.transform, false);
            BoxCollider2D hurtbox = hurtboxObject.AddComponent<BoxCollider2D>();
            hurtbox.isTrigger = true;
            hurtbox.offset = new Vector2(0f, 1f);
            hurtbox.size = new Vector2(0.9f, 2f);

            FighterInputSource input = fighterObject.AddComponent<FighterInputSource>();
            FighterController fighter = fighterObject.AddComponent<FighterController>();
            fighter.EditorConfigure(playerIndex, config, input, null, null);
            return fighter;
        }

        private FighterAttackDefinition CreateAttack(Vector3 hitboxCenter, Vector3 hitboxSize)
        {
            FighterAttackDefinition attack = Track(ScriptableObject.CreateInstance<FighterAttackDefinition>());
            attack.EditorConfigure(
                "Physics2D Sync Test Attack",
                "Physics2DSyncTestAttack",
                1,
                30,
                1,
                10,
                1,
                1,
                0,
                0f,
                GuardHeight.Unblockable,
                hitboxCenter,
                hitboxSize,
                false,
                0);
            return attack;
        }

        private void SetPositionsAndSyncOldPhysics(float player1X, float player2X)
        {
            player1.SetPositionX(player1X);
            player2.SetPositionX(player2X);
            Physics2D.SyncTransforms();
        }

        private void SimulateMatchFrame()
        {
            Call(match, "SimulateMatchFrame");
        }

        private T Track<T>(T createdObject) where T : Object
        {
            createdObjects.Add(createdObject);
            return createdObject;
        }

        private static void SetField(object target, string name, object value)
        {
            FieldInfo field = target.GetType().GetField(name, PrivateInstance);
            Assert.That(field, Is.Not.Null, $"privateフィールドが見つかりません: {name}");
            field.SetValue(target, value);
        }

        private static object Call(object target, string name, params object[] args)
        {
            MethodInfo method = target.GetType().GetMethod(name, PrivateInstance);
            Assert.That(method, Is.Not.Null, $"privateメソッドが見つかりません: {name}");
            return method.Invoke(target, args);
        }
    }
}
