# はるかわーるど ver3.6 北エリア設計資料（north_area_01 「きたのあおいみずべ」）

作成日: 2026-10-28 ／ 対象: ローカル実装（未公開）／ ブランチ `claude/harukaworld-v36`

この資料は、ver3.6 で追加した北の屋外Region `north_area_01` の構造と、その上に載る池・Interaction・カラス・そらをとぶ・セーブの扱いをまとめたものです。ver3.7 以降で別方向のRegionを足すときの手順も最後に書きます。

---

## 1. 北Regionの構造

| 項目 | 値 |
|---|---|
| Region ID（= Map ID） | `north_area_01`（正式採用。今後変更しない） |
| 表示名（Map） | 北エリア ／ セーブ画面の場所名は方向テーマの「きた」 |
| 方向 | `north`（`direction_themes.json` の既存値 `#5b84b1` / 暗 `#3d5f86`。ファイルは無変更） |
| 形式 | Map JSON v4（`halka-world-map` formatVersion 4、`mapType: outdoor`、`grassMode: auto`） |
| サイズ | 21 × 20 セル（Map-local X 0..20 / Y 0..19） |
| World Offset | (-10, 7) |
| World範囲 | X -10..10 / Y 7..26 |
| 所属World | `outdoor_main`（`Assets/Content/Maps/World/outdoor_main.hwworld.json` に1行追加） |
| 中身 | Surface 49（道 27、水 22）、Object 46、Marker 3 |

World Registry（追加後）:

```json
{ "mapId": "first_field",   "offset": { "x": 0,   "y": 0  }, "direction": "center" },   // Map自体が -10..10 / -6..6
{ "mapId": "east_area_01",  "offset": { "x": 11,  "y": -6 }, "direction": "east" },
{ "mapId": "north_area_01", "offset": { "x": -10, "y": 7  }, "direction": "north" }
```

既存2Regionの座標は動かしていません（`WorldVer36Checks` の [region] と [protect] で確認）。

## 2. World座標と接続

```text
            north_area_01  World (-10,7)..(10,26)
     ┌──────────────────────────────┐
     │ 道は World x=0 を y=7..26 まで   │
     │ 池は道の西 x -7..-2 / y 16..20  │  ← Chunk境界 y 15｜16 が池の手前を通る
     └──────────────┬───────────────┘
 ─ ─ ─ ─ ─ ─ ─ ─ ─ ─│─ Region境界 y 6｜7 ─ ─ ─ ─
     first_field     │ (0,6)            east_area_01
     (-10,-6)..(10,6)│ (0,5)            (11,-6)..(36,6)
                   (0,4) ← 草原の道はここで曲がる
```

- 草原の北の道（World x=0）は y=4 で東西の道と交わり、(0,5)(0,6) を経て北エリアの (0,7) に入ります。北エリアの道は local x=10（= World x=0）を y 0..19 まで一直線です。World (0,4)..(0,26) がすべて歩ける土の道として連続しています。
- Region境界の両側（y 6 と 7、x -1..1）にはふさぐObjectを置いていません。
- 境界の越え方は ver3.3 の東と同じです: 1つの `SharedWorldSession` が `OutdoorWorldIndex` で World座標 → Region を引くだけで、Scene切替・暗転・Map読み直し・ワープはありません。歩数は1歩1回、向き・AUTO設定・カラスはそのままです。
- Chunk は 16×16 セル。北エリアの中で y 15｜16 の Chunk境界をまたぎます（Region境界とは別の行）。`ChunkStreamer` が先読みしているので、境界を越えても止まりません。

## 3. 地形（Map-local の配置図）

`=` 道（dirt）、`~` 水（water）、`P` 池をのぞく岸辺（Action Point、発見Marker）、`L` すいれん、`r` 水辺のあし、`T` 木、`b` 青い花、`w` 白い花、`o` 石、`S` 道しるべ（青）

