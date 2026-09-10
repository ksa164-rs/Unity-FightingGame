using GASG.Fighting;
using UnityEditor;
using UnityEngine;

namespace GASG.Fighting.Editor
{
    [CustomEditor(typeof(FighterActionDefinition))]
    public sealed class FightMotionInspector : UnityEditor.Editor
    {
        private bool showInternalId;

        public override void OnInspectorGUI()
        {
            var action = (FighterActionDefinition)target;
            serializedObject.Update();
            using (new EditorGUI.DisabledScope(true))
            {
                EditorGUILayout.TextField("管理番号", string.IsNullOrEmpty(action.MotionId) ? "未採番" : action.MotionId);
            }
            EditorGUILayout.PropertyField(serializedObject.FindProperty("displayName"), new GUIContent("表示名"));
            EditorGUILayout.PropertyField(serializedObject.FindProperty("animationClip"), new GUIContent("Animation Clip"));
            showInternalId = EditorGUILayout.Foldout(showInternalId, "保守用の内部情報", true);
            if (showInternalId)
            {
                using (new EditorGUI.DisabledScope(true)) EditorGUILayout.TextField("内部接続ID", action.ActionId);
            }
            EditorGUILayout.HelpBox("管理番号は固定です。表示名とClipは変更できます。新規登録はモーション管理から行います。", MessageType.Info);
            if (serializedObject.ApplyModifiedProperties()) EditorUtility.SetDirty(action);
        }
    }
}
