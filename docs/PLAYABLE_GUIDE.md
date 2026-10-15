# 最小プレイ版：起動と確認

Unity 6000.3.26f1 / URP / Windows x64。参戦準備から機体構築、1人+99BOTの対戦、再出撃、結果・再戦までを最初の試遊範囲とする。50vs50と別枠の指揮官2枠を保持する。指揮官AI・オンライン・アニメーション/LOD・金功績等は未実装。

## Editorで遊ぶ

1. 開いたシーンを保存し、Play Modeを終了する。
2. **MechaBattle > Playable > Generate and Open Playable Scene** を実行。初回はTMP Essentialsを非対話でインポートする。
3. MechaBattle_Playable.unityでPlayする。既存のP0_Battlefieldシーンは回帰検証用として残す。
4. 青/橙、4機のデッキ、左右腕と武器を選ぶ。テンプレートも同じ構築データ。性能・重量・出力・コスト・CTを確認して保存/出撃する。
5. 敵の建屋内に侵入し、中央コアを攻撃して敵戦力ゲージを0にする。本拠点の反撃は建屋と同じ範囲で届く。中継占領、味方本拠点での補給も有効。

| 操作 | 入力 |
|---|---|
| 移動 | WASD（カメラ方向基準） |
| 照準 | マウス |
| 射撃 | 左クリック |
| 照準拡大 | 右クリック |
| ブースト | 左Shift（残量消費、離すと回復） |
| 格闘 | E（手動入力と再使用時間が必要） |
| 一時停止/再開 | Esc |
| 撃破後 | 使用可能な機体・本拠点/確保中継を選んで出撃 |

全機体がCT中なら待機する。敵がいる中継からは出撃できない。戦力ゲージ0または時間切れで結果画面へ進み、同じデッキで再戦できる。ミニマップは味方と自分の射程内で見える敵だけを表示する。

## 保存

Application.persistentDataPath/mechabattle-deck-v1.json。正常に検証できたデッキのみ保存し、置換時に.bakを残す。不正/旧形式の保存データは起動時に初期デッキへ戻すが、保存操作までは元ファイルを保持する。UserTextは人の資料用で、ゲームやAIの書込先にしない。

## Windows版

Play Modeを終了して **MechaBattle > Playable > Build Windows Playable** を実行する。専用シーンだけを明示的にビルドするため、既存Build Settingsに依存しない。出力は MechaBattle/Build/PlayableWindows/MechaBattle.exe、記録は Build/Reports/playable-windows-build.json。

## 自動検証

- scripts/verify-domain.ps1：純粋C#回帰と構築済み100BOT試合。Unity描画の試験ではない。
- scripts/verify-compile.ps1：全asmdefを依存順にUnity APIへ照合してコンパイル。Unityインポート/Player buildとは区別する。
- Unity Test Runner/EditModeの **Game.Tests.Playable**：実シーン再読込、起動UI、部品変更、100枠、人間枠、構築性能、休止、結果、再戦を一巡する。テスト用シーンと0.5秒の試合設定だけを別途生成し、プレイヤーの保存データは書き換えない。
- **Game.Tests.ModelTools**：実際のUnity JsonUtilityでのカタログ/4枠保存復元、部品混在、Prefabの生成・GUID/共有素材・シーン保護を検証する。

## 現在の確認状況

2026-10-15：ユーザーがEditorで生成→機体構築→戦闘への移行を確認。GPTの独立C#90件成功、全17アセンブリ静的APIコンパイル成功。構築済み100BOT試合は70.06sim秒で決着、撃破312/出撃372/コア命中257/建屋反撃9。リソース上限とCTを全tick確認した。

Unityの新規統合テスト、結果/再戦・手動再出撃の実機受入、Windowsビルド/起動、2019ミドルPCでの100機60fpsは未確認。GPTからのEditor接続は検出ファイルのアクセス拒否。別の隔離コピーでのEditor検証も、サンドボックスOSアカウントにUnityライセンス権利が無いため実行前に停止した。

見た目は1標準フレームと交換腕/武器の静的モデル。戦場は平面hitscanと開放型建屋。地形/壁の物理衝突、立体射撃、歩行/照準アニメーション、詳細バランスは後続作業として明示する。
