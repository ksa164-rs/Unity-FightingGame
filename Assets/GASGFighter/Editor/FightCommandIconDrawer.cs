using UnityEditor;
using UnityEngine;

namespace GASG.Fighting.Editor
{
    // 数値の保存形式を保ちながら、選択と表示を画像に置き換える。
    [CustomPropertyDrawer(typeof(FighterInputDirection))]
    [CustomPropertyDrawer(typeof(FighterAttackButton))]
    public sealed class FightCommandIconDrawer : PropertyDrawer
    {
        public override float GetPropertyHeight(SerializedProperty property, GUIContent label) => 34f;

        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            EditorGUI.BeginProperty(position, label, property);
            Rect field = EditorGUI.PrefixLabel(position, label);
            bool direction = property.type == nameof(FighterInputDirection);
            Texture2D texture = direction
                ? FightCommandIcons.Direction((FighterInputDirection)property.intValue)
                : FightCommandIcons.Attack((FighterAttackButton)property.intValue);
            Rect icon = new Rect(field.x, field.y, 32f, 32f);
            if (texture != null) GUI.DrawTexture(icon, texture, ScaleMode.ScaleToFit, true);
            Rect selector = new Rect(field.x + 36f, field.y + 6f, Mathf.Max(40f, field.width - 36f), 22f);
            string[] names = direction
                ? new[] { "↙ 後ろ下", "↓ 下", "↘ 前下", "← 後ろ", "N ニュートラル", "→ 前", "↖ 後ろ上", "↑ 上", "↗ 前上" }
                : new[] { "弱攻撃", "中攻撃", "強攻撃", "SP 必殺技", "投げ（専用ボタン）" };
            int current = direction ? property.intValue - 1 : property.intValue;
            EditorGUI.BeginChangeCheck();
            int selected = EditorGUI.Popup(selector, current, names);
            if (EditorGUI.EndChangeCheck()) property.intValue = direction ? selected + 1 : selected;
            EditorGUI.EndProperty();
        }

        public static void DrawCommand(FighterMoveBinding binding)
        {
            if (binding == null) return;
            using (new EditorGUILayout.HorizontalScope())
            {
                // 既存の表示名に含まれる数字コマンドは、画像列との二重表示を避ける。
                string title = DisplayName(binding);
                GUILayout.Label(title, GUILayout.MinWidth(80f));
                if (binding.CommandDirections != null)
                    foreach (FighterInputDirection direction in binding.CommandDirections)
                        GUILayout.Label(new GUIContent(FightCommandIcons.Direction(direction), direction.ToString()), GUILayout.Width(30f), GUILayout.Height(30f));
                if (binding.HasDirectionalCommand) GUILayout.Label("+", GUILayout.Width(12f));
                Texture2D attack = FightCommandIcons.Attack(binding.InputButton);
                GUILayout.Label(attack != null ? new GUIContent(attack, binding.InputButton.ToString()) : new GUIContent("投げ"), GUILayout.Width(32f), GUILayout.Height(30f));
            }
        }

        public static string DisplayName(FighterMoveBinding binding)
        {
            return System.Text.RegularExpressions.Regex.Replace(binding.DisplayName ?? string.Empty, "（[1-9]+＋[^）]+）", string.Empty);
        }
    }
}
