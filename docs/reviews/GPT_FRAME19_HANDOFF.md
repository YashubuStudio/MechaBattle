# FRAME-19 — フレーム選択と武器名（GPT）

2026-10-16。要件 FR-401/403/501/805、UserTextのモジュール機体構想、今回のユーザー依頼。全編集予約は解除。実装・独立検証完了、Unity実操作の受入待ち。

## 結果

武器欄の「標準アーム」は weapon.assaultrifle の表示分岐漏れ。PartTitleの未知ID fallbackが腕名だったため。FAアサルトライフルの名称を追加し、標準/補強腕は実際の腕IDだけに限定。未知IDを腕名へ変換しない。

機体制作に「フレーム」を追加。既存の標準(frame.standard.v1)と重量(frame.heavy.v1)を直接選択できる。カードで現在の腕/装備を含むHP・盾・速度・DP耐性・重量/出力・コスト/CTを比較する。構築済みのframeIdが既存のMechaMeshLibraryへ渡り、既存の異なるフレーム形状をプレビュー/戦闘に使用する。形状幅は標準約0.986m、重量約1.243m（フレームメッシュ自体の境界）。新しいモデルや性能値は追加していない。

|フレーム|素体HP|盾|移動速度|DP耐性|武器制限|
|---|---:|---:|---:|---:|---|
|標準|2000|900|12|600|既存13種|
|重量|2650|1100|9.5|750|キャノン/バズーカ/格闘/ポッド/グレネード/地雷/回復FA/レーダー|

左右補強腕ならHPは標準3200、重量3850、速度は各-1。軽量/特殊用途はUserTextの構想段階で、今回数値を新設して実装済みと扱っていない。

## 変更動作

FrameConstruction.TryChangeFrameは元のBuildを変更しない。ID/塗装/対応する腕と武器/装備枠数を保持し、互換外の武器だけ対象フレームの既存presetの主武器へ置換（重量は単発キャノン）。置換の対象枠/武器名をクリック前のカードと適用後noticeに表示。重量制約などの検証に失敗した候補は選択不可。出撃中は変更不可。他のデッキ機体は変更しない。標準へ戻しても既に置換した武器を過去のARへ自動復元することはない。必要なら通常の武器選択で選び直せる。

OpenPartPicker/SelectPart/CyclePartは同じFrameConstruction.CanMountで接続規格とallowedWeaponIdsを確認。重量型の候補に装着不可の銃を出さない。共有Contracts、configの数値、正本、UserText、Scene/PrefabのYAMLに変更なし。

## 対象

- Assets/Game/Runtime/Construction/FrameConstruction.cs（新規）
- Assets/Game/Runtime/Playable/PlayableEntry.cs / PlayableUI.cs
- Assets/Game/Tests/EditMode/FrameConstructionTests.cs（新規12ケース）
- Assets/Game/Tests/EditMode/Playable/PlayableFlowTests.cs
- Assets/Game/Tests/EditMode/ModelTools/ModularMechaTests.cs
- scripts/DomainSmoke/ConstructionDefaultsSmoke.cs

パスはMechaBattle/以下。新規2csの.metaはEditorによる生成を読み取り確認した。既存GUIDの変更なし。

## 検証

独立コピー MechaBattle/Build/FRAME19_1792153185260/ に保存。純C#408/408、全20asm静的コンパイル、両者exit0。81純C#ソース（75 Runtime/Tests + Harness6）、実catalog8preset×2frame=16変更、4394自由三装備の重量化・保存復元を検査。ソース/設定133対象+モデル3資産=136SHA、最終差分0。モデル検査19modules/8presets/153閉立体/6060triもexit0。実config2コピー一致、Scene除外diff整形exit0。

ログ: domain-run.log、static-final.log、各.status.json、project/Build/Reports/domain-smoke.json / static-api-compile.json、model-validation.log、MechaBattle/Build/Reports/simple-mecha.json、source-sha256.json（上記隔離先内）。実100BOT構築試合は437.42sim秒完走、88撃破/187出撃/119コア命中。これはUnity画面やFPSの証明ではない。

Nativeではフレーム比較入口/AR名称/適用/重量武器候補/支援装備維持を既存Flowへ追加。武器候補を固定番号で探す旧試験はAR追加後にずれていたため、表示名で特定する方式へ修正した。モデル試験は旧mixed-arm成功/HP3000を、片側補強の拒否と両側補強HP3200/1940triへ修正。追加1件で両フレームをUnity JsonUtility保存復元してモデル組立する。これらNativeケースはコンパイルのみで未実行。実クリック/レイアウト/実保存再起動、Unity Test Runner、Windows、FPS/GCは未実施。Editor.log最新tailにC#エラー/例外はなかったが操作の合格とは扱わない。

## Claudeへの引継ぎ

直接通信/レビュー受領は未確認。WORK_BOARDとこの文書で共有。左右腕同系統化が残存Nativeモデル試験と不整合だったため上記を修正済み。前回レビューの入力chargeMelee.Copy、被弾方向のworld yaw/SourceUnitId照合、Pause表示時だけの戦績構築も現コードで維持され、今回408件/20asmコピーに含む。ChargeMelee getterは信頼モデルのままというClaudeの判断を記録し、完全なimmutable化済みとは報告しない。

次の操作: Play停止 → Assets > Refresh → コンパイル完了 → 既存PlayableでPlay → 機体制作 → フレーム。Scene再生成は不要。標準/重量の比較と変更、ARの正しい名前、重量型候補の8種、戻る/取消、保存後の再起動、両フレームの見た目を実機確認する。Game.Tests.PlayableとGame.Tests.ModelsのNative試験は未実行のまま引き渡す。GPT起動の処理は全て終了。全予約解除済みでClaudeは続行可能。
