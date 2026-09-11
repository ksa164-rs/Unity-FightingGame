# アニメーション命名・管理ルール

## 管理IDの形式

`AS_p01_200` の3セグメント形式を使用します。

- `AS`: Animation
- `p01`: キャラクターID
- `200`: モーション番号

モーション番号の百の位で用途を予約します。末尾の連番セグメントは使用しません。

| 範囲 | 用途 |
|---|---|
| 000–099 | 基本動作 |
| 100–199 | ダメージ系（ガード、被弾、投げなど） |
| 200–299 | 攻撃（通常技、必殺技、特技） |
| 300–399 | 超必殺技 |
| 400–499 | 超必殺技やられ |

## IDの役割

| 種別 | 例 | 用途 |
|---|---|---|
| Clip管理ID | `AS_p01_200` | Animation Clipの固定管理番号 |
| Action ID | `StandingLightPunch` | ゲーム、Animator、VFX用の意味名 |
| 表示名 | `立ち弱P` | コマンド表などプレイヤー向けの名前 |

## p01 現行一覧

| Clip管理ID | Action ID | 表示名 |
|---|---|---|
| AS_p01_000 | Idle | 待機 |
| AS_p01_001 | CrouchIdle | しゃがみ待機 |
| AS_p01_004 | Walking | 歩き |
| AS_p01_005 | ForwardStep | 前ステップ |
| AS_p01_006 | BackwardStep | 後ろステップ |
| AS_p01_100 | Block | ガード |
| AS_p01_101 | LightHit | 被弾 |
| AS_p01_102 | Knockdown | ダウン |
| AS_p01_103 | Thrown | 投げられ |
| AS_p01_104 | ForwardThrow | 前投げ |
| AS_p01_200 | StandingLightPunch | 立ち弱P |
| AS_p01_201 | StandingMediumPunch | 立ち中P |
| AS_p01_202 | StandingHeavyPunch | 立ち強P |
| AS_p01_203 | CrouchingLightPunch | 下段（弱） |
| AS_p01_204 | CrouchingMediumPunch | 下段（中） |
| AS_p01_205 | CrouchingHeavyPunch | 下段（強） |
| AS_p01_206 | Hadouken | 波動拳 |
| AS_p01_207 | LightShoryuken | 昇龍拳（弱） |

`Jump`、`Land`、`RoundReset` は現時点でAnimation Clip未設定です。Clip管理IDはClipを持つ動作だけに付与します。