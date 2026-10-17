# RESULT-20 — 個人の試合終了スコア（GPT）

2026-10-16。要件 FR-801/805、ユーザー指定の与ダメ/被ダメ/回復/被回復/拠点ダメ数値表示。実装・独立検証完了、Unity実画面受入待ち。全編集予約解除。

## 表示と集計

試合終了画面の右側に「あなたの試合成績 / 全出撃の合計」を追加。左側の全軍サマリー、勝敗、再戦/機体構築への入口も維持。5数値は右揃え・桁区切り・整数への丸めで表示し、撃破/被撃破も添える。

|項目|集計する量|
|---|---|
|与ダメージ|敵機体のHPと盾に対する確定ダメージ。過剰ダメージ・敵本拠点・設置物は含めず、本拠点分は別表示|
|被ダメージ|自機のHPと盾の実損失。本拠点反撃も含む|
|回復量|回復FA/自分の設置ポッドで、他の味方に実際に戻したHP。自分への回復は提供量へ加算しない|
|被回復量|味方の回復FA、自己/味方ポッド、本拠点補給で自分に実際に戻ったHP|
|拠点ダメージ|敵本拠点に受理されたダメージ|

回復量は盾・弾薬の補給を含まない。満タンへの空撃ち、過回復、再出撃時のHPリセットは加算しない。定義は画面にも短く表示した。全項目は操縦slotごとに保持するのでデッキ機体変更・撃破・再出撃をまたいで累計。次の試合では新規Simulationで0に戻る。終了後のStepは既存ルールどおり停止して最終値を維持する。

## 実装

MechaBattle/以下の変更:
- Assets/Game/Runtime/Simulation/BattleSimulation.cs: UnitStateへ累計を追加、GetSlotStatsへ渡し、本拠点のHP補給をRestoreHealthへ接続。
- Runtime/Simulation/SimulationSnapshots.cs: 既存SlotStatsSnapshotへDamageReceived/HealingDealt/HealingReceivedを追加。既存DamageDealt/CoreDamageDealtも含め累計5項目はdouble。
- Runtime/Simulation/BattleFeedback.cs: 同時攻撃の確定HP/盾損失から被ダメを1回加算。確定した攻撃/本拠点/撃破の戦績をcontroller slotへ加算し、弾/地雷が旧機体IDのまま後から当たっても捨てない。
- Runtime/Simulation/BattleSupport.cs: RestoreHealthで実際のHP差分を集計し、回復FA/ポッド/本拠点補給で共有。
- Runtime/Simulation/BattleDeployables.cs: ポッド回復の提供者をOwner slotとして集計。OwnerIdが古くても試合の提供量は保持。
- Runtime/Playable/PlayableEntry.cs / PlayableUI.cs: Resultsの個人5項目と撃破/被撃破を表示。
- Tests/EditMode/PersonalResultTests.cs: 新規16ケース。
- Tests/EditMode/Playable/PlayableFlowTests.cs: 終了画面の5数値/枠内/非raycast、再戦で累計0の確認コードを追加。

共有Contracts・保存形式・configの性能値・正本・UserText・Scene/Prefab YAMLに変更なし。旧IDでも戦績を加算するのは試合累計だけ。現在機体への格闘ヒット時boost還付/演出のUnitId照合は維持し、別機体への効果を発生させない。

既存Snapshotの利用側は内部ctor、PlayableEntryのPause戦績、ScoreboardTests。doubleへ変更した影響を全20asmで静的確認。新規統計型を別途導入せず既存の表示専用Snapshotを拡張した。AI/Observationへ戦績を渡す変更なし。集計は確定処理で加算し、表示イベントリングを後から集計しないため2048件を超えても欠落しない。

## 検証

隔離 MechaBattle/Build/RESULT20_1792154553015/ にコード/設定134対象を保存、82純C#ソースで独立コンパイル。部分45/45、新規16を含む全424/424成功、静的20asm成功、各exit0。134SHAの現行差分0、config2コピー一致、Scene除外diff整形exit0。

新規ケースは与被ダメの上限/盾/同時攻撃/本拠点反撃、回復FAの上限・敵/満タン非加算、基地補給、自己ポッドの被回復だけ加算、owner再出撃後も味方ポッド回復を帰属、被回復の再出撃保持、旧ID弾の与ダメ/撃破/本拠点帰属、イベントリング期限、次試合リセット、終了時凍結を検査。実100BOT構築試合は437.41998sim秒、88撃破/187出撃/119コア命中で完走（Windows/FPSの証拠ではない）。

ログ/レポート: domain-compile.log、quick-tests.log、domain-run.log、static-compile.log、各.status.json、project/Build/Reports/domain-smoke.json / static-api-compile.json、source-sha256.json。Editor.log最新tailも読取保存したがC#エラー/例外がないことを表示/テスト実行の証拠にしない。

Native終了画面/Unity Test Runner、Windows/FPS・GCは未実施。PersonalResultTests.cs.metaは確認時点で未生成。既存GUIDを変更せずEditorのRefreshで生成させる。UIはコードから既存Canvasへ追加するためScene再生成は不要。

## Claude向け

直接送信/レビュー受領は未確認、WORK_BOARDと本書で共有。Claudeの既存SlotStatsSnapshotと戦績集計を拡張した。DamageDealt/CoreDamageDealtの型変更、旧UnitIdでもslotの累計へ加算する変更をレビューしてほしい。被弾方向/格闘還付の現在機体ID検証はそのまま維持している。前回FRAME-19のフレーム選択/互換性/表示修正も今回snapshotに含まれ全回帰に成功。

次の手順: Play停止 → Assets > Refresh → コンパイル完了 → 既存PlayableでPlay → 射撃/回復などを行い試合終了 → 5数値を確認。追加NativeフローはGame.Tests.Playable.PlayableFlowTests。自分が撃破/再出撃した後も累計が残り、同じデッキで再戦すると0から始まること、桁/フォント/クリック領域も実機確認する。全予約解除済み、GPT起動の全検証処理は終了。
