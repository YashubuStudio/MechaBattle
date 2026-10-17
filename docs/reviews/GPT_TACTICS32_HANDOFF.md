# TACTICS-32 — 小隊長カラー・両軍の戦術判断 / Claude引継ぎ（GPT、2026-10-17）

実装保存・関連83/83成功（新23ケース）。全回帰/4マップ100BOT戦は実行中、静的20アセンブリは成功。今回のClaudeへの直接送信/受領は未確認。FR-005/107/110/201/306/404/601/701/702/705/802/805と最新ユーザー依頼へ対応する。仕様正本/UserText/Git状態/Scene YAML/metaは編集していない。

## カラー資格

- 既存SquadRoster.BuildStandardSquadsを利用し、既定は各軍2小隊×4機。各小隊MemberSlots[0]（全体slot0/4/50/54）だけ特殊塗装を許可。別枠の指揮官2枠とは区別する。小隊長不在時に全員へ特殊色を解禁しない。
- PlayableWorldViewのモデル生成/武器変更/再出撃でCoordinator.IsLeaderをMechaMeshLibrary.CreateのallowSpecialPaintへ渡す。一般機は通常陣営装甲、肩/膝/頭頂の識別素材は従来どおり。性能・構築Snapshot/保存色は変えず、格納庫プレビューは従来の選択色。現在の人間操縦枠は各軍先頭なので小隊長資格がある。

## 戦術の判断範囲

- BotSquadCoordinatorは自己・味方50枠の状態・公開された中継所有/本拠点だけを読む。小隊の共通中継目標を約8秒保持、占領で解放。味方密度/他グループの割当が多い中継へ集中しない。小隊員は36m以上離れると生存・非帰投・HP60%以上の先頭メンバーへ合流、健康な仲間が近傍で遅れている場合の待機は最大2秒。死亡/帰投/低HPで遅れた機体を無期限に待たない。
- 共有索敵の利用率は既存BotPopulation.ReadsMapのまま。一般の非地図読みBOTも小隊所属なら公開拠点だけは共有するが、未知の敵報告を読まない。RadarSupport/Interceptorの個体役割を小隊目標で上書きしない。既存の独立裏取り4枠/軍（陣営内9/18/36/45）は公開中継目標の割当から除外し、通常進軍を維持する。
- BotBattlefieldPlannerは有効なTeamContactへ既存mapReactionSecondsの遅れを経て判断。同じ24m/高さ差6mの報告群は迎撃役を原則2機までにし、他は各生存本拠点を分担。敵の真の位置や無条件レーダー範囲は追加しない。報告の失効/UnitIdを尊重し、地図目標から射撃/ロックは生成しない。
- 少数レーダー役は既存有限弾を使い、最小間隔とカバー域を守って最大3個まで展開。RadarPerOwnerLimitの値は3のままpublic constで再利用する。既設レーダーを保つ旧条件が追加設置先への移動まで毎tick消す干渉を修正し、期限/破壊/所有者/補給禁止は維持する。
- BotRecoveryの通常補給先は破壊本拠点を除き、高さの差も評価する。交戦/回復・補給/核運搬制約/敵コア攻撃の優先/スタック復旧は既存Adapterへ残す。人間・RemoteHumanへコマンドを出さない。

## 変更パス・依存

Assets/Game/Runtime/Contracts/BotBehaviorRules.csに任意smartObjectivesEnabled/squadTacticsEnabled、squadsPerTeam=2/squadSize=4、maximumRespondersPerContact=2、objectiveCommitSeconds=8/squadRallyDistance=36を追加。旧fixtureは新フラグfalse、実Playableはtrue。CopyはMemberwiseCloneで新フィールドも含む。理由/仮値は実config2コピーのtactics32Notes。

