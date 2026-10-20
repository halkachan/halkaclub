# はるかわーるど ﾊﾙｶﾁｬﾝ 正式3Dモデル仕様書

作成日: 2026-10-20（Phase 7。同じ日に、2Dのドット絵の描き直しに合わせて 2D の記述を更新） / 対象: はるかわーるど ver3.0（RC3） / 正式モデルの記録: `blender/exports/haruka_player_official_manifest.json`

## 1. ﾊﾙｶﾁｬﾝ正式3Dモデルの採用宣言

**3Dモードで使っているローポリ（マイクラ風）のﾊﾙｶﾁｬﾝを、はるかわーるどの正式な3Dモデルとして採用します。**

- このモデルは、Phase 2.1 で「正式モデルが届くまでの仮モデル」として作られました。
- Phase 7 で、ユーザーが正式採用を決めました。別の正式モデルを待つ必要はありません。
- 今の見た目・頭身・ポーズを正式な基準とします。高精細化や頭身の変更はしません。
- 2D表示では、正式なドット絵（Sprite）を使います。3Dモデルを2Dに出すことはありません。
- 2Dのドット絵は、2026-10-20 に、ユーザーが描き直した4方向の絵をもとに作り直しました（`HARUKA_WORLD_ver3.0_2D_SPRITE_UPDATE_REPORT.md`）。
- このモデルの色は、描き直す前のドット絵に合わせて塗ったものです。今の2Dの絵とは、髪の影や服の細かいところが少し違います。新しい絵に合わせて塗り直すかどうかは、ユーザーが決めます。それまでは、今の見た目が正式です。

正式な基準とする特徴:

| 項目 | 内容 |
|---|---|
| 表現 | マイクラ風の箱だけでできたローポリ。各面を2Dのドット絵（2026-10-20 に描き直す前の絵）に合わせて1ピクセルずつ塗る（頂点カラー） |
| 頭身 | 約2頭身（頭 8×8×8 ピクセル、体 8×6×4、腕 2×6×3、脚 3×5×3。1ピクセル = 0.044 m） |
| 髪 | 金髪（#f1e2bc）と、内側の青（#4b7ae8）。頭の外側に一回り大きい「髪の層」 |
| 頭飾り | 黒い飾り（#24242e）。ポニーテールの結び目（頭の後ろ）にある |
| 顔 | 青い目、目の下に細いピンクの線（#fe1b6d）。シンプルな顔 |
| 衣装 | 黒（ジャケット #3a3a40）と青を中心に、白い脚、黒いブーツ |
| ヘアクリップ | 白（#ffffff）。キャラクターの右側 |
| 体形 | 角ばった体 |
| 動き | 待機（静止）、歩行（腕と脚を振る）、座る（ベンチ・ソファー） |

画像: `blender/exports/previews/phase2_1/haruka_blocky_placeholder_{south_front,north_back,east,west,three_quarter,top,game_2d_angle}.png`（Blender のプレビュー）と、Phase 7 レポートの比較画像（ゲーム内）。

## 2. モデルのSource

| 種類 | ファイル |
|---|---|
| 生成スクリプト（正式な作り方） | `blender/scripts/build_haruka_blocky.py` |
| 共通の関数 | `blender/scripts/halka_lowpoly.py` |
| 色の元にした2Dのドット絵 | 2026-10-20 に描き直す前の `unity-project/Assets/Content/Character/{front,back,left,right}_idle/00.png`（コミット `4350be7` まで）。同じ絵が `tools/poc2d3d/sprite_frames/templates/{front,back,left,right}_idle.gif` の1コマ目に残っている |
| 生成時の記録 | `blender/exports/phase2_1_haruka_blocky_placeholder_manifest.json`（三角形の数、部品、ピボット、大きさ） |
| 正式モデルの記録（Phase 7） | `blender/exports/haruka_player_official_manifest.json`（ファイルの SHA-256、Unity の GUID、表示の決まり） |

モデルは手で編集したものではなく、スクリプトで作られます。スクリプトの中のドット（文字の表）が、見た目の元です。スクリプトは絵のファイルを読まないので、2Dの絵を描き直しても、作り直したモデルは変わりません。

## 3. Blenderファイル

- `blender/sources/phase2_1/haruka_blocky_placeholder.blend`（Blender 5.2.0 LTS で保存）
- 中身: 本体 `haruka_blocky_placeholder`（頭・髪の層・体・スカート・飾り）と、子の `ArmL` `ArmR` `LegL` `LegR`。Rig と Animation はありません。
- 正面は South（Blender の −Y）。

