# MechaBattle — GPT / Claude 共通指示

このワークスペースでは、このエージェントを **GPT**、Anthropic のエージェントを **Claude** と呼ぶ。

## 最初に読む資料
- MechaBattle_MasterPlan/AGENTS.md（全ルールを、このルートと MechaBattle/ の実装にも適用）
- MechaBattle_MasterPlan/README.md、GAME_VISION.md、GAME_SPEC.md、SYSTEM_DESIGN.md
- MechaBattle_MasterPlan/config/prototype-defaults.json
- docs/DEVELOPMENT.md と docs/WORK_BOARD.md

仕様の正本は MechaBattle_MasterPlan/。実装先は MechaBattle/。正本を実装済みの説明へ書き換えない。
Unity 6000.3.26f1 / URP / Windows x64 を維持し、既存ファイルや未保存の Editor 状態を保護する。

## 共同作業
- 作業開始時に docs/WORK_BOARD.md の担当・対象ファイル・依存関係を確認し、着手を記録する。
- 各タスクは owner、status、対象パス、要件ID、完了条件、実行した検証を持つ。
- 同じファイルを同時編集しない。共有 Contracts の変更は先に提案し、利用側を確認する。
- GPT は Contracts/ドメイン・仕様追跡・統合を担当。Claude は Editor 生成・Unity Adapter・実行検証を担当する初期案とする。明示された担当変更を優先する。
- Claude と通信できなければ、未送信・未実施と記録し、Claude の同意や作業結果を捏造しない。
- Scene / Prefab / ScriptableObject は Unity Editor の API で生成する。YAML を直接編集しない。
- P0 完了には100枠・2指揮官・各軍3本拠点・テスト・Windows実行・性能測定が必要。準備用コードだけで完了扱いにしない。
- TBD の仮値は MechaBattle/config/prototype-defaults.json に理由とともに記録する。必須制約を緩和しない。
- 未実行の Unity テスト、Windows ビルド、FPS/GC 計測を合格・達成と報告しない。

## 相互レビューの継続指示（ユーザー指定）
制作の各区切りで相手の作業表・差分・reviews/を確認し、担当を守ってレビューと修正を往復する。実際の受領・検証結果だけを記録する。作業中であるという理由だけでレビュー指摘を無視せず、統合前に解消か未解決を明示する。

## 人が管理する資料
- UserText/ は人が書き込むフォルダー。GPT / Claude は参照のみとし、作成・編集・移動・削除しない。
- UserText/MechaBattle機体構想・カスタマイズシステム構想案.md を機体構築の追加方針として適用する。固定キャラクターの量産ではなく、共通フレームと交換パーツを段階実装する。
