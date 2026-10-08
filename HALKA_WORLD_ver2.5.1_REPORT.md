# HALKA WORLD ver2.5.1 描画品質修正レポート

作業日: 2026-10-09。本編は ver2.5.1、MAP EDITOR は v0.6、Map JSON は v4 のまま。

## 1. 開始状態と保全

- 開始時の HEAD / origin/main: `9a1fbbff14583a7855c76d4b21caaa2dc987463b`。作業ブランチ: `codex/ver2.5.1-pixel-quality`。
- 開始時から `unity-project/Assets/Content/World/GrassDecoration.prefab` に改行差分と、未追跡の `backup-v05-20261006-174557/` があった。どちらも今回のコミット対象から除外する。
- リポジトリ外の保全先: `C:\Users\owner\Documents\ChatGPT\HALKA-v251-backup-20261009`。Git bundle、開始時 status / HEAD、正式Map、Scene、描画関連のScriptとSprite、HP HTML、WebGLファイル一覧を保存した。
- 正式Mapの SHA-256 は作業前後で一致。`first_field.hwmap.json`: `4B61FB227DD3C72F54DF6C1D58DB121CA34A3676D41CC7EA5F9948D3B9A41864`、`halka_house.hwmap.json`: `CE451E4B5E7380B5E99F1BCB148E265FBACD9041F3512C8F7E91BF7F6174448E`。両ファイルの意味的差分もない。ver2.5で配置したベンチ、看板、クッション、机を維持した。

## 2. Sprite自体の劣化

| 調査箇所 | 結果 |
| --- | --- |
| 正面立ち `front_idle/00.png` | 64×64 RGBA、Alpha は 0/255、22 色。青い目と目の下のピンク `(254,27,109,255)` が4ピクセル存在。 |
| 修正前 `bench_sit.png` | 64×64 RGBA、Alpha は 0/255、25 色。赤・ピンク系の目元ピクセルは **0**。 |
| 座り絵生成元 | `SourceGeneratedArt/v24/candidates/candidate_C_source.png` には目の下のピンクが見える。採用済み64×64候補にはない。縮小・整理段階で失われたと判断。 |
| Unity Import | 座り・立ちとも Point / PPU64 / mipmapなし / 非圧縮。PNGから色が失われる設定は確認されなかった。 |

承認済みの座りポーズを保ち、`bench_sit.png` の顔の **4ピクセルのみ** `(254,27,109,255)` に補正した。Pillowの左上原点で `(23,31)`, `(24,31)`, `(32,31)`, `(33,31)`。髪、服、体格、足、Alphaは変更していない。新しい画像生成は行っていない。修正後PNGの SHA-256 は `78139B65D35F3F016E7594EB910C9B940B5A6536785BA9778DE96051E2E8B4FF`。

保存した原寸・最近傍16倍比較: 保全先の `standing_64.png`、`seated_before_64.png`、`bench_sit_candidate_64.png`、`standing_face16x.png`、`seated_before_face16x.png`、`seated_candidate_face16x.png`、`browser-bench-face-before-after.png`。実ゲームの同じ右座席の比較は `before-bench-right-direct.png` と `after-bench-right-direct.png`、左座席は `after-bench-left-direct.png`。座り中の目元のピンクを実画面で確認した。

## 3. Runtime表示経路と原因

