# マップバリエーション: forward-center-skirmish (2026-10-16)

## 依頼内容

> マップのバリエーションを増やしてください。戦場をどこにするかを考えた上で、その部分だけ拠点を前に、それ以外の部分は後ろにすることで、戦上における偏りを作れます。なお、マップ自体は縦横各3倍くらいを基準にもっと広くしてください。

## 設計方針

`config/standard-map.json`（`p0-three-lanes`, halfWidth=240 / halfDepth=140）はそのまま残し、**追加の**マップとして `config/forward-center-map.json`（id: `forward-center-skirmish`）を新規作成した。既存マップの置き換えではない。

- **全体スケール**: halfWidth 240→720、halfDepth 140→420（約3倍）。
- **意図的な偏り**: 「戦場」として中央レーン（Blue-B/Orange-B, approachRoute=`center`）を選び、拠点をマップ中心寄り（x=±300、拠点間距離600）に前進配置。北/南レーン（`north`/`south`）の拠点は後方（x=±650、拠点間距離1300）に配置。中央レーンは短距離・即時交戦向き、南北レーンは長距離・持久戦向きになり、3レーンが均等ではなく意図的に非対称になる。
- **拠点/占領半径はスケールしない**: `spawnRadius`/`attackRadius`/`buildingRadius`/占領`radius`は`standard-map.json`と同値（12/8/20/18）のまま。武器射程・移動速度はマップサイズに依存しない絶対値のため、マップが3倍になっても拠点周りのユニット間インタラクション距離を変える理由がない。
- **障害物は未着手**: 新マップは`obstacles: []`（障害物なし）でスタート。FIELD-04で`standard-map.json`に追加された約83個の障害物を3倍サイズのマップへ手作業で再配置するのは今回の依頼範囲を超えるため、別タスクとして後回し。

## 検証

- `Assets/Game/Runtime/Battlefield/MapDefinition.cs`/`MapValidator`はスキーマ・検証ロジックが完全に汎用的（マップサイズや拠点数の上限をハードコードしていない）ため、**C#コード変更ゼロ**で新マップJSONのみ追加。
- 新規 `scripts/DomainSmoke/MapVariationSmoke.cs`（`verify-domain.ps1`経由、Unity不要）が検証する内容:
  1. `MapValidator.ThrowIfInvalid` — FR-108 (チーム毎最低拠点数)、FR-110 (チーム毎に異なる進入路) を含む既存の全検証ルールを通過。
  2. `halfWidth`/`halfDepth`が`standard-map.json`の2.5~3.5倍の範囲内。
  3. 中央レーン拠点の|x|が南北レーン拠点の|x|より150以上小さい（=意図的な偏りが実際にデータへ反映されている）。
  4. 実際に`BattleSimulation`+`ForwardMarchBotCommandSource`で100スロットBOT戦を最後まで実行し、クラッシュや無効な状態（NaN座標、マップ範囲外、負のHP等）が出ないことを確認。
- 結果: `verify-domain.ps1` 294/294 成功（新規テスト含む）。レポートは`Build/Reports/map-variation-forward-center.json`に出力（この回はpeakAlive=100、拠点到達により片方のゲージが0まで減少し正常終了、センターレーン|x|=300 vs 南北|x|=650）。
- `verify-compile.ps1`（Unity実エンジン参照の静的コンパイル）も実行済み、既存18アセンブリへの影響なし（既存の警告のみ、エラーなし）。

## まだやっていないこと（引き続きの課題）

- **マップ選択への接続**: `Assets/Game/Content/Playable/PrototypeConfig.json`の`standardMap`キーや`BattlefieldSceneGenerator`/`PlayableSceneBuilder`は今も`config/standard-map.json`を直接参照している。このマップを実際に選べるようにするには、マップ選択の仕組み（複数マップから選ぶUI/設定）をGPT所有のPlayable/Construction領域へ提案する必要がある。現状は「Editorのマップファイル参照をこのJSONに差し替えれば試せる」段階。
- **障害物（カバー地形）の追加**: 現状フラットな非対称マップ。FIELD-04相当の地形パスは別タスク。
- **実機プレイテスト**: バランス面（中央レーンが実際に"熱い"戦場として機能するか等）はUnity Editor/実プレイでの確認が必要（Claudeはこのセッションでは未実施）。
