using System.Reflection;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace GASG.Fighting.Editor.Tests
{
    public sealed class FightIntroTests
    {
        private const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;

        private static void Call(object target, string name)
        {
            target.GetType().GetMethod(name, Private).Invoke(target, null);
        }

        [Test]
        public void Settings_ReferenceOriginalImagesAndKeepTransparency()
        {
            FightIntroSettings settings = Resources.Load<FightIntroSettings>(FightIntroSettings.ResourcePath);
            Assert.That(settings, Is.Not.Null);
            Assert.That(settings.GetRoundImage(1), Is.Not.Null);
            Assert.That(settings.GetRoundImage(2), Is.Not.Null);
            Assert.That(settings.GetRoundImage(3), Is.Null);
            Assert.That(settings.GetRoundImage(0), Is.Null);
            foreach (Texture2D image in new[] { settings.GetRoundImage(1), settings.GetRoundImage(2), settings.fightImage })
            {
                Assert.That(image, Is.Not.Null);
                string path = AssetDatabase.GetAssetPath(image);
                StringAssert.StartsWith("Assets/GASGFighter/Graphics/2D/UI/", path);
                TextureImporter importer = (TextureImporter)AssetImporter.GetAtPath(path);
                Assert.That(importer.npotScale, Is.EqualTo(TextureImporterNPOTScale.None));
                Assert.That(importer.alphaIsTransparency, Is.True);
                Assert.That(importer.mipmapEnabled, Is.False);
            }
        }

        [Test]
        public void Intro_PausesResetsChangesRoundImageAndReleasesControlOnFight()
        {
            GameObject root = new GameObject("Intro Test");
            FighterConfig config = ScriptableObject.CreateInstance<FighterConfig>();
            try
            {
                FightMatchManager match = root.AddComponent<FightMatchManager>();
                match.enabled = false;
                Call(match, "Awake");
                FighterController first = CreateFighter(root.transform, "P1", 1, config);
                FighterController second = CreateFighter(root.transform, "P2", 2, config);
                first.Initialize(match, second);
                second.Initialize(match, first);
                match.EditorConfigure(first, second);
                Call(match, "BeginMatch");
                Assert.That(match.RoundIntroActive, Is.True);
                Assert.That(first.State, Is.EqualTo(FighterState.RoundLocked));
                Assert.That(match.DisplayTimeSeconds, Is.EqualTo(99));

                var presentation = new FightIntroPresentation(root.transform,
                    Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf"), match.IntroSettings);
                Assert.That(presentation.Refresh(match), Is.True);
                Canvas canvas = root.GetComponentInChildren<Canvas>();
                Assert.That(canvas.renderMode, Is.EqualTo(RenderMode.ScreenSpaceOverlay));
                Assert.That(canvas.sortingOrder, Is.GreaterThan(1000));
                foreach (Graphic graphic in root.GetComponentsInChildren<Graphic>())
                    Assert.That(graphic.raycastTarget, Is.False);

                Call(match, "SimulateMatchFrame");
                float pausedProgress = match.IntroProgress;
                match.SetOptionsPaused(true);
                presentation.Refresh(match);
                Assert.That(canvas.enabled, Is.False);
                for (int i = 0; i < 10; i++) Call(match, "SimulateMatchFrame");
                Assert.That(match.IntroProgress, Is.EqualTo(pausedProgress));
                match.SetOptionsPaused(false);
                int frames = FightFrameTiming.SecondsToFrames(match.IntroSettings.roundSeconds);
                for (int i = 1; i < frames; i++) Call(match, "SimulateMatchFrame");
                Assert.That(match.RoundActive, Is.True);
                Assert.That(match.FightIntroActive, Is.True);
                Assert.That(match.IntroProgress, Is.EqualTo(0f));
                Assert.That(first.State, Is.Not.EqualTo(FighterState.RoundLocked));
                presentation.Refresh(match);
                Assert.That(root.transform.Find("Round Intro Overlay (Runtime)/Announcement Image").GetComponent<RawImage>().texture,
                    Is.EqualTo(match.IntroSettings.fightImage));

                int fightFrames = FightFrameTiming.SecondsToFrames(match.IntroSettings.fightSeconds);
                for (int i = 0; i < fightFrames; i++) Call(match, "SimulateMatchFrame");
                Assert.That(match.RoundActive, Is.True);
                Assert.That(match.FightIntroActive, Is.False);
                Assert.That(presentation.Refresh(match), Is.False);
                Assert.That(match.DisplayTimeSeconds, Is.LessThan(99));

                Call(match, "FinishRound");
                Assert.That(presentation.Refresh(match), Is.True);
                Assert.That(canvas.enabled, Is.True);
                for (int i = 0; i < 400 && !match.RoundIntroActive; i++) Call(match, "SimulateMatchFrame");
                Call(match, "PrepareRound");
                presentation.Refresh(match);
                Assert.That(root.transform.Find("Round Intro Overlay (Runtime)/Announcement Image").GetComponent<RawImage>().texture,
                    Is.EqualTo(match.IntroSettings.GetRoundImage(2)));

                Call(match, "StartRoundControl");
                Call(match, "FinishRound");
                for (int i = 0; i < 400 && !match.RoundIntroActive; i++) Call(match, "SimulateMatchFrame");
                Call(match, "PrepareRound");
                presentation.Refresh(match);
                Text fallback = root.GetComponentInChildren<Text>();
                Assert.That(fallback.enabled, Is.True);
                Assert.That(fallback.text, Is.EqualTo("ROUND 03"));
                match.ResetMatch();
                Assert.That(match.RoundNumber, Is.EqualTo(1));
                Assert.That(match.IntroProgress, Is.EqualTo(0f));
                presentation.SetVisible(false);
                Assert.That(canvas.enabled, Is.False);
            }
            finally
            {
                Object.DestroyImmediate(root);
                Object.DestroyImmediate(config);
            }
        }

        private static FighterController CreateFighter(Transform parent, string name, int index, FighterConfig config)
        {
            GameObject go = new GameObject(name);
            go.transform.SetParent(parent, false);
            FighterController fighter = go.AddComponent<FighterController>();
            fighter.EditorConfigure(index, config, go.AddComponent<FighterInputSource>(), null, null);
            return fighter;
        }

        [Test]
        public void Results_KnockoutFinalRoundWinnerPauseAndReset()
        {
            GameObject root = new GameObject("Result Test");
            FighterConfig config = ScriptableObject.CreateInstance<FighterConfig>();
            try
            {
                FightMatchManager match = root.AddComponent<FightMatchManager>();
                match.enabled = false;
                Call(match, "Awake");
                FighterController first = CreateFighter(root.transform, "P1", 1, config);
                FighterController second = CreateFighter(root.transform, "P2", 2, config);
                first.Initialize(match, second);
                second.Initialize(match, first);
                match.EditorConfigure(first, second);
                typeof(FightMatchManager).GetField("player2DisplayName", Private).SetValue(match, "AKIRA");
                Call(match, "BeginMatch");
                var presentation = new FightIntroPresentation(root.transform,
                    Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf"), match.IntroSettings);
                for (int round = 0; round < 3; round++)
                {
                    Assert.That(match.IsFinalRound, Is.EqualTo(round == 2));
                    if (round == 2)
                    {
                        presentation.Refresh(match);
                        Assert.That(root.transform.Find("Round Intro Overlay (Runtime)/Missing Round Image Fallback").GetComponent<Text>().text,
                            Is.EqualTo("FINAL\nROUND"));
                    }
                    Call(match, "StartRoundControl");
                    FighterController loser = round == 0 ? second : first;
                    typeof(FighterController).GetField("<CurrentHealth>k__BackingField", Private).SetValue(loser, 0);
                    Call(match, "SimulateMatchFrame");
                    Assert.That(match.KnockoutActive, Is.True);
                    Assert.That(match.RoundEndMessage, Is.EqualTo("K.O."));
                    Assert.That(match.RoundActive, Is.False);
                    Assert.That(match.CanRematch, Is.False);
                    int wins = match.Player1Rounds + match.Player2Rounds;
                    Call(match, "FinishRound");
                    Assert.That(match.Player1Rounds + match.Player2Rounds, Is.EqualTo(wins));
                    match.SetOptionsPaused(true);
                    float progress = match.ResultProgress;
                    for (int i = 0; i < 10; i++) Call(match, "SimulateMatchFrame");
                    Assert.That(match.ResultProgress, Is.EqualTo(progress));
                    match.SetOptionsPaused(false);
                    for (int i = 0; i < 500 && !match.RoundIntroActive && !match.WinnerActive; i++) Call(match, "SimulateMatchFrame");
                }
                Assert.That(match.WinnerActive, Is.True);
                Assert.That(match.WinnerDisplayName, Is.EqualTo("AKIRA"));
                presentation.Refresh(match);
                Assert.That(root.transform.Find("Round Intro Overlay (Runtime)/Winner Name").GetComponent<Text>().text, Is.EqualTo("AKIRA"));
                Assert.That(match.CanRematch, Is.False);
                for (int i = 0; i < 500 && !match.CanRematch; i++) Call(match, "SimulateMatchFrame");
                Assert.That(match.CanRematch, Is.True);
                match.ResetMatch();
                Assert.That(match.ResultPhase, Is.EqualTo(FightMatchManager.ResultStage.None));
                Assert.That(match.WinnerPlayerIndex, Is.Zero);
                Assert.That(match.RoundNumber, Is.EqualTo(1));
                Call(match, "StartRoundControl");
                typeof(FighterController).GetField("<CurrentHealth>k__BackingField", Private).SetValue(first, 0);
                typeof(FighterController).GetField("<CurrentHealth>k__BackingField", Private).SetValue(second, 0);
                Call(match, "SimulateMatchFrame");
                Assert.That(match.RoundEndMessage, Is.EqualTo("DOUBLE K.O."));
                Assert.That(match.Player1Rounds + match.Player2Rounds, Is.Zero);
                match.ResetMatch();
                Call(match, "StartRoundControl");
                typeof(FightMatchManager).GetField("roundFramesRemaining", Private).SetValue(match, 1);
                Call(match, "SimulateMatchFrame");
                Assert.That(match.RoundEndMessage, Is.EqualTo("TIME UP"));
                Assert.That(match.WinnerPlayerIndex, Is.Zero);
            }
            finally
            {
                Object.DestroyImmediate(root);
                Object.DestroyImmediate(config);
            }
        }

        // バッチ検証からも同じテストを実行できる。Sceneやアセットは保存しない。
        public static void RunBatch()
        {
            var tests = new FightIntroTests();
            tests.Settings_ReferenceOriginalImagesAndKeepTransparency();
            tests.Intro_PausesResetsChangesRoundImageAndReleasesControlOnFight();
            tests.Results_KnockoutFinalRoundWinnerPauseAndReset();
            Debug.Log("[GASG Fighter][成功] Round Intro: images, overlay, timing, pause, round02, round03 fallback, reset passed.");
        }
    }
}
