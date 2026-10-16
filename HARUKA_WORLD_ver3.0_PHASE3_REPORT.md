# はるかわーるど ver3.0 Phase 3 レポート：家・屋内・全家具・設備のローポリ3D化

作業日: 2026-10-16（日本時間）／担当: Claude
ブランチ: `claude/ver3.0-2d3d-phase3`（Phase 2.1 `claude/ver3.0-2d3d-phase2-1` の `cc72dc1` から分岐）
worktree: `C:\Users\owner\Documents\ChatGPT\HALKA_HP_2D3D_PHASE3`

## 結論

正式Mapで使われている建物・家具・設備を、すべてBlender製のローポリモデルにしました。屋外と屋内、2Dと3Dの両方で、同じWorld Dataから表示しています。ローカルのWebGL（PC相当・スマホ相当）で操作して確認しました。物理スマートフォンでは確認していません。

| 項目 | 結果 |
|---|---|
| 屋外 | 家・ベンチ・看板（5本）・井戸をBlender製にしました。Phase 2の木・花・石・地面はそのままです |
| 屋内 | 床・壁・出入口・ベッド・机・クッション・録音マイク・イーゼル・棚をBlender製にしました |
| 未3D化のObject | **0件**。正式Mapで使われている13種類すべてにBlenderモデルがあります（ﾊﾙｶﾁｬﾝとCrowは対象外） |
| 家の入口（最重要） | 扉は入口セルの奥にあり、手前は歩いて入れる「くぼみ」です。3Dでは、くぼみへ一歩入ったところで屋内に切り替わります |
| 屋内3Dのカメラ | カメラ側の壁だけを低く切ります（ドールハウスのような見え方）。4方向とも、ﾊﾙｶﾁｬﾝと家具が見えます |
| 2D | 共通の3Dモデルをドット化し、Phase 2.1の輪郭線・元のﾊﾙｶﾁｬﾝSprite・木の半透明をそのまま使っています |
| 検査 | 新しい `Phase3BuildingFurnitureChecks` 88件と、既存の検査すべてに合格しました |
| 性能 | PC・スマホ相当とも、屋内外・2D/3Dの全条件で約60fps。Buildは +84KB（+0.28%） |

ゲームのGrid・当たり判定・Interactionの条件・時間計算・正式Map・Player Sprite・保存キーは変えていません。公開もしていません。

確認していただきたい点が2つあります（52項）。
- 3Dで家から出た直後は、カメラがﾊﾙｶﾁｬﾝの後ろ（北側＝家の向こう）にあるため、家全体が隠れて影だけになります。一歩動くと元に戻ります。
- スマホの縦画面の3Dでは、横方向に見える範囲が狭いため、ﾊﾙｶﾁｬﾝが大きく映ります。Phase 2.1の屋外と同じ性質です。

---

## 1. 開始時Git状態

| 項目 | 値 |
|---|---|
| Phase 2.1 worktree | `HALKA_HP_2D3D_PHASE2_1`、`claude/ver3.0-2d3d-phase2-1` @ `cc72dc1`、変更なし |
| Phase 1 ／ Phase 2 | `claude/ver3.0-2d3d-phase1` @ `6a5166c` ／ `claude/ver3.0-2d3d-phase2` @ `7d5dbf9`、どちらも変更なし |
| 元の作業ツリー | `HALKA HP`、`codex/ver3.0-preview` @ `d3f81ac`。` M GrassDecoration.prefab`、`?? backup-v05-20261006-174557/`（以前からある2件） |
| origin/main ／ ローカル main | `13462fe` ／ `d039acf` |
| Phase 3 | 新しいworktree `HALKA_HP_2D3D_PHASE3` に `claude/ver3.0-2d3d-phase3` を `cc72dc1` から作成 |

## 2. Backup

repo外の `C:\Users\owner\Documents\ChatGPT\HARUKA_WORLD_ver3.0_PHASE3_BACKUP_20261016`（251MB）に保存しました。

- 全refのbundle `repo-all-refs.bundle`（`git bundle verify` 済み）
- `git-refs.txt`（全ブランチ）、`git-status-all.txt`（各worktreeの状態）
- `git-diff-original.patch`: 元の作業ツリーの差分です。GrassDecoration.prefab の差分は改行コードだけなので、patchは空になっています。
- `official-map-sha256-before.txt`（正式Map・Catalog 4ファイル）、`player-sprite-sha256-before.txt`（Player Sprite 50ファイル）
- `phase2_1-webgl-build/`: Phase 2.1のWebGL Build
- `files/`（573ファイル）: POC一式（Runtime・Editor・Data・Scene・Materials・Models・Shaders）、Blender（scripts・sources・exports）、tools（MAP EDITOR・Site Editor・Fixture・poc2d3d ほか）、`Content/Maps`（正式Map・Catalog）、`Content/Character`（Player Sprite）、`Scripts`（Player・Camera・World）、各Phaseのレポート・設計書

既存のユーザー編集（GrassDecoration.prefab、backup-v05）は削除も変更もしていません。

## 3. Blender制作方式

Phase 1〜2.1と同じ方式です。

- Blender 5.2.0 LTS を `--background --factory-startup` で動かし、Pythonだけでモデルを作ります。手作業の工程はありません。
  - 生成: `blender/scripts/build_phase3_buildings.py`（744行）。共通ヘルパーは、Phase 1から使っている `halka_lowpoly.py` です（今回は変更なし）。
  - プレビュー: `blender/scripts/render_phase3_previews.py`
- 形は、箱・角柱・棒・断面をつないだ形の組み合わせです。角ばったローポリにしています。
- 色は頂点カラー（`Col`、sRGB、面の角ごと）です。色は `Assets/Content/World` の元のSpriteから拾っています（読むだけで、Spriteは変えていません）。
- Materialは1つだけです（`HALKA_VertexColor`。UnityではPOCの `POC_ToonVertexColor` に置き換えます）。
- 座標: Blenderの+X＝東、+Y＝北、+Z＝上。1 Blender単位＝1 Unity単位、1マス＝0.5です。原点はRoot Cellの地面の中心で、正面は南（2Dの見え方と同じ）です。
  - `bottom-left` のDefinition（ベッド・机・ベンチ・イーゼル・棚）は、原点から+X／+Yへ広がります。
  - `bottom-center` のDefinition（家・マイク・看板・井戸・クッション）は、原点が中心です。
- **Mapの座標はBlenderに一切書いていません。** どこに置くかは、Map JSON v4とCatalogを読んでUnityで決めます。
- 元のSpriteに描かれていない部分（家の側面・背面、家具の裏など）は、どの方向から見ても成り立つ最小限の形で補いました。補った部分はManifestの `supplemented` に記録しています（4項）。決定したデザインではありません。

再生成の方法（repoのルートで実行）:

```
"C:/Program Files/Blender Foundation/Blender 5.2/blender.exe" --background --factory-startup --python blender/scripts/build_phase3_buildings.py
"C:/Program Files/Blender Foundation/Blender 5.2/blender.exe" --background --factory-startup --python blender/scripts/render_phase3_previews.py
```

## 4. モデル一覧

