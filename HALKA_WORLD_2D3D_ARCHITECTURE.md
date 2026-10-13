# HALKA WORLD 2D／3D共通ワールド 技術仕様書（Phase 1）

作成: 2026-10-14／ブランチ: `claude/ver3.0-2d3d-phase1`（作成元 `codex/ver3.0-preview` の `d3f81ac`）
対象: Unity 6000.3.10f1 / MAP EDITOR v0.6 / Map JSON v4

この文書は、Phase 1の調査と試作で確かめた事実をもとにした設計です。「確定」と書いた項目は、試作のコードと検証で確認しています。「提案」と書いた項目は、ユーザーの承認を待っています。

最重要原則: **できることは増えるが、操作方法は増やさない。**

---

## 1. 現行プロジェクトの調査

| 項目 | 実体（最新コード） |
|---|---|
| Unity Project Root | `unity-project/`（Built-in Render Pipeline、Color Space = Gamma） |
| Packages | `com.unity.modules.audio`、`imgui`、`physics2d` の3つだけ。3D Physics・UGUI・glTFastは入っていない |
| Scene | `Assets/Scenes/FirstDay.unity`（`ProjectBuilder.PrepareScene` がMap JSONから毎回生成） |
| Map Source of Truth | `Assets/Content/Maps/Authoring/*.hwmap.json`（v4）＋ `object_catalog.hwcatalog.json`（formatVersion 3）＋ `entity_catalog.hwentitycatalog.json` |
| Map Cache | `Assets/Content/Maps/<mapId>.asset`（`MapDefinition`）。`MapAuthoringImporter` がJSONから同期 |
| Runtime Loader | `MapRuntimeLoader2D`：Surface・Object・草・Crowを生成。通行不可セルには `BoxCollider2D`＋`GridObstacle` を置く |
| Grid | `GridWorld2D`：1セル＝0.5 unit＝32px。`CanEnter` は **Physics2Dのコライダー**を調べる |
| Player | `PlayerMover`（`GridStepMotion` を所有し、1歩0.18秒）、`CharacterVisual`（64×64 Sprite、PPU64）、`PlayerSeatController` |
| 移動計算 | `GridStepMotion`：純粋なC#。セルの補間と歩行完了だけを扱い、描画に依存しない |
| Camera | `CameraFollow2D`：正射影でorthographicSize 4.5（縦9 unit＝18セル）、SmoothDamp 0.2秒、マップ範囲＋0.8でクランプ |
| Interaction | `InteractionRouter`（クリックとAボタン）、`ExamineInteractable`、`WorldObjectActionInteractable`。判定はPhysics2D経由 |
| AUTO | `AutoModeController`：60秒操作がないと開始。経路は `GridPathfinder2D`、屋内の目的地は `IndoorAutoRoutePlanner2D` |
| Life Log | `LifeLogController`：`StepCompleted` で歩数を数え、PlayerPrefs `HALKA_WORLD_life_log_v1` に保存 |
| World Time | `WorldTimeCycle`（純粋計算）＋`WorldTimeController`。PlayerPrefs `HALKA_WORLD_time_cycle_anchor_v1` に起点を保存 |
| 夜表示 | `WorldNightOverlay2D`：Cameraの子に置いたSpriteで夜色を重ねる。色(0.08,0.12,0.38)、屋外α0.33／屋内α0.16、Fade 2.5秒 |
| Crow | `CrowWander2D`：状態・地上移動・飛行の位置計算・Sprite描画が1クラスにまとまっている |
| 入力 | `GameInput`（WASD／矢印・クリック）、`TouchDpad`、`TouchActionButton`（すべてIMGUI） |
| WebGL Template | `MinimalNoBrand`：デスクトップでは576pxの整数倍、モバイルではDPRを最大2に制限 |
| Build | `ProjectBuilder.BuildWeb`（公開用）、`BuildPreviewWeb`（repo外） |

## 2. 既存2D構造

既存の2D実装は、ゲーム処理と描画が**半分だけ**分かれています。

- 分離済み: `GridStepMotion`、`WorldTimeCycle`、`MapPlacementRules`、`GridPathfinder2D`、`IndoorAutoRoutePlanner2D`、`MapDefinition` はどれも描画に依存しません。
- 未分離:
  - 通行判定が「データ→Physics2Dコライダー→OverlapPoint」と遠回りしている。
  - Interactionの探索がPhysics2Dに依存している。
  - `CrowWander2D` が、状態とSpriteを1つのクラスで持っている。
  - `PlayerMover` が、`transform.position` にXY平面の座標を直接書き込んでいる。

