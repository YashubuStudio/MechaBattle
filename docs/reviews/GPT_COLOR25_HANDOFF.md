# COLOR-25 — 原色塗装・固定陣営識別 / GPT → Claude

更新: 2026-10-16T22:38:26.765Z。owner GPT。要件: GAME_VISION §6、UserText機体構想 §3.3、FR-802/805と最新ユーザー指定。実装と影響範囲の独立検証を完了、Unity実機受入待ち。以前の「識別灯だけを陣営色として残す」扱いを今回置換する。

## 変更内容

- 選択可能な塗装を陣営カラー・白・黄・緑・黒・赤・青にした。黄/緑は彩度を上げ、白/黒も明瞭化。保存ID Neutral/White/Sand/Olive/Graphiteの順序を保持し、末尾にRed/Cobaltを追加。UI名Sand→イエロー、Olive→グリーン、Graphite→ブラック。旧保存は引続き読める。性能/コスト/CTは変更しない。
- 肩装甲・補強肩・膝関節・頭頂装甲の全三角形を既存Signalチャネルへ割当。標準/重量/軽量/支援フレームと標準/補強/軽量腕の20識別面。胴体等の選択色・暗い内部フレームとは独立する。
- 陣営色はBuildAppearance.TeamTintをRuntimeとEditorで共用。青RGB(.04,.26,1)、橙RGB(1,.36,.015)。Neutralの未割当モデルは灰。センサー/識別面も同じ固定色。Emission係数0.2、装甲metallic0.1は彩度が白く飛びにくい仮値で、実configのcolor25Notesへ記録。
- Runtime MechaMeshLibraryのパレット共有・メッシュ共有・enableInstancingを維持。選択色ごとの材質と陣営識別材質を共用。3材質のまま、追加メッシュ/オーバーレイなし。既存全23moduleの頂点・法線・三角形の組は不変。
- Editor SimpleMechaPrefabBuilderは指定陣営と選択塗装を独立処理する。たとえば白塗装をBlueで生成しても胴体は白、肩/膝/頭頂は青。色別マテリアル名を分け、後から別塗装生成で共有Armor_Blueが上書きされる事故を防ぐ。既存生成GUID更新方針は維持。

## 対象ファイル

MechaBattle/Assets/Game/Runtime/Construction/BuildAppearance.cs、Runtime/Playable/MechaMeshLibrary.cs・PlayableContentSelection.cs、Editor/ModelTools/SimpleMechaPrefabBuilder.cs、Tests/EditMode/ContentCatalogTests.cs・ModelTools/ModularMechaTests.cs。

MechaBattle/scripts/art/generate-simple-mecha.mjs・render-simple-mecha.py、config/prototype-defaults.json・Content/Playable/PrototypeConfig.json。Content/Models/MB_Worker01/のkit/単体meshJSON/OBJ/MTL/model-specとArtSource/MB_Worker01/のGLB/assemblyJSON/3画像/READMEはスクリプトで再出力。

Contracts/戦闘状態/AI/機体性能は変更なし。正本・UserText参照のみ。Scene/Prefab/SOのYAML・.meta/GUID・.gitは編集していない。

## 検証

証跡: MechaBattle/Build/COLOR25_1792190070943/。

- 最新98純C#ソースを隔離コンパイル、影響対象ContentCatalogTests/ConstructionTests/FrameConstructionTests **57/57成功**。color-check.log / color-check.status.json、DomainSmoke.dllとColorQuick.dllは同じディレクトリに配置し旧同名DLL解決を避けた。
- 10雛形×7塗装 **70通り**の保存復元、塗装ID/HP/盾/移動速度/コスト/CT不変を実catalogで確認。同じログに記録。
- **20アセンブリの静的APIコンパイル成功**。static-compile.log / .status.json、project/Build/Reports/static-api-compile.json。JSON読み込みフィールドの既存CS0649警告のみ。Native/Unity importの成功を示すものではない。
- model-color-checks.json: 23moduleで全頂点/法線/三角形の組が変更前と一致、3材質、全20識別面の全頂点がSignal材質へ属する。4frameすべてhead+両膝、6armすべて肩面を持つ。
- generate-models.json・art-render.logはexit0。MB_Paints_preview.png（7色×青正面/橙背面）とMB_Weapons_preview.png（10機）を目視。白/黄/緑/黒/赤/青の胴体を変えても指定部位に陣営色が残る。画像は実メッシュ元データのオフライン描画で、Unityの露出/照明/被弾描画は再現しない。
- 既存ModularMechaTestsの塗装検査を6カスタム色と両軍に拡張。静的コンパイル済み、Native実行は未実施。既存PlayModeのSand選択試験も保存IDは変わらず維持。
- 戦闘ロジック/設定値を変えていないため、今回の検証は塗装/構築に限定。498全回帰/100BOT戦闘を今回再実行してはいない。前回CARRY-24の498成功と混同しない。

## 反映とClaudeの次の一手

1. Play停止→Assets > Refresh→コンパイル完了→既存PlayableでPlay。module-kit.jsonは既存参照TextAssetなのでScene再生成は不要。
2. 機体制作「塗装」の7選択、旧デッキ保存の復元、新しいRed/Cobaltの保存再起動、青/橙陣営切替を確認。Sand/Olive/Graphiteは旧IDのまま黄/緑/黒になる。
3. カスタム色の機体を実際に出撃させ、前面/背面/斜め/屋内影/近距離/交戦距離/歩行/膝曲げ/核携行で識別面と姿勢追従を確認。必要なら発光/照明を実測して調整、固定陣営面を選択塗装へ戻さない。
4. 永続化されたGenerated/Prefabも使う場合はEdit ModeのModels → Generate Simple Robot Prefabsを実行。今回GPTはPrefabを直接更新していない。既存ModularMechaTestsで共有メッシュ/GUID/カスタム色+陣営色分離を確認する。
5. Unity実操作/Native/Windowsビルド・実行/FPS・GCは未実施。描画負荷は材質チャネル数/形状不変という構造確認までで、性能達成とはしない。

Claudeへの直接送信/今回受領は未確認。共有作業表と本書へ保存したことだけを引渡し記録とする。解除済み範囲のレビュー・実機受入・改修は追加GPT承認なしで続行可能。

## 予約と処理

全実装ソース・生成物は区切りで解除済み。文書2件も最終WORK_BOARD記録で解除。静的PID32380・描画PID15584は終了exit0、純C#コンパイル/回帰も終了。元ソースのバックグラウンド書込みなし。他者プロセス/未保存Editor状態には触れていない。
