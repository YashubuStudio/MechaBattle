# GPT → Claude：制作継続・担当移管（HANDOFF-13 / CONTINUITY-13）

更新: 2026-10-16T07:17:54.472Z / 作成: GPT / 実装先: D:/game/MechaBattle/MechaBattle。

## ユーザーによる継続権限と担当変更

ユーザー指定: 「Claudeに対して、処理や作業の引継ぎをお願いします。」「リソースが減って作業ができない状態になっても、Claudeが構築、実装に参加できるようにしてください。」

**GPTはこの引継ぎをもって、これまで保持していたコード/設定/テスト/Editor生成の担当を解除する。ClaudeはGPTの返答・追加承認を待たず、作業表へ着手を記録して構築・実装・修正・検証を継続できる。** 以前の「GPT引取中なのでClaudeは提案のみ」「GPT担当へ直接編集しない」は、この移管範囲では失効する。ユーザーの新しい仕様と共通AGENTSの保護規則は引き続き守る。

現在の通信状態: 共有フォルダーへの引継ぎ掲示済み。既存Claudeセッションへ直接送信できるAPIは未確認のため直接送信は未実施。今回の移管のClaude受領・着手は未確認であり、実施済みと捏造しない。Claudeは本書を読んだらWORK_BOARDに受領/現在の担当/着手する対象パスを追記する。これはGPT承認待ちではない。

GPTは今の引継ぎ中に実装コード/実config/Sceneを変更していない。GPTが起動した前回の検証コマンドは終了済みで、引き継ぐGPT起動の実行中検証はない。Claude自身やユーザーのEditor/ターミナルを停止しない。以後GPTが復帰する際も、Claudeの着手範囲を確認してから担当を分ける。

## 最初に読む資料

1. ルートAGENTS.md、MechaBattle_MasterPlan/AGENTS.md、README/GAME_VISION/GAME_SPEC/SYSTEM_DESIGNと正本config。
2. docs/DEVELOPMENT.md、docs/WORK_BOARD.md冒頭の最新担当、docs/COORDINATION.mdの継続ルール。
3. docs/reviews/GPT_SUPPORT12_HANDOFF.mdとGPT_SUPPORT12_VALIDATION.json（直近GPT実装の詳細）。
4. docs/CHARGE_MELEE_AND_ARM_PROPOSAL.md（Claudeからの最新ユーザー要求6点）、docs/MAP_VARIATIONS.md、docs/reviews/GPT_SQUAD12_REVIEW.md。
5. UserText/MechaBattle機体構想・カスタマイズシステム構想案.mdは参照のみ。人の資料を編集・作成しない。

## 引き取れる範囲

実装先はすべてMechaBattle/配下。

| 範囲 | 移管後の担当 | 注意 |
|---|---|---|
| Runtime/Contracts、Contracts/Construction、Construction | Claudeが引取可能 | 共通型は変更案・利用側・互換を先に記録。GPTの追加承認待ちは不要 |
| Runtime/Simulation、Battlefield、Playableと実config両コピー | Claudeが引取可能 | 最新仕様・デッキ保存互換・同一パスの同時編集禁止 |
| Editor/PlayableTools、ModelTools、Preparation、Generators | Claudeが引取可能 | Unity API生成、YAML直編集禁止、未保存Editorを保護 |
| Tests、scripts/DomainSmoke、検証スクリプト | Claudeが引取可能 | 他方の実行と出力先が競合しないよう区切る |
| Observation、AI/Bots、AI/Squads、UnityAdapters、モデル | 従来のClaude担当を継続 | AIへの敵真値公開/人間強制操縦なし |
| docs/、レビュー/継続チェックポイント | Claudeが更新可能 | 実行者・報告受領・独立検証・未実施を区別 |

これは実装担当の移管であり、UserText・正本・既存変更・Git index・未保存Sceneを上書きする許可ではない。Gitは未コミット変更が多い（引継ぎ確認時306パス）。まとめてreset/clean/stash/commitせず、他方の変更を保持する。

## 現在実装済みの基準

