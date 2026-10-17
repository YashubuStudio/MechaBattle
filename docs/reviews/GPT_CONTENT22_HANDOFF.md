# CONTENT-22 — マップ・フレーム・外観拡張（GPT）

2026-10-16。ユーザーの増量・改修依頼、FR-001/108/109/110/401/501/801/805、UserTextのモジュール機体構想に対応。実装と独立検証は完了、Unity実操作受入待ち。全編集予約は解除。

## 遊べる選択肢への接続

|選択場所|内容|
|---|---|
|参戦準備の上部「マップ」|中央工業戦線・段丘工業区・荒地渓谷の3マップ|
|機体制作「フレーム」|標準・重量・軽量・支援の4種|
|機体制作「腕」|標準・補強・軽量の3系統、左右一組|
|機体制作「塗装」|陣営カラー、ホワイト、サンド、オリーブ、グラファイト|
|機体制作の雛形ボタン|旧8種を残して軽量/支援を追加、計10種。2行5列へ整理|

マップ選択では既存の出撃地図/再出撃地図/ミニマップを入れ替え、出撃地点の選択を初期化する。デッキは維持。新しいマップでも従来の拠点クリックで出撃先を選ぶ。試合中の切替は拒否。マップ選択はこのPlayセッション内で保持し、再戦にも使う。個人スコアは別の試合として0に戻る既存動作を維持。

フレーム変更では互換外武器の置換先を選択前に表示し、対応する武器・腕・塗装を保持。新軽量/支援型の既存主武器はFAカービン、重量型は単発キャノン。全フレームとも装備3枠・共通の構築/コスト/CT/補給規則を使用する。

塗装はAppearance.paintIdとして既存デッキ保存へ入り、格納庫と戦闘の両方へ反映する。形状メッシュは色別に複製せず、材料を共用。カスタム色でも識別灯/センサーは陣営の青/橙。ポッド等の設置物は従来の陣営カラー。EditorのGenerateBuildも4塗装を受理する。既存Blue/Orange/NeutralのEditor指定も維持。

## マップ

|名前|範囲|地形ブロック|特徴|
|---|---|---:|---|
|中央工業戦線|480×280m|83|既存データを維持|
|段丘工業区|560×320m|93|共通の中央/左右経路を拡張、段丘を高くして積荷・搬入デッキを追加|
|荒地渓谷|640×360m|89|広い中央の谷筋、岩棚/外周の高台、最大地形高9.2m|

全て各軍3本拠点・3中継。標準の3経路構成を基礎に寸法/高低差と遮蔽配置を変えた試作バリエーション。中央主体のライン戦と左右からの進入を維持する。荒地は暖色、工業区は寒色の地面/構造物へ切替。共通の3D移動/ジャンプ/上昇規則で地形を扱う。forward-center-map.jsonは従来の別実験マップとして保持し、今回の選択可能3種には含めていない。

source: config/map-catalog.json、standard-map.json、terraced-works-map.json、canyon-cross-map.json。node MechaBattle/scripts/content/sync-map-catalog.mjs でAssets/Game/Resources/MechaBattleMapsへ同期する。RuntimeはResourcesから読むので既存Sceneのserializedフィールド追加やScene再生成は不要。Resources欠落時には既存Configureされたマップだけで起動する。データがあるのに不正な場合は検証エラーを返す。

## フレームとモデル

|素体|HP|盾|速度|DP耐性|対応武器|
|---|---:|---:|---:|---:|---|
|標準|2000|900|12|600|13種|
|重量|2650|1100|9.5|750|8種、従来通り|
|軽量|2000|600|14|550|10種、キャノン/バズーカ/継続ビームを除く|
|支援|2300|950|10.5|650|9種、キャノン/バズーカ/SA/チャージを除く|

軽量腕は片側あたり重量6/出力8/盾-50/速度+0.4/DP耐性-25/コスト15。左右で盾-100/速度+0.8/DP-50、HPは下げない。標準腕と同じチャージ格闘特性を共有。補強腕のHP+1200/速度-1等は従来通り。全数値は仮値で、実configのcontent22Notesに意図を記録した。公式の当時数値を確認したとは扱わない。

原型19moduleの各JSON内容を維持し、軽量/支援フレームと軽量腕左右の4moduleを追加、計23module。10組立・230閉立体・9288module合計triを検証。Light_Thigh/Support_Thighも脚の上側へ分類するよう描画ライブラリを一般化し、既存の膝を使う手続きモーションへ接続。共通ソケット/保持位置/約2.47m高を使用。新しい個別当たり判定やリグの実機受入を主張しない。

## 主な実装パス（MechaBattle以下）

