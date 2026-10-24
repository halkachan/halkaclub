# はるかわーるど ver3.2「オープンワールド基盤アップデート」実装レポート

作成日: 2026-10-24
対象: ゲーム本編（Unity 6000.3.10f1 / WebGL）。WORLD EDITOR v1.0 は変更していません。
状態: **ローカル実装と検証まで完了・未公開**。main への Merge・Push、GitHub Pages、halkaclub.com、公開中の ver3.1 Build には一切触っていません。公開はユーザーの確認と許可を待ちます。
設計資料: `HARUKA_WORLD_VER32_ARCHITECTURE.md`（実際のコードと同じ内容。この報告と一緒に読んでください）

## まとめ（最初に読む所）

- 屋外に共通の World 座標（World ID `outdoor_main`）を入れました。`first_field` は Offset (0,0) で置いたので、World の座標は first_field の座標と同じです。家・木・道・カラスの位置は1つも変わっていません。
  - 確認: ver3.1 と ver3.2 の屋外 2D 画面は、カラスの立ち位置以外は同じです（画素差 0.16%）。
  - 確認: 地面の草タイル 1,073・房 200・三角形 3,806 も一致します。
- 屋外を 16×16 セルのチャンクに分けました。
  - プレイヤーの周り（2D の画面と 3D の見通しを合わせた範囲）だけを描きます。
  - 歩く向きに先読みし、遠くなったチャンクは GameObject と Mesh を本当に破棄します。
  - 境界の往復では、作り直しを起こしません（Hysteresis）。
- 通れる／通れない・草・Object は World 全体で持つ「論理」で、表示とは別です。チャンクが描けなくても、壁抜けや閉じ込めは起きません（失敗時は平らな地面で代わりに描き、Step Gate がその場で読み込みます）。
- セーブは Schema 1 のままです（first_field と Map セル）。ver3.0 / ver3.1 のセーブ・Export ファイル・壊れた Snapshot からの復旧は、そのまま読めます。チャンクの状態はセーブに書きません。
- 検証用に、32 / 64 / 128 / 256 の広いテスト World（負の座標・4 Region・Offset あり・木 4%・石 2%・花 3%・道・家・ベンチ）を、メモリの中だけで作りました。公式の first_field・家・公開 Build には入れていません。
- 検査:
  - ver3.2 専用 `WorldVer32Checks` **219 件合格・0 件失敗**
  - ver3.1 専用 191 件合格
  - ローカル WebGL（ver3.2 本体）19 件合格
  - チャンク確認版 WebGL 28 件合格・1 件失敗（測り方の誤り。再確認で合格。38章）
  - 既存の検査の分類は 43章
- 性能（PC の headless Chrome、ローカル WebGL）:
  - ver3.2 本体: 屋外・家・防音室の 2D/3D すべて 60fps（P99 18ms 前後）。ver3.1 と差がありません。
  - テスト World（32〜256）: 静止・長押し・境界の往復・素早い方向転換・AUTO・2D/3D 切替のすべてで、平均 59.7〜60fps、最大フレーム 19.3ms。2D/3D 切替の瞬間だけ最大 33ms（チャンクの作り直しは 0）。
  - チャンク 1 つの生成は WebGL で平均 4.5ms・最大 15ms、解放は平均 0.1ms。Step Gate の同期読み込みは全場面で 0 回でした。
- 実機のスマートフォンでは確認していません（PC Chrome のエミュレーションで画面の形だけ確認）。Standalone の計測もしていません（NOT RUN、理由は 39章）。

## 1. 開始時の Git 状態

`HARUKA_WORLD_V32_BACKUP_20261024/git_state.txt`（2026-10-24 18:25）に保存しました。

| 項目 | 値 |
|---|---|
| origin/main | `144a534`（依頼ページの最終更新日。ver3.1 公開 `71ec01b` の後に 2 Commit） |
| ver3.1 公開 Commit | `71ec01b`（origin/main に含まれる） |
| 71ec01b 以降のゲーム本体（unity-project・blender・halkaworld）の変更 | なし（`tools/HalkaSiteEditor` だけ） |
| 未コミット変更・未追跡ファイル（作業前） | なし（ver3.2 Worktree は新規） |
| Worktree | 23 個（一覧は git_state.txt）。`HALKA HP` のユーザーの編集（`GrassDecoration.prefab`、`backup-v05-*`）には触れていません |

## 2. ver3.1 公開版の基準 Commit

- 基準: `71ec01b`（ver3.1 公開。`release/ver3.1`）を含む origin/main `144a534`。
- 古い開発ブランチ `claude/harukaworld-v31`（`826e9c4`）は使っていません。公開物と origin/main を正としました。
- 公開中の ver3.1 Build（`halkaworld/` のページとゲーム 14 ファイル）は、作業後もバックアップと SHA-256 が一致しています（44章）。

## 3. 作業ブランチ

- `claude/harukaworld-v32`（Worktree `C:\Users\owner\Documents\ChatGPT\HALKA_HP_V32`）。origin/main `144a534` から分岐。
- Push していません。main へ Merge していません。

## 4. バックアップ

`C:\Users\owner\Documents\ChatGPT\HARUKA_WORLD_V32_BACKUP_20261024\`（リポジトリの外）

| 内容 | 中身 |
|---|---|
| `halkaclub_all_refs.bundle` | 全ブランチ・タグの Git bundle（352 MB） |
| `unity-project/` | Assets・ProjectSettings・Packages（Prefab / Scene / .meta / Save のコード / 既存の検査を含む） |
| `blender/` | Blender Source |
| `authoring_maps_and_catalogs/` | 正式 Map JSON・Catalog（json + meta） |
| `halkaworld_published_ver31_webgl/` | 公開中の ver3.1 のページとゲーム |
| `ver31_local_build/` | ver3.1 のローカル確認版 |
| `git_state.txt` | 開始時の Git 状態 |
| `sha256_before.txt` | 1,096 ファイルの SHA-256（作業前） |

2D Sprite・3D FBX は `unity-project/Assets` と `blender/` の中に入っています。作業後の SHA-256 は `logs/sha256_after.txt`、比較結果は `logs/sha256_compare.txt` です。

## 5. 変更ファイル一覧

`git diff --stat 144a534..HEAD`（61 ファイル）。主なものは次のとおりです（全一覧は 47章の Commit）。

| 区分 | ファイル |
|---|---|
| 新規: World 層 | `Runtime/World/WorldGrid.cs`、`OutdoorWorldDefinition.cs`、`OutdoorWorldIndex.cs`、`ChunkStreamer.cs` |
| 新規: 表示 | `Runtime/PocWorldPresenter.Chunks.cs` |
| 新規: テスト用 | `Runtime/World/PocTestWorldGenerator.cs`、`PocTestWorldFixture.cs`、`Runtime/Dev/PocChunkTestBoot.cs`、`Fixtures/ChunkTest/PocTestWorldFixture.asset`、`Scenes/HARUKA_WORLD_ver3.2_CHUNKTEST.unity` |
| 新規: 開発用計測 | `Runtime/Dev/PocDevStats.cs`、`Plugins/WebGL/HarukaDev.jslib` |
| 新規: registry | `Assets/Content/Maps/World/outdoor_main.hwworld.json`、`Editor/OutdoorWorldRegistryJson.cs` |
| 新規: 検査・計測 | `Editor/WorldVer32Checks.cs`、`Editor/WorldVer32Bench.cs`、`tools/poc2d3d/v32_run_regression.sh` |
| 新規: ローカル確認版の付属 | `tools/poc2d3d/v32/`（`serve_v32.py`・`start_v32.bat`・README・staging ページ） |
| 変更 | `SharedWorldModel.cs`、`SharedWorldSession.cs`、`Save/HarukaSaveSystem.cs`、`PocWorldPresenter.cs`、`PocEnvironmentBuilder.cs`、`PocPixelView2D.cs`、`PocCrow.cs`、`PocGameplay.cs`、`Scripts/Player/AutoModeController.cs`、`PocDebugPanel.cs`（版名 ver3.2・開発パネルの行）、`Editor/PocSceneBuilder.cs`、`Editor/PocBuilder.cs`、`Editor/Phase6SaveChecks.cs`（harness に設定口を1つ追加） |
| 名前の変更 | `Scenes/HARUKA_WORLD_ver3.1.unity` → `HARUKA_WORLD_ver3.2.unity`（GUID は同じ） |
| 変更なし | Map（first_field・halka_house・halka_soundbooth）とその Authoring JSON、Catalog、Blender Source、FBX、Sprite、WORLD EDITOR v1.0、公開ページ |

## 6. 新しい World 構造

```
Authoring Map JSON → MapDefinition（変更なし）
   → outdoor_main.hwworld.json（Region = MapID + Offset。ゲーム本編専用・版管理）
   → OutdoorWorldDefinition → OutdoorWorldIndex（World 全体の論理：通行・草・Surface・Object・チャンク表）
   → SharedWorldSession（2D/3D 共通の唯一の状態）
   → PocWorldPresenter + ChunkStreamer（表示だけをチャンク単位で出し入れ）→ 2D View / 3D View
