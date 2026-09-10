using UnityEngine;
using UnityEngine.UI;

namespace GASG.Fighting
{
    [DisallowMultipleComponent]
    public sealed partial class FightHud : MonoBehaviour
    {
        [Header("参照")]
        [SerializeField] private FightMatchManager matchManager;

        [Header("コマンドアイコン")]
        [Range(24f, 56f)] [SerializeField] private float commandIconSize = 36f;

        [Header("左右の入力履歴")]
        [SerializeField] private bool showInputHistory = true;
        [Range(1, 20)] [SerializeField] private int historyRows = 14;
        [Range(20f, 56f)] [SerializeField] private float historyIconSize = 36f;
        [Range(0f, 20f)] [SerializeField] private float historyRowSpacing = 6f;
        [Range(0f, 160f)] [SerializeField] private float historyEdgeMargin = 28f;
        [Range(0.15f, 0.4f)] [SerializeField] private float historyTopOffset = 0.18f;
        [SerializeField] private bool showHistoryFrames = true;

        private sealed class HistoryRow
        {
            public RectTransform Root;
            public Image Background;
            public Text Frames;
            public RawImage Direction;
            public RawImage[] Buttons = new RawImage[4];
            public Text Throw;
            public int LastFrames = -1;
        }
        private HistoryRow[] leftHistory;
        private HistoryRow[] rightHistory;

        [Header("表示色")]
        [SerializeField] private Color player1Color = new Color(0.15f, 0.65f, 1f);
        [SerializeField] private Color player2Color = new Color(1f, 0.28f, 0.18f);
        [SerializeField] private Color healthBackground = new Color(0.08f, 0.08f, 0.08f, 0.92f);

        private const string RuntimeCanvasName = "Fight HUD Canvas (Runtime)";
        private const float ReferenceWidth = 1920f;
        private const float ReferenceHeight = 1080f;

        private Canvas hudCanvas;
        private Image player1HealthFill;
        private Image player2HealthFill;
        private Text player1Label;
        private Text player2Label;
        private Text timerLabel;
        private Text centerLabel;
        private Text controlsLabel;
        private Font runtimeFont;
        private FightIntroPresentation introPresentation;

        private void Awake()
        {
            CreateHud();
        }

        private void LateUpdate()
        {
            if (matchManager == null || matchManager.Player1 == null || matchManager.Player2 == null)
            {
                SetHudVisible(false);
                return;
            }

            if (hudCanvas == null)
            {
                CreateHud();
            }

            if (hudCanvas == null)
            {
                return;
            }

            SetHudVisible(true);
            UpdateHudValues();
        }

        private void CreateHud()
        {
            if (hudCanvas != null)
            {
                return;
            }

            runtimeFont = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            if (runtimeFont == null)
            {
                Debug.LogError("[GASG Fighter][失敗] HUD用のビルトインフォントを取得できませんでした。", this);
                return;
            }

            GameObject canvasObject = new GameObject(
                RuntimeCanvasName,
                typeof(RectTransform),
                typeof(Canvas),
                typeof(CanvasScaler));
            canvasObject.transform.SetParent(transform, false);

            hudCanvas = canvasObject.GetComponent<Canvas>();
            // 対戦HUDは3D空間の奥行きに影響されない画面固定表示にする。
            hudCanvas.renderMode = RenderMode.ScreenSpaceOverlay;

            CanvasScaler scaler = canvasObject.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(ReferenceWidth, ReferenceHeight);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 0.5f;

            CreateBattleLayout(canvasObject.transform);
            leftHistory = CreateHistoryRows(canvasObject.transform, "P1");
            rightHistory = CreateHistoryRows(canvasObject.transform, "P2");

            Debug.Log("[GASG Fighter][成功] 画面固定表示のHUD Canvasを作成しました。", this);
        }

        private void CreateControlIcons(Transform parent)
        {
            HorizontalLayoutGroup layout = parent.gameObject.AddComponent<HorizontalLayoutGroup>();
            layout.spacing = 10f;
            layout.childAlignment = TextAnchor.MiddleLeft;
            layout.childControlWidth = false;
            layout.childControlHeight = false;
            layout.childForceExpandWidth = false;
            layout.childForceExpandHeight = false;
            string[] bindings = { "□ / J", "✕ / K", "○ / L", "△ / I" };
            for (int i = 0; i < bindings.Length; i++)
            {
                Texture2D texture = FightCommandIcons.Attack((FighterAttackButton)i);
                if (texture != null)
                {
                    GameObject iconObject = new GameObject("Command " + (FighterAttackButton)i, typeof(RectTransform), typeof(CanvasRenderer), typeof(RawImage));
                    iconObject.transform.SetParent(parent, false);
                    RawImage icon = iconObject.GetComponent<RawImage>();
                    icon.texture = texture;
                    icon.raycastTarget = false;
                    icon.rectTransform.sizeDelta = Vector2.one * commandIconSize;
                }
                Text binding = CreateText("Button Binding", parent, Vector2.zero, Vector2.zero, TextAnchor.MiddleLeft, 20);
                binding.text = texture != null ? bindings[i] : $"{(FighterAttackButton)i}: {bindings[i]}";
                binding.rectTransform.sizeDelta = new Vector2(texture != null ? 92f : 180f, 40f);
            }
            Text throwBinding = CreateText("Throw Binding", parent, Vector2.zero, Vector2.zero, TextAnchor.MiddleLeft, 20);
            throwBinding.text = "Throw: L1 / U    PS5 / Keyboard P1";
            throwBinding.rectTransform.sizeDelta = new Vector2(540f, 40f);
        }

