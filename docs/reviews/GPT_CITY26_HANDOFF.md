# CITY-26 — 二層都心・本拠点耐久・コア衝突 / GPT → Claude

更新: 2026-10-16T23:39:52.852Z。owner GPT。実装と独立検証完了、Unity実機受入待ち。正本/人のUserTextを変更していない。要件 FR-101/102/105/108/109/110/201/203/301/601/602/603/604/802/805 と最新ユーザー指定。

## 現在のゲームから見える変更

選択マップは **荒地渓谷・立体市街／連絡橋・二層都心／地下回廊** の3つ。中央工業戦線と段丘工業区をplayableカタログから削除。旧Playableシーンが中央工業を参照していても起動時に荒地へ移行し、旧マップを「シーンのマップ」として再追加しない。新規Playable生成も荒地を使う。旧standard/terraced JSONはP0基盤/既存回帰/保存Scene保護の非選択fixtureとして保持し、元ファイルを破壊していない。

全マップで本拠点は青北→南 **A/B/C**、橙北→南 **X/Y/Z**。ID文字列は既存保存/参照のため維持し、MapDefinition.MainBaseNameがZ座標の北順から表示名を決める。Snapshot.Name、3D表示、地図/ミニマップ、出撃先、HUDで同じ呼称を使う。

Playable仮値は **各本拠点HP20,000・直接損害ゲージ係数0.08（従来0.5）・陥落追加8,000を一度**。実configのcity26Notesに理由。実HP損失で減算/拠点与ダメを計上し、超過攻撃や破壊後で稼がない。陥落した拠点は出撃/基地補給/防衛射撃/新しい核要請を停止する。核の友軍被害は維持。通常の機体CT/有限弾/ゲージ規則も維持。P0の古い試験では有限耐久をopt-inとし、実Playable Applyで必ず有効化する。

## 新マップの実体

二層都心・地下回廊（layered-city）、**840×560m・192衝突volume・82公開ナビ節点・各軍3本拠点・6中継**。既存IPの実地形/画像/アセットをコピーせず、ユーザー説明の戦術関係をオリジナルの箱形地形として構成した。

- 地上床Y16・地下床Y0。地下は地上の下にあり、地上床が物理天井になる。地下だけを別マップへ転送する方式ではない。既存世界の最低床0を維持しつつ、目的物/出撃が明示Yを持つ。
- 北1=(0,155)、2=(-230,120)、3=(230,120)。1への中央街路x0を開け、2/3はA/X近傍で守る地点。A/Xは北のビル群と細街路の間、地上のA/Xが経路上もっとも近い敵本拠点。
- 4=(0,-135,Y0)はC/Zの折れた地下通路の中央。C/Z=(-280/+280,-220,Y0)、繰り返しの直角折れで遠距離射線を切る。
- 5/6=(-180/+180,-65,Y16)は入口前。両方に敵側の狙撃遮蔽ビル。64段×0.25mの連続階段で地下へ繋ぐ。地図の水色線が公開地下経路、C/Z/4は地下表示。
- B/Y=(-380/+380,0,Y16)は後方の折れた遮蔽の奥。スポーンだけ(-205/+205,0,Y16)へ前進させ、中央までの距離を最短にする。B/Yの耐久/損害は他拠点と同じで、無敵扱いしない。
- 地上は北/南のビル街と中央の開口、地下は通路中心。地下用point lightは9個、shadowなし。実Unityの暗さ/URP light budget/性能は未検証。

実データから生成して目視した地上/地下/断面図: MechaBattle/Build/CITY26_1792192723488/LayeredCity.png。Unityスクリーンショットではない。

## 不具合と修正

コアは従来、表示だけの箱で移動/カメラの共有衝突に含まれなかった。またカメラが拾った点が銃口より後ろにあってもAimAfterMovementで逆向きへ照準を更新していた。

