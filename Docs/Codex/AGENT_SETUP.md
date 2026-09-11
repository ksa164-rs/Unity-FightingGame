# UnityプロジェクトのCodexエージェント設定

2026-09-10作成。対象はこのプロジェクト内の設定整備と読み込み確認。ゲーム実装・アセット・Scene・Prefabは変更していない。

## 用意した役割とファイル

親は `project_lead` として動く。専門6役は必要な時だけ使用する。

| 役割 | 定義・運用場所 | 主な担当 |
| --- | --- | --- |
| project_lead | [AGENTS.md](../../AGENTS.md) | 依頼整理、分担、モデル選択、統合、完了確認 |
| architecture | [.codex/agents/architecture.toml](../../.codex/agents/architecture.toml) | 全体設計、データ・状態・フレーム基盤、移行判断 |
| combat | [.codex/agents/combat.toml](../../.codex/agents/combat.toml) | 入力と攻防、コンボ、ゲージ、ラウンド、トレーニング |
| animation_ta | [.codex/agents/animation_ta.toml](../../.codex/agents/animation_ta.toml) | モーション・リグ・再生、反転、Root Motion、判定同期 |
| presentation | [.codex/agents/presentation.toml](../../.codex/agents/presentation.toml) | HUD、VFX、カメラ、SE、実画面比較 |
| asset_tools | [.codex/agents/asset_tools.toml](../../.codex/agents/asset_tools.toml) | 参照、インポート、命名、Editorツール、Git管理対象 |
| qa_performance | [.codex/agents/qa_performance.toml](../../.codex/agents/qa_performance.toml) | 再現・退行・差分・フレーム・性能・調整性の確認 |

モデル設定は [.codex/config.toml](../../.codex/config.toml)、判断ルールは [MODEL_ROUTING.md](MODEL_ROUTING.md)。共通ルールと役割定義を分離したため、役割ファイルを書き換えずに作業ごとにモデルを選べる。

## 実際に設定した内容

```toml
model = "gpt-5.6-terra"
model_reasoning_effort = "medium"

[agents]
enabled = true
max_concurrent_threads_per_session = 2
default_subagent_model = "gpt-5.6-terra"
default_subagent_reasoning_effort = "medium"
```

同時数2は親を除く。専門6役のTOMLには `name`、`description`、`developer_instructions` だけを定義し、モデル・推論・権限・MCPを固定していない。起動時の明示指定がない専門役はすべてTerra / mediumになる。architectureをSolにする、棚卸しだけLunaにする等の判断と実際の起動指定は親の仕事であり、役割名からの自動モデル切替機構を追加したものではない。

通常実装はTerra、確定済みの小調整・明確な一覧化はLuna、複雑な相互作用はSol、重大な全体設計はAstra。理由と実モデルを作業開始時に示す。推論は通常medium、必要な設計・検証のみhigh等へ変更する。

## 次回使う時の操作

1. Codexアプリでこのプロジェクトの作業を開始し、入力欄のモデルを **GPT-5.6 Terra / Medium** に合わせる。既存タスクを続ける場合も、現在の選択を確認する。
2. 普段どおり作業を依頼する。親が `AGENTS.md` に従い、必要な役割だけを選ぶ。毎回6役を手動起動する必要はない。

依頼例：

```text
project_leadとして、先行入力がヒットストップ中に失われる問題を調べてください。
必要ならcombatにSol / mediumを指定し、.codex/agents/combat.tomlの役割指示を渡してください。
最初に再現条件を確認し、編集対象とUnity操作担当を決め、修正後に関連する退行を確認してください。
```

現在の親モデルは設定ファイル編集では変わらない。この設定整備は既存のAstraタスク内で実施した。次回以降の通常開始をTerraへ下げるには上記UI選択を確認する。アプリが保持するセッション指定がプロジェクト既定値より優先される場合がある。

CLIで明示的に開始する場合、この端末で検証した同梱実行ファイルは次のとおり。以下のコマンドは作業開始用で、今回の検証では実行していない。

```powershell
& 'C:\Users\ksato\AppData\Local\OpenAI\Codex\bin\8e5b6932251c2c1c\codex.exe' `
  -C 'E:\study\Unity\fighting games' `
  -m gpt-5.6-terra `
  -c 'model_reasoning_effort="medium"'