- Unity6000.3.26f1 / URP / Windows x64。100操縦枠（Playableは1人+99BOT）＋別枠指揮官2、各軍3本拠点。本拠点を主目的、人間は自由、BOTに弾薬/CT特例なし。
- 機体2フレーム/左右腕/最大3装備/4機デッキ、ホイール・1〜3、Spaceジャンプ/Shiftダッシュ。7テンプレート/18モデルモジュール。制作/参戦プレビュー分離、選択デッキを初回出撃へ適用。
- 射撃FA/SA/単発/チャージ/継続ビーム/BZ、格闘5方式、盾/DP/よろけ/転倒。手動赤取得時だけ一瞬吸着、その後自由照準と直進弾。敵設置物は機体より低優先度ロック、世代IDで置換後の古いロック/損害を無効化。
- 回復FAはHP45/発、物理65/黄60/赤25m。ポッド盾/弾薬優先、180秒/HP1800。レーダー900秒/HP900、有限弾・補給不可。敵共有HPは最終観測後1.7秒、レーダーは位置のみ。
- SUPPORT12: 視界内味方HP全体alpha0.5、現在機体に回復FA/ポッド所持で持替え後も味方ロック（予備デッキ不可）。味方ロックは敵警告/共有索敵/攻撃散布ゼロへ混入させない。
- 基礎格闘後隙: 通常0.10/連撃0.18/D格0.16/D連撃0.24/チャージ0.60秒。実ダメージ命中で段ごとに一度だけさらに×0.5、支払済み格闘ブースト50%返還。D格は発動に30必要→命中後15返還・後隙0.08秒。空振りは30/0.16秒。複数巻込みで重複せず、OHは返還で解除しない。現行威力は通常760/D格2280/旧チャージ2600。
- ブースト現行容量100、移動17.5/秒・通常回復100/秒・OH回復20/秒、0でOH・満タン解除。プレイヤー容量1.3倍は次の新要求であり未実装。
- HP現行: 標準2000/重量2650、補強両腕3200/3850、盾900/重量1100。Claudeがユーザーへ確認した全体引下げを統合。建屋反撃1400（標準70%）。コスト/CTは実評価値へ再計算済み。
- IP素材は使わず、システム参考は機動戦士ガンダムオンライン。上記の試作値は公式値の検証結果ではない。

## 次に進める作業

以下のownerはClaudeへ移管可能、実際のstatusは受領・着手時に本人が記録する。GPTの返答待ちを理由に提案段階へ戻さない。

| 優先 | タスク / 要件 | 依存・対象 | 完了条件 |
|---|---|---|---|
| 1 | 最新6点の実装：docs/CHARGE_MELEE_AND_ARM_PROPOSAL.mdのユーザー原文 | Contracts/Construction/Simulation/Playable/実config/試験 | プレイヤーブースト容量1.3倍、格闘で敵機/本拠点ロック、ダウン相手への通常格闘、チャージ格闘別枠・腕タイプ別挙動・左右共通化をゲーム操作へ接続。原文と提案を区別して仮値を記録 |
| 1 | SUPPORT12/ALLY12/MELEE12の実機受入・レビュー（FR-302/501/801/805） | GPT_SUPPORT12_HANDOFF、PlayableFlowTests | 味方バー/ロック/実命中半減/ブースト返還/OH維持を確認。D格2回で倒せない報告は実発動/命中/残量を切り分け |
| 2 | SQUAD12残件 | Runtime/AI/Squads、SquadRosterTests | 公開int[]への直接書換を防ぐ。入力元配列の防御コピー/overflow/重複拒否/コメントは回答受領・読取確認済み |
| 2 | 追加マップを実際に選択して遊べるよう接続（FR-108/110＋最新マップ要求） | forward-center-map.json、Playable/Editor生成/試験 | マップ選択/Assets側データ/描画・カメラ・スポーン・経路を接続し、中央主戦場・側面迂回・高低差/遮蔽を追加して検証。既存標準マップを維持 |
| 3 | TeamKnowledge/小隊の次段階 | docs/TEAMKNOWLEDGE_DESIGN、正本§10 | チーム観測の集計とBOTだけへの入力を段階実装。設計/編成データだけを実戦判断完成としない |
| 3 | Unity/Windows/P0性能受入 | Tests/Editor生成/BuildTools | 100枠・2指揮官枠・各軍3本拠点の実機試験、Windows起動、FPS/GCを測定して記録 |

最新6点についての注意:

- 提案書は実装ではない。具体的な500/1600DP/6.5m/3000等はClaudeの仮案であり、ユーザーが確定した数値と混同しない。合理的な調整は記録して進められる。
- 「ダウン中でも通常格闘は当たる」は、まず**ダウンしている対象への命中**として高さ/胴体/LOS/アニメーションとの整合を調べる。提案書の「攻撃者がスタガー中でも攻撃可能」は別仕様なので、原文から確認済みと扱ってDisabledUntilを外さない。曖昧さが残っても他の独立部分を進める。
- ロック選択と射撃誘導ゲートを分けて調べる。格闘ロック追加によって継続自動照準を復活させない。本拠点はコア/建屋/破壊済み判定と既存候補順位を整合する。
- プレイヤー容量倍率は出撃・回復上限・OH解除・命中返還上限・HUD比率まで有効容量へ統一する。人間/BOTの適用対象を明記し、BOTへの隠し補正にしない。
- 腕統一では有効な旧左右混成セーブを勝手に消さない。明示移行/構築エラー案内と保存保護を用意し、既存6912構成試験の前提変更を「試験削除で合格」にしない。

追加マップはhalfWidth720/halfDepth420、中央拠点x±300/側面±650の定義とドメインスモークが追加済み。現在obstacles=[]、選択UI未接続。定義があることだけで広いPlayableマップが完成したとはしない。MAP_VARIATIONS.mdの294件表記と共有297件報告の差も更新時に整合する。

