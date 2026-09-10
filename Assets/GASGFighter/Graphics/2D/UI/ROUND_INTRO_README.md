# 試合開始UI

対応環境：Unity 6000.3.11f1 / URP 17.3 / uGUI。

既存の `FightHud` と `FightMatchManager` がある試合Sceneを再生すると、自動で開始画像を表示します。Scene・Prefabへの追加設定は不要です。

## 調整

Projectで `Assets/GASGFighter/Resources/FightIntroSettings.asset` を選択してください。

- **Round Images**：先頭からROUND01、ROUND02。ROUND03以降の画像を追加するときは配列を増やして割り当てます。画像がない番号は `ROUND 03` のような文字表示になります。
- **Fight Image**：FIGHT画像。
- **Round Seconds / Fight Seconds**：初期値1.65秒 / 1.1秒。ROUND中は操作をロックし、FIGHTが出るタイミングで操作と試合時間を開始します。
- **Round Width / Fight Width**：1920px幅画面を基準とした画像幅。初期値780 / 1050。画像の縦横比を維持します。
- **Center Offset**：中央からの位置。Xは右、Yは上が正方向です。
- **Entrance Fraction / Exit Fraction**：表示時間のうち出現・退場に使う割合。
- **Fight Start Scale**：FIGHTの出現直後の大きさ。
- **Round Slide Distance / Exit Slide Distance**：出現・退場時の横移動。
- **Echo Distance / Left Echo Color / Right Echo Color**：出現・退場時の色の残像。
- **Backdrop Opacity**：背後の薄い暗幕。0で無効。
- **Sorting Order**：初期値32000。開始画像専用のScreen Space Overlay Canvasを使用し、キャラクター・通常HUDより手前に描画します。

MatchManagerの **Intro Settings** を未指定にしておくと、このResources設定を自動使用します。個別の設定アセットを指定した場合はそちらを優先します。設定がある場合、旧 **Round Start Delay Seconds** より **Round Seconds** が優先されます。

## 動作確認

1. `LocalVersusPrototype` SceneをPlayし、ROUND01 → FIGHT → 表示終了を確認。
2. 画像がキャラクターに隠れないこと、透明な背景が四角く表示されないことを確認。
3. 次ラウンドでROUND02が表示されることを確認。
4. オプションによる一時停止・再開で演出の進行も停止・再開することを確認。
5. 試合リセットでROUND01からやり直すことを確認。

演出の進行は試合と同じ60Hzの時計を使用します。ポーズやヒットストップで表示だけが先に進むことはありません。参考動画の拡縮・短い静止・色のずれを参考にしたUIプロトタイプです。音声・キャラクターの登場アニメーション・背景のモノクロ化は追加していません。

元のPNGは変更していません。画像のImport Settingsのみ、縦横比維持・透明境界・MipMapなし・非圧縮へ調整しています。変更前のコード・画像・Metaはプロジェクト直下の `ReviewBackups/RoundIntro_20260907_124447` に保存しています。
