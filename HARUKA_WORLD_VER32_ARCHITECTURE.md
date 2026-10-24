# はるかわーるど ver3.2 設計資料「屋外World座標とチャンク」

作成日: 2026-10-24
対象: ゲーム本編（Unity 6000.3.10f1 / WebGL）、ブランチ `claude/harukaworld-v32`。
この資料は実際のコードに合わせて書いています。クラス名・ファイル名・数値はコードと同じです。コードを変えたら、この資料も直してください。

---

## 0. 全体の形

```
Authoring Map JSON（WORLD EDITOR v1.0 の形式のまま）
        │  MapAuthoringImporter（変更なし）
        ▼
MapDefinition（first_field / halka_house / halka_soundbooth …）
        │
        │  Assets/Content/Maps/World/outdoor_main.hwworld.json（ver3.2で新設・ゲーム本編専用）
        ▼
OutdoorWorldDefinition（World ID・チャンクサイズ・Region一覧 = MapID + World Offset）
        │  OutdoorWorldIndex（Worldの論理状態：セルの通行・草・Surface・Object・チャンク表）
        ▼
SharedWorldSession（2D/3D共通の唯一のゲーム状態：位置・向き・歩数・遷移）
        │
        ├─ SharedWorldModel（屋外 = OutdoorWorldIndex を包む / 屋内 = MapDefinition 1枚）
        │
        └─ PocWorldPresenter（表示）── ChunkStreamer（どのチャンクを描くか）
                 │
                 ├─ 2D View（PocPixelView2D：同じ3D表示を16px/セルの低解像度で撮る）
                 └─ 3D View（PocThirdPersonCamera）
```

- **論理（Collision・草・Object・Action Point）は World 全体を常に持つ**。チャンクで出し入れするのは **表示（GameObject・Mesh）だけ** です。だから、表示が間に合わない・失敗した場合でも、通れる／通れないの判定は変わりません。
- 2D と 3D は同じ `PocWorldPresenter` の同じチャンクを見ます（2D は 3D 表示を正射影で撮っているため）。2D 専用・3D 専用のチャンクや位置は存在しません。

## 1. World 座標モデル

| 項目 | 内容 | コード |
|---|---|---|
| World ID | `outdoor_main` | `OutdoorWorldDefinition.DefaultWorldId` |
| World Cell | 整数 (x, y)。x = 東、y = 北。1セル = 0.5 Unity unit | `Vector2Int`、`GridSpace3D.CellSize` |
| first_field | Offset (0,0)。World Cell = first_field のセル（(-10,-6)〜(10,6)） | `outdoor_main.hwworld.json` |
| Region | 屋外 Map 1枚を World のどこに置くか（MapID + Offset） | `OutdoorRegionLink` / `OutdoorRegion` |
| 変換 | World = Map-local + Offset、Map-local = World − Offset | `OutdoorRegion.ToWorld / ToLocal` |
| 屋内 | `halka_house`・`halka_soundbooth` は World に入れない。従来どおり MapID + Map-local Cell | `SharedWorldModel(MapDefinition)` |

- セッションの位置（`SharedWorldSession.Cell`）は、**屋外では World Cell、屋内では その Map のセル** です。どちらかは `session.World` が屋外モデルかどうかで決まります（`SharedWorldModel.IsOutdoorWorld`）。
- 共通の変換口:
  - `SharedWorldSession.TryToSessionCell(mapId, mapCell, out world, out sessionCell)` … セーブ・Import の「MapID + Map-local」をセッションの位置へ。屋外 Region の MapID なら World Cell に、屋内ならそのまま。
  - `SharedWorldSession.TryGetSavePlace(out mapId, out mapCell)` … 今いる場所を「MapID + Map-local」で返す（屋外なら、立っている Region の MapID とその Map のセル）。
- Unity の Transform（float）は表示用です。正式な位置は常に整数セルです。

### 1.1 整数・浮動小数の範囲（調査結果）