```text
19  . . . . . . . . . . = . . . . . . . . . .   World y=26
18  . . . . . . . . . . = . . . . . . . . . .   World y=25
17  . . . . . . . . . . = . . . . . . . . . .   World y=24
16  . . . . . T . . T . = . T b . . T . b . .   World y=23
15  . . T . b . . b . . = b . . . . . . . . .   World y=22
14  . . . . . . . . o . = . . . T . . . . . .   World y=21
13  . . b . r ~ ~ . . . = . . . . . . . . T .   World y=20
12  . T . ~ ~ ~ ~ ~ . o = . . . . . . . . . .   World y=19
11  . . . r ~ ~ ~ ~ ~ . = . T . . b . . w . .   World y=18
10  . . o . ~ ~ ~ ~ r b = . . . . . . . . . .   World y=17
 9  . . . . . ~ L ~ . . = . . . . . . T . . .   World y=16
 8  . . T o b = P = = = = . b . . . . . . . .   World y=15
 7  . T . . . . = = . . = . . . . w . . . T .   World y=14
 6  . . . . . . . . . b = . . . . . b . . . .   World y=13
 5  . . . b . w . . . . = b . . . . . . . . .   World y=12
 4  . . . . . . . . . . = . . . T . . . . . .   World y=11
 3  . T . . . . . . . . = . . . . . . . T . .   World y=10
 2  . . . T . . . . b . = . . b . . . . . . .   World y=9
 1  . . . . . . . . . . = S . . . . . b . . .   World y=8
 0  . . . . . . . . . . = . . . . . . . . . .   World y=7
    0 1 2 3 4 5 6 7 8 9 0 1 2 3 4 5 6 7 8 9 0
```

- 道は真北へ一直線。池は道の西に外れていて、道の正面はふさぎません（池の東端は World x=-2、道は x=0）。
- local y=8 の行に、道から池の岸へ向かう細い脇道（local x 5..9、と (6,7)(7,7)）があります。ここが「池を眺める小さな空間」です。
- 木立は池の西と北（local (1,7)(2,8)(1,12)(2,15)(5,16)(8,16)）と道の東側に点在させ、木がﾊﾙｶﾁｬﾝを隠しにくいよう、3D カメラの手前（池の南側）には木を置いていません（ver3.6 制作中に (6,3)→(3,2)、(4,6)→(2,8) へ移動）。
- 青い花 17、白い花 3、石 4、あし 3、すいれん 1、道しるべ 1、木 17。歩ける北エリアのセル 356 個は、どれも歩いて行けます（閉じ込められた角はなし）。

## 4. Object定義（Object Catalog `halka-world-catalog` v3 への追加）

既存の定義は1つも変えていません（追加のみ。Git の差分は追加行だけ）。

| definitionId | 種類 | 通行 | Action Point | 2D画像 | 3Dモデル |
|---|---|---|---|---|---|
| `water`（Surface） | surface | 不可（`blocksMovement: true`） | なし | `World/water.png` | なし（Chunkの中で平面メッシュを生成） |
| `flower_blue` | nature | 不可（黄色い花と同じ） | 正面から調べる「あおい はな。」 | `World/flower_blue.png` | `Models/V36/flower_blue.fbx` |
| `signpost_blue` | fixture | 不可 | 正面から「きたの みずべ。 しずかな いけが ある。」 | `World/sign_blue.png` | `Models/V36/sign_blue.fbx` |
| `water_lily` | nature | 通行判定なし（水の上に置く） | `look_into_water`: 1つ南のセルから上向きで「みずが きらきらしている。」 | `World/water_lily.png` | `Models/V36/water_lily.fbx` |
| `pond_reeds` | nature | 通行判定なし（水の上に置く） | なし | `World/pond_reeds.png` | `Models/V36/pond_reeds.fbx` |

