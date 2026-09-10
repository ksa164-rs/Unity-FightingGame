using UnityEngine;
using UnityEngine.VFX;

namespace GASG.Fighting
{
    /// <summary>
    /// VFX Graph PrefabのRootを相手方向へ移動し、既存のHurtboxと攻撃処理へ接続します。
    /// 描画には関与せず、移動・接触・寿命だけを担当します。
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class FighterProjectile : MonoBehaviour
    {
        private const int HitCapacity = 12;

        private readonly RaycastHit2D[] hitResults = new RaycastHit2D[HitCapacity];
        private readonly Collider2D[] overlapResults = new Collider2D[HitCapacity];
        private FighterController owner;
        private FighterAttackDefinition attack;
        private Vector3 direction;
        private int framesRemaining;
        private int sourceAttackInstanceId;
        private bool pendingContact;
        private Vector3 pendingHitPosition;
        private bool initialized;
        private VisualEffect contactGraph;
        private float contactLifetime;

        public void Initialize(
            FighterController newOwner,
            FighterAttackDefinition newAttack,
            Vector3 newDirection,
            bool useContactEvents = false,
            float impactLifetime = 1.3f)
        {
            if (newOwner == null || newOwner.MatchManager == null || newAttack == null || !newAttack.IsProjectile)
            {
                Debug.LogError("[GASG Fighter][FAIL] FighterProjectileの初期化情報が不正です。", this);
                Destroy(gameObject);
                return;
            }

            owner = newOwner;
            attack = newAttack;
            direction = newDirection.sqrMagnitude > 0.0001f ? newDirection.normalized : Vector3.right;
            framesRemaining = FightFrameTiming.SecondsToFrames(attack.ProjectileLifetime);
            sourceAttackInstanceId = owner.AttackInstanceId;
            contactGraph = useContactEvents ? GetComponent<VisualEffect>() : null;
            contactLifetime = Mathf.Max(0.05f, impactLifetime);
            if (useContactEvents && (contactGraph == null || contactGraph.visualEffectAsset == null))
            {
                Debug.LogError("[GASG Fighter][FAIL] 統合波動拳PrefabのRootにVisualEffectとGraphが必要です。", this);
                Destroy(gameObject);
                return;
            }
            pendingContact = false;
            initialized = true;
            owner.MatchManager.RegisterProjectile(this);
        }

        // 描画Updateでは進めない。停止中はMatchManagerがこのメソッドを呼ばない。
        public void SimulateFrame()
        {
            if (!initialized)
            {
                return;
            }

            if (owner == null || owner.Opponent == null || owner.State == FighterState.RoundLocked)
            {
                Retire();
                return;
            }

            float distance = attack.ProjectileSpeed * FightFrameTiming.FrameDuration;
            Vector3 startPosition = transform.position;

            if (TryFindOpponentHit(startPosition, distance, out Vector3 hitPosition))
            {
                transform.position = hitPosition;
                pendingContact = true;
                pendingHitPosition = hitPosition;
                return;
            }

            transform.position = startPosition + direction * distance;
            framesRemaining--;
            if (framesRemaining <= 0)
            {
                Retire();
            }
        }

        public void ApplyPendingContact()
        {
            if (!initialized || !pendingContact)
            {
                return;
            }
            pendingContact = false;
            if (owner != null && owner.TryApplyProjectileHit(attack, sourceAttackInstanceId, direction.x, out bool blocked, false))
            {
                if (contactGraph != null)
                {
                    // 同じPrefabを接触位置に残し、Graph内の飛翔停止・命中／ガードイベントへ切り替えます。
                    // 粒子の移動・色・サイズ・寿命はGraph内だけで処理します。
                    initialized = false;
                    transform.SetParent(null, true);
                    contactGraph.Stop();
                    contactGraph.SendEvent(blocked ? "OnGuard" : "OnImpact");
                    Destroy(gameObject, contactLifetime);
                    return;
                }
                SpawnImpact(pendingHitPosition);
            }
            Retire();
        }

        public void Retire()
        {
            initialized = false;
            pendingContact = false;
            // Destroyの遅延中に、追いつき処理で同じ弾が再度命中することを防ぐ。
            gameObject.SetActive(false);
            Destroy(gameObject);
        }

        private bool TryFindOpponentHit(Vector3 origin, float distance, out Vector3 hitPosition)
        {
            hitPosition = origin + direction * distance;
            // HurtboxはCollider2Dで構成されているため、飛び道具も2D Physicsで検索する。
            // CircleCastは始点の重なりを取りこぼす場合があるため、発射位置も別途確認する。
            ContactFilter2D contactFilter = ContactFilter2D.noFilter;
            contactFilter.useTriggers = true;
            int overlapCount = Physics2D.OverlapCircle(
                new Vector2(origin.x, origin.y), attack.ProjectileRadius, contactFilter, overlapResults);
            for (int i = 0; i < overlapCount; i++)
            {
                if (overlapResults[i] != null &&
                    overlapResults[i].GetComponentInParent<FighterController>() == owner.Opponent)
                {
                    hitPosition = origin;
                    return true;
                }
            }
            int hitCount = Physics2D.CircleCast(
                new Vector2(origin.x, origin.y),
                attack.ProjectileRadius,
                new Vector2(direction.x, direction.y),
                contactFilter,
                hitResults,
                distance);

            float nearestDistance = float.MaxValue;
            bool found = false;
            for (int index = 0; index < hitCount; index++)
            {
                Collider2D collider = hitResults[index].collider;
                if (collider == null)
                {
                    continue;
                }

                FighterController target = collider.GetComponentInParent<FighterController>();
                if (target != owner.Opponent || hitResults[index].distance >= nearestDistance)
                {
                    continue;
                }

                nearestDistance = hitResults[index].distance;
                Vector2 point = hitResults[index].point;
                hitPosition = new Vector3(point.x, point.y, origin.z);
                found = true;
            }

            return found;
        }

        private void SpawnImpact(Vector3 position)
        {
            GameObject impactPrefab = attack.ProjectileImpactPrefab;
            if (impactPrefab == null)
            {
                Debug.LogWarning($"[GASG Fighter][SKIP] 命中VFX Prefabが未設定です: {attack.DisplayName}", this);
                return;
            }

            Quaternion rotation = direction.x >= 0f ? Quaternion.identity : Quaternion.Euler(0f, 180f, 0f);
            GameObject impact = Instantiate(impactPrefab, position, rotation);
            impact.name = impactPrefab.name + "_Instance";
            Destroy(impact, attack.ImpactVfxLifetime);
        }
    }
}
