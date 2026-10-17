# DEPLOY-16 出撃先マップ選択 — GPT引渡し

2026-10-16。owner GPT。ソース実装と独立検証完了。Unity実機受入・Claudeレビューは未実施/未受領。編集予約は全解除。正本MasterPlanやUserText/、Scene/Prefab/SO YAMLは変更していない。

## 使い方と挙動

- 参戦準備: 右のマップの自軍本拠点（■）をクリック→黄色枠/出撃先名を確認→出撃。機体選択/小さな機体プレビューを維持。初回も選択した本拠点から出る。陣営変更時はその陣営の本拠点へ初期化。
- 撃破後: 左で機体、右のマップで自軍本拠点または確保中継（◆）を選び、右下で確定。敵本拠点/未占領/敵占領/敵接近中中継は使用不可。中継ごとの理由を表示する。機体CTと再出撃遅延は既存の共通ルールを維持。
- マップは上が北、公開地形/高台・味方位置・共有contactの敵最終位置を描画。敵の未観測現在位置を取得して描かない。マップ寸法から等尺で配置し、任意座標スポーンは追加しない。拠点周辺の既存散開を維持。
- 選択後の利用不可は出撃ボタンを無効化。要求後の待機中に中継を奪われた場合も確定時再検証してキャンセルし、別の場所へ自動で出撃しない。選び直して確定する。BOTの自動出撃・枠数/指揮官/各軍3拠点は維持。
- 更新: Play停止→Assets > Refresh→コンパイル完了→既存PlayableでPlay。起動時uGUI生成なのでScene再生成は不要。新規.csの.metaはEditorに生成させる。

## 変更ファイルとAPI

対象は MechaBattle/Assets/Game/ 配下。要件 FR-203/205（前線中継）、FR-401/402/404（デッキCT共通）、FR-801/802 と最新ユーザー要求。

- Runtime/Simulation/BattleSimulation.cs: 初回本拠点指定とスポーン地点選択。UnitStateにPreferredMainBaseを追加、出撃後に解除。ctorの末尾にoptional humanMainBaseIndex=-1（map.mainBasesの全体index）を追加。敵本拠点/不正index/全BOTへの人間指定は拒否。
- Runtime/Simulation/BattleDeployment.cs（新規）: RequestSpawnをここへ移動し、末尾optional mainBaseIndex=-1を追加。既存captureIndex=-1の自軍本拠点自動選択を維持。relayと個別base同時指定は拒否。CanSpawnAtMainBase(team,index)追加。Contractsの構造変更なし。
- Runtime/Playable/DeploymentMapView.cs（新規）: 拠点ボタン・公開地形・方位・選択枠・状態・限定contactの再利用描画。非操作Graphicはraycast=false、Image経由のCanvasRendererを使用。
- Runtime/Playable/PlayableEntry.cs: 初回/再出撃の別選択を管理、クリックの有効性再検証と確定API接続。
- Runtime/Playable/PlayableUI.cs: BattlePrepの右側をマップへ、小型機体プレビューを下部へ。再出撃を左右の機体/マップへ。新しいEventSystem/Prefab/Sceneは追加しない。
- Tests/EditMode/DeploymentTests.cs（新規）: 15件。全6本拠点の初回選択・BOT位置/資源維持・不正/敵base・team配列の混在順・CT/遅延維持・中継/本拠点の明示選択・待機中の中継喪失/再選択。
- Tests/EditMode/Playable/PlayableFlowTests.cs: 初回の実ボタンクリック/枠と名称/出撃Z座標・画面内配置、撃破後の拠点選択→機体選択→実出撃を追加。静的コンパイルのみ、Unity実行未実施。

## 検証証跡

- 対象43/43、全350/350（新規15含む）成功。純C#73ソース・.NET9、終了コード0。Unity Test Runnerとは別。
- 静的Unity APIコンパイル18アセンブリ成功。既存JSON用field警告のみ。Editorインポート/Playerビルドではない。
- 実Playable.Applyを使う100BOT試合は323.9sim秒で決着、58撃破/157出撃/84コア命中、両軍最低47/48。前回AIR15と同じ推移で、BOTデフォルトの地点選択は維持。FPSではない。
- 119ソース/asmdef/試験JSONの検証コピーSHAと現行差分0。実configの2コピー一致=true。生成Sceneを除くgit diff --check成功。
- 最初の新規試験1件は中継スポーンの散開0.309mを0.02m以内と期待した試験の誤りで、既存半径3m内に修正して通過。全検証1回目はGPTのstdoutバッファ不足でコマンド中断、出力先をファイルにして同一コピーを再実行し終了0を確認した。
- 証跡: [機械可読結果](GPT_DEPLOY16_VALIDATION.json)、MechaBattle/Build/DEPLOY16_1792141899606/quick-final.log、domain-final.log、static-final.log、project/Build/Reports/、source-sha256.json。

## Claudeへの継続依頼と未実施

実作業表でClaudeのUI監査優先順位受領/DEPLOY16の競合待機を確認し、ソースは09:13UTCに解放済み。Entry/UI/BattleSimulationの次の通知/被弾方向/戦績作業はGPTの承認待ち不要。既存RequestSpawnの所在変更と新しいマップ/プレビュー位置を保持して進めてください。このDEPLOY16完成内容のClaudeレビュー受領は未確認。

Unityへの従来の直接接続制限は回避していない。**Unity Test Runner、実画面のマップ/クリック/画面サイズごとの配置、Windows起動、FPS/GCは未実施**。次はGame.Tests.PlayableのFlowを実行し、両軍の初回/撃破後の3本拠点と占領中継クリック・CT中/中継使用不可の表示をGameタブで確認する。P0/MasterPlan全体完了とは扱わない。GPTの起動プロセスは全て終了。
