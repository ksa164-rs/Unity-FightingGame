using System;
using UnityEngine;

namespace GASG.Fighting
{
    [DisallowMultipleComponent]
    public sealed class FighterController : MonoBehaviour
    {
        [Header("識別")]
        [SerializeField] [Range(1, 2)] private int playerIndex = 1;
        [SerializeField] private FighterConfig config;

        [Header("参照")]
        [SerializeField] private FighterInputSource inputSource;
        [SerializeField] private Transform visualRoot;
        [SerializeField] private Animator animator;

        [Header("見た目の向き")]
        [Tooltip("モデルが右を向く時の基準回転です。")]
        [SerializeField] private Vector3 visualFacingEuler = new Vector3(0f, 90f, 0f);
        [Tooltip("ミラー反転する向きで、反転後に追加する回転です。モデル固有の正面軸に合わせて調整できます。")]
        [SerializeField] private Vector3 mirroredFacingRotationOffset = new Vector3(0f, 180f, 0f);
        [Tooltip("ミラー側へ掛けるスケール倍率です。X=-1にすることで、180度回転と向きが相殺されるのを防ぎます。")]
        [SerializeField] private Vector3 mirroredScaleMultiplier = new Vector3(-1f, 1f, 1f);

        [Header("プレイヤー識別色")]
        [Tooltip("元マテリアルを変更せず、Rendererへプレイヤー別の乗算色を加えます。")]
        [SerializeField] private bool enablePlayerColorTint = true;
        [InspectorName("1Pの乗算色")]
        [ColorUsage(false, false)]
        [SerializeField] private Color player1ColorTint = new Color(0.45f, 0.82f, 1f, 1f);
        [InspectorName("2Pの乗算色")]
        [ColorUsage(false, false)]
        [SerializeField] private Color player2ColorTint = new Color(1f, 0.48f, 0.3f, 1f);
        [InspectorName("色味の強さ")]
        [Tooltip("0で元の色、1で指定した乗算色をそのまま使用します。")]
        [Range(0f, 1f)] [SerializeField] private float playerColorTintStrength = 0.28f;

        [Header("判定表示（Game / Sceneビュー）")]
        [Tooltip("攻撃中のHitboxを赤色、投げ判定を紫色で表示します。")]
        [SerializeField] private bool showAttackHitboxes = true;
        [Tooltip("被弾判定のHurtboxを緑色で表示します。")]
        [SerializeField] private bool showHurtboxes = true;
        [Range(0.005f, 0.08f)] [SerializeField] private float debugLineWidth = 0.025f;

        private const int HitQueryCapacity = 16;

        private readonly System.Collections.Generic.HashSet<FighterController> hitTargets =
            new System.Collections.Generic.HashSet<FighterController>();
        private readonly Collider2D[] hitQueryResults = new Collider2D[HitQueryCapacity];
        private readonly FighterCommandBuffer commandBuffer = new FighterCommandBuffer();
        private readonly FighterInputHistory inputHistory = new FighterInputHistory();
        private FighterAttackDefinition currentAttack;
        private FighterInputFrame lastInput;
        private FightMatchManager matchManager;
        private int stateFramesRemaining;
        private int attackFrame;
        private int attackVfxStartFrame;
        private int attackInstanceId;
        private int doubleTapFramesRemaining;
        private int lastTapDirection;
        private int previousMoveDirection;
        private int stepFramesRemaining;
        private int stepCooldownFramesRemaining;
        private int stepDirection;
        private float stepDistancePerFrame;
        private float verticalVelocity;
        // ジャンプ開始時に決めた横方向の速度。空中では入力で上書きしない。
        private float airborneHorizontalVelocity;
        private float knockbackVelocity;
        private float attackRecoilVelocity;
        private bool attackStartedInAir;
        private bool attackConnected;
        private bool projectileSpawned;
        private bool attackVfxSpawned;
        private bool facingRight = true;
        private BoxCollider2D hurtbox;
        private FighterPresentation presentation;

        public event Action<FighterController, int, bool> DamageReceived;

        public int PlayerIndex => playerIndex;
        public FighterConfig Config => config;
        public FighterController Opponent { get; private set; }
        public FighterState State { get; private set; } = FighterState.RoundLocked;
        public int CurrentHealth { get; private set; }
        public int MaxHealth => config != null ? config.MaxHealth : 1;
        public bool IsGrounded => transform.position.y <= 0.001f;
        public bool FacingRight => facingRight;
        public float FacingDirection => facingRight ? 1f : -1f;
        public float PushboxHalfWidth => config != null ? config.PushboxHalfWidth : 0.5f;
        public FighterInputSource InputSource => inputSource;
        public FighterAttackDefinition CurrentAttack => currentAttack;
        public int CurrentAttackFrame => attackFrame;
        public int AttackInstanceId => attackInstanceId;
        public FightMatchManager MatchManager => matchManager;
        public int InputHistoryCount => inputHistory.Count;

