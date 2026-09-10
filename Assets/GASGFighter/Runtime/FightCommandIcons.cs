using System.Collections.Generic;
using UnityEngine;

namespace GASG.Fighting
{
    // UIとInspectorで同じ画像を共有する。初回のみ読み込み、以後はキャッシュする。
    public static class FightCommandIcons
    {
        public const string ResourceFolder = "CommandIcons/";
        private static readonly Dictionary<string, Texture2D> Cache = new Dictionary<string, Texture2D>();
        private static readonly string[] DirectionNames =
        {
            "", "dir_1_down_left", "dir_2_down", "dir_3_down_right", "dir_4_left",
            "dir_5_neutral_N", "dir_6_right", "dir_7_up_left", "dir_8_up", "dir_9_up_right"
        };

        public static Texture2D Direction(FighterInputDirection direction)
        {
            int index = (int)direction;
            return index >= 1 && index <= 9 ? Load(DirectionNames[index]) : null;
        }

        public static Texture2D Attack(FighterAttackButton button)
        {
            switch (button)
            {
                case FighterAttackButton.Light: return Load("attack_blue");
                case FighterAttackButton.Medium: return Load("attack_yellow");
                case FighterAttackButton.Heavy: return Load("attack_red");
                case FighterAttackButton.Special: return Load("attack_SP");
                // 現在の投げは専用ボタン。弱＋中の入力とは扱わない。
                default: return null;
            }
        }

        public static Texture2D Assist => Load("assist_AUTO");

        private static Texture2D Load(string name)
        {
            if (!Cache.TryGetValue(name, out Texture2D texture))
            {
                texture = Resources.Load<Texture2D>(ResourceFolder + name);
                Cache[name] = texture;
                if (texture == null) Debug.LogError($"[GASG Fighter][失敗] コマンド画像がありません: {name}");
            }
            return texture;
        }
    }
}
