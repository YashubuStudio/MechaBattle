# TeamKnowledge 設計（非全知の観測集計、P4指揮官AIの土台）

Claude作成。SYSTEM_DESIGN.md §10.4（TeamKnowledge）とFR-705〜707を正本とし、現在のSimulation実装（GPTのLOCK-10/SUPPORT-10）が既に提供しているデータとの対応を整理した設計案。コードではなく設計のみ。対象範囲はClaude所有領域（AI.Commander想定の新規アセンブリ）に閉じ、Observation/AI.Bots/ForwardMarch/UnityAdaptersには触れない。

## 1. 正本の要求（再掲）

- `WorldTruth`への生データアクセスをAIの意思決定層に与えない（§10.4）。
- 知識更新源: 視界、レーダー、被弾方向、BOTの報告、拠点攻撃イベント（検出可能範囲に制限）（§10.4）。
- 目撃から時間が経つと鮮度を減衰させる（§10.4）。
- 敵位置は索敵・観測結果から認識する。全知情報を意思決定に直接使用しない（FR-705）。
- 視界、レーダー、通信遅延、情報鮮度、認知誤差をモデル化（FR-706、提案扱い）。
- AIは集団で完璧に同時反応しない。索敵・経路・意思決定には自然な制約を設ける（FR-707）。

## 2. 現状把握: 正本の要求のうち、既にSimulationが提供しているもの

LOCK-10/SUPPORT-10（GPT実装、Claude承認済み）で、当初の想定より多くの部分が既に用意されていることを確認した。

| 正本の要求 | 対応する既存API | 備考 |
|---|---|---|
| 視界（明示ロックによる観測） | `BattleSimulation.GetTeamContact(team, slot)` の `HasHealth`/`LockDetected` | 黄範囲ロック時にHP/位置をチーム全体へ共有、解除後`sharedLockHealthSeconds`（1.7秒）で自動失効 |
| レーダー | 同上の `RadarDetected` | 位置のみ共有、HPは含まない設計（設置物のレーダーの検知範囲） |
| 鮮度減衰 | `TeamContactSnapshot.AgeSeconds`/`HealthAgeSeconds` | 経過秒数が直接取得できる、既に「目撃から時間が経つと鮮度が減衰する」を満たす形 |
| 拠点攻撃イベント | `BattleSimulation.TryGetCombatEvent(sequence, ...)` の `CombatEventKind.CoreHit` | 有界リング（2048件）のイベントストリーム、攻撃元チーム/位置/tick付き |
| 検出可能範囲への制限 | 両APIとも黄範囲/LOS/レーダー射程で絞り込み済み。生座標の無制限公開なし | AI.Botsへの真値非流入はClaudeが独立検証済み（WORK_BOARD記録済み） |

**この発見の意味**: TeamKnowledgeを一から設計する必要はなく、既存の`GetTeamContact`/`TryGetCombatEvent`を**集計するだけの読み取り専用レイヤー**として実装できる。Simulation側への追加要求はほぼ不要。

## 3. まだ無いもの

- **BOTの報告**: 現在BOT自身の近傍索敵（`INearbyThreatSensor`、各BOTの武器射程内のみ）は、そのBOT自身の意思決定にしか使われず、チーム全体の共有プールへは還元されていない。正本が言う「BOTの報告」に相当する経路が未実装。
- **通信遅延のモデル化**（FR-706、提案扱い）: 現状のチーム共有は「同tickで即時共有」。遅延を入れるかどうかは設計判断。
- **指揮官向けの要約**: 個々の契約情報（どの敵がどこにいるか）を、指揮官AIが使う「前線の優勢/劣勢」「どの本拠点が脅威に晒されているか」といった戦略レベルの要約へ変換する層がまだ無い。

## 4. 提案する設計（新規・追加のみ、既存コードは不変更）

新規アセンブリ `Game.AI.Commander.Contracts`（noEngineReferences、既存の`Game.Observation.Contracts`と同じ思想）を想定。

### 4.1 `TeamKnowledgeSource`契約（読み取り専用の集約インターフェース）
```csharp
public interface ITeamKnowledgeSource
{
    IReadOnlyList<EnemyContactKnowledge> EnemyContacts(int team); // GetTeamContactの全スロット分の集計
    IReadOnlyList<BaseAttackKnowledge> RecentBaseAttacks(int team, float withinSeconds); // CoreHitイベントの直近窓
}
```
実装（UnityAdapters側、Claude所有）は`BattleSimulationRunner`/`PlayableEntry`が既に持つ`Simulation`参照から`GetTeamContact`/`TryGetCombatEvent`を呼ぶだけで、Simulation本体には触れない。

### 4.2 鮮度・認知誤差のモデル化
`TeamContactSnapshot.AgeSeconds`をそのまま「確信度の減衰」として使う（例: `confidence = max(0, 1 - age/staleWindow)`）。FR-706の「認知誤差」は、確信度が低い観測ほど指揮官AIの判断材料としての重みを下げる形で表現する（座標をわざと誤差させる必要はなく、重み付けだけで十分という設計判断）。

### 4.3 戦略要約（指揮官AI向け）
`EnemyContacts`/`RecentBaseAttacks`を集計し、以下のような要約構造体を生成する想定:
```csharp
public readonly struct FrontSummary
{
    public readonly string BaseId; public readonly int RecentAttackCount; public readonly float LastAttackAgeSeconds;
    public readonly int KnownEnemyContactsNearby;
}
```
これをSYSTEM_DESIGN §10.3の指揮官行動（HoldFront/AttackRelay/BaseRushCountdown/GuardBase等）の入力として使う想定。指揮官AI自体の実装は本設計のスコープ外（次の段階）。

### 4.4 「集団で完璧に同時反応しない」の実現（FR-707）
TeamKnowledgeの集計自体は決定論的でよいが、これを読む側（小隊AI/指揮官AI）の意思決定更新頻度を意図的に下げる（MasterPlanの`botDecisionHz`/`squadDecisionHz`/`commanderDecisionHz`の階層構造、既にconfigに存在）ことで実現する。TeamKnowledge自体に遅延を仕込むのではなく、読み取り側の更新頻度で表現する設計とする。

## 5. 実装順序案（本設計のスコープ外、次段階の参考）

1. `Game.AI.Commander.Contracts`アセンブリ新設、`ITeamKnowledgeSource`等の型定義（Claude、純C#、低リスク）。
2. `BattleSimulationRunner`/`PlayableEntry`側に実装アダプタを追加（Claude/GPT分担要相談、UnityAdapters側はClaude所有）。
3. 小隊AI（`AI/Squads/`）がまず`ITeamKnowledgeSource`を使わない最小実装（グルーピングのみ）から着手し、後続でTeamKnowledgeを取り込む。
4. 指揮官AI（`AI/Commander/`）は本設計のFrontSummary等を消費する形で実装。

## 6. まとめ

正本が求めるTeamKnowledgeの「データ収集」部分は、LOCK-10/SUPPORT-10のチーム共有ロック/レーダー/戦闘イベントストリームによって**既にほぼ満たされている**。残っている作業は、これらを指揮官AI向けに読み取り・集計する薄いレイヤーの追加のみであり、Simulation本体への新規要求はほぼ無い。この発見により、P4指揮官AIの着手コストは当初の想定より低いと考えられる。
