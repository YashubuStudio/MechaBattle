# UI-11 / PREVIEW-11 引渡し（GPT、2026-10-16）

owner: GPT / status: ソース実装・独立検証完了。最新Unity描画・操作/Claude最終差分レビュー待ち。対応要件はユーザーの制作画面表示不良と全体見直し、FR-801/805。

## 確認できた不備と対応

| 確認内容 | 対応 |
|---|---|
| 制作画面が完全に透明、戦闘と同じカメラの部分viewportにワールド上のモデルを直接描く | 不透明な制作UIと専用RenderTexture/RawImageへ変更 |
| 参戦準備はカメラを動かすがモデルを生成しない | 選択機体のプレビューを生成し、デッキ/軍の変更へ追従 |
| メインへ戻る際にカメラrect/nearclip/照明を戻さない | 全非戦闘画面でrect全画面・mask0・near0.1・Sun無効、戦闘時は全画面と戦闘描画へ復帰 |
| 参戦画面のselectedDeckを出撃へ渡さず、常に0番機体で開始 | initialHumanDeckIndexの互換任意引数へ渡し、HP/再出撃選択も一致 |
| 出撃できない理由を非表示の制作画面Statusだけへ書く | 参戦準備にも通知/エラーを表示 |
| 画面切替後の非表示ボタンがEventSystemで選択されたまま | UI画面切替でゲーム内選択をクリア |

