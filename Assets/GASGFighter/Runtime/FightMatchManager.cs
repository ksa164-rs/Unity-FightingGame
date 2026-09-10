using System.Collections.Generic;
using UnityEngine;

namespace GASG.Fighting
{
    [DefaultExecutionOrder(100)]
    [DisallowMultipleComponent]
    public sealed class FightMatchManager : MonoBehaviour
    {
        private const int MaximumCatchUpFrames = 4;

        [Header("選手")]
        [SerializeField] private FighterController player1;
        [SerializeField] private FighterController player2;

        [Header("演出")]
        [SerializeField] private FightCameraController fightCamera;
        [Tooltip("未指定時はResources/FightIntroSettingsを使用します。")]
        [SerializeField] private FightIntroSettings introSettings;
        [Header("K.O.後フェード（Inspector調整用）")]
        [Tooltip("K.O.表示後、画面が完全な黒になるまでの時間です。")]
        [Min(0.05f)] [SerializeField] private float knockoutFadeOutSeconds = 0.35f;
        [Tooltip("位置リセット完了後、完全な黒からゲーム画面へ戻る時間です。")]
        [Min(0.05f)] [SerializeField] private float knockoutFadeInSeconds = 0.35f;
        [Tooltip("位置リセット完了後も完全な黒を維持する時間です。切り替わりが見える場合は長くします。")]
        [Min(0f)] [SerializeField] private float knockoutBlackoutHoldSeconds = 0.75f;
        [Tooltip("勝利表示に使う名前。空欄の場合はPLAYER 1 / PLAYER 2。")]
        [SerializeField] private string player1DisplayName = "PLAYER 1";
        [SerializeField] private string player2DisplayName = "PLAYER 2";

        [Header("試合ルール")]
        [Min(10)] [SerializeField] private int roundTimeSeconds = 99;
        [Range(1, 5)] [SerializeField] private int roundsToWin = 2;
        [Min(0.5f)] [SerializeField] private float roundStartDelaySeconds = 1.2f;
        [Min(0.5f)] [SerializeField] private float roundEndDelaySeconds = 2.0f;
        [SerializeField] private Vector3 player1Start = new Vector3(-2.5f, 0f, 0f);
        [SerializeField] private Vector3 player2Start = new Vector3(2.5f, 0f, 0f);

        private int roundFramesRemaining;
        private int transitionFramesRemaining;
        private int hitStopFramesRemaining;
        private int centerMessageFramesRemaining;
        private float simulationAccumulator;
        private bool waitingToStartRound;
        private bool optionsPaused;
        private int introDurationFrames;
        public enum ResultStage { None, Knockout, Pause, Winner, Complete }
        public ResultStage ResultPhase { get; private set; }
        public int WinnerPlayerIndex { get; private set; }
        public string RoundEndMessage { get; private set; } = string.Empty;
        private int resultFramesRemaining;
        private int resultDurationFrames;
        private int roundResetFadeInFramesRemaining;
        private int roundResetFadeInDurationFrames;
        private int roundResetFadeInDelayFrames;
        public bool IsFinalRound => Player1Rounds == roundsToWin - 1 && Player2Rounds == roundsToWin - 1;
        public bool KnockoutActive => ResultPhase == ResultStage.Knockout;
        public bool WinnerActive => ResultPhase == ResultStage.Winner;
        public bool CanRematch => MatchOver && ResultPhase == ResultStage.Complete;
        public float ResultProgress => resultDurationFrames > 0
            ? Mathf.Clamp01(1f - resultFramesRemaining / (float)resultDurationFrames) : 0f;
        public bool RoundResetFadeInActive => roundResetFadeInFramesRemaining > 0;
        public float RoundResetFadeInProgress => roundResetFadeInDurationFrames > 0
            ? Mathf.Clamp01(1f - Mathf.Min(roundResetFadeInFramesRemaining, roundResetFadeInDurationFrames) / (float)roundResetFadeInDurationFrames) : 1f;
        public string WinnerDisplayName => WinnerPlayerIndex == 1
            ? (string.IsNullOrWhiteSpace(player1DisplayName) ? "PLAYER 1" : player1DisplayName.Trim())
            : WinnerPlayerIndex == 2
                ? (string.IsNullOrWhiteSpace(player2DisplayName) ? "PLAYER 2" : player2DisplayName.Trim()) : string.Empty;
        private readonly List<FighterProjectile> projectiles = new List<FighterProjectile>();

