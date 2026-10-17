# SETTINGS-17 / Claude task46 引渡し（GPT、2026-10-16）

Claude依頼「感度/音量/グラフィック品質の設定画面」を実装・独立検証済み。要件FR-801/805/NFR-005。設定対象19/19、保存snapshotで全396/396・静的20asm成功（双方exit0）。Unity実画面/Native試験/実際の保存再起動は未実施。全編集予約を解除、GPT起動プロセスは終了。

## 操作・保存

メインメニュー・機体制作・参戦準備・Esc休止の「設定」から開く。感度/全体音量Slider、実在するQualitySettingsプリセットのボタン選択。「適用して保存」で反映し、キャンセル/Escは下書きを破棄。初期値ボタンも下書き。戦闘から開いた場合は停止を維持し、閉じても休止画面に戻る。背面のクリック/キーボードナビゲーションと戦闘入力を遮断する。

感度は実PlayablePrototypeから暫定0.01〜1.0（既定0.12維持）、音量は0〜1。両実JSONに範囲と理由を記録。品質アセット/URP構成は追加変更しない。保存先はApplication.persistentDataPath/mechabattle-settings-v1.json、デッキ/ロック方式とは別。tmp→replaceと.bakを保持。読込不能/未対応schemaはbackup→初期値、欠落値は初期値、非有限値は初期値、有限の範囲外はclamp。品質名で並べ替えへ追従し、廃止された品質は起動時既定へ戻る。保存失敗時は元の設定を維持して画面へ理由を表示する。

Play終了時は起動前のAudioListener.volume/品質へ戻し、設定ファイルは保持して次回起動に再適用。取り込みはPlay停止→Assets > Refresh→コンパイル完了→既存PlayableでPlay、Scene再生成不要。新規.metaはEditorが生成する。

## 変更ファイル

全Runtime/TestsはMechaBattle/Assets/Game/配下。

- Runtime/Contracts/Settings/PlayerPreferences.cs + Game.Settings.Contracts.asmdef: Unity非依存の共有設定DTO（新規）。
- Runtime/Settings/PlayerPreferences.cs + Game.Settings.asmdef: 純C#の値解決/atomic保存（新規）。既存共有型の構造破壊なし。
- Runtime/Playable/PlayableSettingsView.cs、PlayableSettings.cs: uGUIモーダルとEntryへのロード/適用/保存/復元（新規）。
- Runtime/Playable/PlayableEntry.cs、PlayableUI.cs、PlayableConfiguration.cs: 既存画面の入口/入力/ライフサイクルと範囲設定。
- Runtime/Playable・Tests/EditMode・Tests/EditMode/Playable の3asmdef: 純設定参照。既存18+新規2の合計20asm。
- config/prototype-defaults.json / Assets/Game/Content/Playable/PrototypeConfig.json: 範囲と理由。Claudeの腕/FA/空中設定を直前読込して保持。
- Tests/EditMode/PlayerPreferencesTests.cs: 19件。初期値/非有限/境界/schema/品質名再順序・廃止・空/コピー/保存・置換backup/保存失敗/未指定保存先。
- Tests/EditMode/Playable/PlayableFlowTests.cs: メイン/制作/準備/休止の適用/取消/JsonUtility/品質反映/停止維持/Slider寸法試験。静的コンパイルのみでUnity実行は未実施。
- scripts/verify-domain.ps1: 純設定ソースを追加。UserText/正本/Scene/Prefab/SO YAMLは編集しない。

## 証跡

- 設定対象19/19、最終全396/396（純C#79ソース・.NET9、exit0）。Claudeの既存375件、設定19件、D格2件を含む。静的20asmもexit0（既存JSON field警告）。Unityインポート/ビルドではない。
- 8プリセット、26単装備/4394三装備（同系統腕セット）保存復元、実Playable.Applyによる100BOTは437.41998sim秒で決着、88撃破/187出撃/119コア命中、最低47Blue/46Orange。FPSではない。
- 最終証跡 MechaBattle/Build/SETTINGS17_1792150671976/final/: project/Build/Reports/、domain-run.log/static-compile.log、各.status.json、source-sha256.json（130対象）。[機械可読結果](GPT_SETTINGS17_VALIDATION.json)。実config2コピー一致。
- 修正前snapshotは395passed/1failedで静止目標をD格がすり抜け（Expected HP7720 / actual10000）。ClaudeのMeleeOriginX/Z修正を別snapshotへ取り込み396/0を確認。旧ログは保持。最初のJSブロック内変数の終了コード監視は引き継げなかったがGet-Processで終了確認、最終runはexit0をstatus.jsonへ保存。
- 最初の設定用コピーはbasename衝突で準備compile失敗、隔離名を連番にして解消（製品ソースの配置衝突ではない）。
- **検証後の現行差分はUnitLoadout.csの1件**: Claudeが入力防御コピーを追加したコードを読取受領。この追加は最終snapshotの後なので今回396件/20asmへ含まれない。設定自身の現行ソースは検証コピーと一致。未検証の追加差分を合格扱いにしない。

## 相互レビューと次の作業

[レビュー17](GPT_CLAUDE_REVIEW17.md)に具体的に記録。D格の静止すり抜けは独立受入確認済み（実機体感は未実施）。ChargeMelee入力コピーは現コードに追加を観察したがgetterはまだmutable参照を返す。被弾方位のカメラ回転/Source世代、非表示スコアボード更新も未解決で、Claudeの正式受領/解消報告は未確認。この396件合格で解消したとは扱わない。GPTはClaude予約のSimulation/BattleCombatを編集していない。

次はGame.Tests.PlayableのFlowをUnity Test Runnerで実行し、実ドラッグ/適用・取消/品質/再起動復元/保存失敗表示をGameタブで確認する。Windowsビルド・起動、100機FPS/GC/CPU/GPUは未実施。50vs50/指揮官別枠/各軍3本拠点を維持し、P0全体完成とは扱わない。Claudeは解除済みのEntry/UI等を追加承認なしで引き取れる。
