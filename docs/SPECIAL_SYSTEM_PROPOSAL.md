# 機体専用サブシステム（特殊システム）提案

ユーザー要求: 「機体に対しての専用サブシステム等（イメージはトランザムやALICEなどの試合中におけるモーション変更パーツ）を追加できるようにしてください。」

本資料はClaudeによる設計提案。対象ファイルはGame.Contracts.Construction/Game.Construction/Game.Simulation/Game.Playable（いずれもGPT所有・現在FIELD-04ハンドオフ直後）のため、Claudeは直接実装せず仕様としてGPTへ提案する。実装はGPT、BOTの発動判断（いつ使うか）はClaude(AI.Bots)が担当する分担を想定。

## 1. コンセプト

「トランザム」「ALICE」はあくまでイメージ参照（IP要素は使わない）。システムの振る舞いとしては以下2系統に一般化する:

- **オーバードライブ型**（トランザム的）: 発動中、速度/ブースト/射撃間隔/格闘等のステータスを一時的に強化。発動には溜め時間またはコスト（専用ゲージ消費）があり、終了後はクールダウンまたはデバフ（速度低下等）が入る。
- **支援AI型**（ALICE的）: 発動中、パイロット支援的な効果（よろけ無効化、自動装填、赤ロック角度拡大等）を付与。**既存の索敵範囲（`INearbyThreatSensor`の射程制限）を超える追加の敵情報は一切与えない**（AGENTS.mdの全知AI禁止に抵触しないための明確な制約）。

どちらも「機体に恒常的に付く能力」ではなく、「試合中に発動し、時間制限とコストを伴う」点が共通。既存のブースト/格闘と同じ「溜め→発動→クールダウン」の型を使うため、GPTの既存実装パターンを素直に拡張できる。

## 2. データモデル（Contracts.Construction拡張案）

`PartSlot`に新スロットを追加（既存スロットには影響しない、追加のみ）:

```csharp
public enum PartSlot { LeftArm = 0, RightArm = 1, PrimaryWeapon = 2, SecondaryWeapon = 3, TertiaryWeapon = 4, SpecialSystem = 5 }
```

新規`SpecialSystemDefinition`（`PartDefinition`に`specialSystem`として追加。既存の`weapon`フィールドと同じ考え方で、`slot == SpecialSystem`のときだけ使う）:

```csharp
public enum SpecialSystemCategory { Overdrive, AutoSupport }

[Serializable]
public sealed class SpecialSystemDefinition
{
    public SpecialSystemCategory category;
    public float chargeSeconds;      // 発動までの溜め（0なら即時発動）
    public float durationSeconds;    // 効果時間
    public float cooldownSeconds;    // 終了後、再発動不可の時間
    public float activationCost;     // 発動に必要な専用ゲージ量（0ならゲージ無し＝CTのみで制御）

    // オーバードライブ型（1.0=無変化、全て既定1）
    public float speedMultiplier = 1, boostMultiplier = 1, fireRateMultiplier = 1,
        damageMultiplier = 1, meleeSpeedMultiplier = 1;

    // 支援AI型（既定false/0=無変化）
    public bool staggerImmune, knockdownImmune, autoReload;
    public float redLockAngleBonusDegrees;

    public string visualTag = ""; // 発動時の視覚演出タグ（任意、見た目はGPT/ArtSource側で決定）
}
```

`PartDefinition`への追加は1フィールドのみ:
```csharp
public SpecialSystemDefinition specialSystem; // null = 通常パーツ（既存パーツは無変更で動く）
```

**既存データへの影響はゼロ**: 既存の`parts[]`はこのフィールドを持たないため`null`のままで、既存プリセット/保存デッキはそのまま動く（確立済みの"追加はオプション、既定値で無効"という本プロジェクトの慣習と同型）。

## 3. 実行時の状態機械（Simulation拡張案）

既存の`UnitCommand.UseAbility`フラグ（現在未配線）を使う。ブースト/格闘と同じ形の状態機械:

