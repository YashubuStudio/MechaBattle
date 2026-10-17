# LINE-05 / UX-05 / MOTION-05 / AIM-06 / LOCK-07 引渡し

owner GPT / 2026-10-16 / status 実装・独立検証完了、最新実機受入とClaude最終レビュー待ち。

## 反映した動作

- Spaceジャンプ/Shiftダッシュ。標準HP2400/盾900、補強腕1本3000・2本3600HP。通常格闘950、D格1500、最大チャージ2600。射撃6種は火力/射程/弾速/装填を個別調整。
- 物理射程>黄>赤。非ロック小拡散、有効ロックは散布0の手動方向への直進弾。赤で手動取得した瞬間だけ吸着、以後は自由照準。継続誘導/カメラ追従なし。円+十字、静止相手への命中確度の円色、胴体菱形/距離。
- 装備は一覧から直接選択し、選択中の軍/機体/装備を強調。武器別情報とHP/盾/ブースト/DP、被弾/低HP/行動阻害を表示。元メッシュを股/膝で分割した歩行/横移動/ダッシュ/ジャンプ/着地/射撃反動の簡易モーション。
- 地形は83個の共通Box。中央高台/ランプ、左右通路、外壁/最終進入の遮蔽と幅6mの歩行通行口。主力44は中央、一軍4強襲は外側±112mを迂回、2占領役は内側±80m。BOTのみ役割に合う自軍本拠点から出発し、人間の出撃先は変えない。
- 被ロックは人数+警告音。BOTも被弾前に短い左右回避/共通ブーストで反応する。ロック保持者の攻撃でHP/盾へ実害が出たら攻撃/自分のロックをやめて退避を継続。既知地形の遮蔽で有限ポッド、または通常の自軍本拠点補給で回復。HP/盾100%かつ2.5秒無被弾で復帰し、無害なロックだけで永久拘束しない。人間は警告のみで自動移動なし。
- 本拠点補給はPlayableだけHP400/盾180毎秒へ耐久値に合わせた。コア換算係数0.5、ゲージ30000、撃破係数0.03。通常コア格闘1hit475ゲージ、撃破約11なので本拠点主目的は維持し、最初の強襲だけで試合が終わる状態を緩和。全て理由付き仮値。

## 共有型と判断の境界

UnitLoadout/WeaponDefinitionに黄射程/散布、ProjectileSnapshotに描画用方向を追加。UnitSnapshotのIncomingLockCount/LastDamageTick/LastLockedHitTickは自己警告専用で、攻撃者のID/位置/方角を含めない。有効な敵の明示ロックを黄射程/LOS/出撃IDで確認し、解除/遮蔽/撃破/再出撃で残さない。

飛翔弾の所有スロット/出撃IDは内部判定専用。実際に命中させたロック保持者へ被弾を結び付け、第三者/拠点反撃/解除後の着弾/古い出撃の弾を誤認しない。BOTへの隠れた敵情報は追加していない。

UnitCommand末尾の任意CancelHeldAttackは退避によるチャージ誤解放を防ぎ、CombatInputAdapterでも保持。溜め/照射を共通処理で取消し、開始済み格闘の硬直は消さない。BOTの偏差は連続可視履歴の65%予測で、遠距離の横移動を完全迎撃しない。人間/BOTは共通の弾速/当たり判定/資源/CT/補給を使う。50vs50・別枠指揮官2・各軍3本拠点・人間自由・全知禁止・自動カウンター禁止は維持。

主な対象: Runtime/Simulation（BattleSimulation/BattleCombat/BotTraversalAdapter/BotRecovery）、Runtime/Playable（Entry/UI/CombatWorldView/LocomotionWorldView/ReticleRingGraphic）、Contracts/Construction、config/Content JSON、Tests/EditMode（LineCombat/ManualLock/LockWarning/PlayableFlow）、scripts/DomainSmoke。Claude所有ForwardMarch/Observation/UnityAdaptersはGPTから変更なし。

## 実行した検証

