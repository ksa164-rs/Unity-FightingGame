using System;
using UnityEngine;
using UnityEngine.UI;

namespace GASG.Fighting
{
    public sealed partial class FightHud
    {
        public enum ControlBadge { Custom, Classic, Modern }
        public enum DriveAction { Impact, Parry, Rush, CancelRush, Overdrive, Reversal }

        [Serializable]
        public sealed class BattleDisplay
        {
            [Tooltip("空欄ならFighter Configの表示名を使用します。")]
            public string displayName;
            public Sprite portrait;
            [Tooltip("表示のみ。入力方式は変更しません。Customは独自操作を示す「—」です。")]
            public ControlBadge controlType = ControlBadge.Custom;
            [Range(0f, 6f)] public float drive = 6f;
            [Range(0f, 3f)] public float super = 0f;
        }

        [Header("対戦HUD・表示用プロトタイプ")]
        [SerializeField] private BattleDisplay leftDisplay = new BattleDisplay();
        [SerializeField] private BattleDisplay rightDisplay = new BattleDisplay();
        [SerializeField] private Color leftAccent = new Color(1f, 0.18f, 0.58f);
        [SerializeField] private Color rightAccent = new Color(0.15f, 0.5f, 1f);
        [SerializeField] private Color driveColor = new Color(0.78f, 1f, 0.08f);
        [SerializeField] private Color lowDriveColor = new Color(1f, 0.43f, 0.08f);
        [Header("体力ゲージ")]
        [Tooltip("スト6風の低体力警告色です。")]
        [SerializeField] private Color lowHealthColor = new Color(1f, 0.82f, 0.08f);
        [Tooltip("攻撃中に蓄積し、攻撃終了後に減るダメージバーの色です。")]
        [SerializeField] private Color damageBarColor = new Color(1f, 0.62f, 0.12f);
        [SerializeField] private bool showControlGuide = false;
        [Range(0.1f, 2f)] [SerializeField] private float damageTrailSeconds = 0.65f;

        private sealed class BattleView
        {
            public Image Health, Damage, Super;
            public Image[] Drive = new Image[6];
            // 現在のMatchManagerは2先（roundsToWin = 2）なので、勝利マークも2個だけ表示する。
            public Image[] Wins = new Image[2];
            public Text Name, Badge, Level, Burnout;
            public Image Portrait;
            public Text PortraitFallback;
            public float Trail = 1f;
        }
        private BattleView leftView, rightView;

        // 技の実装側から呼ぶ表示用接続口。戦闘リソースの所有権は持たない。
        public void SetResourceDisplay(int playerIndex, float drive, float super)
        {
            if ((playerIndex != 0 && playerIndex != 1) || float.IsNaN(drive) || float.IsNaN(super)
                || float.IsInfinity(drive) || float.IsInfinity(super))
            {
                Debug.LogWarning("[GASG Fighter][スキップ] HUDのプレイヤー番号またはゲージ値が無効です。", this);
                return;
            }
            BattleDisplay display = playerIndex == 0 ? leftDisplay : rightDisplay;
            display.drive = Mathf.Clamp(drive, 0f, 6f);
            display.super = Mathf.Clamp(super, 0f, 3f);
        }

        public static float GetDriveCost(DriveAction action)
        {
            switch (action)
            {
                case DriveAction.Impact: case DriveAction.Rush: return 1f;
                case DriveAction.Parry: return 0.5f;
                case DriveAction.CancelRush: return 3f;
                case DriveAction.Overdrive: case DriveAction.Reversal: return 2f;
                default: throw new ArgumentOutOfRangeException(nameof(action));
            }
        }

        private void CreateBattleLayout(Transform parent)
        {
            leftDisplay = leftDisplay ?? new BattleDisplay();
            rightDisplay = rightDisplay ?? new BattleDisplay();
            leftView = CreateBattleSide(parent, false, leftAccent);
            rightView = CreateBattleSide(parent, true, rightAccent);
            player1HealthFill = leftView.Health;
            player2HealthFill = rightView.Health;
            player1Label = leftView.Name;
            player2Label = rightView.Name;

            Plate("Timer Shadow", parent, 0.471f, 0.882f, 0.529f, 0.986f, new Color(0.06f, 0.025f, 0.10f, 0.94f));
            timerLabel = Label("Round Timer", parent, 0.456f, 0.883f, 0.544f, 0.986f, 72, TextAnchor.MiddleCenter);
            timerLabel.fontStyle = FontStyle.BoldAndItalic;
            centerLabel = Label("Center Message", parent, 0f, 0.43f, 1f, 0.68f, 54, TextAnchor.MiddleCenter);
            controlsLabel = Label("Controls", parent, 0.2f, 0.09f, 0.8f, 0.13f, 20, TextAnchor.MiddleLeft);
            CreateControlIcons(controlsLabel.transform);
            controlsLabel.gameObject.SetActive(showControlGuide);
        }