- 実行中に使用するPlayer、Crow、Grass、Dirt、Tree、House、Bench、Sign等を含む67枚のImport監査結果は保全先の `sprite-import-audit.csv`。使用中の対象Spriteは Sprite/Single、PPU64、Point、mipmap OFF、非圧縮、Atlasなし。未使用の `front_jump` / `turn` の一部には異なる設定があるが、今回のRuntime描画経路に入らないため変更しなかった。
- 論理セル32 Sprite px = 0.5 Unity Unit、Player64×64 px / PPU64。屋外Cameraは orthographic size 4.5、RenderTextureなし。屋外縦方向の原画相当は `2 × 4.5 × 64 = 576` px。着席の描画Offset `10/64` Unit は原画10 pxであり、これ自体は劣化原因ではない。論理座標、Collision、補間移動、Camera boundsは変更していない。
- 変更前のブラウザ表示では直接1280×720 Canvasで1 Sprite pxが1.25 Device px。HPの1280幅表示では iframe内 Canvas が約892×636、DPR1で約1.104 Device px。これらは非整数倍率。スマホHPの縦表示は Canvas 約291×570で約0.99 Device pxとなり、1px未満の情報は完全には表示できない。
- HP親ページ、iframe、WebGL HTML、Canvas CSSを調査。親iframeと内部Canvasに追加の CSS transform はなく、二重拡大は確認しなかった。Canvasのbacking storeとCSS表示サイズは変更前は一致していた。Browser zoom 100% / DPR1で実測した。DPR2の実機表示は未検証。
- 原因の切り分け: 座り目元は**元の採用PNGで既に消失**。立ち絵や草・木・家の見え方は**非整数倍率の表示補間**が寄与。Unity Import後・WebGL Render後・HP埋め込み後に座り絵の赤ピクセルだけが消えた、という事象ではない。PNGにないため各段階での消失とは判定しない。

## 4. 比較した方式と採用修正

| 方式 | 確認と判断 |
| --- | --- |
| A 旧WebGL | 同じ座席とHP表示をスクリーンショット保存。座りの目元ピンクなし。背景の輪郭に補間の混色。 |
| B Sprite Importのみ | Runtime SpriteのPoint、mipmap OFF、非圧縮は既に設定済み。設定変更なし。 |
| C Camera Pixel Perfect | Unity 6.3の Pixel Perfect Camera を検討。Camera size 4.5 と室内3.4を自動的に上書きしうるため、画面構図・境界・遷移への影響が大きい。Packageは採用せず、Cameraも変更しない。 |
| D WebGL Canvas | Desktopでは屋外の原画高576 pxの整数倍へ内部描画高を寄せ、Canvasの CSS 拡大に `image-rendering: pixelated` を適用。旧表示・bilinear CSS版・採用版を実ブラウザで比較し、輪郭と立ち絵の目元を確認。 |
| E 低解像度World分離Render | WorldとIMGUIの描画経路を分ける大幅改修となるため採用しない。 |

Source of Truth は `unity-project/Assets/WebGLTemplates/MinimalNoBrand/index.html`。Unity WebGLの `devicePixelRatio` をCanvasの表示高から計算し、Desktop横長・幅800px以上では原画576pxの整数倍に近いbacking storeへ調整。ResizeObserverでリサイズ後も再計算する。幅800px未満と縦長では元のDPRを使い、D-pad/Aの大きさと配置を保持する。Canvasには最近傍CSSを使用。遊びメモ書きのBitmap Font自体は変更していない。DesktopとMobileのMenu / Life Log文字が読めることを実画面で確認した。

実測: 直接Desktop 1280×720は採用後 backing 1024×576、HP Desktopは CSS約892×636 / backing約808×576。HP Mobile縦は CSS/backing約291×570、直接 touchControls=1 の390×844は390×844、横844×390は844×390。Mobileは整数倍化を適用していない。Resize直後の一時的な中間フレームを除き、再計算後の表示・操作は正常だった。

Desktopの比較資料は保全先の `trial-original-direct.png`、`trial-virtual576-bilinear-direct.png`、`trial-virtual576-direct.png`、`before-hp-desktop.png`、`after-hp-desktop.png`、`hp-world-before-after.png`。目元の最近傍拡大は `trial-original-direct-face16x.png`、`trial-virtual576-direct-face16x.png`。Mobileは `before-hp-mobile-390.png`、`after-hp-mobile-portrait.png`、`hp-mobile-before-after.png`、`after-mobile-touch-portrait-final.png`、`after-mobile-touch-landscape-final.png`。Desktop通常サイズで草、木、家、Playerを確認。Mobileでは1原画pxが1 Device px未満になる状況があり、すべての微細情報が必ず見えるとは主張しない。物理スマートフォンは未確認。