```

アプリ更新でパスが変わる場合は `Get-Command codex -All` と `--version` で確認する。PATH先頭のnpm版は今回 `0.145.0`、検証に使ったアプリ同梱版は `0.153.4` だった。npm版を更新・削除したりPATHを変更したりはしていない。古い版で設定が無視される場合は、検証済み同梱版かアプリを使う。

## 対応状況と読み込み確認

| 項目 | 結果 |
| --- | --- |
| 実行環境 | Codex Desktop。同梱CLI / App Server `0.153.4` |
| プロジェクト信頼 | 既存のtrusted登録あり。登録は変更していない |
| 設定レイヤー | App Server `config/read` でプロジェクト `.codex` レイヤーの有効化を確認 |
| 親の既定値 | 実効設定 `gpt-5.6-terra` / `medium`、出所はプロジェクト設定 |
| 子の既定値・同時数 | 実効 `[agents]` がTerra / medium / 2、enabled=true |
| モデル利用候補 | `model/list` の表示対象としてAstra、Sol、Terra、Lunaの正確なIDと推論設定を確認 |
| 共通指示 | `debug prompt-input` の生成入力に新規AGENTS.md全文が含まれることを確認 |
| 6役のファイル | 必須キー、名前の一意性、ファイル名との一致、TOML構文、モデル固定なしを確認 |
| 明示的なモデル委任 | 定義レビューを `gpt-5.6-terra` / `medium` 指定のサブエージェントへ委任し、結果を受領 |
| カスタム役割名の直接起動 | 未検証。現在公開されている起動APIには役割名を選択する引数がなく、役割名による自動発見・適用は実証していない |
| 現在の親の切替 | 未実施。設定編集による即時切替とは扱わない |
| ゲーム動作・性能 | 設定整備のため新たな再生・コンパイル・テスト・負荷測定は実施していない |

現在の起動APIでは、親が該当TOMLの `developer_instructions` を読み、委任メッセージに役割指示と目的・対象・完了条件を渡し、`model` と `reasoning_effort` を明示する運用を使う。存在しない `agent_type` 等の引数は使わない。全文履歴フォークがモデル指定を許可しない場合は、必要範囲の履歴か履歴なし＋引継ぎを選ぶ。この方法なら、役割名で直接起動できなくても保存した6役を利用できる。

正式な名前指定に対応するクライアントでは `.codex/agents/` の6役を使用する。役割ファイルの固定モデルが起動指定より優先されるため、今後もモデル・推論を役割ファイルへ固定しない。詳しい優先順位は `MODEL_ROUTING.md` に記載。

ローカル検証結果は `ReviewValidation/CodexAgentSetup/verification.json`、再確認用の一回用スクリプトは同フォルダーの `verify_agent_setup.py`。このスクリプトは設定とモデルカタログを読むだけで、モデルの推論や新規タスクを開始しない。アプリ同梱CLIパスを引数にして使う。カスタム役割の直接起動を検証するものではない。

## 確認したUnity環境（2026-09-10時点）

| 項目 | 確認値・根拠 |
| --- | --- |
| Unity | `6000.3.11f1`、ProjectSettings/ProjectVersion.txt |
| 実効Render Pipeline | URP、Assets/Settings/PC_RPAsset.asset、品質PC。MCP render_pipeline_info |
| 描画環境 | Linear、Direct3D12、Windows64 |
| URP / VFX Graph / Shader Graph | 17.3.0 |
| Input System | 1.19.0、activeInputHandler=1 |
| Timeline / Test Framework / Visual Scripting | 1.8.11 / 1.6.0 / 1.9.10 |
| Cinemachine | manifest/lockに登録なし。現行カメラはFightCameraController |
| Unity MCP | 4.2.0、読取接続成功。Editor停止・非コンパイル |
| 開いているシーン | Assets/GASGFighter/Scenes/TrainingRoom.unity、Active、isDirty=false、ルート7個 |
| Build Settings | SampleSceneとLocalVersusPrototypeが有効。TrainingRoomは未登録 |
| Console | 調査時点でエラー0、警告4、通常ログ1。警告本文は未調査 |

パッケージの根拠はPackages/manifest.json、packages-lock.json、MCP project_packages。環境情報はスナップショットなので実装前に必要範囲を再確認する。接続確認以外のUnity操作は行っていない。

既存GASGメニューはC#のMenuItem宣言18件に同一メニュー文字列の重複なし。Animation Action Manager（モーションID・表示名・Clip・基本動作）、Attack Motion Database（攻撃値）、Hitbox Editor（判定形状）、Camera Tuning（カメラ設定）、Combat Data Validator、VFX Builder、照明調整、シーン生成が存在する。

`FightAttackDatabaseMigration.cs` の **Open Attack Motion Database** はDBがない場合に作成・保存・ImportAsset・Animator同期を行う。読取調査で安易に実行しない。この注意点はasset_toolsにも記載した。既存メニューの追加・変更は行っていない。

## 既存データ・保存範囲

- 新規の運用ファイルは `AGENTS.md`、本書、`MODEL_ROUTING.md`、`.codex/config.toml`、専門6役のTOMLの計10ファイル。
- 既存指示書、個人AGENTS.md、個人config.toml、権限設定、MCP設定、.gitignoreは変更していない。元の設計原則は新規AGENTS.mdへ統合した。
- `.codex/` と `ReviewValidation/` は既存.gitignoreによりローカル管理。Git上で見える追加はAGENTS.mdとDocs/Codex配下。commitはしていない。
- 他のPCやプロジェクトで使う場合は、このローカル設定7ファイルも別途コピーする必要がある。チーム共有のための.gitignore変更・Git追跡は今回の対象外。
- 復旧する場合は今回新規作成した設定だけを退避できる。アセットや個人設定の復元は不要。

形式・優先順位は作業日に取得した[公式Subagents](https://learn.chatgpt.com/docs/agent-configuration/subagents)、[公式Config basics](https://learn.chatgpt.com/docs/config-file/config-basic)に基づく。実効設定・利用モデルは[App Server](https://learn.chatgpt.com/docs/app-server)の読取APIで確認した。説明と現在の公開ツールに差がある部分は上記の未検証・代替運用として区別した。