| 値 | 意味 | 根拠 |
|---|---|---|
| `WorldGrid.MaxAbsCell` = 2^24 = 16,777,216 | 論理座標の上限。Offset・Region の範囲がこれを超えると `Validate` が ERROR | int で数個の和を取ってもあふれない。`CheckedAdd` はあふれると例外（折り返さない） |
| `WorldGrid.RenderSafeAbsCell` = 2^15 = 32,768 | 表示の安全範囲。これを超える Region は `Validate` が WARNING | ±16,384 unit まで、1/4セルの歩行補間が float で正確（検査で確認）。2^23 unit では 0.125 が失われる |

- 現在の first_field（±10セル）とテスト World（±128セル）は、どちらも十分小さい範囲です。全 `Vector2Int` を別の型へ置き換える必要はありません（調査のみ。置き換えはしていません）。
- 将来 ±32,768 セルを超える World を作る場合は、Floating Origin（表示の原点をプレイヤー付近へずらす）を追加してください。論理座標はそのままで足ります。

## 2. チャンク座標モデル

| 項目 | 内容 | コード |
|---|---|---|
| チャンクサイズ | 16×16 セル（registry の `chunkSize`。4〜256 を受け付ける） | `OutdoorWorldDefinition.chunkSize`、`WorldGrid.RequireChunkSize` |
| World → Chunk | 数学的な Floor Division（負の数も正しく） | `WorldGrid.FloorDiv` / `ChunkOf` |
| Chunk-local | Floor Mod。常に 0〜size−1 | `WorldGrid.FloorMod` / `LocalInChunk` |
| Chunk → World | origin + local（local の範囲外は例外） | `WorldGrid.ChunkOrigin` / `ChunkToWorld` |
| チャンクの範囲 | 両端を含む矩形 | `WorldGrid.ChunkRect` → `CellRect` |

16セルの場合: -1→-1、-16→-1、-17→-2、0→0、15→0、16→1（`WorldVer32Checks` の coordinates で確認）。

first_field は、チャンク (-1,-1)・(0,-1)・(-1,0)・(0,0) にまたがり、その外側 8 セルの飾り（`outsideMargin`）を含めると (-2..1, -1..0) の範囲になります。

チャンクサイズを 16 にした理由と比較の数値は、実装レポート 12・13章にあります。

## 3. Region と World の関係

- 正式な Region は `first_field` だけです（Offset (0,0)）。
- 定義ファイル: `unity-project/Assets/Content/Maps/World/outdoor_main.hwworld.json`

```json
{
  "format": "halka-world-outdoor",
  "formatVersion": 1,
  "worldId": "outdoor_main",
  "chunkSize": 16,
  "regions": [
    { "mapId": "first_field", "offset": { "x": 0, "y": 0 } }
  ]
}
```

- 読み込み: シーン生成時に `PocSceneBuilder.LoadOutdoorWorld` → `OutdoorWorldRegistryJson.Parse`（Editor）。結果の `OutdoorWorldDefinition` はシーンの `SharedWorldSession` に保存されます（実行時に JSON は読みません）。
- 検証（`OutdoorWorldDefinition.Validate` と `Parse`）。ERROR が1つでもあればシーンを作りません:
  - format / formatVersion
  - World ID の文字
  - チャンクサイズ 4〜256
  - Region が1つ以上
  - Map が存在する
  - mapId と Map の ID が一致
  - 同じ Map を2回置いていない
  - **屋外 Map だけ**（`MapType == "outdoor"`。屋内 Map は ERROR）
  - Offset のあふれ
  - ±2^24 の範囲
  - **Region どうしが重ならない**
  - ±32,768 セルを超える場合は WARNING
- Authoring Map（JSON / asset）は一切書き換えません。Region の位置は registry だけが持ちます。
- `MapDefinition` の側は何も変わっていません。`MapPlacementRules.Validate` も、各 Region の Map ごとに従来どおり実行します（`SharedWorldModel(OutdoorWorldIndex)`）。

## 4. Shared World Session