- 実体コアを半径AttackRadius・高さcoreHeightの円柱として移動/カメラ/射撃判定へ接続、表示も同サイズの円柱に揃えた。ドメインの共有衝突であり、Unity Rigidbodyの反発やOnCollisionEnterには依存しない。表示primitiveのColliderを残すだけでは解決しないため、権威判定を統一した。
- 範囲外で損害が入らないコア/味方コアも、物体として射撃を遮る。陥落後も見える中立色の残骸は通過させない。
- TraceCameraAimでカメラの後方ヒットを銃口の前向きrayへ切り替え、移動後に照準点が背後へ回った時の人間の反転を防ぐ。直角の照準、通常のカメラ/銃口収束、既存opt-inの明示赤ロック補助は回帰維持。実Playableの継続追尾なし/手動取得時だけ吸着も維持。
- 高さ別の本拠点射撃/爆風/格闘/核被害、占領人数、補給、防衛射撃、出撃を接続。GetSurfaceHeightBelowで屋上を選ばず、目的の階に出撃/核配送する。

## 契約・主要コード

共有変更の提案は着手時WORK_BOARDに保存済み。既存呼出互換を保つoptionalフィールド/末尾引数の追加。単位はm。

- Battlefield/MapDefinition.cs: MainBase.y/maxHealth/spawnOverride/spawnX/Y/Z、CapturePoint.y、Map.surfaceY/navigationNodes、NavigationNodeDefinition。SpawnX/Y/Zプロパティで既存未指定時はcore位置を使う。Validatorは高さ/有限値/出撃空間/リンクを検査し、床の下や高い天井を2Dだけの重複として拒否しない。
- Simulation/SimulationSnapshots.cs: MainBaseSnapshot.Name/Y/Health/MaxHealth/Destroyed、CaptureSnapshot.Y。既存Idは保持。
- Observation/CapturePointObservation.cs: IMainBaseStatusSource（MainBaseCount/IsMainBaseDestroyed）。敵機情報は含まない公開目的物の状態だけ。PlayableEntry/Runner/独立smokeの観測Adapterが提供する。
- Simulation/BattleSimulation.cs・BattleDeployment.cs: 耐久/一度の陥落損失、実損害、陥落後の出撃/補給/防衛停止、高さ別出撃と占領。明示選択が陥落したら勝手に別拠点へ送らず拒否する。
- BattleCombat.cs・BattleTraversal.cs・BattleFeedback.cs・BattleStrategy.cs・BattlePortableNuclear.cs: コア共有衝突、後方照準ガード、高さ別損害、実拠点与ダメ、地下への核配送。各軍3本拠点/100操縦枠/指揮官別2枠は維持。
- BotTraversalAdapter.cs・BotLayerRouting.cs: 公開地形のDijkstra経路で地上/地下を通る。中心/北側/地下の役割と公開占領/陥落状態で目的地を選ぶ。経路配列/探索scratchを共用し毎tick生成しない。敵の隠し位置/核情報を供給しない。ForwardMarchBotCommandSourceも公開陥落情報で生存拠点へ切り替える。
- PlayableWorldView/Minimap/TacticalMapView/DeploymentMapView/PlayableEntry: 名前/HP/陥落、地下経路線、実高さの目的物、前方spawn、コアモデル。既存Canvasを維持し、凡例幅だけ層あり時320pxへ変更。
- PlayableContentSelection・Editor/PlayableTools/PlayableSceneBuilder: 旧2マップを選択に戻さず、新しい既定は荒地。Content/Playable/StandardMap.jsonはGUIDを保持した既存TextAsset名で、中身を荒地へ更新。SourceScene YAMLは編集しない。
- config/prototype-defaults.json・Content/Playable/PrototypeConfig.json、config/map-catalog.json・Resources/MapCatalog.json、config/layered-city-map.json・Resources/LayeredCity.jsonはそれぞれ一致。再生成はscripts/content/generate-layered-city-map.mjs、描画はscripts/art/render-layered-city.py。

## 検証結果と証拠

最終証跡: MechaBattle/Build/CITY26_1792192723488/final/。機械可読一覧: GPT_CITY26_VALIDATION.json。

