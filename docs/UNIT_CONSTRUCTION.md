# 機体構築の実装契約（UNIT-01 / FIELD-04）

方針はUserText/MechaBattle機体構想・カスタマイズシステム構想案.md v0.1。UserTextは参照専用。標準/重量フレーム2種、左右腕各2種、13種の装備から最大3装備を自由選択する。プリセットも通常BuildDefinitionで、固定キャラクターの例外性能はない。

## API・互換性

- Game.Contracts：不変UnitLoadout、WeaponKind、WeaponFireMode、MeleeStyle、UnitCommand。黄/赤ロック範囲と非ロック散布も共通データ。人間/BOT/Simulation共通。
- Game.Construction.Contracts：SerializableなConstructionCatalog/FrameDefinition/PartDefinition/WeaponDefinition/BuildDefinition/SavedDeck/CostPolicy。Unity非依存。
- ConstructionEvaluator.Evaluate/TryEvaluate：ID/規格/必須部位/重量/出力/枠/排他/有限値を検証し、不変ResolvedBuildとCombatLoadoutを返す。
- PartSlotはLeftArm=0/RightArm=1/PrimaryWeapon=2/SecondaryWeapon=3/TertiaryWeapon=4。左右腕と装備1は必須、装備2・3は任意。全装備部品はカタログ上slot=2で定義し、3装備枠のいずれにも選べる。旧schemaVersion=1の1装備保存を読み込める。
- UnitLoadout.WeaponCount/GetWeapon(index)/WithWeapons(...)は1〜3装備の不変スナップショット。HP/盾/速度/コスト/CTは共通、弾薬/射撃性能は各装備。ResolvedBuild.WeaponPartId(index)は枠順の描画部品ID。
- 武器性能には方式・DP・弾倉/装填・チャージ/照射・射程/赤範囲/弾速・爆風・投擲重力/起爆・設置半径/寿命/耐久値（UtilityHealth）を含む。戦闘開始時に各操縦者4機分をSimulationへ渡し、構築データの後編集は試合へ影響しない。
- Unity JsonUtilityが腕の欠落weaponを全0オブジェクトにした場合は未装備として扱い、実性能のある腕weaponは拒否する。

## 仮容量・コスト・CT

標準フレームは重量上限145/出力上限135/武器枠3。全装備の重量・出力・装備費を加算する。性能費は最も高い1装備を100%、残りを35%として加算し、同時発射不可の予備装備を評価する。HP/盾/速度/DP耐性、装填込みDPS/DP、射程/赤範囲/弾速/弾数/爆風、特定部品組合せもコストに含む。

数値の正本はMechaBattle/config/prototype-defaults.json。CTは10 + 0.10×cost + 0.0003×cost²。HP2000〜3850と大きなダメージ表記に合わせコスト係数を正規化し、高コストほど増加する凸形は保持する。金功績による短縮は未実装。

以下は射撃＋格闘＋回復ポッドを持つ8テンプレート。旧1装備データのコスト/CTはそれ自身の性能から再計算する。

| 保存構成 | HP / 盾 / DP耐性 | 速度 | コスト | CT |
|---|---|---|---|---|
| standard-carbine | 2000 / 900 / 600 | 12m/s | 427 | 107.40秒 |
| reinforced-longrifle | 3200 / 900 / 750 | 11m/s | 478 | 126.35秒 |
| standard-cannon | 2000 / 900 / 600 | 12m/s | 440 | 112.08秒 |
| standard-charge | 2000 / 900 / 600 | 12m/s | 453 | 116.86秒 |
| standard-beam | 2000 / 900 / 600 | 12m/s | 435 | 110.27秒 |
| standard-bazooka | 2000 / 900 / 600 | 12m/s | 451 | 116.12秒 |
| heavy-cannon | 3850 / 1100 / 900 | 8.5m/s | 504 | 136.60秒 |
| standard-assaultrifle | 2000 / 900 / 600 | 12m/s | 427 | 107.40秒 |

格納庫は4機保存、左右腕/3装備交換、選択装備のプレビュー、性能と制約表示を接続済み。人間/BOTの戦闘・補給・撃破コスト・CTへ同じ計算結果を使用する。BOTは8テンプレートをずらした4機デッキを持つ。既存P0は構築データを渡さなければ従来ルールを使う。戦闘詳細は[COMBAT_RULES.md](COMBAT_RULES.md)、起動は[PLAYABLE_GUIDE.md](PLAYABLE_GUIDE.md)。

## モデル