## 検証済みと未実施の区別

| 証拠 | 実行・確認者 | 結果/場所 |
|---|---|---|
| SUPPORT12純C#＋構築済み100BOT | GPTが前ターンで実行 | 293/293、静的18アセンブリ。MechaBattle/Build/SUPPORT12_1792131300470/とGPT_SUPPORT12_VALIDATION.json |
| SUPPORT12構築済み100BOT | GPTが前ターンで実行 | 424.28sim秒決着、37撃破/137出撃/95コア命中、両軍最低47機。実画面/FPSではない |
| 小隊修正＋マップ追加後の最新回帰 | Claudeが実行と報告、GPTはJSON確認・写し保存のみ | 297/297、静的18アセンブリ。06:48 UTCのBuild/Reportsの写しはMechaBattle/Build/HANDOFF13_1792134857269/ |
| Unity Test Runner/最新Play/マップ描画/Windows起動/FPS・GC | 未実施 | 合格・完了と報告しない。モデル幾何は今回は再実行していない |

この引継ぎターンでは新しいコード試験を起動していない。旧293件の実行時からSquadRoster/試験/DomainSmoke/追加マップ等はClaudeが更新しているので、旧63ソース/120SHA snapshotを現行全ファイルの一致保証に流用しない。新しい変更後は新しい実行証跡を作る。

## 再開コマンドと環境

リポジトリルートD:/game/MechaBattleで、Unityを開いたまま独立検証可能:

~~~powershell
powershell -NoProfile -ExecutionPolicy Bypass -File ./MechaBattle/scripts/verify-domain.ps1
powershell -NoProfile -ExecutionPolicy Bypass -File ./MechaBattle/scripts/verify-compile.ps1
~~~

.NET9 SDK/NUnit解決済みDLLを使う。verify-domainは新MapVariationSmokeも実行するので、新マップJSONもコピー対象に含める。別エージェントの検証中はBuild/DomainSmoke/CompileCheck/Reportsを共有上書きせず、固有の隔離出力にする。

Unity取込はPlay停止→必要な未保存作業を保護→Assets > Refresh→コンパイル完了→既存PlayableでPlay。SUPPORT12だけならシーン再生成不要。新規4.cs.metaは前回監査で未生成、Editorに生成させる。Scene/Prefab/SOの作成・変更はEditor API。Unityバッチ試験/ビルド時は既存Editorを閉じてから実行し、同じプロジェクトの別Editorを勝手に起動しない。

GPTの最後のUnity接続は検出ファイルへのOSユーザーアクセス拒否。接続回復は未確認で、権限/グローバル設定変更で回避しない。通常execにも初期化エラーがありGPTはNodeツール経由でfs/child_processを使用したが、Claude自身の実行環境に合わせる。既存.gitは読取制限あり。Gitのglobal ignore読取警告は引継ぎ確認でも出たが、設定は変更していない。

## リソース不足でも止めない運用

恒久ルールはルートAGENTS.md・CLAUDE.md・docs/COORDINATION.mdにも記載する。本書を起点に、Claudeは未担当/解除済み範囲を自分の判断で引き取り、実装・修正・試験を続ける。GPT停止を新しい許可待ちにしない。片方が反応しないことを承認/テスト成功の証拠にはしない。

区切りごと/大きな変更前/リソース制限前にWORK_BOARDへ「owner・status・対象・最新保存差分・実行中コマンド/セッション・結果/ログ・未検証・次の具体的1手」を保存する。途中で停止しても再開者が読める状態を優先する。記録前に停止した場合は最新作業表と実差分/実行状態を確認し、実行中の同一ファイル操作を避けながら継続する。相手のプロセスを無断終了しない。

受領・着手の返信はWORK_BOARDへ追記する。Claudeを自動で起動・別アプリへ直接通知したという保証はなく、既に動いているClaudeが共有資料を読む経路を使う。

## RELEASE-14：毎区切りの必須解除（最新ユーザー指定）

停止直前まで待たず、変更や試験が一段落したら必ず共同作業上の編集予約を解除し、WORK_BOARDに内容を書く。未完了/レビュー待ちも同様。解除パス・保存した変更・検証/未実施・残件・次の具体的作業・実行中処理を明記し、lock=releasedを残す。再開時に最新差分を確認して必要範囲だけ再取得する。詳細/記録テンプレートはCOORDINATION.mdのRELEASE-14。

追記時点でClaudeから小隊IReadOnlyList化/298件・18アセンブリ成功の報告と、docs/FEEL_AND_BALANCE_REVIEW.mdの追加提案を作業表で受領。GPTは今回これらを再実行しておらず、レビュー文中の原因仮説/数値案を確定した実装結果として扱わない。これらもClaudeが引き取れる範囲で、GPTの実装予約は取得していない。以前の引取宣言を理由にGPT判断待ちへ戻す必要はない。
