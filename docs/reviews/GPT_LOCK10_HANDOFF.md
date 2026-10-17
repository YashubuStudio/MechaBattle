# LOCK-10 / SUPPORT-10 引渡し（GPT、2026-10-16）

owner: GPT / status: 実装・独立検証完了、Claude最終差分レビューと最新Unity受入待ち。正本MasterPlanは変更していない。

## 対応

- 最新ユーザー要件LOCK-10: 敵設置物も低優先度でロック。敵機が候補にあれば優先し、敵機候補がないとき設置物を選ぶ。中心優先と近い視界内の距離順切替を設定可能。
- SUPPORT-10: ポッド12→180秒（3分）、レーダー45→900秒（15分、20倍）。有限弾数・補給不可・所有者上限は維持。耐久はポッド1800/レーダー900/地雷450で攻撃による対処を追加。
- Claudeの耐久提案・設置物が無地キューブとの指摘を受領し、専用モデル表示・実射撃/爆風/格闘の破壊まで統合。核は未実装であり今回追加していない。
- Claudeが共有した武器調整要望を受領しSA125→138、チャージ最大750→880、ゲロビDP20→13、FA据え置きへ変更。実config両方に理由を記録。

## 操作・選択規則

メインメニュー/参戦準備/休止画面の「ロック方式」で中心優先または距離順切替を選ぶ。右クリックは取得/解除、Tabは取得/次候補。中心優先はカメラ軸からの角度→距離、距離順は距離→角度で一周する。等値は配列index/世代IDで安定決定する。候補は武器の黄範囲、画面内、地形LOSあり。敵機を設置物より優先するため、近い設置物が敵機ロックを奪わない。

設定はApplication.persistentDataPath/mechabattle-controls-v1.jsonへデッキと独立に保存。旧デッキを変更しない。persistProfile=falseのUnity試験では保存/読込とも行わない。赤範囲の明示取得時だけ一度吸着、以後自由照準/直進弾、ロック中散布0は設置物にも共通。

敵設置物には菱形/距離/種類/HPを表示し、現在ロック中の敵設置物だけ本人のマップへ表示する。敵レーダーの検知範囲は公開しない。敵機のチーム共有HP/解除後1.7秒は保持。設置物のチーム共有HP保持は今回追加していない。

## 契約・実装

| 対象パス（Assets/Game/以下） | 変更 |
|---|---|
| Runtime/Contracts/BattleContracts.cs・TargetLockTypes.cs | UnitCommand末尾のLockedDeployableIndex/Id、LockSelectionMode/LockTargetKind。敵機/設置物ロックは排他、既存呼出は省略可 |
| Runtime/Contracts/UnitLoadout.cs・Contracts/Construction/ConstructionContracts.cs・Runtime/Construction/ | utilityHealthの互換任意拡張とResolvedBuild往復。0は旧試験用の対象外、実3種は正の耐久 |
| Runtime/Simulation/TargetLockSelection.cs | 絞込済み候補だけを受ける純C#順位/巡回選択器 |
| Runtime/Simulation/BattleObjectTargets.cs・BattleDeployables.cs・SimulationSnapshots.cs | 設置ごとの新世代ID、HP/最大HP/形状、LOS/黄赤ロック、比例配分の実損害/単独破壊帰属 |
| Runtime/Simulation/BattleCombat.cs・BattleSupport.cs・BattleFeedback.cs・BattleSimulation.cs・CombatInputAdapter.cs | 直撃/飛翔/爆風/格闘、敵設置物による回復射線遮断、失効、ObjectHit/ObjectDestroyed |
| Runtime/Playable/PlayableTargetLock.cs・PlayableEntry.cs・PlayableUI.cs | 入力、設定保存/反映、即時失効、汎用菱形/HP/照準確度 |
| Runtime/Playable/MechaMeshLibrary.cs・CombatWorldView.cs・PlayableWorldView.cs | 既存専用モジュールの設置モデル生成、地面と論理幅/高さへ適合 |
| Runtime/Playable/PlayableMinimap.cs・BattleFeedbackView.cs・CombatImpactView.cs | 自分の敵設置物ロックの点表示・設置物破壊の独立通知 |
| Tests/EditMode/TargetLockSelectionTests.cs・ObjectTargetTests.cs | 新規28ケース |
| Tests/EditMode/Playable/PlayableFlowTests.cs・ModelTools/ModularMechaTests.cs | 設定ボタン反映/実設置メッシュ、寿命更新。Unity上は未実行 |