        private void UpdateHudValues()
        {
            FighterController player1 = matchManager.Player1;
            FighterController player2 = matchManager.Player2;

            SetHealthFill(player1HealthFill.rectTransform, player1.CurrentHealth / (float)player1.MaxHealth, false);
            SetHealthFill(player2HealthFill.rectTransform, player2.CurrentHealth / (float)player2.MaxHealth, true);

            UpdateBattleLayout(player1, player2);

            timerLabel.text = matchManager.DisplayTimeSeconds.ToString("00");
            centerLabel.text = matchManager.CenterMessage;
            // MatchManagerのAwake完了後に設定を取得し、既存Sceneへの手作業の追加を不要にする。
            if (introPresentation == null)
                introPresentation = new FightIntroPresentation(transform, runtimeFont, matchManager.IntroSettings);
            bool introVisible = introPresentation.Refresh(matchManager);
            centerLabel.enabled = !introVisible && !string.IsNullOrEmpty(matchManager.CenterMessage);
            UpdateHistoryRows(leftHistory, player1.InputSource, false);
            UpdateHistoryRows(rightHistory, player2.InputSource, true);
        }

        private HistoryRow[] CreateHistoryRows(Transform parent, string player)
        {
            // 表示行は最初に確保し、入力のたびにGameObjectを生成しない。
            HistoryRow[] rows = new HistoryRow[20];
            for (int i = 0; i < rows.Length; i++)
            {
                Image background = CreateImage(player + " Input History " + i, parent,
                    Vector2.zero, Vector2.zero, Color.clear);
                HistoryRow row = new HistoryRow { Root = background.rectTransform, Background = background };
                row.Frames = CreateText("Frames", row.Root, Vector2.zero, Vector2.zero, TextAnchor.MiddleRight, 22);
                row.Direction = CreateHistoryIcon("Direction", row.Root);
                for (int button = 0; button < 4; button++)
                {
                    row.Buttons[button] = CreateHistoryIcon(((FighterAttackButton)button).ToString(), row.Root);
                    row.Buttons[button].texture = FightCommandIcons.Attack((FighterAttackButton)button);
                }
                row.Throw = CreateText("Throw", row.Root, Vector2.zero, Vector2.zero, TextAnchor.MiddleCenter, 17);
                row.Throw.text = "TH";
                row.Root.gameObject.SetActive(false);
                rows[i] = row;
            }
            return rows;
        }

        private static RawImage CreateHistoryIcon(string name, Transform parent)
        {
            GameObject iconObject = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(RawImage));
            iconObject.transform.SetParent(parent, false);
            RawImage icon = iconObject.GetComponent<RawImage>();
            icon.raycastTarget = false;
            return icon;
        }

        private static void PlaceHistoryElement(RectTransform rect, float x, float width, float height)
        {
            rect.anchorMin = rect.anchorMax = new Vector2(0f, 0.5f);
            rect.pivot = new Vector2(0f, 0.5f);
            rect.anchoredPosition = new Vector2(x, 0f);
            rect.sizeDelta = new Vector2(width, height);
        }

