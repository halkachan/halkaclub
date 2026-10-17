# はるかわーるど ver3.0 Phase 4 レポート：共通ゲームシステム統合・3Dカメラ改善・カラスの3D化

作業日: 2026-10-16〜18（日本時間）／担当: Claude（最終更新 2026-10-18）
ブランチ: `claude/ver3.0-2d3d-phase4`（Phase 3 `claude/ver3.0-2d3d-phase3` の `fcf8beb` から分岐）
worktree: `C:\Users\owner\Documents\ChatGPT\HALKA_HP_2D3D_PHASE4`
Preview: `C:\Users\owner\Documents\ChatGPT\HARUKA_WORLD_ver3.0_PHASE4_PREVIEW`（はるかわーるど ver3.0 Phase 4 Preview）

## 結論

本編のゲーム機能（AUTO・カラス・ベンチ／クッション・Life Log・Save・Menu・World Time）を共通ワールドにつなぎました。2Dでも3Dでも、同じゲーム状態で遊べます。確定指示の3点（退室直後のカメラ、3Dの木の半透明、スマホ横画面専用）も実装しました。

ローカルのWebGLで操作して確認しました（PC、DevToolsのスマホ横画面エミュレーション）。**物理スマートフォンでは確認していません。**

| 項目 | 結果 |
|---|---|
| 退室直後のカメラ | 3Dで家から出ると、家の正面側から家とﾊﾙｶﾁｬﾝを映します。次の移動入力で、いつものカメラへ戻ります。入力の向きは反転しません |
| 3Dのカメラ | 2Dと同じく、いつも南から北を見ます。向きを変えても歩いても、視点は変わりません。距離はPhase 3より少し離しました（屋外3.4・屋内3.6） |
| 3Dの木 | ﾊﾙｶﾁｬﾝを隠している木だけ、0.25秒で40%に薄くなります（2Dと同じ値）。木の影は残ります |
| スマホ | 横画面専用です。縦にすると「スマホを横向きにしてください」を表示し、ゲームは一時停止します。横に戻すと、再読み込みなしで続きから遊べます |
| AUTO | 本編の `AutoModeController` の判断ロジックを、そのまま共通ワールドで動かしています。屋内外・2D/3Dとも60秒で始まり、入力で止まります |
| カラス | Blender製のローポリモデルを作りました（566三角形）。飛ぶ時は翼を広げて羽ばたき、脚を体の下へ引き込みます。行動は本編のロジック1つを2Dと3Dで共有します。2Dは元のドット絵のままです |
| 座る | ベンチとクッションに座れます。2Dは元の `bench_sit.png`、3Dは座ったポーズです。表示を切り替えても座ったままです |
| Life Log・Save・Menu | 本編のものを接続しました。保存キーと形式は変えていません。Menuに「ひょうじ 2D／3D」の行を足しました |
| 検査 | 新しい `Phase4IntegrationChecks` 134件と、既存の検査すべてに合格しました |
| 性能 | PC・スマホ横画面とも、全条件で59.8〜60fps。Buildは +177,089 bytes（+0.60%） |

WebGLで確かめる中で、Editorの検査では出なかった不具合を3つ見つけて直しました（43項・44項）。
- カラスの3Dモデルが2体になる
- Buildで3Dの木が半透明にならない
- スマホを縦にしたとき、ページ全体が縮小される

正式Map・Catalog・Player Sprite・カラスの素材は変えていません（SHA-256で確認）。mainへのMerge、Push、公開はしていません。

本編のScriptを、共通化のために変更しています（AUTO・カラス・Life Log・Menu。3項）。本編の検査 `Version30Checks` は、変更の前後とも合格しています。

Previewを見ていただいた後に、次の3点を直しました（2026-10-18。本文に反映済み。経緯は末尾の付録）。
- カラスの飛行中の脚（立っている時と同じだった → 体の下へ引き込む）
- 3Dカメラの距離（近かった → 少し離す）
- 3Dの視点（向きで変わっていた → 南から北で固定）

確認していただきたい点があります（54項）。主なものは次のとおりです。
- 退室直後のカメラの構図と、戻り方
- カラスの3Dモデル・羽ばたき・飛行中の脚
- クッションの2Dの座り絵（今はベンチの `bench_sit.png` を流用しています。専用の絵はCodexの担当です）

---

## 1. 開始時Git状態

| 項目 | 値 |
|---|---|
| Phase 3 worktree | `HALKA_HP_2D3D_PHASE3`、`claude/ver3.0-2d3d-phase3` @ `fcf8beb`、変更なし |
| Phase 1／2／2.1 | `6a5166c`／`7d5dbf9`／`cc72dc1`、どれも変更なし |
| 元の作業ツリー | `HALKA HP`、`codex/ver3.0-preview` @ `d3f81ac`。` M GrassDecoration.prefab` と `?? backup-v05-20261006-174557/` の2件（以前からあるもの） |
| みねはうす | `HALKA-HP-minehouse`、`claude/minehouse-page` @ `4957aab` |
| ローカル main ／ origin/main | `d039acf` ／ `4957aab` |
| Phase 4 | 新しいworktree `HALKA_HP_2D3D_PHASE4` に、`claude/ver3.0-2d3d-phase4` を `fcf8beb` から作成 |

- origin/main は、Phase 3のレポートの時点（`13462fe`）から `4957aab` に進んでいました。みねはうすのページの作業によるもので、Phase 4の作業ではありません。

## 2. Backup

repo外の `C:\Users\owner\Documents\ChatGPT\HARUKA_WORLD_ver3.0_PHASE4_BACKUP_20261016` に保存しました。

- 全refのbundle `repo-all-refs.bundle`（`git bundle verify` 済み）
- `git-refs.txt`（全ブランチ）、`git-status-all.txt`（全worktreeの状態）
- `git-diff-original.patch`: 元の作業ツリーの差分です。GrassDecoration.prefab の差分は改行コードだけなので、空になっています。
- SHA-256の記録:
  - `official-map-sha256-before.txt`（正式Map・Catalog 4ファイル）
  - `player-sprite-sha256-before.txt`（Player Sprite 50ファイル）
  - `crow-assets-sha256-before.txt`（カラスのSprite 17ファイルと `crow_voice.mp3`）
- `phase3-webgl-build/`: Phase 3のWebGL Build
- `files/`:
  - POC一式、Blender（scripts・sources・exports）、tools
  - 本編の `Scripts`（Player・Camera・World・Interaction・Input・UI。AUTO・PlayerSeatController・LifeLog・WorldTimeを含む）、`Plugins`、`WebGLTemplates`
  - `Content`（正式Map・Catalog、Player Sprite、カラスのSpriteと音声）
  - 各Phaseのレポート・設計書、HP関連ファイル

既存のユーザー編集（GrassDecoration.prefab、backup-v05）は、削除も変更もしていません。

## 3. Phase 3からの変更一覧

**Commit（ブランチ `claude/ver3.0-2d3d-phase4`、Pushなし）**

| Commit | 内容 |
|---|---|
| `31adaaf` | AUTOとカラスの行動を、どの世界でも使えるように共通化（本編Script） |
| `039e0d8` | 共通ワールドのゲーム機能（AUTO・カラス・座る・Life Log・Menu）、退室カメラ、3Dの木の半透明、縦画面の案内、検査・撮影 |
| `659405d` | カラスの翼を横へ開き、背中より上まで羽ばたくように修正 |
| `06441e7` | 検査の実行でUnityが書き換えたMaterialを元に戻す |
| `47cfee4` | Previewページ（回転案内・Safe Area）、README |
| `6d1b00d` | カラスの3Dモデルが2体になる不具合の修正 |
| `c6d0333` | Buildで3Dの木が半透明にならない不具合の修正 |
| `910d35a`・`9d2ae8e` | READMEの説明（立ち上がり方、カラスへの話しかけ方） |
| `d47a2ab` | スマホを縦にしたときのページ縮小の修正 |
| `47b5fa0` | POCパネル（`?debug=1`）を畳んだ状態でも統計を表示 |
| `0f8ab59` | カラスの動画用のコマを書き出す撮影処理 |
| このレポート | Phase 4レポート |

`fcf8beb` からの差分は62ファイル、+6,340行／−1,105行です（レポートを除く）。

**新しいファイル（POC）**

| ファイル | 役割 |
|---|---|
| `Runtime/PocGameplay.cs` | 共通ワールドと本編の部品（AUTO・Life Log・Menu・HUD・カラス）をつなぐ。AUTO用の窓口 `PocAutoWorld` |
| `Runtime/PocCrow.cs` | 共通ワールドのカラス（本編の行動ロジック `CrowBehaviourCore` を継承） |
| `Runtime/PocCrowView3D.cs` | カラスの3D表示（Blenderモデル、動き） |
| `Runtime/PocExitComposition.cs` | 退室直後のカメラの構図の計算 |
| `Runtime/PocTreeFade3D.cs` | 3Dの木の半透明の判定と切り替え |
| `Runtime/PocOrientationGuard.cs` | スマホ縦画面での一時停止 |
| `Shaders/PocToonVertexColorFade.shader`・`Materials/POC_ToonVertexColorFade.mat` | 半透明の木用のShader |
| `Models/Phase4/crow_basic.fbx` | カラスのモデル |
| `Editor/Phase4IntegrationChecks.cs` | Phase 4の検査（134件） |
| `Editor/Phase4Capture.cs` | 比較画像・動画用のコマの撮影 |
| `blender/scripts/build_phase4_crow.py`・`render_phase4_previews.py` | カラスの生成・プレビュー |

**変更したPOCのファイル**
- `SharedWorldSession`（座る状態、出入りの記録、カラスとの当たり判定、話しかけ、縦画面中の入力停止）
- `SharedWorldModel`（座るActionの判定）
- `PocThirdPersonCamera`（退室の構図、木の半透明）
- `PocPixelView2D`（2Dのカラス、木ごとの半透明の値）
- `PocActorView`（座ったポーズ、2Dの座り絵の位置）
- `PocInput`（Menu・AUTO・縦画面、カラスのクリック）
- `PocViewSwitcher`（カラスの表示切替とクリック）
- `PocWorldPresenter`・`PocPresentationCatalog`・Catalog asset（木の半透明の設定）
- `PocDebugPanel`（`?debug=1` の時だけ表示）
- `PocSceneBuilder`・`PocBuilder`・Scene、`Phase3BuildingFurnitureChecks`（42項）

**本編のScriptの変更**

| Script | 変更 | 本編への影響 |
|---|---|---|
| `IAutoModeWorld.cs`（新規） | AUTOが使う世界の窓口（地図・Player・カラス・Message・停止中か） | なし。本編は、従来のScene参照を包んだ `SceneWorld` を使います |
| `AutoModeController.cs` | 判断ロジックはそのままで、参照先を窓口経由に変更。`UseWorld()` を追加 | Version30Checks（AUTOの検査を含む）合格 |
| `GridPathfinder2D.cs` | 「通れるか」を関数で受け取る版を追加。既存の版はそれを呼びます | 同じ結果 |
| `CrowBehaviourCore.cs`（新規） | CrowWander2Dの行動ロジック（状態・時間・飛行・止まり木・Interaction）を移した基底クラス | — |
| `CrowWander2D.cs` | CrowBehaviourCoreの2D版（Sprite・草・Occupancy・木のAnchor） | 2Dのカラスの動きは同じ（Version30Checksのカラスの検査に合格） |
| `TreePerchAnchor2D.cs` | 止まり木の共通の窓口 `ICrowPerch` を実装 | なし |
| `LifeLogController.cs` | `ForegroundBlocked`（縦画面の案内中はプレイ時間を数えない）と `RecordCompletedStep()`（PlayerMoverのない世界から歩数を渡す）を追加 | 既定はfalseなので、本編の動きは同じ |
| `GameMenuController.cs` | `IGameMenuItem` で行を1つ追加できる（今回は「ひょうじ」） | 本編は行を追加しないので、従来どおり |

