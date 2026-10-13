# はるかわーるど ver3.0 Phase 2 レポート：地形・自然オブジェクトのローポリ3D化

作業日: 2026-10-14（日本時間）／担当: Claude
ブランチ: `claude/ver3.0-2d3d-phase2`（Phase 1 `claude/ver3.0-2d3d-phase1` の `6a5166c` から分岐）
worktree: `C:\Users\owner\Documents\ChatGPT\HALKA_HP_2D3D_PHASE2`

**結論**: 現在の屋外マップ first_field の地面・土の道・草・木・花・石を、Blender製のローポリ3Dで再現しました。どれもMap JSON v4から自動で配置されます。Phase 1の2D/3D共通ワールドの上で、2Dドット表示と3D三人称表示の両方を、WebGL（PC相当・スマホ相当）で操作して確認しています。

ゲームのルールは何も変えていません。Grid移動、Object位置、Blocked Footprint、Interaction、AUTO、Playerの歩き方はそのままです。正式公開もしていません。

名称について: 新しい文書とPreview画面の表示だけを「はるかわーるど」にしました。クラス名・Map ID・Save Key・Definition ID・フォルダ名・公開HPは変えていません。

---

## 1. 開始時のGit状態

| 項目 | 値 |
|---|---|
| Phase 1 worktree | `HALKA_HP_2D3D_PHASE1`、`claude/ver3.0-2d3d-phase1` @ `6a5166c`、変更なし |
| 元の作業ツリー | `HALKA HP`、`codex/ver3.0-preview` @ `d3f81ac`。` M GrassDecoration.prefab`、`?? backup-v05-20261006-174557/`（開始時からある2件） |
| origin/main | `13462fe`（ver2.9） |
| ローカル main | `d039acf` |
| Phase 2 | 新しいworktree `HALKA_HP_2D3D_PHASE2` に `claude/ver3.0-2d3d-phase2` を作成。Libraryはコピー |

## 2. Backup

repo外の `C:\Users\owner\Documents\ChatGPT\HARUKA_WORLD_ver3.0_PHASE2_BACKUP_20261014`（231MB）に保存しました。

- `repo-all-refs.bundle`（全ref、verify済み）
- `git-refs.txt`、`git-status-original.txt`、`git-diff-original.patch`（GrassDecorationの差分を含む）、`git-status-phase1.txt`
- `phase1-webgl-build/`（Phase 1のWebGL Buildをそのまま）
- `files/`:
  - POC一式、Blender（スクリプト・.blend・Manifest）、tools
  - 正式Map JSON・Catalog・Map Cache、Scene、Player/World Script（Crowを含む）、World Sprite、WebGL Template、ProjectSettings、Packages
  - Phase 1の文書2本
- `official-map-sha256-before.txt`

## 3. 使用したBlenderのバージョン

Blender 5.2.0 LTS。同梱のPython 3.13（bpy）を `--background --factory-startup` で実行しました。

Unityは6000.3.10f1（Built-in RP、Gamma）のままです。Render Pipelineは変えていません。

## 4. Phase 1からの変更点

| 区分 | 変更 |
|---|---|
| Blender | `halka_lowpoly.py` に形状関数を追加（平面ポリゴン、厚みのある板、草の葉、ゆらぎのある塊、Anchor用Empty）。新しく `build_phase2_environment.py` と `render_phase2_previews.py` を作成 |
| Presentation | `PocPresentationCatalog` を拡張。バリエーション（Instance IDから決める）、Perch Anchor名、影の有無、仮素材フラグ、地面・道・草の設定（`PocEnvironmentPresentation`）、安定ハッシュ（FNV-1a）を追加 |
| 地面 | 新しく `PocEnvironmentBuilder` を作成。Map Bounds・Dirt Surface・Markerから、地面・道・草を1つのメッシュに組み立てる。屋内はPhase 1の生成床のまま |
| 配置 | `PocWorldPresenter` がバリエーションの選択・PerchAnchor・影の有無に対応。作り直すたびに実行時の静的バッチ化をかける |
| 表示 | 右上にBitmap Fontで「はるかわーるど ver3.0 Phase 2 Preview」と表示。HTMLのtitleも同じ（repo外のBuildのindex.htmlだけを書き換え、共通Templateは変えない） |
| MAP EDITOR連携 | Fixtureツールが、木・花・石を各1マス動かしたコピー（`first_field_nature_moved.hwmap.json`）も作る |
| 検査・撮影 | 新しく `Phase2EnvironmentChecks`（130件）と `Phase2Capture` を作成 |
| Build | 出力先を `HARUKA_WORLD_ver3.0_PHASE2_PREVIEW` に変更（Phase 1のBuildは別フォルダにそのまま） |

