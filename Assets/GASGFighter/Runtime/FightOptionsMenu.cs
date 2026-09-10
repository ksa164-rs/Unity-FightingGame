using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

namespace GASG.Fighting
{
    /// <summary>
    /// 対戦HUDとは別Canvasで動作する、トレーニング補助付きオプションメニューです。
    /// </summary>
    [DefaultExecutionOrder(200)]
    [DisallowMultipleComponent]
    public sealed class FightOptionsMenu : MonoBehaviour
    {
        private const string RuntimeCanvasName = "Fight Options Canvas (Runtime)";
        private const float ReferenceWidth = 1920f;
        private const float ReferenceHeight = 1080f;

        [Header("参照")]
        [SerializeField] private FightMatchManager matchManager;

        [Header("初期表示")]
        [SerializeField] private bool showFrameMeter = true;

        [Header("表示色")]
        [SerializeField] private Color panelColor = new Color(0.025f, 0.035f, 0.06f, 0.94f);
        [SerializeField] private Color buttonColor = new Color(0.10f, 0.16f, 0.25f, 0.98f);
        [SerializeField] private Color startupColor = new Color(0.20f, 0.55f, 1f, 1f);
        [SerializeField] private Color activeColor = new Color(1f, 0.24f, 0.16f, 1f);
        [SerializeField] private Color recoveryColor = new Color(1f, 0.72f, 0.12f, 1f);

        private Canvas optionsCanvas;
        private GameObject menuRoot;
        private GameObject frameMeterRoot;
        private Text frameMeterToggleLabel;
        private Text inputAssignmentLabel;
        private Text menuHintLabel;
        private FrameMeterView player1FrameMeter;
        private FrameMeterView player2FrameMeter;
        private Font runtimeFont;
        private bool menuOpen;

        private void Awake()
        {
            if (matchManager == null)
            {
                matchManager = FindFirstObjectByType<FightMatchManager>();
            }

            CreateInterface();
            ApplyVisibility();
        }

        private void Update()
        {
            Keyboard keyboard = Keyboard.current;
            if (keyboard != null &&
                (keyboard.f1Key.wasPressedThisFrame || keyboard.escapeKey.wasPressedThisFrame))
            {
                SetMenuOpen(!menuOpen);
            }
        }

        private void LateUpdate()
        {
            if (matchManager == null)
            {
                return;
            }

            if (showFrameMeter)
            {
                UpdateFrameMeter(player1FrameMeter, "P1", matchManager.Player1);
                UpdateFrameMeter(player2FrameMeter, "P2", matchManager.Player2);
            }
        }

        private void OnDisable()
        {
            if (menuOpen)
            {
                matchManager?.SetOptionsPaused(false);
            }
        }

        private void CreateInterface()
        {
            if (optionsCanvas != null)
            {
                return;
            }

            runtimeFont = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            if (runtimeFont == null)
            {
                Debug.LogError("[GASG Fighter][失敗] Options Menu用フォントを取得できませんでした。", this);
                return;
            }

            GameObject canvasObject = new GameObject(
                RuntimeCanvasName,
                typeof(RectTransform),
                typeof(Canvas),
                typeof(CanvasScaler),
                typeof(GraphicRaycaster));
            canvasObject.transform.SetParent(transform, false);

            optionsCanvas = canvasObject.GetComponent<Canvas>();
            optionsCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
            optionsCanvas.sortingOrder = 1000;

            CanvasScaler scaler = canvasObject.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(ReferenceWidth, ReferenceHeight);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 0.5f;

            EnsureEventSystem();
            CreateMenu(canvasObject.transform);
            CreateFrameMeters(canvasObject.transform);

            menuHintLabel = CreateText(
                "Options Hint",
                canvasObject.transform,
                new Vector2(0.77f, 0.885f),
                new Vector2(0.975f, 0.915f),
                TextAnchor.MiddleRight,
                18);
            menuHintLabel.text = "F1 / ESC : OPTIONS";

            Debug.Log("[GASG Fighter][成功] 独立Options Menuとトレーニング表示を作成しました。", this);
        }

