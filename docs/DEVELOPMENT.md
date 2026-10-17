# MechaBattle 開発入口

呼称は **GPT** と **Claude**。Unityプロジェクトは D:/game/MechaBattle/MechaBattle。
正本は ../MechaBattle_MasterPlan/。この文書は初期P0の開始手順を含む。現行プレイ版は[PLAYABLE_GUIDE.md](PLAYABLE_GUIDE.md)、追加実装と最新検証は[最新引渡し](reviews/GPT_TACTICS32_HANDOFF.md)を参照。P0全体の性能受入は未完了。

## 確認済みの環境
- ProjectVersion: 6000.3.26f1（Unity 6.3 LTS）。Editor実体: C:/Unity/6000.3.26f1/Editor/Unity.exe。
- URP 17.3.0、Input System 1.20.0、AI Navigation 2.0.15、Test Framework 1.6.0、uGUI 2.0.0、Pipeline 0.8.0-exp.1 は既存manifestの値。今回は変更していない。
- 対象は Windows x64。Android/Web/macOS/UWP等の追加モジュールを導入しない。
- ルートのGitリポジトリと.gitignoreは並行作業で作成済み。GPTは既存コミットやリモートを変更していない。

## 初期P0で用意したもの（履歴）
- Runtime/Contracts: 100操縦枠・2指揮官の初期編成と共通 UnitCommand。
- Runtime/Battlefield: 本拠点と中継拠点を別型で定義し、各軍2本拠点未満、重複ID、不正陣営、座標/半径、単一経路宣言を拒否。
- config/standard-map.json: 各軍3本拠点と3中継拠点の仮データ。座標・半径の理由はconfig/prototype-defaults.json。
- Runtime/Simulation: 移動/射撃/撃破/再出撃/デッキCT/有限弾薬/ゲージ/占領/基地補給の平面モデル。
- Runtime/Observation, AI/Bots, UnityAdapters: Claude担当の自己観測・本拠点攻撃/補給BOT・シーン接続。
- Editor/Generators: 標準マップと100機を生成し、Runtime設定をシーンへ保存するコード。
- Tests/EditMode: 編成・マップ・ルール・実BOT連携を検証。独立C#では51件成功。
- Editor/Preparation: 定義検証と、現在の有効シーンを使うWindows開発ビルドの入口。

経路名が異なることは実際の到達性の証明ではない。NavMesh、攻撃Collider、出撃空間、遮蔽物はGeneratorとPlayModeで検証する。
初期P0は平面シミュレーションだった。現行Playableは人間操作・3D移動/遮蔽・ブースト/ジャンプ・射撃/格闘・3装備/設置物・範囲内知覚を追加済み。STRATEGY-23で妨害/広域レーダーと損失補充ポイント、射界/遅延/旋回制限つきBOT知覚を接続。CARRY-24では核を自軍側要請→拾得/運搬→地上使用の迎撃破壊可能な物体へ変更し、B緊急帰投→同機体の補給/再配置を接続。CITY-26で3選択マップ（荒地/立体市街/二層都心）、ABC/XYZの本拠点耐久/陥落、物理的な地上・地下と6中継、公開地形のBOT階層経路、コア衝突と後方照準ガードを接続。TACTICS-32でClaudeの既存小隊データを個体の公開拠点目標/集合へ接続し、迎撃分散を追加。金功績・全作戦プリセット・指揮官AIの戦略判断・核の自動運搬判断は未実装で、指揮官は別枠2枠の編成のみ。

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

このランナーは純粋C#回帰と実設定の統合チェックを直接実行するスモーク検証。2026-10-16のAIR-15時点335件成功（単装備52/三装備8788構成の保存復元と実設定100BOT試合を含む）。Unity Test Runner実行とは区別し、標準スクリプトの結果は MechaBattle/Build/Reports/domain-smoke.json に出す。AIR-15最終検証は競合回避の隔離先 MechaBattle/Build/AIR15_1792138034645/project/Build/Reports/ に保存した（実71ソース、.NET9 optimize+、実config/マップコピー）。構築試合は実PlayablePrototype.Applyで設定を合成し、独立検証へRuntime/Playable/PlayableConfiguration.csも含める。静的APIコンパイルは新規Game.AI.Squadsを含む18アセンブリ成功。

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

## 最小プレイ版（PLAY-01）
起動・操作・保存・試験・Windows出力は[PLAYABLE_GUIDE.md](PLAYABLE_GUIDE.md)を参照。P0用シーンとFoundation Buildではなく、Playable専用の生成/ビルドメニューを使用する。


## YARD-28 / 三帯フィールドと照準のゼロ長レイ（2026-10-17）

「再生工廠・三帯戦線」を4番目のフィールドとして接続。900×520m、上段18m/屋外低地0m、290の共有衝突volumeと76の公開nav節点、6中継/各軍3本拠点。中央dropは有向リンク、両外周は段差0.25mの72段ランプ。生成元は scripts/content/generate-reclamation-yard-map.mjs、実config/ResourcesのJSONを同期する。MapDefinition.lowerLayerNameは追加任意フィールドで、旧JSONの地下名は保持する。

