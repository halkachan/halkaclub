# はるかわーるど ver3.5 設計資料「そらをとぶ（ファストトラベル）」

作成日: 2026-10-28
対象: ゲーム本編（Unity 6000.3.10f1 / WebGL）、ブランチ `claude/harukaworld-v35`。
前提: `HARUKA_WORLD_VER32_ARCHITECTURE.md`（World座標・チャンク）、`HARUKA_WORLD_VER33_REGION_CONNECTION_SPEC.md`（Region）、`HARUKA_WORLD_VER34_CROW_COMPANION_SPEC.md`（カラスの同行モード）。
クラス名・ファイル名・数値は実際のコードと同じです。コードを変えたら、この資料も直してください。

---

## 0. 一言でいうと

**初めての場所には歩いて行く。見つけた場所には、次からカラスといっしょに空を飛んで戻れる。**

- 行き先（ファストトラベル地点）は、JSON ファイル 1 つ（`fast_travel_points.json`）に並べた **地点の定義** です。
- ﾊﾙｶﾁｬﾝが見つけた場所は、**地点の ID だけ** をセーブに残します（`extensions.fastTravel`）。
- 「そらをとぶ」は、カラスの選択肢と、左上の ☰ メニュー（屋外だけ）から使えます。
- 飛ぶのは **同行モードとは別の、一時的な移動** です。「ついてきて」「あそんでおいで」は飛んでも変わりません。

```
地点の定義（ゲームのもの）            見つけた記録（セーブのもの）
fast_travel_points.json               extensions.fastTravel.discovered
 ├─ landmark_halka_home  （はじめから）  ["landmark_east_flower_plaza",
 └─ landmark_east_flower_plaza          "landmark_halka_home"]
     （歩いて見つける）
```

## 1. ファイル

| ファイル | 役割 |
|---|---|
| `Assets/Content/FastTravel/fast_travel_points.json` | **新規**。地点の定義（2章）。`Content/Maps`（Map・World Registry・方角の色）とは別のフォルダにして、Map のデータには触れていません。 |
| `Assets/POC2D3D/Runtime/World/FastTravelRegistry.cs` | **新規**。地点の型（`FastTravelPoint`）・一覧（`FastTravelRegistry`）・検査（`Validate`）・JSON の形。 |
| `Assets/POC2D3D/Runtime/PocFastTravel.cs` | **新規**。見つける処理・行き先の一覧（メニューの1ページ）・飛ぶ処理（状態機械）・安全な着地・飛ぶ演出。 |
| `Assets/POC2D3D/Runtime/PocCrow.Travel.cs` | **新規**。飛んでいる間のカラス（止めておく・見た目だけ動かす・着地・自由行動への戻り）。 |
| `Assets/POC2D3D/Runtime/PocCrow.cs`・`PocCrow.Companion.cs` | 選択肢「そらをとぶ」（`CrowChoice.Travel`）。飛行中は話しかけられない。 |
| `Assets/POC2D3D/Runtime/PocGameplay.cs` | カラスの選択肢に「そらをとぶ」。飛行中の入力・AUTO の停止。AUTO の 1 歩かどうか（`LastStepByAuto`）。 |
| `Assets/POC2D3D/Runtime/SharedWorldSession.cs` | `RelocateOutdoorsNow()`（歩数にならない、屋外の中での移動）。 |
| `Assets/POC2D3D/Runtime/PocWorldPresenter.Chunks.cs` | `PrepareAround()`（行き先のまわりのチャンクを先に作る）。 |
| `Assets/POC2D3D/Runtime/PocActorView.cs` | `PresentationLift`（飛ぶときに ﾊﾙｶﾁｬﾝを少し浮かせて描く。見た目だけ）。 |
| `Assets/POC2D3D/Runtime/PocPixelView2D.cs`・`PocCrowView3D.cs` | World 座標→画面座標（`WorldToScreenPoint`）。`HiddenForTravel` を見る（ver3.5 では使っていません）。 |
| `Assets/Scripts/UI/GameMenuController.cs` | メニューの行に「表示する条件」を付けられるようにした（`AddLinkItem(label, action, shown)`）。条件なしの行（2D 版ゲームのメニュー）は今まで通り。 |
| `Assets/POC2D3D/Runtime/Save/HarukaSaveData.cs`・`HarukaSaveSystem.cs` | 見つけた場所の保存（8章）。 |
| `Assets/POC2D3D/Editor/PocSceneBuilder.cs`・`OutdoorWorldRegistryJson.cs` | JSON を読み、World と照らし合わせて検査し、シーンに入れる（エラーがあるとビルドが止まる）。 |
| `Assets/POC2D3D/Editor/WorldVer35Checks.cs` | ver3.5 専用の検査。 |

