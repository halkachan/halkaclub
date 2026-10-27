# はるかわーるど ver3.3 設計資料「Regionの接続（シームレスエリア拡張）」

作成日: 2026-10-25
対象: ゲーム本編（Unity 6000.3.10f1 / WebGL）、ブランチ `claude/harukaworld-v33`。
前提: `HARUKA_WORLD_VER32_ARCHITECTURE.md`（World座標・チャンク）。この資料はその上に「Regionを増やす手順」を足したものです。
クラス名・ファイル名・数値は実際のコードと同じです。コードを変えたら、この資料も直してください。

---

## 0. 一言でいうと

屋外は **1つのWorld（`outdoor_main`）** で、その上に **Map（Region）を並べて置く** 仕組みです。
Regionを足すのに必要なのは、

1. WORLD EDITOR v1.0 の形式で屋外Mapを1枚作る（`Authoring/*.hwmap.json`）
2. `outdoor_main.hwworld.json` に1行足す（MapID・World Offset・方角）

の2つだけです。シーン・遷移（Transition）・暗転は使いません。境界は「隣のセルへ1歩」です。

```
            World x →   -10 ............ 10 | 11 .......................... 36
  y=6                   ┌──────────────────┐┌────────────────────────────┐
                        │   first_field    ││        east_area_01        │
  y=-1  (道)  ══════════╪══════════════════╪╪════════ 黄色い花の広場 ════╡
                        │   (center)       ││  (east / ひがし / 黄)       │
  y=-6                  └──────────────────┘└────────────────────────────┘
                                        Region境界 x 10|11
                     チャンク境界  x -1|0        x 15|16        x 31|32
```

## 1. Regionの登録方法

### 1.1 World Registry

`unity-project/Assets/Content/Maps/World/outdoor_main.hwworld.json`

```json
{
  "format": "halka-world-outdoor",
  "formatVersion": 1,
  "worldId": "outdoor_main",
  "chunkSize": 16,
  "regions": [
    { "mapId": "first_field",  "offset": { "x": 0,  "y": 0 },  "direction": "center" },
    { "mapId": "east_area_01", "offset": { "x": 11, "y": -6 }, "direction": "east" }
  ]
}
```

| 項目 | 意味 |
|---|---|
| `mapId` | 屋外Map（`MapDefinition.MapId`、`MapType == "outdoor"`）。Authoring JSON の `mapId` と同じ |
| `offset` | そのMapのローカル(0,0)…ではなく **ローカル座標をWorld座標へ動かす量**。`world = local + offset` |
| `direction` | ver3.3で追加。`center` / `north` / `east` / `south` / `west`。省略時は `center`（ver3.2のRegistryもそのまま読める） |

読み込み: `PocSceneBuilder.LoadOutdoorWorld` → `OutdoorWorldRegistryJson.Parse(json, resolve, problems, themes)` → `OutdoorWorldDefinition`。
シーン生成時に一度だけ読み、`SharedWorldSession` に渡されます（ゲーム中に読み直しはしません）。

### 1.2 検証（`OutdoorWorldDefinition.Validate`）

次はすべて **ERROR**（ビルド時に止まる）です。

- World ID が不正 / formatVersion が違う / チャンクサイズが範囲外
- Regionが0個
- 存在しないMapID（`unknown Map …`）
- 同じMapを2回置いた（`placed twice`）
- 屋外でないMap（家・防音室など）を置いた
- 未知の方角、または方角のテーマがない
- Offsetが ±2^24 セルを超える／加算で桁あふれ
- **Region同士の重なり**（1セルでも重なれば `overlaps`）

±32,768セル（2^15）を超える場所は WARNING（float精度の注意）です。
これらは `WorldVer33Checks [registry]` で、わざと壊したRegistryを読ませて確認しています。

## 2. World座標の配置方法

- Regionの矩形 = `MapDefinition` の範囲（ローカル `min..max`）+ `offset`。
- first_field は ver3.2 と同じ `offset (0,0)`、World `(-10,-6)..(10,6)`。**動かしていません**（既存のセル・Object・セーブ座標がそのまま）。
- east_area_01 はローカル `(0,0)..(25,12)`（26×13セル）、`offset (11,-6)` で World `(11,-6)..(36,6)`。
  - 高さを first_field と同じ 13 行にしたので、Worldの外形 `(-10,-6)..(36,6)` の縦幅は変わりません。
  - first_field の東端 x=10 のすぐ隣 x=11 から始まるので、**すき間も重なりもありません**。

### 新しいRegionのOffsetの決め方

1. つなぎたい道のWorld座標を決める（東の道は World y = -1）。
2. 新しいMapのローカルで道を通す行（east_area_01 では local y = 5）を決める。
3. `offset.y = 道のWorld y − 道のローカル y`（-1 − 5 = -6）。
4. `offset.x = 隣のRegionの端 + 1 − 新Mapのローカル最小x`（10 + 1 − 0 = 11）。
5. `Validate` が重なりを見つけたら、どちらかを1セルずらす。

