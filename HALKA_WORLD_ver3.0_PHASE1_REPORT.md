# HALKA WORLD ver3.0 大規模改装 Phase 1 レポート（2D／3D共通ワールド）

作業日: 2026-10-14（日本時間）／担当: Claude
ブランチ: `claude/ver3.0-2d3d-phase1`（worktree: `C:\Users\owner\Documents\ChatGPT\HALKA_HP_2D3D_PHASE1`）
設計の詳細: [HALKA_WORLD_2D3D_ARCHITECTURE.md](HALKA_WORLD_2D3D_ARCHITECTURE.md)

**結論**: Blenderで作ったローポリ3Dの世界を、共通の論理Gridの上で「2Dドット表示（固定視点）」と「3D三人称表示」に切り替えて操作できることを、Unity EditorとローカルのWebGL（PC相当・スマホ相当）で確認しました。

公開版ver2.9、ver3.0 Preview、正式Map、Player Sprite、MAP EDITOR v0.6、Map JSON v4は変更していません。

---

## 1. 作業開始時のGit状態

| 項目 | 値 |
|---|---|
| 元の作業ツリー | `C:\Users\owner\Documents\ChatGPT\HALKA HP`、ブランチ `codex/ver3.0-preview` |
| HEAD | `d3f81acedca184eb3e0124d3ac5d076ccd22f942`（引き継ぎ文の値と一致） |
| origin/main | `13462feb0dc8078e988bfbc96d3c4c4c9048eb17`（HEADが3コミット先、遅れなし） |
| ver3.0 Previewのコミット | `00b3855` 時計・カラス、`c5cdafe` 検証の記録、`d3f81ac` 看板・室内 |
| Remote Branch | `origin/main`、`origin/codex/hide-mine-dungeon` |
| 未コミット | ` M unity-project/Assets/Content/World/GrassDecoration.prefab`（改行差）、`?? backup-v05-20261006-174557/` |

- Phase 1のブランチは、ver3.0 Previewを後で統合しやすいように `codex/ver3.0-preview` の `d3f81ac` から作りました。
- 作業はgit worktree（`..\HALKA_HP_2D3D_PHASE1`）で行い、元の作業ツリーとは分けています。
- 元の作業ツリーは、作業の前後で同じ状態です（HEAD・ブランチ・未コミットの2件とも変化なし）。
- worktreeは `core.autocrlf=true` の環境で作ったため、作業用ファイルの改行はCRLFです。Gitの中身（index）は同じで、差分は出ません。

## 2. Backup

repo外の `C:\Users\owner\Documents\ChatGPT\HALKA_WORLD_ver3.0_PHASE1_BACKUP_20261014`（198MB）に保存しました。

- `halka-hp-all-refs.bundle`（全ref、verify済み）
- `git-refs.txt`、`git-status.txt`、`git-diff-worktree.patch`（GrassDecorationの差分を含む）、`git-diff-origin-main-to-head.stat.txt`
- `files/`: 正式Map JSON・Catalog・Map Cache、Scene、Scripts全体（Player・Crow・AUTO・Time）、Editor、Character・World Sprite、Font、WebGL Template、Plugins、ProjectSettings、Packages、HPのHTML、ver3.0 Previewのレポート2本
- `public-webgl-sha256.txt`（公開WebGL 5ファイル）、`ver3.0-preview-build-sha256.txt`、`official-data-sha256.txt`

`GrassDecoration.prefab` の既存の変更と `backup-v05-20261006-174557/` は、削除・修正・Commitのどれもしていません。

## 3. Claudeの開発環境