- 純粋C# **186成功/0失敗**。旧149を保持し、新規37件は警告17/前線・移動14/手動照準6。実カタログ40単装備/4000三装備保存復元を含む。
- Unity API静的照合 **全17アセンブリ成功**。Json復元用フィールド等のCS0649警告は残るがコンパイルエラー0。
- モデル15部品/6プリセット/97閉立体/3804三角形の形状検証成功。実Unity表示の検証とは区別。
- ソース/資料のgit diff --check成功（既存生成Sceneを除く）。設定/マップのContentコピー一致、検査対象83ファイルは検証後差分0。
- 実設定100BOT: **367.72sim秒**で橙勝利。57撃破/157出撃、コア命中117、建屋反撃39、占領変化9。平均生存99.53、最低青47/橙47。最終ゲージ青0/橙769.15。資源/装備弾薬/高さ/CT制約を全tick検査。
- 平均生存108.36秒、初被害→撃破92.69秒は回復/退避を含み連続被弾TTKではない。中央比率84.51%は|X|<110内の生存位置サンプルのうち|Z|<54の比率で、実交戦比率ではない。コア換算損害58600/撃破損害650.49は両軍合計のクランプ前集計。

機械可読結果は[GPT_LINE05_AIM06_LOCK07_VALIDATION.json](GPT_LINE05_AIM06_LOCK07_VALIDATION.json)。全結果/診断/コンパイル応答/ソースハッシュはMechaBattle/Build/LOCK07Final_1792106511347/。共有DLL/出力との競合を避け、実ソースを.NET9.0.312で独立コンパイル（optimize+）、実設定/マップのコピーで同じハーネスを実行した。Unity Test Runner/Unity描画FPSの代わりではない。

## 解消した問題と訂正

初回100BOTは600秒間コア命中0で未合格。試験の条件を緩和せず、登れる段差の誤迂回、ジャンプ保持後の再押下欠落、被弾元の誤認、隣接中央レーンからの捕捉、旧HP尺度の遅い補給、初期通路配置の非対称を修正した。侵入解消直後は38.86秒で拠点が尽きたためコア換算を調整し、最終367.72秒まで再検証した。

建屋20mマップはCORE-01以来BuildingRadiusが実反撃範囲を決める。baseCounterFireRadius=70を実範囲とした先の説明は誤り。フォールバック20への統一は現在マップの実範囲を変えない。反撃1680/450DP・5秒は維持。

## Claudeへ

FIELD〜AIMのレビュー結果とbaseHits=0の具体的診断を実際に受領し、上記の修正/最終検証で解消を確認した。最新のBotRecovery/ロック被弾元/CancelHeldAttack/回避・偏差/役割出撃/83Boxの進入路、および未レビューのModelTools/Json往復の差分レビューをお願いします。所有ファイルの同時編集を避け、指摘を作業表/reviewsへ先に共有してください。最新差分のClaude受領/承認は未確認。

## 最新実機で残る受入

Play停止→Assets > Refresh（新規.meta生成/コンパイル完了）→MechaBattle > Playable > Generate and Open Playable Scene→Play。最新確認時はBotRecovery.cs/LockWarningTests.csの.metaが未生成で、Editor.logに新しいコンパイルエラーは無いが、最終版の実インポート完了とは扱わない。

一覧選択、Space/Shift、円色/菱形/自由照準、被ロック警告音と赤警告、BOTの回避→退避→回復復帰、中央/側面/段差・通行口、本拠点侵入、結果/再戦を実機確認する。ユーザー確認済みはFIELD-04基本動作まで。今回の見栄え/操作感、Unity Test Runner、Windowsビルド/起動、FPS/GCは未実施。描画部品/弾の視認性を増やした負荷は実測が必要。

地雷の射撃破壊、全身アニメーション、小隊/指揮官AI・金功績・核・オンライン等は別途未完了。全MasterPlanやP0全受入の完成とは報告しない。UserText変更、Scene/Prefab/ScriptableObject YAML直接編集、他者変更の取り消しはしていない。
