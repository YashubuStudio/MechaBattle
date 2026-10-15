# P0共同制作・ドメイン統合報告 — 2026-10-15

状態: ドメイン/試作BOT/Generatorのコードを統合。**P0全体は未完了**。
GPTとClaudeは共有作業表とreviews/で相互レビューし、担当ファイルを分けて変更した。

## 変更と要件
- GPT: Runtime/Simulation/{SimulationConfig,SimulationSnapshots,BattleSimulation}.cs。50Hz・100操縦枠・機体ID更新、移動、通常射撃、HP/Shield/Ammo、デッキごとのCT、撃破/再出撃、各本拠点別損害と戦力ゲージ、中継占領、前線出撃、基地補給、試合終了を平面モデルで実装。
- FR-001/002: 100枠・50対50を維持。指揮官は2枠の編成のみでAI本体は未実装。
- FR-101〜106/108/109/201〜204/401〜404/408: 試作ルールとデータ検証を接続。
- FR-503/504/505/506: BOT→Humanだけ赤ロック距離0.8、通常射撃は距離短縮で禁止しない。被弾からの自動反撃はない。
- 入力検証: 所有UnitId、Tick、連番、有限値、移動入力範囲を検証。再出撃前の旧UnitIdや再送を拒否。
- Claude: Runtime/Observation、AI/Bots、UnityAdapters、Editor/Generators、Tests/EditMode/Generatorsを追加。AIは自己観測と公開本拠点だけを読む試作BOT。
- 自動生成コードは各軍3本拠点・3中継・100機を作り、保存するJSONとTransform参照からRuntimeを復元する。生成自体のUnity実行はまだしていない。
- GPT: SimulationTests.cs / BotIntegrationTests.cs / scripts/verify-domain.ps1 / verify-compile.ps1 / DomainSmoke/*.csを追加・拡張。

## 相互レビューで修正した点
- ClaudeからのAPIレビュー6点へ回答: 移動軸入力、決定的な命中、観測アセンブリ分離、繰返し本拠点損害、数値設定、生存機のみの占領人数。
- GPTから保存/DomainReload後の復元、人間枠をBOT制御しないこと、本拠点/補給への目標設定、URP素材永続化を指摘。Claudeが実装を修正。
- 実BOTのRemoteHuman拒否テストで不具合を検出し、Claude修正後に再試験を通過。
- 追加確認3点（全ロードシーンの未保存保護、Unitのチーム素材、保存/再読込テスト）もClaudeが実装。GPTはテストのEditMode/Awake不整合を再指摘し、該当ケースを引き取ってEnterPlayMode/ExitPlayModeのUnityTestへ修正。Updateによる実時間Tickと失敗時の退出も確認する形にした。Unityでの実行は未実施。詳細はreviews/GPT_REVIEW_CLAUDE_P0.md。

## 検証結果
- 独立C#チェック: **51成功 / 0失敗**。48 NUnitテストケースの直接実行と、標準JSON/試験用100枠対戦/実BOT＋標準設定の3統合チェック。Unity Test Runnerで実行した件数ではない。
- 全10アセンブリの静的APIコンパイル成功。ローカルUnity 6000.3.26f1のモジュールを参照する検査で、Unityのインポート・IL後処理・Playerビルドとは別。JSONフィールドのCS0649警告1件は反射的デシリアライズ用フィールド。
- 同一入力による再現、連番の周回、CT待機、資源上下限、同時致死射撃、本拠点6個それぞれへのゲージ反映、中継の中立化・再占領・CT中機体除外、Human/RemoteHuman保護を検証。

### 試験用入力シナリオ（撃破・CT・再出撃を通す）
100枠・ピーク100機、試合内50.86秒でBlue勝利。撃破106、出撃206、射撃1356、本拠点命中463。全6本拠点への損害を確認。
入力は序盤の範囲限定交戦→本拠点突撃という明示的なテスト用シナリオで、製品BOTとは異なる。
.NETプロセスで50Hzを高速実行し、ウォームアップ50Tick後のコマンド＋Step区間のスレッド割当は0 bytes。UnityのGC/フレーム計測ではない。

### Claude製試作BOT＋実際の標準JSON設定
100枠・ピーク100機、試合内29.74秒で両軍ゲージ0の引分。射撃503、本拠点命中503、撃破0。
公開目標＋自己弾薬のみで本拠点攻撃/補給へ進む。敵機知覚や指揮官は未実装。このケースは撃破/CTの経路を通らないので、それらの網羅確認には上記の別シナリオを使った。

## 仮値・範囲
値と理由はMechaBattle/config/prototype-defaults.jsonのp0Simulation/p0SimulationNotes。HP100・Shield50・Ammo30、移動12m/s、射程65m、損害20、発射0.4秒、4デッキのCT45/60/80/100秒など。
平面hitscan、通常射撃は敵のみ、本拠点は繰返し受付、時間切れ同値は引分。地形遮蔽・3D物理・Boost/格闘・帰投チャネル・支援機・戦略核・金功績・小隊/指揮官・人間操作は未実装。
BOTの停止係数0.9/0.5はp0BotPrototypeNotesへ仮値として記録。現状はコード定数で、JSON編集だけではBOT挙動へ反映しない制限を明記した。

## 未実施・受入待ち
Unityコンパイル/Unity Test Runner/EditMode Generatorテスト/PlayMode/保存再読込/Windows実行/10分Bench A〜E/FPS・GPU・描画負荷計測は未実施。
GPT側CLIはSTATUS_DESCRIPTOR_UNREADABLEで開いているEditorへ接続できない。Claudeも直接接続手段を持たない。権限変更や二重Editor起動で回避していない。
60fps達成・プレイ可能な製品・P0完了とは報告しない。開始手順はDEVELOPMENT.md。