        private void CreateMenu(Transform canvasTransform)
        {
            Image dimmer = CreateImage(
                "Options Dimmer",
                canvasTransform,
                Vector2.zero,
                Vector2.one,
                new Color(0f, 0f, 0f, 0.58f),
                true);
            menuRoot = dimmer.gameObject;

            Image panel = CreateImage(
                "Options Panel",
                menuRoot.transform,
                new Vector2(0.31f, 0.18f),
                new Vector2(0.69f, 0.82f),
                panelColor,
                true);

            Text title = CreateText(
                "Title",
                panel.transform,
                new Vector2(0.08f, 0.82f),
                new Vector2(0.92f, 0.96f),
                TextAnchor.MiddleCenter,
                38);
            title.text = "OPTIONS / TRAINING";

            inputAssignmentLabel = CreateText(
                "Input Assignment",
                panel.transform,
                new Vector2(0.08f, 0.72f),
                new Vector2(0.92f, 0.82f),
                TextAnchor.MiddleCenter,
                18);

            frameMeterToggleLabel = CreateButton(
                "Frame Meter Toggle",
                panel.transform,
                new Vector2(0.12f, 0.60f),
                new Vector2(0.88f, 0.69f),
                ToggleFrameMeter);
            CreateButton(
                "Swap Input Devices",
                panel.transform,
                new Vector2(0.12f, 0.48f),
                new Vector2(0.88f, 0.57f),
                SwapInputDevices).text = "SWAP P1 / P2 INPUT";
            CreateButton(
                "Reset Match",
                panel.transform,
                new Vector2(0.12f, 0.36f),
                new Vector2(0.88f, 0.45f),
                ResetMatch).text = "RESET MATCH";
            CreateButton(
                "Swap Positions",
                panel.transform,
                new Vector2(0.12f, 0.24f),
                new Vector2(0.88f, 0.33f),
                SwapPositions).text = "SWAP POSITIONS";
            CreateButton(
                "Close Options",
                panel.transform,
                new Vector2(0.12f, 0.04f),
                new Vector2(0.88f, 0.16f),
                () => SetMenuOpen(false)).text = "CLOSE";
        }

        private void CreateFrameMeters(Transform canvasTransform)
        {
            frameMeterRoot = new GameObject("Frame Meter Overlay", typeof(RectTransform));
            frameMeterRoot.transform.SetParent(canvasTransform, false);
            Stretch(frameMeterRoot.GetComponent<RectTransform>());

            player1FrameMeter = CreateFrameMeter(
                "P1 Frame Meter",
                frameMeterRoot.transform,
                new Vector2(0.20f, 0.015f),
                new Vector2(0.49f, 0.072f));
            player2FrameMeter = CreateFrameMeter(
                "P2 Frame Meter",
                frameMeterRoot.transform,
                new Vector2(0.51f, 0.015f),
                new Vector2(0.80f, 0.072f));
        }

        private FrameMeterView CreateFrameMeter(
            string objectName,
            Transform parent,
            Vector2 anchorMin,
            Vector2 anchorMax)
        {
            Image panel = CreateImage(objectName, parent, anchorMin, anchorMax, panelColor, false);
            Text label = CreateText(
                "Label",
                panel.transform,
                new Vector2(0.02f, 0.48f),
                new Vector2(0.98f, 0.98f),
                TextAnchor.MiddleLeft,
                17);

            RectTransform barRoot = CreateImage(
                "Bar",
                panel.transform,
                new Vector2(0.02f, 0.10f),
                new Vector2(0.98f, 0.42f),
                new Color(0.05f, 0.05f, 0.06f, 1f),
                false).rectTransform;

            Image startup = CreateImage("Startup", barRoot, Vector2.zero, Vector2.one, startupColor, false);
            Image active = CreateImage("Active", barRoot, Vector2.zero, Vector2.one, activeColor, false);
            Image recovery = CreateImage("Recovery", barRoot, Vector2.zero, Vector2.one, recoveryColor, false);
            Image marker = CreateImage("Current Frame", barRoot, Vector2.zero, Vector2.one, Color.white, false);

            return new FrameMeterView(label, startup, active, recovery, marker.rectTransform);
        }

        private void ToggleFrameMeter()
        {
            showFrameMeter = !showFrameMeter;
            ApplyVisibility();
        }

        private void ResetMatch()
        {
            matchManager?.ResetMatch();
        }

        private void SwapPositions()
        {
            matchManager?.SwapPlayerPositions();
        }

        private void SwapInputDevices()
        {
            if (matchManager != null && matchManager.SwapPlayerInputDevices())
            {
                UpdateInputAssignmentLabel();
            }
        }

        private void SetMenuOpen(bool open)
        {
            menuOpen = open;
            if (menuOpen)
            {
                UpdateInputAssignmentLabel();
            }

            if (menuRoot != null)
            {
                menuRoot.SetActive(menuOpen);
            }

            if (menuHintLabel != null)
            {
                menuHintLabel.enabled = !menuOpen;
            }

            matchManager?.SetOptionsPaused(menuOpen);
        }

        private void ApplyVisibility()
        {
            if (frameMeterRoot != null)
            {
                frameMeterRoot.SetActive(showFrameMeter);
            }

            if (frameMeterToggleLabel != null)
            {
                frameMeterToggleLabel.text = $"FRAME METER : {(showFrameMeter ? "ON" : "OFF")}";
            }

            if (menuRoot != null)
            {
                menuRoot.SetActive(menuOpen);
            }

            UpdateInputAssignmentLabel();
        }

        private void UpdateInputAssignmentLabel()
        {
            if (inputAssignmentLabel == null || matchManager == null)
            {
                return;
            }

            FighterInputSource player1Input = matchManager.Player1 != null ? matchManager.Player1.InputSource : null;
            FighterInputSource player2Input = matchManager.Player2 != null ? matchManager.Player2.InputSource : null;
            string player1Name = player1Input != null ? player1Input.AssignedDeviceName : "NO INPUT";
            string player2Name = player2Input != null ? player2Input.AssignedDeviceName : "NO INPUT";
            inputAssignmentLabel.text = $"P1 : {player1Name}    /    P2 : {player2Name}";
        }

