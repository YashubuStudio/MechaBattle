# Claude の作業入口

あなたは **Claude**、共同作業相手は **GPT** です。
まずルート AGENTS.md と、そこから参照する MasterPlan 全資料、docs/DEVELOPMENT.md、docs/WORK_BOARD.md を読んでください。
Unity プロジェクトは MechaBattle/ です。タスクを着手済みにしてから担当パスだけを編集し、変更・テスト結果・未確認事項を作業表へ返してください。
GPT が編集中の Contracts / Battlefield / Tests/EditMode はレビューを優先し、無断で並行変更しないでください。

## 最新：ユーザー指定によるGPTからの担当移管（2026-10-16）
最初に docs/reviews/GPT_TO_CLAUDE_HANDOFF.md と WORK_BOARD.md 冒頭を読んでください。GPTは従来のContracts/ドメイン/Playable/実config/試験等の担当保持を解除しています。Claudeは構築・実装・修正・試験を引き取り、GPTの応答や追加承認を待たずに進めてください。上記の「GPTが編集中」の制限は、この解除済み範囲には適用されません。最新ユーザー要求6点（docs/CHARGE_MELEE_AND_ARM_PROPOSAL.md）もGPT判断待ちにせず引き取れます。
着手前に WORK_BOARD へ受領・対象パス・依存・完了条件を記録し、実行中の同一ファイル操作を確認してください。各区切りとリソース制限前に継続チェックポイントを残してください。GPTの停止は制作停止の条件ではありません。既存変更・UserText・未保存Editor・正本を保護し、未実行のUnity検証を合格と報告しないでください。

## 各区切りで必ず編集予約を解除する（RELEASE-14）
ユーザー指定: 一段落したら、停止直前まで待たずに担当による編集予約を解除し、WORK_BOARDへ内容を書いてください。実装完了・試験の区切り・レビュー/ユーザー確認待ち・タスク切替・ターン終了前に、lock=releasedと対象パス、変更、試験/未実施、残件、次の1手、実行中処理の有無を記録してください。未完了・承認待ちを理由に予約を保持しないでください。書込み中の処理は完了を確認し、再開時は最新状態を確認して最小範囲だけ再取得してください。詳細はAGENTS.mdのRELEASE-14とdocs/COORDINATION.md。
