# STUCK-31 — BOTスタック復旧・しゃがみ / Claude引継ぎ（GPT、2026-10-17）

実装保存・独立C#568/568・20アセンブリ静的コンパイル・全4マップ100BOT検証完了。Unity実機受入は未実施。最新ユーザー依頼、FR-001/003/107/301/304/802/805へ対応。Claude最新作業表/既存レビューに新規競合なし、今回の直接送信/受領は未確認。

## 判断と境界

- BotTraversalAdapterの最終UnitCommandと自己の連続3D座標を比較。移動要求0.1以上・約2秒・期待移動0.5m以上なのに実移動が0.3m未満の停滞を観測する。個体で窓を最大24%ずらす。意図停止/格闘硬直/格闘溜め/行動阻害/人間・RemoteHumanは除外する。
- 公開AABB・本拠点・マップ境界を使って8方向の空きと元の進行方向を調べ、1.2秒の回避を最大2回。接線から離れる合法方向を安全余白だけで塞がない。経路cache/回避waypointを破棄して再計算させ、十分な実変位が出れば通常行動へ戻す。後退→同じ閉塞への短周期再入も記録し、反復時は帰投へ移る。敵の真の位置は読まない。
- 回避失敗時は攻撃を止め、着地後に通常の緊急帰投を一度だけ要求。チャネル中は移動/攻撃/武器変更を止める。被弾等のキャンセルと最低2秒の再試行待ちを尊重し、無敵/ワープ/資源付与/CT省略を行わない。核運搬時は帰投を要求せず回避のみ。
- 帰投BOTは既存のreserve補給で実HP/盾/燃料と補給可能武器の弾が満たされるまで待ち、同じDeckを別の生存本拠点へRequestSpawn。補給禁止のポッド/レーダー弾は増えない。人間/RemoteHumanの待機と出撃先の手動確定は維持する。
- 左Ctrl保持で地上静止しゃがみ。移動/ジャンプ/ブースト/格闘/行動阻害で解除、通常射撃は可能。帰投中は自動しゃがみ。通常/OH回復とも×2（200/40毎秒）、空中/硬直中の回復禁止、容量上限/満タンOH解除を維持。不可視reserve補給には倍率を付けない。衝突寸法や攻撃力を変えない。
- 腰を0.3m下げ、股/両膝/胴体を既存補間へ接続。カメラのpivot/headも追従。新規素材/Scene/Prefab/SO/YAML/metaの直接編集なし。

## 変更パス

MechaBattle/Assets/Game/配下：Contracts/BattleContracts.csはRuntime/Contracts/、その他Runtime/Simulation/{BotStuckRecovery.cs新規,BattleCrouch.cs新規,BotTraversalAdapter.cs,BattleSimulation.cs,SimulationSnapshots.cs,BattleEmergencyReturn.cs,SimulationConfig.cs,CombatInputAdapter.cs,BattlePortableNuclear.cs}、Runtime/Playable/{PlayableEntry.cs,PlayableConfiguration.cs,LocomotionWorldView.cs,PlayableUI.cs}、Tests/EditMode/StuckCrouchTests.cs新規・Tests/EditMode/Playable/PlayableFlowTests.cs。BattleBoost.csは未変更で予約解除済み。

UnitCommand末尾crouch=false、UnitSnapshotへCrouching/ReturnChanneling/CarryingNuclearを追加。新Configは任意フラグfalseで旧fixture互換、PlayablePrototype.Apply/実JSONは有効。所有者/照準/CT/有限弾の既存契約は維持。BotTraversalAdapter.StuckAvoidanceCount/StuckReturnCountは診断用で、要求数は帰投成功数ではない。

MechaBattle/scripts/DomainSmoke/ConstructedBattleSmoke.csはreturn完了を別計測し、reserve待機を毎tickの撃破/ゲージ損失として数える旧診断を修正。config/prototype-defaults.jsonとContent/Playable/PrototypeConfig.jsonの2コピー、新しい仮値/理由はstuck31Notes。UserText/正本/Git状態は編集しない。

## 実行検証