```
Ready --(UseAbility入力 かつ ゲージ/CT充足)--> Charging --(chargeSeconds経過)--> Active
Active --(durationSeconds経過)--> Cooldown --(cooldownSeconds経過)--> Ready
```

- `Active`中は`speedMultiplier`等を既存の移動/射撃/格闘計算へ乗算するだけ（既存の`boostSpeedMultiplier`と同じ適用点に追加するだけで済むはず）。
- `staggerImmune`/`knockdownImmune`は既存のDP処理（`ApplyDownPoints`相当）にガードを1本追加。
- `autoReload`は既存の装填処理のトリガー条件を緩めるだけ。
- `redLockAngleBonusDegrees`は既存の赤ロック判定角度に加算するだけ。
- 新たな敵情報取得・新たなセンサー拡張は**行わない**（支援AI型の制約）。
- 発動中は格闘/射撃/ブーストの既存の相互排他ルール（予備動作中は射撃不可、等）をそのまま踏襲し、新しい例外は作らない。

撃破・CT・コストへの影響: `SpecialSystemDefinition`を積んだ構成は既存のコスト式（`costPolicy`）にそのまま載せられる想定（例: `activationCost`や`durationSeconds`に応じた追加コスト項を`CostPolicy`へ1〜2個追加する程度で済む）。

## 4. BOTの発動判断（Claude担当範囲、AI.Bots）

`ForwardMarchBotCommandSource`に、`UseAbility`を立てる判断ロジックを追加できる（Simulation側の状態機械が実装された後）。例:
- オーバードライブ型: 弾切れで帰還中以外、交戦中かつ自機の残HPが一定以上のときに発動。
- 支援AI型: 被弾が続いている（よろけ中/CTが近い）ときに発動。

いずれも既存の`SelfObservation`/`INearbyThreatSensor`の範囲内で判断でき、新しい契約追加は不要な見込み（Health情報が必要になる可能性はあるため、その場合は別途`SelfObservation`への追加を調整する）。

## 5. AGENTS.md整合性チェック

- 自動反撃禁止: 発動は`UnitCommand.UseAbility`という明示的な入力であり、被弾への自動反応ではない。問題なし。
- 全知AI禁止: 支援AI型の効果は自己バフ（よろけ無効・装填・ロック角度）のみで、追加の敵位置情報を一切与えない設計に限定。問題なし。
- 人間/BOT同一ルール: 構築カタログのパーツとして人間・BOT共通で扱われ、特定BOTだけの特権にはしない。問題なし。
- コスト/CT: 既存のコスト式に載せる想定で、専用の無制限パワーアップにはしない。

## 6. MSGO準拠方針との関係（確信度の明記）

機動戦士ガンダムオンラインに「トランザム/ALICE相当の一時強化システムがあったか」の正確な仕様は確信度が低く、断定しない。本提案は「一般的なロボット対戦ゲームにある一時強化ギミック」として設計したものであり、MSGOの特定システムの再現を主張するものではない。IPに触れる名称（トランザム/ALICE等）は実装・コード・UI文言には使わない。

## 7. 実装順序の提案

1. GPTがContracts.Constructionへ`PartSlot.SpecialSystem`/`SpecialSystemDefinition`を追加（既存データ無変更、追加のみなので低リスク）。
2. GPTがSimulation側に状態機械を追加（Boost/Meleeと同型のパターンを再利用）。
3. GPTがPlayableUIにゲージ/発動ボタンを追加。
4. Claudeが`ForwardMarchBotCommandSource`へBOTの発動判断を追加（Simulation側が固まった後）。
5. 両者でバランス調整用のサンプルパーツ（オーバードライブ型1種、支援AI型1種）を`prototype-defaults.json`の`constructionPrototype.catalog.parts`へ追加。

GPTの判断で実装順序・詳細仕様を調整してよい。これはClaudeからの提案であり、確定仕様ではない。
