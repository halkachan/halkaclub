# はるかわーるど ver3.3「シームレスエリア拡張・東エリア追加アップデート」実装レポート

作成日: 2026-10-27
対象: ゲーム本編（Unity 6000.3.10f1 / WebGL）。WORLD EDITOR v1.0 は変更していません。
状態: **ローカル実装と検証まで完了・未公開**。main への Merge・Push、GitHub Pages、halkaclub.com、公開中の ver3.2 Build には一切触っていません。公開はユーザーの確認と許可を待ちます。
設計資料: `HARUKA_WORLD_VER33_REGION_CONNECTION_SPEC.md`（Region の足し方。実際のコードと同じ内容）

## まとめ（最初に読む所）

- `first_field` の東に新しい屋外の地域 `east_area_01`（**ひがし**）を足しました。
  - 大きさは 26×13 セルで、World の (11,-6)〜(36,6) にあります。
  - first_field の東の道（World y = -1）が、そのまま東エリアの中を (36,-1) まで続きます。
- 東の道を歩くだけで、(10,-1) → (11,-1) と東エリアに入れます。同じ道を歩いて戻れます。
  - 暗転・Scene の切り替え・カメラの飛び・瞬間移動・二重の歩数は、どれも起きません。
  - 境界の 1 歩は普通の 1 歩です。Map の読み込み（`MapChanged`）もありません。
  - 専用のボタンもありません。
- 方角の色を 1 か所（`direction_themes.json`）で決めました: **北=青・東=黄・南=赤・西=緑**。
  - 東エリアでは、黄色を花・ひまわり・看板・目印に使っています。地面は塗っていません。
- 目印として **「黄色い花が集まる小さな広場」**（ID `landmark_east_flower_plaza`）を作りました。
  - 道の途中の小さな丸い広場で、真ん中にひまわりの群れ、まわりに黄色い花があります。
- 東エリアの見た目は **デザイン案** です（指示書 19章）。図1〜16 で確認をお願いします。
- `first_field` は 1 セルも変えていません。
  - 草の房 200/200、Object 29/29、Collision 273 セルが ver3.2 と同じです。
  - 2D の画面も ver3.2 と同じ位置から写します（カメラの位置 (0.00, 22.98, -19.28) で一致）。
- セーブは Schema 1 のままです（Map ID とその Map のセル）。
  - 東エリアで保存・再開できます。
  - ver3.0 / ver3.1 / ver3.2 のセーブもそのまま読めます。
- 検査:
  - 新しい `WorldVer33Checks`: **122 件合格・0 件失敗**
  - ローカル WebGL（ver3.3 本体、指示書 18.1〜18.4 の道順）: **20 件合格・0 件失敗**
  - 既存の回帰テスト: 新しい機能の退行 0 件。新しく出た失敗 20 件は、どれも「屋外は first_field だけ」という前提で数える古い検査です（40章）。
  - 回帰テストで、東エリアの看板の文がゲームで出ない不具合が 1 件見つかりました。直してあります（17章）。
- 性能（PC の headless Chrome、ローカル WebGL）:
  - 東エリア・境界・first_field の 2D/3D すべてで 60fps。境界をまたぐ 1 歩の最長フレームは 16.8ms です。
  - 境界の往復でチャンクの作り直しは 0 回でした。
  - 開始時から東エリアの手前も描くので、描く量は増えました（GameObject 135 → 266、WASM heap 46.1 → 55.4 MB）。fps は同じです（36・38章）。
- 実機のスマートフォンでは確認していません（PC Chrome のエミュレーションで画面の形だけ確認）。

---

## 1. 開始時 Git 状態

- 作業 Worktree: `C:\Users\owner\Documents\ChatGPT\HALKA_HP_V33`（新規）
- 開始 Commit: `edbb217`「はるかわーるど ver3.2 公開 (2026-10-24)」= `origin/main`
- 未 Commit の変更: なし（`git_state.txt`）
- ユーザーの作業中の Worktree（`HALKA HP`）には触れていません。

## 2. 公開 ver3.2 の基準 Commit

- `edbb217`（main）。公開 Build は `580cf7c6`（`halkaworld/webgl/`）。
- ver3.3 は、この Commit から作ったブランチの上だけで作業しました。

## 3. 公開 Build とローカル Build の区別

| | 公開 ver3.2 | ローカル ver3.3（今回） |
|---|---|---|
| 場所 | リポジトリの `halkaworld/`（GitHub Pages） | `C:\Users\owner\Documents\ChatGPT\HARUKA_WORLD_ver3.3_LOCAL` |
| Build | `580cf7c6` | `48cdef33` |
| 起動 | halkaclub.com | `start_v33.bat`（`http://127.0.0.1:8934/halkaworld/webgl/`） |
| 表示 | ver3.2 | 「はるかわーるど ver3.3 東エリア追加アップデート (local check)」（右上の開発表示も「ver3.3 LOCAL」） |

- 比べるための ver3.2 は、別のローカル Build（`HARUKA_WORLD_ver3.2_LOCAL`、ポート 8932）を使いました。
- 公開中の `halkaworld/` は 1 ファイルも変わっていません（44章）。
- 検査に使った Chrome は、途中で PC の再起動があり Chrome 155 から 156 に上がりました。最終の WebGL の結果と性能は、すべて Chrome 156・最終 Build `48cdef33` のものです。

## 4. Backup

`C:\Users\owner\Documents\ChatGPT\HARUKA_WORLD_V33_BACKUP_20261025`（574 MB）

- `git_state.txt`（Commit・ブランチ・Worktree の一覧）
- `halkaclub_all_refs.bundle`（全ブランチの Git bundle、369 MB）
- `unity-project/`（Content・POC2D3D のソースと設定、819 ファイル）、`blender/`
- `halkaworld_published_ver32_webgl/`（公開中の ver3.2 のページと Build）、`ver32_local_build/`
- `world_registry/`、`authoring_maps_and_catalogs/`、`docs/`
- `sha256_before.txt`（1,230 ファイル）

## 5. 変更ファイル一覧

新規:

