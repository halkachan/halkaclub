# はるかわーるど ver3.0 Phase 2.1 レポート：3D操作・2D輪郭線・仮ﾊﾙｶﾁｬﾝ

作業日: 2026-10-14（日本時間）／担当: Claude
ブランチ: `claude/ver3.0-2d3d-phase2-1`（Phase 2 `claude/ver3.0-2d3d-phase2` の `7d5dbf9` から分岐）
worktree: `C:\Users\owner\Documents\ChatGPT\HALKA_HP_2D3D_PHASE2_1`

**結論**: 3点とも実装し、ローカルWebGL（PC相当・スマホ相当）で操作して確認しました。

1. **3Dの操作をカメラ基準にした。** 押している間は方向が変わらない。S長押しでも往復しない。A/Dで円を描かない。
2. **2Dにドット単位の輪郭線を付けた。** 低解像度の画像の段階で描き、整数倍で拡大している。
3. **既存Spriteを参考に、ﾊﾙｶﾁｬﾝの簡易ローポリ「仮」モデルを作った（3D表示用）。** 後から差し替えられる構造にしてある。
4. **2Dのﾊﾙｶﾁｬﾝは、もともとのイラスト（既存Sprite）で表示する**（初回提出後のご指示による変更。37項）。本編と同じコマ・同じ時間で動き、絵は元の密度のまま、木などとの前後関係も2D本編と同じ考え方で決める。
5. **2Dで、ﾊﾙｶﾁｬﾝの手前にある木は半透明にする**（2回目のご指示。38項）。
6. **3Dの仮ﾊﾙｶﾁｬﾝを、元のドット絵を参考にしたマイクラ風の箱型にした**（2回目のご指示。38項）。最初の丸みのある仮モデルは比較用に残してある。

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
5. 輪郭を付ける対象: 木・花・石・仮House・仮の箱（家具・看板など）。2Dのﾊﾙｶﾁｬﾝは元のイラスト（輪郭が描き込み済み）なので、輪郭処理の対象外です（37項）。地面と道には付きません（検査で、輪郭が出るのは輪郭対象の物の近くだけであることを確認）。草は初期値で付けず、「弱」も比較用に用意しました。
6. 3Dでは2Dカメラ自体が止まるので、輪郭処理は一切動きません（検査）。
7. UI（IMGUIのMenu・Message・D-pad・A・Version・パネル）は、画像の合成後に描かれるので、輪郭の対象になりません。遊びメモ書きのビットマップFontも変えていません。

## 11. 輪郭線の色・太さ

- 初期値は1px・`#252B39`（暗い青灰）です。`#3A2A22`（濃い茶）と `#000000` も比べました（`sheets/05`）。黒は強すぎ、茶は木の幹となじむものの花や石では浮きました。そのため現行Spriteの輪郭に近い `#252B39` を初期値にしています。
- 2pxは遠くからの視認性が上がりますが、16px/マスでは小さい花が線で潰れ気味です（`sheets/04_outline_flower`）。

## 12. 16pxと32pxの比較

同じMap・同じカメラ中心（Cell 5,−1）・同じ見える範囲（縦18マス）で比べました（`sheets/03`、`sheets/04_*`）。

| 密度 | 輪郭なし | 1px | 2px |
|---|---|---|---|
| 16px/マス（RT 512×288 ×2） | 0画素変化 | 2,852画素に線 | 4,922画素に線 |
| 32px/マス（RT 1024×576 ×1） | 0画素変化 | 5,146画素に線 | 9,064画素に線 |

（初回提出時は2,995／5,139／5,488／9,580画素でした。2Dのﾊﾙｶﾁｬﾝを元のイラストにし、3Dモデルの輪郭がなくなった分だけ減っています。）

16pxに1pxの輪郭を付けると、現行Spriteに最も近い「ドット絵らしさ」になりました。32pxでは線が細く、密で上品な絵になります。**最終決定はしていません**（36項）。

## 13. Pixel Shimmerの検証