- `SharedWorldSession`（`Runtime/SharedWorldSession.cs`）が唯一のゲーム状態です。屋外のモデルは1つ（`OutdoorWorld`）で、Region が複数でも1つの World として持ちます。
- `Initialize` は、registry の World から `OutdoorWorldIndex` を作り、Player Start（first_field の Map セル）を World Cell に変換して置きます。
- `FindWorld(mapId)` は、Region の MapID なら屋外モデルを返します。
- 一歩の判定（`TryStep`）は、`World.CanEnter(cell) && CanPlayerEnter(cell) && Prepare(cell)` です:
  - `CanEnter` … World 座標での通行判定（屋外は `OutdoorWorldIndex.IsBlocked`）
  - `CanPlayerEnter` … カラスの占有
  - `Prepare` … 表示側の準備（後述の Step Gate）。**常に true を返し、一歩を断ることはありません**
- 表示側への接続は2本の関数だけです: `PrepareCell`（一歩の前にそのセルのチャンクを描かせる）と `CellReady`（そのセルが描かれているか。AUTO 用）。セッションは表示の型を知りません。

## 5. 2D／3D の Presenter

- `PocWorldPresenter`（`PocWorldPresenter.cs` + `PocWorldPresenter.Chunks.cs`）
  - 屋外では **チャンクモード**（`ChunkMode`）。屋内では従来どおり Map 1枚をまとめて作ります。
  - 地面・道・草の房（tufts）は `PocEnvironmentBuilder.BuildChunk` で、チャンクごとに作ります。セルごとの規則とハッシュ（`PocStableHash.Of(mapId, mapCell, salt)`、キーは Region の MapID + Map セル）は ver3.1 の `Build` と同じです。そのため first_field は、ver3.1 と同じ絵になります（草のタイル数・道の部品・房の位置・三角形数が一致することを検査済み）。
  - 道は World 全体で先に集め（`CollectWorldRoad`）、それをチャンクで切り出します。チャンク境界で道の形が変わりません。
  - Object は、その **Root セルのチャンク** が作ります。Outline ID は ver3.1 と同じく「Map の Object 順 % 249 + 1」です。
  - プレイ中はチャンクごとに Static Batching。結合 Mesh は、チャンクを解放するときに一緒に破棄します。
- `PocPixelView2D`（2D）
  - 2D の草（2D ゲームの Sprite）は、**描かれているチャンクの草セルだけ** を北の行から並べて描きます（`EnsureGrass`。並びは ver3.1 と同じ）。
  - 草があるかどうかの判定は、World 全体の論理（`session.World.HasGrass`）を使います。
  - 2D の画面に映るセル数（`ViewHalfCells2D`）を毎フレーム Presenter に伝えます。
  - カメラの端の制限（`ClampToMap`）は World の範囲（`MinCell`/`MaxCell` = World の Bounds）です。
- 3D（`PocThirdPersonCamera`）は変更していません。3D が見通す距離は `ReachCells3D = 40` セル（20 unit。カメラの距離 3.4・ピッチ 26°で、地面の細部が見える範囲）です。
- **2D と 3D は同じチャンク集合** を使います。読み込む範囲は「2D の画面」と「3D の見通し」を合わせた矩形です。そのため、表示を切り替えてもチャンクの作り直しは起きません（検査: 12回切り替えて、作成・解放とも 0）。

## 6. Chunk Lifecycle

```
Unloaded ──(必要になった)──▶ Queued ──(順番が来た)──▶ Loading ──▶ Ready
   ▲                          │  不要になった              │ 失敗
   │                          ▼  （キャンセル）             ▼
   │                       Unloaded                      Failed ──(1秒×回数後に再試行)──▶ Loading
   │                                                       │ 2回失敗 / Step Gate
   │                                                       ▼
   │                                              Ready（fallback：平らな地面）
   │
   └──(Hysteresis 帯の外へ出た)── Ready（必要範囲の外 = 解放候補。帯の中では残す）
```

`ChunkStreamer`（`Runtime/World/ChunkStreamer.cs`、Unity の Object を持たない純粋な C#）が、毎フレーム次のことを行います:

1. **Required（必要）**:
   - 表示矩形の下のチャンク。表示矩形は、プレイヤーの World Cell を中心に `max(2D の半分, 3D の見通し)`。
   - 歩いている向きには、`PrefetchCells`（= チャンクサイズの半分）だけ矩形を広げます（先読み）。
   - 他のチャンクの Object の絵（`PresentationExtent`）が表示矩形にかかる場合は、その Object の持ち主チャンクも加えます。
