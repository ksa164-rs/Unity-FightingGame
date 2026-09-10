# Ken-style Hadouken VFX Graph Prototype

対象環境: Unity 6000.3.11f1 / URP 17.3.0 / Visual Effect Graph 17.3.0

参照動画を基に、描画をすべてVisual Effect Graphで構成した調整用プロトタイプです。
Runtime C#、Editor Builder、Particle Systemには依存しません。

## Prefabs

- `PF_Hadouken_Projectile_VFX.prefab`
  - Projectile Core: 白青色の高輝度中心
  - Flame Shell: 外周を流れる青い炎状粒子
  - Ribbon Trail: 後方へ残る帯状の残光
  - Micro Sparks: 輪郭から剥がれる細粒子
- `PF_Hadouken_Impact_VFX.prefab`
  - Impact Disc: 命中瞬間の円盤状フラッシュ
  - Impact Sparks: 放射状に飛ぶ青白い火花
  - Impact Smoke: 遅れて消える青紫の煙

## Preview

1. Unityで `Assets > Refresh` を実行します。
2. ProjectileまたはImpact Prefabを空のテストSceneへ配置します。
3. Scene ViewのVFX再生、またはPlay Modeで見た目を確認します。
4. ProjectileはローカルX+方向を正面として構成しています。

## Gameplay integration

- 入力: `236 + Special`
- 発射: Hadoukenモーションの13フレーム目
- 方向: `FighterController.FacingDirection`から相手側を自動判定
- 速度: 7.5 units/sec
- 判定半径: 0.3 units
- 最大寿命: 2.4 sec
- 命中: 既存のダメージ、ガード、ヒットストップへ接続

ゲーム調整値は `Assets/GASGFighter/Data/Attack_Hadouken.asset` にまとめています。

## Designer tuning

各Graphを開き、主に以下のブロックを調整します。

- Spawn: 発生密度
- Set Lifetime: 粒子が残る長さ
- Position Sphere / Circle: コアやシェルの半径
- Velocity from Direction & Speed: 炎やスパークの伸び
- Color over Life: 白、シアン、青、紫の比率
- Size over Life: コアの締まり、衝撃波の拡大
- Force / Drag: スパークや煙の広がり

まずPrefabの各子オブジェクトのTransform Scaleでレイヤー比率を合わせ、
次にGraph内のSpawn、Lifetime、Sizeを調整すると破綻しにくくなります。

## Notes

- 発射体の移動やヒット判定はゲーム側のProjectileロジックでPrefabのRootを動かしてください。
- 命中時にImpact Prefabを再生し、Projectile Prefabを停止またはプールへ戻します。
- BloomはURP Volume側で調整してください。高輝度カラーを使用しているためBloom有効時に発光します。
