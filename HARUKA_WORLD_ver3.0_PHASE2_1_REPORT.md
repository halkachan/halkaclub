# はるかわーるど ver3.0 Phase 2.1 レポート：3D操作・2D輪郭線・仮ﾊﾙｶﾁｬﾝ

作業日: 2026-10-14（日本時間）／担当: Claude
ブランチ: `claude/ver3.0-2d3d-phase2-1`（Phase 2 `claude/ver3.0-2d3d-phase2` の `7d5dbf9` から分岐）
worktree: `C:\Users\owner\Documents\ChatGPT\HALKA_HP_2D3D_PHASE2_1`

**結論**: 3点とも実装し、ローカルWebGL（PC相当・スマホ相当）で操作して確認しました。

1. **3Dの操作をカメラ基準にした。** 押している間は方向が変わらない。S長押しでも往復しない。A/Dで円を描かない。
2. **2Dにドット単位の輪郭線を付けた。** 低解像度の画像の段階で描き、整数倍で拡大している。
3. **既存Spriteを参考に、ﾊﾙｶﾁｬﾝの簡易ローポリ「仮」モデルを作った。** 後から差し替えられる構造にしてある。

ゲームのGrid・当たり判定・Interaction・時間計算・正式Map・Player Spriteは変えていません。公開もしていません。

---

## 1. 開始時のGit状態

| 項目 | 値 |
|---|---|
| Phase 2 worktree | `HALKA_HP_2D3D_PHASE2`、`claude/ver3.0-2d3d-phase2` @ `7d5dbf9`、変更なし |
| Phase 1 worktree | `claude/ver3.0-2d3d-phase1` @ `6a5166c`、変更なし |
| 元の作業ツリー | `HALKA HP`、`codex/ver3.0-preview` @ `d3f81ac`。` M GrassDecoration.prefab`、`?? backup-v05-20261006-174557/`（開始時からある2件） |
| origin/main / ローカル main | `13462fe` ／ `d039acf` |
| Phase 2.1 | 新しいworktree `HALKA_HP_2D3D_PHASE2_1` に `claude/ver3.0-2d3d-phase2-1` を作成。Libraryはコピー |

## 2. Backup

repo外の `C:\Users\owner\Documents\ChatGPT\HARUKA_WORLD_ver3.0_PHASE2_1_BACKUP_20261014`（244MB）に保存しました。

- 全refのbundle（verify済み）
- 各worktreeのstatus、元の作業ツリーのdiff
- Phase 2のWebGL Build一式
- `files/`: POC一式、Blender、tools、正式Map、Player Sprite（`Content/Character`）、Player・Camera・Input Script、ProjectSettings、Phase 2レポート
- 正式MapのSHA-256、Player Sprite 50ファイルのSHA-256

## 3. 3D操作の新しい仕様

3Dでは、W／↑＝カメラから見て前、S／↓＝後ろ、A／←＝左、D／→＝右です。スマホのD-padも同じです。

- 4方向・1マス・0.18秒という移動の仕様はそのままです。新しい操作ボタンは追加していません。
- 2Dは従来どおり、方角固定（W＝北）です。
- パネルの「3D keys」で、方角固定（Phase 1の案A）にも切り替えて比べられます。

## 4. カメラ基準の移動計算

- カメラは「基準方角」（北・東・南・西のどれか）を持ちます。Playerの向きとは別の値です。
- キーを押した瞬間に、`GridSpace3D.CameraRelative(入力, 基準方角)` で東西南北のどれか1方向に決めて保持します（`PocInput.Resolve`）。キーを離すか、別のキーに変わるまで再計算しません。
- 移動は既存の `GridStepMotion`（`SharedWorldSession.Tick`）のままです。歩行中は方向を読まないので、一歩の途中で方向が変わることはありません（検査で確認）。
- カメラの向き（yaw）は、最も近い東西南北に丸めて扱います（44°→北、46°→東 など）。

## 5. S長押し問題への対策

