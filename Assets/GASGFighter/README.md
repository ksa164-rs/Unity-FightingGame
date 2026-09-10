# GASG Local Versus Prototype

Unity 6000.3.11f1 / URP / Input System向けのローカル1対1プロトタイプです。

実装責務とフレーム規約は[ARCHITECTURE.md](ARCHITECTURE.md)を参照してください。

モーションの管理番号・表示名は[モーション命名ルール](MOTION_NAMING.md)を参照してください。既存17件を`AS_p01_000_001`形式へ採番済みです。モーション管理と攻撃編集の選択欄は「管理番号 / 表示名」を使用し、旧Action IDは内部接続用として保持します。

## シーン生成

Unityメニューから次を1回実行します。

`GASG > Fighting Game > Create Local Versus Prototype Scene`

生成先：`Assets/GASGFighter/Scenes/LocalVersusPrototype.unity`

既存のSampleSceneは変更しません。再実行時も既存のDataとMaterialアセットは保持します。

## 操作

| 操作 | Player 1 | Player 2 | Gamepad |
|---|---|---|---|
| 移動・ジャンプ・しゃがみ | WASD | 矢印キー | 左スティック / D-pad |
| 前・後ろステップ | 左右を素早く2回 | 左右を素早く2回 | 左スティック / D-padを左右へ素早く2回 |
| 弱攻撃 | J | Num 1 / 右Ctrl | PS5 □ / West |
| 中攻撃 | K | Num 2 / 右Shift | PS5 ✕ / South |
| 強攻撃 | L | Num 3 | PS5 ○ / East |
| 必殺技 | I | Num 5 | PS5 △ / North |
| 投げ | U | Num 0 / Enter | PS5 L1 / Left Shoulder |

方向コマンドはキャラクターの向きを基準にしたテンキー表記で判定します。現在の確認用Bindingでは、`623 + 弱攻撃`でも弱昇龍拳を出せます。従来の必殺技ボタン`I / Num 5 / △`も互換入力として使用できます。

ゲームパッドは1台目をPlayer 1、2台目をPlayer 2へ割り当てます。

## Player_001モデルと弱攻撃

プロトタイプシーンのPlayer 1 / 2には`Player_001.fbx`を割り当て済みです。弱攻撃時は`Attack_Light.asset`からAnimator Trigger `attack_001`を送り、`attack_001.anim`を再生します。

再適用が必要な場合はUnityメニューから次を実行できます。

`GASG > Fighting Game > Apply Player_001 Visual and attack_001`

## 新しい攻撃モーションをゲームで確認するフロー

1. Mayaなどから書き出したFBXを`Assets/GASGFighter/Graphics/3D/Chara/animations/`へ配置します。
2. UnityでFBXのAnimation Clipを確認し、必要なら`.anim`として取り出します。Player_001と同じ骨階層・Generic設定を使用してください。
3. 初回だけ`GASG > Fighting Game > Create or Update Animation Action Catalog`を実行し、既存モーションをAction ID管理へ移行します。
4. `GASG > Fighting Game > Open Animation Action Manager`を開きます。
5. 対象Action IDの「Animation Clip」へ`.anim`を割り当てます。新規IDの場合は画面下部からActionを追加します。
6. `GASG > Fighting Game > Open Attack Motion Database`を開き、「編集する攻撃」の「再生Action」でAction IDを選択します。
7. 入力は同Databaseの「技ID・コマンド・状態」、発生・持続・後隙、HitStop、Hitboxなどは攻撃データ側で設定します。
8. Action ManagerまたはAttack Motion Databaseの「Animatorへ反映・保存」を押し、`LocalVersusPrototype`をPlayして対応ボタンで確認します。

Action IDは固定IDです。Clip名や表示名を変更してもAction IDは変更しません。Animator Controllerを手作業で編集する必要はありません。

## 待機・しゃがみ・ジャンプ等の登録

`GASG > Fighting Game > Open Animation Action Manager`の「基本Actionの役割」で、待機、しゃがみ待機、ジャンプ、着地、ステップ、ガード、被弾、ダウンなどに使用するActionを選択します。各ActionへAnimation Clipを割り当てて「Animatorへ反映・保存」を押すと、必要なParameter、State、Transitionが同期されます。

## 3Dモデルへの差し替え