        public FighterController Player1 => player1;
        public FighterController Player2 => player2;
        public bool RoundActive { get; private set; }
        public bool MatchOver { get; private set; }
        public int Player1Rounds { get; private set; }
        public int Player2Rounds { get; private set; }
        public int RoundNumber { get; private set; } = 1;
        public int DisplayTimeSeconds => Mathf.Max(
            0,
            Mathf.CeilToInt(roundFramesRemaining / (float)FightFrameTiming.SimulationRate));
        public string CenterMessage { get; private set; } = string.Empty;
        public bool HitStopActive => hitStopFramesRemaining > 0;
        public bool OptionsPaused => optionsPaused;
        public FightIntroSettings IntroSettings => introSettings;
        public float KnockoutFadeOutSeconds => knockoutFadeOutSeconds;
        public float KnockoutFadeInSeconds => knockoutFadeInSeconds;
        public float KnockoutBlackoutHoldSeconds => knockoutBlackoutHoldSeconds;
        public bool RoundIntroActive => waitingToStartRound && !RoundActive && !MatchOver && transitionFramesRemaining > 0;
        public bool FightIntroActive => RoundActive && centerMessageFramesRemaining > 0;
        public float IntroProgress => introDurationFrames > 0
            ? Mathf.Clamp01(1f - (RoundIntroActive ? transitionFramesRemaining : centerMessageFramesRemaining) / (float)introDurationFrames)
            : 0f;

        private void Awake()
        {
            if (introSettings == null) introSettings = Resources.Load<FightIntroSettings>(FightIntroSettings.ResourcePath);
            if (introSettings == null)
                Debug.LogWarning("[GASG Fighter][スキップ] 開始演出設定がないため、文字表示と既存の待ち時間を使用します。", this);
        }

        private void Start()
        {
            if (player1 == null || player2 == null)
            {
                Debug.LogError("[GASG Fighter] Player 1またはPlayer 2が設定されていません。", this);
                enabled = false;
                return;
            }

            if (fightCamera == null)
            {
                fightCamera = FindFirstObjectByType<FightCameraController>();
            }

            player1.Initialize(this, player2);
            player2.Initialize(this, player1);
            BeginMatch();
        }

        private void Update()
        {
            if (player1 == null || player2 == null)
            {
                return;
            }

            if (optionsPaused)
            {
                simulationAccumulator = 0f;
                return;
            }

            if (CanRematch)
            {
                bool restart = (player1.InputSource != null && player1.InputSource.IsRestartPressed()) ||
                               (player2.InputSource != null && player2.InputSource.IsRestartPressed());
                if (restart)
                {
                    BeginMatch();
                }
            }

            // 描画fpsやProject SettingsのFixed Timestepに依存せず、対戦だけを60Hzで進める。
            float elapsed = Mathf.Min(Time.deltaTime, FightFrameTiming.FrameDuration * MaximumCatchUpFrames);
            simulationAccumulator += elapsed;
            int processedFrames = 0;
            while (simulationAccumulator >= FightFrameTiming.FrameDuration && processedFrames < MaximumCatchUpFrames)
            {
                simulationAccumulator -= FightFrameTiming.FrameDuration;
                processedFrames++;
                SimulateMatchFrame();
            }
        }