## 5. 回帰、Build、公開

- `Version251Checks.Run`: **成功**。`Version25Checks.Run` 以下も全て通過。Import、Atlas、Camera構図、576px投影、座りOffset、立ち・座り64×64、目元の色、WebGL Templateを検証。ログ: 保全先の `version251-check-final.log`。
- `MapAuthoringChecks.Run`: **成功**。ログ: `map-authoring-check.log`。Standalone Core Tests: **181/181成功**。MAP EDITOR v0.6とMap JSON v4を維持。
- `ProjectBuilder.BuildWeb`: **成功**。Development Build OFF、Unity Splash/Logo OFF、`stripEngineCode=false`。最終ログ: `webgl-build-mobile-fix.log`。生成された `halkaworld/webgl/index.html` は productVersion 2.5.1、`.data` は `096e1f271bd14a68178e9a2dd636e33f.data` (5,048,772 bytes)、`.wasm` は24,143,653 bytes。
- ローカルProduction WebGL: Desktopの立ち・右/左着席、HP埋め込み、Mobile縦/横、touch D-pad、Menu、Life Logを実ブラウザで確認。ベンチの顔のピンク、草・木・家の輪郭、文字の可読性を確認。通常歩行と既存機能の網羅的な手動再操作は実施していないため、他のGameplay回帰はVersion251Checksの自動結果に基づく。
- HP履歴は上Archiveに `ver2.5.1 / 2026-10-09 / ドット絵の表示品質を改善しました。` を追加。旧履歴と下Archiveは維持。
- 実装コミット: `bead8aca51770f44afd138da5f38a2b7ef1e41cc`。最新 `origin/main` が開始時と同一であることを再確認し、ローカルmainへfast-forward統合、通常pushした。force pushは使用していない。
- GitHub Pagesの実装コミットに対する [deployment run](https://github.com/halkachan/halkaclub/actions/runs/37855184668) は **success**。公開URL: https://halkaclub.com/halkaworld/ 。HP、WebGL、更新後 `.data`、`.wasm` はそれぞれ HTTP 200。
- 公開HPを実ブラウザで読み込み、`ver2.5.1`、上Archiveの ver2.5.1→2.1 の順序、両Archiveの初期Closedを確認した。公開WebGLの起動・屋外描画・GameVersionを確認。直接1280×720表示では backing 1024×576、CSS `pixelated`。Playerをベンチ右Action Pointへ歩かせ、実際に着席・立ち上がりを操作した。着席時の目元のピンクを公開表示で確認した。証拠は保全先の `public-hp.png`、`public-bench-right.png`、`public-bench-right-face16x.png`。
- 公開WebGLのスマートフォン実機、および公開環境での屋内・全Gameplayの手動再操作は未確認。ローカルと自動Regressionの結果を前述の範囲で確認した。
- レポートコミット: `93b51e09bd04ebed2cc8bf602b2c3d170aab0d01`。通常push済みで、対応する [Pages deployment run](https://github.com/halkachan/halkaclub/actions/runs/37855464088) も **success**。
- レポートコミット・push後の `git status --short`: ` M unity-project/Assets/Content/World/GrassDecoration.prefab` と `?? backup-v05-20261006-174557/` のみ。どちらも開始時からのユーザー側の差分として保持し、コミットしていない。正式Mapと生成Sceneの差分はない。

## 6. 未確認事項

物理スマートフォン、DPR2端末での表示、全ブラウザ・全解像度での1px再現は未確認。公開WebGLの右座席は実操作済み。左座席はローカルWebGLで確認した。
