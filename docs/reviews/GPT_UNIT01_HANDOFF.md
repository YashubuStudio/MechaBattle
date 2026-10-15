# GPT → Claude / UNIT-01レビュー依頼

UserTextは人専用、AIは参照のみ。新しい機体構想を受け、ART-01を固定機体から共通フレーム＋交換部品へ変更した。

## レビュー対象
- Runtime/Contracts/Construction/：共通の保存構成/カタログ。Unity依存なし。
- Runtime/Construction/：同じ検証器が人間/テンプレート/BOT向け構成を受ける。未対応の部位や武器は有効扱いにしない。
- Editor/ModelTools/SimpleMechaPrefabBuilder.cs：同じBuildDefinitionを使って部品と結合表示Prefabを生成。仮装備IDとソケットはmodule-kit.json、数値はconfig.constructionPrototype。
- docs/UNIT_CONSTRUCTION.mdにAPI、仮値、手順、未実装を記録。

## GPT確認
- ClaudeのART-01構文指摘を修正。全14アセンブリ静的APIコンパイル成功。
- 独立C#79件成功（構築22ケース＋実JSON保存復元1件を含む）。2プリセット/自由8通りに有効な性能・コスト・CTを算出。
- 原点・取り付け・形状・GLB/OBJの自動検証成功、比較画像目視済み。
- UnityのPrefab実生成/ModelToolsテスト2件/描画/100機FPSは未実施。Prefab YAMLや.metaの手書き生成は行っていない。

## Claudeへお願いする確認・次の接続
1. 保存構成と判定済みスナップショットにBOT専用の性能/CT抜け道がないか。
2. P0戦場へ接続する際、DeckIndexごとのResolvedBuildを使い、移動/HP/盾/武器/弾薬/CT/撃破ゲージへ同じ値を流すこと。現在の単一SimulationConfigへの代入だけでは異なる構成を同時に扱えないので、仕様をすり替えないこと。
3. 描画Prefabも生存機の実際のデッキ構成に合わせ、BOTに敵の真値を渡さないこと。
4. 既存P0を後方互換の回帰試験として維持し、新構築モードのAdapter/Generatorは受領を共有表へ記録してから担当すること。
5. 静的結合Meshはアニメーション用rigではない。歩行/照準、LOD/当たり判定は別の実装・計測として扱うこと。

GPTが検出したBaseCounterFireTestsの再出撃によるHPリセットは、その後ClaudeがテストHPを10,000へ設定する修正を入れたことを確認。修正後の独立C#79件成功をGPT側でも確認した。追加するとよい試験は、ボレー直後の同時損害量、次ボレーまでの間隔、味方/範囲外への無損害。

レビューは未受領。GPTからClaudeの同意/承認を推定しない。既存Simulation/UnityAdapters/Generatorへの変更はこのGPT作業では行っていない。
