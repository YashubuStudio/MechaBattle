# STRATEGY-23 — 立体市街地・BOT知覚・マップ戦略支援（GPT）

2026-10-17。ユーザーの「似たマップの改善、視野外の即時BOT索敵を制限、味方撃破で補充する戦略ポイント、地図から核/妨害/広域レーダー」に対応。FR-001/108/109/110/601/602/603/604/705/707/802/805。実装・独立検証完了、Unity実操作受入待ち。全編集予約は解除。

## 操作

Play停止 → Assets > Refresh → コンパイル完了 → 既存PlayableでPlay。Scene再生成は不要。参戦準備「マップ」で **立体市街・連絡橋** を選び、本拠点を選択して出撃。

戦闘中の **M** で戦術マップを開く。左で戦略支援を選択 → 地図で地点をクリック → 範囲プレビューを確認 → 「指定地点へ発動」。不足ポイント/同種稼働中/CT/範囲外/終了試合は拒否し、理由を表示。M/Esc/戻るで閉じる。地図中も戦闘は進み、自機の移動/攻撃入力は止める。撃破後にも使用可能。Esc休止では地図を閉じる。

## 独立した都市マップ

640×420m、各軍3本拠点・3中継、354の衝突volume。6棟の多層建物（6/12/18/24m）、通り抜け倉庫2棟、中央車道上の連絡橋2本、8m開口・階段・踊り場・吹抜け・バルコニー。壁と階床を分け、中を歩ける空間を残した。中央大通りと前庭を主戦場にし、建物・裏路地で射線を切る。旧3マップの寸法変更コピーではなく新しい街区配置。

階段から6m階床、車道の橋、反対棟まで歩いて通れる。さらに上の階は吹抜け/バルコニーでSpace上昇し、各階へ着地して上昇基準を更新。天井は通過しない。BOTの移動も既存の共通motor/地形ステアリングを使い、NavMeshの新規導入はしていない。

config/urban-overpass-map.jsonを scripts/content/generate-urban-map.mjs で生成し、通常は sync-map-catalog.mjs でResourcesへ同期。カタログの4番目 urban-overpass、theme urban、resource MechaBattleMaps/UrbanOverpass。街のコンクリート外壁、階床、アクセス階段/バルコニー、橋、車道を描き分ける。

自己レビューで見つかった階段と踊り場の隙間/橋入口の窓台を修正。実JSONと半径0.65mのmotorで地上→階段→踊り場→階床→橋→反対棟を連続移動し、高さ6mを保持する統合検査を追加した。既存GetSurfaceHeightのradius=0照会が厳密円交差のため常に0になる不具合も、点の矩形内照会へ修正。新マップの階上設置・中継表示にも使用する。

## BOTの知覚と実照準

Playable設定: 目視最大180m、水平110度/上下90度、LOS必須、0.22秒分散走査、0.5〜0.85秒の継続認識、視線/実照準110度/秒。照会の多重実行で待ち時間が短くならない。走査間は観測位置を保持し、毎フレームの敵真値を返さない。視認・範囲・同一機体の条件を外れた接触は射撃候補から外し、再取得は認識をやり直す。

BotPerceptionが真値とAIの境界。PlayableEntry、BattleSimulationRunner、ConstructedBattleSmokeのセンサーを共通処理へ接続。Game.AI.Bots/Observationは引き続きSimulationを参照しない。非目視の振向きはGetTeamContactで得られる範囲内・期限内・非妨害の最後の位置だけを使う。レーダーだけでは射撃接触/ロックを得ず、最終的に射界とLOSと認識待ちが必要。

さらに命令適用のAimPoint/赤ロックがyawを瞬時に変える経路も修正。各tick開始時の角度を保存し、Prepare/Move/AimAfterMovementを共通ApplyControllerAimへ通す。複数回更新しても1tick分の旋回枠を重ねない。人間のマウス手動照準は制限しない。Playableの赤ロック常時補助は既存通りfalseで、手動取得時だけ吸着する入力仕様を維持。

