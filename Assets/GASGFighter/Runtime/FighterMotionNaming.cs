using System;
using System.Text.RegularExpressions;

namespace GASG.Fighting
{
    /// <summary>3セグメントのモーション管理番号を一か所で管理します。</summary>
    public static class FighterMotionNaming
    {
        private static readonly Regex Pattern = new Regex(@"\AAS_p([0-9]{2})_([0-9]{3})\z");

        public static bool IsValid(string value)
        {
            return TryParse(value, out _, out _);
        }

        public static bool TryParse(string value, out int character, out int motionNumber)
        {
            character = motionNumber = 0;
            if (value == null) return false;
            Match match = Pattern.Match(value);
            if (!match.Success) return false;
            character = int.Parse(match.Groups[1].Value);
            motionNumber = int.Parse(match.Groups[2].Value);
            return character > 0;
        }

        public static string Format(int character, int motionNumber)
        {
            if (character < 1 || character > 99 || motionNumber < 0 || motionNumber > 999)
                throw new ArgumentOutOfRangeException(nameof(motionNumber), "キャラクターは01〜99、モーション番号は000〜999です。");
            return string.Format(System.Globalization.CultureInfo.InvariantCulture, "AS_p{0:D2}_{1:D3}", character, motionNumber);
        }

        public static string SuggestNext(FighterActionCatalog catalog, int character, int categoryStart)
        {
            if (categoryStart < 0 || categoryStart > 900 || categoryStart % 100 != 0)
                throw new ArgumentOutOfRangeException(nameof(categoryStart), "分類の先頭番号は000、100、200、300、400のいずれかです。");

            int highest = categoryStart - 1;
            if (catalog != null)
            {
                foreach (FighterActionDefinition action in catalog.Actions)
                {
                    if (action == null || !TryParse(action.MotionId, out int foundCharacter, out int motionNumber)) continue;
                    if (foundCharacter == character && motionNumber >= categoryStart && motionNumber < categoryStart + 100)
                        highest = Math.Max(highest, motionNumber);
                }
            }

            if (highest >= categoryStart + 99)
                throw new ArgumentOutOfRangeException(nameof(categoryStart), "この分類の番号は使い切っています。");
            return Format(character, highest + 1);
        }
    }
}