- `Assets/Content/Maps/Authoring/east_area_01.hwmap.json`（東エリアの Map。WORLD EDITOR v1.0 の形式）と、取り込み結果の `east_area_01.asset`
- `Assets/Content/Maps/World/direction_themes.json`（方角の色）
- `Assets/Content/Maps/flower_yellow.asset` / `sunflower_cluster.asset` / `signpost_yellow.asset`（Object の定義）
- `Assets/Content/World/flower_yellow.png` / `sunflower_cluster.png` / `sign_yellow.png`（2D 用の絵。道しるべの絵）
- `Assets/POC2D3D/Models/V33/*.fbx`（3 モデル）、`blender/sources/v33/*.blend`、`blender/scripts/build_v33_east.py`・`render_v33_previews.py`、`blender/exports/previews/v33/`
- `Assets/POC2D3D/Editor/WorldVer33Checks.cs`（検査）、`WorldVer33Capture.cs`（全体図）
- `tools/poc2d3d/v33/`（ローカル確認用の起動キット）、`tools/poc2d3d/v33_reduce_sprites.py`、`tools/poc2d3d/v33_run_regression.sh`
- `HARUKA_WORLD_VER33_REGION_CONNECTION_SPEC.md`、この報告

変更:

| ファイル | 内容 |
|---|---|
| `Content/Maps/World/outdoor_main.hwworld.json` | Region を 1 行追加、`direction` 欄 |
| `Content/Maps/Authoring/object_catalog.hwcatalog.json` | 3 種類の Object を追加 |
| `Runtime/World/OutdoorWorldDefinition.cs` | 方角・テーマ、検証（未知の方角・テーマなし） |
| `Runtime/World/OutdoorWorldIndex.cs` | Region に方角 |
| `Runtime/SharedWorldSession.cs` | 今いる Region、Region の変化の数と通知 |
| `Runtime/PocGameplay.cs` | AUTO は今いる Region の外へ出ない |
| `Runtime/Save/HarukaSaveSystem.cs` | Region の場所名、ふさがったセルの戻り先は同じ Region から |
| `Runtime/PocPixelView2D.cs` | 2D カメラの範囲（各 Region の中心まで） |
| `Runtime/PocWorldPresenter.Chunks.cs` | Region 境界のデバッグ線 |
| `Runtime/PocDebugPanel.cs`、`Runtime/Dev/PocDevStats.cs`、`Plugins/WebGL/HarukaDev.jslib` | 開発表示（Region）、版の名前 |
| `Editor/OutdoorWorldRegistryJson.cs`、`Editor/PocSceneBuilder.cs`、`Editor/PocBuilder.cs` | テーマの読み込み、モデルの登録、シーン名・Build 先 |
| `Editor/WorldVer32Checks.cs` | 共通の道具を ver3.3 の検査から使えるように（private → internal。検査の中身は同じ） |
| `Scenes/HARUKA_WORLD_ver3.2.unity` → `HARUKA_WORLD_ver3.3.unity` | 名前の変更（シーンは生成物） |
| `Data/POC_PresentationCatalog.asset`、`Scenes/HALKA_WORLD_2D3D_POC.unity` | 生成物（モデルの登録が増えた分） |

## 6. 東エリアの Map ID

`east_area_01`（表示名「東エリア」、`mapType: outdoor`）。方角は `east`、場所の名前は「ひがし」です。

## 7. 東エリアの広さ

- ローカル (0,0)〜(25,12)、**26×13 セル**（first_field は 21×13）。
- 小さくても完成度を優先しました（指示書 21章）。Object は 47 個です。
  - 黄色い花 19、木 15、白い花 5、石 5、ひまわりの群れ 1、黄色い道しるべ 1、ベンチ 1（一覧は `docs/east_area_01.hwmap.json`）。

## 8. Region Registry

`Assets/Content/Maps/World/outdoor_main.hwworld.json` に 1 行足しました。

```json
{ "mapId": "first_field",  "offset": { "x": 0,  "y": 0 },  "direction": "center" },
{ "mapId": "east_area_01", "offset": { "x": 11, "y": -6 }, "direction": "east" }
```

- 重なり・同じ Map の 2 回登録・未知の Map ID・屋内 Map・未知の方角・範囲外の Offset は、すべてエラーで止まります（壊した Registry を読ませる検査で確認）。
- `direction` のない ver3.2 の Registry も読めます（`center` 扱い）。

## 9. World Offset

`(11, -6)`。決め方:

- 東の道は World y = -1、東エリアの道はローカル y = 5 → `offset.y = -1 - 5 = -6`
- first_field の東端は x = 10 → `offset.x = 10 + 1 = 11`（すき間も重なりもない）

## 10. Region-local 座標

- 東エリアの中の位置は、東エリアの Map のセル（ローカル）で表します。セーブもこの値です。
- Map の目印:
  - `road_west`: ローカル (0,5)
  - `road_east`: ローカル (25,5)
  - `landmark_east_flower_plaza`: ローカル (16,4)

## 11. World 座標

- `world = local + offset`、`local = world - offset`。全セルで往復が一致します。
- 東エリア: World (11,-6)〜(36,6)。first_field: (-10,-6)〜(10,6)（動かしていません）。
- World 全体: (-10,-6)〜(36,6)。縦の幅は ver3.2 と同じです。

## 12. 既存フィールドとの接続

- first_field の (10,-1)（東の道の終わり）の隣が、東エリアの (11,-1) = ローカル (0,5)（道の始まり）です。
- 両側とも `dirt_road` で、境界の列には通行を止める Object を置いていません。
- first_field の中は変えていないので、接続のために first_field の道を延ばす必要はありませんでした（ver3.2 の時点で (10,-1) まで道がありました）。

## 13. 東西の往復

- Editor（2D と 3D）: スタート → (10,-1) → 1 歩で (11,-1) → 広場の前 (27,-2) → 西へ (8,-1)。
  - 境界で Map の読み込み・`MapChanged`・遷移なし。歩数は歩いたセルの数と同じ、向きは右（東）。Region の変化は行きと帰りで 1 回ずつ。
  - 境界の 1 歩の途中で止めても、必ず (11,-1) で終わります（(10,-1) に戻らない）。