Runtime/Simulation/{BotSquadCoordinator.cs新規,BotBattlefieldPlanner.cs,BotRecovery.cs,BattleDeployables.cs,Game.Simulation.asmdef}。Game.Simulationから純C#Game.AI.Squadsへ参照を追加し、ClaudeのSquadRosterは変更しない。Runtime/Playable/{PlayableEntry.cs,PlayableWorldView.cs,MechaMeshLibrary.cs}。Tests/EditMode/BotTacticsTests.cs新規、Tests/EditMode/Playable/PlayableFlowTests.cs、scripts/DomainSmoke/ConstructedBattleSmoke.cs。全般仕様の正本を書き換えずdocs/PLAYABLE_GUIDE.md・COMBAT_RULES.md・DEVELOPMENT.mdを更新する。

## 検証

関連83/83成功（BotTactics23 + 既存BotBehavior/BotPerception/StuckCrouch60）。両軍の小隊長のみ資格・人間自由/非地図読みの境界・共通目標/分散/保持/占領後解放・有限の集合待ち・両軍の迎撃上限/遅延・実3レーダー/3弾消費を検査した。

部分初回73成功/4失敗→76/1→77/77。新しい集合待ちの自位置を中継だと誤るfixtureは物理的に集合してから検査し、人間slot0を使った設置fixtureは真のBOT構成へ修正。追加設置移動を毎tick消す本体の干渉も修正した。製品の視認/反応/弾/CT条件は緩和せず、失敗ログも保持する。MechaBattle/Build/TACTICS32_1792210097260/quick/checks-first.log・checks-second.log・checks-third.log。

最終検証はMechaBattle/Build/TACTICS32_1792210097260/final2/（187入力の固定projectとsource-sha256.json、domain.log・compile.log・status、project/Build/Reports）へ保存。全回帰/試合結果は完了後に追記する。

統合レビューで裏取り役を中継へ呼び戻す可能性と帰投/重傷の先頭メンバーへの追従を検出し、追加6件を旧Coordinatorで77成功/6失敗として再現→修正後83/83へ。quick/checks-review-before-fix.log・checks-review.log。先行finalは584成功/1失敗（都市100BOTで本拠点命中0）であり、全試合成功とは扱わない。統合検査の条件は維持し、裏取り保護後のfinal2で再実行中。構築試合レポートへObjectiveOrders/RegroupOrdersの診断を追加し、回避/帰投要求/帰投完了は別カウンターを維持する。

## Claude実機受入・未実施

Play停止→Assets > Refresh→コンパイル完了→新しい試合。Scene再生成不要。新規2csの.metaはEditorに任せ、GPTは直接編集しない。

1. 両軍の小隊長だけが特殊塗装を使い、一般機は通常陣営装甲であること。武器切替・撃破/再出撃でも制限を維持すること。肩/膝/頭頂で両軍を識別できること。
2. 小隊長の保存塗装は格納庫/次回起動にも残り、色でHP/速度/装備は変わらないこと。Nativeの既存拡張選択/全マップフローへ素材/保存維持/全一般機の検査を追加したが、実行は未実施。
3. 複数小隊が中継を共有して移動し、2秒以上の無期限集合待ちや目標の毎フレーム切替がないこと。人間は自由に動けること。撃ち合い・負傷・補給・スタック復旧が優先されること。
4. 確認済みのレーダー侵入者に迎撃が集中しすぎず、見つかっていないネズミは従来どおり隠れられること。レーダー役が有限弾/間隔/3個上限で未カバーを広げること。
5. Native Test Runner/Windows x64実行/FPS・GC・CPU/GPUと人間のプレイ感。全作戦プリセット/指揮官AIの戦略判断/核の自動運搬は未実装であり、P0/P3全体完了とはしない。

ソース予約は区切りで解除済み。文書4パスのみ最終結果仕上げの保持中。最終区切りで全保持を解除し、背景プロセスの終了/未解決/次の作業をWORK_BOARDへ記録する。Claudeへの今回の直接受領は未確認。