        public FighterInputDirection GetInputDirectionFromNewest(int index)
        {
            return inputHistory.GetDirectionFromNewest(index);
        }

        public FighterInputFrame GetInputFrameFromNewest(int index)
        {
            return inputHistory.GetInputFromNewest(index);
        }

        private void Awake()
        {
            if (inputSource == null)
            {
                inputSource = GetComponent<FighterInputSource>();
            }

            hurtbox = GetComponentInChildren<BoxCollider2D>();
            RebuildPresentation();
            CurrentHealth = MaxHealth;
        }

        /// <summary>
        /// FightMatchManagerだけが呼び出す、60Hzのゲームプレイ更新です。
        /// MonoBehaviourの実行順へ依存させないため、FixedUpdateは各Fighterに持たせません。
        /// </summary>
        public void SimulateFrame()
        {
            if (config == null || inputSource == null || matchManager == null || !matchManager.RoundActive)
            {
                return;
            }

            lastInput = inputSource.ConsumeFrame();
            commandBuffer.Advance(lastInput, config.InputBufferFrames);
            UpdateFacing();
            inputHistory.Advance(lastInput, FacingDirection);

            if (stepCooldownFramesRemaining > 0)
            {
                stepCooldownFramesRemaining--;
            }

            UpdateDoubleTapInput();

            switch (State)
            {
                case FighterState.Neutral:
                case FighterState.Crouching:
                    UpdateGroundControl();
                    break;
                case FighterState.Jumping:
                    UpdateAirControl();
                    break;
                case FighterState.Stepping:
                    UpdateStep();
                    break;
                case FighterState.Attacking:
                    UpdateAttack();
                    break;
                case FighterState.HitStun:
                case FighterState.BlockStun:
                case FighterState.KnockedDown:
                    UpdateReaction();
                    break;
            }

            ApplyKnockback();
            ApplyAirborneHorizontalMotion();
            ApplyVerticalMotion();
            ClampToStage();
            UpdateAnimatorParameters();
        }

        private void LateUpdate()
        {
            // 移動や押し合い解決後の最終位置で向きを確定し、位置交換したフレームでも向かい合わせる。
            UpdateFacing();
            presentation?.UpdateDebug(currentAttack, attackFrame, FacingDirection, hurtbox);
        }

        private void OnDestroy()
        {
            presentation?.Dispose();
            presentation = null;
        }

        public void SetPresentationPaused(bool paused)
        {
            presentation?.SetPaused(paused);
        }

        public void Initialize(FightMatchManager newMatchManager, FighterController opponent)
        {
            matchManager = newMatchManager;
            Opponent = opponent;
        }

        public void ResetForRound(Vector3 position)
        {
            if (inputSource != null) inputSource.DisplayHistory.Clear();
            transform.position = position;
            CurrentHealth = MaxHealth;
            currentAttack = null;
            attackFrame = 0;
            attackInstanceId++;
            attackConnected = false;
            projectileSpawned = false;
            attackVfxSpawned = false;
            doubleTapFramesRemaining = 0;
            lastTapDirection = 0;
            previousMoveDirection = 0;
            stepFramesRemaining = 0;
            stepCooldownFramesRemaining = 0;
            stepDirection = 0;
            stepDistancePerFrame = 0f;
            stateFramesRemaining = 0;
            verticalVelocity = 0f;
            airborneHorizontalVelocity = 0f;
            knockbackVelocity = 0f;
            attackRecoilVelocity = 0f;
            State = FighterState.RoundLocked;
            hitTargets.Clear();
            commandBuffer.Clear();
            inputHistory.Clear();
            UpdateFacing();
            PlaySystemAction(config != null ? config.AnimationProfile?.Reset : null, "Reset");
        }

        public void SetRoundControl(bool enabled)
        {
            if (enabled)
            {
                State = IsGrounded ? FighterState.Neutral : FighterState.Jumping;
            }
            else
            {
                State = FighterState.RoundLocked;
                currentAttack = null;
                attackConnected = false;
                projectileSpawned = false;
                stepFramesRemaining = 0;
                doubleTapFramesRemaining = 0;
                lastTapDirection = 0;
                knockbackVelocity = 0f;
                attackRecoilVelocity = 0f;
                commandBuffer.Clear();
                inputHistory.Clear();
            }
        }

        public void SetPositionX(float x)
        {
            Vector3 position = transform.position;
            position.x = Mathf.Clamp(x, -config.StageHalfWidth, config.StageHalfWidth);
            position.z = 0f;
            transform.position = position;
        }