ユーザーの実例外 Empty aim ray（RefreshAimFeedback:799/809）を確認。TraceAimは有限なゼロ長区間を原点/no-hitへ変更し、NaN/Infinity/不正range拒否を維持。HUDをtry/catchで握り潰していない。修正前再現8成功/3失敗→修正後11/11。全514/514・20asmは地形仕上げ前に実行、コード同一で地形JSON2つだけを更新した最終版は照準11/11・実motor/公開nav・100BOT600秒（367撃破/466出撃/179本拠点命中）と20asmの対象検査も別証跡で成功。詳細と結果は [引継書](reviews/GPT_YARD28_HANDOFF.md)へ記録する。

今回のCLIはstatusで接続なし、pipeline listで起動中/パッケージ有り/SafeModeなし/到達可能server0を確認。更新確認はネットワーク制限で失敗。Editor.logは読み取り可能でユーザーのスタックと一致した。別Editor起動や権限変更はしていない。実Unity/Native/Windows/FPS・GC検証とは区別する。


## BOT-29 / 少数支援役と地図判断の分散（2026-10-17）

PlayablePrototype.botBehaviorをP0へコピーして有効化し、BotPopulationが人間/RemoteHumanを除くBOT枠だけを各軍の比率で抽選する。BotDeckPolicyはレーダー役へ既存の合法支援presetを選び、実装備/コスト/有限弾/CTを維持する。BotBattlefieldPlannerは自己・公開地形/本拠点・味方レーダー・有効なTeamContactだけを読み、設置点/防衛/調査の移動目標を返す。目標からの射撃/ロックは作らず、既存BotPerceptionが視認/LOS/走査/認識/旋回を担当する。BotTraversalAdapterは回復/補給を優先し、戦術目標と通常進軍が同じ階層経路cacheを二重に切り替えない。

schema1のPlayerPreferencesにbotMixConfiguredと3比率を任意追加。旧保存は既定の4%/8%/35%を使い、有限範囲と合計上限を正規化する。既存設定モーダルへ3スライダーを接続し、下書き/初期値/キャンセル/保存を維持。編成への反映は次のBeginMatchから。仮値と理由は実configのbot29Notes、受入/検証/未実機事項は [Claude引継書](reviews/GPT_BOT29_HANDOFF.md)。Scene再生成は不要、新規.csの.metaはEditor Refreshに任せる。

BOT-29の最終検証は106純C#・536/536、全20asm静的コンパイル、全4マップ100BOT600秒成功。途中で見つけた階層cache競合・回復ポッド/本拠点攻撃の地図割込み・低燃料の通常格闘を自動回避がD格化して不発にする干渉を回帰で修正。Native/Unity実画面/Windows/FPS・GCは未実施。最終証跡はBuild/BOT29_1792198879692/final3/。全編集予約解除、GPT背景プロセスなし。


## STUCK-31 / 座標を使ったBOT復旧・しゃがみ（2026-10-17）

BotTraversalAdapterの最終出力にBotStuckRecovery部分を追加。直前に実発行した移動と自己3D座標だけを比較し、硬直/意図停止を除外。分散した停滞窓→公開地形と進行方向を考慮した回避→通常帰投を1回要求し、被弾キャンセル/再試行待ちを扱う。後退して同じ閉塞へ戻る短周期ループも記録して帰投へ進める。BattleEmergencyReturnはBOTだけ、実際の補給が完了した後に同一Deckを別の生存本拠点へRequestSpawnする。人間/RemoteHumanの再配置を自動操作しない。

UnitCommandへ任意Crouch、UnitSnapshotへCrouching/ReturnChanneling/CarryingNuclear（自分の状態）を追加し、全命令コピーへ伝播。BattleCrouchは地上静止・帰投の姿勢を共通化し、通常/OHの回復を2倍にする。人間入力は左Ctrl、LocomotionWorldViewの腰/膝/胴体と頭部視点へ接続。既存命令の引数は末尾任意追加で互換、旧fixtureの新フラグはfalse。新ルールと操作・理由は実configのstuck31Notes。独立100BOTの帰投は撃破/ゲージ損失として数えず、回避/帰投要求/帰投完了を別計測する。検証/実機未実施は[Claude引継書](reviews/GPT_STUCK31_HANDOFF.md)。


## TACTICS-32 / 小隊長カラーと両軍の戦術判断（2026-10-17）

BotSquadCoordinatorがGame.AI.Squadsの既存SquadRosterを利用し、Game.Simulationの依存を1つ追加。所属/指揮官機の固定資格と公開拠点の共有・保持/短時間集合/味方混雑による分散を純C#で扱う。人間は入力対象にせず、共有索敵の利用率はBotPopulationの既存設定を維持する。BotBattlefieldPlannerは報告群への迎撃人数を制限し、レーダー役は実在する有限装備から3個までのカバーを展開する。BotRecoveryは生存補給先と階層を評価する。

PlayableWorldViewの全モデル生成でCoordinator.IsLeaderを渡し、MechaMeshLibraryは戦闘一般機だけ特殊素材を抑止。格納庫プレビュー/保存Build/性能/陣営識別色は維持する。仮値と理由は実configのtactics32Notes。関連83/83成功。統合前レビューで独立裏取り枠と帰投/負傷隊長への追従を保護し、先行都市の本拠点命中0による584成功/1失敗を記録。修正後の全回帰/実4マップはfinal2で実行中、20アセンブリの静的コンパイルは先行入力で成功。未実機・最終証跡は[引継書](reviews/GPT_TACTICS32_HANDOFF.md)。