- 作業の順番（指示書38項）: Version30Checksで変更前を確認（ログ `00`）→ 共通化 → Version30Checks（ログ `01`）→ POCへ接続 → 全Regression（42項）。
- `PlayerMover`・`PlayerSeatController`・`CharacterVisual`・`WorldTimeController`・`GameInput` は変えていません。

## 4. 退室直後のCamera修正

**流れ**
1. 屋内の出口へ進む
2. 屋外へ切り替わる。`SharedWorldSession` が「建物から出た」ことを、扉のセルと出た向きと一緒に記録します（`SharedTransition`）。
3. 3Dのカメラ（`PocThirdPersonCamera`）がそれを見て、構図を計算します（`PocExitComposition`）。
4. 家の正面とﾊﾙｶﾁｬﾝを映したまま、止まります。
5. 次の移動入力で、いつものカメラへ戻ります（6項）。

**構図の決め方**
- 扉のセルに立っている建物を、表示中の物から探します。House専用の判定ではないので、扉のある建物が増えても使えます（指示書5.4）。
- カメラの向き: 出た向きの反対です。南へ出たら、カメラは南側から北（家）を見ます。
- 見る点: ﾊﾙｶﾁｬﾝの胸（高さ0.45）と、建物の中心（高さの42%まで）の間で、建物側へ45%寄せた点です。
- 角度22°。距離は、建物の大きさと画角から決めます（3〜7.5の範囲）。

**家を隠さない（指示書5.1）**
- 構図の間、家の屋根・壁・扉は消えません。影だけになることもありません（検査2）。
- カメラとﾊﾙｶﾁｬﾝの間に別の物がある場合は、従来どおりその物だけを隠します。

**結果**
- Unity: `unity/exit_camera_old_phase3`（旧）・`exit_camera_new_held`（新）・`exit_camera_new_after_first_move`（次の移動の後）、夜も同じ
- WebGL: `pc/17`〜`19b`、スマホ横 `m13`・`m14`、動画01
- 比較: `sheets/01_exit_camera_old_new_after.png`

## 5. Camera基準入力との整合

- 3Dのカメラは、構図の間も普段も「南から北を見る」で固定です（6項）。3Dの移動はカメラ基準なので、W＝北（家の方）、S＝南（カメラの方）、A＝西、D＝東になります。2Dと同じです。
- 移動の向きは、キーを押した瞬間に決まります（Phase 2.1の仕組み）。カメラが戻る途中で向きが計算し直されることはありません。
- Grid Stepは0.18秒のままです。
- **歩いて出た時に押していたキーは、一度離すまで使いません。** 押しっぱなしのSで出ると、そのSで構図がすぐ終わってしまうためです。キーを離して、もう一度押すと歩きます。
- 検査（番号は41項の一覧）:
  - 5: 最初のSで南へ歩く（反転しない）
  - 9・10: A＝西、D＝東
  - 6: 最初の移動で構図が終わり、カメラは同じ向きのまま

## 6. 通常Cameraへの復帰

- 構図が終わる条件:
  - 新しい移動入力
  - 1歩でも動いた時（AUTOの1歩を含む）
  - 座った時
- 戻り方: 位置・向き・角度・距離を、いつものカメラと同じなめらかさで補間します。1コマの移動量は0.12未満です（検査7）。
- 戻る途中も、家は消えません（検査8）。
- 構図が終わっても、カメラの向きは北のままです。カメラは家の南側の位置から、ﾊﾙｶﾁｬﾝの南側のいつもの位置へ、なめらかに近づきます。家から離れた側を動くので、家の中を通りません。
- **普段の3Dのカメラ（ユーザーの指示で変更）:**
  - いつもﾊﾙｶﾁｬﾝの南側に立って、北を見ます（2Dと同じ向き）。向きを変えても、歩いても、止まっても、視点は回りません。屋内も同じで、手前（南）の壁をいつも低く切ります。
  - Phase 2.1〜3の「止まったら背後へ回る」動きは、POCパネルの「3D cam follow」で選べるモードとして残しています（north fixed／turn when idle／fixed at switch／facing）。
  - 距離はPhase 3より少し離しました（屋外2.5→3.4、屋内2.9→3.6。角度は26°・52°のまま）。POCパネルの「3D cam distance」で、2.5／3.0／3.8とも比べられます。
  - 検査61〜64、画像 `sheets/11_camera_distance_3d.png`、WebGL `webgl/pc/nf_*`・`fb_*`、`webgl/mobile/fb_*`
- 適用範囲（指示書5.4）:
  - 3Dで家から出た時だけです。2Dで出てから3Dへ切り替えた場合は、いつものカメラです（検査11）。
  - 屋外を普通に歩いている時（検査12）、屋内、AUTOには適用しません。AUTOの1歩で構図は終わります（検査13）。
- POCパネル（`?debug=1`）の「3D exit camera」で、Phase 3の挙動（背後から）と比べられます。

## 7. 3D Tree Fadeの実装

- Catalogの `tree_basic` の設定を変えました。
  - `fadeInFront3D = true`（3Dで半透明にする）
  - `cameraOccluder = false`（Phase 3までの「隠す」をやめる）
  - `fadeOpacity = 0.4`、`fadeSeconds = 0.25`（Presentation Metadataで変更できます）
- 処理: `PocTreeFade3D`。3Dカメラが毎コマ判定します（8項）。
- 描き方:
  - 半透明の間だけ、木のMaterialを専用のShader `HALKA/POC/ToonVertexColorFade` に切り替えます。濃さは `MaterialPropertyBlock` の `_Alpha` で木ごとに変えます。
  - Shaderは、いつもと同じToonの明るさです。最初に深度だけを描き、次に色を重ねます。木の内側の面が透けて見えることはありません。
  - Opaqueの木のMaterialのAlphaを変えるだけでは透けないため、専用のShaderにしました（指示書7.2）。
  - 100%に戻ったら、元のMaterialに戻し、PropertyBlockも消します。
- 影: Shaderに影のPassを残しています。透けている間も、地面の影は同じです（検査17）。
- **WebGLで見つけた不具合:** 最初のBuildでは、木が半透明になりませんでした。
  - 原因: 半透明の処理をScene作成時にしか作っておらず、Sceneに保存されていなかったためです。
  - 修正: カメラがMaterialを保存し、最初に使う時に処理を作るようにしました（`c6d0333`）。
  - 検査を追加しました。保存されたSceneを開いて確かめます（修正前のSceneでは失敗します）。

## 8. Tree Fadeの判定方式

- カメラから、ﾊﾙｶﾁｬﾝの体の6点への線を調べます（足元・腰・胸・頭、左右の腕）。
- 木のMeshの三角形がどれかの線をさえぎっていたら、その木は「ﾊﾙｶﾁｬﾝを隠している」と判定します。
  - Physicsは使わず、CPUで三角形と線の交差を調べます。三角形はBuild時に記録しています。
- 木の根元のセルとの距離では判定しません。後ろにある木や、画面で重ならない木は透けません（検査18）。
- 木ごとに判定します。2本が同時に隠していれば2本とも透けます。1本だけになれば、もう1本は元に戻ります（検査22・23）。
- カメラやﾊﾙｶﾁｬﾝが動けば、その位置で判定し直します（WebGL: `pc/07b`、`pc/bursts/fadeD_*`。どちらもカメラを北固定にする前の撮影です）。
- カメラは南から北を見るので、ﾊﾙｶﾁｬﾝの**南側**にある木が、彼女を隠した時に半透明になります。

## 9. Tree Fadeの色・濃さ・時間

| 項目 | 値 |
|---|---|
| 通常 | 100% |
| 重なった時 | 40% |
| Fade In ／ Fade Out | 0.25秒（一定の速さ） |
| 色 | 変えません（同じToonの明るさ） |
| 影 | 残します |

- 夜も、40%の木の向こうにﾊﾙｶﾁｬﾝが見えます（`unity/tree_3d_fade_on_night`）。
- POCパネルの「3D see-through trees」で、Phase 3の挙動（隠す）と比べられます。
- 画像: `sheets/02_tree_fade_3d_2d.png`、動画02

## 10. 2D Tree Fadeへの影響

- 2Dは、これまでどおりSpriteの重なりで判定します。濃さと時間は、3Dと同じCatalogの値（0.4・0.25秒）を読みます。値は以前と同じです。
- `Phase21PresentationChecks`（2Dの半透明を含む60件）は合格しています。Phase 4の検査24〜27でも確かめました。
- 2D↔3Dの切替、Mapの切替、カメラの停止の時は、すべての木を元のMaterialに戻します。2Dに3D用のMaterialが残ることはありません（検査24・27）。
- 2Dの輪郭線とピクセル表示は、半透明のMaterialを使いません。
- カラスは別のRendererなので、木と一緒に透けることはありません（検査19）。2Dでも、空中のカラスは半透明の木より後に描きます。

## 11. スマホ横画面設計

**2つの層で対応しています。**
- ページ（HTML）: 回転案内を表示します（12項）。キャンバスをSafe Areaの内側に置きます（14項）。
- Unity: `PocOrientationGuard` が、縦画面の間ゲームを一時停止します。

**スマホかどうか**
- 本編のタッチ操作と同じ条件です（`HalkaTouchUiMode`）。
  - `?touchControls=1`、または「タッチがある」かつ「指で操作する端末」（`pointer: coarse`）
  - Unity側は、さらに `Application.isMobilePlatform` も見ます。
- 縦画面: スマホで、縦が横より長い時です。PCの縦長のウィンドウでは案内を出しません（検査28）。

**横画面**
- これまでどおり遊べます。D-pad・A・Menuは画面の角（Safe Area）を基準に置くので、16:9以外の比率でも使えます（13項）。
- PCの表示は変えていません（高さ576pxの上限もそのまま）。

**縦画面の間（指示書9.4）**

| 項目 | 状態 |
|---|---|
| 移動・Aボタン・クリック | 受け付けません（D-pad・Aボタン・Menuも止めます） |
| AUTO | 始まりません（「前面にいない」と同じ扱い） |
| Life Logのプレイ時間 | 数えません |
| カメラ・表示Mode・Grid位置・歩数 | 変えません |
| World Time | 実時間で進みます。横に戻すと、その時の朝夜を表示します |

**Screen Orientation API**
- ページは、最初のタッチで一度だけ `screen.orientation.lock('landscape')` を呼びます。失敗しても何もしません。
- 多くのブラウザでは、全画面表示中かインストールしたアプリでしか固定できません。今回のDevTools環境では `NotSupportedError` で拒否されました。そのため、回転案内で対応しています。
- 実機で固定できるかは確認していません。
- ゲーム画面をCSSで90°回す方式は使っていません（入力の座標がずれるため）。

**HPのiframeへ統合する時に必要なこと（指示書9.5）**
- 公開HPは変えていません。統合する時に、親ページ側で次の対応が必要です。
  1. スマホでは、iframeを画面いっぱい（`100vw`×`100dvh`）にする。親ページで16:9などの箱に入れると、スマホが縦でもiframeは横長になり、iframeの中の回転案内が出ません。
  2. または、親ページ側にも同じ回転案内を出す。
  3. Safe Area: iframeの中では `env(safe-area-inset-*)` が0になるブラウザがあります。親ページで `viewport-fit=cover` を指定し、iframeの外側に余白を取る必要があります。
  4. 向きの固定: `lock()` は全画面表示が前提です。親ページで、iframeを全画面にするボタンを付ける場合は、`allow="fullscreen"` が必要です。
  5. 親ページのスクロールや拡大の操作が、ゲームのタッチと重ならないようにする（`touch-action`、`overflow: hidden`）。

## 12. 回転案内