- ローカル WebGL（18.1・18.2）: 同じ道順を、キー入力で歩きました。
  - 2D・3D とも、境界の 1 歩で止まり・チャンクの作成なし・暗転なし。
  - 境界の 10 往復（(10,-1) ↔ (11,-1)、20 歩）: Region の変化 20 回、最長フレーム 16.9ms、チャンクの解放 0。
  - 長押しで (9,-1) から東へ: 境界を止まらずに越えて (26,-1) まで。同期読み込み 0。

## 14. Region 境界

- 境界は x = 10 | 11 です。
- 境界の両側のセルは、それぞれ自分の Map の規則で答えます（(10,y) は first_field、(11,y) は east_area_01）。
- 両側が空いている 12 行すべてで、東への 1 歩が許されます（見えない壁はありません）。
- どの Region にも属さないセル（(37,-1)、(11,7) など）は歩けません。草も生えません。World の東端 (36,-1) で東へ押しても歩数は増えません。
- デバッグ表示: `?debug=1` のパネルで「Region borders」を ON にすると、方角の色の線で境界を描きます（初期値 OFF。図10・11）。

## 15. Chunk 境界

- チャンクは 16×16 セルで、ver3.2 と同じ 1 つの表を使います。東エリアのチャンクも同じ表に入ります。
- **Region 境界 ≠ Chunk 境界**:
  - Region 境界 x 10|11 は、チャンク (0,*) の中を通ります。境界をまたいでも、チャンクの出し入れは起きません。
  - Chunk 境界 x 15|16 は、東エリアの中にあります。またいでも Region は変わりません。
- スタート地点の時点で、東エリアの手前のチャンクは描かれています（3D の見通し 40 セル）。境界の 1 歩でチャンクを待つことはありません。
- 東の端まで歩いた時も、足元のチャンクはすべて先に用意されていました（Step Gate の同期読み込み 0）。
- 西の端 → 東の広場 → 戻る: チャンクの解放と作り直しが正しく起き、目印は 1 回だけ描かれます。

## 16. 地面と道の連続性

- 地面の草・道・草の房は、first_field と同じ Ground Builder・同じ規則で作ります。
- 道は (3..36, -1) の 1 本の続いた道です。境界で色・形の継ぎ目はありません（図5・6）。
- 草の房の密度: first_field 1.09 本/セル、東エリア 0.92 本/セル（同じ規則。道と Object の割合の違い）。

## 17. Object

- 新しい Object（WORLD EDITOR のカタログに追加）:

| ID | 表示名 | 大きさ | 調べた時の文 |
|---|---|---|---|
| `flower_yellow` | 黄色い花 | 1 セル | 「きいろい はな。」 |
| `sunflower_cluster` | ひまわり | 1 セル（絵は縦長） | 「ひまわり。 みんな おひさまの ほうを むいている。」 |
| `signpost_yellow` | 道しるべ（ひがし） | 1 セル | 「このさき きいろい はなの ひろば。」 |

- モデルは Blender で作りました（図16）。黄色は `direction_themes.json` の east の色から読みます。
- 道しるべは、文を Object の定義に持たせています（`signpost_yellow`。モデルと絵のファイル名は `sign_yellow`）。
  - 最初は `sign_yellow` という看板にして、文を Map の配置（`signText`）に入れていました。しかし、配置の文を読むのは `sign_basic` だけ（ゲームも WORLD EDITOR v1.0 も）なので、ゲームでは「かんばん。」と出ていました。回帰テストの WORLD EDITOR のテスト（「看板の定義は1つ」）で見つかり、直しました。
  - 今は、正面のセル（ローカル (1,5)）から上を向いて調べると「このさき きいろい はなの ひろば。」と出ることを、検査で確かめています。
  - ID を `sign_east` にしなかったのは、取り込みの旧名変換で `sign_basic` になるためです（設計資料 10章）。
- 既存の木・石・白い花・ベンチは、first_field と同じものを使っています。
- 東エリアの 47 個はすべて Blender のモデルで描かれ、代わりの箱はありません。どの Object も 1 回だけ描かれます。

## 18. Collision

- 通れるかどうかは、セルを持つ Region の Map の規則を、ローカルセルで引きます。東エリアの全セルで、Map の規則と一致しました（ずれ 0）。
- 境界にかかる Object（first_field の看板 (10,0)）は、絵が東のセルにかかりますが、ふさぐセルは first_field のものだけです。1 回だけ描かれます。
- 東エリアの Action Point（看板・ベンチの席・目印・花）は、すべて道の西の入口から歩いて届きます。

## 19. 2D 描画

- 2D は 3D の表示を正射影で撮る仕組み（ver3.2 と同じ）なので、東エリアも同じチャンクから描きます。
- 東エリアの草のセルは、first_field の草と同じ一覧に入ります（北の行から）。
- ひまわりは、手前に来た時に木と同じように薄くなります（Tree Fade）。
- 図2・5・7・10・11。

## 20. 3D 描画

- Outline（すべての東エリアの Object に Outline ID）、影（木とひまわり）、Tree Fade は、first_field と同じです。
- プレイヤーは東エリアでも、自分の World セルの位置に描かれます（2D・3D とも）。
- 図4・6・8・11b。

## 21. Camera

- 3D: プレイヤーを追うだけ（ver3.2 と同じ）。境界の 1 フレームの最大の動き 0.039 unit（first_field の中と同じ）。
- 2D: 画面に World の外を写しすぎないようにする範囲を、「各 Region の中心の上には必ず立てる」に広げました。
  - 理由: 東エリアを足しただけで、first_field の 2D の写り方が 2.5 セル東へずれていたため。
  - 直した後: first_field の中では ver3.2 と同じ位置から写します（スタート (0,0) も西端 (-9,2) も、ver3.2 と同じ (0.00, 22.98, -19.28)）。
  - 境界では範囲が広がるだけなので、カメラは滑らかについていきます（最大 0.031 unit/フレーム。飛びなし）。
- 東エリアで再開した時は、最初からプレイヤーの上にカメラがあります（スタート地点から滑ってこない）。

## 22. 北青・東黄・南赤・西緑の対応

`Assets/Content/Maps/World/direction_themes.json` で一元管理しています（図9）。

| 方角 | 名前 | color | shade | 使っている Region |
|---|---|---|---|---|
| center | まんなか | #f2eee6 | #c8c2b5 | first_field |
| north | きた | #5b84b1 | #3d5f86 | （まだない） |
| east | ひがし | #f2c443 | #c99425 | east_area_01 |
| south | みなみ | #c8564b | #953b33 | （まだない） |
| west | にし | #6e9f57 | #4b7a3b | （まだない） |