Phase 1の仕組み（`SharedWorldSession`、`GridSpace3D`、`PocViewSwitcher`、Pixel View、Third-Person Camera、Input、Time）は、作り直さずにそのまま使っています。

## 5. 制作したモデルの一覧

| モデル | 種類 | 用途 |
|---|---|---|
| ground_grass_a / b / c | 草地 | 1マス（0.5×0.5）の地面。ほぼ同じ3色を、セルのハッシュで選ぶ |
| ground_dirt_full / edge / corner | 土の道 | 1/4マスの部品。Unityが周りのSurfaceを見て、回転・反転して組み立てる |
| grass_tuft_a / b / c | 草の房 | 葉4〜6枚の小さな房。当たり判定なし |
| tree_basic | 木 | 幹・根の張り・枝2本・葉の塊9個。PerchAnchor（Empty）付き |
| flower_basic | 花 | 茎・葉2枚・白い花弁5枚（茶色の縁）・黄色の芯。花の向きを南に28°傾けた |
| stone_basic_a / b / c | 石 | 丸みのある不規則な灰色の石。Instance IDで3形状から選ぶ |

## 6. 各モデルの三角形数

| モデル | 三角形 | 頂点 | 寸法（幅×奥行×高さ, unit） |
|---|---:|---:|---|
| ground_grass_a/b/c | 2 | 4 | 0.50×0.50×0 |
| ground_dirt_full | 2 | 4 | 0.25×0.25（高さ +0.004） |
| ground_dirt_edge | 5 | 7 | 0.23×0.25 |
| ground_dirt_corner | 4 | 6 | 0.21×0.21 |
| grass_tuft_a / b / c | 15 / 12 / 18 | 20 / 16 / 24 | 約0.2×0.13×0.2 |
| tree_basic | 504 | 286 | 1.41×1.01×1.78 |
| flower_basic | 168 | 102 | 0.26×0.21×0.35 |
| stone_basic_a / b / c | 80 / 80 / 80 | 42 | 0.41×0.32×0.28 ／ 0.35×0.36×0.28 ／ 0.44×0.28×0.23 |

first_fieldを組み立てた後の三角形数は、地図全体で11,388（地面・道・草200房・木・花・石・仮の家・仮の箱を含む）です。

## 7. 各モデルのマテリアル数

すべて1です（Blender側 `HALKA_VertexColor` → Unity側の共有 `POC_ToonVertexColor`）。テクスチャは使っていません。色はすべて頂点カラーです。

## 8. Blender Sourceの保存先

- `blender/sources/phase2/ground_grass.blend`、`ground_dirt.blend`、`grass_tuft.blend`、`tree_basic.blend`、`flower_basic.blend`、`stone_basic.blend`
- 再生成: `blender/scripts/build_phase2_environment.py`（共通の補助は `halka_lowpoly.py`）
- プレビュー: `blender/scripts/render_phase2_previews.py` → `blender/exports/previews/phase2/<model>_{south,east,north,west,top,game}.png`（84枚）
- Manifest: `blender/exports/phase2_assets_manifest.json`

## 9. FBX書き出しの結果

`unity-project/Assets/POC2D3D/Models/Phase2/*.fbx` の14ファイル（合計約320KB）です。設定はPhase 1と同じです（-Z forward / Y up / Apply Transform、面ごとの法線、sRGB頂点色）。

木だけは、子Emptyの `PerchAnchor` も一緒に書き出しています。

## 10. Unity Importの結果

`Phase2EnvironmentChecks` で、14モデルすべてについて以下を確認しました。

- Scaleが1、回転と原点が0
- 共有マテリアル1つ
- 頂点色と法線がある
- 三角形数がManifestと一致

個別の確認:
- 草地: ちょうど1マス、高さ0
- 道の部品: 北東1/4マスの範囲内、高さ+0.004
- 草: 高さ0.25未満で、マスの中に収まる
- 木:
  - 幅1.2〜1.5（約3マス）、高さ1.78
  - 幹は根元セルの中心
  - 根の張りは地面に最大3cm沈む（意図的）
  - PerchAnchorは (−0.17, 1.70, 0)