追加でMechaBattle/config/prototype-defaults.jsonとAssets/Game/Content/Playable/PrototypeConfig.json、scripts/DomainSmoke/ConstructionDefaultsSmoke.cs、操作/設計/構築/開発文書を更新。Observation/AI.Bots/UnityAdaptersは変更していない。100操縦枠に設置物を混入させない。Scene/Prefab YAML、UserText、Gitメタデータは直接変更していない。

配列indexと世代IDを照合し、破壊/期限/上限置換で失効する。同tickの旧設置物への保留損害を新設置物へ継承しない。直撃+爆風二重計上なし、機体と設置物を合わせ格闘対象数上限を守る。地雷は敵射撃で破壊すると誘爆しない。設置物破壊で機体撃破数やゲージを増減しない。

## 実行した検証

- 独立C# **253成功/0失敗**（既存225+新規28）。Unity Test Runnerではない。新規は敵機優先、中心/距離順、一周・同値順、遮蔽/範囲/味方除外、直撃/飛翔/爆風/格闘、破壊/期限/同tick再設置/世代失効、実HP量/単独破壊帰属、無誘爆を確認。
- 7preset、標準フレーム48単装備/6912三装備の評価/保存復元。重量フレームの全組合せ網羅とは扱わない。寿命とUtilityHealthの実カタログ/不変データ往復を確認。
- Unity API静的 **17アセンブリ成功**。JSON注入用フィールドの既存CS0649警告のみ。
- モデル源/GLB **18モジュール・7preset・148閉立体・5840三角形成功**。Unity描画の確認ではない。
- 実設定100BOT: 194.87999sim秒で決着、29撃破/128出撃/112コア命中/23反撃/9占領変化。両軍最低46/48機、平均99.56機。コア主目的・資源/CTの回帰成功。長寿命による補給継続がBOT戦時間へも影響するため、試遊時に前線の感触を確認する。
- meanFirstDamageToDeath=61.19秒は回復/退避を含み連続射撃TTKではない。centralBattlefieldFraction=0.8619は|X|<110での中央座標比で交戦場所の比率ではない。生コア損害50160はゲージclamp前の累積。FPS/GCの証拠ではない。
- 検証後の57ドメインソース更新なし、実config/Playableコピー/隔離テスト設定一致、実マップコピー一致。112対象ファイルのSHA256 manifestを保存。
- diff --check成功（既存生成Scene YAMLを除外）。新規.csの.metaは監査時点で全て存在するが、最終Editorインポート成功の証拠とは扱わない。

機械可読証跡: [GPT_LOCK10_VALIDATION.json](GPT_LOCK10_VALIDATION.json)。隔離実行先: MechaBattle/Build/LOCK10_1792124053677/。domain-run.log、Build/Reports/*.json、compile.rsp、source-manifest.json、final-check-0〜2.logを保存。実57ソースを.NET9 Roslyn /optimize+で再コンパイルし、実JSONをコピーして同一DomainSmokeランナーを実行した。標準verify-domain.ps1も同じ試験を発見する。

## 未実行とUnity受入手順

Unity Test Runner、最新Play Mode操作/描画、Windowsビルド起動、100機FPS/GCは未実施。ユーザーの旧動作確認を今回の合格へ流用しない。CLIは別OSユーザーの検出ファイルへのアクセス拒否が続いており、権限変更/別Editor起動で迂回していない。

1. Play停止、開いたシーン保存、Assets > Refresh、コンパイル完了。MechaBattle > Playable > Generate and Open Playable Sceneで再生成してPlay。
2. メイン/参戦準備/休止で方式を変え表示が一致することを確認。再起動で選択が残り、デッキ保存が維持されることを確認。
3. 近い敵設置物と中心付近の敵機で敵機を取得すること、距離順でTab一周、中心優先で照準に近い敵機を取得することを確認。
4. 敵機候補のいない場所で敵ポッド/レーダー/地雷をロックし、菱形の種類/HP/距離、赤取得時だけ吸着、自由照準、本人マップ点を確認。
5. 射撃/爆風/格闘で破壊し、破壊通知・表示終了・ロック解除を確認。同種再設置へ古いロックが引き継がれないことを確認。
6. ポッド3分/レーダー15分、範囲/補給不可/上限、レーダー再設置/期限/Canvas描画の赤エラーなしを確認。Game.Tests.PlayableとModelToolsを実行。

## Claudeとの連携

前回SUPPORT-08/RADAR-09/BOOST-09/MELEE-09の独立225/225・17アセンブリ・差分承認をWORK_BOARDで受領済み。今回は寿命/優先度/ID/損害/設定UIの完成差分の読み取りレビューを同表で依頼する。LOCK-10の新しい承認は現時点未受領。直接のメッセージ接続はなく、相手の受領・実行結果を捏造しない。

全MasterPlan/P0性能受入の完了宣言ではない。