## 2. 地点の定義（fast_travel_points.json）

```json
{
  "format": "harukaworld-fast-travel",
  "formatVersion": 1,
  "points": [
    { "id": "landmark_halka_home", "name": "ハルカチャンの家",
      "worldId": "outdoor_main", "mapId": "first_field",
      "discoverCell": { "x": -7, "y": -5 }, "discoverRadius": 2,
      "arrivalCell": { "x": -7, "y": -5 }, "arrivalFacing": "down",
      "landingSearchRadius": 4, "unlockedAtStart": true, "order": 0 },
    { "id": "landmark_east_flower_plaza", "name": "ひがしのはなのひろば",
      "worldId": "outdoor_main", "mapId": "east_area_01",
      "discoverCell": { "x": 16, "y": 4 }, "discoverRadius": 2,
      "arrivalCell": { "x": 16, "y": 4 }, "arrivalFacing": "up",
      "landingSearchRadius": 4, "unlockedAtStart": false, "order": 10 }
  ]
}
```

| 項目 | 意味 |
|---|---|
| `id` | 安定した ID（英小文字・数字・`_`・`-`、64 文字まで）。**セーブに残るのはこれだけ** なので、一度決めたら変えません。位置を直しても、ID が同じなら「見つけた」は消えません。 |
| `name` | 一覧に出す名前。ゲームのビットマップフォント（遊びメモ書き）にある文字だけ（検査で確かめます）。 |
| `worldId`・`mapId` | どの World の、どの Region（Map）にあるか。 |
| `discoverCell`・`discoverRadius` | 見つける場所。この Map のセルから **マンハッタン距離で半径以内** に自分で歩いて入ると見つかります。 |
| `arrivalCell`・`arrivalFacing` | 着く場所と向き（Map のセル）。使えないときは近くの安全なセル（6章）。 |
| `landingSearchRadius` | 安全なセルを探す範囲（0〜8）。 |
| `unlockedAtStart` | はじめから見つけてあるか（家だけ true）。 |
| `order` | 一覧の順番（小さい順、同じなら JSON の順）。 |

セルは **Map のセル**（Region の中の座標）で書きます。World の中で Region の位置を動かしても、地点は Region といっしょに動きます。

### 今の 2 地点

- **ハルカチャンの家**（`landmark_halka_home`）: first_field の (-7,-5)。家の扉 (-7,-4) の **ひとつ下の屋外のセル**（家から出たときに立つ場所）に、下向きで着きます。扉そのものには着かないので、着いた瞬間に家に入ることはありません。
  - 表示名について: ゲームのフォントには半角カナ（ﾊﾙｶﾁｬﾝ）がないため、全角の「ハルカチャンの家」にしています。半角の表記にするには、フォントの画像（`Resources/asobi_ui_atlas.png`）に半角カナを足す必要があります。
- **ひがしのはなのひろば**（`landmark_east_flower_plaza`）: ver3.3 の ID をそのまま使っています（Map のマーカーと同じ ID・同じセル）。east_area_01 の (16,4) = World (27,-2)、ひまわり (27,-1) の正面に、上向き（ひまわりを見る向き）で着きます。

### JSON の検査（シーンを作るときと検査プログラム）

エラー（ビルドが止まる）: ID の形・ID の重複・名前がない・知らない World・知らない Region・Map の外のセル・向きの綴り・半径の範囲・はじめから見つけてある地点が 1 つもない・64 地点より多い。
警告だけ: 着くセルが歩けない（近くの安全なセルに着くので）。

## 3. 見つける（解放）

- `PocFastTravel` は、**ﾊﾙｶﾁｬﾝ自身の 1 歩が終わるたび**（`SharedWorldSession.StepCompleted`）に、まだ見つけていない地点との距離を調べます（地点の数だけの足し算。今は 2 つ）。
- **AUTO の 1 歩では見つけません**。AUTO が 1 歩を始めるときだけ `PocAutoWorld.InStep` が立ち、その 1 歩は `PocGameplay.LastStepByAuto` になります。初めての場所は自分で歩いて見つけるためです。
- Region に入っただけでは見つかりません（ひろばの正面から 2 セル以内に入ったとき）。
- 見つけたとき: 1 回だけ「**そらをとべるばしょを みつけた！**」を出し、1 秒ほどで保存します（設定の変更と同じ早さ）。2 回目からは何も出ません。
- 飛んで着いたときは見つけません（1 歩ではないので。それに、着ける場所はもう見つけてあります）。
- **古いセーブ**（ver3.0〜3.4。見つけた記録がない）: 家だけ見つけてある状態から始まります。ゲームを開いた時点で **ひろばの範囲の中にいるときだけ** その場で見つけます（メッセージも出ます）。昔ひろばに行ったかどうかはセーブからは分からないので、推測で東を開けることはしません。