        public bool CanBeThrown()
        {
            return IsGrounded &&
                   State != FighterState.HitStun &&
                   State != FighterState.BlockStun &&
                   State != FighterState.KnockedDown &&
                   State != FighterState.RoundLocked;
        }

        public bool TryReceiveAttack(FighterController attacker, FighterAttackDefinition attack, float impactDirection = 0f)
        {
            return TryReceiveAttack(attacker, attack, out _, impactDirection);
        }

        public bool TryReceiveAttack(FighterController attacker, FighterAttackDefinition attack,
            out bool blocked, float impactDirection = 0f, bool playContactVfx = true)
        {
            blocked = false;
            if (attacker == null || attack == null || CurrentHealth <= 0 || State == FighterState.RoundLocked)
            {
                return false;
            }

            if (attack.IsThrow && !CanBeThrown())
            {
                return false;
            }

            blocked = !attack.IsThrow && CanBlock(attack);
            float pushDirection = impactDirection == 0f ? attacker.FacingDirection : Mathf.Sign(impactDirection);
            int damage = blocked ? 0 : attack.Damage;
            CurrentHealth = Mathf.Max(0, CurrentHealth - damage);
            currentAttack = null;
            attackFrame = 0;
            attackConnected = false;

            if (blocked)
            {
                State = FighterState.BlockStun;
                stateFramesRemaining = attack.BlockStunFrames;
                knockbackVelocity = pushDirection * attack.Knockback * (config != null ? config.BlockKnockbackMultiplier : 0.35f);
                PlaySystemAction(config != null ? config.AnimationProfile?.Block : null, "Block");
            }
            else if (attack.IsThrow || attack.KnockdownFrames > 0 || CurrentHealth <= 0)
            {
                State = FighterState.KnockedDown;
                stateFramesRemaining = CurrentHealth <= 0 ? int.MaxValue : Mathf.Max(1, attack.KnockdownFrames);
                knockbackVelocity = pushDirection * attack.Knockback;
                FighterAnimationProfile reactionProfile = config != null ? config.AnimationProfile : null;
                PlaySystemAction(
                    attack.IsThrow ? reactionProfile?.Thrown : reactionProfile?.Knockdown,
                    attack.IsThrow ? "Thrown" : "Knockdown");
            }
            else
            {
                State = FighterState.HitStun;
                stateFramesRemaining = attack.HitStunFrames;
                knockbackVelocity = pushDirection * attack.Knockback;
                PlaySystemAction(config != null ? config.AnimationProfile?.Hit : null, "LightHit");
            }

            DamageReceived?.Invoke(this, damage, blocked);
            if (playContactVfx && !attack.IsThrow)
            {
                FighterVfxPlayback.PlayContact(GetContactVfxPosition(attacker, attack, pushDirection), pushDirection, blocked);
            }
            if (!attack.IsThrow)
            {
                float attackerKnockback = blocked
                    ? attack.AttackerKnockbackOnBlock
                    : attack.AttackerKnockbackOnHit;
                attacker.ApplyAttackRecoil(attackerKnockback);
            }

            if (!blocked && matchManager != null)
            {
                matchManager.RequestHitEffect(attack);
            }
            return true;
        }

        private void UpdateGroundControl()
        {
            FighterMoveContext context = lastInput.moveY < -0.5f
                ? FighterMoveContext.Crouching
                : FighterMoveContext.Standing;
            if (TryStartResolvedAttack(context, AttackCancelTarget.None, false))
            {
                return;
            }

            if (config.AttackDatabase != null && config.AttackDatabase.HasMoveBindings)
            {
                UpdateGroundMovementWithoutLegacyAttacks();
                return;
            }

            if (TryStartBufferedAttack(FighterCommand.Throw, config.ThrowAttack))
            {
                return;
            }

            if (TryStartBufferedAttack(FighterCommand.Special, config.SpecialAttack))
            {
                return;
            }

            if (TryStartBufferedAttack(FighterCommand.Heavy, config.HeavyAttack))
            {
                return;
            }

            if (TryStartBufferedAttack(FighterCommand.Medium, config.MediumAttack))
            {
                return;
            }

            if (TryStartBufferedAttack(FighterCommand.Light, config.LightAttack))
            {
                return;
            }

            if (commandBuffer.Consume(FighterCommand.Jump) || lastInput.moveY > 0.65f)
            {
                StartJump();
                return;
            }

            if (lastInput.moveY < -0.5f)
            {
                State = FighterState.Crouching;
                return;
            }

            State = FighterState.Neutral;
            MoveHorizontally(lastInput.moveX);
        }