- 花: 1マス内、高さ0.35。花の面は上向きで、南（カメラ側）へ傾いている
- 石: 1マス内、高さ0.35未満、地面に接している

## 11. 地面の実装

`PocEnvironmentBuilder` が **Map Boundsと周囲8マスの余白** に、Blender製の草地タイルを1マスに1枚ずつ敷き、1つのメッシュにまとめます（first_fieldで1,073枚）。

- 余白の部分は、少し暗い色（×0.9）で地図の境界を示します。
- 色: 現行2Dの地面（白×(0.85,0.91,0.72)＝#D9E8B8）に合わせました。
- 高さ: 地面は y=0、道は y=0.004 です。Player・Object Rootの高さと、Gridの座標は変えていません。検査で、地面の全頂点がこの2つの高さのどちらかにあることを確認しました。
- 当たり判定は持たせていません。通行判定はPhase 1と同じく共通Gridだけで決めます。

## 12. 土の道（Dirt Surface）の実装

- **Map JSONのDirt Surfaceセル（62マス）だけを道にしています。** World Objectは追加していません。検査で、JSONのDirtセルと道のセルが完全に一致することを確認しました。
- 道の1マスは、1/4マスの部品4つでできています。各1/4の部品は、その角に接する縦・横の隣が道かどうかで決まります。
  - 両方とも道 → full
  - 片方だけ道 → edge（草地側に波打った縁）
  - 両方とも草地 → corner（丸い角）
- 道の縁は、マスの境目では必ず同じ位置（内側0.04）で終わるように作りました。そのため、隣り合う部品はすき間なくつながります。first_fieldの内訳は、full 64・edge 284・corner 28です。
- 4方向の道の端（road_north / east / south / west のMarker）からは、地図の外へ道をまっすぐ8マス延長しています（合計32マス）。2Dで道が画面の外へ続いて見える表現を、3Dでも再現するためです。Markerと通行範囲は変えていません。
- 道の形状がDirtセルの中に収まっていることも、三角形単位で検査しました。

## 13. Grass Decoration（草の房）の実装

- 生える場所: 2Dの草と同じ規則 `MapPlacementRules.HasGrass`（既存のまま）で決めます。道・Blockedセル・Objectの根元には生えません。
- 数と場所:
  - 1マスにつき80%の確率で1房。位置は±0.11ずらし、向きと大きさ（0.85〜1.15）もばらつかせる。
  - 道に面した草のマスには、道の側へ55%の確率で小さな房をもう1つ置く（道端の草として、境界を柔らかく見せるため）。
  - 結果は200房（うち道端51）、草のマス1つあたり1.09房です。
- 再現性: 位置は Map ID＋セル＋用途名 のFNV-1aハッシュで決めます。同じMapから作り直すと、草も地面も完全に同じになることを検査しました。
- 当たり判定はなく、クリックの対象にもなりません。影も落としません（軽さのため）。1つのメッシュにまとめています。

## 14. Treeの実装

- `tree_basic` を正式モデルに差し替えました。Phase 1の仮Treeはファイルとして残していますが、もう割り当てていません。
- 現行Spriteとの比較で、樹冠が幹を隠しすぎて淡すぎることに気づき、2点を直しました。
  - 樹冠を持ち上げ、手前下の塊を外した → 2Dでも幹が見える。
  - 緑を濃くした（#B3D27F／#93BD72／#6A9A6C／#4F7D63）。
- Blocked Footprintは、Catalogどおり根元セルだけです。樹冠の下のセルは通れることを検査しました。
- 将来のカラス用に、Blenderで `PerchAnchor` を作り、Presentation Metadataの `perchAnchorName` で参照しています。4本すべてで、`PocObjectView.PerchAnchor` が取得できます。Crow AIと飛行処理は変えていません。

## 15. Flowerの実装

- `flower_basic` の10本すべてを、正式なRoot Cellに配置しました。ver2.7で追加した東・西の花も含みます。
- 現行Spriteの「白い5枚花弁・黄色い芯・茎と2枚の葉」を再現しました。
- 花弁は厚み0.018の板にして、側面を茶色にしています。これが現行Spriteの輪郭線の代わりになり、淡い緑の地面の上でも白い花弁が埋もれません。
- 花の向きは南へ28°傾けました。2Dの見下ろし（50°）からも、3Dの背後カメラからも、花の面が見えます。

