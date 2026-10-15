# GPT 準備作業報告 — 2026-10-15

## 対応要件と変更
- FR-001/002/003: Assets/Game/Runtime/Contracts/BattleContracts.cs に編成と入力契約。
- FR-108/109: Runtime/Battlefield/MapDefinition.cs、config/standard-map.json に各軍下限2・標準3の本拠点定義/検証。
- FR-110: 別経路の宣言を確認。実空間の到達性は未検証。
- FR-006: Editor/Preparation/PreparationCommands.cs、scripts/build.ps1 にWindows用の入口。
- テスト: Tests/EditMode/FoundationTests.cs と各asmdef、scripts/test.ps1、scripts/verify-domain.ps1、scripts/DomainSmoke/Program.cs。
- 共同作業: AGENTS.md、CLAUDE.md、MechaBattle/AGENTS.md、docs/WORK_BOARD.md、DEVELOPMENT.md、REQUIREMENTS_AND_TESTS.md。

## 仮設定
config/prototype-defaults.jsonはMasterPlanの仮設定を引き継ぎ、preparation欄に標準マップの仮座標/半径と理由を記録。
標準JSONは各軍3本拠点・3中継拠点。境界半幅240m/半奥行140m、拠点X=±200m、3レーンZ=-80/0/80m、出撃半径12m、攻撃半径8m、中継半径18m。
これらはデータ定義検証用であり、製品の地形/バランス/攻撃範囲の正式仕様ではない。

## 実行した検証
- 独立C#スモーク: **23件成功、0件失敗**。実際の本体C#とNUnitアサーションをRoslynでコンパイルし直接実行。Unity Test Runnerとは異なる。
- 詳細結果: MechaBattle/Build/Reports/domain-smoke.json。
- Contracts/Battlefield/Editor.Preparationの3アセンブリ: ローカルUnity 6000.3.26f1のモジュールDLLを参照した静的コンパイル成功。Unityのインポート/IL後処理とは異なる。
- PowerShell: common/test/build/verify-domainの構文解析成功。
- JSON/asmdefとGit差分は最終確認対象。

## 未実施
Unity内コンパイル・EditMode Test Runner・PlayMode・Windowsビルド・実機ゲーム実行・Bench A〜E・FPS/GC/CPU/GPU計測は未実施。
この時点でP0未完了。100機のGameObjectや戦闘シミュレーションを作成したとは報告しない。

## 制約と次作業
Editorはユーザー側で起動済み。CLIは検出ファイルへのアクセス拒否で接続不能。権限変更や二重Editor起動はしていない。
Unityを前面にしてインポート後、PreparationメニューとEditModeテストで確認する。新規 .meta はUnity生成待ち。
Claudeが並行してGit初期化等を実施中。GPTは既存変更・リモート・パッケージ・SampleSceneを変更せず、全体コミットもしない。
Claudeへの独立CLIレビュー依頼は無応答で終了し、レビュー受領未確認。別途作業中のClaudeとは共有作業表で担当を調整する。
次はGPT=Simulation/Match、Claude=Editor Generator/Adapter/PlayMode/計測を提案。受領・契約確認後に着手する。
