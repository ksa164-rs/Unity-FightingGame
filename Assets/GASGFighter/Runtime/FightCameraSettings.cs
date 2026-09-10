using UnityEngine;

namespace GASG.Fighting
{
    public enum FightCameraPreset
    {
        Close,
        Standard,
        Wide
    }

    [CreateAssetMenu(fileName = "FightCameraSettings_New", menuName = "GASG/Fighting/Camera Settings")]
    public sealed class FightCameraSettings : ScriptableObject
    {
        [Header("レンズ")]
        [InspectorName("画角 (FOV)")]
        [Tooltip("小さいほど望遠でキャラクターが大きく見え、大きいほど広角になります。")]
        [Range(20f, 75f)] [SerializeField] private float fieldOfView = 42f;

        [Header("構図")]
        [InspectorName("カメラの高さ")]
        [Tooltip("キャラクターの中心から見たカメラの高さです。")]
        [Range(0.5f, 8f)] [SerializeField] private float cameraHeight = 3.2f;

        [InspectorName("注視点の高さ")]
        [Tooltip("カメラが見る基準の高さです。胸元付近なら1.0～1.4が目安です。")]
        [Range(0f, 4f)] [SerializeField] private float lookAtHeight = 1f;

        [InspectorName("左右オフセット")]
        [Tooltip("画面の中心を左右へずらします。通常は0を使用します。")]
        [Range(-3f, 3f)] [SerializeField] private float horizontalOffset;

        [InspectorName("基本距離")]
        [Tooltip("キャラクター同士が近いときのカメラ距離です。")]
        [Range(5f, 20f)] [SerializeField] private float baseDistance = 10.5f;

        [InspectorName("最大距離")]
        [Tooltip("キャラクター同士が離れたときの最大カメラ距離です。")]
        [Range(6f, 25f)] [SerializeField] private float maximumDistance = 14f;

        [InspectorName("離れ具合によるズーム量")]
        [Tooltip("キャラクター間距離1mあたり、カメラをどれだけ後ろへ移動するかを指定します。")]
        [Range(0f, 1f)] [SerializeField] private float distancePerSeparation = 0.22f;

        [InspectorName("ジャンプ追従率")]
        [Tooltip("0なら地上構図を維持し、1ならジャンプへ完全追従します。")]
        [Range(0f, 1f)] [SerializeField] private float airborneFollow = 0.35f;

        [Header("追従")]
        [InspectorName("位置の追従速度")]
        [Tooltip("大きいほど素早く追従します。0にすると追従しません。")]
        [Range(0f, 30f)] [SerializeField] private float positionSmoothness = 8f;

        [InspectorName("回転の追従速度")]
        [Tooltip("大きいほど注視点へ素早く向きます。")]
        [Range(0f, 30f)] [SerializeField] private float rotationSmoothness = 8f;

        public float FieldOfView => fieldOfView;
        public float CameraHeight => cameraHeight;
        public float LookAtHeight => lookAtHeight;
        public float HorizontalOffset => horizontalOffset;
        public float BaseDistance => baseDistance;
        public float MaximumDistance => maximumDistance;
        public float DistancePerSeparation => distancePerSeparation;
        public float AirborneFollow => airborneFollow;
        public float PositionSmoothness => positionSmoothness;
        public float RotationSmoothness => rotationSmoothness;

        private void OnValidate()
        {
            maximumDistance = Mathf.Max(baseDistance, maximumDistance);
        }

#if UNITY_EDITOR
        public void EditorApplyPreset(FightCameraPreset preset)
        {
            switch (preset)
            {
                case FightCameraPreset.Close:
                    fieldOfView = 36f;
                    cameraHeight = 2.8f;
                    lookAtHeight = 1.05f;
                    horizontalOffset = 0f;
                    baseDistance = 9f;
                    maximumDistance = 12f;
                    distancePerSeparation = 0.18f;
                    airborneFollow = 0.25f;
                    positionSmoothness = 9f;
                    rotationSmoothness = 9f;
                    break;
                case FightCameraPreset.Wide:
                    fieldOfView = 50f;
                    cameraHeight = 3.8f;
                    lookAtHeight = 1.2f;
                    horizontalOffset = 0f;
                    baseDistance = 12f;
                    maximumDistance = 16f;
                    distancePerSeparation = 0.3f;
                    airborneFollow = 0.45f;
                    positionSmoothness = 7f;
                    rotationSmoothness = 7f;
                    break;
                default:
                    fieldOfView = 42f;
                    cameraHeight = 3.2f;
                    lookAtHeight = 1f;
                    horizontalOffset = 0f;
                    baseDistance = 10.5f;
                    maximumDistance = 14f;
                    distancePerSeparation = 0.22f;
                    airborneFollow = 0.35f;
                    positionSmoothness = 8f;
                    rotationSmoothness = 8f;
                    break;
            }

            OnValidate();
        }
#endif
    }
}
