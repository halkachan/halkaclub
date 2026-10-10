# HALKA WORLD ver2.5.4 実装レポート

## 1. 開始状態・保全

- 開始時 HEAD / origin/main: `c4a6423b14b2b40f9d0cccf50c588bd725fa3093`。
- 開始時の既存変更: `unity-project/Assets/Content/World/GrassDecoration.prefab`（改行差）、未追跡 `backup-v05-20261006-174557/`。両方とも今回のコミット対象から除外する。
- 作業ブランチ: `codex/ver2.5.4-sprite-fidelity`。
- repo外バックアップ: `C:\Users\owner\Documents\ChatGPT\HALKA-v254-backup-20261010-123956`。Git bundle（194,674,376 bytes）、開始時status/diff、Character Sprite、Scene、関連Script、旧WebGLとHP、ユーザー添付画像、比較画像、実行ログを保存した。
- 正式Map SHA-256: `first_field` = `4B61FB227DD3C72F54DF6C1D58DB121CA34A3676D41CC7EA5F9948D3B9A41864`、`halka_house` = `CE451E4B5E7380B5E99F1BCB148E265FBACD9041F3512C8F7E91BF7F6174448E`。Map JSONは変更しない。

## 2. 元PNGとUnity取込

- `bench_sit.png`: 64×64 RGBA、Alphaは0/255、非透明領域 `(10,5)-(53,63)`、不透明色25色。SHA-256 `78139b65d35f3f016e7594eb910c9b940b5a6536785ba9778de96051e2e8b4ff`。
- `front/back/left/right_idle`、4方向のwalk、`bench_sit` の実使用計25枚を監査。`PlayerSpriteImportAudit.Run` が、元PNGとUnity取込後のテクスチャで非透明36,640画素のRGBA、全透明画素のAlpha一致を確認した。PNGファイル自体は変更していない。現在未使用の `front_jump` と `turn` のmetaは変更しなかった。
- 既存metaでは25枚とも `spriteMeshType: 1`（Tight）。今回Player Spriteのみ `spriteMeshType: 0`（Full Rect）に統一した。共通条件は Sprite/Single、PPU64、Point、MipMapなし、圧縮なし、sRGB有効、Clamp、WebGL overrideなし、64×64。World SpriteのImport設定は維持。
- SpriteAtlas assetは存在せず、Packing、Atlas override、隣接SpriteのUVにじみは今回の経路にない。

## 3. Runtimeの表示経路

- `CharacterVisual` は通常・着席とも同一の `SpriteRenderer` を使用する。Sceneの `benchSit` 参照GUIDは元 `bench_sit.png` のGUIDと一致する。
- `Player artwork` のScaleは `(1,1,1)`、Rotationは0、Renderer色は白、flipなし、Default Sprites Material。着席は `PlayerSeatController` によりSpriteと表示Offsetだけが変わる。Spriteの縮小、再着色、別Material差し替えはない。
- 草足元マスクは子階層にあり、着席中は無効。顔と胴体を隠す設計ではない。`Version254Checks` は着席・立ち上がりのSprite切替、Scale、Rotation、Mask、Materialを左右両席で検査する。
- `PlayerMover`、`CharacterVisual` のフレーム周期、移動補間、`PlayerSeatController` の動作、Camera追従、Sprite PNG、遊びメモ書きFont Atlasは変更していない。

## 4. ブラウザでの見え方と原因切り分け

- 公開ver2.5.3のPC HP埋め込みでは、iframe表示が約892×636 CSS px、WebGL Canvas内部が808×576 px、DPR1だった。1 Sprite pxが約1.104 CSS pxとなり、画面上で1px/2pxの不均等な幅になった。
- ユーザー添付の無劣化PNGを元Spriteの最近傍拡大と照合すると、座りSpriteの対応領域1,798画素中1,408画素は同位置同色、残り390画素の色も隣接1画素以内に存在した。色の消失より拡大・位置サンプリングの影響と整合する。ただしスクリーンショット1枚からUnity内部の各段階の寄与率は断定できない。
- repo外の旧Buildを使った試作で、HP埋め込み内Canvasを807×576 CSS px、内部807×576 pxにそろえ、中央に配置した。完成Buildも同寸法を確認。直接開いた1280×720画面では1024×576 CSS px＝内部1024×576 px。Canvasの `image-rendering: pixelated` と従来の576px基準を維持した。
- 採用変更: PlayerのみFull Rect import、および大きい横長画面のCanvasを576px高・CSSとBacking Store 1:1に合わせる。UnityのCamera、PPU、移動方式、Font描画はそのまま。
- ブラウザのScreenshot APIはJPEGを返すため、実表示の完全な画素一致はスクリーンショットからは証明できない。元PNGとUnity取込の画素一致はUnity Editor診断で検証した。狭い横画面では1元Sprite pxが1画面px未満になり得る制約が残る。

## 5. 比較画像