2. **Keep（維持）**: Required を `HysteresisChunks = 1` チャンク分広げた範囲です。Keep の外へ出た Ready チャンクだけを解放します。境界を何度往復しても、作り直しは起きません（検査: 40回往復して、作成・解放とも 0）。
3. 足りないチャンクを近い順に Queue へ入れ、時間予算 `ChunkBudgetMs = 4ms` の中で作ります（1フレームに最低1つ）。Required から外れた Queue は、作る前にキャンセルします。
4. **解放** は `Release` → `ReleaseChunkPayload` です。チャンクの GameObject と、そのチャンクが作った Mesh（地面・房・debug 線・Static Batch の結合 Mesh）を `Destroy` します。`SetActive(false)` ではありません。Catalog のモデル・Material・Map 定義は共有なので、残します。

- 失敗時の動き:
  - **Step Gate**（`EnsureReady`）: 一歩の前に、そのセルのチャンクがまだなければ、その場で作ります（`SyncLoads` に数えます）。失敗すれば fallback（平らな地面）にします。プレイヤーが止められることはありません。
  - 通常の読み込みが失敗した場合: Failed になり、1秒×回数の後に再試行します。2回失敗したら fallback にします。原因がなくなれば、次に読み込むときは正しく作り直します。
  - 3D モデルや Catalog が見つからない Object: 従来どおり、灰色の箱（fallback 表示）で描きます。
- 統計: `ChunkStreamerStats`（読み込み・解放の回数と ms、待ち数、失敗、fallback、`SyncLoads`、`ImmediateLoads`）。
- 状態の保存: チャンクの状態は **一時キャッシュ** です。セーブには一切書きません。

| 管理する情報 | どこにあるか |
|---|---|
| Chunk ID / 座標 | `ChunkCoord` |
| World 座標での境界 | `ChunkStatic.Rect` |
| 読み込み状態 | `ChunkRecord.State` |
| 地面・Surface | `OutdoorRegion`（論理）、`ChunkPayload.Ground/Tufts`（表示） |
| Object 配置 | `ChunkStatic.OwnedObjects`（Root がこのチャンク）、`Reaching`（他チャンクから絵が届く Object） |
| Collision | `OutdoorRegion.CellFlags`（World 全体。チャンクを解放しても消えない） |
| 2D / 3D Presentation | `ChunkPayload`（2D と 3D で共通） |
| Player との距離 | `ChunkCoord.ChebyshevDistance`（Queue の順番） |
| 最終参照 | `ChunkRecord.LastNeededTick` |
| ロード時間・回数 | `ChunkRecord.LoadMilliseconds / Loads`、`ChunkStreamerStats` |

## 7. Object の所有権

- 1つの Object は、ちょうど1つのチャンク（Root セルのチャンク）が持ちます（`ChunkStatic.OwnedObjects`）。そのため、二重に作られることはありません（検査: `DuplicateViews` が常に 0）。
- Object の絵が届く範囲（`OutdoorWorldIndex.PresentationExtent`）は、次のものを合わせ、周囲に 1 セル足した矩形です:
  - Footprint（Root Anchor から）
  - Blocked Footprint
- 絵が他のチャンクへ届く場合、そのチャンクに `Reaching (持ち主チャンク, 範囲)` を登録します。表示矩形がその範囲にかかる場合だけ、持ち主チャンクを Required にします。家が境界をまたいでも、半分だけ消えることはありません。
- 当たり判定（Blocked）と Action Point は、論理側（World 座標）にあります。チャンクとは関係なく、いつも正しく判定します。インタラクションの検索（`FindInteractionAhead`）は、プレイヤーの周りの 3×3 チャンクの Object だけを見ます（`ObjectsNear`）。結果が2つ以上ある場合は、Map の Object 順で最初のものを選びます（ver3.1 と同じ結果）。