| モデル | 用途（Definition） | 大きさ（幅×奥行×高さ） | 補った部分（元Spriteにない部分） |
|---|---|---|---|
| `house_main` | 家（house_main） | 2.39×1.20×1.89 | 側面と背面の壁、側面と背面の窓、屋根の背面と側面、扉の内側、入口の段 |
| `bench_basic` | ベンチ | 0.94×0.26×0.50 | 背もたれの裏、座面の裏 |
| `sign_basic` | 看板（5本で共用） | 0.44×0.10×0.56 | 板の裏 |
| `well_basic` | 井戸 | 0.44×0.44×0.15 | 石積みを2段に分けた形（Spriteは上から見た輪だけ） |
| `bed_basic` | ベッド | 0.99×1.49×0.60 | ベッドの下の横木、頭板の裏 |
| `desk_basic` | 机 | 0.94×0.34×0.68 | 机の背板、モニターの裏 |
| `cushion_basic` | クッション | 0.35×0.35×0.10 | 裏面 |
| `recording_mic_basic` | 録音マイク | 0.22×0.22×1.05 | マイクの裏、ショックマウントを簡略化 |
| `art_easel_basic` | イーゼル | 0.44×0.50×1.09 | 後ろの脚、キャンバスの裏 |
| `studio_shelf_basic` | 棚 | 0.94×0.28×0.60 | 背板、上に置いた物の側面 |
| `interior_floor_a` / `_b` | 屋内の床（1マス、2色） | 0.5×0.5 | 屋内の壁の面、出入口の枠と扉、壁の下の幅木 |
| `interior_wall_base` | 屋内の壁の下部（常に表示） | 0.5×0.5×0.14 | （同上） |
| `interior_wall_upper` | 屋内の壁の上部（カメラ側は切る） | 0.5×0.5×1.06 | （同上） |
| `interior_doorway_base` / `_upper` | 屋内の出入口 | 0.5×0.5×1.2 | （同上） |

- 合計16モデルです。正式Map上では、家1・ベンチ1・看板5・井戸1・家具6の計14 Instanceと、屋内の床67マス・壁50マスに使っています。
- Unityでの見た目の比較は `sheets/01`・`03`・`07`・`13`（44項）にあります。

## 5. 各モデルのPolygon数

ManifestとUnityに読み込んだ後の三角形数は一致しています（検査）。

| モデル | 三角形 | 頂点 | 部品 |
|---|---:|---:|---|
| house_main | 1,418 | 1,416 | 壁 `house_main` 776／屋根 `HouseRoof` 510／扉 `HouseDoor` 132 |
| bench_basic | 132 | 88 | 1 |
| sign_basic | 60 | 40 | 1 |
| well_basic | 284 | 184 | 1 |
| bed_basic | 216 | 144 | 1 |
| desk_basic | 252 | 168 | 1 |
| cushion_basic | 74 | 48 | 1 |
| recording_mic_basic | 224 | 132 | 1 |
| art_easel_basic | 120 | 96 | 1 |
| studio_shelf_basic | 368 | 240 | 1 |
| interior_floor_a ／ _b | 2 ／ 2 | 4 ／ 4 | 1 |
| interior_wall_base ／ _upper | 12 ／ 12 | 8 ／ 8 | 1 |
| interior_doorway_base ／ _upper | 38 ／ 120 | 28 ／ 80 | 1 |
| **合計（16モデル）** | **3,334** | | |

組み立てた後のMap全体のMesh（POCの表示値）は次のとおりです。

| Map | Phase 2.1 | Phase 3 |
|---|---:|---:|
| first_field | 11,388 | 13,142 |
| halka_house | 804 | 2,744 |

## 6. Material数

- すべてのモデルが、Material 1つ・部品ごとに1スロットです（検査）。
- Unityでは、Phase 2・2.1と同じ共通の `POC_ToonVertexColor` を1つだけ使います。Textureは使っていません。

## 7. Blender Source保存先

| 種類 | 場所 |
|---|---|
| Source（.blend、11ファイル） | `blender/sources/phase3/`（house_main・bench_basic・sign_basic・well_basic・bed_basic・desk_basic・cushion_basic・recording_mic_basic・art_easel_basic・studio_shelf_basic・interior） |
| 生成Script | `blender/scripts/build_phase3_buildings.py` |
| プレビューScript | `blender/scripts/render_phase3_previews.py` |
| Manifest | `blender/exports/phase3_assets_manifest.json` |
| プレビュー画像（112枚） | `blender/exports/previews/phase3/<モデル>_<向き>.png`（正面・東・背面・西・上・2Dの角度・斜めの7方向） |
| Unity用のFBX（生成物） | `unity-project/Assets/POC2D3D/Models/Phase3/<モデル>.fbx` |

制作元（.blend・Script）とUnity用の生成物（FBX）を分けています。

## 8. FBX Export結果

- 16ファイル。FBX 7.4 binary、前方 −Z、上 Y、Bake Space Transformです（Phase 2と同じ設定）。
- 大きさは15KB（床）〜39KB（家）。合計は約384KBです。
- 頂点カラーは、FBXを読み直した後の状態でプレビューを描いて確認しました（プレビュー画像は、Blenderに読み直したFBXから描いています）。

## 9. Unity Import結果

- 16モデルすべて、エラーなしで読み込めました。
- Scale 1のまま読み込み、拡大縮小はしていません（検査）。
- 法線は三角形の向きと一致し、外向きです（検査）。
- すべての頂点に頂点カラーがあります（不透明で、空ではない。検査）。
- Materialは `POC_ToonVertexColor` に置き換えています（`PocSceneBuilder` が自動で設定）。
- 家は、壁・屋根・扉の3つの部品と、`DoorAnchor`・`EntranceAnchor` を持ったまま読み込めています（検査）。
- FBXのModel PrefabをそのままCatalogへ登録しています。別のPrefabは作っていません。
- Phase 1の仮の家（`poc_house.fbx`）はPhase 1の検査が参照しているので残しています。Catalogからは外しました。

## 10. Houseの外観

- Definition `house_main`: 5×2マス、`bottom-center`、Root Cellは(-7,-4)です。10マスのうち9マスが通行不可で、Root Cell（入口のマス）だけ通れます。この配置は変えていません。
- 元のSprite `house_exterior.png` に合わせて作りました。
  - 白い壁（#f2eee6）と、濃い灰色の柱・梁
  - 柱の根元の石
  - 正面の破風と、その中の金色の菱形の飾り
  - 梁の上の小さな金色の飾り2つ
  - 中央の入口と、濃い色の扉
- 側面と背面の壁・窓は、元のSpriteにないので補いました（4項）。
- 3Dでは、近づくと家の全体が表示されます（検査、`sheets/01`）。
- 2Dでは、Spriteに近い形・色で、扉と窓の位置も合っています（`sheets/01`「2D 16px/マス」）。

## 11. Houseの入口

最重要の項目です。

- 入口のマス（Root Cell (-7,-4)）はもともと通れるマスで、ここへ入ると屋内に切り替わります。
  - Phase 3では、このマスに壁や扉を置かず、「くぼみ」にしました。
  - 家の正面の壁はマスの中心から南へ0.2、扉はマスの北の端の近く（中心から北へ0.22。端は0.25）にあります。
  - 3Dでは、ﾊﾙｶﾁｬﾝが扉に向かってくぼみへ一歩入り、そこで屋内に切り替わります。扉のMeshを突き抜けて見えることはありません。
  - くぼみの奥の面は暗い色（#3e3e44）なので、2Dでも元のSpriteのような暗い入口に見えます。足元には石の段（上面 #5e5e64）があります。
- Anchor: `DoorAnchor`（扉の面、地面の高さ。マスの中心から北0.22）と `EntranceAnchor`（マスの中心の段の上、高さ0.025。立つ位置）。
  - 検査で、扉が入口のマスの北の端にあることを確認しました（world (-3.50, 0.00, -1.78)）。
- 屋内側:
  - 入ると `interior_entry` (0,-3) に北向きで立ちます（既存の位置のまま）。
  - 出入口の部品は `interior_exit` (0,-4) の南の壁に置きました。部品には枠と扉があり、通れる側は開いています。
  - 3Dでも、出入口に壁があるようには見えません（`sheets/05`・`06`）。
- 出る時: (0,-4) へ歩くと、外の (-7,-5) に南向きで出ます（既存の位置のまま）。
- 歩数: 入る・出ることで余分な歩数は増えません（入口まで2歩、出る1歩。検査）。
- 入口のマスと通行判定は変えていません。

## 12. Houseの屋根

- 東西に棟がある切妻屋根です。
  - 軒の高さ1.10、棟の高さ1.83
  - 壁から、前後に0.10、左右に0.12張り出しています（検査: 敷地からの張り出しは0.15以内）