部分94/94成功（新26ケース）。壁に向かう実座標の側方回避、閉じた箱で2回回避→1回帰投→同機体/別本拠点、実補給を待つこと/禁止弾非補給/死亡・ゲージ維持、被弾中断後のトグル連打拒否、静止/進行/硬直/人間の誤判定拒否、しゃがみ通常/OH2倍・空中/解除/満タン・帰投中自動姿勢・核命令コピーを検査。MechaBattle/Build/STUCK31_1792203614108/quick/checks-loop-after-fix.log。初回の遅い弾の着弾待ち不足、補給速度の不正fixtureと重なり機体への射撃は、正規の単発爆風/到達待ちへ修正し、本体の制約は緩和していない。

実試合レビューで、都市slot77が(288.37,0,147.73)の本拠点接線で14m後退→同地点へ戻るループを確認。最初の全回帰は成功したが完了判定を保留し、回避方向と短周期再入の2件を追加。旧92成功/2失敗→94/94へ解消。隔離した14秒座標追跡では、修正後は(263.44,0,85.88)まで前進している（diagnostic/motion-trace-before-loop-fix.jsonとproject/motion-trace.json）。診断ハーネスは14秒で意図的に停止して戦闘完走assertを捕捉するため、全試合の成功とは扱わない。全回帰・20asm・実4マップはfinal2で再実行して成功。


最終結果: final2/project/Build/Reports/domain-smoke.jsonは568成功/0失敗（純fixture561 + 統合7）。109純C#ソース、static-api-compile.jsonは全20アセンブリcompiled。185入力の現行SHA差分0・実config2コピー一致・既存Sceneを除くdiff --check exit0。最終要約は `MechaBattle/Build/STUCK31_1792203614108/final2/final-checks.json`、実ログは同ディレクトリのdomain.log/compile.log/status、固定入力はproject/とsource-sha256.json。

| マップ | 模擬秒 | 最大機数 | 回避 | 帰投要求/完了 | 実レーダー | 本拠点命中 |
|---|---:|---:|---:|---:|---:|---:|
| canyon-cross | 600 | 100 | 188 | 2/2 | 4 | 17 |
| layered-city | 600 | 100 | 3171 | 4/4 | 4 | 168 |
| reclamation-yard | 600 | 100 | 43 | 6/1 | 6 | 555 |
| urban-overpass | 600 | 100 | 313 | 17/17 | 5 | 2 |

帰投要求は完了数ではない。再生工廠では6要求/1完了で、被弾等の通常中断を尊重する。二層都心は回避3171と多く、地形/経路の継続レビュー余地がある。全BOTの移動不能ゼロや人間の操作感・描画性能の合格は主張しない。実際の報告地点が未特定のため、同地点のUnity実機受入は残る。旧finalは566/566でもループ発見のため最終結果には採用せず、ループ修正後のfinal2だけを採用する。

## Claudeの実機受入 / 未実施

Play停止→Assets > Refresh→コンパイル完了→既存Playableで新しい試合。Scene再生成不要、新3csの.metaは存在を確認済みで、GPTは直接編集していない。

1. 左Ctrl保持で腰/両膝/胴体/視点が滑らかにしゃがみ、放す/移動/Space/Shift/格闘で戻ること。通常射撃/ロックは維持する。通常200・OH40毎秒の回復と満タン解除、空中回復なしを確認。
2. B帰投中はCtrlなしでしゃがみ、移動/攻撃/被弾/再Bで中断し姿勢を戻すこと。核運搬は帰投禁止。最新Nativeの既存携行核/帰投フローへ、モデル腰下げ・膝曲げの実検査を追加したが未実行。
3. 実際に報告された停滞地点で、移動要求があるBOTだけが側方へ抜けるか、通常帰投へ移ること。意図的防衛/射撃待ち/硬直を誤って帰投させず、被弾で中断し、帰投キーを連打しないこと。
4. 帰投BOTは消失したままにならず、実補給後に同機体/別の生存本拠点から戻ること。死亡数/ゲージ/戦略ポイント/CTと禁止弾に増減を作らないこと。人間の出撃先を自動選択しないこと。
5. Native Test Runner、Windows x64実行、人間のプレイ感、FPS/GC/CPU/GPUを計測。独立ドメイン試験は実Unity描画や性能の合格ではない。P0全体は未完了。

全実装ソース・文書4パスの予約は解除済み。GPT保持0、背景検証プロセスも全終了・不在確認済み（wrapper29408/29064、domain29416/30604、static35940/5316）。Claudeは解除範囲を追加GPT承認なしでレビュー/実機確認/改修可能。今回の受領/直接送信は未確認。