| 区別 | 意味 | どこ |
|---|---|---|
| Root Cell | Object の基準セル（持ち主チャンクを決める） | `SharedObjectInstance.RootCell`（World）・`LocalRootCell`（Map） |
| Visual Footprint | 絵の広がり | `PresentationExtent` |
| Blocked Footprint | 通れないセル | `OutdoorRegion` の `Blocked` フラグ |
| Action Point | 座る・調べるセル | `WorldObjectDefinition.ActionPoints`（Root + offset） |
| Presentation 範囲 | 持ち主チャンクを呼ぶ範囲 | `ChunkStatic.Reaching` |

## 8. 動的 Entity（カラス・プレイヤー）

- プレイヤーとカラスは、どのチャンクの子でもありません。シーンのルートに1つずつあります。状態（位置・行動・目的地）は、セッションと `PocCrow`（`CrowBehaviourCore`）が持ちます。チャンクを作り直しても、二重になったり Spawn 位置へ戻ったりしません（検査: 128 World で遠くへ往復しても、カラスは1羽のまま、行動を続ける）。
- カラスの止まり木:
  - 候補は「カラスの Map（Region）の木 Object」から作ります（論理）。描かれていない木でも、止まる点（PerchAnchor）はモデルの形から計算します（`PocWorldPresenter.TryGetPerchPosition`。木の種類ごとにキャッシュ）。描かれた木の PerchAnchor と計算値は一致します（誤差 < 0.001）。
  - そのため、飛行中に目的の木のチャンクが解放されても、目的地は失われません。
- ver3.2 では、カラスの行動範囲は、ver3.1 と同じく自分の Region（first_field）の中です。将来の追従や「そらをとぶ」は、World 座標をそのまま使えます（`PocCrow` の位置は World Cell）。
- テスト用の NPC は作っていません。

## 9. Collision

- 通行判定は、World 座標の1つの経路です: `SharedWorldModel.CanEnter(world)` → `OutdoorWorldIndex.IsBlocked(world)` → `OutdoorRegion.FlagsAtWorld`。
- `OutdoorRegion` は、Map を作るときに、`MapPlacementRules` と同じ意味でセルごとのフラグ（Blocked / Grass / Road / BlockingSurface）を一度だけ刻みます。以後の判定は配列を1回見るだけです（ver3.1 は Object 全部を毎回調べていました。256×256 では遅すぎるため）。
- **どの Region にも入っていないセルは通れず、草もありません**（自動で草原を作りません）。
- 一致の確認:
  - first_field の全セルと外側 9 セルで、`MapPlacementRules.BlocksMovement / HasGrass` と同じ答えになることを確認しました。
  - 128×128 のテスト World（4 Region、うち3つは Offset あり）の全セルでも同じです。
- 一歩の途中でチャンクが変わっても、移動は `GridStepMotion` が最後まで行います。歩数・足音・草の揺れは、一歩につき1回です（検査: 2つの境界を越える 23 歩で、23 歩・足音 23 回）。

## 10. AUTO

- `PocAutoWorld.CanEnter` = `World.CanEnter(cell) && session.IsCellReady(cell)`。AUTO は、描かれていないチャンクへは入りません。経路探索（`GridPathfinder2D`）も、描かれた範囲の中だけを探します。World 全体の探索はしません。
- 家の扉は、World 座標の扉（`OutdoorWorld.TryGetHouseDoor`）です。AUTO が遷移を勝手に起こさない規則（`CanAutoEnter`）は、ver3.1 のままです。屋外の判定は `"first_field"` の名前ではなく、registry の屋外 Map（`IsOutdoorMap`）で行います。

## 11. Save 互換

- **Save Schema 1 のまま** です。保存する位置は、従来どおり「MapID + Map-local Cell」です。
  - first_field にいるとき: MapID `first_field`、Cell = World Cell（Offset 0 なので同じ値）。ver3.1 が書くセーブと同じ値です。
  - 将来の別 Region にいるとき: その Region の MapID と、その Map のセル（`TryGetSavePlace`）。
  - World ID・チャンク座標・チャンクの状態は書きません（検査: 保存した JSON に `chunk` や `outdoor_main` が含まれない）。
- 読み込み（`HarukaSaveSystem.ResolveLocation`）:
  - 屋外 Region の MapID なら、World Cell へ変換して、屋外のモデルで「安全なセル」を探します。
  - 安全なセルの条件は ver3.1 と同じです: 通れる、扉・出口ではない、近い所。
  - 見つけたセルを、MapID + Map セルに戻して適用します。