## 16. Stoneの実装

- `stone_basic` の7個すべてを配置しました。
- 3つの形状から、Instance IDのハッシュで決めます。作り直しても同じ形になることを検査しました。
- 今のMapでは、7個の割り当てがcが5個・aが2個で、bはたまたま使われていません。
- 色は現行Spriteの灰色系（#C4C1C7／#AEABB4／#8A889A／#5C5E77）で、陰影は少し強めにしています。

## 17. 元Spriteとの比較

`CAPTURES/sheets/compare_2d_{tree,flower,stone,grass_and_road}.png` に、同じ1280×720の画面・同じセルから切り出して並べました。左から、現行2D Sprite、3D→2Dの16px/マス、3D→2Dの32px/マスです。

- 木: 形（丸い樹冠、根の張った幹）と色は近くなりました。ただし、現行の手描きの黒い輪郭線と細かい葉の陰影はありません。
- 花: 32px/マスなら茎と葉まで読めます。16px/マスでは、白い花と黄色い芯が分かる程度です。
- 石: 形・色・大きさとも近いです。輪郭線はありません。
- 草: 現行2Dは、各マスに大きなセージ色の房があります。Phase 2は、指示どおり「落ち着いた草地」にしたため、細く控えめな房です。現行より草の存在感が弱く見えます（30項で確認をお願いします）。

## 18. 正式Mapとの配置一致

- 木4・花10・石7のすべてで、次の3点が一致していることを検査しました。
  - Instance ID・Definition・Root CellがMap JSONと一致
  - 表示位置が `ToWorld(RootCell)` と一致
  - Blenderモデルである（仮の箱ではない）
- 4方向の景観の違い（北に木、東に花、南に石、西は空いた草原）は、配置データをそのまま使っているので維持されています（`sheets/01`、`06`）。

## 19. 当たり判定の維持

通行判定は、Phase 1と同じく共通Grid（`MapPlacementRules`）だけで決めています。メッシュのColliderは使っていません。

- 木・花・石の根元セルは通れない。木の樹冠の下は通れる。
- 草と道は通れる。
- 地図の範囲・Gridは変わっていない。

WebGL上でも、木・花・石の前で上キーを押し続けて、進めないこと（歩数が増えないこと）を確認しました（`sheets/09`）。

## 20. Interactionの維持

- 検査: 正面のマスが歩ける物はすべて、文言が一致しました（木4/4「き。」、花10/10「はな。」、石7/7「いし。」）。正面が塞がっている物は0件でした。
- 2Dと3Dで同じ花・石・木をクリックすると、同じObject Instanceが選ばれます。
- WebGLのPC:
  - 2Dのクリック: 木・花・石とも反応
  - 3Dのクリック: 木が反応
- WebGLのスマホ相当（Aボタン）:
  - 2D: 花・石とも反応
  - 3D: 花・石とも反応

## 21. MAP EDITOR連携

`tools/Poc2D3DFixture`（MAP EDITOR v0.6 Coreの `MapSession.MoveObject`、無変更）で、正式Mapの**コピー**の上で次の3つを1マスずつ動かしました。

- 木 `obj_b8083…` (5,1)→(6,1)
- 花 `obj_3420672f…` (6,−1)→(7,−1)
- 石 `obj_08973c34…` (9,3)→(10,3)

3つとも、次のことを検査で確認しました（`sheets/10`）。
- 2Dでも3Dでも新しいセルに表示される（同じGameObject）
- 当たり判定が新しいセルへ移り、元のセルは通れるようになる
- 新しいセルには草が生えない

モデル側に地図の座標は埋め込んでいません。

## 22. 2D Pixel Viewの比較

同じMap・同じ物の位置・同じカメラ中心で比較しました（`sheets/01`、`compare_2d_*`、`sheets/08`）。

| 設定 | PC 1024×576 | スマホ相当 780×1688 | 見える範囲 |
|---|---|---|---|
| 16px/マス | RT 512×288 ×2 | RT 130×282 ×6 | 縦18マス（現行と同じ） |
| 32px/マス | RT 1024×576 ×1 | RT 260×563 ×3 | 縦18マス（現行と同じ） |