        private void UpdateHistoryRows(HistoryRow[] rows, FighterInputSource source, bool right)
        {
            if (rows == null) return;
            FightDisplayHistory history = source != null ? source.DisplayHistory : null;
            RectTransform canvasRect = (RectTransform)hudCanvas.transform;
            float size = Mathf.Clamp(historyIconSize, 20f, 56f);
            float spacing = Mathf.Clamp(historyRowSpacing, 0f, 20f);
            float top = Mathf.Clamp(historyTopOffset, 0.15f, 0.4f);
            // 低い画面でも下部の操作ガイドに重ならない行数まで表示する。
            int fittingRows = Mathf.Max(0, Mathf.FloorToInt(canvasRect.rect.height * (0.9f - top) / (size + spacing)));
            int visibleRows = Mathf.Min(Mathf.Clamp(historyRows, 1, rows.Length), fittingRows);
            for (int i = 0; i < rows.Length; i++)
            {
                HistoryRow row = rows[i];
                bool visible = showInputHistory && history != null && i < history.Count && i < visibleRows;
                if (row.Root.gameObject.activeSelf != visible) row.Root.gameObject.SetActive(visible);
                if (!visible) continue;
                FightDisplayHistory.Entry entry = history.GetNewest(i);
                row.Root.anchorMin = row.Root.anchorMax = new Vector2(right ? 1f : 0f, 1f - top);
                row.Root.pivot = new Vector2(right ? 1f : 0f, 1f);
                row.Root.anchoredPosition = new Vector2((right ? -1f : 1f) * Mathf.Max(0f, historyEdgeMargin), -i * (size + spacing));
                Color tint = right ? player2Color : player1Color;
                tint.a = i == 0 ? 0.22f : 0.06f;
                row.Background.color = tint;
                float x = 6f;
                row.Frames.gameObject.SetActive(showHistoryFrames);
                if (showHistoryFrames)
                {
                    if (row.LastFrames != entry.Frames)
                    {
                        row.Frames.text = entry.Frames.ToString();
                        row.LastFrames = entry.Frames;
                    }
                    PlaceHistoryElement(row.Frames.rectTransform, x, 62f, size);
                    x += 68f;
                }
                bool hasDirection = entry.Direction != 0;
                row.Direction.gameObject.SetActive(hasDirection);
                if (hasDirection)
                {
                    row.Direction.texture = FightCommandIcons.Direction((FighterInputDirection)entry.Direction);
                    PlaceHistoryElement(row.Direction.rectTransform, x, size, size);
                    x += size + 3f;
                }
                for (int button = 0; button < 4; button++)
                {
                    bool pressed = (entry.Buttons & (1 << button)) != 0;
                    row.Buttons[button].gameObject.SetActive(pressed);
                    if (!pressed) continue;
                    PlaceHistoryElement(row.Buttons[button].rectTransform, x, size, size);
                    x += size + 3f;
                }
                bool throwing = (entry.Buttons & 16) != 0;
                row.Throw.gameObject.SetActive(throwing);
                if (throwing)
                {
                    PlaceHistoryElement(row.Throw.rectTransform, x, size, size);
                    x += size + 3f;
                }
                // 右側も方向アイコンは反転せず、実際に入力した画面方向を示す。
                row.Root.sizeDelta = new Vector2(x + 6f, size);
                if (right)
                {
                    // フレーム数を外側、ボタンを内側に並べ、方向の列を固定する。
                    for (int child = 0; child < row.Root.childCount; child++)
                    {
                        RectTransform element = (RectTransform)row.Root.GetChild(child);
                        if (!element.gameObject.activeSelf) continue;
                        element.anchoredPosition = new Vector2(x + 6f - element.anchoredPosition.x - element.sizeDelta.x, 0f);
                    }
                }
            }
        }

        private static void SetHealthFill(RectTransform fill, float normalizedHealth, bool fillFromRight)
        {
            float health = Mathf.Clamp01(normalizedHealth);
            fill.anchorMin = fillFromRight ? new Vector2(1f - health, 0f) : Vector2.zero;
            fill.anchorMax = fillFromRight ? Vector2.one : new Vector2(health, 1f);
            fill.offsetMin = Vector2.zero;
            fill.offsetMax = Vector2.zero;
        }

        private static Image CreateImage(
            string objectName,
            Transform parent,
            Vector2 anchorMin,
            Vector2 anchorMax,
            Color color)
        {
            GameObject imageObject = new GameObject(objectName, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            imageObject.transform.SetParent(parent, false);

            RectTransform rectTransform = imageObject.GetComponent<RectTransform>();
            rectTransform.anchorMin = anchorMin;
            rectTransform.anchorMax = anchorMax;
            rectTransform.offsetMin = Vector2.zero;
            rectTransform.offsetMax = Vector2.zero;

            Image image = imageObject.GetComponent<Image>();
            image.color = color;
            image.raycastTarget = false;
            return image;
        }

        private Text CreateText(
            string objectName,
            Transform parent,
            Vector2 anchorMin,
            Vector2 anchorMax,
            TextAnchor alignment,
            int fontSize)
        {
            GameObject textObject = new GameObject(objectName, typeof(RectTransform), typeof(CanvasRenderer), typeof(Text));
            textObject.transform.SetParent(parent, false);

            RectTransform rectTransform = textObject.GetComponent<RectTransform>();
            rectTransform.anchorMin = anchorMin;
            rectTransform.anchorMax = anchorMax;
            rectTransform.offsetMin = Vector2.zero;
            rectTransform.offsetMax = Vector2.zero;

            Text text = textObject.GetComponent<Text>();
            text.font = runtimeFont;
            text.fontSize = fontSize;
            text.fontStyle = FontStyle.Bold;
            text.alignment = alignment;
            text.color = Color.white;
            text.horizontalOverflow = HorizontalWrapMode.Wrap;
            text.verticalOverflow = VerticalWrapMode.Overflow;
            text.raycastTarget = false;
            return text;
        }

        private void SetHudVisible(bool visible)
        {
            if (!visible) introPresentation?.SetVisible(false);
            if (hudCanvas != null && hudCanvas.gameObject.activeSelf != visible)
            {
                hudCanvas.gameObject.SetActive(visible);
            }
        }

        private void OnDisable()
        {
            SetHudVisible(false);
        }

#if UNITY_EDITOR
        public void EditorConfigure(FightMatchManager newMatchManager)
        {
            matchManager = newMatchManager;
        }
#endif
    }
}