- Runtime/Contracts/MapCatalog.cs: MapCatalog/MapCatalogEntry（schema/id/title/description/resourcePath/theme/sourceFile）。純C#、Unity参照なし。
- Runtime/Battlefield/MapCatalogPolicy.cs: 重複/パス逸脱/ID不一致/既存MapValidatorの制約を検証。
- Runtime/Construction/BuildAppearance.cs: 共通の塗装ID/色、性能に依存しない。
- Runtime/Playable/PlayableContentSelection.cs: マップ/塗装の選択。PlayableEntry/PlayableUI、PlayableMinimap/DeploymentMapView、MechaMeshLibrary/PlayableWorldViewを接続。
- Editor/ModelTools/SimpleMechaPrefabBuilder.cs: 追加塗装を既存Editor生成へ接続。
- scripts/art/generate-simple-mecha.mjs、render-simple-mecha.py、render-map-overview.py: モジュールとオフライン一覧/地形図。
- Tests/EditMode/ContentCatalogTests.cs、ModelTools/ModularMechaTests.cs、Playable/PlayableFlowTests.cs、scripts/DomainSmoke/ContentExpansionSmoke.csほか。

MapDefinition/BuildDefinitionの既存保存形式は変更なし。MasterPlanとUserTextは参照のみ。Scene/Prefab/.asset/.metaを直接編集していない。新規Assets9ファイルの.meta存在を最後に読取確認し、GPTによるGUID手書き/再生成はなし。

## 実行した検証

隔離 MechaBattle/Build/CONTENT22_1792160951668/、コード/設定/Resources149対象+アート/スクリプトを含む全165対象SHAを保存、現行差分0。

- 純C#438/438（追加pure13+統合1）、87ソース、exit0。
- 全20asmの静的APIコンパイル成功、exit0。
- 全フレームの合法な3武器構成13314件、標準6591構成から各4frameへの26364変更、50塗装保存復元/性能不変。
- ResourcesとconfigのマップJSON一致、各軍3本拠点/高低差/物理データの検証、モデル/カタログID一致。
- モデル23module/10preset/230閉立体/9288tri、隔離再検査exit0。旧19moduleの内容不変を確認。
- 100BOT新プリセット入り標準戦:423.34sim秒、65撃破/164出撃/276本拠点命中。
- 100BOT段丘:600sim秒の時間切れ判定、66撃破/165出撃/130本拠点命中、中央滞在81.2%。
- 100BOT荒地:600sim秒の時間切れ判定、50撃破/149出撃/32本拠点命中、中央滞在77.9%。

新2マップは終了まで進み、射撃/撃破/再出撃/本拠点攻撃を実行した。どちらもゲージ0決着ではなく制限時間での判定。新マップの人間の操作感/最終バランスの合格とは区別する。50対50やBOTの資源規則を緩めていない。構築試合の全デッキ4枠、指揮官別枠、本拠点主目的を維持。

Nativeには新フレーム/腕/塗装と識別灯・両マップの出撃・地図再構築・試合中変更拒否、Editor4塗装生成の検査を追加。既存の8preset/DP600固定の試験は、新10presetの実装データ（軽量DP500を含む）と装備種を照合する形式へ更新した。テストの削除/無効化はしていない。Nativeは静的コンパイルのみで未実行。

証跡: project/Build/Reports/domain-smoke.json / static-api-compile.json / simple-mecha.json、constructed-battle*.json、domain-run.log / static-compile.log / model-validation.log / model-preview.log / map-preview.log、各.status.json、source-sha256.json。

## 画像と未実施

ArtSource/MB_Worker01/MB_Weapons_preview.png（10機）、MB_Paints_preview.png（5塗装）、上記隔離先MapOverview.pngを生成して目視確認。先頭PATHのPythonには依存がなかったが、既存D:/Tools/Python/Python39/python.exeのNumPy1.26.2/Pillow10.1.0で描画に成功。外部依存の追加導入なし。これらは元データのオフライン描画でUnity画面ではない。

Unityの実クリック/実描画/保存再起動・新Native試験、Windows/FPS・GCは未実施。新マップの地形/追加モデルの性能への影響は未計測。最新Editor.logの末尾にC#エラー/例外は見当たらなかったが、実機合格の根拠にはしない。P0全体の完了宣言はしていない。

## Claudeへ

直接通信/レビュー受領は未確認。共有WORK_BOARDと本書で引き継ぐ。Contracts.MapCatalogの用途は前線の実情ではなく公開マップの選択だけで、BOTへ敵情報を追加する変更はない。地図切替は古い生成要素を非アクティブにし親から外してDestroy、新要素を作成する。遅延Destroy中の古いボタンをFindしないようにし、RectMask2Dの重複追加も防止。Canvas全体は作り直していない。このライフサイクルとteam識別色、new frameの脚分類を重点レビューしてほしい。

次はPlay停止→Assets > Refresh→コンパイル完了→Play。参戦準備「マップ」で新2種を選択し、拠点クリックから出撃。機体制作のフレーム/腕/塗装を試し、保存→Play再開で塗装が残るかを確認。Scene再生成不要。実装/アート/ドキュメントの全編集予約解除、GPT起動の全プロセスは終了。Claudeは解除済み範囲を続行可能。
