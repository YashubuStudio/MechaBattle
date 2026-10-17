# GPT → Claude YARD-28 引継ぎ（2026-10-17）

owner GPT。新フィールド「再生工廠・三帯戦線」と、ユーザーが報告したTraceAimの例外を実装・独立検証した。全編集予約を解除する。Claudeへの直接送信/今回受領は未確認。追加GPT承認なしで受入・レビュー・改修を継続可能。

## 照準例外の修正

実ログ: ArgumentException: Empty aim ray、BattleCombat.cs:227 ← RefreshAimFeedback:799（射撃）/809（回復）← Update:557。Editor.logでも一致した。

敵が銃口へ重なるとTraceCameraAimの補正結果が銃口そのものになり、HUDの次のTraceAimの方向がゼロになる。BattleCombat.csは有限のゼロ長/微小長区間を原点・全target IDなしで返す。HUDは命中確定の緑を付けない。NaN/Infinity/不正rangeは従来どおり例外で拒否。try/catchでフレーム全体を握り潰していない。

AimingTestsに実際の重なり条件・0/微小レイ・不正入力の回帰を追加。同じ検査で旧コードは8成功/3失敗（Empty aim ray）となり、現行コードは11/11。既存の遠距離・横移動・上下・地面・バズーカ・本拠点射線も継続成功。

## フィールドの接続

- 新id reclamation-yard、表示「再生工廠・三帯戦線」、900×520m、290衝突volume、76公開nav。4番目の選択肢。生成元 MechaBattle/scripts/content/generate-reclamation-yard-map.mjs、config/reclamation-yard-map.json と Resources/MechaBattleMaps/ReclamationYard.json は同内容。
- 北: 開いた出入口/工房/屋根/路地とクレーン。中央: 多段の廃材、歩廊、遮蔽間の射線でライン形成。南: 屋外低地のサービス路。上段Y18、低地Y0、中央はdrop有向リンク、戻りは左右120m/.25m×72段の外周ランプ。既存の10m上昇上限を変えていない。
- 各軍3本拠点ABC/XYZ、6中継。1とB/Yが中央、2/3とA/Xが工場、4とC/Zが低地、5/6が上段崖の進入地点。全本拠点は既存HP/ゲージ/衝突の通常ルールに従う。
- MapDefinitionへlowerLayerNameを任意追加。旧JSONの表示は「地下」、本マップは「低地」。出撃選択/再出撃/M戦術マップ/ミニマップは共通の地形背景を使用。既存Playableシーンは再生成不要。ReclamationYard.json.metaの存在を確認、GUID/YAML編集なし。
- PlayableWorldViewは既存worksテーマに、工場・錆びた廃材・低地・アクセス路の材質を分ける。衝突と見た目は同じJSONのAABBから生成する。BOT層/Contracts/入力/機体性能は変更していない。

参考は[当時の公式紹介](https://ggame.jp/news/detail.php?id=2184)にある工場/中央ジャンク/南低地の役割。距離・形状・配置・色は独自の試作値、実config.yard28Notesへ理由を保存。既存IPの画像・マップ形状・艦船・機体素材は使っていない。戦術コンテナ収集など追加経済は今回導入していない。

## 実行結果と証跡

共通出力: MechaBattle/Build/YARD28_1792197282365/。

1. quick/aim-before-fix.log: 旧実装で例外3ケースを再現。quick/aim-fixed-result.json と candidate/quick-acceptance.log: 修正後11/11。
2. accepted/: 全101純C#・514/514、全20asm、全4マップの実設定100BOT戦が成功。これは地形仕上げ前244volume。そこから変更した検証対象は新map JSONの2コピーだけで、全C#・既存3マップ・設定のSHAは同一。
3. final/: 最終290volumeの照準11/11、実JSON/production motor13220ticksの往復、低地4占領、dropからの10m上昇限界、両ランプ、工場ドア/側壁射線、敵Y本拠点攻撃を成功。公開navだけのBOTで4074ticks以内に下降→4制圧→Z本拠点命中。モーター経路単体検査のみ防衛砲を停止し、機動設定は実値を使用した。次項の100BOT戦は防衛砲も含めて実設定。
4. 最終実設定100BOTは600秒完走、peak100、367撃破/466出撃、179本拠点命中、陥落4、占領変更28、平均生存97.96、最小青45/橙41。有限弾・CT・燃料・視認ルールの迂回なし。中央位置割合は旧固定窓の指標で、本マップの主戦場分布の合格値とは扱わない。人間の前線体験の受入は別。
5. 最終20asm静的APIコンパイルもexit0。final/domain.log・compile.log と各.status.json、project/Build/Reports/constructed-battle-reclamation-yard.json・static-api-compile.json、source-sha256.json・final-checks.json。177対象の現行SHA差分0、config/catalog/4mapコピー一致、Sceneを除外したdiff --check exit0。
6. YardOverview.png は実290volumeの配置/断面を描画し目視した確認図。Unityスクリーンショットではない。

## 未実行・Claudeの受入箇所

Unity実画面/Native Test Runner/Windows x64起動/FPS・GCは未実施。CLIはstatusが接続なし、pipeline listではEditor起動中/パッケージ有り/SafeModeなし/到達可能server0。CLI更新確認はネットワーク制限で失敗。Editor.logは読み取れた。権限変更や二重Editor起動はしていない。

- Play停止→Assets > Refresh→コンパイル完了→Play→参戦準備「マップ」から「再生工廠・三帯戦線」を選ぶ。旧Playable再生成不要。
- 射撃/HP回復FAで至近距離の対象に重なり、ConsoleにEmpty aim rayが再発せず、HUDが誤って緑にならないことを実機確認。
- 工房の入口/屋根、廃材の足場、中央から低地への落下、両側のランプ、上空背景と低地ラベル、M・出撃/再出撃・携行核の上下移動を確認。
- Nativeの既存PlayableFlowTestsは新4番目のマップも切り替えるよう更新。実Test Runnerで実行し、画面/操作/性能の結果のみ作業表へ追記する。

全ソース/文書書込み・GPT検証ジョブの完了後、WORK_BOARDへlock: releasedを記録する。他者のプロセス・Editor状態は変更していない。
