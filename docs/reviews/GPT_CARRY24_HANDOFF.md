# CARRY-24 — 携行核・緊急帰投 / GPT → Claude

更新: 2026-10-16T16:29:23.432Z。owner GPT。実装保存・独立検証完了、Unity実操作受入待ち。仕様正本を実装済み説明に変更していない。STRATEGY-23引継ぎの「核の運搬未実装・地図地点へ直接爆発」は今回の仕様で置換する。

## 操作と結果

- **M → 戦略核 → 自軍本拠点の24m以内 → 要請**。敵本拠点や前線を直接爆発地点に指定できない。核の受取場所に有限耐久の箱型オブジェクトを生成。600pt、各軍1基、CT90秒、耐久2400、寿命600秒。初期150pt/味方撃破ごと25pt、上限1000は維持。
- **3m以内・遮蔽なしでF**。味方が拾い、通常の走行/ダッシュ/上昇/燃料/被弾条件で運搬。通常武器と盾・帰投は使用不可。箱と両腕の運搬姿勢を表示。
- **着地後に左クリックを新しく押す**。現在位置で起動し3秒後に半径34mへ機体6500/本拠点4500の損害。味方も巻き込む。空中から押し続けて着地しても自動起動しない。地図を閉じるだけ/拾うだけでは爆発しない。
- **Fで手放す／被撃破・転倒で落とす**。味方が再取得して継続可能。再取得待ち0.5秒。空中投下は現在高度以下の地面へ落下する。屋上の最高面へ吸い上げない。
- 核本体は設置物プールの実体で、通常の射撃/爆風/格闘・低優先度ロックの対象。破壊は誘爆せず、起動後でも停止できる。運搬前・運搬中の敵核を全知マップ公開しない。敵核の3D表示は自機の射界/距離/LOS内、起動済み核は両軍へ警告。機体優先ロックの既存規則を維持。
- **Bで緊急帰投**。地上6秒の待機後、非撃破の補給/再出撃画面へ移る。移動/ジャンプ/ブースト/攻撃/装備切替/装填/F/被弾/行動阻害/再Bで中断。完了tickの被弾も中断、無敵なし。空中・核運搬中は拒否して理由表示。
- 帰投は死亡/ゲージ損失/味方損失ポイント/機体CTを発生させない。同じ機体のHP1200/盾600/補給可能弾30毎秒を補給し、地図で有効な本拠点/占領中継を選択して再配置。補給禁止のポッド/レーダー弾は増やさない。別デッキ変更や再配置瞬間の全回復はしない。再配置でUnitIdを更新し古い入力/ロックを無効化、装備/弾数/試合累計を保持。実HP回復だけを被回復成績へ加算。

数値は歴史的な公式仕様の確認値ではなく、実configのcarry24Notesに理由を記録した仮値。核の通常占領フラグ変更は禁止を維持。100操縦枠・別枠指揮官2・各軍3本拠点・有限資源・BOT情報制約を緩和していない。

## 共有契約と配置

すべてMechaBattle/Assets/Game/配下（configだけMechaBattle/config/）。契約の提案・利用先確認は作業表に先行記録済み。

- Runtime/Contracts/BattleContracts.cs: UnitCommand末尾にoptional EmergencyReturn=false。既存UseAbilityを拾得/手放しに使用。CombatInputAdapterとBotTraversalAdapterの命令コピーで伝播し、既存lockedDeployable参照も維持。
- UnitLoadout.cs: WeaponKind.StrategicNuclear=7。既存enum値0〜6は維持。構築の通常装備カタログへ追加せず、支援生成物の内部定義のみ。
- StrategyTypes.cs: NuclearPhase、StrategyRulesの受取/拾得距離・耐久・寿命、StrategySnapshotの段階/CarrierSlot/DeviceIndex/DeviceId/耐久を追加。既存利用先向けoptional trailing引数を維持。
- ReturnTypes.cs: EmergencyReturnSnapshot(Channeling, InReserve, SecondsRemaining, Progress)。
- Runtime/Simulation/BattlePortableNuclear.cs: 受取・拾得競合・携行・起動・落下・再取得・破壊・公開情報制御。BattleStrategy.csは地図要求検証と段階別更新、既存両軍範囲損害に接続。携行はslotとUnitId、核はpool indexとdeviceIdを照合。
- Runtime/Simulation/BattleEmergencyReturn.cs: 帰投開始/中断/完了/待機補給。BattleDeployment.cs/BattleInventory.cs/BattleSimulation.csが非撃破・保持在庫・同機体再配置を接続。FinishEmergencyReturnsを被害解決後に実行。
- Runtime/Playable/PlayableEntry.cs/PlayableStrategy.cs: B/F入力、帰投待機への遷移、HUD、ボタン/試験用RequestEmergencyReturn/InteractNuclear。通常死亡と帰投を区別し、Return画面は現在deck以外を無効化。
- Runtime/Playable/TacticalMapView.cs: 自軍側の受取地点選択と有効性/費用/理由表示。核の発動範囲を任意地図にpreviewする旧導線を受取範囲へ変更。
- StrategyWorldView.cs: 核箱/把手/識別灯/耐久/カウント、機体向き・携行位置追従、共用materialを終了時破棄。シミュレーションの衝突を使うため表示primitiveのColliderは即時disable後Destroy。通常CombatWorldViewで核の重複表示を省き、LocomotionWorldViewで携行姿勢と通常武器/盾非表示。
- GetStrategy(index, viewerTeam): 未起動の敵核を陣営へ公開しない。GetStrategyForWorld(index, viewerSlot)は物理表示用で、AI観測や共有マップの真値に利用しない。IsCarryingNuclear/GetNearbyNuclearDevice/GetEmergencyReturn/GetActionFeedbackは読み取りAPI。
- SimulationConfig.cs/PlayableConfiguration.cs、config/prototype-defaults.jsonとContent/Playable/PrototypeConfig.jsonは一致。設定値・有限値検証と機能無効時の従来P0挙動を維持。

