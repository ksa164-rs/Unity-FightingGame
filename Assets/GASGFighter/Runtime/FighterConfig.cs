using UnityEngine;

namespace GASG.Fighting
{
    [CreateAssetMenu(fileName = "FighterConfig_New", menuName = "GASG/Fighting/Fighter Config")]
    public sealed class FighterConfig : ScriptableObject
    {
        [Header("基本")]
        [SerializeField] private string displayName = "Prototype Fighter";
        [Min(1)] [SerializeField] private int maxHealth = 1000;

        [Header("移動")]
        [Min(0f)] [SerializeField] private float forwardSpeed = 4.5f;
        [Min(0f)] [SerializeField] private float backwardSpeed = 3.5f;
        [Min(0f)] [SerializeField] private float jumpSpeed = 8.5f;
        [Min(0f)] [SerializeField] private float gravity = 24f;
        [Min(0f)] [SerializeField] private float stageHalfWidth = 8f;
        [Min(0f)] [SerializeField] private float pushboxHalfWidth = 0.55f;

        [Header("空中操作・押し戻し")]
        [InspectorName("空中の横入力倍率")]
        [Tooltip("横移動入力へ掛ける倍率です。移動速度そのものの倍率ではありません。0で空中横操作を無効化します。")]
        [Range(0f, 1f)] [SerializeField] private float airInputMultiplier = 0.55f;
        [InspectorName("空中攻撃中の横入力倍率")]
        [Range(0f, 1f)] [SerializeField] private float airAttackInputMultiplier = 0.25f;
        [InspectorName("ガード時の被攻撃側押し戻し倍率")]
        [Min(0f)] [SerializeField] private float blockKnockbackMultiplier = 0.35f;
        [InspectorName("押し戻し減速量（毎秒）")]
        [Tooltip("被攻撃側と攻撃側の押し戻し初速を減衰させます。大きいほど短い距離で停止します。")]
        [Min(0.01f)] [SerializeField] private float knockbackDeceleration = 10f;

        [Header("入力（60fps基準）")]
        [InspectorName("ボタン入力保持フレーム")]
        [Tooltip("技を出せない瞬間に押したボタンを、何フレーム先まで予約するか指定します。")]
        [Range(1, 12)] [SerializeField] private int inputBufferFrames = 6;

        [Header("ダブルタップステップ（60fps基準）")]
        [InspectorName("二回目の入力受付フレーム")]
        [Range(4, 30)] [SerializeField] private int doubleTapWindowFrames = 12;
        [InspectorName("ステップ移動フレーム")]
        [Range(3, 30)] [SerializeField] private int stepDurationFrames = 10;
        [InspectorName("前ステップ距離")]
        [Range(0.1f, 5f)] [SerializeField] private float forwardStepDistance = 2.2f;
        [InspectorName("後ろステップ距離")]
        [Range(0.1f, 5f)] [SerializeField] private float backwardStepDistance = 1.8f;
        [InspectorName("連続ステップ待機フレーム")]
        [Range(0, 60)] [SerializeField] private int stepCooldownFrames = 8;

        [Header("攻撃モーションデータベース")]
        [Tooltip("攻撃モーションとゲームプレイ用パラメーターを一括管理します。")]
        [SerializeField] private FighterAttackDatabase attackDatabase;

        [Header("アニメーションAction")]
        [Tooltip("Idle、Crouch、Jumpなど、攻撃以外のAction割り当てです。")]
        [SerializeField] private FighterAnimationProfile animationProfile;

        [Header("旧形式の攻撃参照（移行互換用）")]
        [HideInInspector] [SerializeField] private FighterAttackDefinition lightAttack;
        [HideInInspector] [SerializeField] private FighterAttackDefinition mediumAttack;
        [HideInInspector] [SerializeField] private FighterAttackDefinition heavyAttack;
        [HideInInspector] [SerializeField] private FighterAttackDefinition specialAttack;
        [HideInInspector] [SerializeField] private FighterAttackDefinition throwAttack;