- `signpost_blue` は ver3.3 の旧名変換（`sign_north` 系 → `sign_basic`）にかからない名前です。
- すいれん・あしは水のセルに置くため `blocksMovement: false`・`blockedCellOffsets: []` にしています。WORLD EDITOR v1.0 の MapRules は「通行不可のSurfaceの上に、通行不可セルを持つObject」を `surfaceOverlap` として拒否するためです（水そのものが通行不可なので、通れないことは変わりません）。
- 3D の見た目（Presentation Catalog）は `PocSceneBuilder` がコードで作ります。青い花・あしは `minPickSize` 0.3、すいれんは影なし・輪郭 0.5。
- Blender: `blender/scripts/build_v36_north.py`（ver3.3 の制作方式と同じ。ソース `blender/sources/v36/*.blend`、プレビュー `blender/exports/previews/v36/`、一覧 `blender/exports/v36_assets_manifest.json`）。

## 5. 池の表現

池は Object ではなく Surface `water` の**セルの集まり**です。形は Map の水セル 22 個そのもの（local x 3..8 / y 9..13 の不規則な形、およそ 5×4）。

- **3D**: `PocWorldPresenter.Water.cs` が、Chunk を作るときにその Chunk の水セルを1セル1枚の平面（高さ 0.012）で描きます。中央寄りは濃い青 `#3f6ca0`、岸寄りは明るい青 `#5c8dc2`、水と陸の境に土色の縁 `#7c6c4c`（幅 0.035）。地面と同じ頂点カラーのマテリアルなので、朝夜の明るさに地面と同じように従います。水面の光（白い短い線）は2組あり、約0.7秒ごとに交代して静かにきらめきます（シェーダーなし、反射カメラなし、Render Texture なし）。
- **2D**: 2D表示はこれまでどおり同じ3Dメッシュを正射影のドット絵レンダーで描くので、**池の形と位置は2Dと3Dで必ず一致**します（Point Sampling、16px／セル、アンチエイリアスなし）。`water.png` は WORLD EDITOR のパレット用の見本画像です。
- Chunk を解放すると水のメッシュも一緒に解放されます（`WaterCellsDrawn` が増え続けないことを検査済み）。

## 6. 水面のCollision

- `water` は `blocksMovement: true` の Surface なので、`SharedWorldSession` の通行判定で水のセルへの1歩は拒否されます（その場で向きだけ変わる）。2D/3D共通です。
- AUTO の経路探索、カラスの地上経路・着地点探し、そらをとぶの安全着地探索は、どれも同じ通行判定を使うので、水のセルを選びません。
- 水のセルには草が生えません（`grassMode: auto` が水を除外）。

## 7. Interaction「みずをのぞく」

- 対象: すいれん（`water_lily`、local (6,9) = World (-4,16)）。その Action Point `look_into_water` は1つ南の岸辺 local (6,8) = World **(-4,15)** で、上向き。
- 操作: 既存の Object Interaction そのまま。PC は正面のすいれんをクリック、スマホは正面にAボタン。新しいボタンはありません。岸辺以外のセル（となり・一歩うしろ）からは何も起きません。
- 表示: 既存のメッセージ欄に「みずが きらきらしている。」（既存 Bitmap Font の文字だけで書ける文面）。
- 演出: Interaction 発生時に、彼女の向いている先の水のセル（なければすいれんの位置）に波紋を1回だけ出します。2重の輪が 1.4 秒で半径 0.45 まで広がって消える、頂点カラーの輪メッシュです。**演出の間も操作は止まりません**（何も待たない）。
- 汎用 Event Runtime は使っていません。`PocWorldPresenter` が既存の `Interacted` 通知を受けて、定義IDが `water_lily` のときだけ波紋を出す、軽い専用処理です。WORLD EDITOR 側の機能追加もありません。
- ﾊﾙｶﾁｬﾝを水面に映す表現は今回入れていません（任意項目のため。必須の波紋とInteractionを優先）。

## 8. カラス