        private void UpdateAirControl()
        {
            if (TryStartResolvedAttack(FighterMoveContext.Airborne, AttackCancelTarget.None, false))
            {
                return;
            }

            if (config.AttackDatabase != null && config.AttackDatabase.HasMoveBindings)
            {
                return;
            }

            if (TryStartBufferedAttack(FighterCommand.Special, config.SpecialAttack))
            {
                return;
            }

            if (TryStartBufferedAttack(FighterCommand.Heavy, config.HeavyAttack))
            {
                return;
            }

            if (TryStartBufferedAttack(FighterCommand.Medium, config.MediumAttack))
            {
                return;
            }

            TryStartBufferedAttack(FighterCommand.Light, config.LightAttack);
        }

        private void UpdateDoubleTapInput()
        {
            int currentDirection = Mathf.Abs(lastInput.moveX) >= 0.65f ? (int)Mathf.Sign(lastInput.moveX) : 0;
            bool canStartStep = IsGrounded &&
                                (State == FighterState.Neutral || State == FighterState.Crouching) &&
                                stepCooldownFramesRemaining <= 0;

            if (doubleTapFramesRemaining > 0)
            {
                doubleTapFramesRemaining--;
                if (doubleTapFramesRemaining <= 0)
                {
                    lastTapDirection = 0;
                }
            }

            bool directionPressedThisFrame = currentDirection != 0 && previousMoveDirection == 0;
            if (canStartStep && directionPressedThisFrame)
            {
                if (lastTapDirection == currentDirection && doubleTapFramesRemaining > 0)
                {
                    StartStep(currentDirection);
                    lastTapDirection = 0;
                    doubleTapFramesRemaining = 0;
                }
                else
                {
                    lastTapDirection = currentDirection;
                    doubleTapFramesRemaining = config.DoubleTapWindowFrames;
                }
            }
            else if (!canStartStep && State != FighterState.Stepping)
            {
                lastTapDirection = 0;
                doubleTapFramesRemaining = 0;
            }

            previousMoveDirection = currentDirection;
        }

        private void StartStep(int direction)
        {
            bool forwardStep = direction * FacingDirection > 0f;
            float distance = forwardStep ? config.ForwardStepDistance : config.BackwardStepDistance;
            stepDirection = direction;
            stepFramesRemaining = Mathf.Max(1, config.StepDurationFrames);
            stepDistancePerFrame = distance / stepFramesRemaining;
            State = FighterState.Stepping;
            FighterAnimationProfile profile = config != null ? config.AnimationProfile : null;
            PlaySystemAction(
                forwardStep ? profile?.ForwardStep : profile?.BackwardStep,
                forwardStep ? "ForwardStep" : "BackwardStep");
        }

        private void UpdateStep()
        {
            Vector3 position = transform.position;
            position.x += stepDirection * stepDistancePerFrame;
            transform.position = position;

            stepFramesRemaining--;
            if (stepFramesRemaining <= 0)
            {
                stepDirection = 0;
                stepDistancePerFrame = 0f;
                stepCooldownFramesRemaining = config.StepCooldownFrames;
                ReturnToMovementState();
            }
        }

        private void UpdateAttack()
        {
            if (currentAttack == null)
            {
                ReturnToMovementState();
                return;
            }

            attackFrame++;
            TryPlayAttackVfx();
            TrySpawnProjectile();

            if (TryCancelCurrentAttack())
            {
                return;
            }

            if (attackFrame >= currentAttack.TotalFrames)
            {
                currentAttack = null;
                attackConnected = false;
                ReturnToMovementState();
            }
        }

        private void UpdateReaction()
        {
            if (stateFramesRemaining != int.MaxValue)
            {
                stateFramesRemaining--;
                if (stateFramesRemaining <= 0)
                {
                    ReturnToMovementState();
                }
            }
        }

        private void StartAttack(FighterAttackDefinition attack)
        {
            attackInstanceId++;
            currentAttack = attack;
            attackFrame = 0;
            attackVfxStartFrame = attack.StartupFrames;
            if (attack.HasFrameHitboxes)
            {
                // 実際のHitbox開始を優先し、旧フレーム数と不一致の調整データにも合わせます。
                int firstHitboxFrame = int.MaxValue;
                for (int i = 0; i < attack.Hitboxes.Count; i++)
                {
                    if (attack.Hitboxes[i] != null)
                        firstHitboxFrame = Mathf.Min(firstHitboxFrame, attack.Hitboxes[i].StartFrame);
                }
                if (firstHitboxFrame != int.MaxValue) attackVfxStartFrame = firstHitboxFrame;
            }
            attackStartedInAir = !IsGrounded;
            attackConnected = false;
            projectileSpawned = false;
            attackVfxSpawned = false;
            State = FighterState.Attacking;
            hitTargets.Clear();
            PlayTrigger(attack.AnimatorTrigger);
            TryPlayAttackVfx();
            TrySpawnProjectile();
        }

