# GPT → Claude / SUPPORT-12・ALLY-12・MELEE-12 最終引渡し

owner: GPT / status: ソース実装・独立検証完了。最新Unity受入・Claude最終差分レビュー待ち。日付: 2026-10-16。要件: FR-302/501/801/805、最新ユーザー指定の味方HP半透明・回復装備所持での味方ロック・格闘後隙短縮・命中時さらに50%短縮とブースト軽減。Claude経由のユーザー了承済みHP全体調整も統合。正本の仕様書は変更していない。

## 実装と完了条件

- BattleFriendlyLocks: 現在出撃中の機体の最大3装備から回復FA/ポッドを判定。持替え後も味方ロック可能、予備デッキは不可。自己/死亡/別軍/壁/範囲外は不可。所持FAなら黄60m、ポッドのみ8m。回復FA選択中は味方のみ、他装備では適格な味方と敵を選択設定で比較し、設置物より機体を優先する。赤は回復FA選択時のみ25m。赤で手動取得した瞬間だけ吸着し、以後の手動照準と外したら回復しない挙動を維持。
- 既存IsYellowLocked/IsRedLockedは敵専用のまま。PlayableのIsManualUnitYellow/RedLockedだけ拡張。味方ロックは敵の被ロック数/共有索敵/攻撃誘導へ混入しない。攻撃武器の散布も味方ロックでは消さない。
- BattleHealthDisplay.GetUnitHealthBar: 味方は視界内でロック不要の現在HP、敵は既存チーム接触の期限付き観測HPのみ。自己/死亡/壁越しは隠す。敵の解除後1.7秒保持を維持。描画読取をAI/Observationへ渡さない。
- BattleFeedbackView: 味方バー/文字全体をCanvasGroup.alpha=0.5、緑、味方ロックの印。非操作Graphic/CanvasGroupはRaycastを妨げない。ロック菱形/状態/距離/HPも味方色へ。既存Canvas/TMPを保持。
- 格闘基礎後隙: 通常0.10・連撃0.18・D格0.16・D連撃0.24・チャージ0.60秒。威力760/400×3/2280/850×2/2600、DP、予備動作、追加CTは据え置き。通常武器間隔は0.31秒へ整合。
- 実HP/盾損害または有効本拠点損害の確定後だけConfirmMeleeHit。1段1回、後隙×0.5/支払済み格闘ブースト50%返還。通常命中0.05指定は50Hzで0.06秒、D格命中0.08秒。複数巻込みでも重複せず、連撃各段は新たに適用。味方/壁/ミス/同tickの古い設置物置換は対象外。他の射撃が格闘中に命中しても付与しない。
- ブーストは発動時に全額必要。D格30→命中後15返還、空振りは30。返還でOHラッチ/行動阻害は解除しない。中断済み格闘を再開しない。MeleeUntil/NextMelee/表示Phaseを実短縮へ整合。最新50%指定を優先し、命中時D格0.08秒は従来0.1秒下限の例外、空振り0.16秒は下限を守る。
- SimulationConfig/PlayableConfigurationへ任意meleeHitRecoveryMultiplier/refundFraction。0既定は旧補正なし、有限範囲検証あり。共有Contractsの構造変更なし。実JSON両方は0.5/0.5。仮値の理由をsupportLock12Notes/melee12Notes/hp12Notesへ記録。
- Claude共有のユーザー了承を受領し標準HP2400→2000、重量3200→2650。補強腕+600は維持し標準両腕3200/重量両腕3850。人間/BOT共通、盾/DP/射撃威力は不変。建屋反撃は標準70%を保つ1680→1400。コスト/CTは通常格闘周期とHPから再計算（下記レポート）。

## 対象パス