- 「Playerの向き」と「カメラの基準方角」を分けました。
- Sを押すと、Playerは振り向いてカメラ側へ歩きます（向きは南）。カメラの基準方角は変わりません。そのため次のSも同じ方向になり、往復しません。
- Playerがカメラ側を向いて止まっても、カメラは180°回りません。
- 確認:
  - 検査：S長押しで4歩まっすぐ、Sを3回押して3歩
  - WebGL：S長押しの間、カメラは北向きのまま後退した
  - 動画：34項

## 6. A/D長押し問題への対策

- 押している間は方向とカメラの基準方角が固定なので、横へまっすぐ進みます。円は描きません。
- キーを離して、Playerが止まってから0.35秒たった時だけ、横を向いたPlayerの背後へカメラが回り込みます（「turn when idle」）。
- カメラが回っている最中にWを押すと、新しい基準方角（回り込む先）へ進みます。押す前に画面で回り始めているので、結果は予測できます。
- A/Dを素早く交互に押すと、西→東→西で元のマスの周りを往復し、ずれません（検査・WebGLとも）。

## 7. Camera Followの変更点

`PocThirdPersonCamera` の追従方式（パネル「3D cam follow」で切り替え）:

| 方式 | 内容 |
|---|---|
| turn when idle（初期値） | 上の6項のとおり |
| fixed | 3Dに入った時とMapが変わった時以外は回らない |
| facing（Phase 1） | 常に向きの背後。Sを押すと視点が反転する。比較用に残した |

- 3Dに入った時とMapが変わった時は、Playerの背後へ即座に合わせます（Snap）。
- 距離・高さ・平滑化の値はPhase 1のままです。

## 8. 2D操作の維持

- 2DではWASD・矢印・D-padとも、方角固定のままです（検査とWebGLで確認。WebGL：Sで南、Aで西）。
- **切替直後にキーが押されたままの場合:** 進行中の一歩だけを終え、キーを一度離すまで新しい一歩を始めません（`PocInput.RequireRelease`）。切替でCell・向き・歩数・Mapは変わりません（20回切替の検査、WebGL）。
- **AUTO:** カメラ基準の入力を通らず、Grid上の方角で動きます。3Dのカメラが東を向いていても、経路は変わりません（検査）。

## 9. 輪郭線方式の比較

| 方式 | 長所 | 短所 | 判断 |
|---|---|---|---|
| A. Inverted Hull（法線方向に膨らませた裏面） | 実装が単純 | 線の太さが最終解像度で決まり、低解像度化で途切れたり太さがむらになったりする。同じObjectの重なりにも線が出る | 不採用 |
| B. Depth / Normalによる縁検出 | Objectを区別しなくても描ける | 地面の起伏や面の境目すべてに線が出やすい。Object単位の外周にならない | 不採用 |
| **C. Object IDマスク＋低解像度での縁検出** | Object単位の外周だけを、低解像度の1〜2pxで描ける。拡大後もドットのまま。Objectごとに強さを変えられる | マスク描画が1パス増える | **採用** |

## 10. 採用した輪郭線方式

1. 2Dカメラが、カラー画像と同じ行列で、IDマスクを低解像度RTに描きます（`HALKA/POC/OutlineMask`）。中身は、R＝Object ID、G＝輪郭の強さ、BA＝深度16bitです。
2. Object IDと強さは、各メッシュのUV2に書き込みます（`PocOutlineTag`）。マテリアルのプロパティではなく頂点データなので、Phase 2の静的バッチ化を崩しません。
3. 合成シェーダー（`HALKA/POC/OutlineComposite`）が、低解像度のまま1画素ずつ判定します。「隣が別のObjectで、自分より手前にある」画素を輪郭色にします（外周線）。その後で、既存と同じ整数倍・Pointの拡大をします。
4. 内側の線（初期値は「弱」）: 同じObjectの中で、手前の面との色差が大きい境目だけに引きます（樹冠と幹、花弁と芯など）。似た色の面の境目には引かないので、ポリゴンの面ごとに線が入ることはありません。
5. 輪郭を付ける対象: 木・花・石・仮House・仮Player・仮の箱（家具・看板など）。地面と道には付きません（検査で、輪郭が出るのは輪郭対象の物の近くだけであることを確認）。草は初期値で付けず、「弱」も比較用に用意しました。
6. 3Dでは2Dカメラ自体が止まるので、輪郭処理は一切動きません（検査）。
7. UI（IMGUIのMenu・Message・D-pad・A・Version・パネル）は、画像の合成後に描かれるので、輪郭の対象になりません。遊びメモ書きのビットマップFontも変えていません。

