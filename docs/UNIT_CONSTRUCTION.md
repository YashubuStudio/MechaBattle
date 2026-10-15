# 機体構築の実装契約（UNIT-01）

UserText/MechaBattle機体構想・カスタマイズシステム構想案.md v0.1を追加方針とする。UserTextは参照専用。初期は標準フレーム1種、左右の腕2種、主武器2種を用意する。個別の固定機体は定義せず、テンプレートも通常のBuildDefinitionとして保存する。

## GPT / Claude の分担
GPTは純粋C#の構築契約・検証/性能計算、config仮値、モデルとEditor組立ツールを担当。既存P0 Simulation/Adapter/Generatorは変更しない。ClaudeへP0への接続と相互レビューを依頼中（受領前）。

## 実装したAPI
- Game.Construction.Contracts（Runtime/Contracts/Construction/）、namespace MechaBattle.Contracts.Construction：Unity非依存のSerializableなフィールド型。ConstructionCatalog、FrameDefinition、PartDefinition、BuildDefinition、EquipmentSelection、AppearanceDefinition、CostPolicy、CombinationCostRule。
- Game.Construction（Runtime/Construction/）、namespace MechaBattle.Construction：ConstructionEvaluator.Evaluate(catalog, build) → immutable ResolvedBuild。無効な入力はArgumentException。TryEvaluate(catalog, build, out resolved, out errors)でエラー一覧。人間/BOTの区別や特別な性能経路を持たない。
- BuildDefinition：id, frameId, equipment[]（slot, partId）, appearance.paintId。プリセットはこの同一型。外観は性能/コストに影響しない。
- 初期PartSlot：LeftArm=0, RightArm=1, PrimaryWeapon=2。今後の枠は新しい構築スキーマで追加する。宣言だけの未実装部位を有効扱いにしない。
- ResolvedBuild：FrameId, ConnectionStandard, Equipment（ReadOnly）, PaintId, MaxHealth, MaxShield, MoveSpeed, FireDamage, FireRange, FireIntervalSeconds, MaxAmmo, Weight, Energy, Cost, CooldownSeconds。必須部位/重複/ID/部位/規格/重量/出力/武装枠/スキル枠/排他/有限数値を検証してから返す。
- 仮データ：config/prototype-defaults.json の constructionPrototype.catalog。接続後も既存p0Simulationの互換性を保ち、構築データがない既存試験は従来動作とする。BOTも同じResolvedBuildを使い、CT/弾薬/ゲージ損失は共通ルールへ流す。

## 見た目の規格（試作）
規格ID mb.humanoid.standard.v1。Y上/+Z前/メートル/足元原点。標準フレームの見た目高2.47mは仮値で、既存シミュレーション半径とは別。腕原点は肩接続、主武器原点は右腕の保持ソケット。module-kit.jsonに親子/ローカル位置/対応部品IDを記録する。左右腕と主武器を交換し、形状をEditorで組み立てて3サブメッシュへ結合する。アニメーション/LOD/当たり判定の自動生成は後続で、現時点は静的表示。既存P0シーンは変更しない。

## 段階と未実装
段階1の共通フレーム・腕/武器組立と、段階2の性能/コスト/CT算出を先行。戦場への反映、保存UI、BOT条件生成、役割推定、4機小隊最適化、金功績連動、追加部位、100機の描画/アニメ計測は別途検証が必要。2019ミドルPCでの60fpsは未測定。

## 実際の試作データ（正式バランスではない）

| 保存構成 | HP / Shield | 移動速度 | 主武器 威力 / 間隔 / 射程 / 弾数 | 自動コスト | 自動CT |
|---|---|---|---|---|---|
| standard-carbine | 100 / 50 | 12 m/s | 20 / 0.4s / 65m / 30 | 221 | 約112.95秒 |
| reinforced-longrifle | 140 / 50 | 11 m/s | 30 / 0.6s / 85m / 20 | 268 | 約147.14秒 |

左右の腕を独立に選べるため、現在の部品だけで8通りを構築できる。上表はそのうち2つを通常の保存構成として用意したもの。CTは算出結果であり、現時点のP0戦場は従来のdeckCooldownSecondsを使う。構築値を既存試合へ流す接続は未実装。金功績による短縮は未実装。

## Unityでの生成・確認

1. Edit Modeでインポート完了後、MechaBattle > Models > Generate Simple Robot Prefabs を実行。
2. Assets/Game/Content/Models/MB_Worker01/Generated/ に部品Prefab 7個、保存構成2種×Neutral/Blue/Orangeの完成表示Prefab 6個を生成する。既存SceneやBuild Settingsの変更はしない。
3. Window > General > Test Runner のEditModeでGame.Tests.ModelToolsを実行する。2ケースで結合Mesh・取り付け・部品混在・色共有・GUID維持・開いたScene保護・Unity JSON保存復元を確認する。
4. 個別の保存構成には SimpleMechaPrefabBuilder.GenerateBuild(catalog, build, "Blue") を使う。主武器は右腕のweapon.primary、発射位置は武器のmuzzleソケット。チーム色は外観設定に優先できる。

完成表示Prefabは1 MeshRenderer / 3 submeshes。見た目の各部品ノードとソケットは階層に残すが、形状を結合した静的試作であり、ノードを動かしても腕の形状は動かない。腕の可動/歩行/照準アニメーションは未実装。Collider、Rigidbody、Animator、シミュレーション用Scriptは自動付与しない。

## ソース再生成

- MechaBattle/scripts/art/generate-simple-mecha.mjs：Nodeで7部品とOBJ/GLB/Unity用mesh JSONを生成。外部モデル/テクスチャを使用しない。
- validate-simple-mecha.mjs：有限座標、法線、面向き、各閉立体の辺/体積、接続位置、カタログID、GLBバッファ/Float32境界、OBJ部品分割を確認。
- render-simple-mecha.py：Pillow/numpyで確認画像を生成。ランタイム依存ではない。
- ArtSource/MB_Worker01/MB_Worker01.glb：部品単位のMeshと2つのシーンを持つDCC向けソース。Unity Prefabはmodule-kit.jsonからEditor APIで生成する。

## 検証結果と制限

2026-10-15 GPT検証：独立C# 79件成功（構築関連22ケース＋実設定/保存復元1件を含む）。実データ2プリセットと自由構築8通りを検証。モデルの7モジュール/67閉立体インスタンス/2,580部品三角形の検証に成功。完成構成は1,676 / 2,016三角形。Unity APIに対する全14アセンブリ静的コンパイル成功。

今回追加したModelToolsのUnity EditMode試験2件・Prefabの実生成・Unityでの描画確認・100機性能測定は未実施。以前のP0に対するユーザー実行50/50件成功とは区別する。Editorの検出情報をGPT環境から読めないため、実Editor接続による実行結果は報告していない。
