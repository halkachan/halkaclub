# HALKA WORLD MAP EDITOR v0.1

ゲーム本編のバージョンは **ver2.0** のままです。このツールはUnity Editor専用で、WebGL画面には表示されません。

## 開き方

Unityで `unity-project` を開き、メニュー **HALKA WORLD > Map Editor** を選びます。上部の `Map` で `Assets/Content/Maps/first_field.asset` を選びます。v0.1で編集できるのはこの屋外マップです。

## 基本画面と操作

- 左パレット: `Surface` のDirt、`Object` のStone・Flower・Tree。新しい同型のObject Definition assetも自動で候補に出ます。
- 中央: 実Spriteを使った2Dマップ。GridはEditorにだけ表示されます。マウス位置の `Cell (x,y)` は下のStatusに表示されます。
- 下部: 選択ObjectのType、Root Cell、Definition ID、Footprint、Move To Cell、状態とValidation結果。
- マウスホイール: カーソル位置を中心にZoom。中ボタンドラッグ: Pan。Spaceを押しながら左ドラッグでもPan。
- `1` Select、`2` Paint、`3` Erase、`G` Grid切替。左のToggleでGrass、Surface、Object、Collision、Coordinatesの重ね表示を切り替えます。
- `Select`: Objectを選択。Treeは3×4の見た目範囲から選べ、Root Cellは黄色枠と `R` で表示します。House、Crow spawn、Player spawnはLOCKEDです。
- `Paint`: パレットでDirtまたはObjectを選び、セルを左クリック。Dirtは左ドラッグで連続塗りできます。Objectは同じRoot Cellへ重複配置できません。
- `Erase`: 現在のLayer（SurfaceまたはObject）のみ削除します。右クリックも同じLayerを消します。Dirtを消すと歩ける非Objectセルでは草が戻ります。
- 選択Objectの座標修正: 下部の `Move To Cell` と `Move`。House footprint、入口、Crow/Player spawn、四方のRoad EndはObject配置を拒否します。
- `Ctrl+Z` / `Ctrl+Y`: Unity Undo/Redo。`Delete`: 選択Objectを削除。
- `Save Map` / `Ctrl+S`: 明示保存。編集時には `DIRTY` を表示し、1クリックごとの自動保存はしません。Map切替時に未保存なら確認します。
- `Validate Map`: 範囲外、Definition欠損、重複Surface/Object、家入口・道終端の閉塞、不正なboundsを表示します。問題がない場合は `VALID`。

## データとゲームへの反映

`MapDefinition` ScriptableObject（`first_field.asset`）が配置の唯一の元データです。Map ID、Display Name、DataVersion、bounds、Surface Override、World ObjectのDefinitionとRoot Cell、Player Spawn、Entity Spawn、家入口、四方のRoad Endを保持します。Base GroundとGrassをセルごとに保存しません。Grassの表示条件はEditorとRuntimeが共通の `MapPlacementRules.HasGrass` を使います。

`SurfaceDefinition` はID・表示名・Spriteを持ちます。`WorldObjectDefinition` は安定ID、表示名、Sprite、調査メッセージ、Collider、footprint、Rooted Sprite配置、通行・草除外、Sorting Orderを持ちます。Stone/Flower/Treeはそれぞれassetです。新しい同型Objectを追加する場合、SpriteとDefinition assetを作り、ID、見た目、調査文、Colliderなどを設定すればPaletteに出ます。新しい固有挙動が必要なObjectはRuntime Loader側の機能追加が必要です。

`MapRuntimeLoader2D` はシーン起動時にMapDataから土・Object・Grassを生成します。Scene自体には屋外マップの大量の配置Objectを永続保存しません。Mapを編集して保存すれば次のPlayで反映され、WebGLはビルドし直すと反映されます。既存の家、室内、カラス、Player、AUTOのロジックはSceneのSystem Root側にあります。

## 注意事項

- v0.1の編集対象は屋外のDirt・Stone・Flower・Treeです。House、Crow、Player spawnや室内は表示のみ／編集対象外です。
- World ObjectはDirtと同じセルへ配置可能です。そのセルにはGrassは出ません。
- `Validate Map`のERRORを修正してから保存・Buildしてください。自由編集後のGrass数は変化するため、201という数字は移行時点だけの値です。
- `HALKA WORLD > Validate Map Editor v0.1` は一時Map assetで編集・Undo/Redo・Save/Reloadを検証し、`first_field.asset` は変更しません。
- Play中のLive Edit、範囲選択、複数Mapの室内編集、Area Transition編集はv0.1にはありません。