## 11. 輪郭線の色・太さ

- 初期値は1px・`#252B39`（暗い青灰）です。`#3A2A22`（濃い茶）と `#000000` も比べました（`sheets/05`）。黒は強すぎ、茶は木の幹となじむものの花や石では浮きました。そのため現行Spriteの輪郭に近い `#252B39` を初期値にしています。
- 2pxは遠くからの視認性が上がりますが、16px/マスでは小さい花が線で潰れ気味です（`sheets/04_outline_flower`）。

## 12. 16pxと32pxの比較

同じMap・同じカメラ中心（Cell 5,−1）・同じ見える範囲（縦18マス）で比べました（`sheets/03`、`sheets/04_*`）。

| 密度 | 輪郭なし | 1px | 2px |
|---|---|---|---|
| 16px/マス（RT 512×288 ×2） | 0画素変化 | 2,995画素に線 | 5,139画素に線 |
| 32px/マス（RT 1024×576 ×1） | 0画素変化 | 5,488画素に線 | 9,580画素に線 |

16pxに1pxの輪郭を付けると、現行Spriteに最も近い「ドット絵らしさ」になりました。32pxでは線が細く、密で上品な絵になります。**最終決定はしていません**（36項）。

## 13. Pixel Shimmerの検証

- 輪郭は低解像度の段階で描くので、1px単位でカメラを動かす仕組み（Phase 1）がそのまま効きます。検査で、歩行中もカメラ位置が1px単位であることを確認しました。
- WebGLのスマホ相当（RT 130×282 ×6）で、歩いている間の30フレームを撮って比べました。カメラが動いた18ペアすべてで、静止した背景と輪郭の不一致は0pxでした（`LOGS/19-shimmer-mobile-2d-outline.txt`）。
- WebGLでも上下は反転していません（合成はBlit経由）。

## 14. 仮Playerの設計

`haruka_placeholder`（**仮モデル。正式なﾊﾙｶﾁｬﾝではありません**）。

- 大きさ: 約2頭身、高さ0.956。足元が原点で、正面は南です（他のアセットと同じ規約）。
- 再現した特徴（既存Spriteから読み取った配色を使用）:
  - 髪: クリーム色の金髪（#F1E2BC）。側面と背面の下側が青（#4B7AE8／#3C5FBE）
  - 背面: 黒い稲妻形の分け目
  - 頭飾り: 頭頂の黒い角状の飾り2本
  - 髪飾り: 本人の右側（−X）、額の横に白い四角い髪飾り（黒い縁と「H」の線）
  - 毛束: 本人の左側にカールした毛束
  - 前髪: 角ばった毛束を4本と、額を横切る1本
  - 顔: 青い目（上まぶたは黒、白いハイライト）、目の下にピンク（#FE1B6D）
  - 服: 黒い上着、青い襟、白と青の胸章。黒いスカートに青い裾
  - 腕と脚: 黒い袖と白い手。白い脚に黒いブーツ（底は青）
