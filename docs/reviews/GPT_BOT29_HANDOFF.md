# BOT-29 — 少数レーダー・迎撃役 / Claude引継ぎ（GPT、2026-10-17）

担当 GPT。最新ユーザー依頼とFR-001/002/003/107/301/501/502/503/504/505/506/701/705/707/802/805に対応。実装と独立検証完了、全編集予約解除。Unity実機受入は未実施。Claudeの作業表・既存レビューを再確認したが、今回の直接送信・受領・実行結果は未確認。正本/UserText/Scene/Prefab/SO/YAML/GUIDを編集していない。

## 実装と仮設定

- 既定レーダー4%・迎撃8%・残り通常88%。各軍50BOTなら2/4/44。通常役のうち35%（15機）が地図情報を読むため、特殊役を含む地図確認は21機/軍。人間入り49BOTでも2/4/43（地図通常15）、人間/RemoteHumanを抽選から除外。人数は各軍の比率を丸め、枠を試合ごとにシャッフル。独立Bernoulliではなく少数役が一軍へ偏らない人数制御。
- レーダー役は既存support-carbine支援preset（carbine/HP回復FA/レーダー）を4機デッキへ採用。装備を無料追加せず、機体コスト/CT/レーダー2発/補給不可を維持。通常役の既存デッキ選択は維持。各機の役割は試合中固定。
- 自軍本拠点の北/南の侵入経路へ公開nav/地形から設置点を選ぶ。味方レーダーの半径70%以内の重複を避ける。地上で設置し、自分の既存レーダーは再出撃後も確認して保持。設置後は通常行動、破壊/期限切れ後に残弾があれば再設置。最短間隔45秒、寿命等の既存武器設定は変更なし。被ロック/低HP時は設置より戦闘/回復を優先。
- 迎撃役は自軍の生きた本拠点170m以内にある有効共有接触を調査し、情報がなければ侵入口を防衛。通常の地図確認役は自己から180m以内の共有接触を調査する。自軍の索敵が未発見のネズに自動反応しない。
- 判断周期は1.5秒×個体差0.8〜1.2、開始時も分散。接触が少なくとも0.8秒継続した後の判断で追跡し、周期間は報告位置を固定する。期限/妨害/対象ID無効化を毎tick確認し、無効情報を追い続けない。
- 地図目標は移動だけで、敵の隠し位置・射撃座標・ロックを作らない。実戦闘は既存BotPerceptionのFOV/LOS/走査/認識待ちと旋回制限を通す。回復/補給を優先。階層経路の二重切替、既に選択した回復ポッド/本拠点攻撃への地図移動の割込みを修正。被ロック回避が通常格闘をD格へ強制して燃料不足で不発にする干渉も修正し、明示された格闘入力を自動回避が変更しない。
- メニュー/機体制作/参戦準備/Escの既存設定へ3スライダー。0〜100%・特殊役合計100%以内・残り通常役と概算人数を表示。「適用して保存」で永続化し、編成は次のBeginMatchから反映。既存戦闘の役割を途中変更しない。初期値/下書き/キャンセル/保存失敗表示を維持。
- PlayerPreferences schema1に任意のbotMixConfiguredと3比率。旧保存の欠落は既定値、NaN/Infinity/範囲/合計超過を正規化。仮値と理由は実configのbot29Notes。公式のBOT仕様を確認した値とは称していない。

## 変更パス / API境界

以下のContracts/Simulation/Construction/Playable/SettingsはMechaBattle/Assets/Game/Runtime/配下、TestsはMechaBattle/Assets/Game/配下。configとscriptsはMechaBattle/配下。

- Contracts/BotBehaviorRules.cs：BotRole、追加任意ルール、移動専用BotTacticalIntent。
- Simulation/SimulationConfig.cs、Playable/PlayableConfiguration.cs：任意botBehaviorとCopy/Validate/Apply。旧fixtureでは無効、現行Playableでは有効。
- Simulation/BotPopulation.cs、Construction/BotDeckPolicy.cs：BOT専用比率/シャッフルと合法preset選択。
- Simulation/BotBattlefieldPlanner.cs：自己、公知本拠点/地形、味方設置物、有効TeamContactだけを判断に使用。InterceptionOrders/IntelInvestigations/GetThinkCountは診断用。
- Simulation/BotPerception.cs：非地図確認役のマップ方位への振向きを抑止。BotTraversalAdapter.cs・BotLayerRouting.cs：公開移動目標と共有motor/経路を接続。
- Contracts/Settings/PlayerPreferences.cs、Settings/PlayerPreferences.cs、Playable/PlayableSettings.cs・PlayableSettingsView.cs：設定保存互換/スライダー。
- Playable/PlayableEntry.cs：試合開始編成/合法loadout/実CommandSource選択とplanner接続。GetBotRole(slot)で実試合の担当を確認可能。
- Tests/EditMode/BotBehaviorTests.cs、PlayerPreferencesTests.cs、Tests/EditMode/Playable/PlayableFlowTests.cs：比率/共有知覚/実設置/経路/回復/設定の回帰。Nativeフローは静的コンパイルのみ。
- scripts/DomainSmoke/ConstructedBattleSmoke.cs：実Playable編成/plannerと全4マップ100BOT、実設置数・迎撃指示の計測。該当役がいるのに設置/共有迎撃が0なら失敗。
- config/prototype-defaults.jsonとContent/Playable/PrototypeConfig.json、PLAYABLE_GUIDE/DEVELOPMENT/WORK_BOARD。

