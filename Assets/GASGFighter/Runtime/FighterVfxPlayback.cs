using UnityEngine;

namespace GASG.Fighting
{
    /// <summary>
    /// ゲームからのPrefab生成と回収だけを担当します。
    /// 粒子・発光・形状・速度・消散はVFX Graphに任せ、Updateやプロパティ書き換えは行いません。
    /// </summary>
    internal static class FighterVfxPlayback
    {
        private static FighterVfxLibrary library;
        private static bool libraryLoaded;

        public static FighterVfxLibrary Library
        {
            get
            {
                if (!libraryLoaded)
                {
                    library = Resources.Load<FighterVfxLibrary>(FighterVfxLibrary.ResourceName);
                    libraryLoaded = true;
                    if (library == null)
                    {
                        Debug.LogWarning("[GASG Fighter][SKIP] Resources/FighterVfxLibrary が未設定のため追加VFXを再生しません。");
                    }
                }
                return library;
            }
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetCache()
        {
            // Domain Reloadを無効にしたPlayでも古いResource参照を持ち越しません。
            library = null;
            libraryLoaded = false;
        }

        public static void PlayAttack(FighterController fighter, FighterAttackDefinition attack)
        {
            if (!Application.isPlaying || fighter == null || attack == null || attack.IsProjectile || attack.IsThrow) return;
            FighterVfxLibrary settings = Library;
            if (settings == null) return;

            bool rising = settings.IsShoryuken(attack);
            Vector3 offset = rising ? settings.ShoryukenOffset : settings.NormalAttackOffset;
            offset.x *= fighter.FacingDirection;
            Transform parent = !rising && settings.AttachNormalAttackToFighter ? fighter.transform : null;
            Spawn(rising ? settings.ShoryukenPrefab : settings.NormalAttackPrefab,
                fighter.transform.position + offset, fighter.FacingDirection,
                rising ? settings.ShoryukenLifetime : settings.NormalAttackLifetime, parent);
        }

        public static void PlayContact(Vector3 position, float direction, bool blocked)
        {
            if (!Application.isPlaying) return;
            FighterVfxLibrary settings = Library;
            if (settings == null) return;
            Vector3 offset = settings.ContactOffset;
            offset.x *= direction >= 0f ? 1f : -1f;
            Spawn(blocked ? settings.GuardPrefab : settings.HitPrefab, position + offset, direction,
                blocked ? settings.GuardLifetime : settings.HitLifetime, null);
        }

        private static void Spawn(GameObject prefab, Vector3 position, float direction, float lifetime, Transform parent)
        {
            if (prefab == null) return;
            Quaternion rotation = direction >= 0f ? Quaternion.identity : Quaternion.Euler(0f, 180f, 0f);
            GameObject instance = Object.Instantiate(prefab, position, rotation, parent);
            instance.name = prefab.name + "_Instance";
            // PrefabのOnPlayでGraphが自律再生されます。DestroyはGPU粒子の終了後の回収だけです。
            Object.Destroy(instance, Mathf.Max(0.05f, lifetime));
        }
    }
}