- 輪郭は低解像度の段階で描くので、1px単位でカメラを動かす仕組み（Phase 1）がそのまま効きます。検査で、歩行中もカメラ位置が1px単位であることを確認しました。
- WebGLのスマホ相当（RT 130×282 ×6）で、歩いている間の30フレームを撮って比べました。カメラが動いた18ペアすべてで、静止した背景と輪郭の不一致は0pxでした（`LOGS/19-shimmer-mobile-2d-outline.txt`）。
- WebGLでも上下は反転していません（合成はBlit経由）。
- 元のイラストに変えた後も、スマホ相当で東へ歩いた30フレームを撮って比べました。カメラが動いた12ペアすべてで、静止した背景の不一致は0pxでした（`LOGS/26-shimmer-mobile-2d-sprite.txt`）。比較範囲は、開いたDebug Panelより下、かつ横幅192pxのSpriteを除いた範囲です。
- Sprite自体も、毎フレーム低解像度の画素の境目に揃って描かれます（検査）。

## 14. 仮Playerの設計

`haruka_placeholder`（**仮モデル。正式なﾊﾙｶﾁｬﾝではありません**）。**3D表示だけで使います。** 2Dでは元のイラストを表示します（37項）。
**2回目のご指示で、3Dで使うモデルはマイクラ風の `haruka_blocky_placeholder` に替えました（38項）。** この14項と15〜18項・20項は、最初の丸みのあるモデルの記録として残しています（比較: `sheets/11`）。

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
- 初回提出時は、このモデルを2Dでも「足元を軸に起こして」表示していました（`pixelViewFaceCamera`・`pixelViewScale`・`pixelViewDepthBias`）。2Dを元のイラストに変えたため、これらの2D専用の補正は削除しました。
- 2Dでは、モデルの描画（影と輪郭マスクを含む）を止めます。3Dに戻すと再び描画します（検査）。

## 15. 元Spriteとの比較

`sheets/01_player_original_vs_placeholder.png` に並べました。元Sprite（4方向）、Blender、Unity 3D（4方向）、2D 16px・32px です。2Dの2行は元のイラストそのものです（色の一致は検査で確認）。

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

## 19. 2DでのPlayer表示

2Dでは仮モデルを使わず、元のイラストを表示します。詳細は37項にあります（`sheets/01`の2D 16px／32px、`sheets/09`、`sheets/10`）。

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
| placeholder | 仮モデルかどうかの印 |
| sprites | 2Dで使う既存Sprite（4方向×待機・歩行）と、セル基準の高さ0.25（FirstDayと同じ） |

- `PocActorView.BuildModel()` が、実行時にこのエントリーからモデルを作ります。
- Grid・向き・歩数・Interactionはモデルを一切読みません。検査では、Phase 1のマネキンへ差し替えても、Gridの状態と2DのSpriteが変わらないことを確認しました。
- 正式モデルに差し替えても、2Dは元のイラストのままです。
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

**60件成功・失敗0**（`HARUKA_WORLD_ver3.0_PHASE2_1_LOGS/33-phase21-checks-blocky.log`）。
- これまでの件数: 初回提出時は44件（`11-`）、元のイラスト対応後は51件（`21-`）。
- 38項で、半透明の木の検査8件と、マイクラ風モデルの検査（箱の面だけでできている）1件を追加しました。
- 内訳は下のとおりです（38項の分を除く）。

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
- 仮Player（11件）:
  - アセット（Source・FBX・手足・マテリアル・寸法・顔の向き）
  - Catalogの仮素材の印、3Dの4方向、3Dの位置と表示、歩行、元のSprite 50ファイルが無変更
  - 初回の「2Dで起こす補正」の検査は、補正を削除したので外した
- 2Dの元イラスト（8件、37項）:
  - FirstDayのCharacterVisualと同じファイル・同じ順番を参照している
  - 2Dでは3Dモデルを描かない（輪郭マスクにも入らない）。3Dでは描く
  - コマの選び方と時間がCharacterVisualと同じ（待機0.4秒、歩行0.15秒、4方向、切替でコマ0から）
  - 元の密度（64px＝1ユニット）で描く。16px・32px/マス、PC・スマホの大きさで確認。下端はセルの南の縁、左右は中央
  - 何もない地面では、不透明な画素がすべて元の色と一致する（1,609画素、隠れ0、不一致0）
  - 木の北にいるとき隠れる・木の南にいるとき手前に出る（輪郭あり・なしの2件）
  - 歩行中も毎フレーム低解像度の画素に揃う
- モデル差し替え（1件）

## 26. 既存のRegression

