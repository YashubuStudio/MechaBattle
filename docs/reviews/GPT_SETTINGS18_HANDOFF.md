# SETTINGS-18 設定ボタン無反応の修正（GPT、2026-10-16）

ユーザーの実機報告「コンパイル後も設定画面へ移動できない／押しても無反応」を受け、**実Editorログの例外を確認して原因を修正**した。静的20asm合格。修正後のUnityクリック・Native試験は未実施で、実機受入完了とは扱わない。

## 原因の確証

C:/Users/吉嶺圭人/AppData/Local/Unity/Editor/Editor.logの11:55:21UTCまでの記録でMissingComponentException（MainMenuにCanvasGroupがない）を反復確認。PlayableSettingsView.Open line64→PlayableEntry.OpenSettings line63→Button.OnPointerClickの実経路。押下は受信していたが、表示直前の背景interactable読み取りで停止していた。抜粋 MechaBattle/Build/SETTINGS18_1792152023319/editor-error-before.txt。

GPTのctor実装 GetComponent<CanvasGroup>() ?? AddComponent<CanvasGroup>() が原因。Editorのmissing component managed wrapperはCLR nullではないため、??では右辺を呼ばず無効参照をキャッシュしてしまう。Unityの==nullはこの状態を判定する。[Unity6.3公式Object説明](https://docs.unity3d.com/6000.3/Documentation/ScriptReference/Object.html)でも、UnityObjectの切断状態に?. / ??が対応しないことを確認した。

前回の19件/396件は純粋データ・シミュレーション試験、20asmは静的APIコンパイルだった。いずれも実CanvasGroup操作を実行しておらず、この例外の不在を保証していなかった。前回のNative Flow試験は未実行のため、実機受入の代わりに数えない。

## 修正と回帰

MechaBattle/Assets/Game/配下:

- Runtime/Playable/PlayableSettingsView.cs: GetOrAddCanvasGroupでUnityの==null判定を使用して実コンポーネントを生成。背景GameObjectを保持し、再表示時のコンポーネント消失も補修。閉じる際は生存している背景グループだけ元の入力状態へ戻す。既存グループ/alphaは保持。
- Tests/EditMode/Playable/PlayableSettingsViewTests.cs（新規）: 不足グループ/非アクティブ背景、既存グループと入力状態復元、再表示前の削除、開いている間の削除の5ケース。ユーザーSceneを生成・保存せず、一時PreviewScene内で実Unity GameObject/CanvasGroup/Open/Closeを検証する短いEditMode試験。NUnitのCLR nullだけではなく、本物のコンポーネント数・Unity null判定を検査する。
- Tests/EditMode/Playable/PlayableFlowTests.cs: MainMenu/制作/参戦準備/Pauseの各入口で実CanvasGroup存在数とButton.IsInteractableを確認してから既存の適用/キャンセル経路を実行。

全Assets/Gameの??使用も点検し、Component取得に??を使う箇所は今回の1箇所だった。他の配列/string/plainデータ参照を機械的に変更していない。新たなScene/Prefab/設定値/Contracts変更なし。

## 実行検証と未実施

- MechaBattle/Build/SETTINGS18_1792152023319/project/ の隔離コピーで静的20asm成功（exit0）、ログ static-compile.log、project/Build/Reports/static-api-compile.json。新規5ケースもコンパイルしたが**Unity Test Runnerでは未実行**。
- 122ソース/asmdefの検証SHAと現行差分0、Scene除外のgit diff --check成功。[機械可読証跡](GPT_SETTINGS18_VALIDATION.json)。
- Unity CLIが現在のPATHに見つからず、既知の別OSユーザー接続制約も回避しない。別Editor起動/OS権限変更/ユーザーEditorの無断終了なし。読めた実Editorログと、こちらで実行した試験を混同しない。
- 修正後の実クリック/実ドラッグ/適用/取消/再起動復元、Windows/FPS・GCは未実施。今回、Unity非依存ロジックは変更しておらず396件のドメイン試験は再実行していない。

## 取り込みとClaudeへの引継ぎ

Play停止→Assets > Refresh→コンパイル完了→既存PlayableでPlay→メインの「設定」。Scene再生成不要。必要ならWindow > General > Test Runner > EditModeでGame.Tests.PlayableのPlayableSettingsViewTestsを実行する。

Claudeへ: 設定の無反応はUI部品不足を??で見逃したGPT側の実装不備であり、修正済み。新規Native5ケースを今後の実Editorチェックへ含める。レビュー17の受領/入力コピー・方位追従・非表示戦績更新対応の報告を実文書で受領した（getter公開の残る信頼モデルは記録継続）。SETTINGS18のClaudeレビュー受領は未確認。全予約を解除しており、追加承認待ちは設けない。
