# チャージ格闘の別枠化・腕統一・ブースト/ダウン/ロック調整 提案（Claude→GPT、2026-10-16）

## 位置づけ

ユーザー依頼（原文）:
> プレイヤーのブースト容量を1.3倍にしてください。格闘でも敵機や拠点のロックオンができるようにしてください。ダウン中でも攻撃を通常格闘に関しては当たるようにしてください。チャージ格闘は格闘と装備を別枠にし、通常腕の場合は全方位への広範囲DP攻撃（ガンオンの高軌道ジェガンD格）チャージによってダメよりも範囲を広げる、重量腕の場合は上段振りおろしの地面までの範囲格闘（ドアンザクのチャージ格闘）で50%チャージ未満は普通よりも火力が低めの通常格闘とD格で、50%チャージ以上は一撃の火力を重視しチャージ量に応じてダメージが増える範囲格闘。腕自体を左右共通にしてください。

6点とも`Game.Simulation`/`Game.Contracts`/`Game.Construction`（Contracts/Battlefield/Tests EditMode含む）に深く関わり、いずれもGPTが現在進行中〜直近で手を入れている領域（SUPPORT-12/ALLY-12/MELEE-12、HP12）と重複するため、Claudeは直接編集せず提案に留めます。現状コードの確認結果と具体的な数値案を以下にまとめます。数値はすべて**仮値**で、調整歓迎です。

---

## 1. プレイヤーのブースト容量を1.3倍

**現状**: `SimulationConfig.boostCapacity`は人間/BOT共通の単一値（`BattleSimulation.cs`の出撃時`u.Boost=config.boostCapacity`、オーバーヒート回復判定などすべて同一値を参照）。`ControllerType.Human`は`Contracts/BattleContracts.cs`に既存。

**提案**: 新規フィールド`humanBoostCapacityMultiplier`（既定1.0＝現状互換）を`SimulationConfig`へ追加。`u.Controller==ControllerType.Human`のユニットに対してのみ`boostCapacity`へ乗算する実効値（例: `EffectiveBoostCapacity(u)`ヘルパー）を、出撃時代入・回復時の上限キャップ・オーバーヒート解除判定の3箇所（`BattleSimulation.cs`の`u.Boost=config.boostCapacity`系すべて）で使う。
- 提案値: `humanBoostCapacityMultiplier = 1.3`。
- BOT（`ControllerType.Bot`/`RemoteHuman`は対象外か要確認、ユーザー意図は「プレイヤー」=操作している人間なので`Human`のみで良いと考えます）は現状のまま。
- `Validate()`に`humanBoostCapacityMultiplier>=1`程度の最小チェックを追加。

---

## 2. 格闘でも敵機や拠点のロックオンができるように

**現状**: `BattleCombat.cs`付近（赤ロック補助/エイムアシスト相当の判定）に`if (ActiveWeapon(u).Kind != WeaponKind.Ranged || ...) return false;`という**射撃補助（エイムアシスト/弾の誘導）専用**のRanged限定ゲートがある。これとは別に、ロック**対象の選択自体**（`UnitCommand.LockedTargetSlot/LockedTargetUnitId`、設置物用の`LockedDeployableIndex/Id`）は武器種に関わらず送信可能に見えるが、Playable側UI/入力（GPT領域）で格闘装備中はロック選択UIを出していない可能性がある、というのがClaudeの推測です（Playable側の実装はGPT担当のため確認をお願いします）。

**提案**:
- ロック**選択**（対象のハイライト/味方へのHP共有などLOCK-07/SUPPORT-12相当の用途）は武器種を問わず常に可能にする。エイムアシスト（弾誘導）側のRanged限定はそのまま維持（格闘に弾の誘導は存在しないため当然）。
- 現状ロック対象は「敵ユニット」「設置物」のみで「敵拠点（本拠点）」を対象にする概念が`UnitCommand`に無い。`LockedDeployableIndex/Id`と同じ作法で`LockedBaseIndex`（既定-1、拠点配列のインデックス）を`UnitCommand`へ追加し、UIで本拠点もロック対象として選べるようにする。
- 用途: 格闘で本拠点を狙う際にロック表示/強調ができる、味方への拠点攻撃状況共有（既存`TryGetCombatEvent`のCoreHitと組み合わせ可能）。自動照準・自動追尾は付与しない（AGENTS.mdの自動格闘カウンター禁止とは別軸の「選択」機能であることを明記）。

---

## 3. ダウン中でも通常格闘だけは命中するように

**現状確認**（重要な発見）: `Melee()`の命中判定ループ（`BattleCombat.cs`）には**対象(target)のDisruption/DownStageを見るチェックが存在しません**。`t.Alive`・チーム・高さ差・視線・射程・角度のみで判定しており、対象がダウン中でも現状のロジック上は普通に当たるはずです。また`u.Disruption`/`DownStage`/`DisabledUntil`は**攻撃側自身**の行動（`Fire`/`PrepareMelee`/`Melee`継続/`Jump`）を`CurrentTick < u.DisabledUntil`で止める目的でのみ使われています。

