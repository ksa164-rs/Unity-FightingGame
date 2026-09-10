using System;
using System.Collections.Generic;
using UnityEngine;

namespace GASG.Fighting
{
    [CreateAssetMenu(
        fileName = "AnimationActionCatalog_New",
        menuName = "GASG/Fighting/Animation Action Catalog")]
    public sealed class FighterActionCatalog : ScriptableObject
    {
        [Tooltip("Action Managerから作成・編集します。Action IDはプロジェクト内で一意にしてください。")]
        [SerializeField] private List<FighterActionDefinition> actions = new List<FighterActionDefinition>();

        public IReadOnlyList<FighterActionDefinition> Actions => actions;

        public FighterActionDefinition FindByMotionId(string motionId)
        {
            if (string.IsNullOrWhiteSpace(motionId) || actions == null) return null;
            foreach (FighterActionDefinition action in actions)
            {
                if (action != null && string.Equals(action.MotionId, motionId, StringComparison.Ordinal)) return action;
            }
            return null;
        }

        public FighterActionDefinition FindById(string actionId)
        {
            if (string.IsNullOrWhiteSpace(actionId) || actions == null)
            {
                return null;
            }

            for (int i = 0; i < actions.Count; i++)
            {
                FighterActionDefinition action = actions[i];
                if (action != null && string.Equals(action.ActionId, actionId, StringComparison.Ordinal))
                {
                    return action;
                }
            }

            return null;
        }

        public bool ValidateCatalog(out string report)
        {
            List<string> errors = new List<string>();
            HashSet<string> ids = new HashSet<string>(StringComparer.Ordinal);
            HashSet<string> motionIds = new HashSet<string>(StringComparer.Ordinal);

            if (actions == null || actions.Count == 0)
            {
                errors.Add("Actionが1件も登録されていません。");
            }
            else
            {
                for (int i = 0; i < actions.Count; i++)
                {
                    FighterActionDefinition action = actions[i];
                    if (action == null)
                    {
                        errors.Add($"{i}番目のAction参照が空です。");
                        continue;
                    }

                    if (string.IsNullOrWhiteSpace(action.ActionId))
                    {
                        errors.Add($"{action.name}: Action IDが空です。");
                    }
                    else if (!IsValidActionId(action.ActionId))
                    {
                        errors.Add($"使用できない文字を含むAction IDです: {action.ActionId}");
                    }
                    else if (!ids.Add(action.ActionId))
                    {
                        errors.Add($"Action IDが重複しています: {action.ActionId}");
                    }

                    // 未移行の旧データは引き続き読み込める。採番済みの形式と重複は必ず検証する。
                    if (!string.IsNullOrEmpty(action.MotionId))
                    {
                        if (!FighterMotionNaming.IsValid(action.MotionId))
                            errors.Add($"管理番号の形式が不正です: {action.MotionId}");
                        else if (!motionIds.Add(action.MotionId))
                            errors.Add($"管理番号が重複しています: {action.MotionId}");
                    }
                }
            }

            report = errors.Count == 0 ? "Action Catalogに問題はありません。" : string.Join("\n", errors);
            return errors.Count == 0;
        }

        public static bool IsValidActionId(string actionId)
        {
            if (string.IsNullOrWhiteSpace(actionId))
            {
                return false;
            }

            for (int i = 0; i < actionId.Length; i++)
            {
                char character = actionId[i];
                bool allowed = (character >= 'a' && character <= 'z') ||
                               (character >= 'A' && character <= 'Z') ||
                               (character >= '0' && character <= '9') ||
                               character == '_' ||
                               character == '-' ||
                               character == '.';
                if (!allowed)
                {
                    return false;
                }
            }

            return true;
        }

        private void OnValidate()
        {
            if (!ValidateCatalog(out string report))
            {
                Debug.LogWarning($"[GASG Fighter][Action Catalog]\n{report}", this);
            }
        }

#if UNITY_EDITOR
        public void EditorAddAction(FighterActionDefinition action)
        {
            if (action != null && !actions.Contains(action))
            {
                actions.Add(action);
            }
        }
#endif
    }
}
