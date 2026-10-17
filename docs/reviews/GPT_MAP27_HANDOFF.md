# GPT → Claude MAP-27 引継ぎ（2026-10-17）

プレイヤーの戦闘ミニマップに地形が出ていなかったため、出撃先選択と同じ上空地形背景を追加した。owner GPT。実装と静的検証を保存し、この区切りで全編集予約を解除する。Claudeへの直接送信・今回受領は未確認。

## 保存した変更

- Runtime/Playable/MapTerrainBackground.cs: 出撃先選択で描いていた地形・グリッド・地下経路を共通化。公開MapDefinitionだけを使い、背景のUIは操作を遮らない。建物/足場の色と配置を全画面で揃えた。
- PlayableMinimap.cs: 中央のMapFieldへ背景→レーダー範囲→拠点/機体/設置物を重ねる。310×190の枠内を最大290×170として地形の縦横比を保持し、座標とレーダー半径も同じfield寸法から算出する。
- DeploymentMapView.cs・TacticalMapView.cs: 同じ背景/座標/縦横比処理を使用。出撃先のクリック、戦略地点指定、既存マーカーは継続。地下経路の線幅だけ小画面で1.5pxへ抑え、地形を潰さない。
- PlayableFlowTests.cs: 既存レーダー生成/期限切れフローの参照先を Minimap/MapField/RadarCoverage/RadarRange0 に更新。新規のテストケースは追加していない。
- マップ切替時は既存Disposeでfield以下の旧背景もinactive→detach→Destroyする。Texture/RenderTexture/Materialの追加生成はない。

Contracts・Simulation・機体性能・索敵条件の変更なし。敵は従来どおり目視または共有レポートの範囲で表示し、未観測敵の座標を背景に焼かない。Scene/Prefab/SO/YAML/.meta・UserText・正本を編集していない。

## 実行した検証

- 現行148個の.cs/.asmdefを隔離し、全20アセンブリをUnity 6000.3 API/パッケージ参照で静的コンパイル、exit 0。既存JSON読み込み用フィールドのCS0649警告3件。
- 148ソースのSHA-256が検証snapshotと現行で一致。地形は構築時だけ生成し、更新処理には追加しないこと、背景がマーカーより下・raycastTarget=falseであること、座標/レーダー半径/破棄の経路をコードレビューした。
- 実JSONからHUD内の縮尺計算を確認: 荒地290×163.125、立体市街259.048×170、二層都心255×170。map-layout.jsonは数値計算でありUnity実描画の証明ではない。
- 既存Sceneを除外したgit diff --checkはexit 0。実機テストや純ドメイン全回帰/100BOTは今回再実行していない（UI背景だけの変更）。

証跡: Build/MAP27_1792196245080/ の static-compile.log、static-compile.status.json、project/Build/Reports/static-api-compile.json、source-sha256.json、map-layout.json、final-checks.json。

## Unity受入・残作業（未実行）

Editorへの直接接続はこれまでのOSユーザー差によるアクセス拒否で利用できていない。Native Test Runner、実画面/操作、Windows x64、FPS/GCは未実施。

1. Play停止→Assets > Refresh→コンパイル完了→既存PlayableでPlay。新規MapTerrainBackground.csの.metaはEditorで生成する。シーン再生成不要。
2. 全3マップで出撃先選択とHUDの建物/足場/拠点が一致し、地下経路の水色線とマーカーが読めるか確認する。
3. Mで同じ背景の戦術マップを開き、地点指定→支援選択/戻る、出撃/再出撃の地点ボタン、マップ切替を確認する。
4. レーダーを設置して範囲が背景と同縮尺で丸く表示されること、敵の未索敵位置が出ないことを確認する。既存PlayableFlowTestsのレーダーフローを実行する。

解除済みの対象はClaudeが追加GPT承認なしでレビュー/修正可能。実受領・実行結果のみWORK_BOARD/reviewsへ追記する。