- 16px/マスはドット絵らしい粗さが出ます。32px/マスは細部（花の茎、木の幹の陰影）まで読めます。どちらの密度でも、見える範囲は変わりません。
- 見下ろし角と「地面を正方形にする補正」のON/OFFは、Phase 1の比較（40/50/60/90°）と同じ仕組みで、パネルから切り替えられます。今回の初期値は50°・ONです。
- **2D表示方式の最終決定はしていません**（30項）。

## 23. 3D三人称表示の比較

`sheets/03`〜`07`。

- 木は高さ1.78で、仮Player（0.93）の約2倍。カメラ（Pitch 26°）からは樹冠の上部が画面の外に出ることがあります。
- 花・石は足元に小さく見えます。真正面の1マス先の物はPlayerの体に隠れます。この場合、Aボタンでは調べられますが、クリックでは選べません。
- 草の密度は控えめで、道ははっきり見えます。

仮Playerの大きさは、正式キャラクターの寸法として確定していません。

## 24. 朝夜の見た目

時間の計算（既存の `WorldTimeController`、朝10分・夜10分）は変えていません。

- 2D: 既存と同じ夜色を重ねています（屋外α0.33）。
- 3D: 平行光源・環境光・空を暗くします。

夜でも木・花・石・道・草は見分けられることを確認しました（`sheets/07`。Unity出力とWebGLの両方）。

## 25. PC WebGL

repo外のBuildを、別に起動したheadless Chrome（1280×720、GPU: AMD Radeon内蔵／D3D11）で操作しました。次の順に確認しています。

1. 2Dで起動
2. 朝に固定
3. 16px→32px→16pxの全景
4. 木の正面まで12歩 → 上キーを押し続けても進まない → クリックで「き。」
5. 花の正面 → 進まない → 「はな。」
6. 石の正面 → 進まない → 「いし。」
7. 3Dへ切替（Cell 3,−5・歩数20を維持）
8. 3Dで木の正面へ → クリックで「き。」
9. 夜の3Dと2D

切替を含む歩行動画も撮りました（34項）。

## 26. Mobile Preview

DevToolsのエミュレーション（390×844、DPR 2、タッチ、`?touchControls=1`）で確認しました。

- D-padで花の正面まで10歩 → Aで「はな。」（2D）
- 3Dへ切替 → Aで「はな。」
- D-padで石の正面へ → Aで「いし。」（3D）
- 2Dへ戻す → Aで「いし。」
- 夜の2D・3D

**物理スマートフォンでは確認していません。**

## 27. Performance計測（最終ビルド）

計測は、AMD Radeon内蔵GPUのPC上で動くheadless Chrome 154 で行いました。FPSはrAFの5秒平均、Draw Call等はPOC内の `ProfilerRecorder`（Release WebGL）の値です。

| 条件 | FPS | p95 / 最大 | Draw Calls | Batches | SetPass | Tris/frame | Unity Used Memory | JS Heap |
|---|---:|---|---:|---:|---:|---:|---:|---:|
| PC 2D 16px（1024×576） | 60.0 | 16.8 / 16.9 ms | 65 | 64 | 58 | 29,402 | 15.0 MB | 20.2 MB |
| PC 3D（1024×576） | 60.0 | 16.8 / 16.8 ms | 73 | 64 | 58 | 19,018 | 15.0 MB | 20.0 MB |
| スマホ相当 2D（780×1688） | 59.8 | 16.8 / 16.8 ms | 75 | 74 | 68 | 23,188 | 14.9 MB | 10.5 MB |
| スマホ相当 3D（780×1688） | 59.8 | 16.8 / 16.8 ms | 78 | 73 | 67 | 16,516 | 14.9 MB | 10.5 MB |

- Phase 1（仮モデル）は、PC 2D で51 Draw Call、3D で33 Draw Callでした。
- 自然物が増えたことで、静的バッチ化を入れる前は2D 124／3D 114まで増えました。地図を作り直すたびに静的バッチ化をかけ、65／73まで下げています。60fpsは変わりません。
- 地面・道・草は、それぞれ1メッシュです。
- WebGL Build（非圧縮）は29,412,553 bytesで、Phase 1は29,263,147 bytesでした（+149KB）。
- JS HeapはPCとスマホ相当で約2倍違います。GCのタイミングによる差と考えられ、原因は特定していません。WASMヒープ全体と物理スマホの値は計測していません。

## 28. Phase2EnvironmentChecks

**130件成功・失敗0**（`HARUKA_WORLD_ver3.0_PHASE2_LOGS/11-phase2-checks.log`）。