| ツール | 状態 |
|---|---|
| Unity Hub | インストール済み（起動中のプロセスあり） |
| Unity 6000.3.10f1 | `C:\Program Files\Unity\Hub\Editor\6000.3.10f1`（WebGL Supportあり）。同じ版の `-x86_64` フォルダにはWebGL Supportがない。6000.5.4f1も入っているが未使用 |
| Git | 2.55.0.windows.5 |
| Python | 3.11.9（Pillow、numpyあり） |
| PowerShell | Windows PowerShell 5.1（Git Bashと併用） |
| .NET | SDK 10.0.401、Runtime 8.0.21／8.0.31／10.0.12 |
| Chrome | 154.0.8037.95（155のフォルダもある）。headless（new）でGPUの `ANGLE (AMD Radeon(TM) Graphics, D3D11)` が使えた |
| Node.js | 24.21.0（組み込みWebSocketでChrome DevTools Protocolを操作。パッケージの追加はなし） |
| ffmpeg | 8.1.2（WinGet。既にインストールされていたものを使用） |
| WebGL Build | 実行できた（Build 50〜90秒） |
| 使えなかったもの | Unity関連のMCP（存在しない）。Claudeのアプリ内ブラウザは、ペインが非表示の間はrAFが止まりUnityが進まないため、操作検証には使えなかった。代わりに一時プロファイルで起動した別のheadless Chromeを使用（ユーザーのChromeとは別） |

新しい有料ツールや外部サービスは導入していません。Unity Packageも追加していません。

## 4. Blenderのバージョン

Blender 5.2.0 LTS（build 2026-07-14、`C:\Program Files\Blender Foundation\Blender 5.2`）。

## 5. Unityのバージョン

6000.3.10f1（e35f0c77bd8e）。Built-in RP、Gamma。

## 6. Blender MCPの利用可否

- `mcp-for-blender`: サーバーは起動していますが、`get_addon_status` が「Could not connect to Blender」を返しました。BlenderのGUIとアドオンが起動していないためです。
- 公式のBlender MCP（`*_for_cli` 系のツール）: 一覧にはありますが、今回は使っていません。
- 再現性を優先し、すべての制作をBlender Python（`--background`）で行いました。

## 7. Blender Pythonの利用状況

Blender同梱のPython 3.13（bpy）を `blender.exe --background --factory-startup --python …` で実行しました。

- `build_poc_assets.py`: 3つのアセットを生成し、`.blend` の保存とFBXの書き出しまで行います。
- `render_poc_previews.py`: Workbenchでプレビューを描きます。

どちらも再実行すれば同じ結果になります。

## 8. 作成したBlender Asset（すべて仮素材。正式デザインではありません）

| 名前 | 三角形 | 頂点 | 寸法（m＝unit） | 対応 |
|---|---:|---:|---|---|
| POC_Player（仮マネキン） | 312 | 194 | 幅0.38×高0.934（2セル弱） | 正面: 顔・目・青い胸章。背面: 橙の背負い物。右肩: 黄色の印（左右がわかる） |
| POC_Tree | 528 | 280 | 幅約1.23×高1.555 | `tree_basic`（1×1 Blocked、幹がRoot Cellの中心） |
| POC_House | 300 | 200 | 幅2.72×奥行1.18×高1.71 | `house_main`（5×2、Root Cellは扉の凹み） |

- Source: `blender/sources/poc_player.blend`、`poc_tree.blend`、`poc_house.blend`
- Export: `unity-project/Assets/POC2D3D/Models/poc_*.fbx`
- Manifest: `blender/exports/poc_assets_manifest.json`
- プレビュー: `blender/exports/previews/*.png`
- 家と木は既存Spriteの配色（白壁・黒屋根・金飾り・緑の樹冠）を参考にしましたが、正式な家・木・キャラクターのデザインは変えていません。

## 9. Export形式

FBX（Blender 5.2標準の出力、`-Z forward / Y up / Apply Transform / FBX_SCALE_ALL`、面ごとの法線、sRGBの頂点色）。

UnityにFBXのImporterが標準で入っており、追加Packageが不要なため採用しました。glTFは、glTFastなどのPackageを入れる必要があるので見送りました（比較はArchitecture 7項）。

## 10. Unity Importの結果

`PocChecks` の自動検査で、3モデルとも以下を満たしました。

- ルートの回転0・Scale 1・位置0
- 原点が地面（min.y≈0）にある
- 三角形数がBlenderのManifestと一致
- 頂点色と法線がある
- 巻き方向と法線が100%一致
- 共通Materialへの割り当て直し

