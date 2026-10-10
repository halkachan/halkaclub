# HALKA WORLD ver2.5.3 緊急修正レポート

確認日: 2026-10-10。目的は **ver2.5.1のPlayerの動き + ver2.5.2の文字表示**。MAP EDITOR v0.6、Map JSON v4、正式Mapの配置は維持した。

## 1. 開始状態・Backup

- 開始時 `main` / `origin/main`: `493612d26e41b161eb1f2d024389db9de0c49e31`。差分は0/0。既存の未コミット `unity-project/Assets/Content/World/GrassDecoration.prefab` と未追跡 `backup-v05-20261006-174557/` は保護し、今回のコミットへ含めない。
- repo外の `C:\Users\owner\Documents\ChatGPT\HALKA-v253-backup-20261010` に `repo.bundle`、開始時status/diff、正式Map、Scene、Player Script・Sprite、Font Atlas、HP HTML、旧WebGL Buildを保存した。bundleは194,563,261 bytes。旧比較用Buildもrepo外で配信し、正式な作業ファイルへ混ぜていない。
- 正式Mapの前後SHA-256: `first_field.hwmap.json` = `4B61FB227DD3C72F54DF6C1D58DB121CA34A3676D41CC7EA5F9948D3B9A41864`、`halka_house.hwmap.json` = `CE451E4B5E7380B5E99F1BCB148E265FBACD9041F3512C8F7E91BF7F6174448E`。両MapのGit差分なし。

## 2. Player構造の復元

- 基準のver2.5.1は `7129f455e15635cc0b3bd9a66d79e0720be5c359`。`Player - HarukaChan` → `Player artwork`（SpriteRenderer、座り時移動対象）→ `Player grass foot mask`（SpriteMask）の階層で、連続するworld座標をそのまま描画する。
- ver2.5.2の `474567411f94a3bbac45d4b727a5f5b84daf9846` は `Player render pixels` 子Objectと `PlayerRenderSnap2D` を追加し、Camera投影後の描画位置を整数backing pixelへ丸めた。`CharacterVisual.HeadAlignmentWorldY` はIdle/Walkの頭部の1〜2px上下動を相殺した。ユーザーの意図と異なり、自然なコマ間の動きを変えていた。
- `PlayerRenderSnap2D.cs` とmetaを削除。`ProjectBuilder`を元のRenderer/Mask階層に戻し、Sceneを同じSource of Truthから再生成した。Scene内に `Player render pixels`、Snap component、余分なTransformはない。`Version253Checks`はRenderer・Mask・Seat・Grassの参照と階層を検証する。
- `HeadAlignmentWorldY`を削除。正面・背面・左右のIdle/Walk Sprite PNGは一切変更していない。元コマにある上下動をそのまま表示する。承認済み `bench_sit.png` とver2.5.1の目元補正も変更なし。
- `PlayerMover.cs`、`GridStepMotion.cs`、`CharacterVisual.cs`、`PlayerSeatController.cs`、`PlayerGrassOcclusion.cs`、全Player PNGは、最終作業ツリーで基準commit `7129f45`と**Gitの内容差分0**。1セル0.18秒の補間、Idle 0.4秒・Walk 0.15秒のコマ周期、Facing、押し続け時の連続移動、着席時の10/64 Unit offset、足元Maskの仕様は元どおり。

## 3. Font改善の保全

- `asobi_ui_atlas.png`とmeta、`GameBitmapFont.cs`、Atlas生成Script、576px基準のWebGL Templateはver2.5.2実装commit `4745674`と**Gitの内容差分0**。2px stroke、alpha二値化、Point、非圧縮、mipmapなし、IMGUIの整数座標、k8x12L fallbackを維持。Raw遊びメモ書きFontは追加していない。
- `Version252Checks`のFont検証を継承し、廃止したPlayer Snapを要求する検証だけを除去した。Spriteと座りポーズの検証は `Version251Checks` 以下にも残る。
- ver2.5.2 / 2.5.3のメニューを同じ1280×720で比較した `font-252-253-menu.png` は同じ文字の太さ・可読性を示す。生活記録の数値・時刻、正式Mapの看板「ここは HALKA WORLD。」もver2.5.3ブラウザで表示を確認した。

## 4. ブラウザ比較・Gameplay

- repo外のver2.5.1、ver2.5.2旧Buildとver2.5.3 Production Buildを、同じ1280×720 / DPR1 / Canvas backing 1024×576でローカルブラウザ起動した。停止正面を並べた `player-251-252-253-stills.png` では、ver2.5.3でver2.5.1にあった目元の赤・ピンクと自然なコマ位置が確認できる。比較はJPEGスクリーンショットなので**ピクセル精度の色測定には使っていない**。ソースPNGとPlayerコードの完全一致を別途確認した。
- ver2.5.3正式Mapで手動の左右・上移動、D-padの下・右移動を実操作。Spriteは各方向へ切り替わり、1セル歩行と自然なコマ切替を確認した。左・右座席へ座り、Aで立ち、通常Spriteへ戻ることを確認。座ったままメニューを開いても着席が維持され、閉じた後のAで立てる。Crowも通常移動した。
- メニュー、AUTO、せいかつきろく、歩数・プレイ時間、看板Messageを表示した。`v253-bench-seat.jpg`、`v253-bench-right.jpg`、`v253-seat-menu.jpg`、`v253-menu.jpg`、`v253-sign-message.jpg` をrepo外に保存した。比較用の `v251-idle.jpg`、`v252-idle-initial.jpg`、`v253-idle-initial.jpg` も同所。ブラウザ操作ツールの保存画像はJPEGで、高フレームレート動画の直接記録機能がないため、連続動画は作成していない。ライブ操作と実装・Sceneの比較で復元を判定した。
- House入退室、長時間AUTO、物理スマホの実操作は今回のブラウザセッションでは未確認。既存Regressionの対象は通過した。

## 5. 自動検証・Build

- `Version253Checks.Run` 成功 (`version253-check.log`)。内部で `Version252Checks.Run`からver2.0までの既存Regressionを実行。ver2.5.1のRenderer/Mask階層と参照、Snapなし、元コマ参照、0.18秒補間の中間位置0.5セル、Animation周期、Font AtlasとFallbackを検証。
- `MapAuthoringChecks.Run` 成功 (`map-authoring-check.log`)。Standalone MAP EDITOR Core Tests **181/181成功**。Editor v0.6、Map v4は変更なし。
- `ProjectBuilder.BuildWeb` 成功 (`webgl-build.log`)。`productVersion=2.5.3`、Development Build OFF、Unity Splash/Logo OFF、`stripEngineCode=false`。Build生成後、正式MapのSHAは作業前と同じ。旧WebGLの576px基準とCSS設定も維持。
- HP現在VersionとArchive最上段へ `ver2.5.3 / 2026-10-10 / キャラクターの動きを調整しました。` を追加。既存履歴を維持。

## 6. 公開・最終状態

- 検証済みの修正を `main` へ統合し、通常pushでGitHub Pagesへ公開する。force pushしない。
- 公開後のrun、commit、公開WebGLの実操作、最終 `git status` は公開完了時に追記する。