| 検査 | 結果 | ログ |
|---|---|---|
| Phase2EnvironmentChecks | **130件成功** | `36-phase2-checks-blocky.log`（初回 `14-`、37項 `23-`） |
| Phase 1 PocChecks | **102件成功** | `35-phase1-poc-checks-blocky.log`（初回 `13-`、37項 `22-`） |
| Version30Checks（継承チェックを含む） | 成功 | `38-version30checks-blocky.log`（初回 `16-`、37項 `27-`） |
| MapAuthoringChecks | 成功 | `39-mapauthoringchecks-blocky.log`（初回 `17-`、37項 `28-`） |
| MAP EDITOR Core Tests | **181/181成功** | `18-map-editor-core-tests.txt`（初回のみ。今回の変更はMAP EDITORのファイルに触れていないため再実行していない） |

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

元のイラストに変えた後に、再ビルドしたBuildでも確認しました。
- 2D（元のイラスト）で起動し、S長押しで南へ歩く。歩行コマが切り替わる（`sheets/10`）。
- Dで東向き、Wで北向きのSpriteになる。60fps。
- 3D操作の部分はコードを変えていないので、WebGLでは上の1〜9の操作をやり直していません。Unity内の検査（12件）は再実行して合格しています。

## 28. Mobile Preview

DevToolsのエミュレーション（390×844、DPR 2、タッチ、`?touchControls=1`）で確認しました。

- 3Dでの既存D-pad（カメラが南向き）:
  - 上の長押し → 南へ5歩
  - 下 → 北へ戻る（Playerはカメラ側を向き、カメラは回らない）
  - 左 → 東へ進み、止まるとカメラが東へ回り込む
- 2Dでの下 → 南
- Aボタン → 2D・3Dとも「はな。」
- 2Dの輪郭と仮Playerが読める

元のイラストに変えた後（再ビルド後）:
- 2Dで元のイラストが3倍（192px＝2マス分）で表示される。D-padの下で南へ歩く。
- VIEWで3Dにすると仮モデル、2Dに戻すと元のイラストになる（`sheets/07`）。59.8fps。

**物理スマートフォンでは確認していません。**

## 29. Performance

計測は、AMD Radeon内蔵GPUのPC上で動くheadless Chrome 154 で行いました。Draw Call等はPOC内の `ProfilerRecorder`（Release WebGL）の値です。

38項の対応後（半透明の木・マイクラ風モデル、再ビルド後）:

| 条件 | FPS | p95 / 最大 | Draw Calls | Batches | SetPass | Tris/frame | Used Memory |
|---|---:|---|---:|---:|---:|---:|---:|
| PC 2D 16px・輪郭1px・木1本が半透明中 | 59.8 | 16.8 / 33.4 ms | 92 | 88 | 81 | 39,718 | 15.6 MB |
| PC 3D（マイクラ風モデル） | 60.0 | 16.8 / 16.8 ms | 102 | 85 | 72 | 25,446 | 15.7 MB |

- 半透明の木は、木だけを別に描く1パス分（+10 Draw Call程度）。半透明の木がないときは増えません。
- 最大33.4msは、計測開始直後の1フレームだけでした。
- マイクラ風モデルは、面をドットのマスに分けて塗るため、モデル単体で980三角形です（丸いモデルは976）。3Dで三角形が増えたのは、主に影と画面に入る範囲の違いです。
- WebGL Build（非圧縮）は29,534,926 bytes。

元のイラストに変えた後（再ビルド後）:

| 条件 | FPS | p95 / 最大 | Draw Calls | Batches | SetPass | Tris/frame | Used Memory |
|---|---:|---|---:|---:|---:|---:|---:|
| PC 2D 16px・輪郭1px・元イラスト | 60.0 | 16.8 / 16.8 ms | 82 | 81 | 74 | 40,156 | 15.7 MB |
| スマホ相当 2D・輪郭1px・元イラスト | 59.8 | 16.8 / 16.8 ms | 92 | 91 | 84 | 31,352 | 15.6 MB |
| スマホ相当 3D（仮モデル） | 59.9 | — | 88 | 87 | 79 | 18,152 | 15.7 MB |

- 2Dでは仮モデル（5部品と影）を描かなくなった分、Draw Callと三角形が減りました。Spriteの描画は1回分です。
- WebGL Build（非圧縮）は29,495,865 bytes（初回提出時より+15KB）。