## 4. 行き先の一覧（メニューの 1 ページ）

```
カラス「カァ。」                         ☰ メニュー（屋外だけ）
 ├ ついてきて / あそんでおいで            ├ AUTO / ひょうじ / せいかつきろく
 ├ そらをとぶ  ─────┐                   ├ そらをとぶ  ───┐
 └ やめる            │                   ├ セーブデータ     │
                     ▼                   └ やめる           │
              そらをとぶ  ◀────────────────────────────┘
              どこへ とぶ？
               ├ ハルカチャンの家
               ├ ひがしのはなのひろば   ← 見つけた場所だけ
               └ やめる
```

- ver3.4 と同じ **ゲームのメニュー窓**（`IGameMenuPage`）です。同じ枠・同じフォント・同じクリックとタッチの扱い（タッチは指を離したときに押される）。窓が開いている間は、後ろのゲームは動かず、AUTO も止まります。
- 見つけていない場所は一覧に出ません。4 つより多くなったら、4 つずつのページになり「つぎへ」「まえへ」が出ます（今は 2 つなので出ません）。
- 「やめる」・窓の外のタッチ: 閉じるだけで、場所・向き・カラスのモード・見つけた記録・セーブは何も変わりません。
- **今いる場所を選んだとき**（着く場所から 3 セル以内）: 飛ばずに「**もう ここに いるよ。**」と出します。
- メニューの「そらをとぶ」は **屋外にいるときだけ** 表示されます（家・防音室では行が消え、押しても何も起きません）。カラスがどこにいても（遠くで自由に暮らしていても）使えます。
- PC のキーボードの A は今まで通り左への移動です。スマホでは A ボタンで正面のカラスに話しかけます。

## 5. 飛ぶ（状態機械）

```
Idle
 │ 行き先を選ぶ
 ▼
PreparingDestination   1 フレームで全部を確かめる。だめなら何も変えずに Idle（下の表）
 ▼
DepartureAnimation     1.15 秒: カラスが飛んできて、ﾊﾙｶﾁｬﾝといっしょに浮く。空の色で画面が覆われていく
 ▼
Transferring           0.6 秒（空の中）: 最初のフレームで、行き先のチャンクを作り、ﾊﾙｶﾁｬﾝを着地セルへ、
 │                     カメラを付け直し、カラスを横へ。空にはカラスとﾊﾙｶﾁｬﾝ、流れる雲
 ▼
ArrivalAnimation       0.85 秒: 空が晴れて、行き先に降りてくる
 ▼
Complete → Idle        入力・メニュー・AUTO を戻し、着いたことを確かめ、すぐ保存
```

合計 **約 2.6 秒**（ゲーム画面で見て決めた値。`DepartureSeconds`・`CoverSeconds`・`ArrivalSeconds`）。

### 始める前の確認（PreparingDestination）

| 結果（`FastTravelStart`） | 意味 |
|---|---|
| `Busy` | もう飛んでいる |
| `UnknownPoint` | その ID の地点がない |
| `NotDiscovered` | まだ見つけていない |
| `NotOutdoors` | 屋内にいる |
| `UnknownWorld`・`UnknownRegion`・`BadArrival` | 地点の World・Region・着くセルがおかしい |
| `AlreadyHere` | もうそこにいる |
| `NoChunk` | 着くセルがチャンクのない場所 |
| `NoLandingCell` | 安全な着地セルがない（6章） |

どれかに当たったら、何も変えずに終わります（メッセージ「いまは とべないみたい。」、AlreadyHere だけは「もう ここに いるよ。」）。

### 飛んでいる間

- ﾊﾙｶﾁｬﾝの入力（キー・十字キー・A・クリック）は止まります。持ったままのキーも効きません。
- AUTO はその間だけ止まります（ON/OFF の設定は変えません。着いたあとは、また 60 秒何もしないと歩き出します）。
- メニュー（☰）と右上の「3Dへ／2Dへ」は、飛んでいる 2.6 秒だけ消えます（その間の 2D/3D の切り替えはできません）。
- カラスに話しかけることも、もう一度「そらをとぶ」を始めることもできません。
- 歩数は増えません（1 歩ではないので）。Map の読み込みもありません（屋外の World はそのまま）。Unity の Scene の読み込みもしません。
- 朝夜の時計は止めません・戻しません。夜に飛ぶと、空の色は夜の空になります。