- **101純C#ソース、全510/510成功、失敗0**。新12ケース: HP実損害/一度の陥落損失/出撃拒否、北順呼称、コア侵入/カメラ衝突、後方のカメラ/移動後照準、耐久の不正値、地下spawn/重なる上下拠点の別占領/床の遮弾/地下着地。
- 部分27/27に加え、実JSON・半径0.65mのproduction motorでB前方spawn→5→階段→4→C/Z折れ通路→6→地上の往復を検査。中央から1の直線開口、5/6の敵側ビルによる射線遮断も実geometryで検査。
- 観測を自分の状態/公開地図/公開占領・陥落だけに限定した実BOT Adapterで、5→地下4占領→Z攻撃を検査。有限弾/速度/移動を省略しない。
- 最終100BOT3マップは600sim秒の時間切れまで完走。荒地:83撃破/183出撃/204本拠点命中/陥落4。立体市街:82/182/29/陥落0。二層都心:**297撃破/393出撃/123本拠点命中/陥落1/占領変更42**、両軍最小44/37、平均生存97.90。最大100を維持。中心割合の既存固定窓は新mapの北/地下戦場を分類しないので、中央優勢の受入指標として比較しない。人間試遊の前線/防衛バランスは未受入。
- **静的20アセンブリ成功**。最終UI凡例調整後static-final.log exit0。既存JSONフィールドのCS0649警告のみ。Native/Unity import/Playerビルドの成功を示さない。
- 初回acceptedは509成功/1失敗、新map100BOTで拠点攻撃0。原因は補給/退避/近接の2D移動と古い迂回点が層別経路を上書きしていたこと。非戦闘の最終移動も層別経路で整え、古い2D迂回点を解除、上記BOT到達回帰を追加した。試験を弱めず最終全再実行で解消。旧失敗ログはaccepted/に保持。
- 174対象SHA現行差分0、config/catalog/mapの各コピー一致、既存Sceneの空白を除いたdiff整形exit0。地図画像を192volume版で目視。正本/UserText/.git/Scene YAMLを変更していない。

## Claudeの次の一手 / 未実機事項

Unity IPCへこのGPT実行ユーザーから接続できず、**Unity実画面/新旧Native/Windows build/run/FPS・GCは未実施**。別Editorを開いたり権限を変えて回避していない。新4Assetsの.meta（BotLayerRouting、2新規Tests、LayeredCity.json）は最終確認時未生成。Assets > RefreshでEditorに生成させ、GUIDを手書きしない。

1. 未保存作業を保護、Play停止→Assets > Refresh→コンパイル完了→既存PlayableでPlay。Scene再生成不要。Resourcesの新map取り込み/Consoleとmeta生成を確認。
2. マップが荒地/立体市街/二層都心の3つで、旧中央工業/段丘が再挿入されないこと。更新したPlayableFlowTestsのマップ選択・100枠フローをNative実行。
3. 両軍から各拠点へ出撃し、A/B/C・X/Y/Zの地図/3D/HUD/HP表示を確認。B/Yの前方spawnが中央へ早く、コアは後方に残ること。上階から地下4を占領しない/地下spawnが屋上へ上がらないこと。
4. 5→階段→4→C/Zの折れ通路→6を実操作、地下の明るさ、頭上/階段/コア周囲のカメラ、地図水色線/地下表示を確認。北中央→1の開通、2/3の防衛距離、5/6の狙撃遮蔽を人間の武器で確認。
5. **ロックを完全解除して**敵コアへ歩行/横移動/接近しながら射撃し、不意な反転・めり込み・カメラの壁内侵入がないことを確認。共有円柱に対応する当たりのため、古いCubeの輪郭と比較しない。
6. HPを減らして陥落させ、通常減算0.08と一度だけ8000、陥落後の再攻撃/出撃/補給/防衛停止、核による友軍損害/得点の分離を実確認。
7. 100体+9地下灯のWindows実行、GPU/CPU/FPS/GCを計測。アート見栄え/照明/実操作バランスは独立C#の成功で合格としない。

指揮官の自動戦略判断、BOTの自律核運搬/緊急帰投判断は今回未接続のまま。Claudeへの直接送信/今回受領・レビュー完了は未確認で、共有文書への保存だけを引継ぎ事実とする。解除範囲は追加GPT承認なしでレビュー・実機検証・改修を継続可能。

## 予約とプロセス

実装ソース/描画は各区切りで解除済み。文書4件も最終WORK_BOARDで解除する。初回domain PID31356は終了exit1（上記失敗）、初回static PID36564はexit0。最終domain PID9924・static PID27900・凡例仕上げstatic PID32632はすべて終了exit0。生成/部分検証/描画も終了、元ソースへのバックグラウンド書込みなし。他者のプロセス/未保存Editor状態は操作していない。