## 3. 接続する地面の設計

- 境界の両側のセルが **どちらも歩ける道** であること。east_area_01 は local (0,5) が道で、first_field の (10,-1) の道とつながります。
- 道は1本の連続した `dirt_road`。境界の両側で同じSurface・同じ見た目なので、継ぎ目は見えません（`[chunks] one continuous road`）。
- 境界の列（first_field x=10 と east x=11）には **ブロックするObjectを置かない**（置くとそこで道が切れます）。木や石は境界から1セル以上離しています。
- 境界にまたがる見た目（木の葉、看板の絵）は、Objectを持つRegionのものとして1回だけ描かれます（`[chunks] an Object at the region border … drawn once`）。
- どのRegionにも属さないセル（例 (37,-1)、(11,7)）は **歩けない・草なし** です。見た目は ver3.2 と同じ「Worldの外の地面」が少し続くだけです。

Map上の目印（Marker）:

| ID | ローカル | World | 用途 |
|---|---|---|---|
| `road_west` | (0,5) | (11,-1) | 東エリアの道の西の入口 |
| `road_east` | (25,5) | (36,-1) | 道の東端（将来さらに東へつなぐ場所） |
| `landmark_east_flower_plaza` | (16,4) | (27,-2) | ランドマーク「黄色い花が集まる小さな広場」の正面セル（ひまわりは (16,5) = World (27,-1)） |

## 4. ChunkとRegionの関係

- **チャンクはWorldのもの、RegionはMapのもの**。チャンク（16×16セル）はRegionと無関係にWorld座標で切ります。
- 1つのチャンクに複数のRegionのセルが入ってよい。例: チャンク (0,-1) には first_field (x ≤ 10) と east_area_01 (x ≥ 11) の両方が入る。
- だから **Region境界 ≠ チャンク境界**。
  - Region境界 x 10|11 をまたいでも、チャンクの出し入れは起きない（`[seamless] no chunk released or rebuilt by the crossing`）。
  - チャンク境界 x 15|16 は東エリアの中にあり、そこをまたいでもRegionは変わらない。
- `OutdoorWorldIndex` は起動時に全Regionのセル・Object・草を **1つのチャンク表** に入れます。表示（GameObject）だけが `ChunkStreamer` で出し入れされます（ver3.2と同じ）。
- 3D の先読み距離は 40 セルなので、スタート地点 (0,0) の時点で東エリア手前のチャンクはもう描かれています。境界で待つことはありません。

## 5. 2D／3D表示

- 2D と 3D は同じ `PocWorldPresenter` の同じチャンクを見ます（2Dは3D表示を正射影で撮る）。Regionごとの特別扱いはありません。
- 東エリアのObjectは Blender で作ったモデル（`blender/scripts/build_v33_east.py` → `Assets/POC2D3D/Models/V33/`）と、既存の木・石・白い花・ベンチです。
  - `flower_yellow`（黄色い花）、`sunflower_cluster`（ひまわり＝ランドマーク）、`sign_yellow`（黄色い看板と矢印）。黄色は `direction_themes.json` の east から読みます。
  - ひまわりは木と同じく、2D/3Dで手前に来たら薄くなります（Tree Fade）。
- 草の生え方は first_field と同じ規則（同じGround Builder）。
- デバッグ: パネルの「Region borders」をONにすると、チャンクごとに **方角の色の線** でRegion境界を描きます（初期値OFF。公開画面には出ません）。

## 6. Collision

- 通れるか: `OutdoorWorldIndex.IsBlocked(world)` = そのセルを持つRegionのMapの規則を **ローカルセル** で引く。Regionの外は常にブロック。
- 歩く: `SharedWorldSession` の普通の1歩。境界でも `MapChanged` は起きません。歩き終わったときにRegionが変わっていれば `RegionChanges` を数え、`RegionChanged(from, to)` を出します（デバッグ表示用）。
- AUTO: `PocGameplay.CanEnter` に `SameRegion` を追加。AUTOは **今いるRegionの外へは自分から出ません**（プレイヤーの操作では普通に出入りできます）。
- カラス: 住むのは first_field（Map上の配置のまま）。行動範囲も変えていません。

## 7. カメラ

- 3D: プレイヤーを追うだけ（ver3.2と同じ）。境界で動きが跳ねないことを1フレームごとの移動量で確認（最大 0.039 unit/frame）。
- 2D: `PocPixelView2D.ClampToMap` が「Worldの外を写しすぎない」ようにカメラ位置を範囲内に収めます。ver3.3 では **各Regionの中心の上には必ず立てる** ように範囲を広げました。
  - first_field しかないときは何も変わらない。
  - 東エリアを足しても、first_field の中では ver3.2 と **同じ位置** から写す（スタート (0,0) も西端 (-9,2) も `(0.00, 22.98, -19.28)` で一致）。
  - 境界では範囲が広がるだけなので、カメラは滑らかについていき、跳ねません（最大 0.031 unit/frame）。