1. `Player_1/Visual_Placeholder_REPLACE_ME` またはPlayer 2側を複製してバックアップします。
2. FBXモデルを各Playerルートの子へ配置し、足元がローカルY=0になるよう調整します。
3. `FighterController` の `Visual Root` と `Animator` を新しいモデルへ差し替えます。
4. モデルの正面はローカル+Zを想定しています。左向きでは表示モデルのローカルXスケールを反転し、さらにY軸へ180度の補正回転を適用します。回転とミラー倍率は`FighterController`の「見た目の向き」から調整できます。
5. Hurtboxは子の`Hurtbox`にあるBoxColliderで調整します。

1P／2Pの識別色は`FighterController > プレイヤー識別色`で調整できます。元マテリアルは変更せず、各RendererのBase Colorへ実行時の乗算色を加えます。初期値は1Pが淡いシアン、2Pが淡い赤橙、強さ0.28です。

AnimatorパラメータはAction Managerから自動登録します。Controllerへ手動追加しないでください。

- Float: `MoveX`
- Bool: `Crouching`, `Grounded`
- Trigger: `attack_001`, `MediumAttack`, `HeavyAttack`, `SpecialAttack`, `Throw`, `ForwardStep`, `BackwardStep`, `Jump`, `Land`, `Block`, `Hit`, `Knockdown`, `Thrown`, `Reset`

## 調整データ

- キャラクター移動値・ダブルタップ受付・ステップ距離: `Assets/GASGFighter/Data/FighterConfig_Prototype.asset`
- ボタン入力の先行受付時間: 同Assetの「ボタン入力保持フレーム」（初期値6F）
- 攻撃モーション一括管理: `Assets/GASGFighter/Data/AttackMotionDatabase_Prototype.asset`
- 技ID・使用状態・方向コマンド・入力ボタン・解決優先度: 同Attack Motion Databaseの「技ID・コマンド・状態」
- カメラ構図: `Assets/GASGFighter/Data/FightCameraSettings_Prototype.asset`

攻撃フレームは描画fpsから独立した60Hz基準です。赤い攻撃判定は、Play中にFighterを選択するとSceneビューのGizmoとして確認できます。

## ヒットボックス・ヒットストップ調整

通常の攻撃調整は`GASG > Fighting Game > Open Attack Motion Database`から開き、Inspector上部の「編集する攻撃」で`LightAttack / MiddleAttack / HeavyAttack / SpecialAttack / Throw`を切り替えます。選択中の攻撃についてAnimation Clip、Animator Trigger、ダメージ、全フレーム、キャンセル、HitStop、硬直、ノックバック、ガード属性、Hitbox、投げ・ダウン設定を一括編集できます。「アセットを保存」を押すとAnimation ClipとAnimator Controllerも同期します。

Unityメニューから次を開きます。

`GASG > Fighting Game > Open Hitbox Editor`

Attackアセットを1モーションとして選択し、以下を調整できます。

- 発生・持続・後隙フレーム
- 攻撃全体フレームと通常行動へ戻れるタイミング
- 攻撃キャンセルの受付開始・終了フレーム
- ヒット／ガード時だけキャンセル可能にする設定
- キャンセル先（弱・中・強・必殺技・投げ）
- Hitboxの判定名
- 判定の開始・終了フレーム
- 中心位置とサイズ
- 1モーション内の複数Hitbox
- ダメージ
- ヒットストップ（60fps基準フレーム）
- ヒット時カメラシェイク（位置振幅・回転振幅・継続フレーム・周波数）
- ヒット硬直・ガード硬直・ノックバック

Sceneビューでは赤=Hitbox、緑=Hurtbox、黄=編集中のHitboxとして表示します。Play中は`FighterController`の「判定表示」からGameビュー表示を切り替えられます。ヒットストップ中は`Time.timeScale`を変更せず、両キャラクター、Animator、ラウンドタイマーが指定した整数フレーム数だけ停止します。

`後隙フレーム`を増やすほど攻撃後に動けない時間が長くなります。キャンセルを使わない攻撃は「攻撃キャンセルを有効化」をOFFにしてください。キャンセルをONにした場合、指定した受付フレーム内で選択済みの攻撃ボタンを押すと次のモーションへ移行します。

## カメラ調整

Unityメニューから次を開きます。

`GASG > Fighting Game > Open Camera Tuning`

「寄り」「標準」「引き」のプリセットを選んだ後、画角・高さ・距離・ジャンプ追従・追従速度を日本語スライダーで調整できます。Play中の変更はGameビューへ即時反映され、Undoにも対応しています。