- 色相の検査: 北 211°（青）、東 44°（黄）、南 5°（赤）、西 101°（緑）。
- 色の形式違い・方角の欠けはエラーです。
- この色を読むのは、ゲーム（場所の名前・デバッグ線）と Blender のスクリプト（モデルの黄色）です。

## 23. 東エリアの景観

- first_field と同じ明るい草原です。地面は黄色にしていません。
- 東の道に沿って、北側と南側に木の列があります。木の間に石・白い花・黄色い花があります。
- 入口（ローカル (1,6)）に黄色い道しるべ、その先の北側にベンチがあります。ベンチはチャンク境界 x 15|16 をまたいでいます（大きな Object のチャンクまたぎの確認を兼ねる）。
- 広場に近づくほど、黄色い花が増えます。
- 公開用の Map には、景観を壊す大きなテスト Object を置いていません。
- **ユーザーの承認はまだです。デザイン案として、図1〜8・16 で見た目の確認をお願いします。**

## 24. ランドマーク

- 「黄色い花が集まる小さな広場」、ID `landmark_east_flower_plaza`（Map の目印。変えない ID）。
- 道の途中（ローカル x = 16 付近）で、道が丸く広がった小さな広場です。
- 真ん中のローカル (16,5) = World (27,-1) に、ひまわりの群れ（`sunflower_cluster`）があります。
  - 広場のまわりの上下の行に、黄色い花が 8 本あります（東エリア全体では 19 本）。
- 目印のセルはローカル (16,4) = World (27,-2) で、ひまわりの正面（南）です。ここで上を向いて調べると「ひまわり。 みんな おひさまの ほうを むいている。」と出ます。

## 25. Save Schema

- **Schema 1 のまま** です。新しい欄はありません。
- 東エリアで保存すると、Map ID `east_area_01` とローカルのセル（例: (9,6) = World (20,0)）が入ります。チャンクの番号や World 座標は保存しません。
- セーブ画面の場所名は「ひがし」です（方角の名前）。家・防音室・first_field は従来どおり「家」「ぼうおんしつ」「そと」です。

## 26. 旧セーブ互換

| 作った版 | 中身 | 結果 |
|---|---|---|
| ver3.0 | first_field (-1,0)、3D、AUTO ON | 同じ場所・視点・AUTO・歩数・時間 |
| ver3.1 | halka_house (-2,0) | 同じ |
| ver3.1 | halka_soundbooth (3,-1) | 同じ |
| ver3.2 | first_field (10,-1)（東の道の終わり） | 同じ |
| ver3.2 | halka_house (4,-2) | 同じ |

- first_field のセーブで、セルが Map の外（壊れた値）だった時は、**first_field の中** の近い安全なセルから再開します（東エリアに出ることはありません）。
- **戻す場合**（ver3.2 の Build に戻す）: ver3.2 には東エリアがないので、東エリアのセーブは first_field のスタート地点から始まります。歩数・時間・設定は消えません。
  - ただし、ver3.2 で遊ぶとその位置で上書き保存されていきます（東エリアの位置は失われます）。
  - セーブを消して初期化する仕組みは使っていません。

## 27. 東エリアでの保存・再開

- Editor（2D・3D それぞれ）: 東エリアで保存 → 再開で、同じ World セル・同じ視点・歩数そのまま・歩数の追加なし。カメラは最初からプレイヤーの上。
- ローカル WebGL（18.3）: 東エリアで保存 → ページを閉じて開き直す → 東エリア (20,0) から再開。最初の画面から東エリアで、スタート地点が一瞬見えることもありません。そのまま西へ歩いて first_field へ帰れました（図12）。
- 東エリアのふさがったセル（ひまわり）に保存されていた場合は、東エリアの中の隣の空きセルから再開します。

## 28. Export／Import

- 東エリアで Export → Import: 受け付けて、広場の前 (27,-2) に戻ります。
- 知らない Region の Map ID（例 `east_area_99`）は拒否します（`UnknownMap`）。今のセーブは置き換えません。
- 東エリアの Map の外のセルは拒否します（`OutOfMap`）。

## 29. カラス

- カラスは 1 羽で、first_field に住んでいます（Map の配置のまま、ver3.2 と同じ）。
- プレイヤーが東エリアへ行って戻る間も、1 羽のまま first_field で普通に動いていました（歩く・飛ぶ・とまる・降りる）。
- 東エリアにはカラスを置いていません（指示書 21章: 追従・飛行は対象外）。

## 30. AUTO

- AUTO は、今いる Region の外へは自分から出ません。
  - first_field の東端で AUTO を 5 分: 歩き回るが、東エリアには入らない（最大 x = 10）。
  - 東エリアに自分で歩いて入ってから AUTO を 5 分: 東エリアの中だけを歩く（最小 x = 11）、Map の変化なし。
- プレイヤーの操作では、もちろん境界を越えられます。

## 31. 朝夜

- World Time は 1 つです。境界を 6 回往復しても、時間の基準は変わりません。
- 夜の東エリアの地面は、first_field と同じ照明の Material を使います。

## 32. Life Log

- 境界の往復 6 歩は、Life Log でも 6 歩として数えられます。
- World の東端で止められた 1 歩は、数えません。

## 33. 家・防音室

- 東エリア → first_field → 家の前、と歩いて家に入り、家 → 防音室 → 家 → first_field → 東エリア、と戻りました（Editor と WebGL の 18.4）。
- ver3.2 の家具（キッチン・ラグとローテーブル・ソファ・ベッド・棚、防音室の 5 マスの DTM 机・3 マスの液タブ机・ギター・マイク）は、そのままです。

## 34. Unity Test

`WorldVer33Checks`（新規、`logs/ver33_checks.txt`）: **122 件合格・0 件失敗**

