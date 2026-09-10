# K.O.・FINAL ROUND・WIN演出

対応：Unity 6000.3.11f1 / URP 17.3 / uGUI。調整用プロトタイプ。

既存のFightHud / FightMatchManagerがあるSceneをPlayすると自動で有効になります。SceneやPrefabの再生成は不要です。

## 表示の流れ

- 体力ゼロ：K.O.を拡大状態から縮小して表示（1.25秒）→待ち時間（1.5秒）。
- 通常のラウンド勝利：待ち時間後、次のROUND → FIGHT。
- 両者があと1勝：番号付きROUNDの代わりに2段のFINAL / ROUND → FIGHT。1本先取では初回からFINAL ROUND。
- 試合の勝利：待ち時間後、勝者の名前 / WINを2段表示（2秒）→再試合案内。WIN表示が完了してからEnter / Startで再試合できます。
- 時間切れ：TIME UP。同時KO：DOUBLE K.O.。同点では勝数を加算せずDRAWを表示して次ラウンドへ進みます。

動画3本の文字の出現順・拡縮・待ち時間を参考にしています。音声、勝利モーション、専用カメラカットはこのUI追加に含まれません。新規画像の割り当てがなくても、K.O.は銀色の文字、FINAL ROUNDは紫の帯付き文字、名前とWINは白文字で動きます。元動画のロゴ素材を複製したものではありません。

## Inspectorでの調整

`Assets/GASGFighter/Resources/FightIntroSettings.asset` を選択します。

- **Final Round Seconds / Knockout Seconds / Result Pause Seconds / Win Seconds**：各表示と待ち時間。
- **Final Round Width / Knockout Width / Win Width**：1920px画面を基準にした表示幅。
- **Knockout Start Scale**：K.O.出現時の拡大率。
- **Winner Name Font Size / Winner Offset**：名前の最大文字サイズ、名前＋WINの表示位置。長い名前は自動縮小。
- **Final Round Image / Knockout Image / Win Image**：任意の差し替え画像。Win ImageにはWINの文字だけを指定します。名前は別表示です。空欄なら文字表示へ戻ります。
- 既存の**Entrance Fraction / Exit Fraction / Center Offset**も適用されます。

Scene内の**FightMatchManager**で **Player 1 Display Name / Player 2 Display Name** を変更すると勝利名を変更できます。未設定・空白ならPLAYER 1 / PLAYER 2です。位置や入力機器を交換しても選手番号との対応を保ちます。

## 確認

1. 通常のKO後に、WINを挟まず次のラウンドへ進む。
2. 2本先取で1対1にすると、FINAL ROUND → FIGHTになる。
3. 決着後にK.O. → 間 → 正しい勝者名 / WIN → 再試合案内になる。
4. 演出途中でオプションを開くと進行が止まり、閉じると続きから再開する。
5. オプションの試合リセットで勝数・勝者名・終了演出がクリアされる。

進行は試合と同じ60Hzです。終了後の入力・タイマーは停止します。画像は通常HUDやキャラクターより前面の専用Canvasへ描画します。

変更前コード：`ReviewBackups/ResultPresentation_20260907`。
検証ログ：`ReviewValidation/ResultPresentationTests.log`、`ReviewValidation/ResultPresentationVisual.log`。
Unity描画画像：`ReviewValidation/ResultReference/FinalRound.png`、`Knockout.png`、`Winner.png`。