        private bool TryStartBufferedAttack(FighterCommand command, FighterAttackDefinition attack)
        {
            if (attack == null || !commandBuffer.Consume(command))
            {
                return false;
            }

            // 同時押し時に優先度の低い入力が後から暴発しないよう、採用時に攻撃入力をまとめて消費する。
            commandBuffer.ClearAttackCommands();
            StartAttack(attack);
            return true;
        }

        private bool TryStartResolvedAttack(
            FighterMoveContext context,
            AttackCancelTarget allowedCancelTargets,
            bool enforceCancelTargets)
        {
            FighterAttackDatabase database = config != null ? config.AttackDatabase : null;
            if (database == null || !database.HasMoveBindings)
            {
                return false;
            }

            FighterMoveBinding bestBinding = null;
            System.Collections.Generic.IReadOnlyList<FighterMoveBinding> bindings = database.MoveBindings;
            for (int i = 0; i < bindings.Count; i++)
            {
                FighterMoveBinding binding = bindings[i];
                if (binding == null || binding.Attack == null || !binding.IsAvailableIn(context))
                {
                    continue;
                }

                FighterCommand command = ConvertAttackButton(binding.InputButton);
                if (!commandBuffer.Contains(command))
                {
                    continue;
                }

                if (enforceCancelTargets &&
                    (allowedCancelTargets & binding.CancelCategory) == 0)
                {
                    continue;
                }

                if (!inputHistory.Matches(binding.CommandDirections, binding.CommandWindowFrames))
                {
                    continue;
                }

                if (bestBinding == null || binding.Priority > bestBinding.Priority)
                {
                    bestBinding = binding;
                }
            }

            if (bestBinding == null)
            {
                return false;
            }

            commandBuffer.Consume(ConvertAttackButton(bestBinding.InputButton));
            commandBuffer.ClearAttackCommands();
            if (bestBinding.HasDirectionalCommand)
            {
                inputHistory.ConsumeDirectionalCommand();
            }

            StartAttack(bestBinding.Attack);
            return true;
        }

        private void UpdateGroundMovementWithoutLegacyAttacks()
        {
            if (commandBuffer.Consume(FighterCommand.Jump) || lastInput.moveY > 0.65f)
            {
                StartJump();
                return;
            }

            if (lastInput.moveY < -0.5f)
            {
                State = FighterState.Crouching;
                return;
            }

            State = FighterState.Neutral;
            MoveHorizontally(lastInput.moveX);
        }

        public bool TryCollectAttackContact(out FighterAttackContact contact)
        {
            contact = default;
            if (currentAttack == null || Opponent == null || hitTargets.Contains(Opponent))
            {
                return false;
            }

            // 飛び道具の接触判定はFighterProjectile側で行い、近接Hitboxとの二重ヒットを防ぎます。
            if (currentAttack.IsProjectile)
            {
                return false;
            }

            if (currentAttack.HasFrameHitboxes)
            {
                System.Collections.Generic.IReadOnlyList<AttackHitboxFrame> hitboxes = currentAttack.Hitboxes;
                for (int i = 0; i < hitboxes.Count; i++)
                {
                    AttackHitboxFrame hitbox = hitboxes[i];
                    if (hitbox != null && hitbox.IsActive(attackFrame) &&
                        HitboxContainsOpponent(hitbox.Center, hitbox.Size))
                    {
                        contact = new FighterAttackContact(this, Opponent, currentAttack);
                        return true;
                    }
                }

                return false;
            }

            int activeStart = currentAttack.StartupFrames;
            int activeEnd = activeStart + currentAttack.ActiveFrames;
            if (attackFrame >= activeStart && attackFrame < activeEnd &&
                HitboxContainsOpponent(currentAttack.HitboxCenter, currentAttack.HitboxSize))
            {
                contact = new FighterAttackContact(this, Opponent, currentAttack);
                return true;
            }

            return false;
        }

        public void ApplyAttackContact(FighterAttackContact contact)
        {
            if (!contact.IsValid || contact.Attacker != this || hitTargets.Contains(contact.Defender))
            {
                return;
            }

            hitTargets.Add(contact.Defender);
            if (contact.Defender.TryReceiveAttack(this, contact.Attack))
            {
                attackConnected = true;
            }
        }

        /// <summary>飛び道具から既存のダメージ・ガード処理へ接続します。</summary>
        public bool TryApplyProjectileHit(FighterAttackDefinition attack, int sourceAttackInstanceId, float impactDirection)
        {
            return TryApplyProjectileHit(attack, sourceAttackInstanceId, impactDirection, out _);
        }