- ver3.0 / ver3.1 のセーブ・Export ファイル・壊れた最新 Snapshot からの復旧は、そのまま動きます（`WorldVer32Checks` の save、`WorldVer31Checks` の old-saves）。

### 11.1 ver3.3 で複数 Region へ入れるようになったときの保存

- Schema 1 の「MapID + Map-local Cell」は、Region の MapID でもそのまま使えます。**ver3.3 で新しい Region を足すだけなら、Schema の変更は不要** です。
- 変更が必要になるのは、次の場合です:
  - (a) 1つの Map を World の2か所以上に置く場合
  - (b) Region の Offset を後から動かす場合
- そのときの方針（案）は次のとおりです:
  - Schema 2 で `worldId` と `worldCell` を任意項目として追加し、MapID + Map セルも併記します（旧版でも読める）。
  - 読み込みは、Schema 2 なら worldCell を優先します。Region の情報と食い違う場合は、MapID + Map セルを使います。
  - 移行: Schema 1 → 2 は、読み込み時に変換します（元の Snapshot は残す）。
  - Rollback: Schema 2 を ver3.2 以前で開くと、`HasNewerSchema` で保存を止めます（既存の保護。データを上書きしない）。
  - 旧 Import は、Schema 1 のファイルを、今と同じ経路で受け付けます。
- Offset を動かす場合は、registry に `formatVersion` を上げた「旧 Offset 表」を残します。セーブの MapID + Map セルは、Offset が変わっても正しい場所を指すので、移行は不要です。

## 12. WORLD EDITOR との関係

- WORLD EDITOR v1.0（`tools/HalkaWorldMapEditor`）のソースと EXE には、手を加えていません。Map JSON v4・Map Logic v1・Object Catalog v3・Entity Catalog v1・Presentation Catalog の形式も、そのままです。
- WORLD EDITOR で作る1枚の屋外 Map が、そのまま1つの Region になります。World 上の位置は、ゲーム本編側の registry（`outdoor_main.hwworld.json`）に書きます。Map の座標は、Map の中の座標（Map-local）のままです。

## 13. ver3.3 で Region を追加する方法

1. WORLD EDITOR v1.0 で新しい屋外 Map を作ります（`mapType: "outdoor"`）。
   - 例: `east_road_a`。Map の座標は自由です（0 始まりでもよい）。
2. Authoring JSON を `Assets/Content/Maps/Authoring/` に置き、`MapAuthoringImporter.SyncAll` で `Assets/Content/Maps/east_road_a.asset` を作ります（従来の手順）。
3. `outdoor_main.hwworld.json` の `regions` に追加します。
   - 例: `{ "mapId": "east_road_a", "offset": { "x": 11, "y": -6 } }`
   - first_field の東端は x=10 なので、x=11 から始めると隣り合います。
4. シーンを作り直します（`WorldVer32Checks.PrepareBatch` または `PocSceneBuilder.Build`）。
   - registry の検証が、重なり・範囲・屋内 Map の混入を止めます。
5. つなぎ目を確認します:
   - 道の端（`road_east` などの marker）の続き: ver3.2 では、Region の外への道の延長（飾り）は、他の Region の中では止めています（`CollectWorldRoad`）。2つの Region の道は、セルが隣り合うように作ってください。
   - first_field の外周は、今は外側 8 セルの飾り（通れない）です。Region が隣に来ると、その部分はその Region の地面になります。境界のセルを通れるかどうかは、両方の Map の通行判定のままです。
6. 確認:
   - `WorldVer32Checks` を実行します。新しい Region でも、全セルが Map の規則どおりになることを確かめる検査（128 World と同じ方法）を、その Region に足してください。
   - カラスを新しい Region へ行かせる場合は、`PocCrow.PerchCandidates` の Region 制限を外します（今はカラスの Map の中だけ）。
   - AUTO の範囲は、描かれたチャンクの中なので、そのままで足ります。
7. セーブは Schema 1 のままで足ります（11.1）。