- 入口の上に、小さな切妻（頂点1.62）と金色の菱形の飾りがあります。
- 瓦は3色（#3c3c42／#46464c／#55555b）を互い違いに並べ、棟は #2c2c32 です。
- 屋根は `HouseRoof` という別の部品です。壁（`house_main`）と扉（`HouseDoor`）とは別に、表示・非表示を切り替えられます（16項）。
- 2Dでは、屋根の外周にだけ輪郭線が付きます。瓦の1枚ずつには付きません（33項）。

## 13. Indoorの床

- 1マスの床の部品を2種類作りました。`interior_floor_a`（#bbb198）と `_b`（#b8ae95）です。色は元の2Dの部屋の床に合わせています。
- Unityの `PocInteriorBuilder` が、Map Dataから組み立てます。
  - Map Boundsの中で、壁（`house_wall` Surface）でないマスすべてに床を置きます。家具の下にも置くので、家具が浮いたり、床に穴が開いたりしません。
  - 2種類のどちらを置くかは、MapとマスのHashで決まります。毎回同じになります。
  - 床全体を1つのMeshにまとめています（Draw Call 1）。
- 検査: 床67枚＝壁でないマス67。壁のマスには床がありません。家具は床の上に立っています。
- ﾊﾙｶﾁｬﾝの足元の高さは変えていません（床の高さ0）。

## 14. Indoorの壁

- 壁は2つの部品でできています。
  - 下部 `interior_wall_base`（高さ0〜0.14）: 常に表示します。
  - 上部 `interior_wall_upper`（0.14〜1.20）: 3Dでカメラ側にあれば切ります（15項）。
  - 高さ1.20は、2Dで北の壁の面が、元の部屋の上の暗い2段分になるように決めました。
- 色は元の2Dの部屋に合わせた暗い灰色です（上面 #26262d、面 #2f2f37、下部 #3b3b44）。
- 組み立て:
  - `house_wall` Surfaceのマス（50マス）すべてに壁を置きます。それ以外のマスには置きません（検査）。
  - 壁のマスは、一番近いMapの端（北・南・東・西）の面に属します。同じ距離のときは北・南を優先します。MAP EDITORで作った長方形の部屋なら、どれでも同じ方法で分けられます。
  - 下部はまとめて1つのMesh、上部は北・南・東・西の面ごとに1つずつのMeshです。
  - 壁はMap Boundsの内側に収まっています（検査）。
- 通れるマスと通れないマスは変えていません。壁の見た目のために、通行不可のマスを増やしていません。

## 15. Indoor Camera

- カメラの基本の動き（カメラ基準の移動、止まってから0.35秒で背後へ回り込む）は、Phase 2.1のままです。
- 屋内だけ、距離を2.2から2.9へ、角度を50°から52°へ変えました。壁を切るようにしたので、少し離して部屋を広く見せています。
- **カメラ側の壁を低く切る**:
  - 外向きの面がカメラの方を向いている壁の上部（内積 > 0.3、つまりカメラ方向から約72°以内）を、下部（高さ0.14）だけ残して消します。
  - 部屋の輪郭は残り、ﾊﾙｶﾁｬﾝ・家具・足元のマスが見えます。
- 4方向で確認しました（検査、`sheets/06`）。
  - 北向き → 南の壁を切る
  - 南向き → 北の壁を切る
  - 西向き → 東の壁を切る
  - 東向き → 西の壁を切る
  - どの向きでも、カメラとﾊﾙｶﾁｬﾝの間に見える物はありません。
- 入口から、6つの家具すべてが見えます（検査）。
- 歩いている間、カメラは1フレームに最大0.046しか動きません。急に飛びません（検査）。
- カメラの高さは約2.8（注視点0.5＋2.9×sin52°）で、壁（1.2）より上にあります。壁にめり込みません。
- POCパネルに「3D indoor walls: cut camera side / rays only」を追加しました。rays onlyにすると、Phase 1・2の方式（視線上の物だけ隠す）と比べられます。
- 比較（`sheets/06`）: 隠さない／Phase 2の方式／Phase 3の方式（採用）。

## 16. 壁・屋根の可視制御

| 場所 | 動き |
|---|---|
| 屋外3D・家の前 | 家の全体を表示します |
| 屋外3D・家の入口のくぼみ | 何も隠しません（検査） |
| 屋外3D・家の裏 | ﾊﾙｶﾁｬﾝの胸（高さ0.45）へ向かう視線を遮る部品だけを隠します（下記） |
| 屋外3D・家の前へ戻る | すべての部品が戻ります（検査） |
| 屋内3D | 屋根はありません。壁はカメラ側の面だけ切ります（15項） |
| 2D | 南の壁の上部だけを常に切ります（元の2Dの部屋と同じ見え方）。屋外の家は常に表示します |

- 家の裏で隠す部品:
  - 屋根だけが視線を遮っているときは、屋根だけを隠します。
  - 壁か扉が遮っているときは、壁・扉・屋根をまとめて隠します。屋根だけが宙に浮いて見えることはありません。
- 隠した部品も影は残ります（ShadowsOnly）。
- 判定は、部品の三角形と視線が交わるかを1つずつ調べています（POCは3D Physicsを使わない設定なので、CPUで計算しています）。
- 隠すのは3Dの表示だけです。2Dには影響しません。
- 9段階の切替（屋外2D → 屋外3D → 入室 → 屋内3D → 屋内2D → 屋内3D → 退室 → 屋外3D → 屋外2D）を検査とWebGLで確認しました。
  - 屋根や壁が消えたまま残ることはありません。
  - ﾊﾙｶﾁｬﾝが隠れることもありません。
- **確認していただきたい点（50項・52項）:** 3Dで家から出た直後は、ﾊﾙｶﾁｬﾝが南を向いていて、カメラはその後ろ（北側＝家の向こう）にあります。このため家全体が「邪魔な物」として隠れ、影だけになります（`sheets/02` 右下の2枚、動画05）。一歩動くか、カメラが回り込むと元に戻ります。

## 17. Bench

- `bench_basic`: 2×1マス、`bottom-left`、Root (-2,2)。2マスとも通行不可です（変更なし）。
- 座面（板）、脚、2枚の横板の背もたれです。色とシルエットは `bench.png` に合わせました。三角形132です。
- Interaction:
  - 座る場所（Action Point）は `seat_left` (-2,1) と `seat_right` (-1,1) です。どちらも北向き、actionType 2（座る）、poseKey `bench_sit` です。
  - 2D・3DのどちらのAボタンでも「すわる。」が出ます（検査、WebGL `sheets/14`）。
- 座る位置の記録:
  - `SeatAnchor_L`（Root Cellの中心から東0・高さ0.22）と `SeatAnchor_R`（東0.5・高さ0.22）です。座面の高さは0.22で、それぞれのマスの中央にあります（検査）。
  - 本編の座る処理（poseKey `bench_sit`）を3Dにつなぐときは、ﾊﾙｶﾁｬﾝの腰をこのAnchorに合わせる想定です。
  - POCには、座るAnimationや新しい処理は追加していません。
- カメラの遮蔽物としては扱いません（低い物なので）。

## 18. Sign

- `sign_basic`: 1×1マス、`bottom-center`。正式Mapには5本あります。
  - (3,1)「ここは HALKA WORLD。」
  - 北・東・南・西の4本（ver3.0 Previewで追加された方角の看板）
- 支柱と、2枚の板を合わせた看板です。色は `sign.png` に合わせ、正面は南です。三角形60です。
- 文字は焼き込んでいません。メッセージは、既存の `signText` をそのまま使います。
  - 5本すべて、2D・3Dで正しい文が出ます（検査10件）。
  - WebGLで「ここは HALKA WORLD。」を確認しました（`sheets/14`）。
- 補った部分: 板の裏。

## 19. Well

- `well_basic`: 1×1マス、Root (-2,-2)。調べる位置は (-2,-3)、北向きで「いど。」です（変更なし）。
- 丸い石の輪（外側の半径0.22、内側の半径0.14）を2段積みにして、内側に暗い水面（高さ0.05）を入れました。三角形284です。
- `well.png` は上から見た輪なので、高さは0.15に抑えました。2Dで元のSpriteより目立たないようにしています。
- 内側に通れる穴は作っていません。論理Gridは平らなままです。
- 2D・3Dで「いど。」が出ます（検査、WebGL `sheets/14`）。
- 補った部分: 石積みの段の分け方。