向きの確認:
- 目は -Z（南）
- 右肩の印は -X（左右反転なし）
- 背負い物は +Z
- 家の扉は z=0.24（凹みの奥）、金の飾りは南側

**軸の対応は Blender(x,y,z) → Unity(x,z,y) で、追加の補正は不要でした。**

## 11. POC Scene

`Assets/POC2D3D/Scenes/HALKA_WORLD_2D3D_POC.unity` は、`PocSceneBuilder` が生成します。本編の `FirstDay.unity` とBuild Settingsは変更していません。

主な構成:
- `SharedWorldSession`（唯一のゲーム状態）
- `PocWorldPresenter`
- `PocViewSwitcher`
- 2D Pixel Camera＋Presenter Camera
- 3D Third-Person Camera
- Sun（Hard Shadow）
- 既存の `WorldTimeController`、`TouchDpad`、`TouchActionButton`
- POC専用の `PocDebugPanel`

正式Mapは、既存のキャッシュ `first_field.asset` と `halka_house.asset` を**読み取り専用**で参照しています（全29 Object＋屋内6 Object）。

## 12. Grid System

- 共通Gridは `MapPlacementRules.BlocksMovement` から作ります（Physicsは使いません）。
- 移動は既存の `GridStepMotion` と、既存の1歩0.18秒をそのまま使っています。
- 検査で確認したこと:
  - 1歩で隣の1セルへ動く
  - 半歩の時点ではセルが変わらない
  - 斜めには動かない
  - キーを押し続けると1秒で5歩進む
  - 木のセルには入れないが、向きは変わる
  - Map端で止まる

## 13. 2D Pixel View

- 3Dの世界を正射影カメラで低解像度RTに描き、整数倍に拡大します（Point）。初期値は16px/セル、見下ろし角50°、地面を正方形にする補正ONです。
- WebGLの1280×720では、Canvasが1024×576になり、RTは512×288 ×2でした。既存の2Dと同じく1セル＝32 CSS pxで、マップの構図も同じでした（`sheets/01`）。
- モバイル相当の390×844（DPR 2）では、RTが130×282 ×6でした。
- Shimmer: 歩いている間の連続30フレームで、背景のずれはすべて6pxの倍数でした。静止した背景・木・家の不一致は0pxで、差分はPlayer自身だけでした（`LOGS/14-shimmer-mobile-2d.txt`、`CAPTURES/shimmer_residual_only_player_013_014.png`）。
- 発見した問題と修正: `Graphics.DrawTexture` の描画がWebGLでだけ上下反転しました（Editorでは正常）。`Graphics.Blit` に変えて解決しました。

## 14. 3D Third-Person Camera

背後から見る透視投影のカメラです。

- 屋外: 距離2.5、Pitch 26°
- 屋内: 距離2.2、Pitch 50°
- Yawは0.3秒で追従し、位置は0.12秒で追従します。
- 1歩ごとにカメラが跳ねることはありません（60fpsで1フレームあたり最大0.046、`sheets/06`）。
- 家や木がPlayerを隠すときは、その本体だけを隠します（影は残す）。

## 15. 2D／3D切替

- 検査: 40回切り替えても、セル・向き・歩数・Map・読込回数がすべて同じでした。Playerの描画位置は、3Dでは正確に、2Dでは1画素以内でした。
- WebGL（PC）:
  - 2Dで3歩 → 3Dへ。Cell 2,1、向きUp、歩数3を維持。
  - 3Dで歩く → 2Dへ。Cell 5,0、歩数9を維持。
  - 0.15秒間隔で20回連続で切り替えた後も、Cell 5,0、歩数9、切替22回。その間のフレーム時間は最大16.8msでした。
- 再Spawnはしません。歩数も増えません（歩数は `StepCompleted` だけで増えます）。
- 動画: `CAPTURES/poc_2d3d_switch_pc_morning.mp4`、`poc_2d3d_switch_pc_night_realtime.mp4`（どちらも8.2秒、10fps、1024×576）。

## 16. Movement検証

- 自動検査: 12項のとおり。
- WebGLのPC: WASDで、2D・3Dとも1マスずつ動きました。押し続けると1秒で6歩（1歩0.18秒と一致）。家の扉へ入ると、halka_houseのinterior_entry（0,-3）に上向きで出ます。
- WebGLのモバイル相当: 既存のD-padを5回タップして5歩、Cell 5,0。