- ページの上に重ねるHTMLです（`#rotate-guide`）。
  - 背景はページと同じ色（`#e7eecf`）。中央に回転のアイコン（SVG）と「スマホを横向きにしてください」を表示します。
  - ゲームの手書き風Bitmap Fontには「横」「向」がないため、端末の日本語Fontで表示しています。
- ウィンドウの大きさが変わるたびに、表示・非表示を切り替えます。再読み込みは必要ありません。
- Unity側は、案内の間は何も描かず、一時停止します（11項）。
- 確認（DevTools、`mobile/` の画像）:
  - `m21`: 縦にすると案内が出ます。
  - `m22`→`m23`: 縦の間にキーとタッチを送り、65秒待ちました。横に戻しても、位置・向き・歩数は同じで、AUTOも始まっていません。
  - `m31`→`m32`: Life Logのプレイ時間。64秒のうち60秒を縦で過ごし、増えたのは4秒（横の時間だけ）でした。
  - `m24a`〜`m24d`: 縦→横→縦→横。最後の横で、すぐに歩けます。
- 画像: `sheets/09_portrait_guide.png`

## 13. Mobile UI配置

本編のUIをそのまま使っています。新しい操作ボタンは追加していません。

| UI | 位置 |
|---|---|
| D-pad | 左下 |
| Aボタン | 右下 |
| Menu | 左上 |
| Message | 下の中央（D-pad・Aと重なる時は上へ） |
| バージョン表示 | 右上 |

- どれも `Screen.safeArea` の内側に置きます（本編の処理）。
- 検査（38〜41）: 1688×780（844×390・DPR 2）、1280×720、1024×576、1920×1080で、D-padが左下、Aが右下、Menuが左上にあり、画面の中で重なっていないことを確かめました。
- WebGL（DevTools）: 844×390・DPR 2で、D-pad・A・Menu・Life Log・Messageを操作しました（44項）。
  - 640×360（16:9）、PCの1280×720・1024×576も表示を確かめました。
- POCパネル（`?debug=1` の時だけ）は、小さい画面では畳んだ状態で始まります。畳んだ状態でも統計を1行で表示するようにしました（`47b5fa0`）。

## 14. Safe Area

- ページ:
  - `env(safe-area-inset-*)` を、見えない要素で読み取ります（`viewport-fit=cover` は元のテンプレートにあります）。
  - スマホでは、キャンバスをSafe Areaの内側に置きます。PCでは値が0なので、表示は変わりません。
- Unity: UIは本編どおり `Screen.safeArea` を使います。WebGLではキャンバス全体がSafe Areaになるので、ノッチやホームバーを避けるのはページの役目です。
- 確認（DevTools）: 左右47・下21のSafe Areaを設定すると、キャンバスは x=47 から 750×369 になり、D-pad・Aはその内側に入りました（`m25`）。
- **ノッチのある実機では確認していません。**
- **WebGLで見つけた不具合:** スマホを縦にした時、ページ全体が縮小されて表示されました。
  - 原因: 横画面の時のキャンバスの幅（844px）が、縦のウィンドウ（390px）より広いまま残っていたためです。
  - 修正: スマホでは、キャンバスの大きさをウィンドウに対する割合で指定するようにしました（`d47a2ab`）。

## 15. AUTO統合設計

新しいAUTOは作っていません。本編の `AutoModeController` の判断ロジックを、そのまま使っています。

```
AutoModeController（本編の判断。屋内は IndoorAutoRoutePlanner2D）
  ↓ IAutoModeWorld（AUTOが使う世界の窓口）
PocAutoWorld（共通ワールド用の窓口）
  ↓ SharedWorldSession.TryStep（世界の東西南北）
GridStepMotion（0.18秒）
  ↓ 共通のPlayer State（Cell・Facing・位置）
2D（PocPixelView2D）または 3D（PocActorView）が描くだけ
```

- AUTOは表示Modeを知りません。2D用・3D用のAUTOはありません。
- AUTOは世界の東西南北で歩きます。カメラ基準の変換は、プレイヤーのキー入力（`PocInput`）だけで使います。カメラが回っても、AUTOの道は変わりません（検査54: 同じ乱数なら、2Dと3Dで同じ道を歩く）。
- 本編から共通ワールドへ渡すのは、窓口の内容だけです。
  - 地図（Map・家の扉のセル・屋外の入口のセル）
  - Playerの位置・移動中か・動けないか（座っている間）
  - 1歩進む／止める
  - Messageを表示中か、カラスのセル
  - 縦画面の案内中か
- 止まる時（本編の規則のまま）:
  - Menuを開いている間（`SetMenuSuspended`）
  - Messageの表示中
  - 座っている間
  - 縦画面の案内中
- 家への自動の出入りはしません。本編の規則で、扉のセルを避けます（検査45・51・55・56）。
- AUTOの設定: 本編どおり、始めはON、保存しません（検査122）。
- 60秒は本編の値（`IdleSeconds = 60`）のままです。Previewに短い時間は残していません。検査では時計を進めて確かめています。

## 16. AUTO屋外

| 確認 | 2D | 3D |
|---|---|---|
| 55秒 | 始まらない（`pc/15c`） | 始まらない（`pc/15f`） |
| 64秒 | 歩いている（`pc/15d`） | 歩いている（`pc/15g`） |
| その後 | 道や草の上を歩く（`pc/15e`） | カメラが追いかける。家の前を通っても入らない（`pc/15h`・`15i`） |

- 動画05の前半（屋外2D・屋外3D）で、開始の瞬間を撮っています。
- 検査42〜53: 60秒前は始まらない、60秒で始まる、1マスずつ歩く、家・物・カラスに入らない、歩数がLife Logと同じ、入力ですぐ止まる（2D・3Dとも）。

## 17. AUTO屋内

| 確認 | 2D | 3D |
|---|---|---|
| 55秒 | 始まらない（`pc/15k`） | 始まらない（`pc/14`） |
| 64秒 | 歩いている（`pc/15l`） | 歩いている（`pc/15a`） |
| その後 | 家具をよけて部屋を歩く。出口から出ない（`pc/15m`・`15n`） | 同じ（`pc/15b`、動画05の後半） |

- 屋内は、本編の `IndoorAutoRoutePlanner2D` が道を決めます。
- 検査55・56: 家具をよけて歩き、出口を通らない（2D・3D）。

## 18. 60秒開始

実ブラウザ（headless Chrome、実時間）で、4つの条件すべてを確かめました。

| 条件 | 55秒 | 64秒 |
|---|---|---|
| 屋外 2D | 始まらない | 始まる |
| 屋外 3D | 始まらない | 始まる |
| 屋内 2D | 始まらない | 始まる |
| 屋内 3D | 始まらない | 始まる |

- 夜も動きます（動画05は実時間の夜）。
- 検査42・43・48・49は、59.9秒で始まらず、60秒で始まることを確かめています。

## 19. Player Inputで停止

- キー、D-pad、Aボタン、クリック、タッチのどれでも、AUTOはすぐ止まります。今の1歩はマスの上まで歩いて止まります。60秒の計測もやり直しになります（本編の `GameInput` と同じ条件）。
- WebGL: 手動で1歩動かすと止まり、4秒後も止まったままでした（`pc/16a`・`16b`）。
- 表示の切替について:
  - ブラウザでは、MenuやPOCのボタンで切り替えます。その操作自体が入力なので、AUTOは止まります。
  - 入力なしで表示だけを切り替えた場合は、AUTOは同じ道を歩き続けます（検査57・58）。

## 20. Blender Crowモデル

元のドット絵（`crow_*.png`、32px）を見て作りました。リアルな鳥ではなく、可愛らしいローポリキャラクターです。

| 部分 | 形 |
|---|---|
| 体 | 黒〜濃い灰色の丸い体 |
| 頭 | 小さめの頭、灰色のくちばし、白い目 |
| マフラー | 赤。結び目と、垂れた2本の端 |
| 翼 | 左右。たたんだ時は体の横に沿う |
| 尾羽 | 短い扇形 |
| 脚 | 2本、指は3本 |

- 三角形は合計566、Materialは1つ（頂点カラー。他の物と同じToon Shader）です。
- 高さは約0.41（1マスは0.5）です。
- 正面は南向き、原点は両足の間の地面です（他のモデルと同じ約束）。
- 補った部分（ドット絵からは分からない所）:
  - 頭と体の背面（Spriteは32pxの正面・背面・横だけ）
  - たたんだ翼の形と、尾羽の扇形
  - 3本の指
- 画像: `sheets/03_crow_blender_model.png`（正面・背面・東・西・斜め・上、翼を上げた形、元のドット絵）

## 21. Crow Blender Source

| ファイル | 内容 |
|---|---|
| `blender/scripts/build_phase4_crow.py` | 生成Script（Phase 1からの `halka_lowpoly.py` を使用。そちらは変更なし） |
| `blender/sources/phase4/crow_basic.blend` | Blender Source |
| `unity-project/Assets/POC2D3D/Models/Phase4/crow_basic.fbx` | FBX |
| `blender/exports/phase4_crow_manifest.json` | Manifest（部分ごとの三角形・頂点・Pivot、大きさ、補った部分） |
| `blender/scripts/render_phase4_previews.py` | プレビュー（FBXを読み直して描くので、FBXの確認にもなります） |
| `blender/exports/previews/phase4/` | プレビュー9枚 |

再生成:
```
"C:\Program Files\Blender Foundation\Blender 5.2\blender.exe" --background --factory-startup --python blender/scripts/build_phase4_crow.py
"C:\Program Files\Blender Foundation\Blender 5.2\blender.exe" --background --factory-startup --python blender/scripts/render_phase4_previews.py
```

部分（それぞれ関節の位置にPivotがあり、Unityで個別に動かせます）:

| 部分 | 三角形 | Pivot（Blender、m） |
|---|---|---|
| 体 `crow_basic` | 80 | 足元 (0, 0, 0) |
| 頭 `Head`（くちばし・目） | 182 | 首 (0, −0.02, 0.26) |
| マフラー `Scarf` | 80 | 首 |
| 翼 `WingL` ／ `WingR` | 20 ／ 20 | 肩 (±0.105, 0, 0.205) |
| 尾羽 `Tail` | 16 | 腰 (0, 0.115, 0.14) |
| 脚 `LegL` ／ `LegR` | 84 ／ 84 | 股 (±0.045, 0, 0.075) |
| `PerchFeet`（空のObject） | — | 足元。止まる位置の目印 |

## 22. Crow FBX・Animation

**FBX**
- 書き出し: 前は −Z、上は Y、Transformを焼き込み（他のモデルと同じ）
- Unity: 倍率1、Animationの読み込みなし。部分の名前（Head・WingL など）で探して動かします。

**Animation**
- Rigは使わず、部分のTransformを動かしています（`PocCrowView3D`）。

| 状態 | 翼・体 | 脚 |
|---|---|---|
| Ground Idle | 小さく上下に揺れる、頭を左右に向ける。翼はたたむ | 立つ（真下） |
| Ground Walk | ぴょんと跳ねる、少し前に傾く | 交互に振る |
| Takeoff | 翼を0.15秒で開く、速く羽ばたく、頭を上げる、尾羽を上げる | 0.25秒で体の下へ引き込む |
| Flying | 羽ばたく（1秒に3.2回）、少し前に傾く | 引き込んだまま |
| Perched | 翼をたたむ、ゆっくり見回す | 立つ（枝の上） |
| Leaving Perch | 離陸と同じ（翼を開いて速く羽ばたく） | 引き込む |
| Landing Ground | ゆっくり羽ばたき、体を起こして着地 | 0.25秒で前へ出す（地面をつかむ形）。着地したら立つ |