| 分類 | 内容 |
|---|---|
| registry（13） | 2 Region・Offset・座標の往復・Region の外・重なり／重複／未知の方角／未知の Map／屋内 Map／範囲外の拒否・ver3.2 の Registry |
| themes（8） | 4 方角の色と名前・色相・フォント・壊れたテーマの拒否・モデルの黄色 |
| content（14） | Map の取り込み・規則・Player Start なし・道・目印・黄色の Object・first_field 不変・フォント・道しるべの文・看板の定義は 1 つ・Action Point に届く |
| seamless（23） | 2D/3D の往復・1 歩・MapChanged なし・チャンクの作り直しなし・見えない壁なし・1 歩の途中 |
| camera（6） | first_field の写り方が ver3.2 と同じ・境界で飛ばない・東エリアでも追う |
| chunks（10） | 1 つのチャンク表・Region の混ざったチャンク・先読み・Region の外・二重描画なし・解放と作り直し・Collision の一致・境界の Object・道の連続 |
| field-look（3） | first_field の草・Object・Collision が ver3.2 と同じ |
| display（10） | モデル・Outline・影・Tree Fade・草・プレイヤーの位置・2D/3D 切替 6 回・デバッグ線 |
| save（19） | Schema 1・2D/3D の保存と再開・場所名・Export/Import・拒否・ふさがったセル・ver3.0〜3.2 のセーブ・戻す場合 |
| crow-auto（5） | カラス 1 羽・AUTO は Region を出ない（各 5 分） |
| rooms（6） | 東エリア → 家 → 防音室 → 家 → 外 → 東エリア・家具 |
| time-lifelog（3） | 歩数・時間・夜の照明・止められた 1 歩 |

## 35. WebGL Test

ローカル WebGL（`HARUKA_WORLD_ver3.3_LOCAL`、Build `48cdef33`）を、使い捨ての headless Chrome（専用の一時プロファイル、ポート 9344）で自動操作しました。ユーザーのブラウザとセーブには触れていません。

| 道順 | 内容 | 結果 |
|---|---|---|
| 開始 | first_field (0,0)、World `outdoor_main`、Region first_field | 合格 |
| 18.1 | 東の道 (10,-1) → (11,-1)：Region east_area_01、Map の読み込みなし、1 歩、向き右 | 合格 |
| 18.1 | 境界の 1 歩：止まりなし（最長 16.8ms）、チャンクの作成なし、暗転なし | 合格 |
| 18.1 | 黄色い花の広場の正面 (27,-2) で、ひまわりを向いて立つ | 合格 |
| 18.1 | 同じ道を西へ (8,-1)：Region first_field、Map の読み込みなし | 合格 |
| 18.2 | 東エリアで右上のボタン → 3D：同じセル・向き・歩数 | 合格 |
| 18.2 | 3D で西へ境界を越える（12 → 9）：止まりなし（最長 16.8ms）、1 歩ずつ | 合格 |
| 18.2 | 境界で右上のボタン → 2D | 合格 |
| 18.3 | 東エリアで保存：east_area_01 とローカル (9,6)（World (20,0)）、Schema 1 | 合格 |
| 18.3 | ページを閉じて開き直す：東エリア (20,0) から再開。最初の画面から東エリア | 合格 |
| 18.3 | そのまま西へ歩いて first_field へ | 合格 |
| 18.4 | 東エリア → 外 → 家 | 合格 |
| 18.4 | 家 → 防音室 → 家 → 外（ドアの前） | 合格 |
| 18.4 | 外 → 東エリアへ歩いて戻る | 合格 |
| 画面 | スマホ縦 390×844・横 844×390（エミュレーション）：東エリアが映り、ゲームが動く | 合格 |
| 性能 | 境界の 10 往復（(10,-1) ↔ (11,-1)）：Region の変化 20 回、最長 16.9ms、チャンクの解放 0 | 合格 |
| 性能 | (9,-1) から東へ長押し：境界を止まらずに越えて (26,-1)、同期読み込み 0 | 合格 |

合計 **20 件合格・0 件失敗**。比べるための ver3.2 ローカル（ポート 8932）でも、東の道が (10,-1) で終わること（当時の World の端）を確認しました（`logs/webgl_before/`）。

- 道しるべの文（17章）は、ゲームと同じ共通の処理（`SharedWorldModel.ResolveInteraction`）を Editor の検査で確かめています。WebGL の自動操作では、調べた時の文を読み取る仕組みがないため、WebGL では確かめていません。

- ログ: `logs/webgl/WEB_RESULTS.txt`・`WEB_LOG.txt`・`WEB_PERF.json`。
- 注: 境界 10 往復の検査は、1 回目の書き方では (9,-1)↔(10,-1) を往復しており、境界をまたいでいませんでした。(10,-1)↔(11,-1) に直しました。直した最初の実行では 20 回中 19 回になりましたが、原因は検査側でした（前の歩きが終わる前に最初のキーを押していた）。境界で 1 回ずつ確かめた別の確認では、320ms・600ms の間隔とも 20 回中 0 回の取りこぼしでした（ゲームの不具合ではありません）。

## 36. PC Performance

PC の headless Chrome（Chrome 156、1280×720）、ローカル WebGL、release Build。各 5 秒（300 フレーム）。

| 場所 | 視点 | fps | 平均 | P99 | 最長 | 描画中のチャンク | GameObject | Draw Call |
|---|---|---|---|---|---|---|---|---|
| first_field 開始位置（ver3.2） | 2D | 60 | 16.68ms | 16.8ms | 16.8ms | 8 | 135 | 49 |
| first_field 開始位置（ver3.2） | 3D | 60 | 16.68ms | 16.8ms | 16.8ms | 8 | 135 | 72 |
| first_field 開始位置（ver3.3） | 2D | 60 | 16.68ms | 16.8ms | 16.8ms | 10 | 266 | 61 |
| first_field 開始位置（ver3.3） | 3D | 60 | 16.68ms | 16.8ms | 16.8ms | 10 | 266 | 79 |
| first_field 東の道（ver3.3） | 2D | 60 | 16.68ms | 16.8ms | 16.8ms | 10 | 266 | 65 |
| first_field 東の道（ver3.3） | 3D | 60 | 16.68ms | 16.8ms | 16.8ms | 10 | 266 | 89 |
| 東エリア (26,-1) | 2D | 60 | 16.68ms | 16.8ms | 16.8ms | 10 | 266 | 79 |
| 東エリア (26,-1) | 3D | 60 | 16.68ms | 16.8ms | 16.8ms | 10 | 266 | 85 |
| 黄色い花の広場 | 2D | 60 | 16.68ms | 16.8ms | 16.8ms | 10 | 266 | 64 |
| 黄色い花の広場 | 3D | 60 | 16.68ms | 16.8ms | 16.8ms | 10 | 266 | 93 |