初回提出時（2Dも仮モデル）:

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
| `sheets/01_player_original_vs_placeholder.png` | 従来のSprite・Blender・Unity 3D（仮）・2D 16px・2D 32px（元のイラスト）の4方向 |
| `sheets/02_placeholder_3d_game.png` | 3Dゲーム画面（背後・右向き）、3/4、歩行ポーズ |
| `sheets/03_outline_matrix_full.png` | 16/32px × 輪郭なし/1px/2px（全景、同じカメラ） |
| `sheets/04_outline_{tree,flower,stone,player,house}.png` | 物ごとの拡大比較（6通り） |
| `sheets/05_outline_color_inner_grass.png` | 輪郭色3種、内側の線あり/なし、草の輪郭 |
| `sheets/06_night.png` | 夜の2D（輪郭あり）・3D |
| `sheets/07_mobile.png` | スマホ相当の2D（元のイラスト）・3D（WebGL・Unity） |
| `sheets/08_3d_controls_webgl.png` | 3D操作の各段階（WebGL。初回提出時のBuildで撮影） |
| `sheets/09_2d_sprite_draw_order.png` | 2Dの前後関係: 木の北では、半透明なしだと隠れ、半透明あり（初期値）だと透けて見える。木の南では手前（輪郭あり・なし） |
| `sheets/10_2d_sprite_walk_webgl.png` | WebGLでの2D歩行（南へ）のコマと東・北向き |
| `sheets/11_3d_model_rounded_vs_blocky.png` | 3Dの仮モデル: 最初の丸いモデルとマイクラ風（Blender各方向、WebGL 3D） |
| `sheets/12_2d_seethrough_webgl.png` | WebGLの2Dで、北に立ったときに木（5,1）が半透明になる様子 |
| `sheets/01`・`02` | 3Dの行はマイクラ風モデルに更新済み |
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

ブランチ `claude/ver3.0-2d3d-phase2-1` にCommitします（Pushはしません）。
- 初回提出: `1afb9ca`
- 2Dを元のイラストに変える追加対応（37項）: `30f06c1`
- 木の半透明化とマイクラ風モデル（38項）: その次のCommit。Commit IDは最終報告で示します。

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
3. 2Dで木のすぐ北に立つと、樹冠が大きいためﾊﾙｶﾁｬﾝがほぼ全身隠れる件は、木の半透明化で対応しました（38項）。半透明の木は、輪郭線も同じ濃さで薄くなります。
4. 2Dで1マス24pxのような半端な密度にすると、元のイラストの1画素が画面の整数画素にならず、画素の大きさが不揃いになります（本編も画面の大きさによっては同じです）。16pxと32px/マスでは、PC（1080p以下）とスマホ相当で整数倍になります。
5. マイクラ風の3Dモデルは、頭8×8ドットなので、顔は目・まぶた・頬だけの簡単な表現です。前髪の毛束と髪留めは、ドット絵より大まかです。
6. 輪郭の初期値（16px/マス・1px・#252B39・内側線「弱」）は暫定です。
7. カメラが回り込むまでの待ち時間（0.35秒）は暫定です。実機での好みに合わせて調整できます。

## 36. ユーザーに確認してほしい点

1. **3D操作:** カメラ基準の操作感。特に、S（カメラ側へ後退し、カメラは回らない）、A/Dの後でカメラが背後へ回り込むタイミング（離して止まってから0.35秒）、回り込みを無くした「fixed」の方がよいか。
2. **2Dの輪郭:** 1pxと2px、色（`#252B39`／茶／黒）、16px／32px/マスのどれが「はるかわーるどのドット絵」に近いか。内側の線と草の輪郭は必要か。
3. **ﾊﾙｶﾁｬﾝ:** 2Dの元のイラスト表示と、木の半透明（濃さ40%・0.25秒）でよいか。3Dのマイクラ風モデルの見た目（頭の大きさ、顔、髪の色の分け方）でよいか。
4. **正式モデル:** 届く時の形式（.blend／.fbx、Rigの有無、正面の向き、大きさ）。21項の差し替え方式で受け入れられます。
5. **次のPhase:** Phase 3（家・家具）へ進んでよいか。

承認をいただくまで、Phase 3には進みません。