## 14. テスト用 World（Fixture）

- 生成は `PocTestWorldGenerator`（`Runtime/World`）です。
  - 32/64 は、Region 1つ（Offset 0）。128/256 は、Region 4つです。北東は Offset 0、他の3つは Map セルが 0 始まりで Offset あり。
  - 中心を (0,0) にするので、半分は負の座標です。
  - 道は 24 セルごと。木 約4%・石 約2%・花 約3%。家（境界 x=0 または x=16 をまたぐ）、ベンチ（x=15|16 をまたぐ）、井戸、Player Start、カラス。
- テスト用の Map は **メモリの中だけ** です（`HideFlags.HideAndDontSave`。asset を作りません）。
  - 材料の定義の参照は `Assets/POC2D3D/Fixtures/ChunkTest/PocTestWorldFixture.asset`（テスト専用）にあります。
  - これを参照するのは、チャンク確認用シーン `HARUKA_WORLD_ver3.2_CHUNKTEST.unity`（`PocChunkTestBoot`）だけです。公開用シーンには入りません（検査済み）。
- チャンク確認用のビルドは、セーブをメモリだけに書きます（`HarukaMemoryStorage`）。ブラウザのセーブは読みも書きもしません。

## 15. 開発用の表示と計測

- `?debug=1` のときだけ、既存の開発パネルに、次の行と切替が出ます:
  - World ID / チャンク / チャンク内セル
  - チャンク数・待ち・Object 数・読み込み／解放の ms
  - 「Chunk borders」切替（チャンク境界の色付きの線）
- `PocDevStats`（`?debug=1` か `?devstats=1` のときだけ動く）は、計測値を 0.5 秒ごとにページの `window.__halkaDev` へ渡します。
  - 渡す値: World 位置・チャンク・フレーム時間（平均 / P95 / P99 / 最大）・チャンク統計・GameObject 数・三角形・Draw Call・GC・Mono heap。
  - WebAssembly 全体の heap（`HEAPU8.buffer.byteLength`）は、jslib が足します。JS heap とは別の値です。
- スクリプト定義 `HALKA_STRIP_DEV_TOOLS` を付けてビルドすると、パネルと `PocDevStats` は動作しなくなります。
- 公式のゲーム UI（メニュー・右上の切替・D-pad・A）には、何も足していません。

## 16. ファイル一覧（ver3.2 で増えた・変えたもの）

| ファイル | 役割 |
|---|---|
| `Runtime/World/WorldGrid.cs` | `ChunkCoord`・`CellRect`・Floor Division・範囲 |
| `Runtime/World/OutdoorWorldDefinition.cs` | World 定義・Region・検証・registry の形 |
| `Runtime/World/OutdoorWorldIndex.cs` | Region のセルフラグ・Object・チャンク表・問い合わせ |
| `Runtime/World/ChunkStreamer.cs` | Lifecycle・先読み・Hysteresis・予算・失敗処理 |
| `Runtime/World/PocTestWorldGenerator.cs`・`PocTestWorldFixture.cs` | テスト用 World（テスト専用） |
| `Runtime/PocWorldPresenter.Chunks.cs` | チャンク単位の表示・解放・止まり木の計算 |
| `Runtime/Dev/PocDevStats.cs`・`Plugins/WebGL/HarukaDev.jslib` | 開発用の計測 |
| `Runtime/Dev/PocChunkTestBoot.cs` | チャンク確認用シーンの起動（テスト専用） |
| `Editor/OutdoorWorldRegistryJson.cs` | registry JSON の読み込み・検証 |
| `Editor/WorldVer32Checks.cs` | ver3.2 の検査 |
| `Content/Maps/World/outdoor_main.hwworld.json` | 屋外 World の registry |
| 変更: `SharedWorldModel`・`SharedWorldSession`・`HarukaSaveSystem`・`PocWorldPresenter`・`PocEnvironmentBuilder`・`PocPixelView2D`・`PocCrow`・`PocGameplay`・`AutoModeController`・`PocDebugPanel`・`PocSceneBuilder`・`PocBuilder`・`Phase6SaveChecks`（harness の設定口） | 上の各章 |
