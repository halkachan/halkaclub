# HALKA WORLD ver2.7 実装レポート

## 1. 開始時Git状態と保全

- 開始HEAD / `origin/main`: `8b380f582288483eb877d54999ebb5e1f0e7e7d1`。作業ブランチ: `codex/ver2.7-road-landscapes`。
- 開始時の `GrassDecoration.prefab` の改行差と `backup-v05-20261006-174557/` はユーザー既存データとして保持し、今回のコミットに含めない。
- repo外バックアップ: `C:\Users\owner\Documents\ChatGPT\HALKA-v27-backup-20261011`。Git bundle、status/diff、正式Map、Catalog、Scene、Script、Sprite、WebGL一覧、HP HTML、比較画像、検証ログを保存。

## 2. 正式Mapの分析と4方向の道路

`first_field` の範囲は X=-10..10、Y=-6..6。土の道は62セル、既存Objectは18件。Player Spawnは(0,0)、Crow Spawnは(7,2)、Crowの相対巡回範囲はX=-3..1、Y=-2..2。道の終端は北(0,6)、東(10,-1)、南(2,-6)、西(-10,0)。既存Objectは花6、石5、木3、家・ベンチ・看板・井戸が各1件だった。

配置前の全体と各方向はrepo外の `before-local-webgl.png`、`before-map.png`、`before-north.png`、`before-east.png`、`before-south.png`、`before-west.png` に保存した。地形・道・Markerの現行JSONを基準とし、古い仕様書の座標は流用していない。

## 3. 景観設計と追加したObject

| 方向 | Definition | Root Cell | Instance ID | 理由 |
|---|---|---:|---|---|
| 北 | `tree_basic` | (-4,3) | `obj_tree_north_v27_01` | 既存の木と合わせて森へ続く密度を出し、道は開けておく |
| 東 | `flower_basic` | (4,-2) | `obj_flower_east_v27_01` | 道の南に花を置いて明るさを加える |
| 東 | `flower_basic` | (6,-3) | `obj_flower_east_v27_02` | 他の花のAction Pointを避けつつ間隔に変化を付ける |
| 東 | `flower_basic` | (9,1) | `obj_flower_east_v27_03` | 東の終端近くでも花道と分かるようにする |
| 南 | `stone_basic` | (-1,-5) | `obj_stone_south_v27_01` | 南道の西側に点在させる |
| 南 | `stone_basic` | (4,-5) | `obj_stone_south_v27_02` | 道の東側にも離して配置し、石の壁にしない |
| 西 | `flower_basic` | (-9,1) | `obj_flower_west_v27_01` | 出口近くの小さな目印に留め、草地の余白を保つ |

合計7件。既存のTree/Flower/Stone DefinitionとSpriteを再利用し、新種・画像加工・草システムの変更はしていない。追加ObjectのRootは土の道、Player/Crow Spawn、Crow巡回矩形(X=4..8、Y=0..4)を避ける。木のVisual Footprint、各Blocked Footprint、隣接ObjectのAction Pointを確認した。中央ジャンクションと各Road Endは空けた。ユーザーがMAP EDITORで動かす際は、道・出口・他Objectの操作セルを避けること。

## 4. 経路、Crow、AUTO、既存要素

Blocked Footprintを用いた4方向BFSで、Player Spawnから北6、東11、南8、西10セルの経路を確認。House入口12、Bench左3/右2、Sign3、Well5セルも到達可能。新規Objectの各Action Pointも到達可能で、Map内に収まる。既存のHouse、Bench、Sign、Well、木、花、石、道路、Marker、Entity Spawn、Area Transitionは動かしていない。Crow巡回矩形への新規配置はない。AUTOの経路探索・障害物判定には既存処理を使用し、コードは変更していない。

## 5. Map JSON差分とMAP EDITOR

正式な変更は `first_field.hwmap.json` の `objects` 末尾への7件追加のみ。変更前18件は同値・同順、`objects` 以外の全フィールドは意味的に同値。`halka_house.hwmap.json` はbyte単位で一致。Map JSON v4、MAP EDITOR v0.6、エリア遷移0件を維持した。

MAP EDITOR v0.6で正式Mapの**コピー**を開き、25 Objectと新しい木・花・石の表示、個別選択を確認。西の花のRootをコピー上だけ(-9,1)→(-9,2)へ移動し、Undoで元へ、Redoで再移動、保存、Map切替による再読込で(-9,2)の保持を確認した。Editorの検証表示は「問題なし」。正式Mapでは(-9,1)のままであることを別途JSONで確認した。画面証拠はrepo外の `map-editor-v27-reloaded.png`。

## 6. 自動検証

