# モデルの二重表示・小パンのモーション修正

Unity 6000.3.11f1。対象：`Scenes/LocalVersusPrototype.unity`。

## 原因と修正

- 各Playerの子に、元のPlayerモデルPrefabと追加された`Visual_Player_001`が両方有効な状態で存在していました。FighterControllerが使用している表示モデルを残し、重複する元のモデルをシーン内だけ無効化しました。元Prefabやモデルは削除していません。
- 小パンのAttack Definitionが昇龍拳のActionを参照していました。`AS_p01_100_001 / 立ち弱P`へ修正し、入力→技→Animator Trigger `attack_001`→`attack_001.anim`の経路を確認しました。昇龍拳の参照と攻撃数値は維持しています。
- モデル適用ツールは、名前だけでなく登録済みの表示モデルや元FBXの参照を使って再利用するよう変更しました。同じ元モデルの重複表示は削除せず無効化し、Undoにも記録します。武器・VFX・Hurtboxはこの無効化の対象にしません。

## 検証

複製したプロジェクトでUnityコンパイルと40件のテストがすべて成功しました。

- 1P／2Pそれぞれ有効なモデルが1体であること。
- 重複モデルを再度有効にし、表示モデルの名前を変更しても、モデル適用を2回行えば1体だけが有効になり、子オブジェクト数が増えないこと。
- 実際にPlayへ入り、両プレイヤーの小パン入力で`attack_001` Stateと`attack_001.anim`が選ばれること。

描画なしの検証のため、画面の見た目自体の目視確認は含みません。Playを停止し、修正済みの`LocalVersusPrototype`を開いて小パンを確認してください。

修正前バックアップ：`ReviewBackups/VisualFix_20260907_103353/`

テスト結果：`ReviewValidation/VisualFixResults.xml`

Unityログ：`ReviewValidation/VisualFixEditor.log`