Phase 1では本編のこれらのクラスに手を入れていません。試作側で、データから直接作る共通Gridを用意しました。

## 3. 共通World Data設計（確定：試作で実装）

```
Map JSON v4 ──(MapAuthoringImporter)──> MapDefinition（既存のキャッシュ）
                                            │
                                            ▼
                               SharedWorldModel（描画を持たない）
                               ・Bounds、Blocked Set（MapPlacementRules.BlocksMovement）
                               ・Object Instance（InstanceId / DefinitionId / RootCell / SignText）
                               ・Interaction判定（既存ルールを移植）
                                            │
SharedWorldSession（唯一のゲーム状態）◄─────┘
 ・Player: GridStepMotion（既存）／Facing／CompletedSteps
 ・Map切替（家の入退室）、Interact
        │ 読むだけ                     │ 読むだけ
        ▼                              ▼
 2D Pixel View                  3D Third-Person View
（同じ3D GameObjectを              （同じ3D GameObjectを
  正射影＋低解像度で描く）           透視投影で描く）
```

- Player位置・向き・Map・歩数は `SharedWorldSession` に1つだけあります。表示側はこの状態を毎フレーム読むだけで、書き込みません。
- **2D表示と3D表示は、同じ3D GameObjectを別のカメラで描いているだけです。** そのため、同じObject Instanceが2つの表示で別の場所に出ることは構造上起こりません。
- 表示モードを切り替えても、`SharedWorldSession` には一切触れません。40回の連続切替で、セル・向き・歩数・Map・読込回数が変わらないことを検査済みです。

## 4. 3D Coordinate Mapping（確定）

```
World3D = ( cell.x × 0.5,  height,  cell.y × 0.5 )     … GridSpace3D.ToWorld
cell    = ( round(X / 0.5), round(Z / 0.5) )           … GridSpace3D.ToCell
```

- Grid +x＝東、Grid +y＝北です（`Vector2Int.up` が画面の上＝北）。Unityでは +X＝東、+Z＝北、+Y＝上です。
- 軸の反転は不要です。下向き（-Y）でup＝+Zのカメラから見ると、+Xは画面の右になります。2Dの画面とも一致し、POCの描画でも確認しました。
- 向きのYaw: Up＝0°、Right＝90°、Down＝180°、Left＝270°。ルートの `transform.forward` が向いている方向になります。
- Blender素材は南（-Z）を正面にして作ります。キャラクターは、向き用ルートの下に **+180°の子ノード**を1つ挟んで向きをそろえます（`GridSpace3D.ModelFrontYawOffset`）。
- ゲーム処理はこの座標を逆算に使いません。3D座標は表示のためだけに使います。

## 5. GridとCollision（確定）

- 通行可否は `MapPlacementRules.BlocksMovement`（既存の静的ルール）から作る `HashSet<Vector2Int>` で決めます。Physicsは使いません。
- 一致の検証（試作の自動検査）:
  1. Map JSON＋Catalog JSONだけで計算したBlocked集合
  2. 共通Grid
  3. `MapPlacementRules`

  この3つが、first_field（38セル）とhalka_house（66セル）で完全に一致しました。
- 3D Physicsのモジュールは入れていません。クリックで物を選ぶ処理は、`Renderer.bounds.IntersectRay` で行っています。
- 本編へ統合するときの提案:
  - `GridWorld2D.CanEnter` を共通Gridに置き換えます。
  - Physics2Dのコライダーは、クリック判定にだけ使う形へ縮小します。
  - Dynamic Occupancy（`DynamicGridOccupancy2D`）は今の形のまま、共通Gridに重ねて使えます。

## 6. Blender Asset Pipeline（確定）

```
blender/scripts/halka_lowpoly.py      … 形とColor属性を作る補助、FBX出力の設定
blender/scripts/build_poc_assets.py   … 仮Player・Tree・Houseを生成（再実行すれば同じ物ができる）
blender/sources/poc_*.blend           … 制作元（Source of Truth）
unity-project/Assets/POC2D3D/Models/poc_*.fbx … Unity用に書き出したもの（生成物）
blender/exports/poc_assets_manifest.json      … 三角形数・Bounds・書き出し設定
blender/scripts/render_poc_previews.py        … .blendのWorkbenchプレビュー
```

