using UnityEngine;

namespace GASG.Fighting
{
    [DisallowMultipleComponent]
    public sealed class FightCameraController : MonoBehaviour
    {
        [Header("調整データ")]
        [SerializeField] private FightCameraSettings settings;

        [Header("追従対象")]
        [SerializeField] private Transform player1;
        [SerializeField] private Transform player2;

        private Camera targetCamera;
        private float shakeTimeRemaining;
        private float shakeDuration;
        private float shakePositionAmplitude;
        private float shakeRotationAmplitude;
        private float shakeFrequency;
        private float shakeSampleTime;
        private float shakeSeed;
        private Vector3 lastShakePositionOffset;
        private Quaternion lastShakeRotationOffset = Quaternion.identity;

        public FightCameraSettings Settings => settings;

        private void Awake()
        {
            targetCamera = GetComponent<Camera>();
        }

        private void OnEnable()
        {
            if (targetCamera == null)
            {
                targetCamera = GetComponent<Camera>();
            }
        }

        private void LateUpdate()
        {
            RemovePreviousShakeOffset();
            ApplyCamera(false);
            ApplyShake();
        }

        /// <summary>
        /// 戦闘の時間停止とは独立した、表示専用のカメラシェイクを開始します。
        /// </summary>
        public void RequestShake(
            int durationFrames,
            float positionAmplitude,
            float rotationAmplitude,
            float frequency)
        {
            if (durationFrames <= 0 || (positionAmplitude <= 0f && rotationAmplitude <= 0f))
            {
                return;
            }

            float requestedDuration = durationFrames * FightFrameTiming.FrameDuration;
            shakeTimeRemaining = Mathf.Max(shakeTimeRemaining, requestedDuration);
            // 再ヒット時は現在の残り時間を新しい100%強度として扱う。
            shakeDuration = shakeTimeRemaining;
            shakePositionAmplitude = Mathf.Max(shakePositionAmplitude, positionAmplitude);
            shakeRotationAmplitude = Mathf.Max(shakeRotationAmplitude, rotationAmplitude);
            shakeFrequency = Mathf.Max(shakeFrequency, Mathf.Max(1f, frequency));
            shakeSampleTime = 0f;
            shakeSeed = Mathf.Repeat(shakeSeed + 17.371f, 997f);
        }

        private void ApplyCamera(bool immediate)
        {
            if (player1 == null || player2 == null || settings == null)
            {
                return;
            }

            Vector3 midpoint = (player1.position + player2.position) * 0.5f;
            float airborneHeight = Mathf.Max(0f, midpoint.y);
            midpoint.y = settings.LookAtHeight + airborneHeight * settings.AirborneFollow;
            float separation = Mathf.Abs(player2.position.x - player1.position.x);
            float distance = Mathf.Clamp(
                settings.BaseDistance + separation * settings.DistancePerSeparation,
                settings.BaseDistance,
                settings.MaximumDistance);
            Vector3 desiredPosition = midpoint + new Vector3(settings.HorizontalOffset, settings.CameraHeight, -distance);
            Quaternion desiredRotation = Quaternion.LookRotation(midpoint - desiredPosition);

            if (immediate)
            {
                transform.position = desiredPosition;
                transform.rotation = desiredRotation;
            }
            else
            {
                float positionBlend = 1f - Mathf.Exp(-settings.PositionSmoothness * Time.deltaTime);
                float rotationBlend = 1f - Mathf.Exp(-settings.RotationSmoothness * Time.deltaTime);
                transform.position = Vector3.Lerp(transform.position, desiredPosition, positionBlend);
                transform.rotation = Quaternion.Slerp(transform.rotation, desiredRotation, rotationBlend);
            }

            if (targetCamera != null)
            {
                targetCamera.fieldOfView = settings.FieldOfView;
            }
        }

        private void ApplyShake()
        {
            if (shakeTimeRemaining <= 0f || shakeDuration <= 0f)
            {
                ResetShakeState();
                return;
            }

            shakeSampleTime += Time.unscaledDeltaTime;
            shakeTimeRemaining = Mathf.Max(0f, shakeTimeRemaining - Time.unscaledDeltaTime);

            float normalizedRemaining = Mathf.Clamp01(shakeTimeRemaining / shakeDuration);
            // 終端で急に止まらないよう、残り時間を二乗して減衰させる。
            float envelope = normalizedRemaining * normalizedRemaining;
            float sample = shakeSampleTime * shakeFrequency;
            float xNoise = Mathf.PerlinNoise(shakeSeed, sample) * 2f - 1f;
            float yNoise = Mathf.PerlinNoise(shakeSeed + 31.7f, sample) * 2f - 1f;
            float rollNoise = Mathf.PerlinNoise(shakeSeed + 67.3f, sample) * 2f - 1f;

            lastShakePositionOffset =
                (transform.right * xNoise + transform.up * yNoise) *
                shakePositionAmplitude * envelope;
            lastShakeRotationOffset = Quaternion.Euler(
                0f,
                0f,
                rollNoise * shakeRotationAmplitude * envelope);

            transform.position += lastShakePositionOffset;
            transform.rotation *= lastShakeRotationOffset;

            if (shakeTimeRemaining <= 0f)
            {
                shakeDuration = 0f;
                shakePositionAmplitude = 0f;
                shakeRotationAmplitude = 0f;
                shakeFrequency = 0f;
            }
        }

        private void RemovePreviousShakeOffset()
        {
            transform.position -= lastShakePositionOffset;
            transform.rotation *= Quaternion.Inverse(lastShakeRotationOffset);
            lastShakePositionOffset = Vector3.zero;
            lastShakeRotationOffset = Quaternion.identity;
        }

        private void ResetShakeState()
        {
            shakeTimeRemaining = 0f;
            shakeDuration = 0f;
            shakePositionAmplitude = 0f;
            shakeRotationAmplitude = 0f;
            shakeFrequency = 0f;
            lastShakePositionOffset = Vector3.zero;
            lastShakeRotationOffset = Quaternion.identity;
        }

        private void OnDisable()
        {
            RemovePreviousShakeOffset();
            ResetShakeState();
        }

#if UNITY_EDITOR
        public void EditorApplyImmediatePreview()
        {
            if (targetCamera == null)
            {
                targetCamera = GetComponent<Camera>();
            }

            ApplyCamera(true);
        }
#endif

#if UNITY_EDITOR
        public void EditorConfigure(Transform newPlayer1, Transform newPlayer2, FightCameraSettings newSettings = null)
        {
            player1 = newPlayer1;
            player2 = newPlayer2;
            settings = newSettings;
        }
#endif
    }
}