- 構成: 本体（頭・髪・胴・スカート）と、`ArmL`／`ArmR`／`LegL`／`LegR`（肩と股関節が回転の中心）。976 tris、マテリアル1つ。
- **2D表示だけの補正**（Presentation Metadataで調整できる）:
  - `pixelViewFaceCamera` = 1: 足元を軸に40°起こして、顔を2Dカメラへ向ける。50°の見下ろしのままだと頭頂しか見えないため、現行の手描きSpriteのように正面が見えるようにする。
  - `pixelViewScale` = 0.82: 起こしたぶん縦に伸びて見えるので縮める。
  - `pixelViewDepthBias` = 0.35: 視線方向にずらす。正射影なので画面上の位置は変わらず、北側の木の幹などより手前に描かれる。
  - 2DではPlayerの影を消す（Spriteには影がない）。
  - どれも描画だけの処理で、PlayerのセルとActorの根元の位置は変えません（検査）。

## 15. 元Spriteとの比較

`sheets/01_player_original_vs_placeholder.png` に並べました。元Sprite（4方向）、Blender、Unity 3D（4方向）、2D 16px・32px です。

- 金髪と下側の青、角状の飾り、白い髪飾り、青い目とピンク、黒い服と青い縁は、一目でﾊﾙｶﾁｬﾝと分かる程度に再現できました。
- 違い:
  - 前髪と頬のラインがSpriteより角ばっている
  - 側面から見ると、横顔より髪の塊が目立つ
  - 横向きのとき、脚が1本に重なって見える
  - 背面の青い髪が、Spriteよりまっすぐな形

## 16. Blender Source

- `blender/sources/phase2_1/haruka_placeholder.blend`
- 生成: `blender/scripts/build_haruka_placeholder.py`（共通の補助は `halka_lowpoly.py` に `cylinder_between` を追加）
- プレビュー: `blender/scripts/render_haruka_placeholder_previews.py` → `blender/exports/previews/phase2_1/haruka_placeholder_{south_front,east,north_back,west,top,game_2d_angle,three_quarter}.png`
- Manifest: `blender/exports/phase2_1_haruka_placeholder_manifest.json`（「TEMPORARY placeholder」と明記）
- Phase 2の自然物のBlender Sourceとスクリプトは変更していません（22項）。

## 17. FBX Import

`unity-project/Assets/POC2D3D/Models/Phase2_1/haruka_placeholder.fbx`。Phase 1・2と同じImport規則で取り込みました。

検査で確認したこと:
- 5部品とも共有マテリアル
- 4つの手足の節点がある
- 足元が原点、高さ0.9〜1.0、Scale 1
- 顔（目の下のピンク）が南側、白い髪飾りが−X（本人の右）

## 18. 仮PlayerのAnimation

- RigやAnimatorは使わず、`PocActorView` が `ArmL/ArmR/LegL/LegR` を前後に振ります（±26°、2マスで1周期）。歩いている時だけ振り、止まると戻ります。待機中は動かしません。
- 歩行の0.18秒、論理セル、補間は変えていません（検査：一歩の途中でもセルは変わらず、0.18秒後に1マス進む）。
- 本格的な待機と歩行のAnimationは、正式モデル（Rig付き）で行う想定です。

## 19. 2Dでの仮Player表示

- 14項の補正により、2Dで正面の顔（青い目・ピンク）・背面の青い髪と分け目・左右の向きが読めます（`sheets/01`の2D 16px／32px）。
- 輪郭線（1px）も付きます。歩いている間、手足が振れます（3Dの手足と同じ）。
- 16px/マスでは顔が数画素なので細部は粗く、32px/マスでは目とピンクまで読めます。

## 20. 3Dでの仮Player表示

- 3Dはローポリのまま、起こす補正なしで表示します。
- 三人称視点では背面（金髪・青い下側・稲妻の分け目）が見えます。左右へ動くと横を向き、0.09秒で滑らかに向きを変えます。
- 4方向とも、顔の向きがGridの向きと一致します（検査）。
- 位置は共通のセルと完全に一致します。

## 21. 正式モデルへの差し替え方式

`PocPresentationCatalog.Player`（`PocActorPresentation`）の1件を変えるだけで差し替えられます。

