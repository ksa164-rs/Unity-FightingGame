# GASG Fighter 実装ガイド

## 目的

このプロトタイプは、1対1対戦格闘に必要な「入力・戦闘フレーム・表示」の境界を保ち、デザイナーが数値とAnimation Clipを安全に調整できることを優先します。

## Runtimeの責務

| 型 | 責務 | 持たせないもの |
|---|---|---|
| `FightMatchManager` | 60Hz進行、ラウンド、ヒットストップ、両者の更新順、同時ヒット確定 | 個別キャラの技データ、Animator Stateの詳細 |
| `FighterController` | 1体分の状態遷移、移動、攻撃、ガード、被弾 | ラウンドルール、HUD生成、Animator内部検索、判定線生成 |
| `FighterPresentation` | モデルの向き、Animatorパラメータ、Hitbox/Hurtbox可視化 | ダメージ計算、状態遷移、入力判定 |
| `FighterInputSource` | デバイス入力の収集 | 技成立条件、キャンセル規則 |
| `FighterCommandBuffer` | ボタン入力を戦闘フレーム単位で短時間保持 | デバイスAPI、技選択 |
| `FightCameraController` | 2体を収めるカメラ表示 | 戦闘状態の変更 |
| `FightHud` | 試合状態の表示 | 試合ルールの決定 |

`FighterPresentation`と`FighterCommandBuffer`はUnityコンポーネントではありません。Scene上の部品を増やさず、責務だけを分離するための内部クラスです。

## フレーム規約

- ゲームプレイは`FightFrameTiming.SimulationRate`（60Hz）を唯一の基準にします。
- Unityの描画fpsと`Fixed Timestep`は戦闘フレームの基準にしません。
- 攻撃開始時の内部フレームは0です。`startupFrames = 5`なら、0〜4が発生前、5から攻撃判定が有効です。
- Hitboxの開始・終了は両端を含みます。
- ヒットストップ中は戦闘状態、ラウンドタイマー、Animatorを同じ整数フレーム数だけ停止します。
- 両者の攻撃接触を先に収集し、その後でダメージを適用します。同一フレームの相打ちを順序依存にしません。
- 大きな描画遅延時の一度の追いつきは4フレームまでです。無制限な追いつきによる操作不能を避けます。

## Animator運用

- 現段階では全身動作用のBase Layerを1つ使います。攻撃ごとにLayerを増やしません。
- Animation Clipは`FighterActionCatalog`で固定Action IDへ割り当てます。Clip名をIDとして使用しません。
- Idleなどの基本動作は`FighterAnimationProfile`、攻撃は`FighterAttackDefinition`からActionを参照します。
- Animator State、Parameter、TransitionはAction Catalogから同期します。手作業で同じStateや遷移を重複作成しません。
- 各攻撃Stateの再生速度は、Animation Clipの尺が攻撃データの総フレームへ一致するよう自動計算します。
- 攻撃開始遷移とIdle復帰遷移のブレンドは0秒です。ゲームプレイ上の発生とポーズをずらさないためです。
- 上半身差分、表情、追加リアクションが必要になった時だけ、Avatar Mask付きの追加Layerを検討します。
- Animatorは表示結果です。Animation Eventでダメージや判定フレームを決めないでください。

## データの分け方

- Action ID、表示名、Animation Clipは`FighterActionDefinition`、一覧は`FighterActionCatalog`が所有します。
- 基本状態とActionの対応は`FighterAnimationProfile`が参照します。
- 攻撃のフレーム、判定、ダメージは`FighterAttackDefinition`にまとめ、Animation Clip自体はAction参照として分離します。
- キャラクター共通値は`FighterConfig`、技一覧は`FighterAttackDatabase`が所有します。
- Runtimeコードへキャラクター固有の数値を追加せず、Inspectorで調整できるデータへ置きます。
- 空中横入力倍率、ガード時の押し戻し倍率、押し戻し減速は`FighterConfig`の「空中操作・押し戻し」で調整します。
- `AttackMotionId`は既存Editor UIとの互換用です。Runtimeの技選択は、安定した文字列ID、使用状態、方向コマンド、ボタン、優先度を持つ`FighterMoveBinding`リストを優先します。リストが空の旧Databaseだけ固定5技へフォールバックします。
- 方向入力はキャラクター相対のテンキー表記で30戦闘フレーム保持します。コマンド成立後は使用した方向列を消費し、古い入力による再発動を防ぎます。

## 次に分割する条件

2026-09-07の点検結果と調整順序は[対戦システムレビュー](COMBAT_REVIEW_2026_09_07.md)を参照してください。飛び道具は`FightMatchManager`へ登録し、キャラクターと同じ60Hzで移動・接触・寿命を更新します。`FighterProjectile.Update`に戦闘処理を追加しないでください。命中VFXなど演出の実時間とは区別します。

攻撃Databaseの編集対象はRuntimeと同じBinding一覧から選択します。`Validate Combat Data (Read Only)`で技ID、入力、Action、Hitbox期間の矛盾を検証できます。発生・持続とHitboxの期間は現段階では別フィールドのため、変更後に検証してください。

同じAction IDに異なる総フレームまたはClipが割り当てられている場合、Animator同期は変更前に中止します。別Actionなら同じClipを使用しても別Stateを保持し、Clip一致だけで既存Stateを改名しません。

`FighterController`をさらに分割するのは、次のいずれかが実装される時です。

- 投射物や多段技により攻撃解決を複数キャラクターで共有する
- 立ち／しゃがみ／空中を含む多数の技コマンドを扱う
- ロールバック通信のため、Unity Transformから独立した状態スナップショットが必要になる
- ステージギミックや壁／画面端バウンドを汎用移動系として共有する

現段階で移動・状態遷移・攻撃を別MonoBehaviourへ細分化すると、更新順と参照設定が増えるため行いません。
