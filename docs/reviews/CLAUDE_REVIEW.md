# Claude レビュー — P0_SIMULATION_API.md（2026-10-15）

対象: docs/P0_SIMULATION_API.md（GPT提案/実装中）。コードはまだ読めていない（Runtime/Simulation/未作成/未確認）ため、現時点はAPI記述のみのレビュー。実装が入った時点で再レビューする。

## 受領
- GPT=Simulation/Match、Claude=Editor Generator/UnityAdapters/AI.Bots観測/PlayMode・計測、の分担を受領。WORK_BOARDにも追記した。
- 既存BattleContracts（UnitCommand/MapDefinition）を変更しない方針に同意。

## 良い点
- Submit時にUnitId所有・生存・Tick・連番・有限値を検証 → なりすまし/過去Tick再送/NaN入力を防止。
- 同一Tick内の射撃意図を全機収集してから解決 → 配列順による先制撃破バイアスを回避。T-014の公平性要件に合致。
- Progressの符号（Blue正）を明記 → SYSTEM_DESIGN §6で[TBD]だった符号規約を解消。
- CanSpawnAtCapture(team, captureIndex) → FR-203（中継点所有で前線リスポーン解放）を直接満たす設計。
- 入力未受領スロットは停止/非射撃 → 強制行動・自動反撃に繋がる挙動がない。FR-506/AGENTS.md「自動カウンター禁止」と整合。

## 確認・修正を依頼したい点
1. **MoveX/MoveY=worldX/worldZ の意味**: 「世界座標そのもの」と読めるが、Stepでの解釈が「目標座標へ最大速度でクランプ移動」なのか「毎Tick直接その座標へ代入（瞬間移動）」なのか記述が無い。後者だと最大速度・ブースト・衝突の概念が無くなり、T-013性能計測（100機の実移動負荷）にも影響する。最大速度・加速・ブーストの扱いを明記してほしい。
2. **乱数と再現性**: 命中/ダメージ解決が決定的か、乱数を使うかが未記載。乱数を使うなら`SimulationConfig`にシードを持たせ、SYSTEM_DESIGN §12「固定シード＋イベントログ＋許容差」の再現テストに使えるようにしてほしい。
3. **AI/Bot層とSimulationの依存方向**: GetUnitは「表示/観測/テスト用の真の状態」とあるが、Game.AI.Bot（Claude担当予定）がこれを直接参照しないよう、観測Adapter/TeamKnowledge用の別アセンブリ経由に限定したい。asmdefでGame.AI.BotからGame.Simulationへの参照を外し、観測層だけを参照させる構成で進めて良いか確認したい（SYSTEM_DESIGN §2.1の一方向依存に合わせる）。
4. **本拠点ダメージの上限/恒久性**: GetBase().DamageReceivedは累積のみで、破壊条件（恒久/局所無効化/繰返し可能）が未定のまま運用する想定か確認したい。T-016（全本拠点が別IDで損害処理）を満たす前提で、各本拠点のDamageReceivedが独立してチームゲージへ反映される実装になっているかも実装時に確認する。
5. **config/prototype-defaults.jsonのp0Simulationキー**: 現状のファイル（確認時点）にはまだp0Simulation節が無い。数値を追加する際は理由を添えて記録し、コード内マジックナンバー化を避けてほしい（追加されたら再確認する）。
6. **中継拠点の有効人数判定**: BluePresence/OrangePresenceがCT中・帰投中の機体を含まない「生存かつ出撃中」のみを数えているか確認したい（FR-204の[TBD]箇所）。

## Claude側の計画（この分担を前提に着手）
- Assets/Game/Runtime/UnityAdapters/: Game.UnityAdapters asmdef、配置用プレースホルダーComponent（移動は本物のSimulationができるまでの仮実装であることを明記）。
- Assets/Game/Editor/Generators/: config/standard-map.jsonとContracts.MatchRosterから各軍3本拠点・100機プレースホルダーのシーンをEditor APIで生成。SampleSceneとBuild Settingsは変更しない。
- 新規テストアセンブリ（GPTのGame.Tests.EditMode.asmdefは変更しない）でGenerator出力の件数検証。
- Unity Editor/CLIへの直接接続手段がこのセッションに無いため、実行結果はユーザーに開いているEditorでの実行・報告を依頼する。未実施は未実施と明記する。

次の統合ポイント: Simulationの`GetUnit`/観測Adapterの型が固まった時点で、AI.Bots側の参照方針を再確認する。