**脚の形（ユーザーの指示で追加）**
- 飛んでいる間: 腰の位置を軸に、脚を後ろへ85°たたみ、少し小さく（65%）します。羽毛の中に脚をしまったように見えます。
- 着地: 脚を前へ30°出します。
- 画像: `sheets/12_crow_legs_3d.png`、検査71・74・82

**羽ばたきの形**
- 開く: 翼を横へ80°振り出し、平らな面を水平に近づけます（55°ひねる）。
- 羽ばたき: 体の前後の軸のまわりで、水平より20°下から40°上まで動かします。上げる時間が45%、打ち下ろしが55%です。
- 画像: `sheets/04_crow_3d_states.png`（下の段が、たたむ／打ち下ろし／打ち上げ）

**Phase 4の途中で直したこと**
- 最初の版では、翼を回す軸がずれていました。羽ばたくと、翼が後ろや体の中へ動いていました。
- 撮影した画像で気づいて、作り直しました（`659405d`）。
- 翼の頂点の位置を測る検査を追加しました（検査76: 開いた翼は左右へ出る、打ち上げで背中より上へ、後ろへは動かない）。

## 23. Crow Shared State

本編の `CrowWander2D` から、行動のロジックを `CrowBehaviourCore` に移しました。Spriteや草など、2Dの表示の部分は残しています。

```
CrowBehaviourCore（行動・状態。本編のロジック）
  ├ CrowWander2D（本編の2Dのカラス）
  └ PocCrow（共通ワールドのカラス。1羽）
        ├ 2D: PocPixelView2D が元のSpriteを描く
        └ 3D: PocCrowView3D がBlenderモデルを描く
```

- 共通の状態: 地上のセル、向き、行動（7つ）、飛行の始点・終点・進み具合、止まる木、止まっている時間、次に動く時刻、話しかけた後の離陸、地上の占有、話しかけられるか。
- 2Dと3Dは状態を読むだけで、書き換えません。表示の切替でカラスがやり直しになることはありません（検査77）。
- 本編の2Dのカラスも同じ基底クラスで動きます。`Version30Checks` のカラスの検査（話しかけ、飛行、着地など）は合格しています。

## 24. Crow 2D Presenter

- `PocPixelView2D` が、カラスの今のSpriteを、元のドット絵のまま（同じ画素の密度で）描きます。
  - 使うSprite: 待機4方向、歩き4方向×2コマ、飛行2方向×2コマ（すべて元のファイル）
- 前後関係:
  - 地上のカラスとﾊﾙｶﾁｬﾝは、手前にいる方を後に描きます。
  - 空中・木の上のカラスは一番上に描きます（本編の飛行中の並び順と同じ）。半透明の木にも隠れません。
- 音声 `crow_voice.mp3` は元のファイルです。Spriteと音声18ファイルのSHA-256は、作業前と同じです。
- 草との前後関係は **一部だけ** です。
  - 本編では、`CrowGrassOcclusion` が草のマスク（`crow_grass_mask.png`）でカラスの足元を隠します。
  - 共通ワールドの2Dでは、このマスクは使っていません。ﾊﾙｶﾁｬﾝと同じように、手前の3Dの草や物の奥行きで隠します。
- 画像: `unity/crow_2d_ground`・`crow_2d_flying`・`crow_2d_perched`、`sheets/05_crow_2d_and_webgl.png`

## 25. Crow 3D Presenter

- `PocCrowView3D` が、毎コマ `PocCrow` の位置と行動を読んで、Blenderモデルを置き、動かします（22項）。
- 向き: 地上ではカラスの向き（4方向）、空中では飛ぶ方向です。なめらかに回ります。
- 2Dの時は隠します。
- クリック: モデルの範囲（最小0.3）に当たるかで判定します。
- **WebGLで見つけた不具合:** 最初のBuildでは、カラスの3Dモデルが2体ありました。止まったままの1体が、2Dでは地図の中央に、3Dでは動くカラスに重なって見えました。
  - 原因: Scene作成時に作ったモデルがSceneに保存され、起動時にもう1体作っていたためです。
  - 修正: 起動時に、古いモデルを消してから作るようにしました（`6d1b00d`）。
  - 検査133を追加しました。保存されたSceneから起動して確かめます（修正前は失敗します）。

## 26. Crow Flight

本編の時間と状態の流れを、そのまま使っています。

| 項目 | 値（本編と同じ） |
|---|---|
| 地上で次に動くまで | 2〜4秒 |
| 1歩 | 0.27秒 |
| 地上にいる時間（次の自然な飛行まで） | 20〜40秒 |
| 飛ぶ速さ | 2.2／秒（1回の飛行は0.55〜3.5秒） |
| 木に止まる時間 | 4〜8秒 |
| 話しかけてから離陸まで | 0.45秒 |

- 流れ: 地上 → 離陸 → 飛行 → 木に止まる → 木から離れる → 地上へ着地
- 飛行中の位置は、3Dの世界の位置です。地上のセルは、着地した時だけ決まります。Gridを勝手に変えることはありません。
- 3Dのモデルに合わせた補正は、向きと羽ばたきだけです。
- WebGLで見た時間（`crow_watch2.log`）: 12.6秒に飛び立ち、14.2秒に木へ止まり、21.8秒に離れ、25.9秒に地上へ戻りました（止まっていた時間は約7.6秒）。
- 動画03（Unity）、動画04（WebGL）

## 27. Crow Tree Perch

- 止まる位置は、Phase 2のBlenderの木にある `PerchAnchor` です。
- 表示中の木から、その木のAnchorを探します（`PocWorldPresenter` の表示）。木の座標は書いていません。MAP EDITORで木を動かせば、止まる位置も一緒に動きます（検査84）。
- 3D: 樹冠の上に止まります（`unity/crow_3d_perched`）。
- 2D: 同じAnchorの位置に、元の `crow_idle_right` を描きます。樹冠の上に止まって見えます（`pc/24`）。
  - 本編の2D用のAnchor（`TreePerchAnchor2D`）は、共通ワールドでは使っていません。

## 28. Crow Interaction

本編と同じ規則です。

1. 地上のカラスの隣に立ち、カラスの方を向く（`ExamineInteractable` と同じ距離と向きの規則）
2. A（PCはﾊﾙｶﾁｬﾝかカラスのクリック）で話しかける
3. 「カァ。」を表示し、`crow_voice.mp3` を鳴らす
4. 0.45秒後に離陸し、木へ飛ぶ

- 空中・木の上では話しかけられません。反応中にもう一度Aを押しても、二重に飛びません（検査86〜89）。
- 2Dでも3Dでも同じです。
- WebGL:
  - PC 3D（ﾊﾙｶﾁｬﾝのクリック＝A）: `pc/21_crow_talk_*`、`pc/2930_reload_*`
  - スマホ横 2D（Aボタン）: `mobile/m16_crow_talk_*`
- 音: headlessのChromeなので、耳では確かめていません。
  - WebAudioの記録では、話しかけの約90ms後に、0.813秒の音（`crow_voice.mp3` を展開したもの）が始まりました。
  - 同じ時刻にもう1回「開始」が記録されています。調べると、Unityの音声エンジンが、同じチャンネルを一時停止して再開したものでした（1回目は止められて切り離されます）。聞こえる音は1回だと考えています。
  - C#側で音を鳴らす命令は、1回の話しかけで1回です（検査85）。

## 29. Crow Occupancy

本編と同じ規則です。

| 状態 | 地上のセル |
|---|---|
| Ground Idle | 今のセルを占有 |
| Ground Walk | 出発と到着の2セルを占有 |
| Takeoff | 空中に上がるまで、今のセルを占有 |
| Flying ／ Perched ／ Leaving Perch | 占有しない |
| Landing Ground | 着地したら、そのセルを占有 |

- ﾊﾙｶﾁｬﾝはカラスのセルへ入れません。カラスはﾊﾙｶﾁｬﾝのいるセルへ歩いたり、着地したりしません（検査66・67・72・75・78・92・93）。
- 共通ワールドでは、本編の `DynamicGridOccupancy2D` の代わりに、`SharedWorldSession` がカラスに「このセルを使っているか」を聞きます（`ISharedGroundOccupant`）。占有と解放の時期は同じです。

## 30. CrowとView切替

指示書25項の流れを確かめました。

| 手順 | 確認 |
|---|---|
| 1〜3 | 2Dで歩くカラス → 3Dへ切替 → 同じ位置にいる（検査69、動画04） |
| 4〜6 | 飛び始める → 飛行中に2Dへ（または3Dへ）切替 → 同じ飛行が続く（検査77、`pc/25a`〜`25c`） |
| 7〜9 | 木に止まる → 切替 → 同じ木に止まっている（検査78〜80） |
| 10 | 地上へ戻る（検査83、`pc/26a`・`26b`） |

- 切替で、カラスが作り直されたり、飛行のタイマーがやり直しになったりはしません。
- 家との関係:
  - 屋内ではカラスを表示せず、占有も動きもしません（本編のMapの切替と同じ。検査90）。
  - 屋外へ戻ると、本編どおりSpawnの位置で作り直します。1羽だけです（検査91）。

## 31. Bench Sit統合

- ベンチの前（座る位置のAction Point）で、ベンチの方を向いてAを押すと座ります。
  - 論理的なセルは、立っていたセルのままです（検査94・99）。
  - 2Dでは、本編の `PlayerSeatController` と同じように、ベンチのセルの上に `bench_sit.png` を描きます。
- 座っている間:
  - 歩きません。向きも変わりません（本編と同じく、方向キーでは立ち上がりません。検査96・101）。
  - AUTOは始まりません（検査59）。
  - 別の物を調べられません。座っている物（ベンチ）だけが反応して、立ち上がります（検査115）。
- もう一度A（PCはﾊﾙｶﾁｬﾝかベンチのクリック）で立ち上がります。立ち上がった後も同じセルで、歩数は増えていません（検査98・103）。
- 表示を切り替えても座ったままです（検査97・102）。
- WebGL: `pc/12d`〜`12g`、スマホ横 `mobile/m18`〜`m20`、動画06

## 32. Cushion Sit統合

- **本編では、クッションに座れません。** 本編の `PlayerSeatController` はベンチの座り方（`bench_sit`）しか受け付けないので、クッションのAction（`cushion_sit`）では何も起きません。
- Phase 3のPOCでは、座るActionで「すわる。」を表示していました。
- Phase 4では指示書のとおり、共通ワールドでクッションに座れるようにしました（本編のScriptは変えていません）。
  - クッションは乗れる物なので、クッションのセルに立ったまま座ります。
  - 2D: **`bench_sit.png` を流用した仮の表示です。** ベンチより低い位置（Anchorの高さに合わせる）に描きます。専用の絵は、Codexの担当として残しています。
  - 3D: `SitAnchor`（高さ0.1）に座ります。
- 立ち上がり方、表示の切替、AUTO、歩数はベンチと同じです（検査105〜112）。
- WebGL: `pc/12a`〜`12c`、`mobile/m11`・`m12`、動画06

## 33. 2D Sprite維持

- Player Sprite 50ファイルのSHA-256は、作業前と同じです。
- 座った時は、元の `bench_sit.png` を、本編と同じ位置（座る所の中心から10/64上）に描きます（検査95）。
- 立っている時の2Dの表示（Phase 2.1で承認されたもの）は変えていません。
- `CharacterVisual` は変えていません。

## 34. 3D Sit Pose

今の仮のﾊﾙｶﾁｬﾝ（四角いモデル）で、簡単な座ったポーズを作りました。

| 項目 | 内容 |
|---|---|
| 座る位置 | ベンチは `SeatAnchor_L`／`SeatAnchor_R` のうち座る所に近い方、クッションは `SitAnchor` |
| 腰 | Anchorの高さに合わせる（ベンチ0.22、クッション0.1） |
| 脚 | 前へ90° |
| 腕 | 前へ28° |
| 向き | 座る物の正面（南）を向く |

