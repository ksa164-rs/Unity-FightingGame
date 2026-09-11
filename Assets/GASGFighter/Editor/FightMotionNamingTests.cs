using GASG.Fighting;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace GASG.Fighting.Editor.Tests
{
    public sealed class FightMotionNamingTests
    {
        [TestCase("AS_p01_000", true)]
        [TestCase("AS_p99_999", true)]
        [TestCase("AS_p01_000_001", false)]
        [TestCase("AS_ｐ01_000", false)]
        [TestCase("AS_p00_000", false)]
        [TestCase("AS_p1_000", false)]
        public void MotionNumber_RequiresExactHalfWidthFormat(string value, bool expected)
        {
            Assert.That(FighterMotionNaming.IsValid(value), Is.EqualTo(expected));
        }

        [Test]
        public void ChangingDisplayIdentity_PreservesRuntimeIdAndClip()
        {
            var action = ScriptableObject.CreateInstance<FighterActionDefinition>();
            var clip = new AnimationClip();
            try
            {
                action.EditorConfigure("Idle", "Old", clip);
                action.EditorSetMotionIdentity("AS_p01_000", "待機");
                Assert.That(action.ActionId, Is.EqualTo("Idle"));
                Assert.That(action.AnimationClip, Is.SameAs(clip));
                Assert.That(action.DisplayLabel, Is.EqualTo("AS_p01_000 / 待機"));
            }
            finally { Object.DestroyImmediate(action); Object.DestroyImmediate(clip); }
        }

        [Test]
        public void SuggestedNumber_UsesReservedHundredRangeAndCatalogRejectsDuplicates()
        {
            var catalog = ScriptableObject.CreateInstance<FighterActionCatalog>();
            var first = ScriptableObject.CreateInstance<FighterActionDefinition>();
            var second = ScriptableObject.CreateInstance<FighterActionDefinition>();
            try
            {
                first.EditorConfigure("one", "One", null);
                second.EditorConfigure("two", "Two", null);
                first.EditorSetMotionIdentity("AS_p01_209", "弱");
                second.EditorSetMotionIdentity("AS_p01_209", "中");
                catalog.EditorAddAction(first);
                Assert.That(FighterMotionNaming.SuggestNext(catalog, 1, 200), Is.EqualTo("AS_p01_210"));
                Assert.That(FighterMotionNaming.SuggestNext(catalog, 2, 200), Is.EqualTo("AS_p02_200"));
                catalog.EditorAddAction(second);
                Assert.That(catalog.ValidateCatalog(out string report), Is.False);
                StringAssert.Contains("管理番号が重複", report);
            }
            finally { Object.DestroyImmediate(catalog); Object.DestroyImmediate(first); Object.DestroyImmediate(second); }
        }

        [Test]
        public void PrototypeCatalog_UsesUniqueThreeSegmentNumbers()
        {
            var catalog = AssetDatabase.LoadAssetAtPath<FighterActionCatalog>(FightActionCatalogMigration.CatalogPath);
            Assert.That(catalog, Is.Not.Null);
            Assert.That(catalog.Actions.Count, Is.EqualTo(20));
            Assert.That(catalog.ValidateCatalog(out string report), Is.True, report);
            foreach (FighterActionDefinition action in catalog.Actions)
            {
                if (!string.IsNullOrEmpty(action.MotionId))
                    Assert.That(FighterMotionNaming.IsValid(action.MotionId), Is.True, action.name);
            }
            Assert.That(catalog.FindByMotionId("AS_p01_000").ActionId, Is.EqualTo("Idle"));
            Assert.That(catalog.FindByMotionId("AS_p01_200").ActionId, Is.EqualTo("StandingLightPunch"));
            Assert.That(catalog.FindByMotionId("AS_p01_202").ActionId, Is.EqualTo("StandingHeavyPunch"));
        }
    }
}