## 17. Interaction検証

- 自動検査:
  - 木の正面でA → 「き。」
  - 木の横からも反応する（既存の `ExamineInteractable` と同じ）
  - 背を向けると反応しない
  - 看板はsignTextの「こっちは東。」
  - 看板の横からは反応しない（Action Pointどおり）
  - 2Dと3Dで同じ木をクリックすると、同じInstance IDになる
- WebGL: 3Dでも2Dでも、木をクリックすると「き。」（PC）。モバイル相当ではAボタンで、2D・3Dとも「き。」。

## 18. Camera方向の比較（3D操作）

試作の初期値は案A（方角固定）で、パネルから案B（カメラ基準）に切り替えられます。

- 案B: 押した瞬間のカメラ目標Yawで方向を固定すると、押している間に向きが反転しないことを検査で確認しました。
- 背後カメラと組み合わせたときの各案の問題点は、Architecture 12項にまとめました。

**最終方式は未決定です（ユーザーの判断を待ちます）。**

## 19. Object配置の一致

- 正式Mapの全Object Instanceで、Instance ID・Definition・Root CellがJSONと一致しました。
- 3つの方法で作ったBlocked集合が一致しました: JSON＋Catalogから直接計算したもの、共通Grid、`MapPlacementRules`（first_field 38セル、halka_house 66セル）。
- 表示ルートの位置が `ToWorld(RootCell)` と一致しました。
- MAP EDITOR Fixture（コピーの上で木を1マス移動）: 2D・3Dの両方で新しいセルに表示され、衝突も新しいセルで起こりました（`sheets/10`）。

## 20. PC WebGL

repo外のBuildを、ローカルのheadless Chrome（1280×720、DPR 1、GPU）で操作しました。

確認したこと:
- 2D・3Dの表示
- Grid移動
- Camera追従
- 表示の切替
- Objectの表示
- 衝突
- クリックでのInteraction
- 家への入室と屋内表示
- 朝夜（実時間で夜になり、2Dは夜色、3Dは減光と夜空）

最終ビルドでも全項目を通しで再確認しました（`sheets/11`）。

## 21. Mobile Preview

Chrome DevToolsのエミュレーション（390×844、DPR 2、タッチ有効、`?touchControls=1`）で確認しました。

- 既存のD-padとAボタンが出る
- D-padで移動できる
- Aで調べられる（2D・3D）
- 表示を切り替えられる
- 60fps

**物理スマートフォンでは確認していません。**

## 22. Performance測定

計測環境: AMD Radeon内蔵GPUのPCで動くheadless Chrome 154（WebGL2）。FPSはrAFの5秒平均です。Draw Calls・Batches・SetPass・Trisは、POC内の `ProfilerRecorder` で取得しました（Release WebGLでも取れました）。

| 条件 | FPS | p95 / 最大フレーム | Draw Calls | SetPass | Tris/frame | RT | Unity Used Memory | JS Heap |
|---|---:|---|---:|---:|---:|---|---:|---:|
| PC 2D（1024×576） | 60.0 | 16.8 / 17.0 ms | 51 | 27 | 14,188 | 512×288 ×2 | 14.2 MB | 10.7 MB |
| PC 3D（1024×576） | 60.0 | 16.8 / 17.5 ms | 33 | 27 | 8,304 | — | 14.3 MB | 10.6 MB |
| Mobile相当 2D（780×1688） | 60.0 | 16.8 / 16.8 ms | 54 | 38 | 10,728 | 130×282 ×6 | 14.4 MB | 11.2 MB |
| Mobile相当 3D（780×1688） | 59.8 | 16.8 / 17.1 ms | 47 | 37 | 9,788 | — | 14.4 MB | 10.4 MB |
| PC 20回連続切替中 | 59.9 | 最大 16.8 ms | — | — | — | — | — | 11.4 MB |

