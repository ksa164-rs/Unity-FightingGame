using System;
using System.Collections.Generic;
using UnityEngine;

namespace GASG.Fighting
{
    [Serializable]
    public sealed class AttackHitboxFrame
    {
        [InspectorName("判定名")]
        [SerializeField] private string label = "Main";
        [InspectorName("開始フレーム")]
        [Min(0)] [SerializeField] private int startFrame = 5;
        [InspectorName("終了フレーム")]
        [Min(0)] [SerializeField] private int endFrame = 7;
        [InspectorName("中心位置")]
        [SerializeField] private Vector3 center = new Vector3(0.9f, 1.1f, 0f);
        [InspectorName("判定サイズ")]
        [SerializeField] private Vector3 size = new Vector3(1.1f, 0.8f, 1f);

        public string Label => label;
        public int StartFrame => startFrame;
        public int EndFrame => endFrame;
        public Vector3 Center => center;
        public Vector3 Size => size;

        public bool IsActive(int frame)
        {
            return frame >= startFrame && frame <= endFrame;
        }

#if UNITY_EDITOR
        public AttackHitboxFrame(string newLabel, int newStartFrame, int newEndFrame, Vector3 newCenter, Vector3 newSize)
        {
            label = newLabel;
            startFrame = Mathf.Max(0, newStartFrame);
            endFrame = Mathf.Max(startFrame, newEndFrame);
            center = newCenter;
            size = new Vector3(
                Mathf.Max(0.01f, newSize.x),
                Mathf.Max(0.01f, newSize.y),
                Mathf.Max(0.01f, newSize.z));
        }
#endif
    }

    [CreateAssetMenu(fileName = "Attack_New", menuName = "GASG/Fighting/Attack Definition")]
    public sealed class FighterAttackDefinition : ScriptableObject
    {
        [Header("表示")]
        [SerializeField] private string displayName = "New Attack";
        [InspectorName("再生モーション")]
        [Tooltip("Action Catalogで管理しているActionを参照します。フレームデータとAnimation Clipを分離するための参照です。")]
        [SerializeField] private FighterActionDefinition action;
        [HideInInspector]
        [InspectorName("再生するAnimation Clip")]
        [SerializeField] private AnimationClip animationClip;
        [HideInInspector]
        [SerializeField] private string animatorTrigger = "LightAttack";

        [Header("フレーム（60fps基準）")]
        [InspectorName("発生フレーム")]
        [Min(1)] [SerializeField] private int startupFrames = 5;
        [InspectorName("持続フレーム")]
        [Min(1)] [SerializeField] private int activeFrames = 3;
        [InspectorName("後隙フレーム")]
        [Tooltip("攻撃判定が終了してから、通常行動へ戻れるまでの硬直時間です。")]
        [Min(1)] [SerializeField] private int recoveryFrames = 12;

        [Header("攻撃キャンセル（60fps基準）")]
        [InspectorName("攻撃キャンセルを有効化")]
        [SerializeField] private bool enableAttackCancel;
        [InspectorName("キャンセル受付開始フレーム")]
        [Min(0)] [SerializeField] private int cancelStartFrame = 8;
        [InspectorName("キャンセル受付終了フレーム")]
        [Min(0)] [SerializeField] private int cancelEndFrame = 19;
        [InspectorName("ヒット／ガード時のみ")]
        [SerializeField] private bool cancelOnHitOrBlockOnly = true;
        [InspectorName("キャンセル可能な攻撃")]
        [SerializeField] private AttackCancelTarget cancelTargets = AttackCancelTarget.None;