- 追従（ついてきて）中のカラスは、同じ World 座標の上で彼女について北エリアへ入り、Chunk境界 y 15｜16 を越えても1羽のまま・追従のままです。
- 池のまわりでは陸を通る経路で追いかけます（届かないときは既存の飛行で回り込む）。着地点の候補は通行判定で選ぶので、水のセルには降りません（60回の着地選択で水0回、散歩中の「水の上にいたフレーム」0）。
- 彼女が池のそばで止まっていると、近くの北の木（既存の木 `tree_basic` の止まり点）で休みます。2羽目は作りません。
- 北エリアで「あそんでおいで」にすると自由行動に戻り、従来どおり自分の木（first_field）へ帰ります。
- カラスのコード・スプライト・モデル・鳴き声は変更していません（北の木は既存の木なので、そのまま止まり木の候補になります）。

## 9. Fast Travel（そらをとぶ）

`Assets/Content/FastTravel/fast_travel_points.json`（`harukaworld-fast-travel` v1）に追加:

```json
{
  "id": "landmark_north_blue_pond",
  "name": "きたのみずべ",
  "worldId": "outdoor_main",
  "mapId": "north_area_01",
  "discoverCell": { "x": 6, "y": 8 },
  "discoverRadius": 2,
  "arrivalCell": { "x": 7, "y": 8 },
  "arrivalFacing": "up",
  "landingSearchRadius": 4,
  "unlockedAtStart": false,
  "order": 20
}
```

- 発見: 手動の1歩が終わったとき、岸辺 local (6,8)（World (-4,15)）からマンハッタン距離2以内なら初回だけ解放し、既存の「そらをとべるばしょを みつけた！」を出します。道（World x=0）からは距離4なので、道を歩くだけでは見つかりません。脇道に入って World (-2,15) に来たときに見つかります。AUTO の歩行・そらをとぶの到着では発見しません。
- 到着: local (7,8) = World **(-3,15)**、上向き（池を眺める向き）。歩ける岸辺で、水・木・石・カラスと重なりません。着地前に必要な Chunk を準備します。到着しただけでは「みずをのぞく」は起きません。
- 一覧（1ページ）: ハルカチャンの家 / ひがしのはなのひろば / きたのみずべ / やめる。4地点以上のページ切替は ver3.5 のまま残っています。
- 飛行演出は ver3.5 の約2.6秒のものをそのまま使います（北専用の演出はなし）。歩数は増えず、朝夜の時計も変わらず、カラスの同行状態も変わりません。
- 3地点の間の移動（家⇄北、東⇄北、北⇄家）を2D・3Dの両方で検査済みです。

## 10. Save

- Save Schema は **1 のまま**。北エリアの位置は従来どおり `Map ID + Map-local Cell`（例 `north_area_01` (6,8)）で保存されます。向き・2D/3D・AUTO・歩数・プレイ時間・朝夜・カラス同行・発見履歴も従来の項目のままです。
- 発見履歴は ver3.5 の `extensions.fastTravel` に `landmark_north_blue_pond` が加わるだけです。`extensions.crowCompanion` は別の項目として残り、互いに上書きしません。CRC・Snapshot・Backup・Export/Import の仕組みは無変更です。
- ver3.0〜3.5 のセーブ: そのまま読み込めます。北の地点は未発見（正常）、家と東の広場の発見状態は保たれます。
- 不正データ: 存在しない北の Map ID は `UnknownMap`、Map 外のセルは `OutOfMap` で従来どおり拒否。水のセルを指すセーブは、北エリアの一番近い歩けるセルで再開します。
- **古いBuildへ戻した場合（Rollback）**: ver3.5 以前の Build は `north_area_01` を知らないため、北エリアで保存したセーブを読むと「不明な Map」として**最初の Map の開始位置**から始まります。歩数・プレイ時間・カラス同行・発見履歴は残ります（ver3.5 の `PocFastTravel.SetFound` は、知らない地点ID〔後の版の地点〕も捨てずに保持して書き戻し、一覧には出しません）。ただし、古い Build で上書き保存すると、位置は最初の Map の位置で保存されます（北にいた事実は失われますが、壊れはしません）。ver3.6 に戻せば、北の地点は発見済みのまま飛べます。これは実際のローカル ver3.5 Build（ポート 8938）に ver3.6 の池のそばのセーブを入れて確認しました（ver3.5 で開く → 最初の場所 (0,0) から開始・エラーなし → ver3.5 が保存し直したファイルでも歩数 33→34・プレイ時間 22→25 秒・北の発見・カラス同行が残る → そのファイルを ver3.6 に戻すと北へ飛べる。5/5 PASS）。