## 20. Bed

- `bed_basic`: 2×3マス、`bottom-left`、Root (-5,0)。6マスとも通行不可です（変更なし）。
- 灰色の金属の枠、マットレス、白い枕、黄色の掛け布団です。`house_bed.png` に合わせました。三角形216です。
- 調べる位置は (-5,-1)、北向きです（actionType 3、poseKey `bed_sleep`）。2D・3Dで「ベッド。」が出ます（検査、WebGL）。
- 将来のために `SleepAnchor`（Root Cellの中心から東0.25・北0.95・高さ0.33。マットレスの上）を置きました。新しい睡眠の仕組みは追加していません。
- 補った部分: ベッドの下の横木、頭板の裏。

## 21. Desk

- `desk_basic`: 2×1マス、`bottom-left`、Root (2,1)。2マスとも通行不可です（変更なし）。
- 濃い木の天板と4本の脚の上に、モニター・キーボード・マウスを置きました（`desk.png` に描かれている物だけ）。三角形252です。
- 調べる位置は (2,0)、北向きで「つくえ。」です。次の方法すべてで確認しました。
  - 2DのAボタン
  - 3DのAボタン
  - 3Dで机をクリック
  - 3Dでﾊﾙｶﾁｬﾝをクリック
  - 検査とWebGL、スマホ相当
- 将来の作業Animation用に `WorkAnchor`（Root Cellの中心から東0.25・南0.35・高さ0。机の前の立ち位置）を置きました。ゲームのルールは追加していません。
- 補った部分: 机の背板、モニターの裏。

## 22. Cushion

- `cushion_basic`: 1×1マス、Root (0,0)。**通れるマスのまま**です（Collisionは追加していません。検査）。
- 角の丸い黄色のクッションで、中央に濃い色の「×」の縫い目があります。高さ0.10、三角形74です。
- 調べる位置はクッションのマス自身です（actionType 2、poseKey `cushion_sit`）。2D・3Dで「すわる。」が出ます。
- 座る位置として `SitAnchor`（クッションの中心、高さ0.1）を記録しました。
- 小さい物なので、クリックの判定を最低0.3の立方体まで広げています（31項）。カメラの遮蔽物としては扱いません。
- 補った部分: 裏面。

## 23. Recording Mic

- `recording_mic_basic`: 1×2マス、`bottom-center`、Root (4,-1)。2マスとも通行不可です（変更なし）。
- 丸い台、支柱、マイク本体とショックマウントです。色は `recording_mic.png` に合わせました。高さ1.05、三角形224です。
- 2Dで消えないように、支柱は実物より太くしています。
- 細長いので、3Dで視線を遮るときは隠れる遮蔽物です（既存の方式）。
- Action Pointはありません（変更なし）。
- 補った部分: マイクの裏、ショックマウント（簡略化）。

## 24. Art Easel

- `art_easel_basic`: 2×2マス、`bottom-left`、Root (-3,-2)。4マスとも通行不可です（変更なし）。
- 3本脚（A字の前脚2本と後ろ脚）、横木、キャンバスです。キャンバスの絵の色（青・黄・ピンク）は `art_easel.png` に合わせました。高さ1.09、三角形120です。
- 脚は斜めなので、床に接して見えるように、足先が床の下へ6mm入っています。床の上に立っている判定の範囲内です（検査）。
- 2Dでも一目でイーゼルと分かる形です。2Dでは、イーゼルの後ろにいるﾊﾙｶﾁｬﾝは隠れ、前にいるときは手前に描かれます（検査）。
- Action Pointはありません（変更なし）。
- 補った部分: 後ろの脚、キャンバスの裏。

## 25. Studio Shelf

- `studio_shelf_basic`: 2×1マス、`bottom-left`、Root (-1,2)。2マスとも通行不可です（変更なし）。
- 濃い木の枠と棚板に、本を並べました。上には、Spriteに描かれている物だけを置いています。小物は増やしていません。三角形368です。
- Action Pointはありません（変更なし）。
- 補った部分: 背板、上に置いた物の側面。

## 26. その他Catalog Object

Phase 3の開始時と終了時に、最新のObject Catalog（`object_catalog.hwcatalog.json`）を調べました。

- Object Definitionは13種類で、**すべて正式Mapで使われています**。
  - Phase 2: tree・flower・stone
  - Phase 3: house・bench・sign・well・bed・cushion・desk・recording_mic・art_easel・studio_shelf
- Surfaceは4種類です。
  - `base_ground`・`dirt`: Phase 2の地面と道
  - `house_floor`・`house_wall`: Phase 3の屋内の床と壁
- Entityは `player_main`・`crow_main` で、対象外です。

これら以外に、まだ3D化していないObjectはありませんでした。

## 27. 未3D化Object一覧

検査が自動で作る一覧（`HARUKA_WORLD_ver3.0_PHASE3_LOGS/phase3_binding_audit.md`）です。

| Definition ID | 3Dモデル | 状態 |
|---|---|---|
| flower_basic | flower_basic | Blenderモデル |
| house_main | house_main | Blenderモデル |
| stone_basic | stone_basic_a（ほか形違い） | Blenderモデル |
| tree_basic | tree_basic | Blenderモデル |
| bench_basic | bench_basic | Blenderモデル |
| sign_basic | sign_basic | Blenderモデル |
| well_basic | well_basic | Blenderモデル |
| bed_basic | bed_basic | Blenderモデル |
| cushion_basic | cushion_basic | Blenderモデル |
| desk_basic | desk_basic | Blenderモデル |
| recording_mic_basic | recording_mic_basic | Blenderモデル |
| art_easel_basic | art_easel_basic | Blenderモデル |
| studio_shelf_basic | studio_shelf_basic | Blenderモデル |

- **未3D化は0件**です。どちらの正式Mapでも、自動生成の箱や仮モデルは描かれていません（検査）。
- `PocBindingAudit` は、Bindingがないか仮モデルのDefinitionを「不足」として報告します。新しいObjectを追加した時も、検査で見つかります（検査で確認）。
- ﾊﾙｶﾁｬﾝ（Phase 2.1で承認された仮モデル）とCrowは対象外です。

## 28. Presentation Catalog更新

`POC_PresentationCatalog.asset` に、Phase 3の10種類を登録しました（`PocSceneBuilder` が生成します）。

| Definition | Model | 遮蔽物 | その他 |
|---|---|---|---|
| house_main | Phase3/house_main | 部品ごと（`partialOcclusion`） | Phase 1の仮モデル（`placeholder`）から置き換え |
| bench_basic ／ sign_basic ／ well_basic | Phase3/… | 対象外 | |
| bed_basic ／ desk_basic ／ studio_shelf_basic | Phase3/… | 対象外 | |
| cushion_basic | Phase3/cushion_basic | 対象外 | クリック判定の最小0.3 |
| recording_mic_basic ／ art_easel_basic | Phase3/… | 対象（既存の方式） | |

- Scale 1、Yaw 0、Mesh Offset 0、Pivot＝Root Cellの地面の中心です。
  - Blenderの原点をRoot Cellに合わせて作ったので、補正の値はいりません。
  - 輪郭線の強さは1、影ありです（既定値）。
- Phase 2の花・石には、クリック判定の最小0.3（`minPickSize`）を追加しました。
- Bindingに `partialOcclusion` と `minPickSize` の2項目を追加しました。
- 屋内の部品は `PocInteriorPresentation` として登録しました。
  - 床2種、壁の下部・上部、出入口の下部・上部、壁の下部の高さ0.14
- Anchor（`DoorAnchor`・`EntranceAnchor`・`SeatAnchor_L/R`・`SleepAnchor`・`WorkAnchor`・`SitAnchor`）は、BlenderからFBXの子Transformとして持ってきています。
- 登録の不足は `PocBindingAudit` で調べられます（27項）。