        private static void UpdateFrameMeter(FrameMeterView view, string playerLabel, FighterController fighter)
        {
            if (view == null || fighter == null)
            {
                return;
            }

            FighterAttackDefinition attack = fighter.CurrentAttack;
            if (attack == null || attack.TotalFrames <= 0)
            {
                view.Label.text = $"{playerLabel}  {fighter.State.ToString().ToUpperInvariant()}";
                view.SetBarsVisible(false);
                return;
            }

            int frame = Mathf.Clamp(fighter.CurrentAttackFrame, 0, attack.TotalFrames - 1);
            string phase = frame < attack.StartupFrames
                ? "STARTUP"
                : frame < attack.StartupFrames + attack.ActiveFrames
                    ? "ACTIVE"
                    : "RECOVERY";
            view.Label.text = $"{playerLabel}  {attack.ActionId}  {frame + 1:00}/{attack.TotalFrames:00}  {phase}";
            view.SetBarsVisible(true);

            float total = attack.TotalFrames;
            float startupEnd = attack.StartupFrames / total;
            float activeEnd = (attack.StartupFrames + attack.ActiveFrames) / total;
            SetHorizontalRange(view.Startup.rectTransform, 0f, startupEnd);
            SetHorizontalRange(view.Active.rectTransform, startupEnd, activeEnd);
            SetHorizontalRange(view.Recovery.rectTransform, activeEnd, 1f);

            float markerPosition = (frame + 0.5f) / total;
            SetHorizontalRange(view.Marker, markerPosition - 0.003f, markerPosition + 0.003f);
        }

        private void EnsureEventSystem()
        {
            EventSystem eventSystem = FindFirstObjectByType<EventSystem>();
            if (eventSystem != null)
            {
                return;
            }

            GameObject eventSystemObject = new GameObject(
                "Fight Options EventSystem (Runtime)",
                typeof(EventSystem));
            eventSystemObject.transform.SetParent(transform, false);
            InputSystemUIInputModule inputModule = eventSystemObject.AddComponent<InputSystemUIInputModule>();
            inputModule.AssignDefaultActions();
        }

        private Text CreateButton(
            string objectName,
            Transform parent,
            Vector2 anchorMin,
            Vector2 anchorMax,
            UnityEngine.Events.UnityAction onClick)
        {
            Image background = CreateImage(objectName, parent, anchorMin, anchorMax, buttonColor, true);
            Button button = background.gameObject.AddComponent<Button>();
            button.targetGraphic = background;
            button.onClick.AddListener(onClick);

            ColorBlock colors = button.colors;
            colors.highlightedColor = Color.Lerp(buttonColor, Color.white, 0.18f);
            colors.pressedColor = Color.Lerp(buttonColor, Color.black, 0.22f);
            button.colors = colors;

            return CreateText(
                "Label",
                background.transform,
                new Vector2(0.03f, 0.05f),
                new Vector2(0.97f, 0.95f),
                TextAnchor.MiddleCenter,
                24);
        }

        private static Image CreateImage(
            string objectName,
            Transform parent,
            Vector2 anchorMin,
            Vector2 anchorMax,
            Color color,
            bool raycastTarget)
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
            image.raycastTarget = raycastTarget;
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
            text.horizontalOverflow = HorizontalWrapMode.Overflow;
            text.verticalOverflow = VerticalWrapMode.Overflow;
            text.raycastTarget = false;
            return text;
        }

        private static void Stretch(RectTransform rectTransform)
        {
            rectTransform.anchorMin = Vector2.zero;
            rectTransform.anchorMax = Vector2.one;
            rectTransform.offsetMin = Vector2.zero;
            rectTransform.offsetMax = Vector2.zero;
        }

        private static void SetHorizontalRange(RectTransform rectTransform, float minimum, float maximum)
        {
            rectTransform.anchorMin = new Vector2(Mathf.Clamp01(minimum), 0f);
            rectTransform.anchorMax = new Vector2(Mathf.Clamp01(maximum), 1f);
            rectTransform.offsetMin = Vector2.zero;
            rectTransform.offsetMax = Vector2.zero;
        }

#if UNITY_EDITOR
        public void EditorConfigure(FightMatchManager newMatchManager)
        {
            matchManager = newMatchManager;
        }
#endif

        private sealed class FrameMeterView
        {
            public FrameMeterView(Text label, Image startup, Image active, Image recovery, RectTransform marker)
            {
                Label = label;
                Startup = startup;
                Active = active;
                Recovery = recovery;
                Marker = marker;
            }

            public Text Label { get; }
            public Image Startup { get; }
            public Image Active { get; }
            public Image Recovery { get; }
            public RectTransform Marker { get; }

            public void SetBarsVisible(bool visible)
            {
                Startup.enabled = visible;
                Active.enabled = visible;
                Recovery.enabled = visible;
                Marker.gameObject.SetActive(visible);
            }
        }
    }
}