        private void SimulateMatchFrame()
        {
            if (optionsPaused)
            {
                return;
            }
            if (HitStopActive)
            {
                hitStopFramesRemaining--;
                if (!HitStopActive)
                {
                    SetFighterPresentationPaused(optionsPaused);
                }

                return;
            }

            if (RoundActive)
            {
                player1.SimulateFrame();
                player2.SimulateFrame();
                ResolvePushboxes();
                ResolveAttackContacts();
                roundFramesRemaining--;

                if (centerMessageFramesRemaining > 0)
                {
                    centerMessageFramesRemaining--;
                    if (centerMessageFramesRemaining == 0)
                    {
                        CenterMessage = string.Empty;
                    }
                }

                if (player1.CurrentHealth <= 0 || player2.CurrentHealth <= 0 || roundFramesRemaining <= 0)
                {
                    FinishRound();
                }

                return;
            }

            if (ResultPhase != ResultStage.None)
            {
                AdvanceResult();
                return;
            }

            if (MatchOver || transitionFramesRemaining <= 0)
            {
                return;
            }

            if (roundResetFadeInFramesRemaining > 0)
            {
                roundResetFadeInFramesRemaining--;
            }

            transitionFramesRemaining--;
            if (transitionFramesRemaining > 0)
            {
                return;
            }

            if (waitingToStartRound)
            {
                StartRoundControl();
            }
            else
            {
                PrepareRound();
            }
        }

        private void BeginMatch()
        {
            Player1Rounds = 0;
            Player2Rounds = 0;
            RoundNumber = 1;
            MatchOver = false;
            simulationAccumulator = 0f;
            PrepareRound();
        }

        public void ResetMatch()
        {
            if (player1 == null || player2 == null)
            {
                Debug.LogWarning("[GASG Fighter][スキップ] Player参照が不足しているため、試合をリセットできません。", this);
                return;
            }

            BeginMatch();
            Debug.Log("[GASG Fighter][成功] オプションメニューから試合をリセットしました。", this);
        }

        public void SwapPlayerPositions()
        {
            if (player1 == null || player2 == null)
            {
                Debug.LogWarning("[GASG Fighter][スキップ] Player参照が不足しているため、位置を入れ替えられません。", this);
                return;
            }

            Vector3 player1Position = player1.transform.position;
            Vector3 player2Position = player2.transform.position;
            player1.transform.position = player2Position;
            player2.transform.position = player1Position;
            Physics2D.SyncTransforms();
            Debug.Log("[GASG Fighter][成功] Player 1とPlayer 2の位置を入れ替えました。", this);
        }

        public bool SwapPlayerInputDevices()
        {
            FighterInputSource player1Input = player1 != null ? player1.InputSource : null;
            FighterInputSource player2Input = player2 != null ? player2.InputSource : null;
            if (player1Input == null || player2Input == null)
            {
                Debug.LogWarning("[GASG Fighter][スキップ] Player 1またはPlayer 2の入力参照がないため、割り当てを交換できません。", this);
                return false;
            }

            int player1GamepadIndex = player1Input.GamepadIndex;
            player1Input.AssignGamepadIndex(player2Input.GamepadIndex);
            player2Input.AssignGamepadIndex(player1GamepadIndex);
            Debug.Log("[GASG Fighter][成功] Player 1とPlayer 2のコントローラー割り当てを交換しました。", this);
            return true;
        }

        public void SetOptionsPaused(bool paused)
        {
            if (optionsPaused == paused)
            {
                return;
            }

            optionsPaused = paused;
            simulationAccumulator = 0f;
            SetFighterPresentationPaused(optionsPaused || HitStopActive);
        }