        private BattleView CreateBattleSide(Transform parent, bool right, Color accent)
        {
            // 各パーツのアンカーを左右反転し、文字やポートレート自体は反転しない。
            BattleView view = new BattleView();
            string prefix = right ? "P2 " : "P1 ";
            Image rail = SidePlate(prefix + "Health Frame", parent, .025f, .924f, .468f, .959f, new Color(.72f, .7f, .76f), right);
            Image bed = Plate("Health Bed", rail.transform, .002f, .1f, .998f, .9f, healthBackground, right);
            view.Damage = Plate("Damage Trail", bed.transform, 0, 0, 1, 1, damageBarColor, right);
            view.Health = Plate("Health Fill", bed.transform, 0, 0, 1, 1, accent, right);
            SidePlate(prefix + "Accent Underline", parent, .025f, .919f, .462f, .923f, accent, right);

            Image portraitFrame = SidePlate(prefix + "Portrait Frame", parent, .025f, .962f, .078f, .996f, accent, right);
            view.Portrait = CreateImage("Portrait", portraitFrame.transform, new Vector2(.03f, .06f), new Vector2(.97f, .94f), new Color(.16f, .13f, .2f));
            view.Portrait.preserveAspect = true;
            view.PortraitFallback = Label("Portrait Fallback", portraitFrame.transform, 0, 0, 1, 1, 20, TextAnchor.MiddleCenter);
            view.PortraitFallback.text = right ? "P2" : "P1";
            view.Name = SideLabel(prefix + "Name", parent, .112f, .960f, .35f, .996f, 24, right);
            Image badge = SidePlate(prefix + "Control Type", parent, .083f, .964f, .104f, .996f, accent, right);
            view.Badge = Label("Control Type Label", badge.transform, 0, 0, 1, 1, 24, TextAnchor.MiddleCenter);

            for (int i = 0; i < view.Wins.Length; i++)
                view.Wins[i] = SidePlate(prefix + "Round " + (i + 1), parent,
                    .421f + i * .015f, .972f, .43f + i * .015f, .983f, new Color(.18f, .17f, .22f), right);

            Image driveFrame = SidePlate(prefix + "Drive Frame", parent, .265f, .881f, .464f, .912f, new Color(.035f, .045f, .025f, .95f), right);
            for (int i = 0; i < 6; i++)
            {
                float start = .016f + i * .162f;
                if (right) start = 1f - start - .151f;
                Image cell = Plate("Drive Cell " + (i + 1), driveFrame.transform, start, .15f, start + .151f, .85f, new Color(.21f, .26f, .06f), right);
                view.Drive[i] = Plate("Drive Fill", cell.transform, 0, 0, 1, 1, driveColor, right);
            }
            view.Burnout = Label("Burnout", driveFrame.transform, 0, -1f, 1, 0, 18, TextAnchor.MiddleCenter);
            view.Burnout.color = lowDriveColor;

            // 下中央の既存フレームメーターに重ならない幅を確保する。
            SidePlate(prefix + "SA Underline", parent, .025f, .024f, .191f, .027f, accent, right);
            Image superBed = SidePlate(prefix + "SA Frame", parent, .056f, .036f, .19f, .065f, new Color(.7f, .65f, .75f), right);
            Image superInner = Plate("SA Bed", superBed.transform, .006f, .14f, .994f, .86f, new Color(.08f, .055f, .13f), right);
            view.Super = Plate("SA Fill", superInner.transform, 0, 0, 1, 1, accent, right);
            view.Level = SideLabel(prefix + "SA Level", parent, .023f, .018f, .06f, .085f, 62, right);
            view.Level.fontStyle = FontStyle.BoldAndItalic;
            Text superCaption = SideLabel(prefix + "SA Caption", parent, .063f, .068f, .19f, .086f, 15, right);
            superCaption.text = "SUPER ART";
            superCaption.color = new Color(.87f, .85f, .92f);
            return view;
        }