- headless Chrome は画面の更新に合わせて 60fps で止まるので、fps の差は出ません。差は描画の量（GameObject・Draw Call）で見てください。
- ver3.3 では、開始位置の時点で東エリアの手前のチャンクも描くので、チャンク 8 → 10、GameObject 135 → 266、Draw Call が 7〜12 増えます。それでもフレームは 16.8ms のままでした。

- 境界をまたぐ 1 歩（2D / 3D）: 最長フレーム 16.8ms / 16.8ms、50ms を超えたフレーム 0。
- 2D/3D 切替 6 回（東エリア）: 最長 16.8ms。
- 最初の読み込み（ロード画面が消えるまで、3 回）: ver3.2 527〜529 ms、ver3.3 528〜531 ms。
- Standalone での計測はしていません（WebGL が公開形式のため。ver3.2 と同じ）。

## 37. Mobile Performance

- **実機のスマートフォンでは確認していません（未実施）。**
- PC Chrome のエミュレーション（390×844 縦・844×390 横）で、東エリアが画面に出てゲームが動くことだけを確認しました（図13・14）。エミュレーションの fps は PC の性能なので、スマホの性能としては扱いません。

## 38. Memory

- WebAssembly の heap（`HEAPU8.buffer.byteLength`）、JS heap、Mono heap を分けて載せています。
- ver3.3: WASM heap 55.4 MB、JS heap 5.8〜7.9 MB、Mono heap 1.48〜1.59 MB（東エリア・first_field で同じ）。
- ver3.2（同じ PC・同じ日）: WASM heap 46.1 MB、JS heap 6.8〜7.6 MB、Mono heap 1.02〜1.04 MB。
- ver3.2 との差（WASM heap +9.3 MB、Mono heap +0.5 MB）は、東エリアの論理（Map・セル・Object 47 個）と、開始時から描く東エリアの 2 チャンク（GameObject +131）の分です。WASM heap は Emscripten の仕組みで段階的に大きくなる（一度大きくなると小さくならない）ため、増え方は実際の使用量より大きく見えます。ver3.2 のチャンク確認版の 32×32 World でも同じ 55.4 MB でした。
- 描いている GameObject は 266 個（first_field でも東エリアでも同じ。読み込んでいるチャンクは 10 個）。
- Build の大きさ: ver3.3 30,564,844 bytes、ver3.2 30,504,088 bytes（+60,756 bytes、+0.2%。東エリアの Map・3 つのモデル・2D の絵）。

## 39. GC Alloc

- 5 秒の計測の間の GC の回数: ver3.3 は 3〜4 回、ver3.2 は 5 回（どちらも Unity の自動 GC。止まりなし）。
- release Build では 1 フレームの GC Alloc（ProfilerRecorder）は取れないため NOT RUN です（ver3.2 と同じ）。
- ver3.3 で毎フレームの処理に足したのは「今いる Region」を引く処理だけです（辞書を引くだけで、割り当てなし）。

## 40. 既存Regression

`tools/poc2d3d/v33_run_regression.sh` で全部を実行しました（`logs/regression/summary.txt`）。Unity はウィンドウを出さずに実行しています。公開 ver3.2 の時の実行（`HARUKA_WORLD_V32_LOGS/regression_interior`）と、失敗した項目を 1 行ずつ比べました（`logs/regression/compare_with_ver32.txt`）。

| 検査 | ver3.2（公開時） | ver3.3 | 新しい失敗の分類 |
|---|---|---|---|
| PocChecks（Phase 1） | 102 合格 | 102 合格 | — |
| Phase2EnvironmentChecks | 130 合格 | 123 合格・7 失敗 | 7 件：屋外が first_field だけという前提（下の注 1） |
| Phase21PresentationChecks | 60 合格 | 60 合格 | — |
| Phase3BuildingFurnitureChecks | 74 合格・7 失敗 | 76 合格・7 失敗 | ver3.2 と同じ 7 件 |
| Phase4IntegrationChecks | 125 合格・9 失敗 | 124 合格・10 失敗 | ver3.2 と同じ 9 件 ＋ 1 件（注 2） |
| Phase5ReleaseCandidateChecks | 117 合格・14 失敗 | 114 合格・17 失敗 | ver3.2 と同じ 14 件 ＋ 3 件（注 1） |
| Phase6SaveChecks | 108 合格・2 失敗 | 108 合格・2 失敗 | ver3.2 と同じ 2 件 |
| Phase7ReleaseChecks | 53 合格・15 失敗 | 53 合格・15 失敗 | ver3.2 と同じ 15 件 |
| Version30Checks（2D 本編の連鎖） | 停止 | 停止（同じ所） | 過去の版専用（ver3.2 と同じ） |
| MapAuthoringChecks | 停止 | 停止（同じ所） | 過去の版専用（ver3.2 と同じ） |
| WorldVer31Checks | 184 合格・7 失敗 | 182 合格・9 失敗 | ver3.2 と同じ 7 件 ＋ 2 件（注 3） |
| WorldVer32Checks | 249 合格 | 242 合格・7 失敗 | 7 件：屋外が first_field だけという前提（注 4） |
| WorldVer33Checks（新規） | — | **122 合格** | — |
| WORLD EDITOR Core Tests | 181 合格 | 181 合格 | —（注 5） |
| Site Editor Tests | 314 合格 | 314 合格 | — |