## 37. 追加対応: 2Dのﾊﾙｶﾁｬﾝを元のイラストに変更

初回提出後のご指示「2Dでのハルカチャンは3Dではなく、もともとのイラストでお願いします」への対応です。

**表示するもの**
- FirstDayのCharacterVisualが使うSpriteと同じファイル・同じ順番を、そのまま参照しています（`Assets/Content/Character/` の待機2コマ×4方向、歩行4コマ×4方向）。ファイルとmetaは変えていません（50ファイルのSHA-256一致）。
- コマの選び方と時間は、CharacterVisualと同じです（待機0.4秒・歩行0.15秒、向きか歩行状態が変わるとコマ0から）。時間はCharacterVisualの定数をそのまま参照しています。
- 3Dでは引き続き仮モデルを表示します。2D・3Dの切替では、Grid・向き・歩数は変わりません。

**大きさと位置**
- 元のイラストは、2D本編と同じ密度（64px＝1ユニット＝2マス）で描きます。2D Pixel Viewの低解像度画像（1マス16px）には入れず、拡大した画面に直接重ねるので、絵が縮んで潰れることはありません。
- 例: PC 1164×576（拡大×2）で64px、スマホ相当 780×1688（拡大×6）で192px。どちらも元の1画素が画面の整数画素になります。
- 下端はセルの南の縁、左右はセルの中央です。FirstDayのSpriteの位置（セル中心から上へ0.25）と同じ関係です。
- 位置は低解像度の画素の境目に揃えるので、カメラが追従してもちらつきません（13項）。

**前後関係**
- Spriteを「セルの南の縁に立てた板」とみなします。輪郭用マスクの深度と1画素ずつ比べ、手前にある物（南側の木・花・石・家など）があればその画素を描きません。2D本編のY順と同じ結果になります。
  - 木の北に立つと樹冠に隠れる。
  - 木の南に立つと幹の前に出る。
- 地面と草の飾りは、本編と同じくキャラクターの下として扱い、Spriteを隠しません。
- 手前の物がSpriteに重なる境目には、その物の輪郭線（1px／2px）を描きます。
- 2Dの輪郭を「なし」にしても、前後関係のためにマスクだけは描きます。
- 板の位置は、南の縁から0.1手前にしています（北隣のマスの木の樹冠の前面が、ちょうど南の縁に来るため）。

**描く順番**
低解像度の画像を拡大 → 元のイラスト → 夜の色 → UI。夜は2D本編と同じく、イラストも夜の色で暗くなります。

**変更したファイル**
- `Runtime/PocPresentationCatalog.cs`: `PocSpriteSet` を追加し、`PocActorPresentation.sprites` で持つ。2D専用の補正3項目と `outlineStrength` を削除。
- `Runtime/PocActorView.cs`: 2Dでモデルを描かない。CharacterVisualと同じコマ選び。起こす補正を削除。
- `Runtime/PocPixelView2D.cs`: `DrawActorSprite`、`ComposeScreen`、マスクを常に描く処理。カメラのnear/farを投影行列と一致させる（深度の比較のため）。
- `Runtime/PocPixelPresenter.cs`: 拡大の後、夜の色の前にSpriteを描く。
- `Shaders/PocPixelSprite.shader`（新規）、`Materials/POC_PixelSprite.mat`（新規）。
- `Editor/PocSceneBuilder.cs`: Spriteの読み込みと配線。
- `Editor/PocCapture.cs`・`Phase2Capture.cs`・`Phase21Capture.cs`: 画面サイズの合成画像（Spriteを含む）を保存する。前後関係のキャプチャを追加。
- `Editor/Phase21PresentationChecks.cs`: 37項の検査8件を追加。
- `tools/poc2d3d/README_POC.txt`

**作業中に直した不具合**
- 最初は、2Dカメラの `farClipPlane`（1000）と投影行列のfar（60）が食い違っていました。そのためSpriteの深度が実際の約1/17になり、常に手前と判定されていました。検査で見つけ、2つの値を揃えて直しました。
- 地面のタイルがわずかに高いため、足元の数行が地面に隠れていました。地面は隠さない扱いにして直しました。

## 38. 追加対応（2回目）: 2Dの木の半透明化、マイクラ風の3Dモデル