        public bool TryApplyProjectileHit(FighterAttackDefinition attack, int sourceAttackInstanceId,
            float impactDirection, out bool blocked, bool playContactVfx = true)
        {
            blocked = false;
            if (attack == null || !attack.IsProjectile || Opponent == null)
            {
                return false;
            }

            if (!Opponent.TryReceiveAttack(this, attack, out blocked, impactDirection, playContactVfx))
            {
                return false;
            }

            // 前に撃った弾の命中で、別の攻撃のヒット確認キャンセルを許可しない。
            if (currentAttack == attack && attackInstanceId == sourceAttackInstanceId)
            {
                attackConnected = true;
            }
            return true;
        }

        private void TrySpawnProjectile()
        {
            if (projectileSpawned || currentAttack == null || !currentAttack.IsProjectile ||
                attackFrame < currentAttack.ProjectileSpawnFrame)
            {
                return;
            }

            projectileSpawned = true;
            FighterVfxLibrary vfxLibrary = FighterVfxPlayback.Library;
            bool useContactEvents = vfxLibrary != null && vfxLibrary.IsHadouken(currentAttack) && vfxLibrary.HadoukenPrefab != null;
            GameObject projectilePrefab = useContactEvents ? vfxLibrary.HadoukenPrefab : currentAttack.ProjectilePrefab;
            if (projectilePrefab == null)
            {
                Debug.LogError($"[GASG Fighter][FAIL] 飛び道具Prefabが未設定です: {currentAttack.DisplayName}", this);
                return;
            }

            Vector3 offset = currentAttack.ProjectileSpawnOffset;
            offset.x *= FacingDirection;
            Vector3 spawnPosition = transform.position + offset;
            Vector3 direction = Vector3.right * FacingDirection;
            Quaternion rotation = FacingDirection >= 0f ? Quaternion.identity : Quaternion.Euler(0f, 180f, 0f);

            GameObject instance = Instantiate(projectilePrefab, spawnPosition, rotation);
            instance.name = $"{projectilePrefab.name}_P{playerIndex}";
            FighterProjectile projectile = instance.GetComponent<FighterProjectile>();
            if (projectile == null)
            {
                projectile = instance.AddComponent<FighterProjectile>();
            }

            projectile.Initialize(this, currentAttack, direction, useContactEvents,
                useContactEvents ? vfxLibrary.HadoukenImpactLifetime : currentAttack.ImpactVfxLifetime);
        }

        private void TryPlayAttackVfx()
        {
            if (attackVfxSpawned || currentAttack == null || currentAttack.IsProjectile || currentAttack.IsThrow ||
                attackFrame < attackVfxStartFrame) return;
            // ゲームの判定開始時に一度だけ再生要求を渡し、以降の見た目はGraphが進めます。
            attackVfxSpawned = true;
            FighterVfxPlayback.PlayAttack(this, currentAttack);
        }

        private Vector3 GetContactVfxPosition(FighterController attacker, FighterAttackDefinition attack, float direction)
        {
            Vector3 center = attack.HitboxCenter;
            if (attack.HasFrameHitboxes)
            {
                for (int i = 0; i < attack.Hitboxes.Count; i++)
                {
                    AttackHitboxFrame frame = attack.Hitboxes[i];
                    if (frame != null && frame.IsActive(attacker.CurrentAttackFrame))
                    {
                        center = frame.Center;
                        break;
                    }
                }
            }

            Vector3 contact = attacker.GetHitboxWorldCenter(center);
            if (hurtbox != null)
            {
                Bounds bounds = hurtbox.bounds;
                contact.x = direction >= 0f ? bounds.min.x : bounds.max.x;
                contact.y = Mathf.Clamp(contact.y, bounds.min.y, bounds.max.y);
            }
            else
            {
                contact.x = transform.position.x - direction * PushboxHalfWidth;
            }
            contact.z = transform.position.z;
            return contact;
        }

        private bool HitboxContainsOpponent(Vector3 localCenter, Vector3 hitboxSize)
        {
            Vector3 worldCenter = GetHitboxWorldCenter(localCenter);
            int overlapCount = Physics2D.OverlapBoxNonAlloc(
                new Vector2(worldCenter.x, worldCenter.y),
                new Vector2(Mathf.Abs(hitboxSize.x), Mathf.Abs(hitboxSize.y)),
                0f,
                hitQueryResults);

            for (int i = 0; i < overlapCount; i++)
            {
                Collider2D overlap = hitQueryResults[i];
                if (overlap == null)
                {
                    continue;
                }

                FighterController target = overlap.GetComponentInParent<FighterController>();
                if (target == Opponent)
                {
                    return true;
                }
            }

            return false;
        }