ミニマップの自機ローカル検出もCanVisuallySeeを使用。見えていない敵は有効な共有接触位置だけを使う。P0比較用の旧設定はbotPerceptionEnabled=falseを維持し、今のPlayableではtrue。指揮官/小隊の戦略自動判断は今回追加していない。

## 戦略ポイントと支援

陣営共有150pt開始、1000上限。自軍機の撃破確定ごと25ptを一度加算。出撃やrespawnで消去せず、新規試合で初期化。BOTも人間も同じ損失規則。設置物の破壊は機体損失として加算しない。

|支援|費用|予告|範囲|効果/持続|同種CT|
|---|---:|---:|---:|---|---:|
|核|600|12秒|34m球|敵味方の盾+HPへ6500、両軍本拠点4500、設置物にも損害、爆発表示2秒|90秒|
|マップ妨害|150|3秒|100m|敵の範囲内共有接触を45秒抑制|35秒|
|広域レーダー|100|2秒|140m|25秒、敵位置だけを短期TTL付きで共有|30秒|

同種は各軍1つまで、ポイント消費は要求の全検証が通った後に一度だけ。核の位置/予告/カウントダウン/危険範囲を両軍へ公開し、戦場の範囲線とHUDでも警告。遮蔽を貫く領域攻撃で敵味方の盾とHPへ同率損害。本拠点は両軍ゲージの既存倍率へ反映。占領フラグは直接書き換えない。味方核の巻き込みを攻撃者の敵撃破/与ダメ/敵拠点戦績へ加算しないが、被ダメ/被撃破と実損失のポイント補充は数える。

妨害は範囲内の敵軍共有接触を隠し、新しいロック/レーダー共有も抑制。直接目視・手動ロック・味方位置/HPは維持。広域レーダーは壁越しの範囲内位置だけでHP/射撃ロックは付与せず、妨害が切れた次の走査から回復する。敵の妨害/広域レーダーの位置をGetStrategyから無条件公開しない。核予告だけは両軍公開。各支援とポイントは試合終了後凍結、次試合で新規状態。

これらはstrategy23Notesの**仮設定**で、当時公式の確認済み数値ではない。核は今回**地図指定の予告型試作**。機体での運搬・拾得・破壊可能な核設置物・護衛/迎撃、指揮官AIの自動発動は未実装。携行核の完成とは扱わない。

## 公開APIと主な変更パス

MechaBattle以下:

- Runtime/Contracts/StrategyTypes.cs: StrategyKind/Rules/Definition、陣営ポイントと発動物snapshot。純C#、数値検証/防御コピー。既存UnitCommand/構築の保存形式は変更しない。
- Runtime/Simulation/BattleStrategy.cs: RequestStrategy(requesterSlot,kind,x,z,out reason)、CanRequestStrategy、GetStrategyTeam、GetStrategyDefinition、GetStrategyCooldown、GetStrategy(index,viewerTeam)、IsMapJammed。ローカル試作APIで、将来オンライン時の呼出権限の認証は別。
- SimulationConfig/PlayableConfiguration: フラグ/知覚数値/strategyを読み、検証・コピー。実configとContent/Playable/PrototypeConfig.jsonを同期。
- BattleSimulation/BattleCombat: 損失加算、全攻撃の一括解決へ戦略効果、BOTの物理旋回上限。
- BattleTeamContacts/BattleTraversal: 妨害と広域観測、点の地表照会修正。
- BotPerception、PlayableEntry/PlayableMinimap、UnityAdapters/BattleSimulationRunner: 共通知覚と情報表示。
- PlayableStrategy/TacticalMapView/StrategyWorldView: M操作、入力遮断と復帰、選択/地点/範囲preview/費用/CT、3D予告。既存Canvasへ追加し、Settings/Prep等は作り直さない。
- Tests/EditMode/BotPerceptionTests(14ケース)/StrategyTests(15)/UrbanMapTests(4)、Playable/PlayableFlowTests。ContentExpansionSmokeは4マップ/室内/連続階段-橋経路/100BOTを検査。

