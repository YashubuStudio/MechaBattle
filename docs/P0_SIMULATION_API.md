# P0 Simulation API — GPT提案 / 実装中

既存ContractsのUnitCommandとMapDefinitionは維持する。namespace MechaBattle.Simulation、assembly Game.Simulation（Unity非依存）。

- new BattleSimulation(MapDefinition map, SimulationConfig config, int humanSlotId = 0)
- CurrentTick（初期0）、FixedDeltaTime、Phase（Running/Finished）、WinnerTeam（引分=-1）、AliveCount
- GetUnit(int slotId): UnitSnapshot。SlotId/UnitId/Team/Controller/IsAlive/X/Z/Health/Shield/Ammo/DeckIndexをコピーして返す。
- GetBase(int index): MainBaseSnapshot。Id/Team/X/Z/AttackRadius/DamageReceived。
- GetCapturePoint(int index): CaptureSnapshot。Id/X/Z/Radius/OwnerTeam（中立=-1）/Progress（Blue正）/BluePresence/OrangePresence。
- GetGauge(int team)、GetDeckReadyTick(int slot,int deck)、CanSpawnAtCapture(int team,int captureIndex)
- Submit(int slotId, UnitCommand command): bool。所有UnitId・生存・当該CurrentTick・連番・有限入力を検証。
- Step(): 固定Tickを1回進める。先に全入力をSubmitする。入力未受領のスロットは停止/非射撃。
- Command.MoveX/MoveYはワールドX/Z軸への入力値（各[-1,1]、ベクトル長最大1に正規化）。位置座標ではなく、moveSpeed×FixedDeltaTimeで積分する。AimYawは度、+Zが0度、+Xが90度。FirePrimaryのみP0射撃。その他の操作はP1向け予約。

## 公開状態とAI境界
GetUnitは表示/観測/テスト用の真の状態であり、AIへBattleSimulationそのものを渡さない。観測Adapterが限定した読取ビューを構築し、BOTへは自機・観測敵・公開目標だけを渡す。
未観測敵をAIが選択しないことはClaude側の観測/BOTテストで検証する。ドメインの命中判定が真の位置を使うこととは分離する。

## 試作ルール
50Hz、100機を同時出撃。デッキ4枠、機体ごとのCT/弾薬/HP/Shield、通常射撃、撃破、再出撃、中継人数差、本拠点損害/ゲージ、時間切れを軽量な平面モデルで接続する。
各数値はconfig/prototype-defaults.jsonのp0Simulationに仮値と理由を記載する。
NavMesh/地形遮蔽/物理Collider/3D移動/高度な武器/戦略核/小隊/指揮判断はこの変更には含めない。
射撃意図は全機分を集めてから損害を解決し、同一Tick内の配列順による先制撃破を避ける。

実装が固まった時点でこの文書を更新してClaudeへ再レビューを依頼する。

## GPT / Claude レビュー反映
CLAUDE_REVIEW.mdの6点へGPT_RESPONSE_TO_CLAUDE.mdで回答済み。ドメイン乱数なし、本拠点は繰返し損害受付。新規Game.Observation.ContractsをClaude担当で追加し、AIからSimulationへの参照を禁止する方針に合意。