2回目のご指示「木の近くでは木を半透明にしてください」「ﾊﾙｶﾁｬﾝの３Dモデルはもとのドット絵を参考にマイクラのキャラみたいにしてみて」への対応です。

### 38.1 2Dの木の半透明化

**どの木が半透明になるか**
- 次の両方を満たす木です。
  - ﾊﾙｶﾁｬﾝと同じ列か、それより南（手前）に根元がある。
  - 画面上でSpriteの四角と重なっている。
- 北にある木（もともとSpriteの後ろ）は変わりません。
- 対象はCatalogの `fadeInFront2D` で指定し、今は木（`tree_basic`）だけです。花・石・家は変わりません。

**見え方**
- 不透明（1.0）から0.25秒かけて40%の濃さまで薄くなり、離れると0.25秒で元に戻ります（検査: 0.1秒後に0.76、その後0.4）。
- 半透明の木を通して、ﾊﾙｶﾁｬﾝと地面が見えます。画面の色が「Spriteの色と木の色を0.4で混ぜた値」と一致することを、1,588画素で確認しました（不一致0）。
- 木の影は地面に残ります。木の輪郭線も同じ濃さで描きます。
- 夜の色はその上にかかります。UIは影響を受けません。
- Debug Panelの「2D see-through trees」でオン・オフを切り替えられます（初期値はオン）。オフにすると、37項の前後関係（木に隠れる）になります。

**仕組み**
- 半透明にする木は、使っていないレイヤー30に移し、通常の描画では「影だけ」にします。輪郭用マスクにも入らないので、Spriteは隠されません。
- その木だけを、輪郭用マスクの深度に対して前後判定しながら、低解像度の別の画像に描きます。濃さは `_Alpha` として書き込みます。ToonシェーダーにはAlphaの出力を足しましたが、通常の描画は変わりません。
- 拡大した画面 → 元のイラスト → 半透明の木（`HALKA/POC/FadeLayer`）→ 夜の色 → UI の順に重ねます。
- 3Dに切り替えると、すべての木を元に戻します。3Dは今までどおり、カメラとﾊﾙｶﾁｬﾝの間にある物を消す方式です。

**確認**
- 検査8件を追加しました。
  - 初期値
  - なめらかに薄くなる
  - 影が残り、マスクから外れる
  - 合成の色が正しい
  - 半透明なしより多く見える（1,589画素 対 1画素）
  - 離れると元に戻る
  - 北の木は変わらない
  - 3Dで元に戻る
- WebGL（PC相当）: (0,0)から歩いて木(5,1)の北(5,2)に立つと、その木だけが半透明になりました（`sheets/12`）。59.8fps。
- スマホ相当のWebGLでは、半透明化をまだ確認していません。

### 38.2 マイクラ風の3Dモデル（仮）

`haruka_blocky_placeholder`（**仮モデル。正式なﾊﾙｶﾁｬﾝではありません**）

**作り**
- マイクラのキャラクターのように箱だけで組み、各面を「ドット」のマスに分けて頂点カラーで塗っています。材質は今までと同じ共通の1つです。
- 色は、元のドット絵の配色をそのまま使っています。
- 頭は8×8×8ドットです。その外側に、マイクラの「帽子レイヤー」のように一回り大きい髪の殻（9×9×10）を重ねています。
  - 前髪の毛束と、横から後ろへ下がる髪を表します。
  - 下側の青い髪も描いています。
  - 顔の部分は殻を抜いています。
- 顔:
  - 目は2×2ドットで、黒いまぶたと白いハイライトがあります。
  - 目の下にピンク（#FE1B6D）を入れています。
- 小さな箱で付けたもの:
  - 頭の後ろのポニーテール：ヘアゴム、その下の短い毛束、ヘアゴムに付いた黒い飾り（左右に2段ずつ、上・外側・後ろへ立ち上がる）。正面からは頭の左右上にのぞきます（38.3）。
  - 額の本人から見て右側の髪留め（黒地に白い「H」）
- 胴体は8×6×4ドットです。
  - 黒い上着に、青い襟と白・青の胸章を付けています。
  - スカートは9×3×5で、黒と濃い灰のプリーツに青い裾です。
- 腕は2×6×3ドットで、黒い袖・青い袖口・白い手です。
- 脚は3×5×3ドットで、白い脚・黒いブーツ・青い底です。
- 1ドットは0.044m、飾りまでの高さは0.968（約2マス）です。三角形は952個で、同じ色が横に並ぶ部分はまとめています。