- **新しい機能の退行: 0 件。** 新しく出た失敗は 20 件で、どれも「屋外の World は first_field だけ」という前提で、数を数えている古い検査です。1 件ずつ原因を確かめました。数はすべて「first_field の分 ＋ 東エリアの分」で説明できます。
- first_field そのものが変わっていないことは、`WorldVer33Checks` の field-look（first_field のセルだけを数える。草の房 200/200・Object 29/29・Collision 273 セル）と、ver3.2 と同じ写り方の 2D 画面（図1b・2b）で確かめています。
- 注 1（Phase 2 の 7 件、Phase 5 の 3 件）: 地面のタイル・草・Object・2D の草の絵を、first_field の Map の範囲だけを前提に数えています。
  - 地面のタイル: 1,827 = first_field と周り 8 セル（37×29 = 1,073）＋ 東エリアの分 754
  - 草の房: 436 = 200（first_field、ver3.2 と同じ）＋ 236（東エリア）
  - 2D の草: 441 = 184（first_field）＋ 257（東エリア）
  - 木・花・石のモデルの数は、first_field の Map JSON の数（4・10・7）と比べています。東エリアの木・花・石が足されたためです。
- 注 2（Phase 4「カラスのとまり木は木の数だけ」）: 描かれた木の数を「4」と決めて数えています。東エリアの木 15 本も描かれるので合いません。
  - カラス自身は、自分の Region（first_field）の木しか選びません（`PocCrow.PerchCandidates` が Region で絞る）。WorldVer33Checks でも、カラスが first_field に住み続けることを確かめています。
- 注 3（ver3.1「セッションは 3 つの Map を遊ぶ」×2）: 今は first_field・家・防音室に東エリアを足した 4 つです。
- 注 4（ver3.2 の 7 件）: Registry が「Region は first_field の 1 つだけ」、開始時のチャンクが 8 個、地面・草・Object・Outline・Collision の数を、World が first_field だけの時の値と比べています。
  - 例: Object 76 = 29（first_field）＋ 47（東エリア）。Collision の不一致 102 は、first_field の東の外側 9 セルの範囲が、今は東エリアの歩けるセルになっている分です。
  - これらの観点は、ver3.3 の検査で「first_field のセルだけ」を数える形に置き換えて、合格しています。
- 注 5: 最初の全体実行では、WORLD EDITOR のテスト「看板の定義は 1 つ」が失敗しました。これは本当の不具合（東エリアの看板の文がゲームで出ない）でした。17章のとおり直して、181 件合格です。
- 古い検査は 1 件も削除・緩和していません。指示書の分類:

| 分類 | 内容 |
|---|---|
| 現在も有効な共通機能テスト | すべて合格（上の合格件数） |
| 過去バージョン専用テスト | Phase 3〜7 の ver3.2 と同じ失敗、Version30Checks・MapAuthoringChecks の停止 |
| 意図的な配置変更による不一致 | ver3.1 の改装・ver3.2 の家具による失敗（ver3.2 と同じ） |
| 新しい機能による前提の変化 | 上の 20 件（東エリアを足したので、first_field だけの数と合わない） |
| 新しい機能退行 | 0 |
| 未実施 | 実機スマホ |
| 実行不能 | なし |

## 41. 画像

付録の図1〜16（`images/`）。指示書 19章の 14 枚との対応:

| 指示書 | 図 |
|---|---|
| 1. 現在のフィールド全体・2D | 図1（全体）・図1b（ゲームの画面） |
| 2. 東エリア追加後の全体・2D | 図2・図2b |
| 3. 現在のフィールド全体・3D | 図3・図3b |
| 4. 東エリア追加後の全体・3D | 図4・図4b |
| 5. 東の道路の接続部分・2D | 図5（ver3.2 の道の終わりは図5a） |
| 6. 東の道路の接続部分・3D | 図6（図6a） |
| 7. 黄色い花の小広場・2D | 図7 |
| 8. 黄色い花の小広場・3D | 図8 |
| 9. 配色案 | 図9 |
| 10. Region 境界デバッグ | 図10 |
| 11. Chunk 境界デバッグ | 図11・11b |
| 12. 東エリアの保存再開画面 | 図12 |
| 13. スマートフォン縦画面 | 図13（エミュレーション） |
| 14. スマートフォン横画面 | 図14（エミュレーション） |

- 「全体」の図（1〜4）は、ゲームの中の説明用のカメラで World 全体を写したものです（ゲームのカメラではありません。2D の草の絵は 2D の画面だけで描くので、全体図には出ません）。
- ゲームの画面そのものは「b」の図（ローカル WebGL）です。

## 42. 動画

`video/HARUKA_WORLD_ver3.3_region_round_trip.mp4`（約 31 秒、1280×720）。**自動操作の録画** です（人の操作ではありません。画面の下にも表示）。

1. 2D で first_field から東へ長押し、境界を越えて東エリアへ（Region 境界のデバッグ線を表示）
2. 広場の前で上を向く
3. 右上のボタンで 3D に
4. 3D で西へ長押し、境界を越えて first_field へ
5. 2D に戻す

## 43. Git Commit

ブランチ `claude/harukaworld-v33`（ローカルだけ。Push していません）:

| Commit | 内容 |
|---|---|
| `3a75905` | 東エリア（east_area_01）を first_field につなぐ（シームレス）・方角の色・新しいモデル・検査 |
| `737ac6e` | 2D カメラが first_field を ver3.2 と同じ位置から写す・全体図の撮影ツール |
| `7b5260e` | 設計資料・回帰テストの実行スクリプト・開発パネルの Region 表示・古い絵の削除 |
| `47526d2` | 東エリアの看板の文が出ない不具合の修正（`signpost_yellow`） |
| （この報告の Commit） | この報告と設計資料の最終版 |

すべての Commit に `Co-Authored-By` を付けています。

## 44. 最終Git状態

- ブランチ `claude/harukaworld-v33`、`origin/main`（`edbb217`）より 5 Commit 先（ローカルだけ）。
- **main への Merge・Push・GitHub Pages の更新・halkaclub.com の更新はしていません。** `origin/main` は `edbb217`（公開 ver3.2）のままです。
- 作業ツリー: 変更なし（Unity の検査が書き換えた生成物・改行だけの変化は、Git の状態に戻しています）。
- 既存データの保護（SHA-256、`logs/sha256_compare.txt`）:
  - 作業前の 1,230 ファイルのうち、1,210 ファイルは同じです。
  - 変わった 18 ファイルは、5章の「変更」のファイル（コード・Registry・カタログ・生成物）です。
  - なくなった 2 ファイルは、シーンの名前変更（`HARUKA_WORLD_ver3.2.unity` とその .meta）です。