「線やSun」の発生ビュー（Game/Scene）は任意質問に未回答のため未確定。上記のコード不備は確認したが、ユーザーの画面を直接見たとは報告しない。ライト等のアイコンはEditorギズモの可能性もある。[Unity 6.3のGizmos説明](https://docs.unity3d.com/6000.3/Documentation/Manual/GizmosMenu.html)を確認した。実使用URP17.3のUniversalRendererRenderGraph.csではOverlay UI後にもPostImageEffectsギズモを描くため、不透明UIだけでは完全には対処できない。

## 変更ファイル・表示方式

- Runtime/Playable/HangarPreview.cs（新規）: 1024×768 ARGB32/depth24のRTを一つ再利用。専用カメラ/キー・補助ライト、描画層31、地形から離したスタジオ。メッシュ境界球と4:3に合わせ自動で全体を収め、回転中の切れを抑える。モデル交換時は旧表示を即時無効化、終了時はRT/カメラ/照明を解放。
- Runtime/Playable/PlayableEntry.cs: 上記の所有/表示状態、メイン/制作/参戦準備/戦闘の遷移・通知・初回出撃番号・解放。プレイヤーカメラをEntry配下へ置き、プレビューと分離。
- Runtime/Playable/PlayableUI.cs: 既存Canvasを保って不透明背景と4:3 FitInParentのRawImageを追加。制作/参戦準備ともraycastを妨げない。通知欄と安定した操作ボタン名を追加。
- Runtime/Simulation/BattleSimulation.cs: コンストラクタ末尾に任意initialHumanDeckIndex=0。人間の最初のPreferredDeckだけへ適用し、範囲外や人間なし試合の非0指定を拒否。旧呼出/BOTのデッキ順/CT/ゲージ/出撃数は維持。共有Contractsは変更していない。
- Editor/PlayableTools/PlayableEditorPresentation.cs（新規）: MechaBattleのPlay開始中、既存Gameビューのギズモを一度だけ抑えるEditor限定スコープ。元がtrueだったウィンドウだけ記録し、Play終了・再コンパイル前・Editor終了で復元。新規ウィンドウを開かず、Sceneビュー/フォーカス/グローバル設定は変更しない。Play中の明示再有効化を繰り返し無効化しない。
- Tests/EditMode/PlayableRulesTests.cs: 初回出撃の新規7ケース。
- Tests/EditMode/Playable/PlayableFlowTests.cs: 実ボタンで一巡、プレビュー構成/画面/カメラ/照明/縦横比/枠内、7preset、選択した2番機体からの出撃、ネイティブ描画があれば画素に機体があることを追加。
- Tests/EditMode/Playable/PlayableEditorPresentationTests.cs（新規）: GameViewギズモtrue/false復元・Scene/フォーカス保護・明示再有効化の3ケース。Unity未実行。

Editorギズモ制御は、Unity 6000.3の内部GameView.drawGizmosへ限定したアダプター。存在と読書可否を検査して未対応なら操作しない。通常の公開APIではないため、Editorバージョン変更時はこの回帰試験を実機で確認する。[Unity公式6000.3 C#参照ソース](https://github.com/Unity-Technologies/UnityCsReference/blob/6000.3/Editor/Mono/GameView/GameView.cs)のプロパティと一致を確認し、Unity製品コードを複製していない。Windows PlayerにはこのEditorコードを含めない。

レンダー層31は現TagManagerで未命名、PC/Mobile RendererのOpaque/Transparent maskは全層を許可していることを読取確認。TagManagerやRenderer assetを編集していない。RTのサイズ/4:3はUI表示用の実装選択で、FPS/GC効果は未計測。

## 実行結果

- 独立C# **260成功/0失敗**（従来253+初回出撃7）。両軍・選択0/1、初期HP/弾薬/ゲージ/CT/100出撃、BOT既定順、無効番号を検証。
- 実設定100BOTは前回と同じ194.87999sim秒/29撃破/128出撃/112コア命中/23反撃/9占領。選択番号省略時の回帰成功。人間のUI/描画を確認した試合ではない。
- Unity API静的 **17アセンブリ成功**（Editor対策/新規Unity向け試験も含む）。既存JSON注入用CS0649警告のみ。
- 生成Scene YAMLを除くdiff --check成功。実設定/マップの隔離コピー一致、検証後の57ドメインソース更新0。115対象のSHA256を保存。
- 検証先: MechaBattle/Build/UI11_1792127212957/。compile.rsp、compile.log、domain-run.log、Build/Reports/、source-manifest.json、final-check-0/1.log。機械可読は[GPT_UI11_VALIDATION.json](GPT_UI11_VALIDATION.json)。

モデル幾何や戦闘パラメータをこの作業では変更していない。既存のUserText/Scene/Prefab YAML/Git indexを保持。Claude所有Observation/AI.Bots/UnityAdaptersへ変更なし。

## 未実行・受入

Unity Test Runner、実プレビューの描画/画素読取、GameViewギズモ復元、最新ユーザー試遊、Windows起動、FPS/GCは未実施。Unity CLIは今回の実行PATHに存在せず、従来の別OSユーザー検出ファイルアクセス拒否も未解消。新規3.csの.metaは監査時点で未生成。権限回避/別Editor起動はしない。

1. Play停止→Assets > Refresh→コンパイル完了→既存MechaBattle_PlayableをPlay、Gameタブで確認。このUI修正自体はScene再生成を要求しない。P0用シーンには今回のUIはない。
2. 起動→機体制作で右側に機体と選択装備、背景に戦場/古いフレームが残らないことを確認。7presetと腕/装備交換で表示を確認。
3. メインへ戻る→参戦準備で4枠を選び、モデル/軍色/選択表示を確認。2〜4番を選んで出撃し、その機体/装備/HPで始まることを確認。
4. 休止→参戦準備→制作→メイン、結果→制作、再戦を確認。通知欄とカーソル/画面選択、プレビュー二重表示がないことを確認。
5. GameビューのGizmosが元々ONならPlay中に一時OFF、停止で元に戻ること、Sceneビュー/フォーカスを勝手に変えないことを確認。Play中に手動ONへ戻した場合はそれを継続して上書きしない。
6. Test RunnerのGame.Tests.Playableを実行。画素検査はNull graphics deviceでは描画受入を行わない。

## 共同レビュー

ClaudeのLOCK10/SUPPORT10独立253/253・17アセンブリと最終承認を実際にWORK_BOARDで受領。UI11は途中/完成差分の読取レビューを依頼済み、今回の新しい受領/承認は現時点未確認。上記の静的/純C#成功を実機の表示合格や全MasterPlan完了と扱わない。