**仕組み**
- 手足の節点名（ArmL/ArmR/LegL/LegR）と、肩・股関節の回転の中心は前のモデルと同じです。歩くと、マイクラのように手足が前後に振れます。
- Catalogの1か所（`PlaceholderPlayerPath`）を替えただけで差し替えました。Grid・向き・Interactionには影響しません（Phase 1・2・2.1の検査で確認）。
- 最初の丸いモデル（`haruka_placeholder`）のファイルは比較用に残しています。ゲームでは参照していません。

**ファイル**
- 生成: `blender/scripts/build_haruka_blocky.py`
- Blender: `blender/sources/phase2_1/haruka_blocky_placeholder.blend`
- FBX: `unity-project/Assets/POC2D3D/Models/Phase2_1/haruka_blocky_placeholder.fbx`
- Manifest: `blender/exports/phase2_1_haruka_blocky_placeholder_manifest.json`
- プレビュー: `blender/exports/previews/phase2_1/haruka_blocky_placeholder_*.png`
  - 描画スクリプト `render_haruka_placeholder_previews.py` は、モデル名を引数で受け取れるようにしました。

**確認**
- 検査:
  - 箱の面だけでできている
  - 5部品・共通の材質1つ
  - 足元が原点、高さ0.9〜1.0
  - 顔（ピンク）が南、白い髪留めが本人の右（-X）
  - 3Dの4方向で顔の向きがGridの向きと一致する
  - 歩くと手足が振れる
- WebGLの3Dで表示を確認しました（`sheets/11` の右下）。60fps。

**変更したファイル（38項）**
- `Runtime/PocPixelView2D.cs`: 半透明の判定、別レイヤーの描画、`DrawOverlays`。Spriteの位置計算は `PlaceSprite` に分けた。
- `Runtime/PocViewSwitcher.cs`: 3Dへの切替で木を元に戻す。
- `Runtime/PocWorldPresenter.cs`・`PocPresentationCatalog.cs`: `fadeInFront2D`
- `Runtime/PocPixelPresenter.cs`・`PocDebugPanel.cs`
- `Shaders/PocFadeLayer.shader`（新規）、`Materials/POC_FadeLayer.mat`（新規）
- `Shaders/PocToonVertexColor.shader`: `_Alpha` と `keepalpha`（不透明の描画は変わらない）
- `Editor/PocSceneBuilder.cs`・`Phase21Capture.cs`・`Phase21PresentationChecks.cs`
- `blender/scripts/build_haruka_blocky.py`（新規）・`render_haruka_placeholder_previews.py`
- `tools/poc2d3d/README_POC.txt`

### 38.3 マイクラ風モデルの手直し（3回目のご指示）

ご指示「赤枠で囲った黒いドットっぽいのと変にはみ出してる黄色いのは必要ないです」「黒い髪飾りはポニテ部分のヘアゴムについているアクセサリーなので後ろに下げてください」への対応です。

- 消したもの:
  - 背面の黒い稲妻形のドット
  - 頭頂の黒い点と、濃い色の点
  - 本人の左側にはみ出していた黄色い毛束（カール）
- 黒い飾りを、頭頂の左右から、頭の後ろのポニーテールのヘアゴムへ移しました。
  - ヘアゴムは後頭部の上寄り（高さ17.5〜19ドット）にあり、その下に背中側へ沿う短い毛束（ポニーテール）を付けました。毛束は横にははみ出しません。
  - 飾りは、ヘアゴムから左右・上・少し後ろへ2段で立ち上がります。元のドット絵と同じく、正面からは頭の左右上にのぞき、横からは頭の後ろに見えます。
- 確認:
  - Unity検査60件はすべて合格です（`41-phase21-checks-blocky2.log`）。高さ0.968、顔は南、髪留めは本人の右。
  - WebGLの3Dで背面と横向きを確認しました（`webgl/blocky2_pc_3d_strip.png`、`sheets/11` の右下）。
  - 比較シート `sheets/01`・`02`・`11` を更新しました。
- ログ: `40-build-scene-blocky2.log`、`41-phase21-checks-blocky2.log`、`42-phase21-capture-blocky2.log`、`43-webgl-build-blocky2.log`