| 検査項目 | 内容 |
|---|---|
| Asset | Manifest・.blend・スクリプトがある |
| Import（14モデル） | Scale・Pivot・頂点色・法線・マテリアル・三角形数 |
| 個別の形状 | 木（幅・高さ・幹の位置・PerchAnchor）、花（大きさ・面の向き）、石（大きさ・接地）、地面・道・草の寸法 |
| 地面 | タイル数がMap Boundsと一致、高さ、当たり判定なし、Boundsが変わらない |
| 道 | Dirtセルと完全に一致、部品数、端の延長、Dirtセルからはみ出さない |
| 草 | 道とBlockedセルに生えない、HasGrassと一致、当たり判定なし、密度、決定的に再生成できる |
| 配置 | 木・花・石の全Instanceが一致、根元だけ通れない、樹冠の下は通れる、PerchAnchor、石のバリエーションが安定、家が仮素材として区別されている |
| Interaction | 全件の文言、2D/3Dのクリックが同じInstanceを選ぶ |
| 切替 | 20回切り替えても状態は変わらず、地面も作り直されない |
| MAP EDITOR Fixture | 木・花・石の移動 |
| 夜 | 2Dのα、3Dが真っ黒にならない |

## 29. Phase 1 PocChecks

**102件成功・失敗0**（`12-phase1-poc-checks.log`）。件数はPhase 1と同じです（Phase 1の検査は変更していません）。

## 30. Version30Checks

成功（`15-version30checks.log`）。ver2.0からver3.0 Previewまでの継承チェックをすべて含みます。

チェックの中の `PrepareScene` で `FirstDay.unity` などが作り直されるので、Phase 1と同じく、テスト後にworktree内で元に戻しました。

## 31. MapAuthoringChecks

成功（`16-mapauthoringchecks.log`、「Standalone Map Contract v0.6: passed」）。

## 32. MAP EDITOR Core Tests

**181/181成功**（`17-map-editor-core-tests.txt`）。件数に増減はありません。

## 33. 比較画像の一覧