このため、ユーザー報告の「ダウン中は攻撃が当たらない」は次のいずれかだと考えられます（Claudeは実機確認ができないため断定不可、GPTでの実機確認をお願いします）:
- (a) **攻撃側が被弾スタガー中**（`DisruptionState.Staggered`、`DownStage==1`）で`DisabledUntil`により`PrepareMelee`が`return`し、そもそも格闘入力自体が不発になっている。「ダウン中でも通常格闘だけは当たるように」を「攻撃側がスタガー中でも、通常格闘(`MeleeStyle.Normal`)の入力だけは例外的に発動を許可する」と読む解釈。
- (b) Playable側（GPT領域）のアニメーション/入力制御で、見た目のダウン演出中はクライアントが攻撃入力を送っていない可能性。

**提案**（(a)の解釈を主として): `PrepareMelee`の`if (!held || CurrentTick < u.DisabledUntil || CurrentTick < u.MeleeUntil) return;`について、**`RequestedMeleeStyle(u)==MeleeStyle.Normal`のときだけ`CurrentTick < u.DisabledUntil`の制限を除外する**例外を追加（Combo/Dash/DashCombo/Chargeは従来通りダウン中は発動不可のまま）。既定で`DisabledUntil`チェック自体を外すのではなく、新規フラグ（例: `normalMeleeBypassesDisabledEnabled`、既定false＝現状互換）で有効化することを推奨します。
- (b)の可能性がある場合は、Playable側の入力抑制ロジックの確認もお願いします。
- もし意図が「対象がダウン中でも当たる」(読み違いの可能性)であれば、上記の通り現状のコードは既にそうなっているはずなので、実機で本当に外れているなら別の原因（視線判定ClearSegmentの何らかの副作用等）を調査する必要があります。

---

## 4. チャージ格闘を「格闘」「装備」と別枠にする

**現状**: `weapon.melee`（通常/コンボ/D格/Dコンボを提供する格闘武器）は他の武器・装備（`weapon.carbine`等のランチ武器、`weapon.repairpod`等の支援装備）と同じ**武器ハードポイント枠**（`PartSlot.PrimaryWeapon`等、`weaponHardpoints`予算を共有）に属する。チャージ格闘（`MeleeStyle.Charge`）は現在`SimulationConfig.meleeAttacks`の**グローバル共有5種**の1つで、どの武器を選んでも同じ内容になっており、武器選択とは独立だが「腕」とも紐付いていない。

**提案**: チャージ格闘を武器ハードポイント枠の消費対象から外し、**フレーム/腕の構成から自動的に決まる常設能力**にする（新規`PartSlot`は不要）。つまり:
- 武器枠（ランチ/格闘/支援装備）の選択に関わらず、どの機体も必ずチャージ格闘を1つ持つ。
- どちらのチャージ格闘になるかは、下記5節の通り装備している「腕」パーツ（`arm.standard.*` / `arm.reinforced.*`）で決まる。
- 実装案: `PartDefinition`に新規オプション項目（例: `MeleeAttackDefinition chargeMelee`）を追加し、腕パーツ側にのみ値を持たせる。Construction側のビルド解決（ロードアウト組立）で、両腕が同一のchargeMeleeを持つ前提（6節で左右統一するため常に一致）でそれを拾い、Simulationへ渡す。`config.meleeAttacks`の既存Charge(style=4)は「腕にchargeMelee未設定の場合のフォールバック」として残すか、完全に置き換えるかはGPTの判断にお任せします。

---

## 5. 腕タイプ別チャージ格闘の挙動

カタログ確認: `arm.standard.left/right`（healthDelta=0, weight=8）＝「通常腕」、`arm.reinforced.left/right`（healthDelta=600, weight=14）＝ユーザー言う「重量腕」に対応すると考えられます（WORK_BOARDの「補強2腕3200/3850」の数値とも整合）。

現在の既存グローバルChargeは `damage=2600, downPoints=1800, range=4.2, halfAngle=65°, boostCost=25, maxTargets=3, minChargeSec=0.4, fullChargeSec=1.4, minChargeMult=0.3, recovery=0.6, cooldown=0.7, lunge=2.8`。参考として他スタイルは `通常: damage760/DP900/range3.4/半角60°`、`D格(Dash): damage2280/DP1400/range3.8/半角50°/boost30`。