### 移動そのもの（Transferring の最初のフレーム）

1. `PocWorldPresenter.PrepareAround(着地セル)`: 着いたときに 2D/3D の画面に映る範囲（ふだん彼女のまわりに用意する範囲と同じ）のチャンクのうち、まだ作っていないものをその場で作ります。距離は関係ありません（遠い行き先が増えても同じ処理）。着地セルのチャンクが作れなかった（作れずに無地の地面になった）ら **やめます**。
2. `SharedWorldSession.RelocateOutdoorsNow(着地セル, 向き)`: 共通の World Session の位置を直接変えます（1 歩ではない・扉は発火しない・Region は着いたセルから決まる）。
3. チャンクの読み込み範囲を彼女に合わせ直し（`UpdateStreaming(true)`）、2D と 3D のカメラを付け直します（`SnapToTarget`）。カメラが遠くからすべってくることはありません。カメラの付け直しは、画面が空で覆われている間だけです。
4. カラスを彼女の横に置きます（7章）。

**歩いて Region をまたぐときは、今まで通り暗転もカメラの付け直しもありません**。付け直しをするのは、このファストトラベルだけです。

### やめたとき（行き先のチャンクが作れなかったなど）

ﾊﾙｶﾁｬﾝは動いていません。カラスは元の場所・元の状態に戻り、入力・メニュー・AUTO が戻って、「いまは とべないみたい。」と出ます。セーブは出発した場所のままです。

### 着いたことの確認（Complete）

位置が着地セル・Region が地点の Region・そのチャンクが描かれている・歩けるセル・入力が戻っている・歩数が増えていない・カラスが止められていない、をすべて確かめます（`LastArrivalVerified`）。

## 6. 安全な着地

- 着くセル（`arrivalCell`）から、マンハッタン距離 0, 1, 2 … `landingSearchRadius`（4）までの輪を順に調べます。同じ距離では **同じ行が先、左が先**。調べるのは最大 2r(r+1)+1 セル（r=4 で 41 セル）。
- 安全なセル: 地点の Region の中・歩ける（Collision）・扉や部屋の出口ではない・カラスが立っているセルではない・チャンクがある。
- 見つからなければ飛びません（`NoLandingCell`、何も変わらない）。
- ﾊﾙｶﾁｬﾝとカラスが同じセルに着くことはありません（カラスは彼女が着いたあとで、彼女のいないセルを選ぶ）。

## 7. カラス

カラスは今まで通り **1 羽だけ** です（新しいカラスは作りません）。

| 場面 | カラス |
|---|---|
| 飛び始め | `HoldForTravel()`: 自分の判断（歩く・飛ぶ・木に止まる・追従）を止め、**論理上のセルはそのまま**。見た目の位置だけを `ShowTravelFlight()` で動かします: 今いる所（画面の外なら、彼女の横の空中）から彼女の頭の上へ飛んできて、いっしょに上がります。2D では 2D 版の飛ぶドット絵、3D ではローポリのカラスの飛ぶ姿です。 |
| 空の中 | 画面いっぱいの空に、カラスと、その下の ﾊﾙｶﾁｬﾝ（2D のドット絵、正面向き）が飛んでいく絵を描きます。これは絵で、カラスの状態とは関係ありません。 |
| 着地 | 空の中で `LandBeside()`: 彼女の **後ろ** を最初に、次に横、なければ近くの空いたセルに置きます（扉・彼女のセル・彼女の進む先の 2 セルは使わない）。空が晴れる間に、上から降りてくる姿が見えます。 |
| 追従（ついてきて） | 飛んでも追従のまま。着いたら、そのまま彼女といっしょに歩きます。家の前に着いて家に入れば、ver3.4 と同じく外で待ちます。 |
| 自由行動（あそんでおいで） | 飛んでも自由行動のまま（追従にはなりません）。彼女の横に降りて、**1.2 秒後に自分の木へ帰ります**（あそんでおいでと同じ `FlyHome`: 東エリアにいれば first_field の木へ飛んで帰る、first_field なら近くの木へ）。東エリアに残ったままにはなりません。 |
| やめたとき | 元の場所・元の状態（歩いていた・飛んでいた・止まっていた）にそのまま戻ります。 |

## 8. セーブ

Save Schema は **1 のまま** です。ver3.4 の `extensions.crowCompanion` と同じやり方の、チェック付きの拡張です。

```json
"extensions": {
  "crowCompanion": { "mode": "follow", "checksum": "crc32:…" },
  "fastTravel": { "discovered": ["landmark_east_flower_plaza", "landmark_halka_home"], "checksum": "crc32:…" }
}
```

