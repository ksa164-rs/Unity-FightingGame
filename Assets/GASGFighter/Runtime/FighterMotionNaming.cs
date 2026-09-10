using System;
using System.Text.RegularExpressions;

namespace GASG.Fighting
{
    /// <summary>モーション番号の形式と採番を一か所で管理します。</summary>
    public static class FighterMotionNaming
    {
        private static readonly Regex Pattern = new Regex(@"\AAS_p([0-9]{2})_([0-9]{3})_([0-9]{3})\z");

        public static bool IsValid(string value)
        {
            return TryParse(value, out _, out _, out _);
        }

        public static bool TryParse(string value, out int character, out int category, out int sequence)
        {
            character = category = sequence = 0;
            if (value == null) return false;
            Match match = Pattern.Match(value);
            if (!match.Success) return false;
            character = int.Parse(match.Groups[1].Value);
            category = int.Parse(match.Groups[2].Value);
            sequence = int.Parse(match.Groups[3].Value);
            return character > 0 && sequence > 0;
        }

        public static string Format(int character, int category, int sequence)
        {
            if (character < 1 || character > 99 || category < 0 || category > 999 || sequence < 1 || sequence > 999)
                throw new ArgumentOutOfRangeException(nameof(sequence), "キャラクターは01〜99、分類は000〜999、連番は001〜999です。");
            return string.Format(System.Globalization.CultureInfo.InvariantCulture, "AS_p{0:D2}_{1:D3}_{2:D3}", character, category, sequence);
        }

        public static string SuggestNext(FighterActionCatalog catalog, int character, int category)
        {
            int highest = 0;
            if (catalog != null)
            {
                foreach (FighterActionDefinition action in catalog.Actions)
                {
                    if (action == null) continue;
                    // 移行済み番号と、番号形式の内部IDの両方を予約済みとして扱う。
                    foreach (string id in new[] { action.MotionId, action.ActionId })
                    {
                        if (TryParse(id, out int c, out int g, out int s) && c == character && g == category)
                            highest = Math.Max(highest, s);
                    }
                }
            }
            return Format(character, category, highest + 1);
        }
    }
}
