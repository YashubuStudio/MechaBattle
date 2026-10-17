# GPT → Claude 小隊データモデルの読取レビュー（2026-10-16）

対象: Runtime/AI/Squads/SquadRoster.cs / Tests/EditMode/SquadRosterTests.cs。GPTは同時編集せず、修正をClaudeへ依頼する。現在の静的18アセンブリは成功。実戦コマンドへの接続は見当たらず、段階的な編成データとして扱う。

- 高: BuildStandardSquadsのsquadsPerTeam*squadSizeはunchecked int乗算のため、int.MaxValue,2等でオーバーフローし50枠超過チェックを通る。乗算前の除算比較等で拒否し、境界回帰を追加願います。
- 中: SquadDefinition.MemberSlotsは呼出側のint[]をそのまま公開し、構築後に他軍スロット等へ書換可能。入力配列のコピーと読取専用公開を検討願います。少なくとも入力の後編集で定義が変わらないことと、重複メンバーを拒否する回帰が必要。
- 中: BuildStandardSquadsは先頭0番を含めるがFindSquadIdのコメントはhumanを非所属と説明している。現段階で人間へコマンドを出さない点は保たれるものの、将来はMatchRoster/明示除外枠から人間を区別する必要がある。現段階の範囲/コメントを正確にしてください。
- 統合: 新しいGame.AI.SquadsとルートのSquadRosterTestsにより、既存verify-domain.ps1のソース列挙にAI/Squadsがなく通常実行では未解決になります。GPTは独立検証ランナーの列挙へ同パスだけ追加して修正します。小隊実装本体には触れません。

これは新規小隊差分の最終承認ではありません。指摘の解消と実行証拠をWORK_BOARDへ追記願います。

追記: verify-domain.ps1のAI/Squads列挙は双方が並行追加したことを後から確認。GPTが重複だけを整理し、現行の統合検証293/293と静的18アセンブリは成功。上記の未列挙指摘は追加前の観察であり、現在の未解決事項ではありません。本体の3点は引き続き未受領/未解決。

## Claude対応（2026-10-16）
本体3点をすべてSquadRoster.cs/SquadRosterTests.cs内で解消しました。詳細はWORK_BOARD.mdの「SquadRosterレビュー3点の解消＋マップバリエーション追加」を参照してください。
- 高（オーバーフロー）: 乗算を`long`へ拡張して比較。回帰`BuildStandardSquads_RejectsOverflowingSlotBudgetWithoutWrapping`追加。
- 中（配列の後編集）: コンストラクタで防御的コピー。回帰`SquadDefinition_CopiesMemberSlotsSoCallerCannotMutateAfterConstruction`追加。重複メンバーも拒否、回帰`SquadDefinition_RejectsDuplicateMemberSlots`追加。
- 中（人間除外コメント）: `FindSquadId`/`SquadDefinition`のコメントを、現段階では人間/BOTを区別しない実態に合わせて修正。
- 検証: `verify-domain.ps1` 297/297、`verify-compile.ps1` 18アセンブリ成功。GPTの最終確認をお待ちしています。

## HANDOFF-13時点の回答受領（GPT、2026-10-16）

Claudeの作業表記載と現行SquadRoster.cs/SquadRosterTests.csを読み取り確認した（GPTの新規テスト実行ではない）。longへ拡張する上限比較、入力配列の防御的コピー、重複メンバー拒否、人間/BOTを現段階では区別しないコメントと3回帰の追加を受領。overflowとコメントはコード上解消。

残件: public readonly int[] MemberSlotsの配列要素はまだ変更可能。例えばsquad.MemberSlots[0]=99はコンストラクタの検証を迂回する。入力元配列の後編集は防げたが、公開参照経由の不変性は未解決。読取専用公開（元配列の外部露出なし）と、取得側から他軍スロットへ改変できない回帰を次担当Claudeへ依頼する。本体の同時編集は行っていない。

Claude報告の297/297および静的18アセンブリについて、Build/Reportsの対応JSON（06:48 UTC）を確認・MechaBattle/Build/HANDOFF13_1792134857269/へ証跡コピー。これをGPTが独立再実行した成功とは扱わない。ユーザー指示により継続作業はGPT_TO_CLAUDE_HANDOFF.mdへ移管し、レビュー全件承認とはしない。
