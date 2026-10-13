# HALKA WORLD ver3.0 Preview 追加改修レポート

作業日: 2026-10-13～14（日本時間）
対象: `codex/ver3.0-preview` のローカルPreview。公開版はver2.9のまま。

## 依頼と結果

ユーザーの公開前フィードバックを反映した。机をベッドと同じ俯瞰方向のDTM用デスクへ、木目の強い床を単色寄りの暖色マットへ変更した。屋外には北・東・南・西の看板を1個ずつ追加した。室内のカメラ倍率を屋外と同じにし、歌用マイク、絵描き用イーゼル、制作資料の棚を追加した。MAP EDITORはv0.6、Map JSONはv4のまま。

## 開始状態と保全

- 開始HEAD: `c5cdafe75f23b76c8994b021c62fbb495befd8c1`、作業ブランチ: `codex/ver3.0-preview`。公開`origin/main`: `13462feb0dc8078e988bfbc96d3c4c4c9048eb17`。
- 開始時からある`GrassDecoration.prefab`の改行差と`backup-v05-20261006-174557/`は変更・削除・Commit対象にしない。
- repo外Backup: `C:\Users\owner\Documents\ChatGPT\HALKA_WORLD_ver3.0_PREVIEW_REVISION_BACKUP_20261013`、`C:\Users\owner\Documents\ChatGPT\HALKA_WORLD_ver3.0_PREVIEW_INTERIOR_BACKUP_20261013`。Git bundle、status/diff、Map、Catalog、Scene、元Sprite、Script、Build、比較候補、ログを保存。

## デザインと実装

- `desk.png`は元の机の暗い木色を参照し、俯瞰64×32pxの机上に小型モニター、波形、鍵盤、操作機器を加えた。机の設置座標、2×1のBlocked Footprint、「つくえ。」の操作は変えていない。
- `house_floor.png`は32×32pxのマット状に変更し、Catalogと生成Assetの名称を「マット」にした。床の通行判定は変えていない。
- 新しい`recording_mic.png` (32×64px)、`art_easel.png` (64×64px)、`studio_shelf.png` (64×32px)は既存の机のドット絵を参考画像としてImageGenで作成した。透明背景の生成画像を切り出し、指定サイズに縮小、24色以下へ減色、Alphaを二値化して採用した。Point Filter、PPU64、MipMapなし、非圧縮。原画像と16倍最近傍確認画像、変換Scriptは上記INTERIOR_BACKUPの`candidates/`等に保存。
- 生成元: `C:\Users\owner\.codex\generated_images\01a0e8c0-4ba7-7e01-8e7a-710256d98695\exec-57d8d0fc-aa0a-49b2-b44b-d9e418c37600.png`（マイク）、`exec-3501d572-f556-407a-bb5a-3e5c417179bc.png`（イーゼル）、`exec-6ac55fc7-81f4-4e63-9004-8e827b56e696.png`（棚）、`exec-946460af-e16e-44c5-9bf0-1d1f9aba44e8.png`（DTM机）。それぞれ既存の暗い木机の画風を参照し、マイクスタンド、キャンバスと画材、資料棚、制作機材付き机を指定した。
- Catalogへ編集可能な`recording_mic_basic`、`art_easel_basic`、`studio_shelf_basic`を追加。すべて通常のObject Instanceで通行不可。MAP EDITORの既存契約に合わせ`allowedMapTypes`は空配列。各Definition AssetをImporterで再生成した。
- 室内の`HouseArea2D`と`MapWorldController2D`から屋内専用の`orthographicSize=3.4`を除き、屋外と同じ初期サイズ4.5を使用。Player SpriteやAnimation、補間、PPU、Canvas倍率は変更していない。

## 正式Mapの追加分

### 屋外 `first_field.hwmap.json`

| Instance ID | Root Cell | signText | Action Cell |
|---|---:|---|---:|
| `obj_sign_north_v30_preview` | (2,5) | こっちは北。 | (2,4) |
| `obj_sign_east_v30_preview` | (10,0) | こっちは東。 | (10,-1) |
| `obj_sign_south_v30_preview` | (5,-3) | こっちは南。 | (5,-4) |
| `obj_sign_west_v30_preview` | (-8,1) | こっちは西。 | (-8,0) |