## 29. Map JSONとの配置一致

- 位置はすべてMap JSON v4とCatalogから決めています。BlenderにもUnityのコードにも、Mapの座標は書いていません。
- 検査（first_field・halka_house）:
  - すべてのInstance ID・Definition ID・Root CellがMap JSONと一致しています。
  - モデルは地面に立ち、自分の敷地の中に収まっています（屋根の張り出しは0.15以内）。
  - Blocked FootprintとAction Pointのマスは変わっていません。Action Pointのマスは通れて、モデルにふさがれていません。
  - 屋内のBounds、入口（`interior_entry`）、出口（`interior_exit`）が一致しています。
- 正式Map・Catalog 4ファイルのSHA-256は、作業前と同じです（検査とFixtureツールの両方で確認）。

## 30. Collision

- 当たり判定は、これまでどおりCatalogのBlocked Footprintと、`house_wall` Surfaceから作ります。
- 3Dモデルの大きさでは変えていません。
- クッションは通れるままです。家の入口のマスも通れるままです（検査）。
- 3DモデルにColliderは付けていません。クリックの判定は、CPUで三角形と箱を調べて行います（31項）。

## 31. Interaction

Aボタン、PCのクリック、正面の物を調べる、Action Point、メッセージは、これまでどおりです。新しい操作は追加していません。

**小さい物・隠れた物のクリック（24.1）**

1. 小さい物（花・石・クッション）は、クリックの判定を最低0.3の立方体まで広げました。
2. 3Dでは、クリックがﾊﾙｶﾁｬﾝの体に当たって、どの物にも当たらなかったとき、「ﾊﾙｶﾁｬﾝが向いている物」を調べます。これはAボタンで調べる物と同じです。
   - ﾊﾙｶﾁｬﾝの体に隠れた正面の花・看板・井戸も、体をクリックすれば調べられます。
3. クリックの線上に物があれば、これまでどおりその物を選びます（Phase 2の検査で確認）。
4. 立っている壁より奥の物は選びません。
5. カメラのために隠している部品（影だけの部品）は、クリックを止めません。
6. 家は、三角形で正確に判定します（家の箱の範囲には、入口の前の空間も入るため）。
7. 距離・向き・Action Pointの条件（`TryInteract`）は変えていません。遠くの物を調べられるようにはなっていません。
8. Aボタンの動きは変えていません。

検査:
- 体の上から隠れた花をクリック → 花
- 頭をクリック → 向いている物
- 正面に何もない → 何も起きない
- 遠くの花 → 調べない
- 屋内で見えている家具 → 選べる
- 壁の奥 → 選ばない

WebGLでは、ﾊﾙｶﾁｬﾝのクリックで机・看板・ベンチ・井戸を調べられることを確認しました。

Phase 2.1の未解決事項2（3Dで正面の小さな物が体に隠れてクリックできない）は、この方法で解決しました。ただし、隠れている物そのものの位置をクリックすることはできません（見えていないため）。

## 32. MAP EDITOR連携

- MAP EDITOR v0.6のCore（`MapSession.MoveObject`）で、試験用のコピーMapを作りました（`tools/Poc2D3DFixture`）。正式Mapには書き戻していません。
  - `first_field_buildings_moved.hwmap.json`:
    - 家 (-7,-4)→(-7,-3)
    - ベンチ (-2,2)→(-1,2)
    - 看板 (3,1)→(4,1)
    - 井戸 (-2,-2)→(-1,-2)
  - `halka_house_furniture_moved.hwmap.json`: ベッド・机・クッション・マイク・イーゼル・棚を、それぞれ東へ1マス移動
- MAP EDITORの既存のルールに従っています。
  - 家は東西には動かせませんでした（「既存Objectと重なります」「家の入口前が使用できません」）。そのため北へ1マス動かしました。
- 検査:
  - 動かした物は、2D・3Dとも新しいマスに描かれ、当たり判定も一緒に動きます。
  - 動かした家の入口からも屋内へ入れます。
  - 動かした机は、新しいAction Pointで「つくえ。」と答えます。
  - 家具を動かした後も、壁でないマスすべてに床があります。
- Map JSON v4とMAP EDITOR v0.6は変えていません。MAP EDITORのファイルにも触れていません（Core Tests 181/181）。

## 33. 2D Outline

- Phase 2.1と同じ方式です。Object IDの輪郭線を、低解像度の画像の段階で描きます。設定も同じです（16px/マス、1px、#252B39、内側の線「弱」）。
- 家・設備・家具にも、そのまま適用しています。
- 検査: 輪郭線の量が、外周の長さと同じくらいです（線だらけになっていない）。
  - 家 566／外周350
  - ベッド 380／208
  - 机 137／138
  - 棚 107／130
  - イーゼル 111／138
  - ほか
- 屋根の瓦、ベッドの枠、棚の段の1つずつには、黒い線が付きません（`sheets/01`・`07`）。
- 屋内の床と壁は、地面と同じ扱い（ID 0）です。輪郭線は付かず、ﾊﾙｶﾁｬﾝを隠しません。

## 34. 2D Player Sprite

- 2Dのﾊﾙｶﾁｬﾝは、Phase 2.1で承認された方式のままです。
  - もともとのSprite、同じコマ、同じタイミング、同じ密度、同じ向き、同じ足元の位置
- Player Sprite 50ファイルのSHA-256は、作業前と同じです。
- 前後関係（検査）:
  - 家の後ろにいるときは、家に隠れます（隠れた画素4,092）。
  - イーゼルの後ろにいるときは、イーゼルに隠れます（416）。
  - 前にいるときは手前に描かれます（0）。机・クッションの前でも隠れません。
- 屋内でも、3Dモデルではなく元のSpriteを描いています（検査）。
- UI（POCパネル・メッセージ）は、常に一番手前です（検査）。

## 35. 2D Tree半透明

- Phase 2.1のままです。手前の木だけが40%まで半透明になります（0.25秒、2Dだけ、影は残る）。
- 家と家具は半透明にしません（検査「家は半透明にならない」）。
- 家具を半透明にする必要は、今回の確認では見つかりませんでした。

## 36. 3D Camera基準入力

- 入力の仕組みとカメラの追従は変えていません。
  - W/S/A/Dはカメラ基準、4方向・1マスずつ、0.18秒で移動
  - 押している間は方向が変わらない
  - 止まってから0.35秒で背後へ回り込む
- 屋内での検査:
  - S長押しで、まっすぐ南へ下がります。カメラの向きは変わりません。
  - A長押しで、まっすぐ西へ進みます。円を描きません。
- Phase21PresentationChecks（60件）にすべて合格しています。
- WebGLでは、屋内で長押しすると、まっすぐ4歩進みました（`webgl/pc_28`）。
- 変えたのは、屋内のカメラの距離と角度だけです（15項）。

## 37. 朝夜

- World Timeはそのままです（朝10分・夜10分、UTC起点、再読み込みしても続く、屋内外共通、2.5秒のFade）。時間の計算と保存キーは変えていません。
- 2Dの夜: 屋内は0.16、屋外は0.33の暗さです（本編の2Dと同じ値。検査）。
- 3Dの夜: 屋内は屋外より明るくしています（検査）。
  - 環境光は屋内(0.30,0.32,0.45)、屋外(0.16,0.20,0.36)
  - 太陽の明るさは屋内0.42、屋外0.26
- Phase 3での変更: 屋内3Dの背景を、空の色からMapの背景色（2Dと同じ）にしました。壁を切ると、部屋の外に空が見えてしまうためです。
- 夜でも、家と家具は見分けられます（`sheets/08`・`09`、WebGLのPC・スマホ相当）。

## 38. AUTO・Crowへの影響