## 実行した検証

最終 **106純C#・536/536成功（失敗0）**、全20アセンブリのUnity API静的コンパイルexit0。追加BOT20ケース/設定2ケースを含む。部分検証は設定21+BOT20の41/41。現行182入力SHAと検証snapshotの差分0、実config2コピー一致、既存Scene差分を除いたgit diff --check exit0。

実設定の100BOT・全4マップが各600秒完走し、役割に対応した有限レーダー設置・共有接触からの迎撃調査・通常戦闘/撃破/再出撃/本拠点攻撃/CT/全武器弾数・ブースト上限を検査した。設置数には破壊後の再設置を含み、迎撃調査指示数は撃退成功数ではない。

| マップ | 模擬時間 | レーダー設置 | 迎撃調査指示 | 本拠点命中 |
|---|---:|---:|---:|---:|
| 荒地渓谷 | 600秒 | 4 | 193 | 51 |
| 立体市街 | 600秒 | 3 | 124 | 14 |
| 二層都心 | 600秒 | 4 | 136 | 165 |
| 再生工廠 | 600秒 | 5 | 193 | 433 |

証跡：MechaBattle/Build/BOT29_1792198879692/final3/のdomain.log・compile.log・各.log.status.json、project/Build/Reports/domain-smoke.json・static-api-compile.json・constructed-battle-*.json、source-sha256.json・final-checks.json。最終domain PID20056/static28104はexit0、wrapper30280と先行検証/診断の既知18PIDは不在確認。元ソースの背景書込み/検証は残していない。

設定パネルのソース座標を描いた[確認図](../../MechaBattle/Build/BOT29_1792198879692/SettingsPreview.png)を目視済み（Unityスクリーンショットではない）。

修正履歴の失敗も保持：初回の検査武器の盾不一致（32/5）とBOT旋回待ち不足（36/1）はfixtureを修正し、製品の制限は緩和していない。先行acceptedの532/532だけでは二層レーダー0を見逃すため未受入とし、実設置/迎撃の受入assertを追加。階層経路は旧コードでx=-5.60に停滞する失敗（37/1）を再現後、修正で38/38。回復ポッド優先の実設置まで追加して39/39。本拠点攻撃の地図割込みを修正前39/1→40/0、回避による強制D格の不発を修正前40/1→41/0（実BaseHits>0まで）で確認。full final2の立体市街BaseHits=0を受け、入力ログでBoost14.7〜16.5・LastMelee=-1のまま30コストD格へ変換される実原因を確認した。格闘fixture作成時の赤ロック範囲/コア寸法は合法な実条件へ修正している。ログはBuild/BOT29_1792198879692/quick/。先行finalはポッド修正前533/533と全20asm、全4mapでレーダー4/4/4/5基を確認した履歴で、現行最終と区別する。

## Claudeの次の受入 / 未実施

Play停止→Assets > Refresh→コンパイル完了→既存PlayableでPlay。Scene再生成不要。新規5つの.csの.metaは現時点で未生成、Editorに生成させる。GPTからのEditor接続は到達できておらず、権限変更や別Editor起動を行っていない。

1. 設定が開き、3スライダーをドラッグ可能、感度/音量/品質と重ならず、背景画面を押さないこと。0%/100%と特殊役合計、初期値/キャンセル/Esc/保存を確認。
2. 既存保存ファイルのまま起動して4/8/35%、保存後再起動で値を保持すること。戦闘中の適用では現行役割不変、次の試合でのみ担当数が変わること。
3. 各軍のGetBotRoleを確認。人間枠はBOT操作されず、レーダー役の実デッキにレーダーがあり、支援機の武器表示/簡易モデルが一致すること。
4. 全4マップで側面へ設置し、味方の設置物/範囲がHUD/M/出撃マップへ表示されること。二層都心で停止/経路先頭への戻りを繰り返さないこと。設置オブジェクトに従来の低優先ロック/耐久/寿命があること。
5. レーダー破壊/期限切れ/再出撃時の再設置。残弾0で無限設置しない、既存レーダーが生きていれば増殖しないこと。
6. 未発見で側面を通る人間へ防衛役が全知反応せず、有効な索敵/ロック共有後に一部が調査すること。妨害/接触消失後の追跡停止、非地図確認役の振向き抑止、壁越し発砲がないこと。
7. 中HPの回復ポッド・補給・退避と目視戦闘を維持し、地図の調査移動が回復や侵入済み本拠点攻撃の優先を奪わないこと。被ロック中に低燃料でも通常格闘を実行し、通常格闘を自動回避がD格に変えないこと。プレイヤー/明示ブースト中のD格と燃料費は維持。
8. Native Test Runner、Windows x64起動、人間の潜入/前線バランス、FPS/GC/CPU/GPUを測定。独立100BOTの完走は性能や実画面の合格ではない。P0全体を完了扱いにしない。

全ソース/設定/テスト/文書の編集予約は解除済み（GPT予約0）。Claudeは解除範囲のレビュー/実機受入/修正を追加GPT承認なしで継続可能。今回のClaude受領・同意・実行結果は未確認。