- 規約: Blenderの +X＝東、+Y＝北、+Z＝上。1 Blender unit＝1 Unity unit、1セル＝0.5。原点は、ルートセルの地面中心に置きます。
- 色は面の角ごとのColor属性 `Col`（Float Color、sRGBで書き出し）に持たせます。Unity側は共通のマテリアル1つだけなので、**形と色の管理はBlenderだけで完結**します。
- `.blend` の中には、セルの位置を示すEmpty（`*_GridGuides`）を置いています。これはFBXには書き出しません。
- 実行コマンド: `blender.exe --background --factory-startup --python blender/scripts/build_poc_assets.py`

## 7. Unity Import方式（確定：FBX）

| 比較項目 | FBX（採用） | glTF / GLB |
|---|---|---|
| Unity標準Importer | あり（追加なし） | なし（glTFastなどのPackageが必要。今回は導入しない） |
| 軸・Scale | Blenderで `-Z forward / Y up / Apply Transform`、UnityでBake Axis Conversion。回転0・Scale 1・原点が地面中心であることを検査済み | Packageによる |
| Normals／Material | 面の法線をImport。Blender側のMaterial名をUnityの共有Materialへ割り当て直す | 標準はPBR |
| Animation | Armatureと `bake_anim` で対応できる（Phase 2で検証） | 対応 |
| WebGL | 実行時に変換が不要（Importで完結） | 実行時に読む方式ならWASMが増える |

- Import規則は `PocModelPostprocessor` に置き、`Assets/POC2D3D/Models/` だけに効かせています。Scale 1、Bake Axis、Animationなし、法線Import、溶接なし、Material割り当て直し。
- 確認済みの項目:
  - 目の色の頂点が -Z（南）側にある。
  - 右肩の印が -X（西）にあり、左右が反転していない。
  - 背中の印が +Z にある。
  - 家の扉は z≈0.24 で、ルートセル内の凹み（奥側）にある。
  - 三角形の巻き方向と法線の向きが100%一致している。
  - 頂点色の誤差は±2/255以内。

## 8. Presentation Binding（確定：試作で実装）

```
Object Definition ID ─► PocPresentationCatalog.Find(id)
                          ├ 見つかった: model prefab ＋ yaw / meshOffset / uniformScale / cameraOccluder
                          └ 見つからない: Blocked Cellから箱を自動生成（未3D化の仮表示）
                       ─► GridSpace3D.ToWorld(RootCell) に配置
```

- 配置データ（Instance ID、Definition ID、Root Cell、Footprint、Blocked Offsets、Action Point、Facing、signText）はMap JSON v4とCatalogが持っており、**変更していません**。
- 3D表示専用の値（Scale・Rotation・Mesh Offset・遮蔽フラグ）は、別のPresentation Metadata（`POC_PresentationCatalog.asset`）に分けています。
- Phase 1でBlenderモデルを割り当てたのは `tree_basic` と `house_main` です。残りの11種（stone、flower、sign、bench、well、bed、cushion、desk、recording_mic、art_easel、studio_shelf）は自動生成の箱で表示しています。
- 本編への提案: Presentation Metadataは、Catalogの隣に置くJSON（例: `presentation_catalog.hwpresentation.json`）にします。MAP EDITORからも読めるようにし、Map JSONのSchemaは変えません。

## 9. 2D Pixel View（確定：方式Aで試作）

```
同じ3D World ─► 正射影カメラ（見下ろし角θ） ─► 低解像度RenderTexture（Point） ─► Graphics.Blitで整数倍拡大 ─► 画面
```

- **ドット密度**: 16／24／32px/セルを切り替えられます（初期値は16）。
- **整数倍**: 拡大倍率は `n = round(画面の高さ ÷ (18セル × px/セル))` で決めます（最小1）。RTは画面サイズ÷nの切り上げで、はみ出した数pxは左右・上下に均等に切り捨てます。結果として、どのテクセルも必ずn×n画素になります。
  - 例: 1024×576 → 512×288 ×2。780×1688 → 130×282 ×6。
