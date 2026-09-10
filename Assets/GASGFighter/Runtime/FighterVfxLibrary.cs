using System;
using UnityEngine;

namespace GASG.Fighting
{
    /// <summary>
    /// Unity 6000.3 / VFX Graph 17.3用の再生先一覧です。
    /// 色・粒子数・速度・サイズ・フェードは各VFX GraphのInspectorで調整します。
    /// </summary>
    [CreateAssetMenu(fileName = "FighterVfxLibrary", menuName = "GASG/Fighting/VFX Library")]
    public sealed class FighterVfxLibrary : ScriptableObject
    {
        public const string ResourceName = "FighterVfxLibrary";

        [Header("各エフェクトの単一Prefab")]
        [InspectorName("通常攻撃")]
        [SerializeField] private GameObject normalAttackPrefab;
        [InspectorName("昇龍拳")]
        [SerializeField] private GameObject shoryukenPrefab;
        [InspectorName("ダメージヒット")]
        [SerializeField] private GameObject hitPrefab;
        [InspectorName("ガード")]
        [SerializeField] private GameObject guardPrefab;
        [InspectorName("波動拳（飛翔・命中・ガードを統合）")]
        [SerializeField] private GameObject hadoukenPrefab;

        [Header("ゲームプレイとの接続")]
        [Tooltip("Action Catalog内の固定Action IDです。表示名を変更しても接続は維持されます。")]
        [SerializeField] private string[] shoryukenActionIds = { "SpecialAttack" };
        [SerializeField] private string[] hadoukenActionIds = { "SpecialAttack_Hadouken" };
        [Tooltip("キャラクター原点からの発生位置です。Xは攻撃方向に応じて反転します。")]
        [SerializeField] private Vector3 normalAttackOffset = new Vector3(0.3f, 1.1f, -0.08f);
        [SerializeField] private Vector3 shoryukenOffset = new Vector3(0.12f, 0f, -0.08f);
        [Tooltip("命中判定の接触点からの追加位置です。Xは攻撃方向に応じて反転します。")]
        [SerializeField] private Vector3 contactOffset = new Vector3(0f, 0f, -0.08f);
        [Tooltip("通常攻撃をキャラクター原点の子にして移動に追従させます。粒子の軌跡はGraph内で生成します。")]
        [SerializeField] private bool attachNormalAttackToFighter = true;

        [Header("再生後のオブジェクト回収（秒）")]
        [Tooltip("発生・消散のタイミングはGraph側で設定します。回収時間はGraphの最長寿命より長くしてください。")]
        [Min(0.05f)] [SerializeField] private float normalAttackLifetime = 0.65f;
        [Min(0.05f)] [SerializeField] private float shoryukenLifetime = 1.5f;
        [Min(0.05f)] [SerializeField] private float hitLifetime = 1f;
        [Min(0.05f)] [SerializeField] private float guardLifetime = 1f;
        [Min(0.05f)] [SerializeField] private float hadoukenImpactLifetime = 1.3f;

        public GameObject NormalAttackPrefab => normalAttackPrefab;
        public GameObject ShoryukenPrefab => shoryukenPrefab;
        public GameObject HitPrefab => hitPrefab;
        public GameObject GuardPrefab => guardPrefab;
        public GameObject HadoukenPrefab => hadoukenPrefab;
        public Vector3 NormalAttackOffset => normalAttackOffset;
        public Vector3 ShoryukenOffset => shoryukenOffset;
        public Vector3 ContactOffset => contactOffset;
        public bool AttachNormalAttackToFighter => attachNormalAttackToFighter;
        public float NormalAttackLifetime => normalAttackLifetime;
        public float ShoryukenLifetime => shoryukenLifetime;
        public float HitLifetime => hitLifetime;
        public float GuardLifetime => guardLifetime;
        public float HadoukenImpactLifetime => hadoukenImpactLifetime;

        public bool IsShoryuken(FighterAttackDefinition attack)
        {
            return MatchesAction(attack, shoryukenActionIds);
        }

        public bool IsHadouken(FighterAttackDefinition attack)
        {
            return MatchesAction(attack, hadoukenActionIds);
        }

        private static bool MatchesAction(FighterAttackDefinition attack, string[] actionIds)
        {
            if (attack == null || actionIds == null) return false;
            for (int i = 0; i < actionIds.Length; i++)
            {
                if (string.Equals(attack.ActionId, actionIds[i], StringComparison.Ordinal)) return true;
            }
            return false;
        }

        private void OnValidate()
        {
            normalAttackLifetime = Mathf.Max(0.05f, normalAttackLifetime);
            shoryukenLifetime = Mathf.Max(0.05f, shoryukenLifetime);
            hitLifetime = Mathf.Max(0.05f, hitLifetime);
            guardLifetime = Mathf.Max(0.05f, guardLifetime);
            hadoukenImpactLifetime = Mathf.Max(0.05f, hadoukenImpactLifetime);
        }
    }
}