- POCには、AUTOとCrowはまだつながっていません（Phase 1から同じです）。
- Phase 3では、本編のAUTO・Pathfinder・Crowのコード（`Assets/Scripts`）を一切変えていません（差分なし）。
- 本編につなぐ箇所（`HALKA_WORLD_2D3D_ARCHITECTURE.md` 16・17項）:
  - AUTO: `AutoModeController` の `player.TryStep` を `SharedWorldSession.TryStep` に、`world.CanEnter` を共通Gridに差し替えます。屋内の目的地は `IndoorAutoRoutePlanner2D` をそのまま使います。3D専用のAUTOは作りません。
- Phase 3で家具の見た目が変わっても、通行判定（Blocked Footprint）は同じなので、AUTOの経路は変わりません。
- Crowが止まる木のAnchor（Phase 2の `PerchAnchor`）にも触れていません。

## 39. 新規自動テスト

`Phase3BuildingFurnitureChecks`（`Assets/POC2D3D/Editor/`）: **88件すべて合格**（`HARUKA_WORLD_ver3.0_PHASE3_LOGS/04-phase3-checks.log`）

| 分類 | 主な内容 | 件数 |
|---|---|---:|
| Blender Asset | Script・Manifest・.blend・プレビューがある。三角形数がManifestと一致する。Scale 1、法線、頂点カラー、Material 1つ。家の部品とAnchor、ベンチの座る位置 | 9 |
| 配置（2つのMap） | ID・Root Cell、地面と敷地、Blocked Footprint・Action Point | 6 |
| Catalog | すべてにBlenderモデルがある、仮の箱がない、不足を報告できる、一覧を出力する | 4 |
| 家（3D） | 近づくと全体を表示、くぼみでは隠さない、裏では部品ごとに隠す、戻ると表示、扉の位置 | 5 |
| 入退室 | 入室・屋内の組み立て・出入口・退室・歩数 | 5 |
| 屋内 | 床・壁・Bounds・家具の接地・当たり判定 | 5 |
| 屋内カメラ・操作 | 4方向、壁を切る、家具が見える、カメラが滑らかに動く、S・A長押し | 6 |
| 2D | 輪郭線、前後関係、元のSprite、南の壁、UI、木の半透明、家は半透明にしない | 7 |
| Interaction | すべての家具・設備のAボタン（2D／3D）、Action Pointがない物 | 24 |
| クリック | 体・頭・正面に何もない・遠く・屋内・壁 | 6 |
| 切替 | 9段階の切替、歩数 | 2 |
| 朝夜 | 2D・3Dの屋内の明るさ | 2 |
| MAP EDITOR | コピーの作成、屋外・屋内で動かした物、扉、机、床 | 6 |
| 保護 | 正式Map・CatalogのSHA-256 | 1 |

- 合計88件です。指示書32項の各項目（Blender Asset／配置／2D／3D／World／Catalog）をすべて含んでいます。
- Interactionの24件は、Action Pointのある10個（ベンチ・看板5本・井戸・ベッド・クッション・机）の2Dと3Dの組20件と、Action Pointのない物4件（家・マイク・イーゼル・棚）です。
- 実行方法（Unity Editor不要）:

```
Unity.exe -batchmode -projectPath <worktree>\unity-project -executeMethod Halka.Poc2D3D.EditorTools.Phase3BuildingFurnitureChecks.RunBatch -logFile <log>
```

- Editorのメニューからも実行できます: `HALKA/POC 2D3D/…`
- 比較画像（Unity）は `Phase3Capture.CaptureBatch` で作ります。

## 40. 既存Regression

最終状態のコードで、すべてもう一度実行しました。

| 検査 | 結果 | ログ |
|---|---|---|
| Phase 1 PocChecks | **102件合格** | `05-phase1-poc-checks.log` |
| Phase2EnvironmentChecks | **130件合格** | `06-phase2-checks.log` |
| Phase21PresentationChecks | **60件合格** | `07-phase21-checks.log` |
| Version30Checks（継承分を含む） | 合格 | `08-version30checks.log` |
| MapAuthoringChecks（Map Contract v0.6） | 合格 | `09-mapauthoringchecks.log` |
| MAP EDITOR Core Tests | **181/181合格** | `10-map-editor-core-tests.txt` |

