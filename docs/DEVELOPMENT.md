# MechaBattle 開発入口

呼称は **GPT** と **Claude**。Unityプロジェクトは D:/game/MechaBattle/MechaBattle。
正本は ../MechaBattle_MasterPlan/。この文書は実装準備の記録でありP0完了報告ではない。

## 確認済みの環境
- ProjectVersion: 6000.3.26f1（Unity 6.3 LTS）。Editor実体: C:/Unity/6000.3.26f1/Editor/Unity.exe。
- URP 17.3.0、Input System 1.20.0、AI Navigation 2.0.15、Test Framework 1.6.0、uGUI 2.0.0、Pipeline 0.8.0-exp.1 は既存manifestの値。今回は変更していない。
- 対象は Windows x64。Android/Web/macOS/UWP等の追加モジュールを導入しない。
- ルートのGitリポジトリと.gitignoreは並行作業で作成済み。GPTは既存コミットやリモートを変更していない。

## いま用意したもの
- Runtime/Contracts: 100操縦枠・2指揮官の初期編成と共通 UnitCommand。
- Runtime/Battlefield: 本拠点と中継拠点を別型で定義し、各軍2本拠点未満、重複ID、不正陣営、座標/半径、単一経路宣言を拒否。
- config/standard-map.json: 各軍3本拠点と3中継拠点の仮データ。座標・半径の理由はconfig/prototype-defaults.json。
- Tests/EditMode: 編成・マップ定義の回帰テスト。これは100機戦闘テストではない。
- Editor/Preparation: 定義検証と、現在の有効シーンを使うWindows開発ビルドの入口。

経路名が異なることは実際の到達性の証明ではない。NavMesh、攻撃Collider、出撃空間、遮蔽物はGeneratorとPlayModeで検証する。
UnitCommandは入力契約のみ。移動・射撃・撃破・デッキ/CT・補給・赤ロック・知覚・指揮官AIは未実装。

## 開いているUnityで確認する
1. プロジェクトを前面にしてインポートとコンパイルを完了させる（必要なら Assets > Refresh）。新規 .meta はUnityに生成させる。
2. Consoleのコンパイルエラーがないことを確認する。
3. MechaBattle > Preparation > Validate Foundation を実行する。
4. Window > General > Test Runner のEditModeで Game.Tests.EditMode を実行する。
5. 必要なら MechaBattle > Preparation > Build Windows Foundation。対象は現在Build Settingsで有効なシーンであり、SampleSceneだけならテンプレートの動作確認にとどまる。

未保存シーンを自動保存・上書きしない。P0シーンの作成はClaude担当案のGeneratorから始める。

## コマンド実行
Unityを開いたまま利用できる独立C#検証（.NET 9 SDKとUnity解決済みNUnit DLLが必要、NuGet通信なし）:

~~~powershell
powershell -NoProfile -ExecutionPolicy Bypass -File ./MechaBattle/scripts/verify-domain.ps1
~~~

このランナーは同じC#回帰テストを直接呼び出すスモーク検証。Unity Test Runner実行とは区別し、結果は MechaBattle/Build/Reports/domain-smoke.json に出す。

**バッチ実行時はこのプロジェクトのUnityを閉じる。** 実行中のEditorを別プロセスで開き直さない。

~~~powershell
powershell -NoProfile -ExecutionPolicy Bypass -File ./MechaBattle/scripts/test.ps1 -Mode EditMode
powershell -NoProfile -ExecutionPolicy Bypass -File ./MechaBattle/scripts/test.ps1 -Mode PlayMode
powershell -NoProfile -ExecutionPolicy Bypass -File ./MechaBattle/scripts/build.ps1
~~~

Editorが別の場所にある場合は -UnityPath 'C:/.../Editor/Unity.exe' を渡す。
PlayModeテストはまだない。スクリプトは0件・未実行・失敗を合格扱いにしない。生成物は MechaBattle/Build/（既存.gitignoreの対象）へ出力する。
Windowsビルドはツールチェーンの確認用であり、P0・100機稼働・60fpsの達成を意味しない。

## GPT / Claude の協調手順
1. WORK_BOARD.mdを読み、担当・状態・変更対象を宣言する。
2. 共有契約の変更は提案を残し、利用者と調整する。
3. ファイルの担当を分け、他方の作業をコミット・削除・上書きしない。
4. 要件IDとテスト結果を記録し、未実行は未実行とする。
5. 統合後にUnity EditMode → PlayMode → Windows実行 → 性能測定へ進む。

初期担当案はGPT=Contracts/ドメイン/統合、Claude=Generator/Adapter/実行計測。受領前の担当案をClaudeの合意済みとして扱わない。

## このセッションの制約
Unity起動はユーザーが確認済み。GPTからのCLI接続はSTATUS_DESCRIPTOR_UNREADABLE（検出ファイルへのアクセス拒否）。Pipelineは導入済みで、パッケージ不足とは判定しない。
アクセス権やユーザーのグローバル設定を変更して回避しない。接続回復まではシーン操作をせずソース準備とオフライン検証にとどめる。
通常のexecツールにも初期化エラーがあり、ファイル操作とプロセス実行は利用可能なNodeツール経由で実施した。
GPTが別途起動した読み取り専用Claude CLIレビューは応答を得られず終了。ユーザーが開いているClaudeとは別であり、そのセッションは停止していない。Claudeからのレビュー受領は未確認。