        private void UpdateBattleLayout(FighterController p1, FighterController p2)
        {
            UpdateSide(leftView, leftDisplay, p1, matchManager.Player1Rounds, false, leftAccent);
            UpdateSide(rightView, rightDisplay, p2, matchManager.Player2Rounds, true, rightAccent);
            controlsLabel.gameObject.SetActive(showControlGuide);
        }

        private void UpdateSide(BattleView view, BattleDisplay display, FighterController fighter, int wins, bool right, Color accent)
        {
            view.Name.text = !string.IsNullOrWhiteSpace(display.displayName) ? display.displayName :
                fighter.Config != null ? fighter.Config.DisplayName : (right ? "PLAYER 2" : "PLAYER 1");
            view.Badge.text = display.controlType == ControlBadge.Classic ? "C" : display.controlType == ControlBadge.Modern ? "M" : "—";
            view.Portrait.sprite = display.portrait;
            view.Portrait.color = display.portrait != null ? Color.white : new Color(.16f, .13f, .2f);
            view.PortraitFallback.enabled = display.portrait == null;
            float health = Mathf.Clamp01(fighter.CurrentHealth / (float)Mathf.Max(1, fighter.MaxHealth));
            bool opponentIsAttacking = fighter.Opponent != null && fighter.Opponent.State == FighterState.Attacking;
            if (health >= view.Trail)
            {
                // ラウンド開始や回復では残像を即座に同期する。
                view.Trail = health;
            }
            else if (!opponentIsAttacking)
            {
                // 攻撃中に蓄積した被ダメージを、攻撃終了後にまとめて減らす。
                view.Trail = Mathf.MoveTowards(view.Trail, health, Time.deltaTime / Mathf.Max(.1f, damageTrailSeconds));
            }
            SetHealthFill(view.Damage.rectTransform, view.Trail, right);
            view.Health.color = health <= .3f ? lowHealthColor : accent;
            view.Damage.color = damageBarColor;
            for (int i = 0; i < view.Wins.Length; i++)
                view.Wins[i].color = i < wins ? new Color(1f, .86f, .37f) : new Color(.18f, .17f, .22f);
            for (int i = 0; i < 6; i++)
            {
                SetHealthFill(view.Drive[i].rectTransform, Mathf.Clamp01(display.drive - i), right);
                view.Drive[i].color = display.drive <= 1f ? lowDriveColor : driveColor;
            }
            view.Burnout.text = display.drive <= 0f ? "BURNOUT" : string.Empty;
            float sa = Mathf.Clamp(display.super, 0, 3);
            int level = Mathf.FloorToInt(sa);
            view.Level.text = level.ToString();
            view.Level.color = level == 3 ? new Color(1f, .87f, .45f) : Color.white;
            SetHealthFill(view.Super.rectTransform, level == 3 ? 1f : sa - level, right);
        }

        private static Image Plate(string name, Transform parent, float x0, float y0, float x1, float y1, Color color, bool mirror = false)
        {
            GameObject go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(FightHudPlate));
            go.transform.SetParent(parent, false);
            var image = go.GetComponent<FightHudPlate>();
            image.mirror = mirror;
            image.color = color;
            image.raycastTarget = false;
            image.rectTransform.anchorMin = new Vector2(x0, y0);
            image.rectTransform.anchorMax = new Vector2(x1, y1);
            image.rectTransform.offsetMin = image.rectTransform.offsetMax = Vector2.zero;
            return image;
        }

        private static Image SidePlate(string name, Transform parent, float x0, float y0, float x1, float y1, Color color, bool right)
            => Plate(name, parent, right ? 1 - x1 : x0, y0, right ? 1 - x0 : x1, y1, color, right);

        private Text Label(string name, Transform parent, float x0, float y0, float x1, float y1, int size, TextAnchor alignment)
        {
            Text text = CreateText(name, parent, new Vector2(x0, y0), new Vector2(x1, y1), alignment, size);
            text.resizeTextForBestFit = true;
            text.resizeTextMinSize = Mathf.Max(10, size / 2);
            text.resizeTextMaxSize = size;
            return text;
        }

        private Text SideLabel(string name, Transform parent, float x0, float y0, float x1, float y1, int size, bool right)
            => Label(name, parent, right ? 1 - x1 : x0, y0, right ? 1 - x0 : x1, y1, size, right ? TextAnchor.MiddleRight : TextAnchor.MiddleLeft);
    }
}