### 5a. 通常腕: 全方位範囲DP攻撃（ジェガンD格イメージ）
ダメージは抑え、**チャージ量に応じて範囲（射程）を広げる**。範囲攻撃でDP（ダウン値）を重視、キルよりも集団への牽制・怯み取りが狙い。
- 提案値: `damage=500`（固定、チャージで変化させない）、`downPoints=1600`（固定、高め）、`halfAngleDegrees=180`（全方位）、`range`: チャージ最小時3.0m→フルチャージ時6.5mへ**線形スケール**、`maximumTargets=6`、`boostCost=25`、`minimumChargeSeconds=0.4`、`fullChargeSeconds=1.4`（既存Chargeと同じ入力感覚）、`recoverySeconds=0.5`、`cooldownSeconds=0.8`、`lungeMeters=0`（その場で回転する攻撃、ダッシュ性能は持たせない）。
- 実装上の論点: 既存の`power`（ダメージ/DPへの乗算）とは別に「チャージ度を射程へ適用する」ロジックが新規に必要（現行コードはpowerをdamage/downPointsにのみ使う）。新規フラグ（例: `chargeScalesRange: bool`、`minimumRange: float`）を`MeleeAttackDefinition`に追加し、`chargeScalesRange==true`のときは`damage`/`downPoints`に`power`を掛けず固定値のまま使い、`range`を`minimumRange`〜`range`の間でチャージ進行度に応じて線形補間する、という案を提示します（具体の実装形はGPTにお任せします）。

### 5b. 重量腕: 上段振り下ろしの範囲格闘（ドアンザクイメージ）
50%未満のチャージでリリースすると**通常格闘(760)・D格(2280)より弱い**火力の格闘として出る。50%以上のチャージでは**一撃の火力重視**、チャージ量に応じてダメージが増加し、地面まで届く縦の範囲（前方扇状、やや狭め）を取る。
- 提案値（チャージ進行度f=保持時間/フルチャージ時間、0〜1）:
  - `f < 0.5`: `damage=550`（通常760・D格2280より明確に弱い）、`downPoints=700`
  - `f >= 0.5`: `damage`を`1800`(f=0.5)から`3000`(f=1.0)まで線形増加（D格2280・旧Charge2600を上回る、フルチャージ最強の一撃にする）、`downPoints`を`1500`→`2200`で同様に線形増加
  - `range=4.5`、`halfAngleDegrees=40`（D格の50°よりやや狭い縦振り）、`maximumTargets=5`（振り下ろしの軌道に居る複数機を巻き込む）、`boostCost=30`（D格と同等の重い消費）、`minimumChargeSeconds=0.4`、`fullChargeSeconds=1.4`、`recoverySeconds=0.7`（大技の後隙）、`cooldownSeconds=1.0`、`lungeMeters=2.0`。
- 実装上の論点: 「閾値50%で挙動が変わる2段階カーブ」は既存の`minimumChargeMultiplier`単純線形補間では表現できません。新規フィールド（例: `thresholdChargeFraction=0.5`、`belowThresholdDamage`、`belowThresholdDownPoints`）を追加し、`f<threshold`なら固定値、`f>=threshold`なら`(f-threshold)/(1-threshold)`で`damage`/`downPoints`へ線形補間、という案を提示します。具体の実装形・フィールド名はGPTの裁量にお任せします。

---

## 6. 腕自体を左右共通に

**現状**: `PartSlot.LeftArm`(0)/`RightArm`(1)は独立したスロットで、現状の`BuildDefinition`は理論上`arm.standard.left`＋`arm.reinforced.right`のような**左右非対称の混成**を許容しています（検証ロジック上の明示的な禁止は確認できていません）。

**提案**: 4〜5節でチャージ格闘の挙動を「腕タイプ」から一意に決めるため、左右で腕タイプが食い違うと挙動が未定義になります。これを防ぐため:
- Construction側のビルド検証に「LeftArmとRightArmの選択パーツは同じ系統（`arm.standard.*`同士、または`arm.reinforced.*`同士）でなければならない」というルールを追加（違反は既存の構築エラーと同様に拒否）。
- UI（Garage画面、GPT領域）側は左右2つのピッカーではなく「腕タイプ」1つのピッカーに変更し、選択した瞬間にLeftArm/RightArmへ同じ系統のパーツを同時に反映する（内部的に`EquipmentSelection`が2件になるのは変えず、常に両方を同期させるだけで良いと考えます）。
- 既存の「補強2腕」プリセット等、既に左右が一致しているものは無影響。

---

## まとめ・優先順位の提案

依頼全体がひと続きの大きな機能追加のため、実装順はGPTの判断にお任せしますが、依存関係から見ると:
1. 6節（腕統一ルール）→ 5節（腕別チャージ格闘の前提）→ 4節（チャージ格闘の別枠化）の順が自然。
2. 1節（ブースト1.3倍）・3節（ダウン中通常格闘例外）は独立して先行着手可能。
3. 2節（格闘ロックオン・拠点ロック）はPlayable側のUI変更を伴うため、Simulation側の土台（拠点ロックフィールド追加）が先、UI反映は別途。

いずれも既定値0/false/既存互換で後方互換を保てる設計を意識しました。実機（Unity Editor/PlayMode）確認が必要な部分（特に3節の原因特定、2節のPlayable UI）はClaudeでは検証できません。GPTの着手判断・数値調整を歓迎します。