接続規格mb.humanoid.standard.v1、Y上/+Z前/メートル/足元原点、フレーム高2.47m。腕原点は肩、現在装備は右腕のweapon.primaryソケットへ表示する。19モジュール（フレーム2＋腕4＋装備12）をmodule-kit.jsonに記録。予備装備の背面収納表示はまだない。

**MechaBattle > Models > Generate Simple Robot Prefabs** で部品19個と8構成×Neutral/Blue/Orangeの完成表示24個をContent/Models/MB_Worker01/Generatedへ生成する。開いたSceneを変えず、既存GUID/共有素材を維持する。完成Prefabは現在装備1つの静的結合1 MeshRenderer/3 submeshes。任意構成はSimpleMechaPrefabBuilder.GenerateBuild(catalog, build, "Blue")。

Playable実戦は股関節/膝/右腕/現在装備を分け、歩行・ダッシュ・ジャンプ/着地と反動、格闘動作・銃の上下照準・盾/刃/弾道/爆発・よろけ/転倒を描画。装備切替に外観も追従する。全身スケルトン/完成版アニメーション/LOD/mesh衝突は未実装。

ソース生成はscripts/art/generate-simple-mecha.mjs、幾何検査はvalidate-simple-mecha.mjs、画像はrender-simple-mecha.py。外部モデル不使用。GLBは18部品メッシュ/7組立シーン、OBJは標準カービン4部品。確認画像はUnity画面ではない。

## 検証・後続

独立C#で8テンプレート、標準フレームの自由52単装備構成と8788三装備構成の全評価/保存復元/順序不変を確認。モデル19モジュール/153閉立体/6060部品三角形、8構成は1676〜2016三角形。Unity用には実JsonUtilityの3装備混在、Prefab/共有素材/GUID/Scene保護と画面一巡試験を更新したが、実Unity Test Runnerは未実施。

ユーザーは旧構築→戦闘、新攻撃/DPと照準改善を実機確認済み。FIELD-04の基本動作はユーザー実機確認済み。LINE-05/AIM-06の操作感/見栄え、Windows起動/100機性能は未確認。小隊/指揮官、金功績、追加フレーム/部位、アートと最終バランスを後続として管理する。

最新追加: weapon.repairgun（味方HP回復FA）/weapon.radar（設置索敵）。ポッドとレーダーはammoResupplyDisabled=true、ポッドの盾/弾薬はpodShieldPerSecond/podAmmoPerSecondで外部定義。末尾任意引数で旧装備は互換。最新の支援/ブースト/格闘と検証値は[引渡し](reviews/GPT_AIR15_HANDOFF.md)参照。

設置物のutilityHealthはWeaponDefinition→ResolvedBuild→UnitLoadoutへ互換末尾引数で渡す。実ポッド1800/レーダー900/地雷450、旧省略0はロック/破壊対象外。寿命はポッド180秒/レーダー900秒、有限弾数と補給不可は維持。耐久/長寿命の追加コスト係数はまだ導入せず、今回のHP引下げと通常格闘間隔0.35→0.31秒により、上記8構成のコスト/CTは再計算済み。命中後の条件付き補正は現状のコスト式へ直接加算しない仮仕様とする。全フレームの全組合せを網羅したとは扱わず、8788通りは標準フレームでの検証。実戦に置いたポッド/レーダー/地雷もMechaMeshLibrary.CreateDeployableで既存専用モジュールを使い、地面と論理判定の高さ/幅へ合わせる。

UI-11: 機体制作/参戦準備は専用RenderTextureのプレビューへ分離し、参戦準備で選んだデッキ番号を初回出撃へ反映。以前の「選択にかかわらずデッキ0へ出撃」は修正。詳細は[UI修正引渡し](reviews/GPT_UI11_HANDOFF.md)。

SUPPORT-12: 標準フレーム2000/重量2650へ共通HPを引き下げた（Claude経由でユーザー了承を受領）。標準補強腕2本3200と最低2000の指定を維持する。現在機体の回復FA/ポッド所持が味方ロックを許可し、FAは物理65/黄60/赤25m。予備デッキは含めない。格闘の命中時だけ後隙×0.5/消費ブースト50%返還。通常格闘の基礎間隔0.31秒へ合わせ、表のコスト/CTは実評価値へ更新済み。追加の命中効果を考慮したコスト係数は今後の調整対象。

AIR-15統合: Claude追加のAR/8番目構成に必要なweapon.assaultrifle表示モジュールを追加生成。全19部品IDがカタログと一致し、元の18部品の形状は変えない。19部品/8構成/153閉立体/6060三角形の幾何検証と全GLB接続検査は成功。Unity Prefab生成/見栄えは未確認。フレーム別武器許可リストはClaude実装を保持し、標準フレーム52/8788の保存復元を再確認した。