        private void PrepareRound()
        {
            bool resetAfterResult = ResultPhase != ResultStage.None;
            ResultPhase = ResultStage.None;
            resultFramesRemaining = 0;
            WinnerPlayerIndex = 0;
            RoundEndMessage = string.Empty;
            ClearProjectiles();
            RoundActive = false;
            CancelHitStop();
            waitingToStartRound = true;
            roundFramesRemaining = roundTimeSeconds * FightFrameTiming.SimulationRate;
            transitionFramesRemaining = FightFrameTiming.SecondsToFrames(
                introSettings != null ? Mathf.Max(0.5f, IsFinalRound ? introSettings.finalRoundSeconds : introSettings.roundSeconds) : roundStartDelaySeconds);
            introDurationFrames = transitionFramesRemaining;
            roundResetFadeInDurationFrames = resetAfterResult
                ? FightFrameTiming.SecondsToFrames(Mathf.Max(0.05f, knockoutFadeInSeconds))
                : 0;
            roundResetFadeInDelayFrames = resetAfterResult
                ? FightFrameTiming.SecondsToFrames(Mathf.Max(0f, knockoutBlackoutHoldSeconds))
                : 0;
            roundResetFadeInFramesRemaining = roundResetFadeInDelayFrames + roundResetFadeInDurationFrames;
            centerMessageFramesRemaining = 0;
            CenterMessage = IsFinalRound ? "FINAL ROUND" : $"ROUND {RoundNumber}";
            player1.ResetForRound(player1Start);
            player2.ResetForRound(player2Start);
        }

        public void RequestHitStop(int frames)
        {
            if (!RoundActive || frames <= 0)
            {
                return;
            }

            hitStopFramesRemaining = Mathf.Max(hitStopFramesRemaining, frames);
            SetFighterPresentationPaused(true);
        }

        public void RequestHitEffect(FighterAttackDefinition attack)
        {
            if (attack == null || !RoundActive)
            {
                return;
            }

            RequestHitStop(attack.HitStopFrames);
            fightCamera?.RequestShake(
                attack.CameraShakeDurationFrames,
                attack.CameraShakePositionAmplitude,
                attack.CameraShakeRotationAmplitude,
                attack.CameraShakeFrequency);
        }

        private void CancelHitStop()
        {
            hitStopFramesRemaining = 0;
            SetFighterPresentationPaused(optionsPaused);
        }

        private void OnDisable()
        {
            ClearProjectiles();
            // Play停止やコンポーネント無効化でもAnimatorの一時停止を確実に戻す。
            optionsPaused = false;
            CancelHitStop();
        }

        private void StartRoundControl()
        {
            waitingToStartRound = false;
            RoundActive = true;
            CenterMessage = "FIGHT";
            centerMessageFramesRemaining = FightFrameTiming.SecondsToFrames(
                introSettings != null ? Mathf.Max(0.2f, introSettings.fightSeconds) : 0.65f);
            introDurationFrames = centerMessageFramesRemaining;
            player1.SetRoundControl(true);
            player2.SetRoundControl(true);
        }

        private void FinishRound()
        {
            if (!RoundActive) return;
            RoundActive = false;
            waitingToStartRound = false;
            centerMessageFramesRemaining = 0;
            ClearProjectiles();
            player1.SetRoundControl(false);
            player2.SetRoundControl(false);

            bool p1Wins = player1.CurrentHealth > player2.CurrentHealth;
            bool p2Wins = player2.CurrentHealth > player1.CurrentHealth;

            if (p1Wins)
            {
                Player1Rounds++;
                WinnerPlayerIndex = 1;
            }
            else if (p2Wins)
            {
                Player2Rounds++;
                WinnerPlayerIndex = 2;
            }
            else
            {
                WinnerPlayerIndex = 0;
            }

            MatchOver = Player1Rounds >= roundsToWin || Player2Rounds >= roundsToWin;
            bool knockedOut = player1.CurrentHealth <= 0 || player2.CurrentHealth <= 0;
            RoundEndMessage = knockedOut ? (WinnerPlayerIndex == 0 ? "DOUBLE K.O." : "K.O.") : "TIME UP";
            transitionFramesRemaining = 0;
            BeginResultStage(ResultStage.Knockout, introSettings != null ? introSettings.knockoutSeconds : 1.25f);
        }

        private void BeginResultStage(ResultStage stage, float seconds)
        {
            ResultPhase = stage;
            resultDurationFrames = FightFrameTiming.SecondsToFrames(Mathf.Max(0.1f, seconds));
            resultFramesRemaining = resultDurationFrames;
            CenterMessage = stage == ResultStage.Knockout ? RoundEndMessage
                : stage == ResultStage.Winner ? WinnerDisplayName + "\nWIN"
                : WinnerPlayerIndex == 0 ? "DRAW" : string.Empty;
        }

