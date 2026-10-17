# DEPLOY-30 — 同時設置数 / Claude引継ぎ（GPT、2026-10-17）

実装と独立検証完了、全編集予約解除。Unity実機受入は未実施。最新ユーザーの明示仕様（レーダー/回復ポッド各3個、地雷5個）をFR-301/304/705/707/802/805へ適用。Claude側の最新作業表/レビューに追加競合なし、今回の直接送信/受領は未確認。

## 保存した実装

- BattleDeployables.cs：Slotを所有者として種類別3/3/5。装備の複数枠・デッキ・再出撃で別枠にせず、別プレイヤーには影響しない。上限では新規設置成功時に同種の最も古い現存IDを置換。寿命の短い物を最古と誤判定せず、期限切れは同時数から除く。設置失敗で弾数を使わない従来処理は維持。
- 最大容量は1102（100Slot×11設置物+各軍の核2）。Simulation.MaximumDeployables、設置物実体・損害ID/損害値/impact-head配列を同期。Projectileの512枠は別用途で変更なし。
- CombatWorldView.csの種類/陣営バッファはworld.DeployableCapacityで確保、PlayableMinimap.csのアイコン/レーダー範囲配列はMaximumDeployablesで確保。512超で再発する表示IndexOutOfRangeを避ける。ContractsやScene/Prefab/SO/YAML/metaを編集していない。
- 実configのレーダー弾2→3、地雷4→5、ポッド3維持。既存保存の装備IDを変えず、同じ構築手順で新しい弾数を解決。補給禁止/弾倉/装填/寿命/HP/索敵/回復量は維持。最新値と理由はdeploy30Notes、過去ノートの同時数/弾数より最新指示を優先。
- EquipmentRulesTests.cs：上限と+1・所有者・複数枠・再出撃・寿命差による最古判定・全100人1100個・高index594の実ロック/損害。ObjectTargetTests.csは3個を保持した後の4個目置換時に旧ロック/未解決損害が新IDへ移らない回帰へ更新、範囲外indexは実容量を使う。
- ConstructionDefaultsSmoke.cs：実カタログがレーダー3/ポッド3/地雷5の上限まで置ける弾数を持つことを検査。guide/COMBAT_RULESを更新。

上記Runtime/TestsはMechaBattle/Assets/Game/以下。実config2コピーはMechaBattle/config/prototype-defaults.jsonとAssets/Game/Content/Playable/PrototypeConfig.json。検証スクリプトはMechaBattle/scripts/DomainSmoke/以下。

## 実行検証

部分67/67成功。旧BattleDeployablesで現行回帰を動かし58成功/9失敗を再現後、現行へ戻して全成功。MechaBattle/Build/DEPLOY30_1792201802813/quick/checks-before-fix.log・checks-after-fix.log。純C#上の1100個実設置と594番へのロック/80損害が成立している。最終は全純fixture **535/535**・静的 **20アセンブリexit0**。先行finalの統合7件（全4マップ100BOT各600秒/全構築/既存P0など）は全成功。先行の全体結果540/2の2失敗は旧1個上限fixtureだけで、Radar/確定格闘の検査を3個共存→4個目置換へ更新した。格闘の元の特典なし期待値70・旧ID非転送は維持し、期待値を弱めていない。

再検証で変えた入力はTeamRadarTests.cs・ConfirmedMeleeTests.csの2つだけで、Runtime/JSONは先行統合とSHA同一。不要な試合再実行を避けたため、一度に542件成功したとは記録しない。acceptedの535純検査はfixtures-only Programへ隔離変更した検証器で実行、元のscripts/DomainSmoke/Program.csは変更していない。実行器差分とSHAはharness-override.json、182入力の現行差分0・config2コピー一致・git diff --check（既存Scene除外）exit0。

| マップ | 模擬時間 | 実レーダー設置 | 本拠点命中 |
|---|---:|---:|---:|
| canyon-cross | 600秒 | 4 | 65 |
| urban-overpass | 600秒 | 4 | 3 |
| layered-city | 600秒 | 4 | 94 |
| reclamation-yard | 600秒 | 6 | 547 |

最終証跡：MechaBattle/Build/DEPLOY30_1792201802813/accepted/domain.log・compile.log・各.status.json・source-sha256.json・harness-override.json・final-checks.json・project/Build/Reports/。先行統合と失敗2件はMechaBattle/Build/DEPLOY30_1792201802813/final/に保持。既知のwrapper/domain/static計6PIDは不在、GPT背景検証なし。

## Claudeの次の受入 / 未実施

Play停止→Assets > Refresh→コンパイル完了→既存PlayableでPlay、新しい試合を開始。Scene再生成不要。既存IDの武器を使い、新規.cs/metaは今回追加していない。

1. レーダー3・ポッド3・地雷5をそれぞれ設置し、モデル/ミニマップ/M/出撃マップに同時表示されること。上限の次の設置は同種の最古だけを置換。
2. 装備枠や機体/再出撃を変えても合算し、味方別プレイヤーの設置物を消さないこと。寿命/破壊で空き、補給禁止弾が増えないこと。
3. 敵の設置物への低優先ロック、破壊/置換時の解除・新IDへのロック非転送、Radar範囲と攻撃エフェクトを確認。512超の物が存在する条件でモデル/ミニマップが範囲外例外を出さないこと。
4. Native Test Runner、Windows x64、人間のプレイバランス、増加時のFPS/GC/CPU/GPUを測定。独立1100設置はUnity描画や性能の合格ではない。P0全体は未完了。

実装/設定/試験/文書すべての予約解除済み（GPT予約0）。Claudeは解除範囲を追加GPT承認なしでレビュー/受入/改修可能。今回の直接送信/受領は未確認。