        [Header("ヒット効果")]
        [Min(0)] [SerializeField] private int damage = 50;
        [Min(1)] [SerializeField] private int hitStunFrames = 14;
        [Min(1)] [SerializeField] private int blockStunFrames = 9;
        [InspectorName("ヒットストップ（フレーム）")]
        [Tooltip("攻撃がヒットしたときに、戦闘とAnimatorを停止する60Hz基準のフレーム数です。")]
        [Min(0)] [SerializeField] private int hitStopFrames = 5;
        [HideInInspector]
        [SerializeField] private float hitStopTimeScale = 0.1f;
        [InspectorName("攻撃側ノックバック（ヒット時）")]
        [Tooltip("ヒット成立時に攻撃者を後ろへ滑らせる初速です。小技の連打で間合いが離れるようになります。")]
        [Min(0f)] [SerializeField] private float attackerKnockbackOnHit = 1.8f;
        [InspectorName("攻撃側ノックバック（ガード時）")]
        [Tooltip("ガード成立時に攻撃者を後ろへ滑らせる初速です。通常はヒット時より少し大きくします。")]
        [Min(0f)] [SerializeField] private float attackerKnockbackOnBlock = 2.4f;
        [Min(0f)] [SerializeField] private float knockback = 2.5f;
        [SerializeField] private GuardHeight guardHeight = GuardHeight.Mid;

        [Header("ヒット時のカメラシェイク")]
        [InspectorName("位置の振幅")]
        [Tooltip("カメラを上下左右へ揺らす最大距離です。0で位置シェイクを無効化します。")]
        [Range(0f, 0.5f)] [SerializeField] private float cameraShakePositionAmplitude = 0.04f;
        [InspectorName("回転の振幅（度）")]
        [Tooltip("カメラのZ回転へ加える最大角度です。0で回転シェイクを無効化します。")]
        [Range(0f, 3f)] [SerializeField] private float cameraShakeRotationAmplitude = 0.2f;
        [InspectorName("継続フレーム")]
        [Tooltip("60fps基準の継続時間です。ヒットストップ中も実時間で再生されます。")]
        [Range(0, 60)] [SerializeField] private int cameraShakeDurationFrames = 5;
        [InspectorName("周波数")]
        [Tooltip("1秒間の揺れの細かさです。大きいほど細かく振動します。")]
        [Range(1f, 60f)] [SerializeField] private float cameraShakeFrequency = 32f;

        [Header("攻撃判定（キャラクター原点基準）")]
        [Tooltip("開始・終了フレームを指定した複数の攻撃判定です。専用Hitbox Editorから編集できます。")]
        [SerializeField] private List<AttackHitboxFrame> hitboxes = new List<AttackHitboxFrame>();

        [HideInInspector]
        [SerializeField] private Vector3 hitboxCenter = new Vector3(0.9f, 1.1f, 0f);
        [HideInInspector]
        [SerializeField] private Vector3 hitboxSize = new Vector3(1.1f, 0.8f, 1.0f);

        [Header("飛び道具")]
        [InspectorName("飛び道具として扱う")]
        [SerializeField] private bool isProjectile;
        [InspectorName("発射体VFX Prefab")]
        [SerializeField] private GameObject projectilePrefab;
        [InspectorName("命中VFX Prefab")]
        [SerializeField] private GameObject projectileImpactPrefab;
        [InspectorName("発射フレーム")]
        [Min(0)] [SerializeField] private int projectileSpawnFrame = 12;
        [InspectorName("発射位置")]
        [Tooltip("キャラクター原点基準です。Xは向きに応じて自動反転します。")]
        [SerializeField] private Vector3 projectileSpawnOffset = new Vector3(0.95f, 1.15f, 0f);
        [InspectorName("飛翔速度")]
        [Min(0.01f)] [SerializeField] private float projectileSpeed = 7.5f;
        [InspectorName("当たり判定半径")]
        [Min(0.01f)] [SerializeField] private float projectileRadius = 0.28f;
        [InspectorName("最大生存時間（秒）")]
        [Min(0.05f)] [SerializeField] private float projectileLifetime = 2.4f;
        [InspectorName("命中VFX生存時間（秒）")]
        [Min(0.05f)] [SerializeField] private float impactVfxLifetime = 1.2f;