```

- 既存の SharedWorld の形（Session が1つ、2D と 3D は同じ表示を別の Camera で撮る）は、そのままです。その上に、屋外の World 層とチャンク層を足しました。
- 屋内（家・防音室）は、ver3.1 と同じ仕組みのままです。

## 7. World ID

- `outdoor_main`（指示書の候補）。既存の ID（Map ID・セーブのキー・PlayerPrefs）と重ならないため、そのまま採用しました。
- セーブには書きません（31章）。開発パネルと `window.__halkaDev` に表示します。

## 8. World 座標

- 整数セル (x, y)。x = 東、y = 北。1セル = 0.5 unit。4方向・1 Step 単位・斜めなし（変更なし）。
- 屋外のセッション位置は World Cell です。屋内は、従来どおり その Map のセルです。
- 範囲の調査（設計資料 1.1）:
  - 論理の上限は ±2^24 セル。和を取ってもあふれず、あふれる場合は例外にします。
  - 表示の安全範囲は ±32,768 セル。ここまでは 1/4 セルの歩行補間が float で正確です（検査で確認）。それを超える Region は WARNING にします。
- 全 `Vector2Int` の置き換えは不要と判断しました（調査のみ）。

## 9. Map-local 座標との変換

- World = Map-local + Offset、Map-local = World − Offset（`OutdoorRegion.ToWorld / ToLocal`）。
- 共通の口:
  - `SharedWorldSession.TryToSessionCell(mapId, mapCell)` … セーブ・Import → 位置
  - `TryGetSavePlace()` … 位置 → セーブ
- 検査:
  - first_field の全 273 セルで World = Map-local。
  - 128 World の Offset (-64,0) の Region の全 4,096 セルで、往復の変換が一致。
  - 家・防音室は Map-local のまま（屋外 World に入らない）。

## 10. Chunk 座標

- 16×16 セル。`ChunkCoord`（X, Y）は World Cell から一意に決まります（`WorldGrid.ChunkOf`）。
- Chunk-local は常に 0〜15（`LocalInChunk`）。Chunk + local → World は元のセルに戻ります（-300〜300 を 8・16・32 で全数確認）。

## 11. 負数座標の処理

- 数学の Floor Division（`WorldGrid.FloorDiv`）。0 方向への切り捨ては使っていません。
- 指示書の表（-1→-1、-16→-1、-17→-2、0→0、15→0、16→1）に加え、-32→-2、-33→-3、31→1、32→2 も検査で確認しました。
- first_field の角: (-10,-6) と (-1,-1) はチャンク (-1,-1)、(0,0) と (10,6) はチャンク (0,0)。
- テスト World は中心を (0,0) にしてあり、半分が負の座標です。WebGL でも (-20,-20)（チャンク (-2,-2)）を歩きました（図8）。

## 12. Chunk サイズの比較

Unity Editor（batch、ウィンドウなし）で、同じ道のり（静止・長押し・素早い方向転換・境界の往復・遠くまで歩いて戻る・2D/3D 切替）を、チャンクサイズ 8 / 16 / 32 で比べました（`logs/chunk_size_bench_editor.csv`、`WorldVer32Bench`）。**Editor の値です**（WebGL は 38〜42章）。

| World | サイズ | 1チャンクの生成 平均 / 最大 | 解放 平均 | 描画中のチャンク 平均 / 最大 | GameObject 最大 | 三角形 最大 | 1フレームの最大（ms） | Index 作成 |
|---|---|---|---|---|---|---|---|---|
| first_field | 8 | 1.1 / 14.3 ms | — | 24 / 24 | 216 | 19,532 | 6.0 | 0.09 ms |
| first_field | 16 | 1.1 / 2.9 ms | — | 8 / 8 | 136 | 19,532 | 2.7 | 0.11 ms |
| first_field | 32 | 2.1 / 3.1 ms | — | 4 / 4 | 116 | 19,532 | 3.0 | 0.06 ms |
| 128×128 | 8 | 1.7 / 10.1 ms | 0.07 ms | 157 / 166 | 2,805 | 687,384 | 72.0 | 3.4 ms |
| 128×128 | 16 | 5.5 / 15.0 ms | 0.23 ms | 55 / 61 | 2,697 | 797,500 | 81.1 | 2.0 ms |
| 128×128 | 32 | 18.0 / 30.6 ms | — | 22 / 24 | 3,329 | 1,031,539 | 116.5 | 2.0 ms |
| 256×256 | 8 | 1.9 / 9.1 ms | 0.08 ms | 158 / 166 | 2,935 | 761,405 | 73.4 | 10.0 ms |
| 256×256 | 16 | 7.1 / 16.4 ms | 0.27 ms | 54 / 61 | 3,388 | 997,472 | 104.0 | 10.9 ms |
| 256×256 | 32 | 26.9 / 35.7 ms | — | 22 / 24 | 4,983 | 1,523,280 | 161.0 | 8.6 ms |

- 衝突判定の参照は、どのサイズでも 1回 45〜90 ns（20 万回の平均）です。World 全体の配列を1回見るだけなので、チャンクサイズに左右されません。
- 「1フレームの最大」は、Editor のフレーム（チャンク生成以外の処理も含む）の最大です。
- 比較した項目（指示書 6.2）:

| 項目 | 8 | 16 | 32 |
|---|---|---|---|
| 生成時間 | 短い（〜2ms）が、数が多い | 中（平均 5〜7ms、最大 16ms ＝ 1フレームに収まる） | 長い（平均 18〜27ms、最大 36ms。読み込むたびに 2フレーム落ちる） |
| 破棄時間 | 0.1ms | 0.2〜0.3ms | （範囲が広く、この道のりでは破棄なし） |
| 表示 GameObject 数 | 約 2,900 | 約 2,700〜3,400 | 約 3,300〜5,000（遠くまで描く） |
| Draw Call | チャンクごとに地面・草の房・結合 Mesh があるので、チャンク数に比例して増える（16 の約 3 倍） | WebGL 実測 2D 172〜186・3D 111〜256（release）、development Build 2D 149・3D 268〜271 | 少ない（16 の約半分）が、三角形が多い |
| CPU（予算 4ms の中の処理） | 1フレームで複数作れる | 1フレームに 1つ | 予算を毎回大きく超える |
| GC Alloc（Mono の増加） | 2.4〜3.3 MB | 3.0〜5.7 MB | 0.2〜6.9 MB |
| Collision 参照 | 同じ（World 配列） | 同じ | 同じ |
| 2D/3D の描画 | 境界の線が多いだけで見た目は同じ | 同じ | 同じ |
| カメラが見通す範囲 | 3D の見通し 40 セルに対して、余り（読み込みすぎ）が少ない | 最大 16 セル余分 | 最大 32 セル余分（遠くを描きすぎ） |
| スマホ WebGL の制約 | 1フレームの負担は小さいが、Draw Call が多い | 生成 1回が 16ms 以内 | 生成 1回が 1フレームを超える |

## 13. 採用した Chunk サイズと理由

**16×16 セル**（registry の `chunkSize`。いつでも変えられます）。

1. 1チャンクの生成が、最大でも 1フレーム（16.7ms）に収まります（Editor 最大 16.4ms、WebGL 最大 15ms・平均 4.5ms）。32 では毎回 2フレーム以上かかり、歩くたびに引っかかりが出ます。
2. 描くチャンクの数（約 55）と Draw Call が、8 の約 3 分の 1 です（WebGL 実測の Draw Call は 16 で 2D 約 180・3D 約 250）。スマホの WebGL では、Draw Call の数が負担の大部分です。
3. first_field（21×13）は 8 チャンク（飾りの外周を含む）に分かれます。境界をまたぐ確認を、正式なフィールドでも行えます。
4. 3D の見通し（40 セル）と 2D の画面（約 41×23 セル）に対して、読み込みすぎが小さく済みます。

## 14. Chunk Registry

- 論理の表: `OutdoorWorldIndex.Chunks`（`ChunkStatic`）。
  - 内容: 座標、World での範囲、持ち主の Object、他チャンクから絵が届く Object（持ち主チャンク + 範囲）、Region のセル数、飾りの有無。
  - Index を作る時に一度だけ作ります（256×256 で 9〜11ms）。
- 状態の表: `ChunkStreamer.Records`（`ChunkRecord`）。
  - 内容: 状態（Unloaded / Queued / Loading / Ready / Failed）、表示物、fallback かどうか、失敗回数、再試行の時刻、読み込み時間と回数、最後に必要とされた時。
- 管理する情報の一覧は、設計資料 6章の表にあります。指示書 6.4 の全項目を持っています。

## 15. 読み込み・先読み

- 範囲は、固定の 3×3 ではありません:
  - 2D の画面のセル数（`PocPixelView2D` が毎フレーム、低解像度 RT の大きさから計算。上下は高い木の分 +4 セル、左右 +2 セル）
  - 3D の見通し（40 セル）
  - 上の2つを合わせた矩形
  - 歩いている向きへ 8 セル（チャンクの半分）広げた範囲（先読み）
  - 大きな Object の絵がかかる持ち主チャンク
- 足りないチャンクは近い順に Queue へ入れ、1フレーム 4ms の予算で作ります（1フレームに最低1つ）。
- 先読みが間に合わない時は、Step Gate（一歩の前の `EnsureReady`）がその場で作ります。この回数は `SyncLoads` に数えています。
  - 検査（Editor）では、通常の歩行・長押しで 0 回でした。
  - WebGL でも、長押し・往復・方向転換・AUTO・切替のすべてで 0 回でした（38章）。
- 大型 Object: 家の絵が隣のチャンクにかかる場合、そのチャンクが見えれば、家の持ち主チャンクも読み込みます。家が半分だけ描かれることはありません。
  - 最初の実装では「チャンクの端の木」でも隣を呼んでいたため、無駄に 1 周多く読み込んでいました。検査で見つけ、「絵の範囲が表示矩形にかかる時だけ」に直しました。

## 16. 解放・再読み込み

- 解放: Keep（必要範囲 ±1 チャンク）の外へ出たチャンクを解放します。
  - そのチャンクの GameObject と、チャンクが作った Mesh（地面・房・debug 線・Static Batch の結合 Mesh）を Destroy します。
  - `SetActive(false)` は使っていません。
  - Catalog のモデル・Material・Map 定義は共有なので残します（指示書 7.4 の区別）。
- 再読み込み: 戻ってくると、同じ規則で作り直します。位置・Object・当たり判定は論理側にあるので、元と同じになります。
  - 検査: 家の位置が 1e-4 unit 以内で一致し、1つだけ描かれる。
  - WebGL: 解放されたチャンクが戻ると全部作り直される（38章）。
- 振動の防止: Keep の帯があるので、境界の往復では作り直しません。
  - Editor: 40 回往復して、作成・解放とも 0。
  - WebGL: 12 回往復して、0（38章）。
- キャンセル: 必要でなくなった Queue は、作る前に取り消します（検査済み）。

## 17. Chunk 境界の Collision

- 通行判定は World 座標の1つの経路です: `SharedWorldModel.CanEnter` → `OutdoorWorldIndex.IsBlocked`。チャンク内の座標は使いません。
- 境界のすぐ向こうの木は、正しく止めます（検査）。
- 128 World の全セル（4 Region、Offset あり）で、`MapPlacementRules` と同じ答えになります。
- 未定義の屋外（どの Region にも入らないセル）は通れず、草もありません。草原を自動で作りません。
- 一歩の途中でチャンクが変わっても、その一歩は最後まで進みます。15→16 の途中から、必ず 16 に着きます（元へ戻らない）。
- 2つの境界を越える 23 歩で、次のとおりでした:
  - 歩数・Life Log・StepStarted・StepCompleted: すべて 23
  - 足音: 23 回
  - 向き: 変わらない
  - 草の揺れ: 一歩につき 1 回

## 18. 大型 Object

- 区別している項目（設計資料 7章）: Root Cell（持ち主チャンク）、Visual Footprint（`PresentationExtent`）、Blocked Footprint（論理のフラグ）、Action Point、Presentation 範囲（`Reaching`）。
- テストの家（5×2）が、境界 x=0 と x=16 をまたいでいます（図9）。ベンチ（2×1）は x=15|16 をまたいでいます。
  - 1つだけ描かれ、両側の当たり判定が欠けません。
  - ベンチの Action Point は、境界の両側で正しく反応します。
  - 遠くへ行って戻ると、同じ位置にもう一度描かれます。
- 2D の前後関係: 2D は 3D の表示を撮っています。Object の前後は、ver3.1 と同じく、深さと Outline ID で決まります。Outline ID は ver3.1 と同じく Map の Object 順なので、first_field の見た目は変わりません（検査）。
- 3D の遮蔽: 木の透明化・家の部分遮蔽の設定は、Catalog のままです（検査: 描いた木はすべて catalog と同じ設定）。

## 19. Grass

- 草があるかどうかは、World 全体の論理のフラグ（`MapPlacementRules.HasGrass` と同じ規則を、Index を作る時に刻む）で決まります。
- 2D の草（2D ゲームの Sprite）は、描いているチャンクの草セルだけを、北の行から並べます。ver3.1 の並び順と同じなので、南の草が北の草に重なる関係も同じです。
- 3D の房は、チャンクごとの Mesh です。Hash は Region の MapID + Map セルで決まるので、first_field の房 200 本はすべて ver3.1 と同じ位置です。2D の時は、ver3.1 と同じく房を隠します。
- 草を減らしてはいません（first_field の草タイル 1,073 は ver3.1 と同じ）。

## 20. 2D 表示

- 16px/セル、Point Sampling、低解像度 RT の整数倍拡大、既存 Sprite、Grass Occlusion、深さ: 変更なし。
- チャンク境界でも、地面に隙間や線は出ません。first_field の地面は ver3.1 と同じ三角形数で、画面も画素差 0.16%（カラスの位置だけ）でした。
- カメラの端の制限（World の範囲）: ver3.1 と同じ規則で、World の Bounds を使います。

## 21. 3D 表示

- Blender のローポリモデル、Outline、影、Tree Fade、草、カラスの飛行: 変更なし。
- 見通しは 40 セル（20 unit）です。first_field（飾りを含めて 37×29 セル）は全部描かれるので、ver3.1 と同じ絵になります（画素差 0.68%：カラスと草の揺れ）。
- チャンクを解放しても、影は消えません。影は、描かれている Object がそのまま落とします。見通しの中は、すべて描いています。

## 22. Camera

- 2D・3D とも、Camera 自体は変更していません。どちらも World の位置を追います。
  - 検査: 境界を越えた後も、Camera の注視点はプレイヤーから 2.5 unit 以内。
- 家・防音室から出る時の Snap と、出口の構図は ver3.1 のままです（検査: 1フレーム目と 1.5 秒後の Camera 位置の差 < 0.06）。

## 23. Tree Fade

- 2D の透明化（`fadeInFront2D`）、3D の透明化（`fadeInFront3D`・40%・0.25 秒）は、チャンクで作った木にも同じ設定で付きます（検査）。
- 透明化の対象は、Presenter の View 一覧（描かれている Object）です。解放されたチャンクの木は、一覧から外れます。

## 24. カラス

- カラスは、どのチャンクの子でもありません。Session 側の1羽のままです（検査: 1羽。チャンクの読み込みで増えない）。
- 止まり木の候補は、論理の木 Object から作ります。描かれていない木でも、止まる点を計算できます。
  - 誤差 0.00000（40 本で確認）。
  - 飛行中に目的の木のチャンクが解放されても、目的地を失いません。
- 128 World で、遠くへ往復しながら 2 分見ました:
  - 地上歩行・待機・飛行・止まり木・離陸・着地をすべて行いました。
  - Spawn へ戻ることはありませんでした。
  - 占有セルは、地上にいる時だけでした。
- 行動範囲は ver3.1 と同じく、自分の Region の中です（設計資料 8章）。

## 25. AUTO

- 初期 ON、約 60 秒の無操作で開始、手動で解除、着席中は動かない、Map Transition を起こさない: 変更なし。
- 描かれていないチャンクには入りません（`CanEnter` に `IsCellReady` を追加）。そのため、経路探索も描かれた範囲の中だけです。
  - 256 World で 10 分間: 112 セルを歩き、描かれていないセルへは 0 回。最も遠くて開始から 13 セル。扉には乗りませんでした。
- 屋外かどうかの判定を、`"first_field"` という名前から「registry の屋外 Map」へ変えました（将来の Region でも AUTO が動くように）。

## 26. 屋外→家

- first_field: 扉 (-7,-4) へ歩いて入ると家 (0,-3) へ移ります（2D・3D・混合。Editor 検査・WebGL）。
- テスト World 64: 家の扉 (0,6) から、同じ家（halka_house）へ入って、出られました。屋外の扉は World 座標で判定し、屋内は Map セルのままです。
- 家の中では、屋外のチャンクを全部解放します（検査: 家の中のチャンクの GameObject は 0）。

## 27. 家→防音室

- 家の (4,-1) から防音室の扉 (4,0) へ歩くと、防音室 (0,-4) へ移ります（2D・3D・混合）。ver3.1 のままです。

## 28. 防音室→家

- 防音室の出口から、家の (4,-1)（防音室の前、下向き）へ戻ります。
- 家の出口から屋外へ出ると、扉の前 (-7,-5) に戻り、周りのチャンクを描きます（WebGL: チャンク 8 個）。

## 29. 朝夜

- 10 分周期のまま。チャンクの読み込み・解放で、World Time の基準は動きません（検査）。
- 新しく読み込んだチャンクも、同じ共有 Material と同じ太陽で描くので、時刻は他のチャンクと同じです（夜に読み込んだチャンクも同じ Material を使う、検査）。

## 30. Life Log

- 歩数は、一歩が終わった時だけ増えます。チャンクの切り替わりでは増えません。
  - 検査: 6 回の瞬間移動とチャンクの入れ替えで +0。境界をまたぐ 23 歩で +23。
- プレイ時間はそのまま進みます。

## 31. Save Schema

- **Schema 1 のまま**です。保存するもの（MapID、Cell X/Y、Facing、2D/3D、AUTO、累計歩数、累計プレイ時間、時間の基準）は、ver3.1 と同じです。
- first_field にいる時の保存値は ver3.1 と同じ（MapID `first_field` + そのセル）です。World ID・チャンクは書きません（検査: 保存した文に `chunk`・`outdoor_main` が含まれない。WebGL でも同じ）。
- 将来 Region が増えても、「その Region の MapID + Map セル」で保存できます（テスト World の Offset 付き Region で保存・再開できることを確認）。
- ver3.3 での拡張の方針（変更が必要になる条件、Schema 2 案、移行・Rollback・旧 Import）は、設計資料 11.1 にあります。今回は Schema を変えていません。

## 32. ver3.0 互換

- ver3.0 の屋外・屋内のセーブ（2D/3D、AUTO ON/OFF）は、同じ場所・表示・AUTO・歩数・時間で始まります（Editor 検査）。
- ver3.0 の Build で書き出した Export ファイル（家、3D、AUTO ON、20 歩）も、取り込めます。

## 33. ver3.1 互換

- ver3.1 の屋外・家・防音室のセーブ（2D/3D、AUTO ON/OFF）は、そのまま始まります。
- 扉の上・フィールドの外（未定義の屋外）を指すセーブは、近くの安全なセルから始まります。歩数は保たれます。
- 最新の Snapshot が壊れていても、1つ前の Snapshot から始まります（屋外 (9,-5)、100 歩）。

## 34. Export／Import

- Export → Import（屋外・家・防音室、テスト World の Offset 付き Region）: 受け付けられ、同じセルに戻ります（Editor 検査、ver3.1 の検査 191 件の save・old-saves を含む）。
- ver3.0 の Export ファイル: 受け付けられます（32章）。

## 35. WORLD EDITOR 互換

- WORLD EDITOR v1.0（`tools/HalkaWorldMapEditor`）のソース・EXE には手を加えていません（Git の差分なし）。
- Map JSON v4・Map Logic v1・Object Catalog v3・Entity Catalog v1・Presentation Catalog の形式も、変えていません。
- 屋外の Region の登録は、ゲーム本編専用の `outdoor_main.hwworld.json` です。
  - 版管理: `format`・`formatVersion`
  - 検証: 重なり・範囲・屋内 Map の混入・同じ Map を2回・未知の Map
  - Authoring Map を書き換えません。
  - 検査で、壊れた registry 9 種類が拒否されることを確認しました。
- ver3.3 での Region の追加手順は、設計資料 13章にあります。

## 36. テスト用 World

| サイズ | Region | 座標 | Object（木 / 石 / 花） | 道 |
|---|---|---|---|---|
| 32×32 | 1（Offset 0） | (-16,-16)〜(15,15) | 検査 `test-worlds` の値 | 24 セルごと |
| 64×64 | 1（Offset 0） | (-32,-32)〜(31,31) | 同上 | 同上 |
| 128×128 | 4（北東 Offset 0、他 3 つは Offset あり） | (-64,-64)〜(63,63) | 同上 | 同上 |
| 256×256 | 4 | (-128,-128)〜(127,127) | 同上 | 同上 |

- 密度は first_field と同じ程度です（木 約4%・石 約2%・花 約3%、草はほとんどのセル）。空っぽの World では測っていません。
- 家（境界をまたぐ）、ベンチ（境界をまたぐ）、井戸、Player Start、カラスを置いています。
- 同じ seed なら同じ World ができます（検査）。
- 正式コンテンツとの分離:
  - Map はメモリの中だけです（asset を作らない）。
  - 材料の定義の参照（`PocTestWorldFixture.asset`）は、チャンク確認用シーンだけが参照します。
  - 公開用シーンには入っていません（検査: シーンに chunktest も Fixture の GUID もない）。
  - 公式 first_field・家の位置は変わっていません。テスト用の NPC は作っていません。
- 指示書 10.2 の 13 項目は、Editor の検査（movement・large-objects・lifecycle・view・crow）と WebGL（38章）の両方で行いました。

## 37. Unity Test

`WorldVer32Checks`（`logs/ver32_checks.txt`）: **219 件合格・0 件失敗**

| 区分 | 件数 | 主な内容 |
|---|---|---|
| coordinates | 26 | 正数・負数・原点・境界の前後・Map-local↔World・World↔Chunk・大きな値・無効な値・往復・float の精度 |
| registry | 14 | 正式 registry、壊れた registry 9 種類、重なり、隣接は可、範囲の WARNING、Authoring 不変 |
| test-worlds | 23 | 4 サイズの妥当性・負の座標・密度・Offset・再現性・正式データに混ざらない |
| lifecycle | 21 | 未読み込み→待ち→生成中→使用可能、解放候補、解放、再読み込み、重複防止、キャンセル、失敗→再試行→fallback、往復、同時に多数 |
| official-field | 9 | first_field の地面・房・Object・当たり判定・草・Outline が ver3.1 と同じ |
| movement | 15 | 4方向、境界通過、歩数・足音・向き・草の揺れ、一歩の途中、境界の木、未定義の屋外、128 World 全セル、カラスの占有 |
| large-objects | 11 | 家・ベンチのチャンクまたぎ、二重生成なし、Action Point、解放と再描画 |
| crow | 9 | 1羽、止まり木の計算、チャンクの入れ替えの間の行動、Region の中、占有 |
| auto | 6 | 256 World で 10 分、描かれた範囲だけ、扉に乗らない |
| view-2d-3d | 17 | 2D・3D でチャンク移動、境界で切替（12 回で何も変わらない・作り直しなし）、読み込み中の切替、Camera、草、木の透明化、Outline、影、デバッグ表示 |
| failures | 7 | 生成の失敗、fallback、原因が消えた後の作り直し、先読みが遅れた時、読み込み中に家へ入る |
| interiors | 32 | first_field とテスト World で、屋外↔家↔防音室（2D・3D・混合）、Camera の Snap、AUTO が扉に入らない |
| save | 21 | Schema 1、ver3.0/3.1 のセーブ、扉・範囲外からの安全な再開、境界での保存、Export/Import、Snapshot の復旧 |
| time-lifelog | 4 | 時間の基準・歩数・プレイ時間・夜に読み込んだチャンク |
| protection | 4 | 正式 Map・WORLD EDITOR 不変、公開用シーンにテスト World なし |

- `WorldVer31Checks`（ver3.1 の 191 件）: **191 件合格・0 件失敗**（ver3.2 のコードで再実行）。

## 38. WebGL Test

ローカル WebGL。headless Chrome（自動操作）、専用の一時プロファイル（`chrome_v32_profile`）、ポート 9344。普段のブラウザ（9334）とユーザーのセーブには触れていません。サーバー:

| 用途 | フォルダ | ポート |
|---|---|---|
| ver3.2 本体 | `HARUKA_WORLD_ver3.2_LOCAL` | 8932 |
| ver3.1（比較用） | `HARUKA_WORLD_ver3.1_LOCAL` | 8931 |
| チャンク確認版（セーブはメモリだけ） | `HARUKA_WORLD_ver3.2_CHUNKTEST` | 8933 |

公開中の halkaclub.com と ver3.1 の Build は使っていません。

**ver3.2 本体（19 件合格・0 件失敗、`logs/webgl/WEB_RESULTS.txt`）**
- 起動: outdoor_main・first_field・チャンク 16・8 チャンク
- 右上のボタン: 2D⇔3D、同じセル、作り直しなし
- first_field のチャンク境界（x -1|0、y -1|0）を歩く: 3 チャンクを通過、Step Gate の読み込み 0
- 屋外→家→防音室→家（3D）→屋外: 扉の前に戻り、チャンクを再描画
- セーブ: first_field・Schema 1・チャンクの情報なし。再読み込みで同じセル
- スマホ縦 390×844・横 844×390（エミュレーション）
- ウィンドウ 1024×640 / 1600×900 / 1280×720: 右上のボタンが効く。ゲームの大きさは ver3.1 と同じ（921×576 / 1024×576 / 1024×576）

**チャンク確認版（`logs/webgl_chunks/WEB_RESULTS.txt`）: 28 件合格・1 件失敗（再確認で合格）**

| 指示書 10.2 / 21.3 の項目 | 32 | 64 | 128 | 256 |
|---|---|---|---|---|
| World へ入る（チャンク 16、Player Start (3,2)） | 合格（16 チャンク） | 合格（36） | 合格（36） | 合格（36） |
| 長押しで複数の境界を通過 | 27 セル・2 チャンク | 59 セル・4 チャンク | 91 セル・6 チャンク | 142 セル・10 チャンク |
| 歩数が 1 セル 1 歩 | 合格 | 合格 | 合格 | 合格 |
| 歩いた後ろのチャンクを解放 | — | （小さく、全体が範囲内） | 35 解放 | 55 解放 |
| 境界の往復 12 回で作り直しなし | 合格 | 合格 | **失敗→再確認で合格**（下の注） | 合格 |
| 素早い方向転換 40 回 | 最大 18.3ms | 18.7ms | 18.3ms | 18.5ms |
| 2D/3D 切替 6 回: 同じセル・作り直しなし | 合格 | 合格 | 合格 | 合格 |
| 遠くで解放された開始付近のチャンクが、戻ると全部作り直される | — | — | 19 個すべて | 36 個すべて |
| AUTO が描かれた範囲だけ歩く（Step Gate 0） | — | 86 歩 | — | 76 歩 |

- 注（128 の往復）: 最初の実行は「読み込み +4・解放 0」で失敗にしました。
  - 原因: 測り始めた時、境界まで歩いてくる間に Queue に入ったチャンクが、まだ作られている最中でした（作り直しではありません）。
  - Queue が空になってから測り直しました（`logs/webgl_chunks/OSC_RESULTS.txt`、`OSC_RESULTS_2.txt`）。64 の境界 2 か所と、128・256 の境界 x=16・-16・48 の 6 か所、計 8 か所で、**解放 0・作り直し 0** でした（64 は `OSC_RESULTS.txt`。同じファイルの 128・256 の行は、自動操作が家に入ってしまった無効な測定です）。
  - そのうち 3 か所では、最初の往復で、歩く向きの先読みが 1 列（+1〜+5 チャンク）を新しく読み込みました。これは 1 回だけで、その後は増えません。一度描いたチャンクを、もう一度作ることはありませんでした。
- 解放→再読み込み後の当たり判定（`logs/webgl_chunks/COLLISION_RELOAD.txt`）:
  - 道 y=-12 の x=-10〜10 で北を向いて進もうとし、止められたセルを記録しました。
  - そのあと東の端まで歩きました（256 では開始付近のチャンク (-1,-1)・(0,-1) が解放）。戻って、同じことをしました。
  - 止められたセルは、前後で同じでした（128: x=-8,-3、256: x=-8,-3,2）。
  - 通れたセルの数（15 → 17）が違うのは、1回目に自動操作が 2 か所へ行けなかったためです。
  - 最初の自動検査（4方向に押すだけ）は、隣に障害物がなく 0/0 で、意味のない合格でした。このため上の方法でやり直しました。
- スクリーンショット（図5〜11）:
  - チャンク境界の表示（2D・3D）
  - 家のまたぎ
  - 読み込み状態（48 チャンク）
  - 負の座標 (-20,-20)
  - 南西へ遠く歩いた後の解放状態（開始付近の (1,0) などが解放済み）
  - 128・256 の World

## 39. PC Performance

**ver3.2 本体と ver3.1（同じ PC・同じ headless Chrome・1280×720、rAF で 5 秒）**

| 場所 | 版 | 2D fps / P99 / 最大 | 3D fps / P99 / 最大 | Draw Call 2D / 3D |
|---|---|---|---|---|
| 屋外 | ver3.1 | 60 / 18.2 / 18.4 ms | 60 / 18.2 / 18.5 ms | （ver3.1 は計測なし） |
| 屋外 | ver3.2 | 60 / 17.9 / 18.4 ms | 60 / 18.1 / 18.1 ms | 49 / 76 |
| 家 | ver3.2 | 60 / 18.3 / 18.4 ms | 60 / 18.3 / 19.3 ms | 24 / 34 |
| 防音室 | ver3.2 | 60 / 18.3 / 18.5 ms | 60 / 18.0 / 18.7 ms | 24 / 37 |

- 1% Low（P99 の逆数）は、どこも 54〜56fps です。重大な低下はありません。
- 初回ロード（読み込み画面が消えるまで、各 3 回、毎回ストレージを消してから。`logs/webgl/FIRST_LOAD.txt`）:
  - ver3.1: 630 / 555 / 569 ms
  - ver3.2: 610 / 569 / 571 ms
  - 差はありません。
  - 表の「first load 1,650 ms」は、開発用の数値が最初に届くまで（30 フレーム後）を測った別の値です。

**テスト World（チャンク確認版、release 設定、`logs/webgl_chunks/WEB_PERF.json`）**

| World | 場面 | fps | P95 / P99 / 最大（ms） | 50ms 超 | 読み込み / 解放 / 同期 | 描画中チャンク | GameObject | Draw Call |
|---|---|---|---|---|---|---|---|---|
| 32×32 | 静止 2D / 3D | 60 / 59.8 | 16.9 / 18.0 / 18.4 | 0 | — | 16 | 325 | 172 / 111 |
| 32×32 | 長押し（27 セル、2 チャンク） | 60 | 17.1 / 18.0 / 18.7 | 0 | 0 / 0 / 0 | 16 | 325 | — |
| 64×64 | 静止 2D / 3D | 60 / 60 | 17.1 / 18.0 / 18.4 | 0 | — | 36 / 30 | 1,080 | 186 / 213 |
| 64×64 | 長押し（59 セル、4 チャンク） | 59.9 | 16.9 / 17.8 / 18.2 | 0 | 5 / 6 / 0 | 最大 35 | 最大 1,075 | — |
| 64×64 | AUTO 25 秒（86 歩） | 59.9 | 17.1 / 18.4 / 19.2 | 0 | 6 / — / 0 | — | — | — |
| 128×128 | 静止 2D / 3D | 60 / 60 | 17.1 / 18.2 / 18.9 | 0 | — | 36 / 39 | 1,999 | 183 / 242 |
| 128×128 | 長押し（91 セル、6 チャンク） | 59.9 | 17.1 / 18.2 / 19.2 | 0 | 18 / 35 / 0 | 最大 55 | 最大 2,939 | — |
| 256×256 | 静止 2D / 3D | 60 / 60 | 17.3 / 18.0 / 18.5 | 0 | — | 36 / 40 | 1,999 | 183 / 256 |
| 256×256 | 長押し（142 セル、10 チャンク） | 59.9 | 16.9 / 17.7 / 19.3 | 0 | 42 / 55 / 0 | 最大 55 | 最大 3,080 | — |
| 256×256 | AUTO 25 秒（76 歩） | 59.9 | 17.5 / 18.8 / 19.3 | 0 | 15 / — / 0 | — | — | — |
| 各サイズ | 境界の往復 12 回 | 59.9 | 〜17.2 / 18.3 / 18.6 | 0 | 0 / 0 / 0（128 は 38章の注） | — | — | — |
| 各サイズ | 素早い方向転換 40 回 | 60 | 17.0 / 18.0 / 18.7 | 0 | 0〜5 / 0 / 0 | — | — | — |
| 各サイズ | 2D/3D 切替 6 回 | 59.5〜59.9 | 16.9 / 18.5 / 33.5 | 0 | 0 / 0 / 0 | — | — | — |

- チャンクの生成（WebGL、長押しの間）: 平均 2.1〜4.5ms、最大 9〜15ms。解放: 平均 0.08〜0.13ms、最大 2ms。1フレームの streamer の処理: 最大 11〜12ms（予算 4ms ＋ 1チャンク）。
- 2D/3D 切替の最大 33ms は、表示の切替そのもの（Camera・RT の準備）です。チャンクの作り直しは 0 です。

- Unity Editor の値は 12章です。
- **Standalone: NOT RUN**。Windows 版を動かすと、ユーザーのデスクトップに窓が開きます（隠して実行する方針に反します）。batch mode では描画しないため、比べられる値になりません。
- Draw Call: ProfilerRecorder の値です。release 設定の WebGL でも値が出たので、そのまま載せました。development Build の値は 41章です。

## 40. Mobile Performance

- **NOT RUN（実機）**。実機のスマートフォンは使っていません。
- PC Chrome のエミュレーション（390×844・844×390、DPR 3）では、画面の形と操作だけ確認しました（38章）。これは実機の性能ではありません。
- 実機での確認は、ユーザーにお願いする項目です（49章）。

## 41. GC Alloc

- Editor（12章）: 同じ道のりの間の Mono heap の増加は、16 で 0.1〜5.7 MB、GC は 3〜24 回（256 World で 197 歩）。
  - 主な原因: チャンク生成の Mesh 作成（`PocMeshBuilder` の List）
- WebGL（release）の Mono heap: first_field 0.8〜1.0 MB、テスト World 2.3〜11.7 MB（World の論理と生成した Map を含む）。
- 1フレームの GC Alloc（ProfilerRecorder "GC Allocated In Frame"、development Build、静止時）: 約 1.8〜1.9 KB/フレーム（2D/3D、128・256 とも同じ）。
  - これはチャンクの読み込みとは関係のない、毎フレームの既存処理の分です。
  - release Build では、この値は取れないため NOT RUN です。

## 42. Memory

- WebGL の Memory は、**WebAssembly の heap（`HEAPU8.buffer.byteLength`）と JS heap を分けて** 載せています。JS heap だけを Unity の Memory として扱ってはいません。
- ver3.2 本体: WASM heap 46.1 MB（屋外・家・防音室とも同じ）、JS heap 6.3〜8.1 MB、Mono heap 0.8〜1.0 MB。ver3.1 の JS heap は 6.8〜8.6 MB でした。ver3.1 の WASM heap は、計測の仕組みがないので NOT RUN です。
- テスト World（チャンク確認版）の WASM heap: 32×32 55.4 MB、64×64 115 MB、128×128 198.8 MB、256×256 198.8〜238.5 MB（長押しで 256 の端まで歩いた後）。
  - WASM heap は、一度大きくなると小さくなりません（Emscripten のメモリの仕組み）。そのため、この値は「その時までの最大」です。
  - 大部分は、テスト World の論理（256×256 で 4 Region・約 6,000 Object）と生成した Map の分です。チャンクを解放すると、GameObject は 3,080 → 1,999 に戻ります。
  - JS heap は 5.6〜8.4 MB です。
- development Build の "Total Used Memory": 123〜143 MB（128・256）。
- Build の大きさ: ver3.2 本体 30,470,744 bytes（最終 Build。ver3.1 は 30,106,943 bytes。+363,801 bytes、+1.2%。World 層・チャンク・開発用計測のコード）。

## 43. 既存回帰テスト

`tools/poc2d3d/v32_run_regression.sh` で全部を実行しました（`logs/regression/summary.txt`）。Unity はウィンドウを出さずに実行しています。そのあと、下の 1 件を直して、影響のある検査をもう一度実行しました（`logs/regression2/`）。表は最終の結果です。ver3.1 の報告（34章）の結果と、失敗した項目を 1 行ずつ比べました。

| 検査 | ver3.1 | ver3.2（最終） | 失敗の分類 |
|---|---|---|---|
| PocChecks（Phase 1） | 102 合格 | 102 合格 | — |
| Phase2EnvironmentChecks | 130 合格 | 130 合格 | —（下の注） |
| Phase21PresentationChecks | 60 合格 | 60 合格 | — |
| Phase3BuildingFurnitureChecks | 74 合格・7 失敗 | 74 合格・7 失敗 | ver3.1 と同じ 7 件（過去の版専用・その巻き添え・意図した変更） |
| Phase4IntegrationChecks | 125 合格・9 失敗 | 125 合格・9 失敗 | ver3.1 と同じ 9 件（過去の版専用・意図した変更） |
| Phase5ReleaseCandidateChecks | 117 合格・14 失敗 | 117 合格・14 失敗 | ver3.1 と同じ 14 件 |
| Phase6SaveChecks | 110 合格 | 110 合格 | — |
| Phase7ReleaseChecks | 54 合格・14 失敗 | 53 合格・15 失敗 | ver3.1 と同じ 14 件 ＋ 基準の移動 1 件（下の注） |
| Version30Checks（2D 本編の連鎖） | 停止（ver2.5 の段） | 停止（同じ所） | 過去の版専用（家に `desk_basic` が 1 台ある前提。ver3.1 で撤去） |
| MapAuthoringChecks | 停止 | 停止（同じ所） | 過去の版専用（検査用のカラスを家の (3,0) に置く。そこは ver3.1 から防音室） |
| WORLD EDITOR Core Tests | 181 合格 | 181 合格 | — |
| Site Editor Tests | （ver3.1 では未記載） | 314 合格 | — |
| WorldVer31Checks | 191 合格 | 191 合格 | — |
| WorldVer32Checks（新規） | — | 219 合格 | — |

- **新しい機能の退行: 0 件。**
- 失敗の項目は、ver3.1 の失敗と 1 行ずつ同じです（`diff` で確認）。ただし、次の 2 件は新しく出たので、原因を調べました。
  - **Phase 2「20 回の切替で何も作り直さない」（1回目の実行で失敗 → 直して合格）**
    - 原因: この古い検査は、切替の前後で地面の Object（`GroundObject`）が同じかどうかを比べます。ver3.2 の互換用 `GroundObject` は「立っているチャンクの地面」を返していました。
    - 検査の途中で、開始位置の右の石に止められて、隣のチャンク (-1,0) へ歩いていたため、別の Object を返していました。作り直しは起きていません（ver3.2 の検査で、作成・解放 0 を確認済み）。
    - 対応: 互換用の答えを「Map の (0,0) のチャンクの地面」（歩いても変わらない）に変えました。検査の中身は変えていません。ゲームの動きは変わりません（チャンクモードでは使われない値）。
  - **Phase 7「更新履歴の ver3.0 の段」（新たな失敗・基準の移動）**
    - 公開ページの更新履歴は、ver3.1 の公開で main 側が変わりました。ver3.2 のブランチは、ページを一切変えていません（`git diff 144a534` に halkaworld/ なし）。
    - ver3.0 の時点のページを前提にした、過去の版専用の検査です。
- WorldVer32Checks は、全体の実行で 1 回だけ 218 件・1 失敗になりました。
  - 原因: 「テスト World が正式データに混ざらない」の確認で、Git の差分を `Assets/Content` 全体で見ていたためです。2D 本編の検査（Version30Checks）が `GrassDecoration.prefab` を改行だけ書き直した直後に動いたので、差分ありと判定しました。
  - 対応: 確認の範囲を `Assets/Content/Maps` に絞りました。最終の実行で 219 件合格です。
- 古い検査は 1 件も削除・緩和していません。指示書 20章の分類:

| 分類 | 件数（最終） |
|---|---|
| 現在も有効な共通機能テスト | すべて合格（上の合格件数） |
| 過去バージョン専用テスト | Phase 3〜5・7 の版固有の失敗、Version30Checks・MapAuthoringChecks の停止（ver3.1 の報告と同じ内訳） |
| 意図的な配置変更による不一致 | ver3.1 の改装による失敗（ver3.1 の報告と同じ） |
| 新しい機能退行 | 0 |
| 未実施 | 実機スマホ、Standalone の計測 |
| 実行不能 | なし |

## 44. 既存データの保護

- 作業前の 1,096 ファイルのうち、1,080 ファイルは SHA-256 が同じです。
  - 変わった 14 ファイル: 5章の「変更」のコードと、POC シーン（作り直し）。
  - なくなった 2 ファイル: ver3.1 のシーンの名前変更（`HARUKA_WORLD_ver3.1.unity` とその .meta）。
  - 詳しくは `logs/sha256_compare.txt`。
- 変わっていないもの:
  - 正式 Map（first_field・halka_house・halka_soundbooth）とその Authoring JSON
  - Catalog、Blender Source、FBX、Sprite
  - WORLD EDITOR v1.0
  - 公開中の ver3.1（`halkaworld/` の 14 ファイル、バックアップと同じ）
- ユーザーのブラウザのセーブには触れていません。
  - 検査はメモリのセーブ・一時プロファイル・ローカルのテスト用 origin だけで行いました。
  - チャンク確認版は、セーブをメモリだけに書きます。
- Unity の検査が書き換えた Material・Catalog・シーンの改行だけの変化と、検査中の Sprite の状態は、元に戻してから Commit しました。

## 45. 比較画像

付録の図 1〜14（＋参考）。すべてローカル WebGL（headless Chrome、自動操作）の画面です。

| 指示書の項目 | 図 |
|---|---|
| ver3.1 屋外 2D / ver3.2 屋外 2D | 図1 / 図2 |
| ver3.1 屋外 3D / ver3.2 屋外 3D | 図3 / 図4 |
| Chunk 境界のデバッグ表示 | 図5（2D）・図5b（3D） |
| Chunk 読み込み状態 | 図6 |
| Chunk 解放状態 | 図7 |
| 負数座標のテスト | 図8 |
| 大型 Object の Chunk またぎ | 図9 |
| 128×128 テスト World | 図10・図10b |
| 256×256 テスト World | 図11・図11b |
| 家・防音室が正常な状態 | 図12・12b・12c・12d |
| スマホ縦向き / 横向き | 図13 / 図14（エミュレーション） |

## 46. 操作動画

- `video/ver32_chunks_auto.mp4`（27 秒、約 60fps、14 MB）。**自動操作の録画です**（headless Chrome に CDP でキー入力。画面下に「自動操作の録画」と表示）。
- 内容（指示書 24.4 の順）:
  1. 128×128 のテスト World に入る（開発パネル・チャンク境界の線を表示）
  2. 2D で東へ長押しし、チャンク境界をまたぐ
  3. 開発パネルの Chunks ready / queued・load / free の数値で、読み込み・解放を確認
  4. 3D へ切り替える
  5. 同じ道を 3D で西へ戻る
  6. 2D に戻し、元のチャンクの場所へ歩く
  7. 4方向に動かし、表示と当たり判定が正常であることを確認
- 実際の人の操作の録画ではありません。

## 47. Git Commit

ブランチ `claude/harukaworld-v32`（ローカルのみ。Push・Merge していません）:

| Commit | 内容 |
|---|---|
| `6abbf9b` | Phase 2〜3: World 座標・チャンク表・streaming・registry・テスト World の生成（ver3.1 の検査 191 件合格） |
| `991d068` | WorldVer32Checks（219）と、検査で見つけた streaming の修正 |
| `2d23ad4` | 開発用の計測、チャンク確認版、ver3.2 ローカルの付属一式、チャンクサイズの比較、設計資料 |
| `924cb6a` | 回帰の一括実行、互換用 GroundObject の安定化、この報告 |
| （この後） | 報告の最終の値（47・48章） |

## 48. 最終 Git 状態

- ブランチ: `claude/harukaworld-v32`。origin/main `144a534` から 5 Commit 先です。
- origin/main は `144a534` のまま（Push なし）。main への Merge もしていません。GitHub Pages・halkaclub.com は変えていません。
- 作業ツリー: 未コミットの変更なし。検査が改行だけ書き換えた Material・Catalog・シーンと、検査中の Sprite の状態は、元に戻しました。
- ローカル Build（リポジトリの外）:
  - `HARUKA_WORLD_ver3.2_LOCAL`（build `0e1134f5…`、`start_v32.bat` → http://127.0.0.1:8932/halkaworld/）
  - `HARUKA_WORLD_ver3.2_CHUNKTEST`（`webgl/` と `webgl_dev/`。ポート 8933）
- 公開中の ver3.1（`halkaworld/`）と、ver3.1 のローカル版（`HARUKA_WORLD_ver3.1_LOCAL`）はそのままです。

## 49. 未解決事項

- 実機のスマートフォンでの確認（表示・操作・性能）: 未実施。
- Standalone での計測: 未実施（39章）。
- ver3.1 の WASM heap: 計測の仕組みがないため未計測（ver3.2 との差は JS heap と fps で比較）。
- 3D の見通しの先（40 セルより遠く）は、描いていません。
  - first_field では全体が見通しの中なので、変化はありません。
  - 広い World では、3D の地平線の手前で地面が終わって見えます。
  - 将来の地域を足す時に、霧などで遠くを隠すかどうかは、見た目の判断が必要です（ver3.3 の検討項目）。
- テスト World の生成コード（`PocTestWorldGenerator`）は、コードとしては公開用 Build にも含まれます（使われず、データもありません。数 KB）。
- 開発パネル（`?debug=1` の時だけ）は、チャンクの行を足したため、狭い画面では最後の行（AUTO・カラス）が少し切れます。開発用だけで、通常の画面には出ません。
- 自動操作の道順の都合で、WebGL の再確認を何度かやり直しました（38章の注）。ゲームの不具合ではありません。

## 50. 技術的完成判定

| 指示書 25章の条件 | 判定 | 根拠 |
|---|---|---|
| 屋外共通 World 座標が実装されている | 達成 | 7〜9章、37章 coordinates |
| 現在の first_field を新 World へ配置できる | 達成 | registry・Offset (0,0) |
| 現行フィールドの配置が変化していない | 達成 | 37章 official-field、図1〜4 |
| 負数座標を正しく扱える | 達成 | 11章 |
| Chunk への分割 | 達成 | 10・14章 |
| Player 周囲の Chunk を読み込める | 達成 | 15章、38章 |
| 不要な Chunk を解放できる | 達成 | 16章、38章 |
| 再読み込みしても表示が正常 | 達成 | 16・18章、38章 |
| Chunk 境界を歩いて通過できる | 達成 | 17章、38章 |
| 2D／3D が同じ World 座標を使用する | 達成 | 5・22章、37章 view |
| Object や Collision が Chunk 境界で壊れない | 達成 | 17・18章 |
| Grass・Depth・Outline・影が維持される | 達成 | 19〜23章 |
| カラスの既存行動が維持される | 達成 | 24章 |
| AUTO が正常 | 達成 | 25章 |
| 家へ出入りできる / 防音室へ出入りできる | 達成 | 26〜28章 |
| ver3.1 の右上切替ボタンが正常 | 達成 | 38章 |
| 旧 ver3.0 セーブ / ver3.1 セーブを読み込める | 達成 | 32・33章 |
| 歩数・プレイ時間・朝夜が維持される | 達成 | 29・30章 |
| 256×256 のテスト World で Chunk が機能する | 達成 | 38章（142 セルの長押し、55 解放、36 チャンクの再読み込み） |
| PC ローカル WebGL で正常動作する | 達成 | 38章 |
| 性能に重大な Regression がない | 達成（PC） | 39章。実機スマホは未確認 |
| WORLD EDITOR v1.0 が変更されていない | 達成 | 35・44章 |
| 公開 ver3.1 が変更されていない | 達成 | 44章 |
| 設計資料と検証レポートが完成している | 達成 | この報告と設計資料 |

**判定: 技術的には完成（PC のローカル WebGL・Unity の検査の範囲で）。**

- 指示書 25章の条件は、すべて満たしています。
- 未確認なのは実機のスマートフォンと Standalone の計測で、どちらも NOT RUN と明記しました。
- 公開は、ユーザーの確認と許可を待ちます。

公開の前に、次の確認をお願いします:
1. ローカル版（`start_v32.bat`）で、いつもの遊び方（屋外・家・防音室、2D/3D）に違和感がないか。
2. 可能なら、実機のスマートフォンでの表示と重さ。

## 51. ver3.3 で必要になる作業

1. 新しい屋外 Region を WORLD EDITOR v1.0 で作り、registry に Offset を書いて追加します（手順は設計資料 13章）。最初は first_field の道の端（東西南北の `road_*` marker）に隣り合わせて置きます。
2. Region どうしのつなぎ目:
   - 道の続きを、セルが隣り合うように作ります。
   - first_field の外周の飾り（今は通れない外側 8 セル）を、隣の Region の地面と置き換える確認をします。
3. 遠景: 3D の見通しの先（霧・遠景の絵・LOD）を決めます（49章）。
4. カラス: 行動範囲を Region の外へ広げるかどうか（`PocCrow.PerchCandidates` の Region 制限）。追従・「そらをとぶ」は World 座標をそのまま使えます。
5. AUTO: 新しい Region で散歩してよい範囲（今は描かれたチャンクの中）。
6. セーブ: Region を足すだけなら Schema 1 のままで足ります。同じ Map を2か所に置く・Offset を動かす場合だけ、Schema 2（設計資料 11.1）を検討します。
7. 実機スマホでの計測。Draw Call の上限を決め、必要ならチャンクの結合（地面と房を1つの Mesh に）を検討します。
8. registry を読み込む時の、Region の境界の自動検査を足します。両側の Map で、境界のセルの通行が食い違わないことを確かめます。