- 切替1回の処理時間（Unity内）: 0.00〜1.00 ms（WebGLのタイマー分解能の範囲）
- 地図メッシュ: first_fieldで4,808三角形
- WebGL Build: 29,263,147 bytes（wasm 24.34MB、data 4.47MB、非圧縮）。現行Previewは29,587,712 bytes。
- Unity Editor上の数値は計測していません。上の表はすべてWebGLの数値です。
- 測れなかったもの: WASMヒープ全体の確保量、物理スマホでの数値。推測値では埋めていません。

## 23. 既存Regression

すべて今回のブランチで実行し、成功しました。ログは `C:\Users\owner\Documents\ChatGPT\HALKA_WORLD_ver3.0_PHASE1_LOGS\` にあります。

- `Version30Checks.Run`（Version29Checksを含む、ver2.0〜3.0 Previewまでの継承チェック全部）→ 成功（`11-version30checks.log`）
- `MapAuthoringChecks.Run` → 成功（`12-mapauthoringchecks.log`）
- MAP EDITOR Core Tests → **181/181成功**（`13-map-editor-core-tests.txt`）
- Phase 1の `PocChecks.RunBatch` → **102件成功・失敗0**（`20-poc-checks.log`）

補足: Version30Checksの中で `ProjectBuilder.PrepareScene` が `FirstDay.unity` を作り直すため、ID再採番の差分と、GrassDecoration・ProjectSettingsの改行差が出ます。これはworktree内でテストが生成したものなので、元に戻しました。

## 24. 発見した問題

1. WebGLで `Graphics.DrawTexture` を使うと、RTが上下反転する → `Graphics.Blit` で解決。
2. Editorで画像を出力するとき、3Dで隠した遮蔽物が2Dに戻っても隠れたままになる（Edit Modeでは `OnDisable` が呼ばれないため） → 切替時に明示的に戻すよう修正し、検査を追加。
3. アプリ内ブラウザのペインが非表示の間は、rAFが止まってUnityが進まない → 検証用に別のheadless Chromeを使用。
4. 3Dの屋内では壁（高さ1.1）が大きく映る → 遮蔽物を隠す処理と屋内用の見下ろし角で改善。壁の高さはデザインの判断が必要。
5. 24px/セルは、576pxの画面では整数倍で18セル表示にならない（24セルか12セル）。
6. 16px/セルの3Dドット化では、Playerの細部（顔・髪）が既存の手描きSpriteに及ばない（`sheets/01`、`03`）。

## 25. Phase 2の実装計画（案。承認後に着手）

1. ﾊﾙｶﾁｬﾝの正式ローポリモデル（Blender Python＋手修正）。資料の確認とデザインの承認を先に行います。
   - 既存Spriteから読み取れる要素:
     - 約2頭身
     - 金髪で、内側と下側が青の2色髪。背面に稲妻形の分け目
     - 青い目、目の下のピンク
     - 頭の黒い角状の飾り2つ
     - 片側の白い四角い髪留め
     - 黒い上着（青の縁と胸章）、黒いスカート（青い裾）
     - 白い脚、黒い靴
   - 未確定の部分は推測で決めません: 背面・側面の衣装の細部、髪留めの立体形状、カールした髪束の位置など。
2. 4方向の歩行と待機のAnimation（Armature）→ FBXのAnimationをImportして検証。
3. 本編の統合（Architecture 21項）。Grid判定のデータ化 → Player状態の分離 → Menuに「表示 2D / 3D」を1行追加 → AUTO・Crow・Life Logの接続。
4. 2D表示方式の決定（Architecture 23項）。キャラクターは手描き品質を守る経路を残す。
5. 未3D化の11種を順番にBlenderで制作（Codexの画像素材と役割を分担）。
6. 物理スマホ（iOS／Android）での計測。Brotliでの圧縮配信を検討。

## 26. Git Commit

ブランチ `claude/ver3.0-2d3d-phase1` の状態です（Pushはしていません）。

- `742111e` Add ver3.0 Phase 1 2D/3D shared-world POC（96ファイル追加）
  - Unity `Assets/POC2D3D/**`
  - `blender/**`（.blend、スクリプト、プレビュー、Manifest）
  - `tools/Poc2D3DFixture/**`
  - `tools/poc2d3d/**`
  - `unity-project/POC2D3DFixtures/**`
- この文書2本は、続くCommitに含めます。

既存ファイルの変更は1つもありません（2つのCommitともファイルの追加だけ）。

## 27. Previewの起動方法

Build: `C:\Users\owner\Documents\ChatGPT\HALKA_WORLD_ver3.0_2D3D_PHASE1_POC`（README_POC.txtあり）

```
cd C:\Users\owner\Documents\ChatGPT\HALKA_WORLD_ver3.0_2D3D_PHASE1_POC
python serve_poc.py --port 8890
```

ChromeでURLを開きます。スマホ相当で試すときは `?touchControls=1` を付けます。

- PC: http://127.0.0.1:8890/
- スマホ相当: http://127.0.0.1:8890/?touchControls=1

操作:
- WASD／矢印で移動
- 左クリックで調べる
- 左上の「VIEW」で2D⇄3Dを切り替える。「POC options」で比較用の設定を開く

Unity Editorは不要です。

比較画像・動画は `C:\Users\owner\Documents\ChatGPT\HALKA_WORLD_ver3.0_PHASE1_CAPTURES\` にあります。

| ファイル | 内容 |
|---|---|
| `sheets/01` | 現行2DとPOCの比較 |
| `sheets/02` | 同じ位置での2D・3D |
| `sheets/03` | Playerの4方向 |
| `sheets/04` | 木と家 |
| `sheets/05` | Blenderのプレビュー |
| `sheets/06` | 3Dカメラの追従 |
| `sheets/07` | モバイル |
| `sheets/08` | 2D設定の比較 |
| `sheets/09` | 屋内 |
| `sheets/10` | 夜・Grid・Fixture |
| `sheets/11` | 最終ビルドでの操作確認 |
| `unity/`、`webgl/` | 元画像 |
| `*.mp4` | 切替動画 |

## 28. 最終Git状態

- Phase 1 worktree: 文書Commitの後、`git status` は空です（PrepareSceneによる再生成分は元に戻し済み）。
- 元の作業ツリー（`HALKA HP`）: `codex/ver3.0-preview` @ `d3f81ac`。` M GrassDecoration.prefab` と `?? backup-v05-20261006-174557/` だけで、開始時と同じです。
- `main` と `origin/main` は `13462fe` のまま変更していません。

## 29. 公開版の保護状況

- mainへのMerge、origin/mainへのPush、GitHub Pagesの更新は、どれもしていません。
- `halkaworld/` の公開WebGL・HP・更新履歴・Versionは変更していません。公開WebGL 5ファイルのSHA-256はBackupに記録してあります。
- 正式Map JSON、Catalog、Player Sprite、WebGL Template、`ProjectBuilder`、本編Sceneは無変更です。
- POCのBuildはrepo外にあり、`PocBuilder` はrepo内に出力しようとすると例外で止まります。
- ver3.0 PreviewのBuildと、そのローカルサーバー（8877／8878）には触れていません。

## 30. ユーザーに確認してほしい点

1. **2Dの見た目**: 3Dドット化（`sheets/01`、`08`）は、HALKA WORLDらしさとして許容できますか。ドット密度（16／32px/セル）と見下ろし角（初期値50°）の好みも教えてください。
2. **ﾊﾙｶﾁｬﾝの2D表示**: 既存の手描きSpriteを2Dで使い続けるか、3Dモデルから描くか（方式A／B。Architecture 23項）。
3. **3Dの操作方向**: 案A（W＝北）か、案B（W＝画面の奥）か（Architecture 12項）。
4. **3Dカメラ**: 距離・高さ・見下ろし角、屋内での見え方（`sheets/06`、`09`）。屋内の壁の高さを下げてよいか。
5. **夜の3D表現**: 減光と夜空の強さ（`sheets/10`）。
6. **Phase 2の開始**: 25項の計画に沿って、ﾊﾙｶﾁｬﾝの正式モデル制作へ進んでよいか。必要な資料（背面・側面の設定画など）。

ユーザーの承認があるまで、Phase 2には進みません。