        [Header("投げ")]
        [SerializeField] private bool isThrow;
        [Min(0)] [SerializeField] private int knockdownFrames;

        public string DisplayName => displayName;
        public FighterActionDefinition Action => action;
        public string ActionId => action != null ? action.ActionId : animatorTrigger;
        public AnimationClip AnimationClip => action != null ? action.AnimationClip : animationClip;
        public string AnimatorTrigger => ActionId;
        public int StartupFrames => startupFrames;
        public int ActiveFrames => activeFrames;
        public int RecoveryFrames => recoveryFrames;
        public int TotalFrames => startupFrames + activeFrames + recoveryFrames;
        public bool EnableAttackCancel => enableAttackCancel;
        public int CancelStartFrame => cancelStartFrame;
        public int CancelEndFrame => cancelEndFrame;
        public bool CancelOnHitOrBlockOnly => cancelOnHitOrBlockOnly;
        public AttackCancelTarget CancelTargets => cancelTargets;
        public int Damage => damage;
        public int HitStunFrames => hitStunFrames;
        public int BlockStunFrames => blockStunFrames;
        public int HitAdvantageOnFirstActiveFrame =>
            CalculateFrameAdvantage(hitStunFrames, activeFrames, recoveryFrames);
        public int BlockAdvantageOnFirstActiveFrame =>
            CalculateFrameAdvantage(blockStunFrames, activeFrames, recoveryFrames);
        public int HitStopFrames => hitStopFrames;
        public float AttackerKnockbackOnHit => attackerKnockbackOnHit;
        public float AttackerKnockbackOnBlock => attackerKnockbackOnBlock;
        public float Knockback => knockback;
        public GuardHeight GuardHeight => guardHeight;
        public float CameraShakePositionAmplitude => cameraShakePositionAmplitude;
        public float CameraShakeRotationAmplitude => cameraShakeRotationAmplitude;
        public int CameraShakeDurationFrames => cameraShakeDurationFrames;
        public float CameraShakeFrequency => cameraShakeFrequency;
        public Vector3 HitboxCenter => hitboxCenter;
        public Vector3 HitboxSize => hitboxSize;
        public IReadOnlyList<AttackHitboxFrame> Hitboxes => hitboxes;
        public bool HasFrameHitboxes => hitboxes != null && hitboxes.Count > 0;
        public bool IsProjectile => isProjectile;
        public GameObject ProjectilePrefab => projectilePrefab;
        public GameObject ProjectileImpactPrefab => projectileImpactPrefab;
        public int ProjectileSpawnFrame => projectileSpawnFrame;
        public Vector3 ProjectileSpawnOffset => projectileSpawnOffset;
        public float ProjectileSpeed => projectileSpeed;
        public float ProjectileRadius => projectileRadius;
        public float ProjectileLifetime => projectileLifetime;
        public float ImpactVfxLifetime => impactVfxLifetime;
        public bool IsThrow => isThrow;
        public int KnockdownFrames => knockdownFrames;

        public bool CanCancelAt(int frame, bool attackConnected)
        {
            if (!enableAttackCancel || cancelTargets == AttackCancelTarget.None)
            {
                return false;
            }

            if (cancelOnHitOrBlockOnly && !attackConnected)
            {
                return false;
            }

            return frame >= cancelStartFrame && frame <= cancelEndFrame;
        }

        public bool AllowsCancelTo(AttackCancelTarget target)
        {
            return (cancelTargets & target) != 0;
        }

        /// <summary>
        /// 最速の攻撃判定で接触した場合の硬直差を計算します。
        /// 正数は攻撃側有利、負数は攻撃側不利です。
        /// </summary>
        public static int CalculateFrameAdvantage(int defenderStunFrames, int activeFrameCount, int recoveryFrameCount)
        {
            int attackerFramesRemaining = Mathf.Max(1, activeFrameCount) + Mathf.Max(1, recoveryFrameCount);
            return Mathf.Max(1, defenderStunFrames) - attackerFramesRemaining;
        }