## 検証済みの範囲

証跡はMechaBattle/Build/CARRY24_1792167268499/accepted/。機械可読一覧はGPT_CARRY24_VALIDATION.json。

- 最新純C#98ソースの独立回帰 **498/498、失敗0**。新27件（携行核14、帰投13）。部分83/83も成功。部分Quick.dllと対象DLLは同じフォルダーに配置して旧同名DLL解決を避けた。
- 敵側指定拒否/非消費、拾得チーム/距離/LOS、空中保持と地上新規トリガー、通常攻撃禁止、同時拾得、転倒/死亡drop・味方継続、破壊で起動取消、未起動の敵情報非公開を検証。既存核範囲試験も実際に拾得/運搬/起動してから友軍・拠点被害を検証。
- 帰投の移動/攻撃/ジャンプ/ブースト/再B中断、同tick被弾、死亡との区別、非撃破のゲージ/ポイント/CT、徐々にHP/盾/弾補給・補給禁止維持、再配置とUnitId更新、累計成績と新試合resetを検証。
- Unity参照DLLに対する静的 **20アセンブリ**、最終static-final.log exit0。既存JSON読み込みフィールドのCS0649警告のみ。Unity import/Nativeテスト/Playerビルド成功の証拠ではない。
- 既存100BOTが4マップで完走。標準150.70秒・60撃破・159出撃・282本拠点命中。都市600秒時間切れ・116撃破・215出撃・35本拠点命中・両軍最小47/48・平均生存99.394。今回の機能をBOTが自動使用しない通常試合はSTRATEGY-23と同値。AIの核運搬成功を示すものではない。
- 最終165対象SHA現行差分0、実config2コピー一致、既存Sceneの空白警告を除外したdiff整形exit0。Scene/Prefab/SO YAML、正本、UserText、.gitは編集していない。
- NativeフローPortableNuclearAndReturnKeepTheirInputsAndReserveScreenConnectedをPlayableFlowTests.csに追加。M要請→F拾得→B拒否→F手放し→B帰投→待機画面→同機体再配置と表示オブジェクトを検査するコードまで静的コンパイル済み。**未実行**。

## Claudeへ残す実機受入

UnityのIPCへこの実行ユーザーから接続できないため、Unity実操作/新Native実行/Windowsビルドと実行/FPS・GCは未実施。アクセス権変更や二重Editor起動はしていない。5新規.csの.metaは現時点で未生成で、Unity Assets > Refreshに任せる。GUIDを手書きしない。

1. 未保存Sceneを保護し、Play停止 → Assets > Refresh → コンパイル完了 → 既存PlayableでPlay。Scene再生成は不要。Consoleと新規.meta生成を確認。
2. 既存Nativeフローと新フローを実行。手動でBのHUD6秒・被弾/移動中断・待機補給/弾数・出撃先選択・被撃破数不変を確認。
3. 核の必要ptまで通常戦闘、または専用試験構成で初期ptを確保し、M自軍本拠点→F→運搬→着地左クリック。常用configのコストを試験目的で無断緩和しない。空中保持、F/drop、味方拾得、転倒、起動後の敵射撃破壊、3秒後の両軍範囲被害を確認。
4. 箱/両腕/武器盾切替・HUD文字の重なり・敵未起動核の地図非公開・起動警告/耐久/低優先ロックを実描画確認。屋内/屋上で空中落下が上の階へ吸い上げられないことを確認。
5. Windows x64 Playerと性能受入。自律BOTによる核要請/運搬/起動、緊急帰投判断、指揮官の戦略判断は今回未接続。引き取る場合は新taskを作業表へ記録し、共有APIの情報制約を守る。

Claudeへの直接送信・今回のレビュー受領は未確認。共有作業表と本書へ保存した事実だけを引渡し記録とする。解除済みファイルのレビュー/修正/実機検証は追加GPT承認なしで継続可能。参照API/表示と実ドメインの矛盾を見つけたらreviews/と作業表へ実測付きで返す。

## 予約とプロセス

CARRY-24全実装ソースは先行区切りですべてlock: released。文書4件も最終WORK_BOARD記録で解除。domain PID7676、初回static PID33936、最終static PID35568はすべて終了exit0を.status.jsonで確認。元ソースへの書込み/バックグラウンド生成なし。ユーザー/Claudeのプロセスや未保存Editor状態は変更していない。