- Runtime/Simulation: BattleFriendlyLocks.cs / BattleHealthDisplay.cs（新規）、BattleBoost.cs / BattleCombat.cs / BattleFeedback.cs / BattleObjectTargets.cs / BattleSimulation.cs / SimulationConfig.cs / PlayableConfiguration.cs。
- Runtime/Playable: PlayableTargetLock.cs / PlayableEntry.cs / PlayableUI.cs / BattleFeedbackView.cs。
- config/prototype-defaults.json / Assets/Game/Content/Playable/PrototypeConfig.json。
- Tests/EditMode: FriendlySupportTests.cs / ConfirmedMeleeTests.cs（新規）、Playable/PlayableFlowTests.cs、scripts/DomainSmoke/ConstructionDefaultsSmoke.cs。
- scripts/verify-domain.ps1はClaude新規AI.Squadsの列挙を統合（双方並行追加の重複を1本へ整理）。Squad本体は編集せず別レビューへ。
- docs: PLAYABLE_GUIDE / COMBAT_RULES / UNIT_CONSTRUCTION / PLAYABLE_DESIGN / DEVELOPMENT / WORK_BOARD、本引渡し・VALIDATIONとGPT_SQUAD12_REVIEW。

## 実行した検証

- 隔離先: MechaBattle/Build/SUPPORT12_1792131300470/。実63純C#ソースを.NET9/Roslyn optimize+、Unity解決済みNUnit DLLでコンパイル。新規対象27/27（味方支援10、命中補正17）成功。実設定/マップを隔離configへコピーした全回帰293/293成功（旧260+新規27+Claude新規小隊6）、exit0。Unity Test Runnerとは別。
- 静的Unity APIコンパイル: 新規Game.AI.Squadsを含む18アセンブリ成功、exit0。既存JSON注入用CS0649警告のみ。画面試験追加後のソースを含む。
- 実カタログ7テンプレート、標準48単装備/6912三装備の評価/保存復元。最低2000・補強3000以上・通常760/D格3倍・基礎硬直・新規0.5値・回復65/60/25を検査。
- 実設定・構築済み100BOT試合: 424.28sim秒で決着、37撃破/137出撃/95コア命中/35反撃/17占領変化、両軍最低47機、平均99.616機、中央戦場割合0.7644。ゲージ損害は本拠点43700/撃破500.13。これは独立ドメイン試合であり人間の操作性/FPS/実画面の証拠ではない。
- 命中/空振り/壁/味方/多重命中/連撃各段/OH維持/旧設定互換/射撃の誤帰属/有効本拠点/設置物置換の回帰成功。味方ロックの持替え/予備デッキ/範囲/遮蔽/死亡再出撃/敵警告非混入/散布非悪用、敵HP1.7秒も成功。
- PlayableFlowTestsへ実装備→出撃→回復FA選択→味方手動ロック→半透明HP/文字/非Raycast→格納庫復帰を追加。静的コンパイルのみ、Unity実行は未実施。
- 63ドメインソースと全静的コンパイル対象の更新時刻を監査し、各コンパイル後の更新0。実config両コピー/試験config/試験マップ一致。SHA256 manifestとdiff整形結果はVALIDATIONへ記録。

## 相互レビューと未実施

UI11のClaude独立260件/17アセンブリ/最終承認を受領。HP全体調整・D格不発仮説と新規小隊6件も作業表で受領。D格不発の実機原因は未確定で、ブースト不足または空振りを断定していない。今回の命中返還は開始時不足を免除しない。

Claudeの新規SquadRosterを読み、乗算overflow・可変配列公開・人間除外コメントをGPT_SQUAD12_REVIEW.mdへ返した。現時点で修正回答/再受領は未確認。AI.Squadsは実戦入力に未接続で本タスクの味方表示/格闘とは独立するが、最終承認せず未解決として管理する。今回SUPPORT12のClaude最終レビューも未受領。実際の応答だけを追記する。

Unity CLIは既知の検出ファイルへのOSユーザーアクセス拒否で接続できず、別Editor起動/権限回避/未保存状態変更は行っていない。最新Unity Test Runner/味方ゲージ・菱形/モーション/Play/Windowsビルド起動/FPS・GCは未実施。P0全体・全MasterPlan完成とは報告しない。モデルは今回変更していないため過去の幾何検証を新規実行扱いにしない。UserText/正本/Scene YAML/Git indexを変更していない。

取込: Play停止→Assets > Refresh→コンパイル完了→既存PlayableでPlay。今回の実設定/コードは直接参照するためシーン再生成不要。味方HP、回復FA装備時の右クリック/Tab、D格命中と空振りの硬直/残ブースト、0からOHが維持されることをGameタブで確認する。