## 4. FBX

- `unity-project/Assets/POC2D3D/Models/Phase2_1/haruka_blocky_placeholder.fbx`
- 書き出し: 前方 −Z、上 Y、スケールを適用、Animation なし、頂点カラー sRGB、面ごとの法線。
- 三角形 1,030（本体 806、腕 60×2、脚 52×2）。

**ファイル名について**: 名前に `placeholder`（仮）が残っていますが、変えません。

- 理由: Scene、カタログ、検査、スクリプトがこの名前と GUID で参照しているため。
- 仮モデルという扱いは、カタログの印、生成時の記録、この仕様書で終わらせました（Phase 7）。

## 5. Unity Prefab

- 別の `.prefab` は作っていません。FBX を Unity が取り込んだ「モデルの Prefab」をそのまま使います。
  - FBX の GUID: `e8371efd9aea7364698cfd1f3c20b58e`
- 参照の場所: `Assets/POC2D3D/Data/POC_PresentationCatalog.asset` の `player`（`PocActorPresentation`）。
  - `model` が、この FBX です。
  - `placeholder` は Phase 7 で `0`（正式）にしました。
- ゲームは `PocActorView` がこのモデルを作り（Instantiate）、論理セルの上に置きます。
- カタログの古い `playerModel`（`poc_player.fbx`）は Phase 1 の技術検証用です。ゲームでは表示しません。

## 6. Material

- すべての部品で、1つの共通 Material `Assets/POC2D3D/Materials/POC_ToonVertexColor.mat`（GUID `43dd4b12f1608244089beb1b1322bdbb`）を使います。
- Shader は `PocToonVertexColor.shader`。色は頂点カラーで、テクスチャはありません。
- FBX の取り込みでは、Material をこの共通 Material に置き換えています（`PocSceneBuilder.RemapModelMaterial`）。
- 3Dの輪郭線（1秒に6回のゆらぎ）は、画面全体の後処理です。このモデルの Material には入っていません。

## 7. Scale

- Unity での大きさは 1（`uniformScale: 1`、FBX の取り込みは file scale、`globalScale: 1`）。
- 高さは 0.968 m（飾りを含む）で、約2セル分の見た目です（1セル = 1 m の Grid の上）。
- 幅は 0.528 m、奥行きは 0.50 m。

## 8. Pivot

- 本体の原点は「足元の中心」です。ゲームでは、論理セルの中心の地面に置きます（`pivotOffset: 0`）。
- 腕のピボットは肩（±0.22, 高さ 0.462）、脚のピボットは股関節（±0.066, 高さ 0.22）です。腕と脚はここを中心に回ります。

## 9. Facing

- モデルの正面は South です。ゲームの向き（上・下・左・右）に合わせて、Y軸で回します。
- 回転の補正は `yawOffsetDegrees: 180`（`GridSpace3D.ModelFrontYawOffset`）です。
- 向きは論理セルの向き（`SharedWorldSession.Facing`）と同じで、4方向だけです。
- 確認: 4方向それぞれで、モデルの正面が Grid の向きと一致します（Phase 2.1・Phase 7 の検査）。

## 10. Idle

- 待機は静止した姿勢です。腕と脚の角度は 0 です。
- 待機のアニメーションクリップはありません。

## 11. Walk

- 歩行は手続き的な動きです。1歩（0.18 秒、1マス）の間、`ArmL` `ArmR` `LegL` `LegR` を前後に 26 度振ります。
- 1歩が終わると 0 度に戻ります。
- 論理の移動（1マスずつ、4方向、カメラ基準の入力）は、見た目の動きとは別です。見た目は移動を変えません。

## 12. Sit

座る姿勢も手続き的です（`PocActorView` の座る表示）。

| 場所 | 腰の上げ量 | 家具の中央への寄せ | 脚 | 腕 |
|---|---|---|---|---|
| ベンチ（屋外） | 0.03 | 0.08 | 90 度 | 28 度 |
| ソファー（屋内） | 0.07 | 0.06 | 90 度 | 28 度 |
| 床のクッション（今の地図では使っていない） | 0.07 | 0 | 62 度 | 28 度 |