- 論理的なセルは変えません。表示のモデルだけをAnchorへ動かします。立ち上がると、元の位置に戻ります。
- 画像: `unity/bench_sit_3d`・`cushion_sit_3d`、`sheets/06_bench_cushion_sit.png`

## 35. Life Log統合

- 本編の `LifeLogController` をそのまま使っています。共通ワールドの「1歩終わった」（`StepCompleted`）で歩数を足します。
- 確かめた規則:

| 規則 | 確認 |
|---|---|
| 1歩で1増える | 検査116 |
| AUTOの歩数も数える | 検査46・52 |
| 木などで進めなかった時は増えない | 検査117 |
| 表示の切替・カメラの動きでは増えない | 検査118 |
| 座っただけでは増えない | 検査98・103 |
| 縦画面の案内中は、プレイ時間を数えない | 検査34、WebGL `m31`→`m32` |
| 再読み込み後も累計が残る | 検査119、WebGL（36項） |

- プレイ時間は、本編の「前面で遊んでいる時間」の定義のままです（タブが見えている、フォーカスがある）。
- WebGL: Menuの「せいかつきろく」（`pc/27`、`mobile/m05`）

## 36. Save維持

| 保存するもの | Key | 形式 | 状態 |
|---|---|---|---|
| Life Log | `HALKA_WORLD_life_log_v1` | `v1` ＋ 歩数 ＋ 秒（縦棒で区切る） | 変えていない |
| World Time | `HALKA_WORLD_time_cycle_anchor_v1` | 基準の時刻 | 変えていない |
| AUTOの設定 | なし | — | 本編どおり保存しない（始めはON） |
| 表示Mode・Playerの位置 | なし | — | これまでも保存していないので、追加していません |

- WebGLで、PlayerPrefs（ブラウザのIndexedDB）を直接読みました。再読み込みの前後とも `v1|1652|2308` で、Life Logの画面も同じ累計でした（`pc/2930_reload_*`、`2930_reload_hunt.log`）。

## 37. Menu統合

- 本編の `GameMenuController` を、そのまま使っています（手書き風のBitmap Font、同じ見た目）。

| 行 | 内容 |
|---|---|
| AUTO | ON／OFF |
| ひょうじ | 2D／3D（今回追加） |
| せいかつきろく | 本編のLife Log画面 |

- 「表示」の漢字はBitmap Fontにないので、「ひょうじ」としました。
- Menuを開いている間、AUTOは止まります。閉じると、60秒の計測が始まります（検査123〜129）。
- タッチとマウスの両方で操作できます。
- POCのDebug Panelは、`?debug=1` の時だけ表示します。最終的なゲーム画面には入れません（検査131）。
  - 普段は、右上に「はるかわーるど ver3.0 Phase 4 Preview」の表示だけを出しています。

## 38. World Time

- 2Dと3Dで、同じ `WorldTimeController` を使っています（朝10分・夜10分、実時間、再起動後も続く、オフラインの経過を反映、屋内外共通、Fade 2.5秒）。本編の設定のままです。
- 表示の切替でも、縦画面の案内中でも、時計はやり直しになりません（検査120）。
- 夜に確かめたこと:
  - カラス（Unityの夜の撮影、動画04の一部）
  - AUTO（動画05は実時間の夜）
  - ベンチ・クッション（`unity/bench_sit_*_night`・`cushion_sit_*_night`）
  - Menu・Life Log（`mobile/m32`）
  - 木の半透明（`unity/tree_3d_fade_on_night`。木の向こうのﾊﾙｶﾁｬﾝが見えます）
  - 退室カメラ（`unity/exit_camera_new_held_night`、PCの17〜19は実時間の夜）
- 画像: `sheets/10_morning_night.png`

## 39. 旧本編との機能比較表

「旧本編」は ver3.0 Preview（`codex/ver3.0-preview`、2Dのみ）です。「新Preview」は Phase 4 Preview（共通ワールド、2D/3D）です。コードがあるだけのものは「統合済み」にしていません。

| 機能 | 旧本編（2D） | 新Preview（2D/3D） | 判定 |
|---|---|---|---|
| 移動 | 4方向Grid、0.18秒、長押しで連続 | 同じ `GridStepMotion`・同じ規則。3DはCamera基準（Phase 2.1） | **一部統合**（足音と、2Dで草が足元を隠す表現は未接続。40項） |
| AUTO | 60秒で開始、屋内外、入力で停止、ON/OFF | 本編の `AutoModeController` を共通ワールドで動かす | **完全統合** |
| Interaction（調べる） | A／クリック、距離と向きの規則、Message | 同じ規則（`SharedWorldModel`）。2D・3Dとも | **完全統合** |
| Bench | 座る（`bench_sit.png`）、Aで立つ | 同じ。3Dは座ったポーズ。切替でも座ったまま | **完全統合** |
| Cushion | 座れない（Actionはあるが、本編の座る処理がベンチ専用） | 座れる（指示書に合わせて追加）。2Dの絵は `bench_sit.png` の流用 | **一部統合**（専用の2Dの絵がない。本編との違いあり） |
| Bed | 「ベッド。」 | 「ベッド。」（新しい睡眠システムはなし） | **完全統合** |
| Crow | 歩く・飛ぶ・木に止まる・話しかけ・鳴き声・占有 | 本編の行動ロジック1つを2D/3Dで共有。2Dは元のSprite、3DはBlenderモデル | **一部統合**（2Dで草が足元を隠すマスクは未使用。24項） |
| House | 入口で屋内へ、出口で屋外へ | 同じ（Phase 1〜3）。3Dの退室直後は家の正面を映す | **完全統合** |
| World Time | 朝10分・夜10分、実時間、保存 | 同じ `WorldTimeController` | **完全統合** |
| Life Log | 歩数・プレイ時間、保存 | 本編の `LifeLogController`。縦画面の案内中は時間を数えない | **完全統合** |
| Save | Life Log・World Time（AUTOの設定は保存しない） | 同じKey・同じ形式 | **完全統合** |
| Menu | AUTO・せいかつきろく | 本編のMenuに「ひょうじ 2D/3D」を追加 | **完全統合** |
| 看板・井戸・机などのMessage | あり | 同じ | **完全統合** |
| タッチ操作（D-pad・A） | あり | 同じ（横画面専用、縦は案内） | **完全統合** |
| 2Dのカメラ追従 | `CameraFollow2D` | POCの2D表示（共通の3Dをドット化） | **対象外**（表示方式が違う） |

## 40. 未統合機能一覧

| 機能 | 本編の部品 | 状態 | 理由・対応案 |
|---|---|---|---|
| 足音 | `PlayerFootstepAudio`（1歩ごとに `footstep_one_step.wav`） | 未統合 | 共通ワールドに「1歩始まった」の通知がまだない。Phase 5で、`StepStarted` を足してつなぐ |
| 2Dで草がﾊﾙｶﾁｬﾝの足元を隠す | `PlayerGrassOcclusion` | 未統合 | 共通ワールドの2Dは、3Dの草の奥行きで隠す方式。本編と同じ見え方にするかは要確認 |
| 2Dで草がカラスの足元を隠す | `CrowGrassOcclusion`（`crow_grass_mask.png`） | 一部統合 | 上と同じ |
| クッションの2Dの座り絵 | （本編にない） | 仮 | `bench_sit.png` を流用。専用の絵はCodexの担当 |
| 2Dの止まり木の位置 | `TreePerchAnchor2D` | 置き換え | 3Dの `PerchAnchor` を2Dでも使う（樹冠の上に見える） |
| 表示Modeの保存 | （本編にない） | なし | 必要なら別のKeyで追加できます |

## 41. Phase4IntegrationChecks

`Halka.Poc2D3D.EditorTools.Phase4IntegrationChecks.RunBatch` — **134件合格、0件失敗**（ログ `42a-phase4-checks.log`、全項目の一覧 `42a-phase4-pass-list.txt`）

| 分類 | 件数 | 主な内容 |
|---|---|---|
| 退室カメラ（1〜13） | 13 | 家が見える・隠れない、正面とPlayerが映る、入力まで保持、方向が反転しない（S・A・D）、なめらかに戻る、2D・通常の散歩・AUTOでは使わない |
| 3Dの木（14〜27） | 14 | 隠す木だけ40%、0.25秒、影が残る、隠さない木はそのまま、2本同時、離れると戻る、2D↔3Dを4回切り替えても壊れない、カラスとPlayerは透けない |
| スマホ（28〜41） | 14 | PCの縦長では案内なし、縦画面で入力・A・AUTO・プレイ時間が止まる、横に戻ると同じ状態から続く、2回繰り返しても残らない、4つの画面サイズでD-pad・A・Menuの位置 |
| AUTO（42〜60） | 19 | 屋外2D/3D（60秒・1マスずつ・家に入らない・Life Logと一致・入力で停止）、2D/3Dで同じ道、屋内2D/3D、夜、表示の切替、座っている間、OFF |
| 3Dのカメラ（61〜64） | 4 | 距離（屋外3.4・屋内3.6、角度は同じ）、北固定が標準、屋外でA/S/D/Wどれで歩いても北のまま、屋内も北のまま |
| カラス（65〜93） | 29 | 7つの行動、占有と解放、Blenderモデル、翼の開き方と羽ばたき、脚（地上は立つ・飛行は引き込む・着地は前へ）、切替で変わらない、木のAnchor、話しかけ（「カァ。」・音・離陸・二重にならない・空中は不可・距離）、屋内では出ない、屋外で作り直し、Playerと同じセルに入らない |
| 座る・家具（94〜115） | 22 | ベンチ2D/3D（左右のAnchor）、クッション2D/3D、座ったまま切替、キーでは歩かない、Aで立つ、歩数が増えない、机・ベッドは従来どおり、座っている間は他の物が反応しない |
| Life Log・Save・Time・Menu（116〜131） | 16 | 歩数の規則、保存Key・形式、同じWorld Time、夜の2D、AUTOの設定、MenuのAUTO・ひょうじ・せいかつきろく、2Dと3Dが同じセル、Debug Panelは `?debug=1` だけ |
| 保護（132） | 1 | 正式Map・Catalog 4ファイルのSHA-256 |
| 保存されたScene（133・134） | 2 | 起動時にカラスのモデルが1体だけ、木の半透明がSceneから復元される |

- Edit Modeの検査です。Play Modeで呼ばれる `Awake`・`OnEnable` などは、検査の中で呼んでいます。時計は検査が進めます（60秒を待たずに確かめるため）。
- スマホの項目（28〜41）は、Unity側の一時停止と配置の計算を確かめています。ページの回転案内とSafe Areaは、WebGL（DevTools）で確かめました（44項）。
- 実行中は、Life LogのPlayerPrefsと乱数の状態を保存し、終わったら元に戻します。
- 2026-10-18に追加した7件（脚3件・距離1件・北固定3件）は、どれも変更前のコードで失敗することを確かめました（北固定の「標準になっている」も、変更前は失敗します）。

## 42. 既存Regression

最終のコードで、すべて実行しました（ログ `HARUKA_WORLD_ver3.0_PHASE4_LOGS\42*`）。

| 検査 | 結果 |
|---|---|
| Phase4IntegrationChecks（新規） | 134件合格 |
| Phase3BuildingFurnitureChecks | 88件合格 |
| Phase 1 PocChecks | 102件合格 |
| Phase2EnvironmentChecks | 130件合格 |
| Phase21PresentationChecks | 60件合格 |
| Version30Checks（本編。Version18〜29の検査を含む） | 合格 |
| MapAuthoringChecks（Map Contract v0.6） | 合格 |
| MAP EDITOR Core Tests | 181件合格 |