        public string DisplayName => displayName;
        public int MaxHealth => maxHealth;
        public float ForwardSpeed => forwardSpeed;
        public float BackwardSpeed => backwardSpeed;
        public float JumpSpeed => jumpSpeed;
        public float Gravity => gravity;
        public float StageHalfWidth => stageHalfWidth;
        public float PushboxHalfWidth => pushboxHalfWidth;
        public int InputBufferFrames => inputBufferFrames;
        public int DoubleTapWindowFrames => doubleTapWindowFrames;
        public int StepDurationFrames => stepDurationFrames;
        public float ForwardStepDistance => forwardStepDistance;
        public float BackwardStepDistance => backwardStepDistance;
        public int StepCooldownFrames => stepCooldownFrames;
        public float AirInputMultiplier => airInputMultiplier;
        public float AirAttackInputMultiplier => airAttackInputMultiplier;
        public float BlockKnockbackMultiplier => blockKnockbackMultiplier;
        public float KnockbackDeceleration => knockbackDeceleration;
        public FighterAttackDatabase AttackDatabase => attackDatabase;
        public FighterAnimationProfile AnimationProfile => animationProfile;
        public FighterAttackDefinition LightAttack => attackDatabase != null ? attackDatabase.LightAttack : lightAttack;
        public FighterAttackDefinition MediumAttack => attackDatabase != null ? attackDatabase.MiddleAttack : mediumAttack;
        public FighterAttackDefinition HeavyAttack => attackDatabase != null ? attackDatabase.HeavyAttack : heavyAttack;
        public FighterAttackDefinition SpecialAttack => attackDatabase != null ? attackDatabase.SpecialAttack : specialAttack;
        public FighterAttackDefinition ThrowAttack => attackDatabase != null ? attackDatabase.ThrowAttack : throwAttack;

        private void OnValidate()
        {
            maxHealth = Mathf.Max(1, maxHealth);
            airInputMultiplier = Mathf.Clamp01(airInputMultiplier);
            airAttackInputMultiplier = Mathf.Clamp01(airAttackInputMultiplier);
            blockKnockbackMultiplier = Mathf.Max(0f, blockKnockbackMultiplier);
            knockbackDeceleration = Mathf.Max(0.01f, knockbackDeceleration);
            forwardSpeed = Mathf.Max(0f, forwardSpeed);
            backwardSpeed = Mathf.Max(0f, backwardSpeed);
            jumpSpeed = Mathf.Max(0f, jumpSpeed);
            gravity = Mathf.Max(0f, gravity);
            stageHalfWidth = Mathf.Max(0f, stageHalfWidth);
            pushboxHalfWidth = Mathf.Max(0f, pushboxHalfWidth);
            inputBufferFrames = Mathf.Clamp(inputBufferFrames, 1, 12);
            doubleTapWindowFrames = Mathf.Clamp(doubleTapWindowFrames, 4, 30);
            stepDurationFrames = Mathf.Clamp(stepDurationFrames, 3, 30);
            forwardStepDistance = Mathf.Max(0.1f, forwardStepDistance);
            backwardStepDistance = Mathf.Max(0.1f, backwardStepDistance);
            stepCooldownFrames = Mathf.Clamp(stepCooldownFrames, 0, 60);
        }

#if UNITY_EDITOR
        public void EditorConfigure(
            string newDisplayName,
            int newMaxHealth,
            float newForwardSpeed,
            float newBackwardSpeed,
            float newJumpSpeed,
            float newGravity,
            float newStageHalfWidth,
            float newPushboxHalfWidth,
            int newDoubleTapWindowFrames,
            int newStepDurationFrames,
            float newForwardStepDistance,
            float newBackwardStepDistance,
            int newStepCooldownFrames,
            FighterAttackDefinition newLightAttack,
            FighterAttackDefinition newMediumAttack,
            FighterAttackDefinition newHeavyAttack,
            FighterAttackDefinition newSpecialAttack,
            FighterAttackDefinition newThrowAttack)
        {
            displayName = newDisplayName;
            maxHealth = Mathf.Max(1, newMaxHealth);
            forwardSpeed = Mathf.Max(0f, newForwardSpeed);
            backwardSpeed = Mathf.Max(0f, newBackwardSpeed);
            jumpSpeed = Mathf.Max(0f, newJumpSpeed);
            gravity = Mathf.Max(0f, newGravity);
            stageHalfWidth = Mathf.Max(0f, newStageHalfWidth);
            pushboxHalfWidth = Mathf.Max(0f, newPushboxHalfWidth);
            doubleTapWindowFrames = Mathf.Clamp(newDoubleTapWindowFrames, 4, 30);
            stepDurationFrames = Mathf.Clamp(newStepDurationFrames, 3, 30);
            forwardStepDistance = Mathf.Max(0.1f, newForwardStepDistance);
            backwardStepDistance = Mathf.Max(0.1f, newBackwardStepDistance);
            stepCooldownFrames = Mathf.Clamp(newStepCooldownFrames, 0, 60);
            lightAttack = newLightAttack;
            mediumAttack = newMediumAttack;
            heavyAttack = newHeavyAttack;
            specialAttack = newSpecialAttack;
            throwAttack = newThrowAttack;
        }

        public void EditorSetAttackDatabase(FighterAttackDatabase newAttackDatabase)
        {
            attackDatabase = newAttackDatabase;
        }

        public void EditorSetAnimationProfile(FighterAnimationProfile newAnimationProfile)
        {
            animationProfile = newAnimationProfile;
        }
#endif
    }
}
