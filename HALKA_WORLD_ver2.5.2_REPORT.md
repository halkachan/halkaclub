# HALKA WORLD ver2.5.2 描画品質修正レポート

公開判断日: 2026-10-10。対象は本編のPlayer描画と「遊びメモ書き」Bitmap UI。MAP EDITOR v0.6、Map JSON v4、Gameplay、正式Map配置は維持した。

## 1. 開始状態と保全

- 開始HEAD / origin/main: `7129f455e15635cc0b3bd9a66d79e0720be5c359`、本編ver2.5.1。
- 既存の未コミット `GrassDecoration.prefab` と未追跡 `backup-v05-20261006-174557/` はユーザーの状態として保護し、今回のコミット対象から除外する。
- repo外の `C:\Users\owner\Documents\ChatGPT\HALKA-v252-backup-20261009` にGit bundle、開始時status/diff、正式Map、Scene、Scripts、旧Build、HP、入力画像・動画と連続フレームの比較資料を保全した。
- 正式Map SHA-256は前後一致: `first_field` = `4B61FB227DD3C72F54DF6C1D58DB121CA34A3676D41CC7EA5F9948D3B9A41864`、`halka_house` = `CE451E4B5E7380B5E99F1BCB148E265FBACD9041F3512C8F7E91BF7F6174448E`。JSONのGit差分もない。

## 2. Animation frameと顔の調査

- 正面/背面/左右のIdle、Walk、`bench_sit.png`をPNGのRGBA画素で検査した。正面の赤・ピンクの目元は各正面コマに存在し、立ち絵と承認済み座りSpriteを変更していない。
- 正面Idleの奇偶コマは頭部全体がソースで2pxずれ、Walk正面は1pxずれる。平行移動して比較すると正面の顔画素は一致する。停止中でもアニメーションによる縦揺れが起こる主因だった。左/右も目元の高さ差があり、背面には目元がない。`front-frame-contact.png` を参照。
- 元絵の顔を再生成せず、見た目の子Transformへコマごとの補正を適用した。着席中は補正0で、ベンチ座りポーズと足元マスクを維持した。

## 3. Subpixel、Camera、Canvasの切り分け

- `PlayerMover`は1セルを0.18秒で補間し、途中のworld座標は端数となる。`CharacterVisual`のコマ切替は移動0.15秒、停止0.4秒。論理セルと描画位置は別に記録した。
- 屋外Cameraはorthographic size 4.5、SpriteはPPU64。高さ576のWebGL backingでは1 Sprite px = 1 backing px。Desktop 1280×720ではbacking1024×576からCSS1280×720へ1.25倍表示。HP内ではiframeにもレスポンシブなサイズ変化がある。DPR、画面幅、ブラウザ倍率によって最終device pixel倍率は一定ではない。
- Cameraだけの丸めは現在の固定Camera位置で効果がなく、導入を取り消した。720px native backingへの変更はキャラクター・UIが約20%小さくなったため取り消した。576px基準と既存CSSは維持し、画面構図を保った。
- `PlayerRenderSnap2D`はCamera投影後の**描画専用子Object**をbacking pixel格子へ揃える。頭部コマ補正はその後で適用し、720px等で1 Sprite pxが1.25画面pxとなる場合も補正量を再丸めしない。PlayerのCell、Transform、Collision、StepCompleted、Life Log、AUTO、Action Point、Camera boundsは変更しない。
- CUAのスクリーンショットはJPEGになるため、比較は診断ページでCanvasから直接取得した無圧縮PNGを使用した。診断ページはリポジトリへ含めない。

## 4. Playerの修正前後

- 修正前の停止正面8フレームではピンク画素の中心Yが `47,49,49,47,47,49,49,47` と2px振れた。同条件の修正後8フレームはすべて `48`。ピンク画素数はいずれも4。実際の顔を最近傍8倍で比較した資料は `player-idle-before-after-nearest8x.png`。
- 移動中のCanvas PNGも取得した。左移動後の修正前は同じセルに停止してからピンク画素Yが300→302と揺れたが、修正後の同条件では269で安定した。座標の絶対値差はゲーム内の開始位置が違うためで、比較対象はセル内の時系列変動。
- 上下左右の実操作、左右ベンチ着席と立ち上がりをローカルProduction WebGLで確認した。顔色は失われず、足元マスクも一緒に移動する。動画圧縮画像だけで判定していない。

## 5. Bitmap Fontの原因と比較

