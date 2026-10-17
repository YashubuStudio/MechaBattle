# AIR-15 引渡し：立体的な機動（GPT、2026-10-16）

owner GPT / status 実装・独立検証完了、Unity受入・Claude最終レビュー待ち。要件FR-501/801、SYSTEM_DESIGNの空中高速移動、最新ユーザー「全体的にもっと立体的な機動」。正本/UserTextは変更していない。

## 実装

- Space短押しで従来の離陸、保持で上昇6m/s、空中で押し直すと加速度24m/s²で落下を制動して再上昇する。二段ジャンプの瞬間加速は与えない。上限は最後の離陸面から10mで保持中は高度維持。上限でも25boost/秒を消費し続ける。
- Shift＋WASDは地上ダッシュ/空中滑空。Spaceなしの滑空は落下2.5m/sへ制動し、横ブースト17.5/秒を一度だけ消費。Space同時なら斜め上へ進み消費も合算。入力を離すと重力落下する。
- 空中速度係数0.95、加減速70m/s²で切返し/停止を短く補間。衝突で切られた実速度を保存するため、壁へ押し付けた速度を蓄積しない。天井/足場/壁の既存衝突を共用する。
- 空中で回復しない。燃料0でOH、空中推力/滑空/ブースト不可。着地後の低速回復20/秒・実効満タン解除を保持。人間容量130/BOT100のClaude変更を保持。保持のまま着地しても自動再離陸しない。出撃時に空中速度/基準面をリセット。
- BOTの公開地形の段差認識を同じ上昇上限の85%へ拡張。5機に1機の射撃役だけ、可視接触/健全HP/燃料/行動可能等を満たす時に0.65秒上昇を6.5秒間隔で使う。退避/補給/OH/行動阻害時は使わず、他の主力はラインを保つ。新しい敵真値や人間入力への介入はない。
- 脚/胴体の姿勢を縦速度と空中旋回に合わせ補間。表示が攻撃判定を変えない。既存Vitalsに現在高度/実効容量、既存操作文に上昇/滑空を追加。4行のVitals/案内は既存枠内へ収まるよう改行し、uGUI階層を再生成していない。
- 新規任意aerialBoostEnabled/botAerialManeuversEnabledと速度/上限/周期をSimulationConfig/PlayablePrototypeへ外出し。未指定falseは旧弾道ジャンプ/移動の回帰を保持。実config両コピーに理由付きaerial15Notes。既存最大3装備・Space/Shift・50vs50+2指揮官・各軍3本拠点を保持。

## 対象と区切り

Simulation/BattleAerialMotion.cs、BotAerialTraversal.cs、Tests/EditMode/AerialTraversalTests.csが新規。BattleTraversal/BattleSimulation/SimulationConfig/BotTraversalAdapter、PlayableConfiguration、BattleBoostのGetBoostCapacityを接続。PlayableEntry/PlayableUI/LocomotionWorldView、実config両方、独立検証スクリプトと関連docsを変更。

JSONは第1区切り、移動コードは第2区切り、HUD/姿勢/検証コードは第3区切り、後述モデル補修は第4区切りで解除した。最終の文書区間もWORK_BOARDへlock: releasedを残して終了する。GPTの保持を理由にClaudeを提案/承認待ちへ戻さない。

## 統合検証の訂正と実行

ConstructedBattleSmokeには独自の設定転記があり、新AIR項目だけでなく以前の格闘命中補正/人間倍率等もコピーされていなかった。手作業の一覧を廃止し、ゲーム本体と同じ**実PlayablePrototype.Apply**へ統一した。verify-domain.ps1にもUnity非依存のRuntime/Playable/PlayableConfiguration.csだけを追加。過去の構築BOT結果を全Playable設定が反映された試合として扱った説明は訂正する。過去のログや個別テストの実行自体を改変せず、適用範囲を限定する。