        private void OnValidate()
        {
            startupFrames = Mathf.Max(1, startupFrames);
            activeFrames = Mathf.Max(1, activeFrames);
            recoveryFrames = Mathf.Max(1, recoveryFrames);
            hitStopFrames = Mathf.Max(0, hitStopFrames);
            hitStopTimeScale = Mathf.Clamp(hitStopTimeScale, 0.01f, 1f);
            attackerKnockbackOnHit = Mathf.Max(0f, attackerKnockbackOnHit);
            attackerKnockbackOnBlock = Mathf.Max(0f, attackerKnockbackOnBlock);
            cameraShakePositionAmplitude = Mathf.Max(0f, cameraShakePositionAmplitude);
            cameraShakeRotationAmplitude = Mathf.Max(0f, cameraShakeRotationAmplitude);
            cameraShakeDurationFrames = Mathf.Clamp(cameraShakeDurationFrames, 0, 60);
            cameraShakeFrequency = Mathf.Max(1f, cameraShakeFrequency);
            projectileSpawnFrame = Mathf.Max(0, projectileSpawnFrame);
            projectileSpeed = Mathf.Max(0.01f, projectileSpeed);
            projectileRadius = Mathf.Max(0.01f, projectileRadius);
            projectileLifetime = Mathf.Max(0.05f, projectileLifetime);
            impactVfxLifetime = Mathf.Max(0.05f, impactVfxLifetime);

            int lastAttackFrame = Mathf.Max(0, TotalFrames - 1);
            cancelStartFrame = Mathf.Clamp(cancelStartFrame, 0, lastAttackFrame);
            cancelEndFrame = Mathf.Clamp(cancelEndFrame, cancelStartFrame, lastAttackFrame);
        }

#if UNITY_EDITOR
        public void EditorSetAction(FighterActionDefinition newAction)
        {
            action = newAction;
        }

        public void EditorConfigure(
            string newDisplayName,
            string newAnimatorTrigger,
            int newStartupFrames,
            int newActiveFrames,
            int newRecoveryFrames,
            int newDamage,
            int newHitStunFrames,
            int newBlockStunFrames,
            int newHitStopFrames,
            float newKnockback,
            GuardHeight newGuardHeight,
            Vector3 newHitboxCenter,
            Vector3 newHitboxSize,
            bool newIsThrow,
            int newKnockdownFrames)
        {
            displayName = newDisplayName;
            animatorTrigger = newAnimatorTrigger;
            startupFrames = Mathf.Max(1, newStartupFrames);
            activeFrames = Mathf.Max(1, newActiveFrames);
            recoveryFrames = Mathf.Max(1, newRecoveryFrames);
            enableAttackCancel = false;
            cancelStartFrame = startupFrames + activeFrames;
            cancelEndFrame = TotalFrames - 1;
            cancelOnHitOrBlockOnly = true;
            cancelTargets = AttackCancelTarget.None;
            damage = Mathf.Max(0, newDamage);
            hitStunFrames = Mathf.Max(1, newHitStunFrames);
            blockStunFrames = Mathf.Max(1, newBlockStunFrames);
            hitStopFrames = Mathf.Max(0, newHitStopFrames);
            knockback = Mathf.Max(0f, newKnockback);
            guardHeight = newGuardHeight;
            hitboxCenter = newHitboxCenter;
            hitboxSize = newHitboxSize;
            hitboxes = new List<AttackHitboxFrame>
            {
                new AttackHitboxFrame(
                    "Main",
                    startupFrames,
                    startupFrames + activeFrames - 1,
                    newHitboxCenter,
                    newHitboxSize)
            };
            isThrow = newIsThrow;
            knockdownFrames = Mathf.Max(0, newKnockdownFrames);
        }
#endif
    }
}