- 東エリアでセーブして再開したときは、最初からプレイヤーの上にカメラがあります（スタート地点から滑ってこない）。

## 8. Save

- **Save Schema は 1 のまま**。保存するのは「MapID + そのMapのローカルセル」で、ver3.0〜3.2 と同じ形です。
  - 東エリアでは `east_area_01` とローカルセル（例 (9,6) = World (20,0)）。チャンク番号やWorld座標は保存しません。
- 読み込み（`HarukaSaveSystem.ResolveLocation`）:
  - 屋外のMapIDなら、そのRegionのOffsetでWorldセルに戻す。
  - そのセルがふさがっていたら、**まず同じRegionの中** で一番近い空きセル、なければWorld全体から探す（first_field のセーブが東エリアで再開されることはない）。
- Export／Import: 東エリアのセーブもそのまま書き出し・読み込みできます。
  - 未知のMapID（例 `east_area_99`）は `UnknownMap` で拒否、Mapの外のセルは `OutOfMap` で拒否（今のセーブは置き換えません）。
- セーブ画面の場所名: Regionは方角の名前（east_area_01 →「ひがし」）。家・防音室・first_field は従来どおり「家」「ぼうおんしつ」「そと」。
- 戻す場合（ver3.2 の Build に戻す）: 東エリアのセーブは「知らないMap」なので、ver3.2 は first_field のスタート地点から始めます。歩数・時間・設定は消えません。ただし ver3.2 で遊ぶと、その位置で上書き保存されていきます。

## 9. 方角別テーマ（将来の北・南・西）

`unity-project/Assets/Content/Maps/World/direction_themes.json`（形式 `halka-direction-themes` v1）で一元管理します。

| direction | 名前 | color | shade | 今 |
|---|---|---|---|---|
| center | まんなか | #f2eee6 | #c8c2b5 | first_field |
| north | きた | #5b84b1 | #3d5f86 | （予約） |
| east | ひがし | #f2c443 | #c99425 | east_area_01 |
| south | みなみ | #c8564b | #953b33 | （予約） |
| west | にし | #6e9f57 | #4b7a3b | （予約） |

- 地面は塗りません。花・看板・目印など **アクセント** にだけ使います。
- 色を読む場所: ゲーム（`OutdoorWorldDefinition.ThemeOf`、セーブ画面の場所名、デバッグ線）と Blender（`build_v33_east.py`）。同じファイルなので、色を変えるときは1か所です。
- 壊れたテーマ（色の形式違い、方角の欠け）は ERROR。

## 10. WORLD EDITORとの関係

- **WORLD EDITOR v1.0 本体は変更していません。** 東エリアは v1.0 Core（`MapFormat.SaveAtomic`、`MapRules.Validate`）を外部の小さなツールから使って作り、検証しています。
- 東エリアの Map JSON は v1.0 の形式そのもの（Map JSON v4）。WORLD EDITOR で開いて編集できます。
- 新しく足したObject（`flower_yellow`、`sunflower_cluster`、`sign_yellow`）は `object_catalog.hwcatalog.json` に追加しています。
  - 注意: 名前が `sign_north` / `sign_east` / `sign_south` / `sign_west` だと、取り込み時の旧名変換で `sign_basic` になります。方角の看板は `sign_<色>` などの別名にしてください。
- World Registry（`outdoor_main.hwworld.json`）と方角テーマはゲーム本編専用です。WORLD EDITOR v1.0 はこれを読みません（v1.1以降の課題）。

## 11. 新しいRegionを足す手順（チェックリスト）

1. Map を作る: WORLD EDITOR v1.0 で屋外Mapを新規作成（`mapType: outdoor`、Player Start・カラス・遷移は置かない）。
2. 接続する辺に道を通し、境界の列にはブロックするObjectを置かない。
3. `MapRules.Validate` が0件になるまで直す。
4. `outdoor_main.hwworld.json` に `{ "mapId", "offset", "direction" }` を1行足す（§2の計算）。
5. 方角の色を使うObjectが必要なら、カタログに足し、Blenderスクリプトで `direction_themes.json` の色から作る。
6. Unity で `PocSceneBuilder` を実行（Registryの検証が通ること）。
7. テスト: `WorldVer33Checks` と同じ観点（往復・1歩・MapChangedなし・チャンク・カメラ・セーブ・AUTO）を新Regionにも当てる。
8. ローカルWebGLで実際に歩いて往復し、2D/3D・セーブ再開を見る。