- ログの場所: `C:\Users\owner\Documents\ChatGPT\HARUKA_WORLD_ver3.0_PHASE3_LOGS\`
- **既存の検査を1件だけ書き換えました。**
  - Phase2EnvironmentChecksの「placeholders: house is still marked as a Phase 1 placeholder」（家がまだPhase 1の仮モデルであること）です。
  - Phase 3で家をBlenderモデルにすることが目的だったので、この確認の前提が変わりました。
  - そこで「house: the Phase 3 Blender model replaced the Phase 1 placeholder」に変えました。家が仮モデルでも代わりの箱でもなく、`house_main` のモデルであることを確かめる内容です。
  - 件数は同じ130件です。条件をゆるめたものではありません。
- ほかの検査は、削除も変更もしていません。
- Version30Checksは、本編のファイル（FirstDay.unity・GrassDecoration.prefab・ProjectSettings.asset）を作り直します。実行するたびに、worktree内で元に戻しました。

## 41. PC WebGL

repo外のRelease Buildを、別に起動したheadless Chrome（1280×720、AMD Radeon内蔵GPU）で操作しました。画像は `HARUKA_WORLD_ver3.0_PHASE3_CAPTURES\webgl\`、流れは `sheets/11`・`12`・`14` にあります。

| # | 指示書の流れ | 結果 | 画像 |
|---|---|---|---|
| 1 | 2Dで起動 | 59.8fps | pc_01 |
| 2 | 新しいHouse | 2Dで、元のSpriteに近い家 | pc_01・02 |
| 3 | Bench | 2Dでクリック →「すわる。」 | pc_35・36 |
| 4 | Sign | 2Dでクリック →「ここは HALKA WORLD。」 | pc_30・31 |
| 5 | Well | 2Dでクリック →「いど。」 | pc_39・40 |
| 6・7 | 3Dへ切替、Houseの外観 | 家全体が表示される（60fps） | pc_03 |
| 8・9 | 3Dで入口へ移動し、入室 | 上を押し続けると、くぼみへ入って屋内へ | pc_seq_enter・pc_04 |
| 10・11 | 床・壁・家具 | カメラ側の壁が切られ、家具がすべて見える | pc_04・05 |
| 12・13 | 4方向へ移動、カメラの回転 | 北・東・南・西。止まると背後へ回り込む | pc_05・06・12a・12b |
| 14・15 | ベッド・机の近く | どちらも見通しがよい | pc_12・08・09 |
| 16・17 | クッション、その他の設備 | クッション・マイク・イーゼル・棚 | pc_14・07・12 |
| 18 | 3DでInteraction | 机をクリック／ﾊﾙｶﾁｬﾝをクリック →「つくえ。」、「ベッド。」「すわる。」 | pc_10・11・13・15 |
| 19・20 | 2Dへ切替、同じ家具の配置 | 元の部屋と同じ配置 | pc_16 |
| 21 | 2DでInteraction | クッション「すわる。」 | pc_17 |
| 22 | 屋外へ退出 | (-7,-5) に南向きで出る | pc_18 |
| 23・24 | 3Dへ切替、家の外観が戻る | 切替の直後は、カメラが北（家の向こう）なので家全体が隠れ、影だけになる。一歩横へ動くと、家全体が戻る | pc_19・20 |
| 25 | 夜の家と家具 | 屋外・屋内、2D・3Dとも見分けられる | pc_22〜26 |
| 26 | 連続切替と長押し | 8回すばやく切り替えても、位置・向き・歩数は同じ。長押しでまっすぐ4歩 | pc_27a・27b・28 |

- 3Dの看板・ベンチ・井戸も確認しました（pc_32〜34・37・38・41・42）。どれもﾊﾙｶﾁｬﾝのクリックで調べられます。
  - 看板の上に見えている板は、遠くの北の看板です。クリックしても調べられません（距離のルールどおり）。
- 23・24について: 「House外観が元に戻る」は、一歩動いた後に満たされます。切り替えた直後は、Phase 2.1のカメラの約束（背後から映す）のため家が隠れます（50項・52項）。

## 42. Mobile Preview

DevToolsのエミュレーションで確認しました（390×844、DPR 2、タッチ、`?touchControls=1`）。画像は `sheets/10` にあります。

| 項目 | 結果 |
|---|---|
| 2Dの家・3Dの家 | 表示される（mobile_01・03、m_a01） |
| 入退室 | D-padで家の入口へ歩いて入室し、出られる（mobile_08・07、m_a01） |
| D-pad | 2Dは東西南北、3Dはカメラ基準 |
| Aボタン | 机「つくえ。」、ベッド「ベッド。」、クッション「すわる。」が、2Dと3Dの両方で出る（m_a03・05・07・09・11・12） |
| 屋内カメラ | 机・ベッド・クッションの前で、ﾊﾙｶﾁｬﾝと家具が見える（m_a04・08・12） |
| 夜の屋内 | 2D・3Dとも見分けられる（m_a13・14）。3Dの屋内は夜でも59.8fps、2Dは60fps |
| UIの可読性 | D-pad・Aボタン・メッセージ枠は読みやすい。POCパネルは文字が小さいが、本編には入らない |
| 性能 | 屋内外・2D/3Dとも約60fps（43項） |

- 机・ベッド・クッション・夜の確認は、タッチのイベントだけを使う設定で行いました（スマホのUser Agentは使っていません）。
- 入室と性能の測定は、スマホのエミュレーションを全部有効にして行いました。
- **気づいた点:** 縦画面の3Dでは、横方向に見える範囲が狭いので、ﾊﾙｶﾁｬﾝが大きく映り、正面の家具の一部を隠します。Phase 2.1の屋外3Dと同じ性質です（52項）。
- **物理スマートフォンでは確認していません。**

## 43. Performance

headless Chrome（AMD Radeon内蔵GPU、60Hz）上のRelease WebGLで、Phase 2.1とPhase 3を同じ手順で続けて計測しました。

- 手順: 起動 → 屋外2D → 3D → 2Dで家へ歩いて入室 → 屋内2D → 3D
- 各条件で5秒間の `requestAnimationFrame` の間隔を測り、POCパネルの値（`ProfilerRecorder`）を読みました。
- スマホ相当は「朝」に固定して測りました（Phase 3の1回目は実時間が夜だったので、条件をそろえて測り直しました）。PCは2回とも実時間の朝でした。

**PC（1280×720、DPR 2、Unity画面2048×1152）**

| 条件 | FPS 2.1 → 3 | Draw Calls | Batches | SetPass | Tris/frame | Used Memory |
|---|---|---|---|---|---|---|
| 屋外 2D | 60 → 60 | 83 → 79 | 82 → 78 | 75 → 75 | 40,228 → 47,334 | 15.6 → 16.1 MB |
| 屋外 3D | 60 → 59.8 | 105 → 102 | 84 → 81 | 71 → 71 | 23,000 → 27,638 | 15.7 → 16.2 MB |
| 屋内 2D | 60 → 59.8 | 79 → 81 | 79 → 78 | 75 → 75 | 4,496 → 10,202 | 15.9 → 16.3 MB |
| 屋内 3D | 60 → 60 | 93 → 90 | 87 → 84 | 72 → 72 | 7,660 → 12,538 | 15.9 → 16.4 MB |

**PC（1280×720、DPR 1、Unity画面1024×576）**

- FPSは、Phase 2.1が全条件60、Phase 3が59.8〜60です。
- 画面に表示された範囲の値（Draw Calls・Batches、2DのSetPass・Tris/frame）は、DPR 2のときと同じでした（三角形は±2）。
- Phase 3ではパネルが17行になったため、3DのSetPass・Tris/frameと、2D・3DのMemoryが画面の外に出て読めませんでした（50項の8）。そのためDPR 2の表を正式な値としています。

**スマホ相当（390×844、DPR 2、Unity画面780×1688）**

| 条件 | FPS 2.1 → 3 | Draw Calls | Batches | SetPass | Tris/frame | Used Memory |
|---|---|---|---|---|---|---|
| 屋外 2D | 60 → 60 | 93 → 93 | 92 → 88 | 85 → 85 | 31,418 → 34,620 | 15.7 → 16.3 MB |
| 屋外 3D | 59.8 → 60 | 99 → 99 | 94 → 91 | 81 → 81 | 19,090 → 20,548 | 15.8 → 16.4 MB |
| 屋内 2D | 59.8 → 59.8 | 106 → 88 | 89 → 88 | 85 → 85 | 3,738 → 9,776 | 16.0 → 16.4 MB |
| 屋内 3D | 59.8 → 60 | 117 → 95 | 95 → 94 | 82 → 82 | 7,068 → 11,178 | 15.9 → 16.4 MB |

- Frame Time: 24回の計測すべてで、中央値16.7ms、95%値16.8ms、最大16.9ms以下でした（60Hzの上限で止まっています）。
- **Build Size（非圧縮）: 29,535,817 → 29,619,891 bytes（+84,074、+0.28%）**
  - `.data` +55,327（モデル）
  - `.wasm` +28,747（コード）
  - framework.js・loader.jsは同じ大きさです。
- 増えたもの: 描く三角形は増えました（屋内2Dで約2.3〜2.6倍）が、どれも1フレーム5万未満です。Memoryは約0.5MB増えました。
- 減ったもの: 屋内のDraw Callsは減りました（スマホ相当の3Dで117 → 95）。
  - 床を1つのMesh、壁を「下部1つ＋4面」のMeshにまとめたためです。
  - Materialは1つを共有し、物は既存のStatic Batchingの対象です。
- 2DのTris/frameが3Dより多いのは、2Dで色・輪郭線用のIDなど、数回描くためです（Phase 2.1と同じ）。
- 計測していないもの: GPU・CPUの負荷の内訳、物理スマホでの値。
- 補足: 別の時間に測ったスマホ相当の3Dでは、Phase 3が30.8fps、Phase 2.1が28.6fpsと、どちらも約30fpsでした。両方のBuildで同じなので、実行環境（GPUの状態）によるもので、Phase 3で遅くなったのではないと判断しています。最終の計測では両方とも60fpsでした。

## 44. 比較画像

場所: `C:\Users\owner\Documents\ChatGPT\HARUKA_WORLD_ver3.0_PHASE3_CAPTURES\`

| ファイル | 内容 |
|---|---|
| `sheets/01_house_original_blender_2d_3d.png` | 家: 元のSprite、Blender（正面・斜め・2Dの角度・東・背面・西・上）、2D、Unity 3D（正面・横・背面・上）、ゲーム画面（玄関へ・くぼみ・家の裏） |
| `sheets/02_house_entrance_3d.png` | 入口: 2D／3Dの家の前、くぼみ、3Dで入室する8コマ、退室後（2D・3D・一歩横へ） |
| `sheets/03_outdoor_objects.png` | ベンチ・看板・井戸: 元のSprite／Blender／2D／3D |
| `sheets/04_outdoor_full_2d_3d.png` | 屋外の全体: 本編の2D、Phase 3の2D、3Dの俯瞰、家の周り |
| `sheets/05_indoor_original_vs_new.png` | 屋内: 本編の2D、Phase 3の2D、3Dの俯瞰、3Dの入口 |
| `sheets/06_indoor_3d_camera.png` | 屋内カメラ: 隠さない／Phase 2の方式／Phase 3の方式、4方向、ベッド・机の前 |
| `sheets/07_furniture.png` | 家具6点: 元のSprite／Blender（斜め・正面）／2D／3D |
| `sheets/08_indoor_morning_night.png` | 屋内の朝・夜（Unity・WebGL PC・スマホ相当） |
| `sheets/09_outdoor_night.png` | 屋外の夜（2D・3D） |
| `sheets/10_mobile.png` | スマホ相当（屋外・屋内の2D/3D、机・ベッド・クッションのA、夜、退室後） |
| `sheets/11_webgl_pc_flow.png` | PC WebGLの確認の流れ |
| `sheets/12_interaction_click.png` | 調べる（クリック・Aボタン、ﾊﾙｶﾁｬﾝのクリック） |
| `sheets/13_blender_models.png` | Blenderモデル16点 |
| `sheets/14_outdoor_fixtures_webgl.png` | WebGLで看板・ベンチ・井戸を調べる（2Dクリック／3D／ﾊﾙｶﾁｬﾝのクリック） |
| `unity/`（51枚） | Unityの固定カメラの画像（`Phase3Capture`） |
| `webgl/`（PC・スマホ相当・性能 `perf/`） | WebGLの画面 |
| `original/` | 本編（ver3.0 Preview）の屋外・屋内の2D |
| Blenderのプレビュー | repoの `blender/exports/previews/phase3/`（112枚） |

## 45. 動画

PC WebGL（1164×576、10fps、朝に固定）です。場所: `HARUKA_WORLD_ver3.0_PHASE3_CAPTURES\videos\`

| ファイル | 内容 | 長さ |
|---|---|---|
| `haruka_phase3_walkthrough_pc_morning.mp4` | 全体: 2Dで家へ → 3D → 入室 → 屋内3D → 2D → 3D → 退室 → 一歩横へ → 2D | 29.0秒 |
| `clip_01_outdoor_2d_to_3d.mp4` | 1. 屋外2D → 3D切替 | 4.3秒 |
| `clip_02_enter_house_3d.mp4` | 2. 3DでHouseへ入る | 4.7秒 |
| `clip_03_indoor_walk_3d.mp4` | 3. 屋内3Dで歩き回る（カメラの回り込みを含む） | 5.8秒 |
| `clip_04_indoor_3d_to_2d.mp4` | 4. 屋内3D → 2D切替（2Dで出口へ） | 5.6秒 |
| `clip_05_exit_house_3d.mp4` | 5. 3DでHouseから退出（直後は家が隠れ、一歩横へで戻る） | 6.5秒 |
| `clip_06_outdoor_3d_to_2d.mp4` | 6. 屋外へ戻り、2Dへ切替 | 8.0秒 |
| `haruka_phase3_walkthrough_pc_night.mp4` | 同じ流れを夜に撮ったもの（参考） | 29.0秒 |

3Dのカメラが、ﾊﾙｶﾁｬﾝを隠さずに追いかける様子が分かります。

## 46. Preview起動方法

Build: `C:\Users\owner\Documents\ChatGPT\HARUKA_WORLD_ver3.0_PHASE3_PREVIEW`（README_POC.txtあり、タイトルは「はるかわーるど ver3.0 Phase 3 Preview」）

```
cd C:\Users\owner\Documents\ChatGPT\HARUKA_WORLD_ver3.0_PHASE3_PREVIEW
python serve_poc.py --port 8893
```

- PC: http://127.0.0.1:8893/
- スマホ相当: http://127.0.0.1:8893/?touchControls=1
- Unity Editorは不要です。
- Phase 1（8890）、Phase 2（8891）、Phase 2.1（8892）のBuildは、それぞれのフォルダにそのまま残しています。

## 47. Git Commit

ブランチ `claude/ver3.0-2d3d-phase3`（Pushはしていません）:

| Commit | 内容 |
|---|---|
| `bf3edd6` | Phase 3の実装（Blenderのモデル・Script・Manifest・プレビュー、FBX、Catalog、屋内の組み立て、カメラ・クリック、検査・撮影、Fixture、README） |
| このレポート | Phase 3レポート（このCommitのIDは報告文で示します） |

## 48. 最終Git状態

- Phase 3のworktree: Commit後、`git status` は空です。
  - 検査や撮影の時に、Unityがmaterialに最後に描いたコマのTextureなどを書き込むことがあります。これはCommitに入れず、元に戻しました。
- Phase 1・2・2.1のworktreeとブランチは変えていません（6a5166c／7d5dbf9／cc72dc1、変更なし）。
- 元の作業ツリー: 開始時と同じ2件（` M GrassDecoration.prefab`、`?? backup-v05-…`）だけです。
- ローカル `main`（`d039acf`）と `origin/main`（`13462fe`）は変えていません。

## 49. 公開版保護状況

- 正式Map・Catalog（4ファイル）のSHA-256は、作業前と同じです。Player Sprite（50ファイル）のSHA-256も同じです。
- 変えていないもの:
  - 本編の `Assets/Scripts`・`Assets/Content`・ProjectSettings・Packages・WebGL Template・`halkaworld/`（差分なし）
  - `CharacterVisual`、Save Key
- 公開版ver2.9、ver3.0 Preview、GitHub Pages、公開HPは変えていません。
- mainへのMerge、Push、force pushはしていません。

## 50. 未解決事項

1. **物理スマホでの操作感と性能は未確認です。**
2. **3Dで家から出た直後、家全体が隠れます。** 「背後から映す」カメラの約束のため、カメラが家の向こうにあるからです。一歩動けば元に戻ります。直すにはカメラの約束を変える必要があるので、承認をいただいてから行います（51項・52項）。
3. **スマホの縦画面の3Dでは、ﾊﾙｶﾁｬﾝが大きく映ります。** 横方向に見える範囲が狭いためです（Phase 2.1の屋外と同じ）。
4. 3Dでﾊﾙｶﾁｬﾝの体に隠れた物は、体をクリックすれば調べられます。ただし、隠れた物そのものの位置はクリックできません（見えていないため）。
5. 座る・寝る・机で作業するAnimationはありません。Anchorだけを記録しています（ルールの追加は対象外のため）。
6. 補ったデザイン（4項）は仮のものです。家の側面・背面・窓、家具の裏などです。
7. Phase2EnvironmentChecksの1件を、Phase 3の状態を確かめる内容に書き換えました（40項）。
8. PCの576pxの高さの画面では、POCパネルが17行になったので、統計の下の数行が画面の外に出ます（POCのパネルだけの問題です）。性能はDPR 2で、すべての値を読みました。
9. 屋内の壁の分け方（一番近い端の面に属する）は、長方形の部屋を前提にしています。L字のような部屋をMAP EDITORで作った場合は、確認が必要です。

## 51. 次のPhaseへの提案

1. **退室直後のカメラ:**
   - 案A: 家から出た直後だけ、カメラを家の正面側（南）に置いて北を向かせ、ﾊﾙｶﾁｬﾝと家の正面を一緒に映す。最初の入力があるまで、背後への回り込みを待つ
   - 案B: 現状のまま
   - 案Aは、Phase 2.1で決めたカメラの約束に例外を作るので、承認が必要です。
2. **スマホ縦画面の3D:** 縦画面のときだけ視野を広げる（横方向に見える範囲を一定以上にする）か、カメラを少し引く。屋外にも影響するので、承認が必要です。
3. 正式なﾊﾙｶﾁｬﾝの3Dモデルが届いたら、Phase 2.1の22項の方式で差し替えます。座る・寝る位置は、今回のAnchorに合わせられます。
4. CrowのBlenderモデルと、木の `PerchAnchor` をつなぐこと。
5. POCを本編に統合すること（AUTO・Crow・保存を共通Worldへ）。
6. 物理スマホ（iPhone・Android）での性能の確認。

## 52. ユーザーへの確認事項

1. **家:** 外観（補った側面・背面・窓・屋根）と、入口のくぼみの見え方でよいか。
2. **設備・家具:** ベンチ・看板・井戸・ベッド・机・クッション・録音マイク・イーゼル・棚の形と色でよいか。補った部分（4項）を含みます。
3. **屋内の3D:** カメラ側の壁を低く切る見せ方と、カメラの距離・角度（2.9・52°）でよいか。
4. **退室直後のカメラ（50項の2）:** 案A（直後だけ家の正面から映す）にするか、現状のままにするか。
5. **スマホ縦画面の3D（50項の3）:** 縦画面だけ視野を広げるか、現状のままにするか。
6. **クリック:** ﾊﾙｶﾁｬﾝをクリックすると、向いている物を調べる仕様でよいか。
7. **次のPhase:** Phase 4へ進んでよいか。

承認をいただくまで、正式公開はせず、Phase 4にも進みません。