Scene/Prefab/既存.asset/.metaを直接編集していない。新Assets10ファイルの.metaはEditorによる生成を最後に確認。MasterPlanとUserTextは参照のみ。Gitのstage/commit/resetやOS/Unityのロック削除なし。

## 実行結果

最終証跡 **MechaBattle/Build/STRATEGY23_1792164282251/accepted/**。全164コード/設定/Resources/生成スクリプト/描画スクリプトのSHA保存、現行差分0。途中の旧結果は親/finalへ保存し、最新コードの受入にはacceptedを使用。

- 独立C# **471/471**（新33ケース）、93ソース、exit0。正式Unity Test Runnerとは別。
- 部分44/44、exit0。Quickを同じディレクトリに置いたquick-tests-local.logが対象。別ディレクトリからのLoadFromは旧隣接DLLを解決したため、新3件の実行証拠に流用しない。
- 静的 **20アセンブリ**、exit0。Unity import/Playerビルドとは別。
- 実都市データの連続移動検査 exit0（MechaBattle/Build/STRATEGY23_1792164282251/final/city-paths.log）、最終全回帰にも同じ検査を含む。
- 全マップResources/source一致、各軍3本拠点、構築13314構成等の既存回帰も成功。

|実設定100BOT|sim秒|撃破|出撃|本拠点命中|最小Blue/Orange|
|---|---:|---:|---:|---:|---|
|標準|150.70|60|159|282|47/46|
|段丘|159.74|38|138|268|47/48|
|荒地|315.62|84|183|179|47/47|
|都市|600.00|116|215|35|47/48|

都市は時間切れ判定（ゲージ25782.30/20501.29）。平均生存99.39、中央位置サンプル80.7%。他3種はゲージ0で終了。BOTの資源や枠数は緩和していない。都市の人間の操作感・最終バランスの合格とは区別する。

ログ: domain-run.log / static-compile.log / .status.json、project/Build/Reports/domain-smoke.json、static-api-compile.json、constructed-battle*.json、source-sha256.json、quick-tests-local.log。全起動プロセスは終了exit0。MapOverview.png / UrbanOverpass.pngを実JSONから描画し目視確認、art-render.jsonの2コマンドexit0。これらはオフライン形状図でUnity画面ではない。

Unity実クリック/M入力/範囲表示/新しいNative試験、Windowsビルド実行、FPS/CPU/GPU/GCは**未実施**。静的コンパイルやEditor.logの参照を実プレイ合格の根拠にしない。建物増量の描画負荷は未計測。P0全体完成とはしない。

## Claudeへ（共有文書による引渡し）

直接送信・今回レビュー受領は未確認。旧レビュー/WORK_BOARDにはtask38とレビュー17対応以降、Claudeの新規編集予約はなかった。今回も受領や実行結果は捏造しない。全範囲の予約解除済みなので、追加のGPT承認なしでレビュー/修正/構築を続行できる。

優先して見てほしい箇所:

1. UnityでGame.Tests.Playableの新フローを実行し、Mマップ中の非休止/入力抑制、クリック座標、費用/範囲preview、Pause/死亡/再戦/終了の寿命を確認。
2. TacticalMapViewのReticleRingGraphicは全てCanvasRenderer付き。既存のレーダー例外を再導入しない。Disposeで非表示→親から外す→Destroyし、同一フレーム再戦で旧RootをFindしない。
3. BOTへ真値を返す新しいセンサーを別実装しない。BotPerceptionとApplyControllerAimを通し、Game.AI.BotsのSimulation依存を追加しない。
4. 核は両軍損害・基地損害・占領通常更新を維持。今後運搬/迎撃を足す場合も、敵支援の全位置をAIへ公開しない。
5. 実画面で都市の階段/上階/橋と手動の裏取りを試す。354volumeのFPS/GCを100機で測定し、未計測を合格としない。

具体的な次の作業はUnityでの上記Native/実操作と、必要ならWindows/性能確認。今回のContracts/Simulation/Adapter/Playable/config/生成・描画スクリプト/試験/文書は全解除。ユーザー/Claudeのプロセスは停止していない。