        private void AdvanceResult()
        {
            if (ResultPhase == ResultStage.Complete || --resultFramesRemaining > 0) return;
            switch (ResultPhase)
            {
                case ResultStage.Knockout:
                    BeginResultStage(ResultStage.Pause, introSettings != null ? introSettings.resultPauseSeconds : roundEndDelaySeconds);
                    break;
                case ResultStage.Pause:
                    if (MatchOver)
                        BeginResultStage(ResultStage.Winner, introSettings != null ? introSettings.winSeconds : 2f);
                    else
                    {
                        RoundNumber++;
                        PrepareRound();
                    }
                    break;
                case ResultStage.Winner:
                    ResultPhase = ResultStage.Complete;
                    CenterMessage = WinnerDisplayName + " WIN\nPRESS ENTER / START TO REMATCH";
                    break;
            }
        }

        private void ResolveAttackContacts()
        {
            // Fighter移動・画面端Clamp・PushBox解決で確定したTransformを、
            // Auto Sync Transforms無効時も同じtickの2D接触照会へ一度だけ反映する。
            Physics2D.SyncTransforms();

            // 両者の接触候補を先に確定してから適用することで、同一フレームの相打ちを許可する。
            bool hasPlayer1Contact = player1.TryCollectAttackContact(out FighterAttackContact player1Contact);
            bool hasPlayer2Contact = player2.TryCollectAttackContact(out FighterAttackContact player2Contact);

            // 飛び道具も同じ60Hzで進め、全接触の検出をダメージ適用より先に終える。
            for (int i = projectiles.Count - 1; i >= 0; i--)
            {
                if (projectiles[i] == null)
                {
                    projectiles.RemoveAt(i);
                }
            }
            for (int i = 0; i < projectiles.Count; i++)
            {
                projectiles[i].SimulateFrame();
            }

            if (hasPlayer1Contact)
            {
                player1.ApplyAttackContact(player1Contact);
            }

            if (hasPlayer2Contact)
            {
                player2.ApplyAttackContact(player2Contact);
            }

            for (int i = 0; i < projectiles.Count; i++)
            {
                projectiles[i].ApplyPendingContact();
            }
        }

        public void RegisterProjectile(FighterProjectile projectile)
        {
            if (projectile != null && !projectiles.Contains(projectile))
            {
                projectiles.Add(projectile);
            }
        }

        private void ClearProjectiles()
        {
            // この試合で生成・登録された弾だけを片付ける。シーンの他のオブジェクトには触れない。
            for (int i = 0; i < projectiles.Count; i++)
            {
                if (projectiles[i] != null)
                {
                    projectiles[i].Retire();
                }
            }
            projectiles.Clear();
        }

        private void SetFighterPresentationPaused(bool paused)
        {
            player1?.SetPresentationPaused(paused);
            player2?.SetPresentationPaused(paused);
        }

        private void ResolvePushboxes()
        {
            if (!player1.IsGrounded || !player2.IsGrounded)
            {
                return;
            }

            float x1 = player1.transform.position.x;
            float x2 = player2.transform.position.x;
            float minimumDistance = player1.PushboxHalfWidth + player2.PushboxHalfWidth;
            float distance = Mathf.Abs(x2 - x1);
            if (distance >= minimumDistance)
            {
                return;
            }

            float direction = x2 >= x1 ? 1f : -1f;
            float correction = (minimumDistance - distance) * 0.5f;
            player1.SetPositionX(x1 - direction * correction);
            player2.SetPositionX(x2 + direction * correction);
        }

#if UNITY_EDITOR
        public void EditorConfigure(FighterController newPlayer1, FighterController newPlayer2)
        {
            player1 = newPlayer1;
            player2 = newPlayer2;
        }
#endif
    }
}