| 項目 | 内容 |
|---|---|
| model | FBXまたはPrefab |
| yawOffsetDegrees | 正面の向きの補正（Blenderで南向き正面なら180） |
| pivotOffset／uniformScale | 原点と大きさの補正 |
| pixelViewScale／pixelViewFaceCamera／pixelViewDepthBias | 2Dだけの見え方 |
| armLeft/armRight/legLeft/legRight | 手足の節点名。空欄なら手足を振らない |
| outlineStrength | 2D輪郭の強さ |
| placeholder | 仮モデルかどうかの印 |

- `PocActorView.BuildModel()` が、実行時にこのエントリーからモデルを作ります。
- Grid・向き・歩数・Interactionはモデルを一切読みません。検査では、Phase 1のマネキンへ差し替えてもGridの状態が変わらないことを確認しました。
- 正式モデルにRigとAnimatorがある場合は、手足の節点名を空にして、Animatorを駆動する小さな部品を追加する想定です（Phase 3以降）。

## 22. Phase 2の自然物の保護

- Phase 2で作った地面・土・草・木・花・石のBlender Source、FBX、Manifestは変更していません。
- 木のPerchAnchorもそのままです。Phase2EnvironmentChecks 130件が引き続き成功しています。
- 変わったのは2Dの見た目（輪郭線）だけです。

## 23. AUTO／Interactionの回帰

- AUTOの経路はGrid上の方角で、3Dのカメラに影響されません（8項）。
- 3DでのInteraction:
  - S（後ろ）で花を向いてAを押すと「はな。」
  - A（左）で石を向くと「いし。」
  - W（前）で木を向くと「き。」
  - どの場合も、向いた物以外は反応しない（検査）。
- WebGLのスマホ相当で、D-padで花の前まで歩き、Aを押すと2D・3Dとも「はな。」でした。

## 24. 朝夜の回帰

- 時間計算・Fade・保存キーは変えていません。
- 夜の2Dでは、輪郭は既存の夜色と重なって暗い青灰（約 #1F2746）になり、黒潰れしません。白い花弁は明るさ約0.7を保ちます（検査）。
- 夜の3Dは、Phase 2と同じ減光と夜空です（`sheets/06`）。

## 25. 新しい検査（Phase21PresentationChecks）

**44件成功・失敗0**（`HARUKA_WORLD_ver3.0_PHASE2_1_LOGS/11-phase21-checks.log`）。

- 3D操作（12件）:
  - 4方向×W/S/A/Dの対応表、方角への丸め、カメラ基準が初期値
  - S長押し、S離し後もカメラが回らない、Sを3回押す
  - A長押し、離した後の回り込み、回り込み中のW、D長押し、A/D交互、一歩の途中でキーを変える
- 向きとInteraction（3件）
- AUTOと2Dの方角固定（2件）
- 切替（3件）: キーを押したまま切り替える、離した後に再開する、20回切り替える
- 2D輪郭（12件）:
  - 配線と初期値
  - 16px／32pxのそれぞれで、「なし・1px・2pxの差」「物の周りだけに線」「見える範囲」
  - RTの分離、1px単位のカメラ移動、3Dでは無効、夜
- 仮Player（12件）:
  - アセット（Source・FBX・手足・マテリアル・寸法・顔の向き）
  - Catalogの仮素材の印、3Dの4方向、3Dの位置、2Dの位置と補正、歩行、元のSprite 50ファイルが無変更
- モデル差し替え（1件）

## 26. 既存のRegression

| 検査 | 結果 | ログ |
|---|---|---|
| Phase2EnvironmentChecks | **130件成功** | `14-phase2-checks.log` |
| Phase 1 PocChecks | **102件成功** | `13-phase1-poc-checks.log` |
| Version30Checks（継承チェックを含む） | 成功 | `16-version30checks.log` |
| MapAuthoringChecks | 成功 | `17-mapauthoringchecks.log` |
| MAP EDITOR Core Tests | **181/181成功** | `18-map-editor-core-tests.txt` |

- 既存の検査は1件も削除・変更していません。Phase 1の検査が使う旧シグネチャ `PocInput.Resolve(raw, thirdPerson, yaw)` は、互換用に残しています。
- Version30Checksの `PrepareScene` が作り直したファイルは、worktree内で元に戻しました。

