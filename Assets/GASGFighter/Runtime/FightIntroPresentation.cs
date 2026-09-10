using UnityEngine;
using UnityEngine.UI;

namespace GASG.Fighting
{
    // 開始演出だけを前面Canvasへ分離し、既存HUDの奥行き設定を保持する。
    public sealed class FightIntroPresentation
    {
        private readonly Canvas canvas;
        private readonly RawImage main;
        private readonly RawImage leftEcho;
        private readonly RawImage rightEcho;
        private readonly Image backdrop;
        private readonly Text fallback;
        private readonly Text winnerName;
        private readonly Image finalAccent;
        private readonly Image knockoutFade;
        private readonly FightAnnouncementGradient textGradient;
        private readonly FightIntroSettings settings;

        public FightIntroPresentation(Transform parent, Font font, FightIntroSettings settings)
        {
            this.settings = settings;
            GameObject root = new GameObject("Round Intro Overlay (Runtime)",
                typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler));
            root.transform.SetParent(parent, false);
            canvas = root.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = settings != null ? settings.sortingOrder : 32000;
            CanvasScaler scaler = root.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            // 横幅に合わせ、縦長画面でも画像が左右にはみ出さないようにする。
            scaler.matchWidthOrHeight = 0f;

            GameObject shade = new GameObject("Intro Backdrop", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            shade.transform.SetParent(root.transform, false);
            backdrop = shade.GetComponent<Image>();
            backdrop.raycastTarget = false;
            backdrop.rectTransform.anchorMin = Vector2.zero;
            backdrop.rectTransform.anchorMax = Vector2.one;
            backdrop.rectTransform.offsetMin = Vector2.zero;
            backdrop.rectTransform.offsetMax = Vector2.zero;
            GameObject accent = new GameObject("Final Round Accent", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            accent.transform.SetParent(root.transform, false);
            finalAccent = accent.GetComponent<Image>();
            finalAccent.raycastTarget = false;
            finalAccent.rectTransform.localRotation = Quaternion.Euler(0f, 0f, -7f);
            leftEcho = CreateImage("Cyan Echo", root.transform);
            rightEcho = CreateImage("Purple Echo", root.transform);
            main = CreateImage("Announcement Image", root.transform);

            GameObject label = new GameObject("Missing Round Image Fallback", typeof(RectTransform), typeof(CanvasRenderer), typeof(Text));
            label.transform.SetParent(root.transform, false);
            fallback = label.GetComponent<Text>();
            fallback.font = font;
            fallback.fontSize = 130;
            fallback.fontStyle = FontStyle.Bold;
            fallback.alignment = TextAnchor.MiddleCenter;
            fallback.raycastTarget = false;
            fallback.supportRichText = false;
            fallback.rectTransform.sizeDelta = new Vector2(1500f, 250f);
            textGradient = label.AddComponent<FightAnnouncementGradient>();
            Outline outline = label.AddComponent<Outline>();
            outline.effectColor = new Color(0.07f, 0.04f, 0.12f, 0.85f);
            outline.effectDistance = new Vector2(2f, -3f);
            GameObject nameLabel = new GameObject("Winner Name", typeof(RectTransform), typeof(CanvasRenderer), typeof(Text));
            nameLabel.transform.SetParent(root.transform, false);
            winnerName = nameLabel.GetComponent<Text>();
            winnerName.font = font;
            winnerName.fontStyle = FontStyle.Bold;
            winnerName.alignment = TextAnchor.MiddleCenter;
            winnerName.raycastTarget = false;
            winnerName.supportRichText = false;
            winnerName.resizeTextForBestFit = true;
            winnerName.resizeTextMinSize = 24;
            winnerName.rectTransform.sizeDelta = new Vector2(1300f, 150f);
            nameLabel.AddComponent<Outline>().effectColor = new Color(0f, 0f, 0f, 0.7f);
            GameObject fadeObject = new GameObject("K.O. Fade", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            fadeObject.transform.SetParent(root.transform, false);
            knockoutFade = fadeObject.GetComponent<Image>();
            knockoutFade.color = Color.black;
            knockoutFade.raycastTarget = false;
            knockoutFade.rectTransform.anchorMin = Vector2.zero;
            knockoutFade.rectTransform.anchorMax = Vector2.one;
            knockoutFade.rectTransform.offsetMin = Vector2.zero;
            knockoutFade.rectTransform.offsetMax = Vector2.zero;
            knockoutFade.enabled = false;
            SetVisible(false);
        }

        public void SetVisible(bool visible)
        {
            if (canvas != null) canvas.enabled = visible;
        }

        public bool Refresh(FightMatchManager match)
        {
            if (match == null) { SetVisible(false); return false; }
            bool round = match.RoundIntroActive;
            bool fight = match.FightIntroActive;
            bool knockout = match.KnockoutActive;
            bool winner = match.WinnerActive;
            bool resultPause = match.ResultPhase == FightMatchManager.ResultStage.Pause;
            bool resetFadeIn = match.RoundResetFadeInActive;
            bool finalRound = round && match.IsFinalRound;
            bool visible = round || fight || knockout || winner || resultPause || resetFadeIn;
            // オプション操作中は隠し、閉じたら同じ進行位置から表示する。
            SetVisible(visible && !match.OptionsPaused);
            UpdateKnockoutFade(match);
            if (!visible) return false;
            // 結果待ち中は黒フェードだけを表示し、中央メッセージは表示しない。
            if (resultPause) return true;

            if (resetFadeIn)
            {
                knockoutFade.enabled = true;
                knockoutFade.color = new Color(0f, 0f, 0f, 1f - match.RoundResetFadeInProgress);
            }

            float progress = knockout || winner ? match.ResultProgress : match.IntroProgress;
            float entrance = settings != null ? settings.entranceFraction : 0.18f;
            float exit = settings != null ? settings.exitFraction : 0.16f;
            float enterT = Mathf.Clamp01(progress / Mathf.Max(0.01f, entrance));
            float exitT = Mathf.Clamp01((progress - (1f - exit)) / Mathf.Max(0.01f, exit));
            float ease = 1f - Mathf.Pow(1f - enterT, 3f);
            float alpha = Mathf.Clamp01(enterT * 4f) * (1f - exitT);
            float scale = knockout ? Mathf.Lerp(settings != null ? settings.knockoutStartScale : 2.5f, 1f, ease) : fight
                ? Mathf.Lerp(settings != null ? settings.fightStartScale : 1.55f, 1f, ease)
                : Mathf.Lerp(0.85f, 1f, ease);
            Vector2 position = settings != null ? settings.centerOffset : Vector2.zero;
            if (winner) position += settings != null ? settings.winnerOffset : new Vector2(0f, -40f);
            position.x += (round ? -(settings != null ? settings.roundSlideDistance : 160f) * (1f - ease) : 0f)
                + (knockout || winner ? 0f : (settings != null ? settings.exitSlideDistance : 300f) * exitT * exitT);

            Texture2D texture = settings != null ? (fight ? settings.fightImage : settings.GetRoundImage(match.RoundNumber)) : null;
            float width = settings != null ? (fight ? settings.fightWidth : settings.roundWidth) : (fight ? 1050f : 780f);
            if (finalRound) { texture = settings != null ? settings.finalRoundImage : null; width = settings != null ? settings.finalRoundWidth : 780f; }
            if (knockout) { texture = settings != null && match.RoundEndMessage == "K.O." ? settings.knockoutImage : null; width = settings != null ? settings.knockoutWidth : 700f; }
            if (winner) { texture = settings != null ? settings.winImage : null; width = settings != null ? settings.winWidth : 500f; }
            Vector2 size = new Vector2(width, texture != null ? width * texture.height / texture.width : 250f);
            // 色の残像は出現直後と退場時だけ広げ、静止中の文字を読みやすくする。
            float echo = Mathf.Max(1f - ease, exitT);
            float distance = (settings != null ? settings.echoDistance : 35f) * echo;
            Color left = settings != null ? settings.leftEchoColor : Color.cyan;
            Color right = settings != null ? settings.rightEchoColor : Color.magenta;
            left.a *= echo * alpha;
            right.a *= echo * alpha;
            Draw(main, texture, size, position, scale, new Color(1f, 1f, 1f, alpha));
            Draw(leftEcho, texture, size, position + Vector2.left * distance, scale, left);
            Draw(rightEcho, texture, size, position + Vector2.right * distance, scale, right);
            fallback.enabled = texture == null;
            textGradient.enabled = knockout || finalRound;
            if (texture == null)
            {
                string message = knockout ? match.RoundEndMessage : winner ? "WIN" : finalRound ? "FINAL\nROUND" : fight ? "FIGHT" : $"ROUND {match.RoundNumber:00}";
                if (fallback.text != message) fallback.text = message;
                fallback.fontSize = knockout ? (match.RoundEndMessage == "K.O." ? 240 : 130) : winner ? 130 : 130;
                fallback.resizeTextForBestFit = true;
                fallback.resizeTextMinSize = 24;
                fallback.resizeTextMaxSize = fallback.fontSize;
                fallback.rectTransform.sizeDelta = new Vector2(width, finalRound ? 330f : 300f);
                fallback.color = new Color(1f, 1f, 1f, alpha);
                fallback.rectTransform.anchoredPosition = position;
                fallback.rectTransform.localScale = Vector3.one * scale;
            }
            winnerName.enabled = winner;
            if (winner)
            {
                winnerName.text = match.WinnerDisplayName;
                winnerName.resizeTextMaxSize = settings != null ? settings.winnerNameFontSize : 100;
                winnerName.color = new Color(1f, 1f, 1f, alpha);
                winnerName.rectTransform.anchoredPosition = position + Vector2.up * 145f * scale;
                winnerName.rectTransform.localScale = Vector3.one * scale;
            }
            finalAccent.enabled = finalRound && texture == null;
            finalAccent.color = new Color(0.32f, 0.03f, 0.7f, alpha * 0.85f);
            finalAccent.rectTransform.sizeDelta = new Vector2(width * 0.85f, 210f);
            finalAccent.rectTransform.anchoredPosition = position;
            finalAccent.rectTransform.localScale = Vector3.one * scale;
            backdrop.color = new Color(0f, 0f, 0f, (settings != null ? settings.backdropOpacity : 0.08f) * alpha);
            canvas.sortingOrder = settings != null ? settings.sortingOrder : 32000;
            return true;
        }

        private void UpdateKnockoutFade(FightMatchManager match)
        {
            if (knockoutFade == null || match == null || match.OptionsPaused)
            {
                if (knockoutFade != null) knockoutFade.enabled = false;
                return;
            }

            float alpha = 0f;
            if (match.ResultPhase == FightMatchManager.ResultStage.Knockout)
            {
                float fadeSeconds = Mathf.Max(0.05f, match.KnockoutFadeOutSeconds);
                float fadeFrames = fadeSeconds * FightFrameTiming.SimulationRate;
                float remainingFraction = 1f - match.ResultProgress;
                // K.O.表示の終盤だけを黒にするため、設定時間を結果ステージのフレーム数から割合へ変換する。
                float totalFrames = Mathf.Max(1f, Mathf.RoundToInt((settings != null ? settings.knockoutSeconds : 1.25f) * FightFrameTiming.SimulationRate));
                alpha = Mathf.InverseLerp(fadeFrames, 0f, remainingFraction * totalFrames);
            }
            else if (match.ResultPhase == FightMatchManager.ResultStage.Pause)
            {
                // 次のラウンドの位置リセットが完了するまで完全な黒を維持する。
                alpha = 1f;
            }
            else if (match.ResultPhase == FightMatchManager.ResultStage.Winner)
            {
                float fadeSeconds = Mathf.Max(0.05f, match.KnockoutFadeInSeconds);
                float fadeFrames = fadeSeconds * FightFrameTiming.SimulationRate;
                float elapsedFrames = match.ResultProgress * Mathf.Max(1f, Mathf.RoundToInt((settings != null ? settings.winSeconds : 2f) * FightFrameTiming.SimulationRate));
                alpha = 1f - Mathf.Clamp01(elapsedFrames / fadeFrames);
            }

            knockoutFade.enabled = alpha > 0.001f;
            knockoutFade.color = new Color(0f, 0f, 0f, Mathf.Clamp01(alpha));
        }

        private static RawImage CreateImage(string name, Transform parent)
        {
            GameObject go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(RawImage));
            go.transform.SetParent(parent, false);
            RawImage image = go.GetComponent<RawImage>();
            image.raycastTarget = false;
            return image;
        }

        private static void Draw(RawImage image, Texture texture, Vector2 size, Vector2 position, float scale, Color color)
        {
            image.enabled = texture != null;
            image.texture = texture;
            image.color = color;
            image.rectTransform.sizeDelta = size;
            image.rectTransform.anchoredPosition = position;
            image.rectTransform.localScale = Vector3.one * scale;
        }
    }
}