**前提が変わった検査（Phase 3の検査）**
- `Phase3BuildingFurnitureChecks` の4件: ベンチ（左右）とクッションの「座る」Actionです。
  - Phase 3では、座るActionで「すわる。」が出ることを確かめていました。
  - Phase 4では指示書のとおり実際に座るので、「座った（座る物・座り方・同じセル）、Messageは出ない、そのあと立ち上がる」を確かめる内容に変えました。
  - 件数は88件のままです。机・ベッドなどのMessageの検査はそのままです。
- Phase 3の「退室後の表示切替」の検査:
  - この検査の環境では、カメラの「Mapが変わった」の通知を受け取っていないため、退室の構図は動きません。
  - そのため、今もPhase 3の通常のカメラの流れを確かめています。Phase 4の構図は、Phase4IntegrationChecks（1〜13）で確かめています。
- 「止まったら背後へ回る」カメラを確かめる検査（Phase 1 PocChecksの1件、Phase 2.1の入力の検査、Phase 3の屋内の壁の検査）:
  - 3Dのカメラの標準を北固定に変えたので、これらの検査は、確かめる動き（turn when idle）を指定してから実行するようにしました。
  - 検査の中身は変えていません。この動きは選べるモードとして残っており、今も合格しています。北固定は、Phase 4の検査61〜64で確かめています。
- 検査を緩めただけの変更はありません。

**その他**
- `Version30Checks` は、実行するとworktreeの `FirstDay.unity`・`GrassDecoration.prefab`・`ProjectSettings.asset` を書き換えます。毎回、元に戻しています（Commitしていません）。
- 検査や撮影の後、UnityがPOCのMaterialに最後のTextureを書き込むことがあります。これも元に戻しています。

## 43. PC WebGL