## 27. PC WebGL

repo外のBuildを、別に起動したheadless Chrome（1280×720、AMD Radeon内蔵GPU）で操作しました。

確認した順序:
1. 2Dで起動（輪郭あり）
2. 2DでW → 3Dへ（Cell 0,1・基準方角が北）
3. W長押し → S長押し（後退し、カメラは北のまま）
4. A長押し → 離す（カメラが西へ回り込む）
5. W（西へ前進）
6. Sを3回押す（東へ3マス後退）
7. A/D交互（南北へ往復）
8. 2Dへ（Sで南、Aで西）
9. 3Dへ戻す（Wで西へ前進）

すべて60fpsでした。

## 28. Mobile Preview

DevToolsのエミュレーション（390×844、DPR 2、タッチ、`?touchControls=1`）で確認しました。

- 3Dでの既存D-pad（カメラが南向き）:
  - 上の長押し → 南へ5歩
  - 下 → 北へ戻る（Playerはカメラ側を向き、カメラは回らない）
  - 左 → 東へ進み、止まるとカメラが東へ回り込む
- 2Dでの下 → 南
- Aボタン → 2D・3Dとも「はな。」
- 2Dの輪郭と仮Playerが読める

**物理スマートフォンでは確認していません。**

## 29. Performance

計測は、AMD Radeon内蔵GPUのPC上で動くheadless Chrome 154 で行いました。Draw Call等はPOC内の `ProfilerRecorder`（Release WebGL）の値です。

| 条件 | FPS | p95 / 最大 | Draw Calls | Batches | SetPass | Tris/frame | Used Memory |
|---|---:|---|---:|---:|---:|---:|---:|
| PC 2D 16px・輪郭1px | 60.0 | 16.8 / 16.8 ms | 88 | 87 | 72 | 43,090 | 15.7 MB |
| PC 2D 16px・輪郭なし | 60.0 | 16.8 / 16.8 ms | 81 | 80 | 70 | 30,724 | 15.8 MB |
| PC 3D | 60.0 | 16.8 / 16.9 ms | 86 | 81 | 70 | 22,138 | 15.8 MB |
| スマホ相当 2D・輪郭1px（RT 130×282 ×6） | 59.9 | 16.8 / 16.9 ms | 96 | 95 | 82 | 34,282 | 15.7 MB |
| スマホ相当 3D | 60.0 | 16.8 / 16.8 ms | 95 | 90 | 79 | 18,866 | 15.7 MB |

- 輪郭のコスト: マスクの1パス分で、PC 2Dでは +7 Draw Call・+2 SetPass・三角形約1.4倍。60fpsは変わりません。
- RTは、色・マスク・合成の3枚です（いずれも低解像度。PCで512×288）。
- Phase 2（PC 2D 65・3D 73 Draw Call）から増えた理由は2つあります。
  - 仮Playerが5部品になり、それぞれ影の分も描く
  - 輪郭のマスクパス
- WebGL Build（非圧縮）は29,480,607 bytesで、Phase 2は29,412,553 bytesでした（+68KB）。
- GPU・CPUそれぞれの負荷の内訳と、物理スマホでの値は計測していません。

## 30. 比較画像・動画