- `Version27Checks.Run`: 合格。`Version26Checks.Run`以下を継承し、GameVersion 2.7、Map v4、定義・Instance ID・配置、4テーマ、Spawn、Crow巡回、Road End、House/Bench/Sign/Well経路、Point/無圧縮/MipMap Off/PPU64を検証。ログ: repo外 `version27-checks-final.log`。
- `MapAuthoringChecks.Run`: 最終Mapで合格。ログ: repo外 `map-authoring-checks-final.log`。
- MAP EDITOR Standalone Core Tests: **181件合格**、正式Mapの不書換えも合格。ログ: repo外 `map-editor-core-tests.txt`。
- `tree.png`、`flower.png`、`stone.png` の既存Pixel Art Import設定を維持。Player Sprite、CharacterVisual、PlayerMover、Camera、GameBitmapFont、WebGL表示CSSは未変更。

## 7. 実画面比較とローカルWebGL

Production WebGLの通常表示で、北は木、東は花、南は石、西は草地の余白として違いを確認。比較画像はrepo外の `after-local-webgl.png`、`after-map.png`、`after-north.png`、`after-east.png`、`after-south.png`、`after-west.png`。地図全体が不自然に過密になっていないことを確認した。

ローカルChrome上の正式Mapで `ver2.7` を確認し、Playerを操作して北・東・南・西の4つのRoad Endへ到達した。左ベンチの着席と立ち上がり、看板クリックによる「ここは HALKA WORLD。」、MenuとLife Log、Crowの移動を確認した。看板操作時のスクリーンショットでは既存Message UIの表示を確認した。右ベンチ・井戸・家の往復はこのWebGL操作では最後まで確認できていない。これらは継承したVersion25/26のRuntime検証では合格したが、実ブラウザ操作済みとは扱わない。

390×844、`touchControls=1` でD-pad、Aボタン、Menu、Life Logの表示とTapを確認。MenuとLife Logは画面内に収まり、文字も読める。D-padでの移動は確認した。Aボタン経由の個別Object操作と4終端の全往復はモバイルプレビューでは未確認。画面証拠はrepo外 `mobile-v27-lifelog.png`。物理スマホは未確認。

Player正面・移動・ベンチ着席の見た目に明らかな変化は見られない。描画関連ソースとアセットは変更していない。遊びメモ書きも維持。

## 8. Version、Build、HP

GameVersion、Unity productVersion、WebGL表示、HPの現在Versionを2.7へ更新。更新履歴の上Archive最上段に `ver2.7` / `2026-10-11` / 「4方向の道の景観を整えました。」を追加した。下Archiveと既存Entryは維持し、両Archiveに`open`属性はない。

`ProjectBuilder.BuildWeb` によるProduction WebGL Buildは成功。`BuildOptions.None`、Unity Splash/Logo Off、`stripEngineCode=false`を維持。新しい `.data` とWebGL indexを生成し、Player/Font/Camera/CSSの設定は変更していない。ログはrepo外 `production-build.log`。

## 9. GitHub公開と残る確認

実装・正式Map・Build・Reportを `5212bfbba28cc0bfca0f70ebcef9d0c7ec55fd00` としてコミットし、検証範囲の記録を `514a67ae41fdc986f81c4bc32d32495045914762` で追記した。公開前の差分確認でユーザー既存の `GrassDecoration.prefab` 改行差と `backup-v05-20261006-174557/` をコミットから除外した。`origin/main` へ通常Push済み。GitHub Pagesの最新ビルドは同コミットで `built`、2026-10-11 14:06:10 UTCに完了した。force pushは行っていない。

公開HPのChrome実表示で `ver2.7`、上Archiveの `ver2.7 / 2026-10-11 / 4方向の道の景観を整えました。`、下Archiveの初期Closedを確認。公開WebGLはロード完了し、北の木、東の花、南の石、西の余白を実画面で確認した。公開WebGLで右方向へPlayerを1歩操作し、移動を確認した。公開HP、WebGL、`.wasm`、`.data` のHTTP HEADはすべて200。証拠画像はrepo外 `public-v27-page.png`。公開環境で4方向のRoad Endまでの歩行は行っておらず、その項目はローカルProduction WebGLの実操作で確認した。

最終作業ツリーでは今回の変更はコミット済みで、残るのは作業開始前から存在した `unity-project/Assets/Content/World/GrassDecoration.prefab` の改行差と `backup-v05-20261006-174557/` のみ。これらを削除・コミットしていない。

残る手動確認は右ベンチ着席、井戸、House入退室、スマホAボタン操作、物理スマホ。次の候補はロードマップ上のver2.8だが、今回実装していない。