repo外のRelease Buildを、別に起動したheadless Chrome（Chrome 154、1280×720、AMD Radeon内蔵GPU）で操作しました。キーとマウスは、Chrome DevTools Protocolで送っています。画像は `HARUKA_WORLD_ver3.0_PHASE4_CAPTURES\webgl\pc\`、流れの記録は `PC_WEBGL_FLOW_NOTES.md` です。

- 下の30項目の流れは、2026-10-18の3点の変更の**前**のBuildで行いました（カメラ距離2.5、止まると背後へ回るカメラ）。
- 変更の後のBuildでは、次を確かめました。
  - 3Dで左・下・右・上へ歩いても、視点が北のまま（`nf_0`〜`nf_4`）
  - 新しい距離の見え方（`fb_3d_outdoor_distance_3.4.png`、スマホ `mobile/fb_mobile_3d_distance_3.4.png`）
  - POCパネルの「3D cam distance」の切替
  - 性能（45項）
  - カラスの飛行（WebGLでは近くから撮れなかったので、脚の形はUnityの撮影で確認）

| # | 指示書の流れ | 結果 | 画像 |
|---|---|---|---|
| 1 | 屋外2Dで起動 | 60fps、Menuボタン、カラスがSpawnの位置 | 01 |
| 2 | 手動で移動 | OK | 02 |
| 3 | 3Dへ切替 | Menu →「ひょうじ」→ 3D | 03a〜03c |
| 4 | 木へ近づく | 3D、Camera基準。S→Wで方向は反転しない | 04 |
| 5・6 | 木で隠れる → 半透明 | 木(5,1)が40%になり、向こうのﾊﾙｶﾁｬﾝが見える。カラスは透けない | 05_06 |
| 7・8 | 離れる → 元に戻る | 横へ1歩で不透明に戻る。カメラが背後へ回ると、また透ける | 07_08、07b、bursts |
| 9 | 家へ移動 | 2Dで移動 | 09 |
| 10 | 3Dで入室 | OK | 10a・10b |
| 11 | 屋内3Dで家具 | OK | 11 |
| 12 | ベンチ・クッション | クッション: 3Dで座る→2D→3Dでも座ったまま→クリックで立つ。ベンチ: 2Dで座る→3D→キーでは歩かない→クリックで立つ | 12a〜12g |
| 13 | AUTOをON | Menuで ON→OFF→ON | 13a〜13d |
| 14・15 | 60秒無操作 → AUTO開始 | 55秒は始まらず、64秒で開始（屋内3D。屋外2D/3D、屋内2Dも確認、18項） | 14、15a〜15n |
| 16 | 手動入力で停止 | 1歩で停止、4秒後も停止のまま | 16a・16b |
| 17〜19 | 退室 → 家の正面 → 次の移動で通常へ | OK（実時間の夜） | 17、18、19a、19b |
| 20 | カラスを探す | OK | 20a・20b |
| 21〜23 | 3Dで話しかける → 鳴き声とMessage → 離陸 | 「カァ。」、音の再生（WebAudioの記録）、離陸（3回成功） | 21_crow_talk_*、22、23 |
| 24 | 木に止まる | 2Dで確認。3Dの止まった姿はUnityの撮影で確認 | 24 |
| 25 | 飛行中に2D／3D切替 | 2D→3D→2D。飛行はそのまま続き、木に止まった | 25a〜25c |
| 26 | 地上へ戻る | 2D・3D | 26a・26b |
| 27 | Life Log | 「あるいたかず」「プレイじかん」 | 27 |
| 28 | 朝夜切替 | POCの「Time」で朝・夜を固定、実時間に戻す | 28a〜28d |
| 29 | 再読み込み | OK | 2930_reload_29 |
| 30 | 保存データ | PlayerPrefsのLife Logは、再読み込みの前後とも同じ値（36項）。画面の累計も同じ | 2930_reload_30 |

- 朝と夜: 実時間の時計で、木の半透明・入退室・座る・AUTO・退室カメラは夜に、カラス・Life Log・再読み込みは朝（固定）に行いました。Unityの撮影では、全項目を朝と夜の両方で撮っています。
- 移動の多い所（家まで、カラスまで）は、セル座標を見ながら操作するため `?debug=1` を付けました。ゲームの動きは同じです。
- PCの1024×576も、2D・3Dの表示を確かめました（`pc_1024x576_2d`・`3d`）。

**WebGLで見つけて直した不具合（Editorの検査では出なかったもの）**

| 不具合 | 原因 | 修正 | 追加した検査 |
|---|---|---|---|
| カラスの3Dモデルが2体 | Scene作成時のモデルが保存され、起動時にもう1体作っていた | `6d1b00d` | 126（保存されたSceneから起動） |
| Buildで3Dの木が半透明にならない | 半透明の処理がSceneに保存されていなかった | `c6d0333` | 127（保存されたSceneから復元） |
| スマホを縦にするとページが縮小 | 横の時のキャンバスの幅が残っていた | `d47a2ab` | WebGLで確認（44項） |

- どちらの検査も、修正前のコードで失敗し、修正後に合格することを確かめました。

## 44. Mobile Landscape WebGL

**DevToolsのエミュレーションで確認しました。物理スマートフォンでは確認していません。**
- 環境: headless Chrome 154。844×390、DPR 2、スマホとして表示（mobile）、タッチ有効、画面の向きのエミュレーション
- 操作: タッチイベント（D-pad・Aボタン・Menu）
- 画像: `HARUKA_WORLD_ver3.0_PHASE4_CAPTURES\webgl\mobile\`、`sheets/08_mobile_landscape.png`・`09_portrait_guide.png`

| 項目 | 結果 | 画像 |
|---|---|---|
| 2Dの移動 | D-padで4方向 | m02 |
| 3Dの移動 | D-padでCamera基準 | m07 |
| D-pad | 左下。押した方向へ歩く | m01 |
| Aボタン | 右下。看板「ここは HALKA WORLD。」、カラス「カァ。」、クッション・ベンチに座る・立つ | m03、m16、m11・m12、m18〜m20 |
| Menu | AUTO・ひょうじ・せいかつきろく | m04、m05b |
| Life Log | 表示される | m05 |
| 家の入退室 | 3Dで入室、退室直後は家の正面 | m08〜m10、m13・m14 |
| 屋内のカメラ | ﾊﾙｶﾁｬﾝと家具が見える | m09・m10 |
| 木の半透明 | 3Dで40% | m15 |
| カラス | Aで話しかけ、離陸 | m16_crow_talk_* |
| ベンチ・クッション | 座る・切替・立つ | m11・m12、m18〜m20 |
| 朝夜 | 朝・夜とも見分けられる | m27〜m30 |
| 回転案内 | 縦で表示 | m21 |
| 縦→横→縦→横 | 位置・向き・歩数は同じ。最後の横ですぐ歩ける | m22・m23、m24a〜m24d |
| 縦の間のプレイ時間 | 64秒のうち縦の60秒は数えない（+4秒） | m31・m32 |
| Safe Area | 左右47・下21の時、キャンバスがその内側に入る | m25 |
| 16:9（640×360） | D-pad・A・Menuが角に入る | m26 |

- 動画07（スマホ横画面の操作、縦→横を含む）
- 横画面のUIは読みやすく、ボタンが画面の端で切れることはありませんでした。

## 45. Performance

headless Chrome（AMD Radeon内蔵GPU、60Hz）上のRelease WebGLで、Phase 3とPhase 4を同じ手順で続けて測りました。詳細: `webgl\perf\PERFORMANCE_NOTES.md`、ログと画像も同じフォルダです。

- 手順: 起動 → 屋外2D → 3D → 2Dで家へ歩いて入室 → 屋内2D → 3D（Phase 3と同じ）
- FPS・Frame Time: 5秒間の `requestAnimationFrame` の間隔
- Draw Calls・SetPass・三角形・Memory: POCパネルの値（Unityの `ProfilerRecorder`）
- 時刻は、両方とも朝に固定しました。

**PC（1280×720、DPR 2、Unity画面2048×1152、POCパネルを開いた状態＝Phase 3と同じ条件）**

| 条件 | FPS 3 → 4 | Frame Time（4）中央値／95%／最大 | Draw Calls | Batches | SetPass | Tris/frame | Used Memory |
|---|---|---|---|---|---|---|---|
| 屋外 2D | 60 → 60 | 16.7／16.8／16.9 ms | 79 → 103 | 78 → 102 | 75 → 99 | 47,332 → 47,558 | 16.1 → 16.7 MB |
| 屋外 3D | 60 → 60 | 16.7／16.8／17.1 | 102 → 125 | 81 → 104 | 71 → 94 | 27,636 → 27,858 | 16.2 → 16.7 MB |
| 屋内 2D | 60 → 60 | 16.7／16.8／17.1 | 81 → 104 | 78 → 101 | 75 → 98 | 10,200 → 10,406 | 16.3 → 16.6 MB |
| 屋内 3D | 60 → 60 | 16.7／16.8／17.1 | 90 → 113 | 84 → 107 | 72 → 95 | 12,536 → 12,742 | 16.4 → 16.7 MB |

- Draw CallsとSetPassの+23は、ほとんどが画面のUIです。Phase 4では、本編のMenuボタン・バージョン表示と、POCパネルの行が増えました。開いたPOCパネルだけで約33回の描画があります（屋外2D: 開いた状態103、畳んだ状態70）。
- 世界の描画の違いは、三角形で+206〜226です（カラスとUI）。

**PC（DPR 2、POCパネルを畳んだ状態。Phase 4の新しい条件）**

| 条件 | FPS | Frame Time 中央値／95%／最大 | Draw Calls | SetPass | Tris | Memory |
|---|---|---|---|---|---|---|
| 屋外 2D（参考） | 60 | — | 70 | 66 | 46,354 | 16.5 MB |
| 3D Tree Fade中（木1本が半透明） | 60 | 16.7／16.8／16.9 | 88 | 65 | 32,352 | 16.7 MB |
| 3D Crow（3秒×8回、うち1回は飛行中） | 8回とも60 | 16.7／16.8／16.9以下 | 97〜102 | 61 | 35,636〜36,776 | 16.5〜16.7 MB |
| 3D AUTO中 | 60 | 16.7／16.8／16.9 | 104 | 61 | 30,854 | 16.8 MB |

**スマホ横画面（844×390、DPR 2、mobile＋タッチのエミュレーション、Unity画面1247×576）**

| 条件 | FPS 3 → 4 | Frame Time（4）95%／最大 | Draw Calls（4） | SetPass（4） | Tris（4） | Memory（4） |
|---|---|---|---|---|---|---|
| 屋外 2D | 60 → 60 | 16.8／16.9 ms | 79 | 75 | 46,434 | 16.7 MB |
| 屋外 3D | 59.8 → 59.8 | 16.8／16.8 | 111 | 71 | 32,164 | 16.6 MB |
| 屋内 2D | 60 → 59.8 | 16.8／16.9 | 80 | 74 | 9,290 | 16.8 MB |
| 屋内 3D | 59.8 → 60 | 16.8／16.9 | 89 | 71 | 11,636 | 16.8 MB |
| 3D Tree Fade中 | — → 59.8 | 16.8／17.0 | 100 | 75 | 33,930 | 16.7 MB |
| 3D Crow（3秒×8回） | — → 8回とも60 | 16.8／17.1以下 | 107〜112 | 71 | 35,720〜36,858 | 16.7〜16.8 MB |
| 3D Crow 飛行中 | — → 60 | 16.8／16.8 | 107 | 71 | 35,730 | 16.8 MB |
| 3D AUTO中（カラスも飛行中） | — → 59.8 | 16.8／16.8 | 113 | 71 | 38,460 | 16.8 MB |

- Phase 3のスマホ横画面では、POCパネルが畳まれて統計の行がないため、Draw Calls などは測れませんでした（推測で埋めていません）。
- Frame Timeの中央値は、すべての計測で16.7msでした（60Hzの上限で止まっています）。

**Build Size（非圧縮）**

| | Phase 3 | Phase 4 | 差 |
|---|---|---|---|
| .data | 4,605,956 | 4,702,998 | +97,042（カラスのモデル、半透明のShader、Scene） |
| .wasm | 24,555,068 | 24,620,560 | +65,492（コード） |
| .framework.js | 431,885 | 431,905 | +20 |
| .loader.js | 26,982 | 26,982 | 0 |
| 合計 | 29,619,891 | 29,782,445 | **+162,554（+0.55%）** |

**変更の後（カメラ距離3.4/3.6、2026-10-18）**

上の3D条件を、同じ手順で測り直しました（`p4b_*`）。

| 条件 | FPS | Draw Calls | SetPass | Tris/frame | Memory |
|---|---|---|---|---|---|
| PC DPR 2 屋外3D | 60 | 125 → 133 | 94 → 96 | 27,858 → 32,736 | 16.7 → 16.8 MB |
| PC DPR 2 屋内3D | 60 | 113 → 112 | 95 → 97 | 12,742 → 13,396 | 16.7 → 16.7 MB |
| スマホ横 屋外3D | 59.8 | 111 → 108 | 71 → 70 | 32,164 → 32,536 | 16.6 → 16.7 MB |
| スマホ横 屋内3D | 60 | 89 → 86 | 71 → 71 | 11,636 → 12,212 | 16.8 → 16.7 MB |

- 景色が多く入る分、三角形が増えました（PCの屋外3Dで約+4,900）。FPSは変わりません。
- PCのDraw Calls・SetPassには、POCパネルに足した行の分（約+2）が含まれます。
- 最終のBuild Sizeは29,796,980 bytes（Phase 3から +177,089、+0.60%）です。

- 測っていないもの: GPU・CPUの負荷の内訳、物理スマホでの値。

## 46. 比較画像

場所: `C:\Users\owner\Documents\ChatGPT\HARUKA_WORLD_ver3.0_PHASE4_CAPTURES\`

| シート（`sheets/`） | 内容 |
|---|---|
| `01_exit_camera_old_new_after.png` | 退室直後の旧カメラ／新カメラ／次の移動の後（朝・夜、WebGL） |
| `02_tree_fade_3d_2d.png` | 3D Tree Fade OFF／ON、2D Tree Fade（朝・夜、WebGL・スマホ） |
| `03_crow_blender_model.png` | Crow Blender 正面・背面・東・西・斜め・上・翼を上げた形、元のドット絵 |
| `04_crow_3d_states.png` | Crow 3D 地上待機・歩行・離陸・飛行・止まり木・着地、翼の3つの形 |
| `05_crow_2d_and_webgl.png` | Crow 2D（地上・飛行・木の上）、WebGLの話しかけ・離陸・切替・着地 |
| `06_bench_cushion_sit.png` | Bench Sit 2D/3D、Cushion Sit 2D/3D、元の `bench_sit.png`、WebGL・スマホ |
| `07_auto_2d_3d.png` | AUTO 屋外2D・屋外3D・屋内3D（55秒・64秒）、入力で停止 |
| `08_mobile_landscape.png` | スマホ横画面2D/3D、A・Menu・Life Log・カラス・屋内・退室・Safe Area・16:9・PC 2サイズ |
| `09_portrait_guide.png` | スマホ縦画面の回転案内、縦→横→縦→横 |
| `10_morning_night.png` | 朝夜の比較（PC・スマホ、2D・3D） |
| `11_camera_distance_3d.png` | 3Dカメラの距離の比較（2.5／3.0／3.4／3.8、屋外2か所・スマホ横・屋内） |
| `12_crow_legs_3d.png` | カラスの脚（立つ／飛行は引き込む／着地は前へ。横・正面・後ろ上から、ゲーム内） |

- 元の画像:
  - `unity/`: Unityで撮影（夜は `_night`。カラスの翼の形 `crow_3d_pose_*`）
  - `webgl/pc/`、`webgl/mobile/`、`webgl/perf/`: WebGL
  - Blenderのプレビュー: `blender/exports/previews/phase4/`（repo内）

## 47. 動画

場所: `HARUKA_WORLD_ver3.0_PHASE4_CAPTURES\videos\`（H.264、30fps）

| # | ファイル | 内容 | 長さ | 撮り方 |
|---|---|---|---|---|
| 1 | `01_exit_camera_house_front.mp4` | 3Dで退室 → 家の正面 → Sで通常のカメラへ | 10秒 | A |
| 2 | `02_3d_tree_see_through.mp4` | 3Dで木が半透明になる・戻る・カメラが回ってまた透ける | 9秒 | A |
| 3 | `03_3d_crow_walk_fly_perch_land.mp4` | 3Dカラスの歩行・離陸・飛行・木への着地・離れる・着地 | 13秒 | **B** |
| 4 | `04_crow_2d_3d_switching.mp4` | 2D/3Dを切り替えながらカラスを見る（14回切替） | 47秒 | A |
| 5 | `05_auto_outdoor_2d_3d_indoor_3d.mp4` | AUTO（屋外2D・屋外3D・屋内3D。それぞれ無操作56.5秒からの13秒で、60秒で歩き始める瞬間を含む） | 41秒 | A |
| 6 | `06_bench_cushion_sit_2d_3d.mp4` | ベンチ（2Dで座る→3D→立つ）、クッション（3Dで座る→2D→3D→立つ） | 19秒 | A |
| 7 | `07_mobile_landscape_controls.mp4` | スマホ横画面（D-pad・A・Menu・Life Log・3D・縦→横） | 24秒 | A（エミュレーション） |
| 8 | `08_2d_3d_overall_play.mp4` | 2D/3D総合（2Dで歩く→3Dで木の半透明とカラス→2Dで家へ歩いて入室→3Dの屋内→3Dで退室・家の正面→2D） | 35秒 | A |

- **撮り方A:** WebGLの画面を、Chrome DevToolsの画面転送（`Page.startScreencast`）で、描かれたコマごとに受け取りました（約60fps、JPEG）。コマの時刻どおりに並べて30fpsの動画にしています。スクリーンショットをつなげた低fpsの動画ではありません。
  - 動画7は、縦の間のコマが縦長なので、横長の枠の中央に置いています。
- **撮り方B（動画3）:** 3Dのカラスを近くから追うため、Unity Editor（batch）で、共通のカラスを1秒30回動かし、その3D表示を1コマずつ描きました（`Phase4Capture.CrowVideoBatch`）。WebGLの画面ではありません。木に止まる時間は、動画のために短くしています（1.5秒）。WebGLのカラスは動画4と画像で確認しています。
- 動画5と7は、実時間の夜に撮りました。
- 動画1・2・4〜8は、2026-10-18の変更の前（カメラ距離2.5、止まると背後へ回るカメラ）に撮ったものです。動画3（Unity）は、新しい脚の動きで作り直しました。
- 音は入っていません。

## 48. Preview起動方法

Build: `C:\Users\owner\Documents\ChatGPT\HARUKA_WORLD_ver3.0_PHASE4_PREVIEW`（`README_POC.txt` あり。タイトルは「はるかわーるど ver3.0 Phase 4 Preview」）

```
cd C:\Users\owner\Documents\ChatGPT\HARUKA_WORLD_ver3.0_PHASE4_PREVIEW
python serve_poc.py --port 8894
```

- PC: http://127.0.0.1:8894/
- PCでスマホの操作ボタンを出す: http://127.0.0.1:8894/?touchControls=1
- POCパネル（開発用）: http://127.0.0.1:8894/?debug=1
- Unity Editorは不要です。
- 今もポート8894で起動したままにしています。
- Phase 1（8890）、Phase 2（8891）、Phase 2.1（8892）、Phase 3（8893）のBuildは、それぞれのフォルダにそのまま残しています。上書きしていません。

## 49. Git Commit

ブランチ `claude/ver3.0-2d3d-phase4`（Pushはしていません）:

`31adaaf`・`039e0d8`・`659405d`・`06441e7`・`47cfee4`・`6d1b00d`・`c6d0333`・`910d35a`・`9d2ae8e`・`d47a2ab`・`47b5fa0`・`0f8ab59`（内容は3項）、レポート `7f44e8d`、フィードバック対応:

| Commit | 内容 |
|---|---|
| `9366322` | カラスの飛行中の脚、3Dカメラの距離（3.4/3.6）、POCパネルの距離の行 |
| `4677870` | レポートの追補 |
| `a455d0b` | 3Dの視点を北固定に（標準）。背後へ回る検査はモードを指定 |
| `a956a5d` | レポートの追補 |
| このレポート | 本文への反映（このCommitのIDは報告文で示します） |

## 50. 最終Git状態

- Phase 4のworktree: レポートのCommit後、`git status` は空です。
- Phase 1・2・2.1・3のworktreeとブランチは変えていません（`6a5166c`／`7d5dbf9`／`cc72dc1`／`fcf8beb`）。
- 元の作業ツリー: 開始時と同じ2件（` M GrassDecoration.prefab`、`?? backup-v05-…`）だけです。
- ローカル `main`（`d039acf`）と `origin/main`（`4957aab`）は、開始時と同じです。

## 51. 本番環境の保護

- SHA-256（作業前と同じ）:
  - 正式Map・Catalog 4ファイル（`official-map-sha256-after.txt`）
  - Player Sprite 50ファイル（`player-sprite-sha256-after.txt`）
  - カラスのSpriteと音声 18ファイル（`crow-assets-sha256-after.txt`）
- 変えていないもの:
  - `Assets/Content`、ProjectSettings、Packages、WebGL Template、`halkaworld/`
  - `PlayerMover`・`PlayerSeatController`・`CharacterVisual`・`WorldTimeController`・`GameInput`、Save Key
  - Map JSON v4、MAP EDITOR v0.6
- 本編のScriptの変更は、このブランチの中だけです（3項）。本番のBuildには入っていません。
- 公開版ver2.9、ver3.0 Preview、GitHub Pages、公開HPは変えていません。
- mainへのMerge、Push、force pushはしていません。新しい有料ツールや外部サービスも使っていません。

## 52. 未解決事項

1. **物理スマートフォンでは確認していません。** 操作感、性能、向きの固定（`lock()`）、ノッチのSafe Areaは未確認です。
2. **音は耳で確かめていません**（headless）。WebAudioの記録で、話しかけの後に `crow_voice.mp3` が再生されることは確かめました（28項）。
3. **クッションの2Dの座り絵は仮です**（`bench_sit.png` の流用）。
4. **本編ではクッションに座れません。** Phase 4の共通ワールドだけ座れます。本編も同じにするかは確認が必要です（54項）。
5. **退室の構図:** 歩いて出た時のキーは、一度離すまで使いません。押しっぱなしで出ると、離して押し直すまで歩きません（5項）。
6. 足音と、2Dで草が足元を隠す表現は、まだ共通ワールドにつないでいません（40項）。
7. 2Dのカラスの止まり木の位置は、3Dの `PerchAnchor` を使っています。本編の2D用のAnchorとは少し位置が違う可能性があります（27項）。
8. WebGLでは、3Dで木に止まったカラスや、飛んでいるカラスの脚を近くから撮った画像がありません。Unityの撮影（`crow_3d_perched`、`12_crow_legs_3d`）と動画3で確認しています。
9. Phase 3の検査のうち4件（座るAction）の内容を、Phase 4の動きに合わせて変えました（42項）。
10. ブラウザで表示を切り替えると、それが入力になるので、AUTOは止まります（19項）。
11. 回転案内の文字は端末のFontです（手書き風のBitmap Fontに「横」「向」がないため）。デザインを合わせるなら、Codexに専用の画像を作ってもらう方法があります。
12. Draw CallsとSetPassは、POCパネルを開くと約33回増えます（POCパネルだけの問題です）。
13. 3Dのカメラを北固定にしたので、北を向いて歩く時はﾊﾙｶﾁｬﾝの背中、南へ歩く時は顔が見えます。木の半透明は、彼女の南側の木だけで起きます。
14. WebGLの画像の多くと、動画1・2・4〜8は、2026-10-18の変更の前に撮ったものです（43項・47項）。

## 53. Phase 5提案

1. **物理スマホ（iPhone・Android）での確認:** 操作感・性能・Safe Area・向きの固定。
2. **本編Sceneへの統合:** 2Dだけの本編Sceneを、共通ワールドに少しずつ置き換える。今回の窓口（`IAutoModeWorld`・`CrowBehaviourCore`）で、AUTOとカラスはすでに共通です。
3. **足音と草の表現:** 共通ワールドに `StepStarted` を足して、本編の足音をつなぐ。2Dで草が足元を隠す表現を、本編と同じにする。
4. **クッション:** 専用の2Dの座り絵（Codex）。本編でも座れるようにするかの決定。
5. **正式なﾊﾙｶﾁｬﾝの3Dモデル:** 届いたら、Phase 2.1の方式で差し替える。座るポーズも合わせて作り直す。
6. **HPのiframeへの統合:** 11項の親ページ側の対応（画面いっぱいのiframe、回転案内、Safe Area、全画面）。
7. **カラスの細かい動き:** 止まり木での向き変え、着地の跳ね、3Dでの草との前後関係。
8. 必要なら、表示Mode（2D/3D）を別のKeyで保存する。

## 54. ユーザーへの確認事項

1. **退室直後のカメラ:** 家の正面とﾊﾙｶﾁｬﾝの構図（距離・角度）でよいか。歩いて出た時のキーを一度離す必要があることを、許容できるか。
2. **3Dの木の半透明:** 40%・0.25秒（2Dと同じ）の見え方でよいか。
3. **カラスの3Dモデル:** 形・赤いマフラー・大きさ（高さ約0.41＝1マスの8割強）、羽ばたき、飛行中に脚を引き込む動きでよいか。
4. **3Dカメラの距離:** 屋外3.4・屋内3.6でよいか（POCパネルで2.5／3.0／3.8とも比べられます）。
5. **クッション:** 共通ワールドで座れるようにしたこと、2Dの絵が仮（ベンチの絵の流用）であることでよいか。本編でも座れるようにするか。
6. **座っている間:** 本編と同じく、方向キーでは立ち上がらず、Aで立ち上がります。この操作でよいか。
7. **回転案内:** 「スマホを横向きにしてください」とアイコンのデザインでよいか。
8. **Menuの「ひょうじ 2D／3D」:** 名前と位置でよいか。
9. **足音・草の表現**（40項）を、次のPhaseで優先してつなぐか。
10. Phase 5と正式公開には、承認をいただいてから進みます。

---

## 付録: Previewへのフィードバック対応の経緯（2026-10-18）

内容は本文（6項・22項・41項〜47項）に反映済みです。ここには、変更の経緯と、その時点の値を残しています。Commit `9366322`・`a455d0b`（Pushなし）。

### A. カラスの飛行中の脚

- **以前:** 飛んでいる時も、脚は立っている時と同じ（真下へまっすぐ）でした。
- **今:** 状態に合わせて脚を動かします（`PocCrowView3D`）。

| 状態 | 脚 |
|---|---|
| 地上（待機・歩き）、木の上 | 立つ（真下）。歩く時は交互に振る |
| 離陸・飛行・木から離れる | 0.25秒で、後ろへ85°たたみ、少し小さくして体の下へ引き込む（羽毛の中にしまう見え方） |
| 地上への着地 | 0.25秒で前へ30°出す（地面をつかむ形）。着地したら立つ |

- 画像: `sheets/12_crow_legs_3d.png`（立つ／飛行の打ち下ろし・打ち上げ／着地。横・正面・後ろ上から、ゲーム内）
- 動画: `videos/03_3d_crow_walk_fly_perch_land.mp4` を新しい動きで作り直しました（撮り方は47項のBと同じ）。
- WebGLでも動かしましたが、飛んでいるカラスを近くから撮ることはできませんでした。脚の形は、Unityの撮影で確認しています。
- 検査を3件追加しました。
  - 地上では、脚が真下にあり、足が地面に着いている
  - 飛行中は、脚が上がり、つま先が前に出ない
  - 着地の時は、脚が前に出て、着地した後は立つ
  - 3件とも、修正前のコードでは失敗します。

### B. 3Dカメラの距離

- **以前（Phase 3と同じ）:** ﾊﾙｶﾁｬﾝから、屋外2.5・屋内2.9の距離でした。
- **今:** 屋外 **3.4**・屋内 **3.6** です。角度（屋外26°・屋内52°）は同じです。
  - 4つの距離（2.5／3.0／3.4／3.8）で撮り比べました。「もう少し離す」に合うものとして3.4を選びました（`sheets/11_camera_distance_3d.png`）。
  - ﾊﾙｶﾁｬﾝの画面の大きさは、以前の約7割です。周りの景色が多く入ります。
- POCパネル（`?debug=1`）に「3D cam distance」の行を足しました。押すたびに、次の順に切り替わります。比べて決めていただけます。
  - 3.4/3.6（新しい標準）
  - 2.5/2.9（Phase 3）
  - 3.0/3.3
  - 3.8/4.0
- 退室直後のカメラ（家の正面）の距離は、別に計算するので変わりません。構図が終わると、新しい距離へなめらかに移ります。
- 検査を1件追加しました（屋外3.4・屋内3.6、角度は同じ。修正前のコードでは失敗します）。
- WebGLで確認:
  - PC: `webgl/pc/fb_3d_outdoor_distance_3.4.png`
  - スマホ横: `webgl/mobile/fb_mobile_3d_distance_3.4.png`
  - パネルの行で、距離が切り替わることも確かめました。

**性能（新しい距離、Phase 4の以前の値 → 今）**

| 条件 | FPS | Draw Calls | SetPass | Tris/frame | Memory |
|---|---|---|---|---|---|
| PC DPR 2 屋外3D | 60 → 60 | 125 → 133 | 94 → 96 | 27,858 → 32,736 | 16.7 → 16.8 MB |
| PC DPR 2 屋内3D | 60 → 60 | 113 → 112 | 95 → 97 | 12,742 → 13,396 | 16.7 → 16.7 MB |
| スマホ横 屋外3D | 59.8 → 59.8 | 111 → 108 | 71 → 70 | 32,164 → 32,536 | 16.6 → 16.7 MB |
| スマホ横 屋内3D | 60 → 60 | 89 → 86 | 71 → 71 | 11,636 → 12,212 | 16.8 → 16.7 MB |

- 景色が多く入る分、三角形が増えました（PCの屋外3Dで約+4,900）。FPSは変わりません。
- PCのDraw CallsとSetPassの+2前後には、POCパネルに足した行の分が含まれます。
- Frame Timeの中央値は16.7msのままです（最大18.3msが1回）。

### 検査とBuild

| 検査 | 結果 |
|---|---|
| Phase4IntegrationChecks | 131件合格（127件＋追加4件。この後、Cで3件を足して134件） |
| Phase 3 ／ Phase 1 ／ Phase 2 ／ Phase 2.1 | 88 ／ 102 ／ 130 ／ 60件合格 |
| Version30Checks、MapAuthoringChecks、MAP EDITOR 181件 | 合格 |

- ログは `HARUKA_WORLD_ver3.0_PHASE4_LOGS*`〜`33*` です。
- Previewは同じフォルダに作り直しました（ポート8894。READMEにも追記）。

### 注意: 以前の画像・動画について

- `unity/` の画像と比較シートは、新しい距離と脚で撮り直しました。
- WebGLの画像（`webgl/pc`・`webgl/mobile`・`webgl/perf`）と、動画1・2・4〜8は、変更の前（距離2.5/2.9）に撮ったものです。新しい距離のWebGLの画像は、上に挙げた `fb_*` です。

### C. 3Dの視点を、向きで変えない（2026-10-18、Commit `a455d0b`）

- **以前（Phase 2.1〜）:** 横を向いて止まると、カメラがﾊﾙｶﾁｬﾝの背後へ回り込みました。
- **今:** 3Dのカメラは、いつも南側から北を見ます（2Dと同じ向き）。屋外でも屋内でも、ﾊﾙｶﾁｬﾝがどちらを向いても、歩いても、視点は変わりません。
  - W／A／S／Dは、いつも北／西／南／東です（2Dと同じ）。
  - 退室直後の家の正面の構図も北向きなので、構図が終わっても向きは変わりません。
  - 屋内は、手前（南）の壁をいつも低く切ります。
- POCパネル（`?debug=1`）の「3D cam follow」で、以前の動きと比べられます。押すたびに、次の順に切り替わります。
  - north fixed（新しい標準）
  - turn when idle（Phase 2.1〜3の動き）
  - fixed at switch
  - facing（Phase 1の動き）
- 検査:
  - Phase 4に3件を追加しました（標準が北固定、屋外でA/S/D/Wのどれで歩いても視点が北のまま、屋内も同じ）。
  - Phase 1・2.1・3の検査のうち、「背後へ回る」動きを確かめる検査（Phase 1の1件、Phase 2.1の入力の検査、Phase 3の屋内の壁の検査）は、その動きのモードを指定してから確かめるようにしました。内容は変えていません。この動きは、選べるモードとして残っています。
  - 全体: Phase4IntegrationChecks 134件、Phase 3 88件、Phase 1 102件、Phase 2 130件、Phase 2.1 60件、Version30Checks、MapAuthoringChecks、MAP EDITOR 181件、すべて合格（ログ `40*`〜`43*`）。
- WebGL: 3Dで左・下・右・上へ歩き、2.5秒ずつ待っても、景色の向きは同じでした（`webgl/pc/nf_0_start.png`〜`nf_4_after_W_north.png`、実時間の夜）。
- 注意: 2項（Tree Fade）のWebGLの手順（木の横で西を向くと、背後へ回ったカメラとの間に木が入る）は、以前の動きを前提にしています。今は、カメラとの間（南側）に木がある時に半透明になります。
