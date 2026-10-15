# MechaBattle 開発入口

呼称は **GPT** と **Claude**。Unityプロジェクトは D:/game/MechaBattle/MechaBattle。
正本は ../MechaBattle_MasterPlan/。この文書はP0制作の開始手順。最新の実施結果はP0_REPORT.md。P0全体は未完了。

## 確認済みの環境
- ProjectVersion: 6000.3.26f1（Unity 6.3 LTS）。Editor実体: C:/Unity/6000.3.26f1/Editor/Unity.exe。
- URP 17.3.0、Input System 1.20.0、AI Navigation 2.0.15、Test Framework 1.6.0、uGUI 2.0.0、Pipeline 0.8.0-exp.1 は既存manifestの値。今回は変更していない。
- 対象は Windows x64。Android/Web/macOS/UWP等の追加モジュールを導入しない。
- ルートのGitリポジトリと.gitignoreは並行作業で作成済み。GPTは既存コミットやリモートを変更していない。

## いま用意したもの
- Runtime/Contracts: 100操縦枠・2指揮官の初期編成と共通 UnitCommand。
- Runtime/Battlefield: 本拠点と中継拠点を別型で定義し、各軍2本拠点未満、重複ID、不正陣営、座標/半径、単一経路宣言を拒否。
- config/standard-map.json: 各軍3本拠点と3中継拠点の仮データ。座標・半径の理由はconfig/prototype-defaults.json。
- Runtime/Simulation: 移動/射撃/撃破/再出撃/デッキCT/有限弾薬/ゲージ/占領/基地補給の平面モデル。
- Runtime/Observation, AI/Bots, UnityAdapters: Claude担当の自己観測・本拠点攻撃/補給BOT・シーン接続。
- Editor/Generators: 標準マップと100機を生成し、Runtime設定をシーンへ保存するコード。
- Tests/EditMode: 編成・マップ・ルール・実BOT連携を検証。独立C#では51件成功。
- Editor/Preparation: 定義検証と、現在の有効シーンを使うWindows開発ビルドの入口。

経路名が異なることは実際の到達性の証明ではない。NavMesh、攻撃Collider、出撃空間、遮蔽物はGeneratorとPlayModeで検証する。
共通UnitCommandで平面シミュレーションが動く。人間操作・3D物理/遮蔽・Boost/格闘・帰投チャネル・支援機・戦略核・金功績・敵機知覚・小隊/指揮官AIは未実装。指揮官は2枠の編成のみ。

## 開いているUnityで確認する
1. プロジェクトを前面にしてインポートとコンパイルを完了させる（必要なら Assets > Refresh）。新規 .meta はUnityに生成させる。
2. Consoleのコンパイルエラーがないことを確認する。
3. MechaBattle > Preparation > Validate Foundation を実行する。
4. Window > General > Test Runner のEditModeで Game.Tests.EditMode と Game.Tests.Generators を実行する。
5. MechaBattle > Generators > Generate P0 Battlefield Scene を実行。既定は人間枠なしの全100BOTベンチシーン。
6. シーン保存→再読込→Playで100機が動くことと、Consoleエラーがないことを確認する（まだ未実施）。
7. 必要なら MechaBattle > Preparation > Build Windows Foundation。対象は現在Build Settingsで有効なシーンであり、SampleSceneだけならテンプレートの動作確認にとどまる。

Generatorは既存SampleScene/Build Settingsを変更しない。未保存の作業を保護してから実行する。P0をビルドする場合は生成後、Build ProfilesでP0_Battlefieldを対象に選ぶ。既存build.ps1は有効シーンを使うため、SampleSceneだけのままならP0ビルドではない。

## コマンド実行
Unityを開いたまま利用できる独立C#検証（.NET 9 SDKとUnity解決済みNUnit DLLが必要、NuGet通信なし）:

~~~powershell
powershell -NoProfile -ExecutionPolicy Bypass -File ./MechaBattle/scripts/verify-domain.ps1
~~~

このランナーは同じC#回帰テスト48ケースと3統合チェックを直接実行するスモーク検証。Unity Test Runner実行とは区別し、結果は MechaBattle/Build/Reports/domain-smoke.json に出す。

静的APIコンパイル（Unityインポートとは別）も開いたまま実行できる:

~~~powershell
powershell -NoProfile -ExecutionPolicy Bypass -File ./MechaBattle/scripts/verify-compile.ps1
~~~

**バッチ実行時はこのプロジェクトのUnityを閉じる。** 実行中のEditorを別プロセスで開き直さない。

~~~powershell
powershell -NoProfile -ExecutionPolicy Bypass -File ./MechaBattle/scripts/test.ps1 -Mode EditMode
powershell -NoProfile -ExecutionPolicy Bypass -File ./MechaBattle/scripts/test.ps1 -Mode PlayMode
powershell -NoProfile -ExecutionPolicy Bypass -File ./MechaBattle/scripts/build.ps1
~~~

Editorが別の場所にある場合は -UnityPath 'C:/.../Editor/Unity.exe' を渡す。
専用PlayModeアセンブリは未追加。保存再読込のGenerator試験はEditMode側のUnityTestでPlay Modeへ入り、Awake復元と実時間Tick進行を確認するコードを用意済み（実行は未実施）。スクリプトは0件・未実行・失敗を合格扱いにしない。生成物は MechaBattle/Build/（既存.gitignoreの対象）へ出力する。
Windowsビルドはツールチェーンの確認用であり、P0・100機稼働・60fpsの達成を意味しない。

## GPT / Claude の協調手順
1. WORK_BOARD.mdを読み、担当・状態・変更対象を宣言する。
2. 共有契約の変更は提案を残し、利用者と調整する。
3. ファイルの担当を分け、他方の作業をコミット・削除・上書きしない。
4. 要件IDとテスト結果を記録し、未実行は未実行とする。
5. 統合後にUnity EditMode → PlayMode → Windows実行 → 性能測定へ進む。

分担はClaudeから受領済み: GPT=Contracts/ドメイン/統合、Claude=Generator/Adapter/BOT/実行計測。レビュー往復はdocs/reviews/に保存。

## このセッションの制約
Unity起動はユーザーが確認済み。GPTからのCLI接続はSTATUS_DESCRIPTOR_UNREADABLE（検出ファイルへのアクセス拒否）。Pipelineは導入済みで、パッケージ不足とは判定しない。
アクセス権やユーザーのグローバル設定を変更して回避しない。接続回復まではシーン操作をせずソース準備とオフライン検証にとどめる。
通常のexecツールにも初期化エラーがあり、ファイル操作とプロセス実行は利用可能なNodeツール経由で実施した。
GPTが別途起動した読み取り専用Claude CLIレビューは応答を得られず終了。ユーザーが開いているClaudeとは別であり、そのセッションは停止していない。今回は共有文書経由でClaudeの受領・APIレビュー・修正回答を確認済み。