- **地面を正方形にする補正**: 見下ろし角θのとき、投影の縦方向を `sin θ` で補正します。これで地面の1セルが画面上でも正方形（既存の32×32タイルと同じ配置）になります。高さ方向は `cot θ` 倍で見えます（θ＝50°で0.84）。
- **Shimmer対策**:
  - カメラは、低解像度で1画素分の幅（`0.5/px`、北方向は補正込み）単位でしか動かしません。
  - Playerの表示位置も同じ単位にそろえます。
  - WebGLのモバイル相当画面で歩行中を連続撮影すると、背景の移動量はすべて6px（＝低解像度1画素）の倍数でした。静止した背景・木・家で、ずらして重ねたときに違う画素は0pxでした（差分はPlayer自身だけ）。
- **WebGL CSSとPlayer Spriteには触れていません。** 既存のTemplateが出すCanvasサイズをそのまま受け取って、倍率を計算しています。
- 注意: `Graphics.DrawTexture` を使うとWebGLで上下が反転しました。`Graphics.Blit`（UVのScale/Offset指定）に変えて解決しています。

## 10. 3D Third-Person View（確定：試作で実装）

- 透視投影です。FOVは横長画面で42°、縦長画面で58°です。
- 屋外: 距離2.5、Pitch 26°。屋内: 距離2.2、Pitch 50°。注視点の高さは0.5です。
- カメラのYawは、Playerの向きを追いかけます（SmoothDampAngle、0.3秒）。
- 遮蔽物の扱い: カメラからPlayerの頭と足元への線分にかかる遮蔽Object（家・木・壁セル）は、`ShadowsOnly` にして本体だけを隠します。影は残ります。
- 屋内の比較オプション「近づける＋室内制限」: カメラが部屋の外へ出そうなときは、まず見下ろし角を上げてカメラを上へ逃がします。それでも外に出る場合だけ、距離を縮めます。

## 11. Camera Follow（確定）

- カメラが追いかけるのは、**補間中の連続した位置**です（セル単位で飛ばない）。60fpsで6セル歩いたとき、1フレームあたりの移動は最大0.046でした（自動検査の上限は0.06）。
- 2Dでは、既存と同じSmoothDamp 0.2秒＋マップ範囲＋0.8のクランプを使い、そのうえで画素単位にそろえます。
- 向きが変わったときのYaw追従: 東を向いたPlayerの背後へ、2秒以内に回り込むことを検査済みです。
- 切替時と入退室時は、カメラを目標位置へ即座に合わせます（Snap）。

## 12. 3D入力方向比較（未決定：ユーザーの判断を待つ）

| | 案A 方角固定（試作の初期値） | 案B カメラ基準 |
|---|---|---|
| 意味 | W＝北、S＝南（2Dと完全に同じ） | W＝画面の奥、A/D＝画面の左右 |
| 予測しやすさ | カメラが回っている途中でも結果は常に同じ | 押した瞬間のカメラ目標Yawで固定（押している間は変えない）すれば往復しない |
| 問題点 | 南を向くと、カメラがPlayerの正面側へ回り込み、Wが「画面の手前」になる | Sを押すと振り向いてカメラが180°回るため、その後のSは逆向きになる。Dの連打は「右へ曲がる」操作になる |
| 2D／3Dの一貫性 | 同じキーで同じセルへ進む | 表示ごとに意味が変わる |

- Phase 1の判断材料: 案Bは、押した瞬間の向きに固定する方式（`PocInput.Resolve`）で、押し続けても往復しないことを検査済みです。ただし「背後カメラ」との組み合わせでは、Sが実質「振り向き」になります。
- **最終方式はユーザーの承認を待ちます。**

## 13. Object配置（確定）

- 全Object Instanceについて、表示ルートが `ToWorld(RootCell)` と一致することを検査済みです。
- Blenderの家は5×2のFootprint内に収まり、はみ出しは軒の分（約0.12）だけでした。
- 自動生成の箱は、Blocked Cellの範囲を超えません。

## 14. MAP EDITOR連携（確定：Fixtureで検証）

1. `tools/Poc2D3DFixture`: **MAP EDITOR v0.6のCore**（`MapSession.MoveObject`、無変更）を使い、正式Mapの**コピー**上で木 `obj_b8083…` を (5,1)→(6,1) へ動かしました。保存先は `unity-project/POC2D3DFixtures/`（Assetsの外）です。
2. 正式JSONのSHA-256が変わっていないことを、ツール自身が確認しています。
3. Unity側では、FixtureのJSONを読んでメモリ上に `MapDefinition` を作り、共通Grid→2D／3Dへ反映しました（アセットは書き込みません）。
   - Blockedの差分は {(5,1)}→{(6,1)} だけでした。
   - 同じGameObjectが2D・3Dの両方で新しいセルに表示されました。
   - 衝突は新しいセルで起こり、元のセルは歩けるようになりました。