比較資料は上記repo外バックアップに保存。`user-bench-before.png` はユーザー添付、`before-public-seated.jpg` は公開ver2.5.3、`preview-one-to-one-seated.jpg` は旧Buildの倍率のみ変更した試作、`local-v254-direct-seated.jpg` と `local-v254-left-seated.jpg` は完成Production Buildの左右着席、`local-v254-hp-seated-final.jpg` は完成BuildのHP埋め込み着席、`local-v254-front-player.jpg`・`local-v254-horizontal-player.jpg`・`local-v254-back-player.jpg` は通常Sprite、`source-vs-user-game-8x.png` と `bench-original-before-preview-8x.png` は最近傍拡大比較。通常Playerの上下左右移動をローカルWebGLで目視した。ブラウザ取得画像がJPEGである限界は上記の通り。

## 6. Version254Checks と回帰

- `Version254Checks.Run` は `Version253Checks.Run` を呼び、GameVersion、25枚の取込設定、Atlas不在、元Sprite参照、着席時同一RendererとTransform、左右着席と復元、Fontを含む既存回帰を確認する。
- `MapAuthoringChecks.Run`: `HALKA WORLD Standalone Map Contract v0.6: passed.`
- MAP EDITOR Core Tests: 181/181 passed。
- 正式Mapの配置・Schema v4、MAP EDITOR v0.6、Gameplay Script、Font Atlasは変更していない。
- `ProjectBuilder.BuildWeb` が再生成したSceneはファイルIDの不要な大量差分を生むため、ビルド後に元のScene sourceへ戻した。配布WebGLには再生成済みSceneが含まれる。Sceneの生成元はMap JSONとProjectBuilderである。
- 最終Build後の `PlayerSpriteImportAudit.Run` と `Version254Checks.Run` も成功。`MapAuthoringChecks.Run` は最終Build後も成功。`git diff --check` は0。

## 7. Production Build・ローカル実操作

- Unity `ProjectBuilder.BuildWeb` により非Development WebGLを生成。Unity Splash/Logo OFF、`stripEngineCode=false` を維持。`halkaworld/webgl/index.html` にproductVersion `2.5.4` を確認。
- ローカルWebGLでver2.5.4、正面・背面・左右の通常Sprite、移動、ベンチ左右着席・立ち上がり、メニュー、生活記録を確認。HP埋め込みCanvasは807×576 CSS/Backing、直接WebGLは1024×576 CSS/Backing（DPR1）。390×844縦、844×390横でタッチUIと文字表示を確認した。
- Font AtlasおよびFallbackを変更していない。メニューと生活記録の表示を目視し、文字品質の退行を認めなかった。物理スマホは未確認。

## 8. HP・Git・公開

- HP現在表示を `ver2.5.4`、上Archive最上段に2026-10-10「キャラクター画像の表示を調整しました。」を追加。Build参照は新Build hash `6972507e2873658666a6fa559835de29` へ更新。
- 実装コミット: `00754063034d2f8dca7a6cde8761940500f18af9`。最新origin/mainと同じ開始点からfast-forward統合し、`git push origin main` で通常pushした。force pushなし。
- GitHub Pages workflow `https://github.com/halkachan/halkaclub/actions/runs/38022863032` はhead `0075406` でsuccess、Pages statusはbuilt。公開 `.data` はHTTP 200。
- 公開 `https://halkaclub.com/halkaworld/` でver2.5.4、上Archive先頭の更新文、WebGL起動、807×576 CSS/Backing一致を確認。公開WebGLを直接操作して右座席へ着席し、元 `bench_sit.png` の見た目、立ち上がりと元Sprite復元、遊びメモ書きMenu表示を確認。画像はrepo外の `public-v254-seated.jpg` と `public-v254-menu.jpg`。公開HP内の座り表示もローカル完成Buildで操作確認済み。
- REPORTコミット後も、開始時からの `GrassDecoration.prefab` 改行差と `backup-v05-20261006-174557/` を変更・コミットしない。
- REPORT作成中にorigin/mainへ別件のアクセス解析追加とSite Editorテスト更新が入った。両コミットを通常mergeで保護し、Site Editor Tests 304/304 passed、`git diff --check` 0を確認した。ver2.5.4の実装・Map・Fontへの競合変更はない。
- 最終作業ツリーに残るのは開始時からの `M unity-project/Assets/Content/World/GrassDecoration.prefab` と `?? backup-v05-20261006-174557/` のみ。prefabのSHA-256は開始前後とも `AF2C0D69096B25658261B299BB7A8173DEBEC1B94BD599339615359E92857D09`。これらはステージ・コミットしていない。

## 9. 残課題

- ブラウザのJPEGスクリーンショットでは、最終WebGLの各画素が元PNGと数学的に完全一致するとは証明できない。
- 576pxより低い横画面では元Spriteを1:1で表示する画素数が不足する。既存のレスポンシブ構図を優先した。