- 旧`asobi_ui_atlas.png`は2048×640、313グリフ、50px原寸/64pxセル。2px stroke、Bilinear sampling、IMGUIの端数配置で描画。元Atlasには中間alphaが51,362画素あり、透明合成と補間で黒い文字の縁に灰色が出た。隣接グリフの混入や不正なRGBは主因ではない。
- Strokeなし、1px stroke+alpha sharpen、2px stroke+二値alpha、Bilinear/Pointを同じ表示サイズで比較した。1px方式は文字が細くなり可読性を落としたため不採用。全面的なCanvasのnative解像度化もUIを小さくしたため不採用。
- 採用方式は既存の**2px stroke**を維持し、alphaを128閾値で0/255に整理、AtlasをPoint/非圧縮/mipmapなしで取り込み、IMGUIの描画矩形を整数画面座標へ揃える。新Atlasは透明/不透明の2色、半透明alpha0画素、不透明ink123,904画素。元の手書き形と読みやすい太さを保つ。
- 1280×720の実Canvas PNGで、Menu文字領域の黒/中間灰画素は旧 `298/121`、採用版 `330/0`。看板Message領域は旧 `1767/687`、採用版 `2023/0`。これは測定した選択領域内の値で、WebGLの全画面に灰色がないという意味ではない。比較画像は `font-final-before-after-css.png`、`font-binary-vs-old.png`、生PNGは `raw-baseline/`・`raw-canvas/` にある。
- `GameBitmapFont`と既存k8x12L fallbackを維持。動的看板文字でAtlasにない文字は従来のfallbackに進む。Raw TTF/OTFはリポジトリにもWebGL Buildにも追加していない。Atlasの再生成スクリプトだけを更新し、公式Font ZIPは私有の一時領域で使用した。

## 6. 実画面と回帰

- Desktop 1280×720、狭いDesktop 720×600、HP iframe埋め込み、Mobile縦390×844・横844×390をローカルProduction WebGLで目視。初回起動とリサイズ後を確認した。Menu、AUTO、生活記録、看板Message、D-pad、Aは表示範囲内。旧版と修正版を同じ390×844でCanvas PNG比較した `mobile-portrait-before-after.png` を保全した。物理スマホは未確認。
- 正式Mapで上下左右の移動、ベンチ左/右着席・立ち上がり、着席中Menu、看板「ここは HALKA WORLD。」、Crow表示・移動を実操作した。座りSprite、家/草/道の見た目、手書きFontの可読性を確認。家の入退室とAUTOの長時間実操作は今回のブラウザセッションでは未確認で、継承回帰チェックの対象。
- `Version252Checks.Run` **成功**。内部で `Version251Checks.Run` 以下を実行し、Sprite Import、正面/座り目元、Camera、ベンチ・看板・家具・Menu・Life Log等を継承検証。追加でPlayer描画子階層、草マスク、AtlasのPoint/非圧縮/mipmapなし、PNG alpha、主要文字とfallback経路、GameVersionを検証。ログは保全先 `version252-check.log`。
- `MapAuthoringChecks.Run` **成功** (`map-authoring-check.log`)。Standalone Core Tests **181/181成功**。MAP EDITOR v0.6とMap JSON v4は変更なし。
- `ProjectBuilder.BuildWeb` **成功** (`webgl-build-final.log`)。Development Build OFF、Unity Splash/Logo OFF、`stripEngineCode=false`。productVersion2.5.2。生成Buildは `.data` 4,924,650 bytes、`.wasm` 24,146,906 bytes。GameVersionとHP上Archiveをver2.5.2へ更新し、2026-10-10の履歴を最上段に追加した。

## 7. 公開・残る問題

- 実装Commit `474567411f94a3bbac45d4b727a5f5b84daf9846` をmainへfast-forwardし、通常push済み。force pushなし。GitHub Pagesのrun [37951774318](https://github.com/halkachan/halkaclub/actions/runs/37951774318) はbuild/deployとも成功。
- 公開URL [https://halkaclub.com/halkaworld/](https://halkaclub.com/halkaworld/) でver2.5.2の起動、歩行、看板文章、ベンチ着席、Menuの文字を実操作で確認。HP Archiveは旧項目を残してver2.5.2を最上段に追加。公開HP、WebGL、`.data`、`.wasm`はHTTP 200。クリック操作の画面で、灰色のにじみ軽減とPlayerの色の保持を確認した。
- 最終git statusは作業前から存在した `GrassDecoration.prefab` の変更と未追跡 `backup-v05-20261006-174557/` のみ。今回の実装・公開記録はすべてCommit対象で、診断用HTMLは削除した。
- 576px backingを非整数倍率で表示する画面では、最終device pixelへの厳密な1:1整列は保証できない。今回の比較では顔のコマ揺れと文字の灰色縁は改善した。物理スマホ、異なるDPR機器、長時間AUTOは未検証。
- 比較資料の保存先: `C:\Users\owner\Documents\ChatGPT\HALKA-v252-backup-20261009`。ユーザーの既存未コミットPrefabとBackupは今回の変更から除外した。