MAP EDITORそのものを3D表示に対応させる作業は、Phase 1の範囲外です。

## 15. Interaction（確定）

- `MapRuntimeLoader2D.CreateObject` と同じ振り分けを使います（`SharedWorldModel.UsesAuthoredActions`）。
  - 複数セルのObject・sign・well・Behavior Noneの物 → 作成済みのAction Point
  - それ以外 → Examineルール（向いている方向の隣のセル）
- Aボタン（前方を調べる）とクリック（Rayで選んだObject Instance→同じ距離判定）は、2D／3Dで同じ結果になります。WebGLで、木は2D・3Dとも「き。」、看板はsignTextの「こっちは東。」でした。
- メッセージは、既存の遊びメモ書きビットマップFontで描いています。

## 16. AUTO（設計）

- AUTOは1つだけにします。`AutoModeController` の判断ロジックは表示と関係がないので、`player.TryStep` → `SharedWorldSession.TryStep`、`world.CanEnter` → 共通Gridに差し替えれば、2D／3Dの両方でそのまま動きます。
- 試作での確認: 既存の `GridPathfinder2D` の経路が、共通Grid上で共通Playerを2D・3Dとも同じセル列で歩かせました。既存の `IndoorAutoRoutePlanner2D` も共通Gridを受け付け、41か所の目的地を出しました（出口と家具を除く）。
- `AutoModeController` 本体は、まだ接続していません（Phase 2以降で接続）。
- 3D専用のAUTOは作りません。

## 17. Crow（設計）

```
CrowState（新設・描画なし）: cell, facing, activity（GroundIdle..LandingGround）,
                              flight leg（グリッド座標上の始点・終点、高さ曲線、開始時刻・所要時間）,
                              perch tree instanceId, occupancy
CrowPresenter2D: 既存Sprite（idle/walk/flight）＋草のマスク
CrowPresenter3D: Blenderモデル（Phase 2以降）。flight legを ToWorld＋高さで描く
```

- 木への止まり位置は、Spriteの絵の位置ではなく「Tree Instance＋Presentation Metadataの止まり位置オフセット（2D用と3D用）」にします。
- 話しかけた後の飛行（ver3.0 Preview）は、`IExamineReaction` の経路をそのまま `CrowState` へ移します。

## 18. World Time（確定）

- 時計は既存の `WorldTimeController` 1つです（試作でもそのまま使用し、作り直していません）。
- 2D: 既存の `WorldNightOverlay2D.TargetAlpha`、`FadeSeconds`、夜色をそのまま使い、拡大後の画面に重ねます。
- 3D: 平行光源の強さと色、環境光、空の色を、同じ2.5秒のFadeで変えます。重ね色は使いません。
- 片方の表示にしか効かないようにしているため、暗くなる効果が二重にかかることはありません。
- WebGL実機で、実時間の朝⇄夜の切替とFade途中の状態（blend 0.83）を観測しました。
- UIは暗くしません（IMGUIは最後に描画されます）。

## 19. Life Log／Save（設計）

- 現在保存しているもの: `HALKA_WORLD_life_log_v1`（歩数・時間）、`HALKA_WORLD_time_cycle_anchor_v1`。AUTOとPlayer位置は保存していません。
- 歩数は `StepCompleted` だけで増えます。表示の切替は歩数を増やさないことを検査済みです。
- 提案: 表示モードは端末ごとの好みとして `HALKA_WORLD_view_mode_v1`（`2d`／`3d`）に保存します。既存のキーと形式は変えません。

## 20. WebGL最適化（計測結果からの方針）

- POCのBuildフォルダは29.26MB（非圧縮）で、現行Previewの29.59MBとほぼ同じです。3DモデルはFBX3つで約86KBでした。
- 最大の項目は既存の `Resources/asobi_ui_atlas.png`（5.0MB）です。公開版では圧縮配信（Brotli）を検討します。
- 1フレームあたり（屋外）: 2D描画は51 Draw Call、3D描画は33 Draw Callでした。地面は1メッシュにまとめています。
- 自動生成の箱が1 Object＝1 Draw Callになっています。本番では、静的Objectをまとめる（Static Batching、またはMeshの結合）と減らせます。
- 影はHard Shadowで、トゥーンシェーダーが二値化しています。端末によっては、影の距離を短くしたり影なしにしたりする品質切替を検討します。