場所: `C:\Users\owner\Documents\ChatGPT\HARUKA_WORLD_ver3.0_PHASE2_1_CAPTURES\`

| ファイル | 内容 |
|---|---|
| `sheets/01_player_original_vs_placeholder.png` | 従来のSprite・Blender・Unity 3D・2D 16px・2D 32px（4方向） |
| `sheets/02_placeholder_3d_game.png` | 3Dゲーム画面（背後・右向き）、3/4、歩行ポーズ |
| `sheets/03_outline_matrix_full.png` | 16/32px × 輪郭なし/1px/2px（全景、同じカメラ） |
| `sheets/04_outline_{tree,flower,stone,player,house}.png` | 物ごとの拡大比較（6通り） |
| `sheets/05_outline_color_inner_grass.png` | 輪郭色3種、内側の線あり/なし、草の輪郭 |
| `sheets/06_night.png` | 夜の2D（輪郭あり）・3D |
| `sheets/07_mobile.png` | スマホ相当の2D・3D（WebGL・Unity） |
| `sheets/08_3d_controls_webgl.png` | 3D操作の各段階（WebGL） |
| `haruka_phase2_1_3d_controls_pc.mp4` | 3D操作の動画（7.6秒、WebGL）。W長押し→**S長押し（カメラは回らない）**→A長押し→離してカメラが回り込む→W→Sの連打→A/D交互 |

## 31. Previewの起動方法

Build: `C:\Users\owner\Documents\ChatGPT\HARUKA_WORLD_ver3.0_PHASE2_1_PREVIEW`（README_POC.txtあり、タイトル「はるかわーるど ver3.0 Phase 2.1 Preview」）

```
cd C:\Users\owner\Documents\ChatGPT\HARUKA_WORLD_ver3.0_PHASE2_1_PREVIEW
python serve_poc.py --port 8892
```

- PC: http://127.0.0.1:8892/
- スマホ相当: http://127.0.0.1:8892/?touchControls=1

Phase 1（8890）とPhase 2（8891）のBuildは、別のフォルダにそのまま残しています。比較用のDebug PanelはPreview専用です。

## 32. Git Commit

ブランチ `claude/ver3.0-2d3d-phase2-1` にCommitします（Pushはしません）。Commit IDは、Commit後の最終報告で示します。

## 33. 最終Git状態

- Phase 2.1 worktree: Commit後、`git status` は空にします。
- Phase 1・Phase 2のworktreeとブランチは変更していません。
- 元の作業ツリー: 開始時と同じ2件だけです。
- ローカル `main` と `origin/main` は変更していません。

## 34. 公開版の保護

- 正式Map・CatalogのSHA-256は作業前後で一致しました。Player Sprite 50ファイルのSHA-256も一致しています（検査）。
- `CharacterVisual`、Save Key、本編のScene・Script・ProjectSettings・WebGL Template・`halkaworld/` は変更していません。
- mainへのMerge、Push、GitHub Pagesの更新、公開HPの変更はしていません。

## 35. 未解決事項

1. 物理スマホでの操作感と性能は未確認です。
2. 3Dで、真正面の1マス先の小さな物（花・石）がPlayerの体に隠れ、クリックでは選べません。Aボタンでは調べられます。
3. 2Dで起こす補正のため、真北の1マス先にある背の高い物とは、前後関係が現行Spriteの描き順と完全には一致しない場合があります。0.35の視線方向補正で主なケースは解消しています。
4. 仮Playerの横顔が弱いです。髪の塊が目立ち、横向きのとき脚が1本に重なって見えます。
5. 輪郭の初期値（16px/マス・1px・#252B39・内側線「弱」）は暫定です。
6. カメラが回り込むまでの待ち時間（0.35秒）は暫定です。実機での好みに合わせて調整できます。

## 36. ユーザーに確認してほしい点

1. **3D操作:** カメラ基準の操作感。特に、S（カメラ側へ後退し、カメラは回らない）、A/Dの後でカメラが背後へ回り込むタイミング（離して止まってから0.35秒）、回り込みを無くした「fixed」の方がよいか。
2. **2Dの輪郭:** 1pxと2px、色（`#252B39`／茶／黒）、16px／32px/マスのどれが「はるかわーるどのドット絵」に近いか。内側の線と草の輪郭は必要か。
3. **仮ﾊﾙｶﾁｬﾝ:** 正式モデルが届くまでの仮として、この見た目でよいか。特に2Dで顔を起こす補正の見え方。
4. **正式モデル:** 届く時の形式（.blend／.fbx、Rigの有無、正面の向き、大きさ）。21項の差し替え方式で受け入れられます。
5. **次のPhase:** Phase 3（家・家具）へ進んでよいか。

承認をいただくまで、Phase 3には進みません。