- 変わっていないもの（`git diff edbb217` で差分 0）:
  - WORLD EDITOR v1.0（`tools/HalkaWorldMapEditor`）、Site Editor
  - 公開中の ver3.2（`halkaworld/` の 18 ファイル。バックアップとも同じ）
  - 正式 Map（first_field・halka_house・halka_soundbooth）とその Authoring JSON
- ユーザーのブラウザのセーブには触れていません（検査は一時プロファイルとローカルのテスト用 origin だけ）。

## 45. 未解決事項

- 実機のスマートフォンでの確認（表示・操作・性能）: 未実施。
- 東エリアの見た目は、ユーザーの承認前のデザイン案です。
- 戻す場合（ver3.2 に戻す）: 東エリアのセーブは first_field のスタートから始まり、ver3.2 で遊ぶと東エリアの位置は上書きされます（26章）。
- AUTO は Region の外へ出ません。将来「AUTO で地域をまたいで散歩」が必要なら、別の設計が要ります。
- WORLD EDITOR v1.0 は World Registry と方角テーマを読みません。東エリアは 1 枚の Map として編集できますが、隣の first_field と並べて見ることはできません（v1.1 以降の課題）。
- 3D の見通し（40 セル）より遠くは描きません（ver3.2 と同じ）。今の World では全体が見通しの中です。
- 開発パネルの「Map」の行は、屋外では「今いる Region の Map」を出すように変えました（ver3.2 では屋外では常に first_field でした）。開発用の表示だけです。

## 46. ユーザーの目視確認事項

1. 東エリアの見た目（図2・4・7・8、動画）: 木・花の量、黄色の強さ、広場の形。
2. 黄色い道しるべの色と矢印（図5・6）、文「このさき きいろい はなの ひろば。」。
3. ひまわりの群れ（目印）の大きさと見え方（図7・8・16）。
4. 境界を歩いた時に、継ぎ目・暗転・カメラの飛びがないこと（動画、またはローカル版で実際に歩く）。
5. first_field の 2D の写り方が ver3.2 と同じこと（図1b と 2b、3b と 4b）。
6. セーブ画面の場所名「ひがし」。
7. できれば実機のスマートフォンで、東エリアまで歩けること。

ローカル版の起動: `HARUKA_WORLD_ver3.3_LOCAL\start_v33.bat` を開くと `http://127.0.0.1:8934/halkaworld/webgl/` で始まります（公開版とは別の場所・別のセーブです）。

## 47. 技術的完成判定

指示書 23章の完成条件:

| 条件 | 判定 | 根拠 |
|---|---|---|
| 現在の草原をそのまま維持 | ✅ | 草 200/200・Object 29/29・Collision 273 セル・2D の写り方が同じ |
| 東側へ正式な新 Region | ✅ | `east_area_01`、Registry の検証 |
| 東の道から普通に歩いて進める | ✅ | Editor・WebGL 18.1 |
| 新地域から普通に歩いて戻れる | ✅ | Editor・WebGL 18.1/18.2 |
| 境界で暗転なし | ✅ | 遷移・Map 読み込みなし、最長フレーム 16.8ms |
| 境界で Camera Snap なし | ✅ | 1 フレームの最大の動き 2D 0.031・3D 0.039 |
| 境界で Scene 切替なし | ✅ | `MapChanged` なし・mapLoads 変化なし |
| 移動・歩数・Facing が正常 | ✅ | 1 歩 = 1 歩、向き右、10 往復で 20 歩 |
| 2D で新地域が表示 | ✅ | 図2b・5・7 |
| 3D で新地域が表示 | ✅ | 図4・6・8 |
| 黄色が東のテーマとして自然に使われる | ✅（見た目はユーザー確認） | 花・ひまわり・看板。地面は塗らない |
| 北青・東黄・南赤・西緑の対応が定義 | ✅ | `direction_themes.json` |
| Chunk が正常に読み込まれる | ✅ | 先読み・同期読み込み 0 |
| 不要 Chunk が解放される | ✅ | 西端 → 東の広場 → 戻るで解放と作り直し |
| 再読み込みで異常なし | ✅ | 目印は 1 回だけ、二重描画なし |
| Object・Collision が正常 | ✅ | 東の全セルが Map の規則と一致 |
| カラスの既存動作 | ✅ | 1 羽、first_field で普通に動く |
| AUTO が正常 | ✅ | 各 Region の中で 5 分、外へ出ない |
| 家と防音室を維持 | ✅ | 18.4 |
| ver3.2 までの家具を維持 | ✅ | rooms の検査 |
| 朝夜が正常 | ✅ | 1 つの World Time、夜の照明 |
| Life Log が正常 | ✅ | 歩数の一致 |
| 旧セーブが読み込める | ✅ | ver3.0〜3.2 |
| 新地域で保存・再開できる | ✅ | Editor・WebGL 18.3 |

技術面では、指示書の条件をすべて満たしたと判断します。実機スマートフォンの確認と、東エリアの見た目の承認だけが残っています。

## 48. 将来の北・南・西エリアを追加する方法

詳しくは `HARUKA_WORLD_VER33_REGION_CONNECTION_SPEC.md` の 11章（チェックリスト）です。要点:

1. WORLD EDITOR v1.0 で屋外 Map を作る（Player Start・カラス・遷移は置かない）。
2. つなぐ辺に道を通し、境界の列には通行を止める Object を置かない。
3. Offset を「道の World 座標 − 道のローカル座標」と「隣の端 + 1」で決める。
   - 例: 北なら first_field の北端 y = 6 の上、`offset.y = 7 − 新 Map のローカル最小 y`。
4. `outdoor_main.hwworld.json` に `{ "mapId", "offset", "direction": "north" }` を 1 行足す。色は `direction_themes.json` にもうあります。
5. 方角の色の Object は、`build_v33_east.py` と同じやり方で、テーマの色からモデルを作る。ID は `sign_north` などの旧名を避ける。
6. `PocSceneBuilder` で Registry の検証が通ることを確かめ、`WorldVer33Checks` と同じ観点の検査を新 Region に足す。
7. ローカル WebGL で実際に歩いて往復・保存・再開を確かめる。

- 注意: first_field の北・南・西の辺には、今は道が World の外まで続いている所があります（北と南の道）。新しい Region をそこにつなぐ時は、その道の位置に合わせて道を通します。