## 21. 既存ゲームからの移行方針（提案）

1. **Grid統合**: `GridWorld2D.CanEnter` と `InteractionRouter.TryInteractAhead` を、共通Grid（データ）判定へ移します。挙動は変えません（Version18〜30Checksで回帰確認）。
2. **状態の集約**: `PlayerMover` を、`SharedWorldSession` 相当の「Player State」と「2D Presenter」に分けます。Player Spriteと承認済みの動き（`CharacterVisual`）は、2D Presenterとしてそのまま残します。
3. **表示の切替**: 既存のMenuに「表示 2D / 3D」を1行だけ追加します。試作のデバッグ用パネルは本編に入れません。
4. **3D Presenter**: Blender素材を、Presentation Metadataで順番に割り当てます。素材がない物は箱で仮表示します。
5. **2Dの見た目**: 2D表示を「既存Sprite」と「3Dドット化（方式A）」のどちらにするかは、ユーザーの判断を待ちます（22・23項）。

## 22. 技術リスク

| リスク | 内容 | 対策・状態 |
|---|---|---|
| ドット絵の品質 | 3Dから低解像度で描くと、既存Spriteの手描きの細部（顔・髪）には届かない。16px/セルではPlayerは低解像度で約11×27画素（既存Spriteは約48×62画素） | Phase 2でキャラクター専用の高密度化か、方式Bの併用を検討 |
| 24px/セルの拡大率 | 576pxの画面では18セルにならず、24セル（×1）か12セル（×2）になる | 16と32が整数倍で扱いやすい |
| 屋内3D | 壁の高さ1.1では、背後カメラで大きく映る | 遮蔽物を隠す処理と、屋内用の見下ろし角で対処。壁の高さはデザインの判断 |
| 案Bの操作 | 背後カメラでは、Sが振り向き操作になる | 12項。ユーザーの判断 |
| Crowの分離 | 飛行の位置計算が2DのSprite位置に依存している | 17項の状態分離 |
| ブラウザ非表示時 | rAFが止まり、ゲームが進まない | 既存と同じ（AUTOは前面にある時だけ動く） |
| 物理スマホ | 未確認（今回の計測はデスクトップGPU上のheadless Chrome） | Phase 2で実機計測 |

## 23. 推奨する正式構造（提案）

```
Assets/
  Scripts/World/Shared/      SharedWorldModel, Grid(データ), WorldSession, CrowState, Interaction判定
  Scripts/Presentation/2D/   既存Sprite Presenter（CharacterVisual等）, PixelView（方式A採用時）
  Scripts/Presentation/3D/   ThirdPersonCamera, ModelBinding, Occluder
  Content/Models/            Blender Export（FBX）
  Content/Maps/Authoring/    *.hwmap.json（Source of Truth、v4のまま）
                             presentation_catalog.hwpresentation.json（新設：3D表示専用の値）
blender/                     scripts / sources(.blend) / exports
```

- ゲーム処理は1つにまとめ、表示は複数持てる形にします。表示側は状態を読むだけで、書き込みません。
- **2D表示方式の推奨**（ユーザーの決定待ち）:
  - 地形・建物・家具: 方式A（3Dを実時間でドット化）。Blenderの素材1つが2D・3Dの両方に使えて、更新の手間が最も少ないためです。
  - ﾊﾙｶﾁｬﾝ（とCrow）: 「既存の手描きSpriteを2Dで使い続ける」か「方式Bで事前に描き出したSprite」か。どちらにしても、手描きの品質を守る経路を残すことを推奨します。

| 比較 | A: 実時間で低解像度描画 | B: BlenderからSpriteを事前生成 |
|---|---|---|
| 処理負荷 | 3Dを1回描く（試作では60fps） | 2Dと同じで軽い |
| ドット絵の品質 | 形は正確だが、手描きの味は出にくい | 生成後に手で直せる |
| Animation制作 | Rigを1回作れば全方向に使える | 方向×コマ数だけ描き出しと修正が必要 |
| 2D／3Dの一貫性 | 完全に一致する（同じGameObject） | 描き出し時点の形。後でずれる可能性がある |
| モデル修正時の更新 | 自動 | 描き出し直しと再確認が必要 |