既存の歓迎看板は維持。道の中心・終端・家の出入口を避け、各看板の正面セルへ到達できる位置に置いた。文章はC#ではなくInstanceの`signText`に保存した。ブラウザで東看板をクリックし「こっちは東。」をGameHudで確認。他の3枚の個別クリックは未実施だが、同じDefinitionとInstance文章経路を使い、MapAuthoringChecksで到達可能性を検査した。

### 屋内 `halka_house.hwmap.json`

| Instance ID | Definition | Root Cell | 配置意図 |
|---|---|---:|---|
| `obj_recording_mic_v30_preview` | `recording_mic_basic` | (4,-1) | 右側に歌収録の一角 |
| `obj_art_easel_v30_preview` | `art_easel_basic` | (-3,-2) | ベッドの手前側に画材 |
| `obj_studio_shelf_v30_preview` | `studio_shelf_basic` | (-1,2) | 北側の壁際に制作資料 |

ベッド、クッション、机、出口は移動していない。正式Mapの旧データとJSONを再帰比較し、両Mapとも上記Object追加以外のSemantic Diffはない。道路、Surface、Marker、Entity Spawn、Area Transition、既存Objectの内容は同一。生成されたSceneはBuildにより大きく再採番されるため、Build後に元のSceneへ戻した。Map JSONと生成Map AssetをSource of Truthとし、次のPreview BuildでSceneは再生成される。

## 検証

- `Version30Checks.Run`: Unity batchmode exit 0。時間・Crow・既存回帰に加え、7件の追加Object、Asset設定、室内外のカメラサイズ一致を検証。ログ: INTERIOR_BACKUP内`Version30Checks-Run-final.log`。
- `MapAuthoringChecks.Run`: Unity batchmode exit 0。ログ: `MapAuthoringChecks-Run-final.log`。
- MAP EDITOR Core Tests: **181/181成功**。ログ: `map-editor-core-tests-rerun.txt`。
- `ProjectBuilder.BuildPreviewWeb`: Production相当の`BuildOptions.None`で成功、Unity exit 0。公開WebGLとは別のrepo外`C:\Users\owner\Documents\ChatGPT\HALKA_WORLD_ver3.0_PREVIEW`へ出力。ログ: `preview-build-unity.log`。
- PCブラウザの正式Map: ver3.0 Preview、方角看板の表示、家への手動入室、マット・机・追加家具の描画、クッションへの移動、机クリックで「つくえ。」、家からの退出を実操作で確認。屋内でPlayerが拡大されず、入退室で表示倍率が変化しない。
- 390×844相当、`touchControls=1`: D-padで正式Mapの家まで歩いて入室。室内のマット、棚、机、イーゼル、マイクの表示とD-pad/A/Menuの画面内表示を確認。小さい縦画面では同倍率のため部屋の左右が視野外になるが、Player追従と操作は維持。物理スマホは未確認。
- テスト中に変更したPreview originのAUTO設定は元のONへ戻した。

## Previewの開き方と公開状態

ローカルサーバーが稼働中なら [ver3.0 Preview](http://127.0.0.1:8878/) を開く。サーバーを再起動する場合は、`C:\Users\owner\Documents\ChatGPT\HALKA_WORLD_ver3.0_PREVIEW`を配信ルートとして`python -m http.server 8878 --bind 127.0.0.1 --directory "C:\Users\owner\Documents\ChatGPT\HALKA_WORLD_ver3.0_PREVIEW"`を実行する。スマホ相当のUIはURL末尾に`?touchControls=1`を付ける。

本改修はPreviewブランチとrepo外Preview Buildに限定。`main`へのMerge、`origin/main`へのPush、公開HP・更新履歴・公開WebGLの差し替えは行っていない。ユーザー確認前の正式公開は禁止のまま。

## 残る確認事項

- 夜の暗い背景上にある小さなVersion文字はコントラストが低い。既存Previewレポートの指摘と同じで、今回の内装改修では扱っていない。
- 家具の位置と画風、屋内を屋外と同倍率で表示したときの余白・モバイルの見える範囲をユーザーに確認してもらう。