場所: `C:\Users\owner\Documents\ChatGPT\HARUKA_WORLD_ver3.0_PHASE2_CAPTURES\`

| ファイル | 内容 |
|---|---|
| `sheets/01_full_map_current_vs_new.png` | 現行2D、新2D 16px/32px、新3Dの俯瞰・三人称・真上（4方向の道） |
| `sheets/02_blender_*.png` | 各モデルのBlender Render（南・東・北・西・上・ゲーム角度） |
| `sheets/03_tree_2d_3d.png`、`04_flower_2d_3d.png`、`05_stone_2d_3d.png` | 2D比較、3D拡大、三人称、夜 |
| `sheets/06_terrain.png` | 草地、交差点、草と道の境界、2D比較、4方向の道 |
| `sheets/07_morning_night.png` | 朝と夜の2D・3D（Unity・WebGL） |
| `sheets/08_mobile.png` | スマホ相当の2D・3D（WebGL・Unity、16/32px） |
| `sheets/09_interaction_collision_webgl.png` | WebGLで進めない・調べられることの確認 |
| `sheets/10_grid_and_map_editor_fixture.png` | Grid表示、MAP EDITORで動かした木・花・石 |
| `sheets/compare_2d_*.png` | 現行Spriteと16px・32pxの拡大比較 |
| `unity/`（30枚）、`webgl/`（元画像） | 各シートの元画像 |

## 34. 動画の一覧

`CAPTURES/haruka_phase2_walk_switch_pc.mp4`（10.8秒、10fps、1024×576）。

2Dで歩く → 3Dへ切替 → 3Dで歩く → 2Dへ戻る → …を、朝の固定で繰り返したWebGLの実画面です。

## 35. Previewの起動方法

Build: `C:\Users\owner\Documents\ChatGPT\HARUKA_WORLD_ver3.0_PHASE2_PREVIEW`（README_POC.txtあり）

```
cd C:\Users\owner\Documents\ChatGPT\HARUKA_WORLD_ver3.0_PHASE2_PREVIEW
python serve_poc.py --port 8891
```

- PC: http://127.0.0.1:8891/
- スマホ相当: http://127.0.0.1:8891/?touchControls=1

Phase 1のBuild（8890）は別のフォルダにあり、そのまま残しています。

## 36. Git Commit

ブランチ `claude/ver3.0-2d3d-phase2` の状態です（Pushはしていません）。

- `dcd395f` Add はるかわーるど Phase 2 low-poly ground and nature assets（140ファイル）
  - Blender（スクリプト・.blend 6本・プレビュー・Manifest）
  - FBX 14本
  - POCのRuntimeとEditorの変更、Phase2EnvironmentChecks、Phase2Capture
  - Fixtureツールとコピー Map
  - 確認用ツール（`compare_crops.py`、README）
- このレポートは、続くCommitに含めます。

本編のファイル（`Assets/Content`、`Scenes`、`Scripts`、`ProjectSettings`、`WebGLTemplates`、`halkaworld/`）は変更していません。

## 37. 最終Git状態

- Phase 2 worktree: レポートのCommit後、`git status` は空です（テストによる再生成分は元に戻し済み）。
- 元の作業ツリー: `codex/ver3.0-preview` @ `d3f81ac`。開始時と同じ2件だけです。
- Phase 1ブランチ・worktreeは変更していません。
- ローカル `main`（`d039acf`）と `origin/main`（`13462fe`）も変更していません。

## 38. 公開版の保護状況

- 正式Map・CatalogのSHA-256は、作業前後で一致しました（first_field `5E2B0380…`、halka_house `52C4D683…`、object_catalog `3E5D7EEC…`、entity_catalog `FA178855…`。worktreeの改行コードでの値）。
- `d3f81ac` と比べて、`Assets/Content`、`Assets/Scenes`、`Assets/Scripts`、`ProjectSettings`、`WebGLTemplates`、`halkaworld/` に差分はありません。
- Player Sprite、CharacterVisual、Save Key、公開HP、GitHub Pagesは変更していません。mainへのMerge、origin/mainへのPushもしていません。
- 「はるかわーるど」の表示は、repo外のPreviewと新しい文書だけです。

## 39. 未解決の問題

1. 物理スマホでの確認と計測をしていない。
2. 草の房が、現行2Dの大きなセージ色の房より控えめで、草原の印象が弱い（指示の「落ち着いた草地」を優先した結果）。
3. 3D→2Dのドット化には、現行Spriteの黒い輪郭線と手描きの細かい陰影がない。
4. 石のバリエーションbが、今のMapではたまたま使われていない（Instance IDのハッシュで決まるため）。
5. 3Dでは、真正面の1マス先の小さな物（花・石）がPlayerの体に隠れ、クリックでは選べない（Aボタンでは調べられる）。
6. 家・看板・ベンチ・井戸・家具は、まだPhase 1の仮表示（仮の家、箱）のまま。
7. 道を地図の外へ延長した表現は、現行2D（地図の外は無地）とは違う。

## 40. Phase 3への提案（承認後に着手）

1. 家（外観）の正式ローポリ化と、入口の凹み・屋内の見え方の調整。Phase 1で作った仮Houseの構造をもとにします。
2. 看板・ベンチ・井戸の3D化。Interaction用のAction Pointはそのまま使います。
3. 屋内の床・壁・家具（ベッド・机・クッション・マイク・イーゼル・棚）の3D化と、屋内カメラの調整。
4. 2D表示の品質を上げる案:
   - 輪郭線の追加（ポストエフェクトで縁を描く、または背面を太らせた輪郭メッシュ）
   - 32px/マスの採用を検討
5. 草の存在感の調整（房を大きくする、密度を上げる、色を明るくするなど）。ユーザーの判断に合わせます。

## 41. ユーザーに確認してほしい点

1. **全体の印象**: `sheets/01` と動画で、「今のはるかわーるどの屋外」と同じ場所だと感じられるか。
2. **木・花・石の形と色**: `sheets/02`〜`05`。特に木の樹冠の形と、花の大きさ。
3. **草の量と見え方**: 今の控えめな草でよいか、現行2Dのように存在感を強めるか（39項 2）。
4. **土の道**: 波打った縁と丸い角、地図の外へ続く表現でよいか（`sheets/06`）。
5. **2Dのドット密度**: 16px/マスと32px/マス、どちらの方向がよいか（`compare_2d_*`）。最終決定はまだしていません。
6. **3Dの木の大きさ**: 仮Playerとの比率（約2倍）でよいか。
7. **Phase 3の開始**: 40項の計画に沿って、家と家具に進んでよいか。

承認をいただくまで、Phase 3には進みません。