        private Vector3 GetHitboxWorldCenter(Vector3 localCenter)
        {
            return transform.position + new Vector3(
                localCenter.x * FacingDirection,
                localCenter.y,
                localCenter.z);
        }

        private bool TryCancelCurrentAttack()
        {
            FighterAttackDefinition sourceAttack = currentAttack;
            if (sourceAttack == null || config == null || !sourceAttack.CanCancelAt(attackFrame, attackConnected))
            {
                return false;
            }

            FighterMoveContext context = IsGrounded
                ? (lastInput.moveY < -0.5f ? FighterMoveContext.Crouching : FighterMoveContext.Standing)
                : FighterMoveContext.Airborne;
            if (TryStartResolvedAttack(context, sourceAttack.CancelTargets, true))
            {
                return true;
            }

            if (config.AttackDatabase != null && config.AttackDatabase.HasMoveBindings)
            {
                return false;
            }

            if (IsGrounded && sourceAttack.AllowsCancelTo(AttackCancelTarget.Throw) &&
                TryStartBufferedAttack(FighterCommand.Throw, config.ThrowAttack))
            {
                return true;
            }

            if (sourceAttack.AllowsCancelTo(AttackCancelTarget.Special) &&
                TryStartBufferedAttack(FighterCommand.Special, config.SpecialAttack))
            {
                return true;
            }

            if (sourceAttack.AllowsCancelTo(AttackCancelTarget.Heavy) &&
                TryStartBufferedAttack(FighterCommand.Heavy, config.HeavyAttack))
            {
                return true;
            }

            if (sourceAttack.AllowsCancelTo(AttackCancelTarget.Medium) &&
                TryStartBufferedAttack(FighterCommand.Medium, config.MediumAttack))
            {
                return true;
            }

            if (sourceAttack.AllowsCancelTo(AttackCancelTarget.Light) &&
                TryStartBufferedAttack(FighterCommand.Light, config.LightAttack))
            {
                return true;
            }

            return false;
        }

        private static FighterCommand ConvertAttackButton(FighterAttackButton button)
        {
            switch (button)
            {
                case FighterAttackButton.Medium:
                    return FighterCommand.Medium;
                case FighterAttackButton.Heavy:
                    return FighterCommand.Heavy;
                case FighterAttackButton.Special:
                    return FighterCommand.Special;
                case FighterAttackButton.Throw:
                    return FighterCommand.Throw;
                default:
                    return FighterCommand.Light;
            }
        }


        private bool CanBlock(FighterAttackDefinition attack)
        {
            if (!IsGrounded || (State != FighterState.Neutral && State != FighterState.Crouching &&
                                State != FighterState.BlockStun))
            {
                return false;
            }

            float moveX = lastInput.moveX;
            bool holdingBack = moveX * FacingDirection < -0.35f;
            if (!holdingBack)
            {
                return false;
            }

            bool crouching = lastInput.moveY < -0.5f;
            switch (attack.GuardHeight)
            {
                case GuardHeight.Low:
                    return crouching;
                case GuardHeight.Overhead:
                    return !crouching;
                case GuardHeight.Unblockable:
                    return false;
                default:
                    return true;
            }
        }

        private void MoveHorizontally(float inputX)
        {
            if (Mathf.Abs(inputX) < 0.1f)
            {
                return;
            }

            bool movingForward = inputX * FacingDirection > 0f;
            float speed = movingForward ? config.ForwardSpeed : config.BackwardSpeed;
            Vector3 position = transform.position;
            position.x += Mathf.Sign(inputX) * speed * FightFrameTiming.FrameDuration;
            transform.position = position;
        }

        private void StartJump()
        {
            verticalVelocity = config.JumpSpeed;
            airborneHorizontalVelocity = GetGroundHorizontalSpeed(lastInput.moveX);
            State = FighterState.Jumping;
            PlaySystemAction(config.AnimationProfile?.Jump, "Jump");
        }

        private float GetGroundHorizontalSpeed(float inputX)
        {
            if (Mathf.Abs(inputX) < 0.1f)
            {
                return 0f;
            }

            bool movingForward = inputX * FacingDirection > 0f;
            float speed = movingForward ? config.ForwardSpeed : config.BackwardSpeed;
            return Mathf.Sign(inputX) * speed;
        }

        private void ApplyAirborneHorizontalMotion()
        {
            if (State != FighterState.Jumping && !(State == FighterState.Attacking && attackStartedInAir))
            {
                return;
            }

            Vector3 position = transform.position;
            position.x += airborneHorizontalVelocity * FightFrameTiming.FrameDuration;
            transform.position = position;
        }