- 隔離先: MechaBattle/Build/AIR15_1792138034645/。実71ドメインソース/Unity API用ソース/実config全JSONをコピーし、.NET9 Roslyn optimize+でコンパイル。ClaudeとBuild/DomainSmoke/CompileCheck/Reportsの実行出力を共有しない。静的検証は既存verify-compile.ps1の出力/参照先だけを隔離に合わせた複製を使用。
- 対象49/49成功（新規AIR18＋既存地形/ブースト31）。上昇/短押し/再推力/上限保持/滑空の消費一度/逆入力慣性/壁天井/5.4m高台着地と再離陸/再出撃/燃料0・OH/保持着地/旧無効設定/BOT高台越え/限定上昇/不正設定を検査。
- 初回2失敗は試験準備不備。天井まで上昇し終える前から自由落下を計測したケースは実測Y/Vを確認して準備待ちを修正、本拠点の禁止領域に試験足場を置いたケースは合法な高台へ実移動/着地してから再離陸する試験へ修正。バリデーションや期待挙動を弱めていない。
- 全回帰**335/335**・静的**18アセンブリ**成功。ClaudeのFA/AR/フレーム武器制限と新規回帰も含む。8プリセット/標準52単装備・8788三装備の評価/保存復元が成功。実設定合成後100BOTは323.9sim秒で決着、58撃破/157出撃/84コア命中/21反撃/11占領変化、両軍最低47/48、平均99.46機、中央戦場割合75.04%。
- 同時空中最大55機・空中機体秒8150.68・最高Y13.12mを記録。10mは離陸面からの相対上限なので、高台からの到達高度は地面基準10mを超える。全員が常に飛んでいたという意味ではない。
- 最新結果: project/Build/Reports/domain-smoke.json、constructed-battle.json、static-api-compile.json。quick.log/domain-run.log/static-compile.log、manifestと現行差分はGPT_AIR15_VALIDATION.json。

## 並行作業の受領と表示部品の補修

Claudeの人間倍率/通常例外/高さ/格闘ロック312件、追加FA/AR/8構成、フレーム武器制限5回帰を作業表で受領。JSON同時編集の連絡を受け、毎回最新を読み直して双方の変更を保持し、第1区切りでGPTのJSON予約を解除した。Claudeが検出したAIR初回2失敗は修正前のスナップショットで、今回の335件成功で解消を確認。Claude同意/受領を捏造していない。

統合時にweapon.assaultrifleの表示モジュール欠損を発見（Kit18に対しカタログ19ID、MechaMeshLibraryにVisual part missing例外の経路）。モデル作業の新規予約なしを確認・追加予約して、既存生成スクリプトへ独自ARの5閉立体を追加した。既存18モジュールは形状JSON差分0。Kit/GLB/sourceを生成・検証し、**19部品/8構成/153閉立体/6060三角形**、全カタログID/全GLB選択装備/ソケット/法線/閉立体/バッファ検査成功。Scene/Prefab/SOのYAMLは変更していない。画像/Unity描画/Prefab生成の新規確認はしていない。

## 未実施と次の確認

Unity CLIは現在PATHに見当たらず、前回のEditor検出はOSユーザー間アクセス拒否。別Editor起動や権限変更で回避していない。Unity Test Runner/最新Play/画面配置と姿勢/空中照準/Windows起動/FPS・GCは未実施。ドメイン試合と静的コンパイルを実機合格として報告しない。

取込はPlay停止→未保存作業を保護→Assets > Refresh→コンパイル完了→既存PlayableでPlay（今回シーン再生成不要）。Space短押し/長押し/落下中の再保持、Shift滑空/斜め上移動、5.4m高台越え、天井/壁、燃料0の落下と着地回復、ARを持つBOTの表示を確認する。

Claudeへ: 上昇の相対上限/無限回復防止、連続推力とOH、BOTの自己/可視接触制限、実Applyを使う検証、AR部品IDを読み取りレビュー願います。今回AIR15の最終レビューは未受領。チャージ格闘別枠/腕統一/本拠点ロック/追加マップUI/最新UI刷新等の未実装項目は従来の引継書と作業表に維持し、今回完了とは扱わない。UserText/正本/Editor未保存状態/Git indexは保護。
