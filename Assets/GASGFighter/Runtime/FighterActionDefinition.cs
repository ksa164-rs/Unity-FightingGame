using UnityEngine;

namespace GASG.Fighting
{
    /// <summary>
    /// ゲームプレイ数値を持たず、再利用可能なAction IDとAnimation Clipだけを定義します。
    /// </summary>
    public sealed class FighterActionDefinition : ScriptableObject
    {
        [HideInInspector]
        [InspectorName("内部接続ID（互換用）")]
        [Tooltip("保存データや参照に使用する固定IDです。作成後は変更しないでください。")]
        [SerializeField] private string actionId = "action.new";

        [InspectorName("モーション管理番号")]
        [Tooltip("AS_p01_000形式。p01はキャラクター番号で、対戦の1Pではありません。")]
        [SerializeField] private string motionId = string.Empty;

        [InspectorName("表示名")]
        [SerializeField] private string displayName = "New Action";

        [InspectorName("Animation Clip")]
        [SerializeField] private AnimationClip animationClip;

        public string ActionId => actionId;
        public string MotionId => motionId;
        public string DisplayLabel => $"{(string.IsNullOrEmpty(motionId) ? "未採番" : motionId)} / {displayName}";
        public string DisplayName => displayName;
        public AnimationClip AnimationClip => animationClip;

#if UNITY_EDITOR
        public void EditorSetMotionIdentity(string newMotionId, string newDisplayName)
        {
            if (!FighterMotionNaming.IsValid(newMotionId))
            {
                throw new System.ArgumentException("管理番号はAS_p01_000形式の半角英数字で指定してください。");
            }
            motionId = newMotionId;
            displayName = string.IsNullOrWhiteSpace(newDisplayName) ? motionId : newDisplayName.Trim();
        }

        public void EditorConfigure(string newActionId, string newDisplayName, AnimationClip newAnimationClip)
        {
            actionId = string.IsNullOrWhiteSpace(newActionId) ? "action.new" : newActionId.Trim();
            displayName = string.IsNullOrWhiteSpace(newDisplayName) ? actionId : newDisplayName.Trim();
            animationClip = newAnimationClip;
        }
#endif
    }
}