- 座る位置（セル、アンカー `SeatAnchor_L` / `SeatAnchor_R`）は、家具と地図のとおりです。
- この表は見た目の調整です。この表の数値は、このモデルの太もも・腰に合わせて測りました（Phase 5）。

## 13. 2D Spriteとの切替

- 2D（ドット絵の固定視点）では、Sprite を描きます。
  - Sprite: `Assets/Content/Character` の 50 枚（待機・歩行の4方向、振り向き、ジャンプ、座る絵。2026-10-20 に描き直した絵）
  - 2D では 3D モデルを隠します。
- 3D では、このモデルを描き、Sprite を隠します。
- 切り替えても、セル・向き・歩数・座っている状態は変わりません。2D と 3D は同じ論理世界を見ているためです。
- Sprite 50 枚の SHA-256 は `tools/poc2d3d/player_sprite_sha256.txt` と正式モデルの記録にあります（2026-10-20 に描き直した絵の値）。記録と同じであることを検査で確かめています。
- Sprite の作り方（4方向の絵から全部のコマを作る道具）: `tools/poc2d3d/sprite_frames/`

## 14. 今後のAnimation拡張方法

今の動きは、すべてスクリプトによる手続き的なものです。増やすときは、次のどちらかにします。

1. **手続き的な動きを足す**（おすすめ。モデルを変えずにすむ）
   - `PocActorView` に新しい姿勢を足し、腕・脚の4つの部品を回します。
   - 例: 調べるときに腕を上げる、待機中にゆっくり揺れる。
   - 論理（セル、向き、0.18 秒の1歩）は変えません。
2. **Rig とクリップを入れる**
   - Blender で Armature を足して、Animation 付きの FBX を書き出します。
   - Unity で Animator を付けます。
   - このとき、次のことを守ります。
     - 原点（足元の中心）、正面（South）、大きさ（1）を変えない
     - 座る位置に合うかを測り直す（12章の表）
     - 歩行は 0.18 秒の1歩に合わせる

どちらの方法でも、2D の Sprite は変わりません。

## 15. Blender再生成手順

1. Blender 5.2 で、次を実行します（リポジトリの一番上で）。
   ```
   "C:\Program Files\Blender Foundation\Blender 5.2\blender.exe" --background --factory-startup --python blender/scripts/build_haruka_blocky.py
   ```
2. 次のファイルが作り直されます。
   - `.blend`
   - `.fbx`
   - `phase2_1_haruka_blocky_placeholder_manifest.json`
3. Unity で開いて、FBX を取り込み直します。`.fbx.meta` の GUID は変わりません。
4. `python tools/poc2d3d/player_model_manifest.py` で、正式モデルの記録（SHA-256）を作り直します。
5. Phase 2.1、Phase 5、Phase 7 の検査を実行して、見た目（向き、大きさ、座り）を比べます。

**注意**: 正式モデルは「今の見た目」が基準です。

- 同じスクリプトから作っても、Blender の版によって FBX のバイトが変わることがあります。
- 作り直したときは、SHA-256 の検査が「変わった」と知らせます。
- 見た目の比較を確かめてから、記録を更新してください。

## 16. Asset差し替え時の注意点

モデルを差し替えたり直したりするときは、次を守ってください。

- **参照を切らない**: FBX は同じパスに上書きし、`.fbx.meta`（GUID）を残します。ファイル名を変えるなら、Unity の中で移動して GUID を保ちます。
- **決まりを守る**:
  - 原点は足元の中心
  - 正面は South
  - 大きさは 1
  - 高さは約 0.97 m
  - 部品名は `ArmL` / `ArmR` / `LegL` / `LegR`（ピボットは肩と股関節）
  - Material は共通の1つ（頂点カラー）
- **座る表示を測り直す**: 12章の表はこのモデル用です。形が変わったら、ベンチとソファーで太ももがめり込まないように測り直します。
- **2Dには影響させない**: モデルを直しても、Sprite は置き換えません。2D の見た目は Sprite だけで決まります。
- **記録と検査を更新する**: `player_model_manifest.py` で記録を作り直し、Phase 7 の検査（SHA-256、三角形の数、高さ、4方向、座り）を通します。
- **セーブには影響しない**: セーブに入るのはマップ・セル・向きだけで、モデルの情報は入りません。差し替えても、セーブの形式（Schema 1）は変えません。
- **ユーザーの確認**: 正式モデルの見た目を変えるときは、ユーザーの承認を得てから行います。
