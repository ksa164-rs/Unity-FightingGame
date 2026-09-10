using UnityEngine;

namespace GASG.Fighting
{
    [CreateAssetMenu(menuName = "GASG/Fighting/Round Intro Settings")]
    public sealed class FightIntroSettings : ScriptableObject
    {
        public const string ResourcePath = "FightIntroSettings";

        [Header("開始画像（配列の先頭がROUND01）")]
        public Texture2D[] roundImages;
        public Texture2D fightImage;

        [Header("決着・最終ラウンド（画像未指定時は文字を描画）")]
        public Texture2D finalRoundImage;
        public Texture2D knockoutImage;
        public Texture2D winImage;
        [Min(0.5f)] public float finalRoundSeconds = 1.65f;
        [Min(0.2f)] public float knockoutSeconds = 1.25f;
        [Min(0.1f)] public float resultPauseSeconds = 1.5f;
        [Min(0.5f)] public float winSeconds = 2f;
        [Range(200f, 1600f)] public float finalRoundWidth = 780f;
        [Range(200f, 1600f)] public float knockoutWidth = 700f;
        [Range(100f, 1200f)] public float winWidth = 500f;
        [Range(1f, 4f)] public float knockoutStartScale = 2.5f;
        [Range(40, 200)] public int winnerNameFontSize = 100;
        public Vector2 winnerOffset = new Vector2(0f, -40f);

        [Header("表示時間（秒・試合の60Hz進行と同期）")]
        [Min(0.5f)] public float roundSeconds = 1.65f;
        [Min(0.2f)] public float fightSeconds = 1.1f;

        [Header("1920×1080を基準とした表示サイズ")]
        [Range(200f, 1600f)] public float roundWidth = 780f;
        [Range(200f, 1600f)] public float fightWidth = 1050f;
        public Vector2 centerOffset = Vector2.zero;

        [Header("出現・退場（各表示時間に対する割合）")]
        [Range(0.05f, 0.4f)] public float entranceFraction = 0.18f;
        [Range(0.05f, 0.4f)] public float exitFraction = 0.16f;
        [Range(1f, 2.5f)] public float fightStartScale = 1.55f;
        [Range(0f, 500f)] public float roundSlideDistance = 160f;
        [Range(0f, 1000f)] public float exitSlideDistance = 300f;

        [Header("色の残像・背景")]
        public Color leftEchoColor = new Color(0f, 0.85f, 1f, 0.45f);
        public Color rightEchoColor = new Color(0.8f, 0.05f, 1f, 0.45f);
        [Range(0f, 100f)] public float echoDistance = 35f;
        [Range(0f, 0.5f)] public float backdropOpacity = 0.08f;

        [Header("描画順（通常HUD・キャラクターより前面）")]
        [Range(1, 32767)] public int sortingOrder = 32000;

        public Texture2D GetRoundImage(int round)
        {
            return roundImages != null && round > 0 && round <= roundImages.Length
                ? roundImages[round - 1] : null;
        }
    }
}