- `discovered`: 見つけた地点の ID（並べ替え済み・重複なし）。**家しか見つけていないときは書きません**。その場合のセーブは ver3.4 とまったく同じ形です（`"extensions": {}` か `crowCompanion` だけ）。
- `checksum`: `"fastTravel|" + ID をカンマでつないだもの + "|" + 本体のチェックサム` の CRC-32。本体のチェックサムを含むので、他のセーブの `fastTravel` を貼り付けても通りません。
- `crowCompanion` と `fastTravel` は **別々の項目** で、両方同時に保存できます（片方がもう片方を上書きすることはありません）。
- 本体の値とチェックサムは今までと同じです。だから ver3.0〜3.4 も、このファイルをそのまま読めます（知らない拡張は無視するため）。

### 読むときの扱い

| 中身 | 扱い |
|---|---|
| ない | 家だけ見つけてある（古いセーブ・新しいゲーム） |
| 正しい | そのまま使う |
| 同じ ID が 2 回（チェックサムは正しい） | 1 回として読む |
| 形の正しい、知らない ID（後の版の場所） | **消さずに持っておき、次の保存にもそのまま書く**。一覧には出さない |
| ID の形がおかしい・文字列でない・リストでない・64 より多い・チェックサムがない／違う・項目がオブジェクトでない | そのセーブは **壊れている** 扱い（読まない）。スナップショットの古い世代（バックアップ）から読み直します（ver3.0 Phase 6 からの仕組み）。エクスポートしたファイルなら、インポートを断り、何も変えません |

### 保存のタイミング

- 見つけたとき: 1 秒ほどで（設定の変更と同じ）。
- 飛んでいる間: セーブには **出発した場所** が書かれます（ページを閉じても、飛び始める前の場所から再開。途中の場所は書きません）。
- 着いたとき: すぐに行き先を保存します（`fast travel`）。

### 古い版へ戻した場合（Rollback）

ver3.4 以前の版でこのセーブを開くと、場所・生活記録・設定・カラスのモード（ver3.4）はそのまま読めます。ただし、その古い版が保存し直すと、`fastTravel` の項目は書かれないので、**ひろばを見つけた記録だけが消えます**（ひろばへもう一度歩けば、また見つかります）。公開前に念のためエクスポートしておくと安心です。

### エクスポート・インポート

エクスポートしたファイルには `fastTravel` も入ります。インポートすると、見つけた場所もそのファイルの内容になります（そのファイルに記録がなければ、家だけになります）。

## 9. 2D／3D

- 移動は 2D/3D 共通の `SharedWorldSession` だけを変えます。2D と 3D で別々の位置はありません。
- 2D で飛べば 2D のまま、3D で飛べば 3D のまま着きます（勝手に切り替えません）。
- 位置・Region・向き・カラスのモード・見つけた記録は、どちらの表示でも同じです。

## 10. 性能

- 見つける判定: 1 歩ごとに、まだ見つけていない地点の数だけの距離計算。
- 飛ぶ処理: 空の中の 1 フレームで、行き先のまわりの、まだ作っていないチャンクを作ります。今の World（2 Region）では、ふだんから World 全体が用意されているので作るチャンクは 0 です。遠くて用意されていない行き先でも同じ処理で、Editor の検査では 2 チャンクを 4.5ms で作りました（実際のブラウザでの数値は実装レポートの性能の章）。
- 空で覆っている間に作るので、もし少し時間がかかっても見えません。

## 11. 新しい行き先の足し方（北・南・西の地域ができたとき）

1. 新しい Region を ver3.3 の手順（`HARUKA_WORLD_VER33_REGION_CONNECTION_SPEC.md` 11章）で足します。
2. `Assets/Content/FastTravel/fast_travel_points.json` の `points` に 1 つ足します。
   - `id`: 新しい安定した ID（例 `landmark_north_lake`）。あとで変えない。
   - `name`: ゲームのフォントにある文字で。
   - `mapId`: 新しい Region の Map ID。`discoverCell`・`arrivalCell`: その Map のセル。
   - `unlockedAtStart`: false（歩いて見つける）。`order`: 一覧の順番。
3. シーンを作り直す（`PocSceneBuilder.BuildReleaseCandidateBatch`）。JSON にエラーがあればここで止まります。
4. `WorldVer35Checks` を流す（地点の検査は、JSON のすべての地点に対して働きます）。

コードを足す必要はありません。見つける処理・一覧・飛ぶ処理・着地・セーブは、どの地点も同じ処理です。