        private void ApplyVerticalMotion()
        {
            if (IsGrounded && verticalVelocity <= 0f)
            {
                verticalVelocity = 0f;
                Vector3 groundedPosition = transform.position;
                groundedPosition.y = 0f;
                groundedPosition.z = 0f;
                transform.position = groundedPosition;
                return;
            }

            verticalVelocity -= config.Gravity * FightFrameTiming.FrameDuration;
            Vector3 position = transform.position;
            position.y += verticalVelocity * FightFrameTiming.FrameDuration;
            if (position.y <= 0f)
            {
                position.y = 0f;
                verticalVelocity = 0f;
                airborneHorizontalVelocity = 0f;
                if (State == FighterState.Jumping || (State == FighterState.Attacking && attackStartedInAir && currentAttack == null))
                {
                    State = FighterState.Neutral;
                    PlaySystemAction(config != null ? config.AnimationProfile?.Land : null, "Land");
                }
            }

            position.z = 0f;
            transform.position = position;
        }

        private void ApplyKnockback()
        {
            if (Mathf.Abs(knockbackVelocity) < 0.01f && Mathf.Abs(attackRecoilVelocity) < 0.01f)
            {
                knockbackVelocity = 0f;
                attackRecoilVelocity = 0f;
                return;
            }

            Vector3 position = transform.position;
            position.x += (knockbackVelocity + attackRecoilVelocity) * FightFrameTiming.FrameDuration;
            transform.position = position;
            knockbackVelocity = Mathf.MoveTowards(
                knockbackVelocity,
                0f,
                config.KnockbackDeceleration * FightFrameTiming.FrameDuration);
            attackRecoilVelocity = Mathf.MoveTowards(
                attackRecoilVelocity,
                0f,
                config.KnockbackDeceleration * FightFrameTiming.FrameDuration);
        }

        private void ApplyAttackRecoil(float recoilSpeed)
        {
            if (recoilSpeed <= 0f)
            {
                return;
            }

            // 攻撃者が向いている方向と逆へ押し戻す。再ヒット時は初速を入れ直し、毎回同じ距離を確保する。
            attackRecoilVelocity = -FacingDirection * recoilSpeed;
        }

        private void ClampToStage()
        {
            Vector3 position = transform.position;
            position.x = Mathf.Clamp(position.x, -config.StageHalfWidth, config.StageHalfWidth);
            position.z = 0f;
            transform.position = position;
        }

        private void UpdateFacing()
        {
            if (Opponent == null)
            {
                return;
            }

            facingRight = Opponent.transform.position.x >= transform.position.x;
            presentation?.SetFacing(facingRight);
        }

        private void RebuildPresentation()
        {
            presentation?.Dispose();
            Color selectedPlayerTint = playerIndex == 1 ? player1ColorTint : player2ColorTint;
            presentation = new FighterPresentation(
                transform,
                visualRoot,
                animator,
                visualFacingEuler,
                mirroredFacingRotationOffset,
                mirroredScaleMultiplier,
                enablePlayerColorTint,
                selectedPlayerTint,
                playerColorTintStrength,
                showAttackHitboxes,
                showHurtboxes,
                debugLineWidth);
        }

        private void OnValidate()
        {
            playerIndex = Mathf.Clamp(playerIndex, 1, 2);
            playerColorTintStrength = Mathf.Clamp01(playerColorTintStrength);
            if (presentation != null)
            {
                RebuildPresentation();
            }
        }

        private void ReturnToMovementState()
        {
            State = IsGrounded ? FighterState.Neutral : FighterState.Jumping;
            stateFramesRemaining = 0;
        }

        private void UpdateAnimatorParameters()
        {
            presentation?.UpdateAnimator(lastInput, State, IsGrounded);
        }

        private void PlayTrigger(string parameterName)
        {
            presentation?.PlayTrigger(parameterName);
        }

        private void PlaySystemAction(FighterActionDefinition action, string legacyActionId)
        {
            string actionId = action != null ? action.ActionId : legacyActionId;
            PlayTrigger(actionId);
        }

        private void OnDrawGizmosSelected()
        {
            if (presentation == null)
            {
                RebuildPresentation();
            }

            presentation.DrawSelectedGizmos(currentAttack, attackFrame, FacingDirection);
        }

#if UNITY_EDITOR
        public void EditorConfigure(
            int newPlayerIndex,
            FighterConfig newConfig,
            FighterInputSource newInputSource,
            Transform newVisualRoot,
            Animator newAnimator)
        {
            playerIndex = Mathf.Clamp(newPlayerIndex, 1, 2);
            config = newConfig;
            inputSource = newInputSource;
            visualRoot = newVisualRoot;
            animator = newAnimator;
            RebuildPresentation();
        }
#endif
    }
}
