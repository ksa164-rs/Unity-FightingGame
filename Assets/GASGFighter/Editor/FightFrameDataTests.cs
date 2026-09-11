using GASG.Fighting;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace GASG.Fighting.Editor.Tests
{
    public sealed class FightFrameDataTests
    {
        [Test]
        public void SecondsToFrames_UsesSharedSixtyHertzRate()
        {
            Assert.That(FightFrameTiming.SecondsToFrames(0.5f), Is.EqualTo(30));
            Assert.That(FightFrameTiming.SecondsToFrames(1f), Is.EqualTo(60));
        }

        [Test]
        public void HitboxFrame_IncludesStartAndEndFrames()
        {
            AttackHitboxFrame hitbox = new AttackHitboxFrame(
                "Test",
                5,
                7,
                Vector3.zero,
                Vector3.one);

            Assert.That(hitbox.IsActive(4), Is.False);
            Assert.That(hitbox.IsActive(5), Is.True);
            Assert.That(hitbox.IsActive(7), Is.True);
            Assert.That(hitbox.IsActive(8), Is.False);
        }

        [Test]
        public void HitboxFrame_ClampsInvalidEditorValues()
        {
            AttackHitboxFrame hitbox = new AttackHitboxFrame(
                "Test",
                -2,
                -10,
                Vector3.zero,
                Vector3.zero);

            Assert.That(hitbox.StartFrame, Is.EqualTo(0));
            Assert.That(hitbox.EndFrame, Is.EqualTo(0));
            Assert.That(hitbox.Size.x, Is.GreaterThan(0f));
            Assert.That(hitbox.Size.y, Is.GreaterThan(0f));
            Assert.That(hitbox.Size.z, Is.GreaterThan(0f));
        }

        [TestCase(14, 3, 7, 4)]
        [TestCase(9, 3, 7, -1)]
        [TestCase(22, 4, 14, 4)]
        [TestCase(16, 4, 14, -2)]
        public void CalculateFrameAdvantage_UsesFirstActiveFrame(
            int defenderStunFrames,
            int activeFrames,
            int recoveryFrames,
            int expectedAdvantage)
        {
            int actual = FighterAttackDefinition.CalculateFrameAdvantage(
                defenderStunFrames,
                activeFrames,
                recoveryFrames);

            Assert.That(actual, Is.EqualTo(expectedAdvantage));
        }

        [Test]
        public void InputDirection_IsRelativeToFacingDirection()
        {
            Assert.That(
                FighterInputHistory.Quantize(1f, 0f, 1f),
                Is.EqualTo(FighterInputDirection.Forward));
            Assert.That(
                FighterInputHistory.Quantize(1f, 0f, -1f),
                Is.EqualTo(FighterInputDirection.Back));
            Assert.That(
                FighterInputHistory.Quantize(-1f, -1f, -1f),
                Is.EqualTo(FighterInputDirection.DownForward));
        }

        [Test]
        public void InputHistory_MatchesQuarterCircleWithHeldDirections()
        {
            FighterInputHistory history = new FighterInputHistory();
            history.Advance(0f, -1f, 1f);
            history.Advance(0f, -1f, 1f);
            history.Advance(1f, -1f, 1f);
            history.Advance(1f, 0f, 1f);

            FighterInputDirection[] command =
            {
                FighterInputDirection.Down,
                FighterInputDirection.DownForward,
                FighterInputDirection.Forward
            };

            Assert.That(history.Matches(command, 12), Is.True);
            Assert.That(history.Matches(command, 2), Is.False);
        }

        [Test]
        public void ConsumedCommand_CannotReuseOldDirectionSequence()
        {
            FighterInputHistory history = new FighterInputHistory();
            history.Advance(1f, 0f, 1f);
            history.Advance(0f, -1f, 1f);
            history.Advance(1f, -1f, 1f);
            FighterInputDirection[] command =
            {
                FighterInputDirection.Forward,
                FighterInputDirection.Down,
                FighterInputDirection.DownForward
            };

            Assert.That(history.Matches(command, 12), Is.True);
            history.ConsumeDirectionalCommand();
            Assert.That(history.Matches(command, 12), Is.False);
        }

        [Test]
        public void InputHistory_PreservesDirectionAndButtonForVisualization()
        {
            FighterInputHistory history = new FighterInputHistory();
            FighterInputFrame input = new FighterInputFrame
            {
                moveX = 1f,
                moveY = -1f,
                lightPressed = true
            };

            history.Advance(input, 1f);

            Assert.That(history.GetDirectionFromNewest(0), Is.EqualTo(FighterInputDirection.DownForward));
            Assert.That(history.GetInputFromNewest(0).lightPressed, Is.True);
            Assert.That(history.GetInputFromNewest(0).heavyPressed, Is.False);
        }

        [Test]
        public void DisplayHistory_DoesNotShowNeutralForButtonOnlyInput()
        {
            FightDisplayHistory history = new FightDisplayHistory();

            history.Advance(Vector2.zero, 1);

            Assert.That(history.GetNewest(0).Direction, Is.EqualTo(0));
            Assert.That(history.GetNewest(0).Buttons, Is.EqualTo(1));
        }

        [Test]
        public void PrototypeDatabase_HasUniqueMoveIdsAndShoryukenCommand()
        {
            FighterAttackDatabase database = AssetDatabase.LoadAssetAtPath<FighterAttackDatabase>(
                "Assets/GASGFighter/Data/AttackMotionDatabase_Prototype.asset");

            Assert.That(database, Is.Not.Null);
            Assert.That(database.MoveBindings.Count, Is.EqualTo(7));

            System.Collections.Generic.HashSet<string> moveIds =
                new System.Collections.Generic.HashSet<string>();
            for (int i = 0; i < database.MoveBindings.Count; i++)
            {
                FighterMoveBinding binding = database.MoveBindings[i];
                Assert.That(binding, Is.Not.Null);
                Assert.That(binding.Attack, Is.Not.Null, binding.MoveId);
                Assert.That(moveIds.Add(binding.MoveId), Is.True, binding.MoveId);
            }

            FighterMoveBinding shoryuken = database.FindMoveById("ken.light_shoryuken.command");
            Assert.That(shoryuken, Is.Not.Null);
            Assert.That(shoryuken.Attack.ActionId, Is.EqualTo("LightShoryuken"));
            Assert.That(shoryuken.Attack.AnimationClip, Is.Not.Null);
            Assert.That(shoryuken.Attack.AnimationClip.name, Is.EqualTo("shouryuken"));
            Assert.That(shoryuken.InputButton, Is.EqualTo(FighterAttackButton.Light));
            Assert.That(shoryuken.CancelCategory, Is.EqualTo(AttackCancelTarget.Special));
            Assert.That(shoryuken.CommandDirections, Is.EqualTo(new[]
            {
                FighterInputDirection.Forward,
                FighterInputDirection.Down,
                FighterInputDirection.DownForward
            }));
        }

        [Test]
        public void AttackDefinition_UsesReusableActionReference()
        {
            FighterActionDefinition action = ScriptableObject.CreateInstance<FighterActionDefinition>();
            FighterAttackDefinition attack = ScriptableObject.CreateInstance<FighterAttackDefinition>();
            AnimationClip clip = new AnimationClip();

            try
            {
                action.EditorConfigure("attack.test", "Test Attack", clip);
                attack.EditorSetAction(action);

                Assert.That(attack.Action, Is.SameAs(action));
                Assert.That(attack.ActionId, Is.EqualTo("attack.test"));
                Assert.That(attack.AnimationClip, Is.SameAs(clip));
            }
            finally
            {
                Object.DestroyImmediate(attack);
                Object.DestroyImmediate(action);
                Object.DestroyImmediate(clip);
            }
        }

        [Test]
        public void ActionCatalog_ReportsDuplicateIds()
        {
            FighterActionCatalog catalog = ScriptableObject.CreateInstance<FighterActionCatalog>();
            FighterActionDefinition first = ScriptableObject.CreateInstance<FighterActionDefinition>();
            FighterActionDefinition second = ScriptableObject.CreateInstance<FighterActionDefinition>();

            try
            {
                first.EditorConfigure("attack.duplicate", "First", null);
                second.EditorConfigure("attack.duplicate", "Second", null);
                catalog.EditorAddAction(first);
                catalog.EditorAddAction(second);

                Assert.That(catalog.ValidateCatalog(out string report), Is.False);
                StringAssert.Contains("重複", report);
            }
            finally
            {
                Object.DestroyImmediate(first);
                Object.DestroyImmediate(second);
                Object.DestroyImmediate(catalog);
            }
        }

        [TestCase("attack.light", true)]
        [TestCase("state_crouch-idle", true)]
        [TestCase("attack light", false)]
        [TestCase("攻撃.弱", false)]
        [TestCase("", false)]
        public void ActionIdValidation_AcceptsOnlyStableIdCharacters(string actionId, bool expected)
        {
            Assert.That(FighterActionCatalog.IsValidActionId(actionId), Is.EqualTo(expected));
        }
    }
}