## 11. 2D カメラ（北方向）

ver3.3 までの 2D カメラは、すべてのRegionを囲む範囲でカメラ位置を Clamp していました。北エリアが加わると範囲が北へ広がるため、そのままでは草原（first_field）の構図が変わってしまいます。そこで ver3.6 では次の規則（`PocPixelView2D.ClampToMap` の帯（band）規則）を入れました。

1. first_field と同じ行（y -6..6）にあるRegion（first_field・東）の中では、**これまでと同じ Clamp**（草原・東の構図は ver3.5 と同じ。検査で座標が一致）。
2. 北エリアに入ると、境界の時点でカメラと彼女の間に残っていた差を、`CatchUpRows` = 12 行かけて 0 に縮めます。カメラの動きは最大でも彼女の速さの (1 + 差 / 12行) 倍なので、**跳ねません**（実測で1フレーム最大 0.094 unit）。
3. 北の端（World y=26）では Map の端でカメラが止まり、彼女は画面内に見えています。
4. この規則は「first_field の行の外にRegionがあり、かつ first_field の行にあるRegionがちょうど first_field の行に収まっている」ときだけ働きます（ver3.2 の検査用 World のように、横のRegionの方が背の高い World では従来どおり）。南に将来Regionを足しても対称に働きます。

3D カメラは従来どおり彼女の南側から追うだけで、変更はありません。保存した位置から再開したときは、2D/3D とも最初のフレームからその位置の構図で始まります（跳ねない）。

## 12. 今後のRegion追加手順（ver3.7 西エリアなど）

ver3.6 で使った手順と道具は、次の方向のRegionにもそのまま使えます。

1. **座標を決める**: 既存Regionと重ならない Offset を決める（既存Regionは動かさない）。西なら first_field の西隣、たとえば `west_area_01` を X -31..-11 / Y -6..6 など。接続する道のセルを両側で合わせる。
2. **Map JSON v4 を作る**: `tools/poc2d3d/v36_north_author/`（WORLD EDITOR v1.0 の MapFormat / MapRules をそのまま参照する小さなコンソール。エディタ本体は書き換えない）をコピーし、配置を書き換えて実行すると、`Assets/Content/Maps/Authoring/<id>.hwmap.json` を出力し、MapRules の検査結果（0 issues）を表示します。
3. **新しいObject**が要るときは、Blender スクリプトでモデル（FBX）と2D画像を作り、`v36_catalog_add.py` 方式（自分の追加分だけを入れ替える冪等スクリプト）で Object Catalog に追加する。既存IDは変えない。旧名変換にかからない名前にする。
4. **World Registry** に1行追加（`outdoor_main.hwworld.json`）。
5. **Unity に取り込む**: `MapAuthoringImporter.SyncAll` で `.asset` キャッシュを作り、`PocSceneBuilder` で Scene を作り直す。
6. **カメラ**: 東西方向のRegionは first_field の行の中なら追加作業なし。行の外（北・南）なら ver3.6 の帯規則がそのまま働く。
7. **そらをとぶ**: 地点を `fast_travel_points.json` に足す（発見セルは道から距離3以上、到着セルは歩ける岸や広場）。4地点を超えると一覧は既存のページ切替になる。
8. **検査**: `WorldVer36Checks` を雛形にした `WorldVer37Checks` を作る。旧テストでRegion数や地点数を固定している箇所は「2以上」「含む」の形に直す（ver3.6 で V33/V35 を直した方法）。
9. ローカル WebGL での実操作検証（徒歩・境界・Interaction・飛行・保存再開・スマホ表示）。

注意: 水を使うときは、水の上に置く飾り（すいれん・あし）は `blocksMovement: false` / `blockedCellOffsets: []` にしないと WORLD EDITOR の MapRules に拒否されます。
