# HALKA WORLD MAP EDITOR v0.2 Standalone Edition

ゲーム本編は **ver2.0** のままです。このツールは開発専用Windowsアプリで、ゲーム画面には出ません。

## 起動と保存

`tools/HalkaWorldMapEditor/scripts/publish-win-x64.ps1` でwin-x64の自己完結版を作り、`tools/HalkaWorldMapEditor/dist/win-x64/HALKA WORLD MAP EDITOR.exe` を起動します。EXEと同じフォルダーのDLL群をまとめて保持してください。Unityを起動しなくてもマップを編集できます。

初回起動時はEXEの親ディレクトリから `unity-project` を探索します。見つからない場合はUnity Projectフォルダーを選択します。前回のProject、Map、Zoom、Pan、Overlay設定は `%LOCALAPPDATA%/HALKA WORLD MAP EDITOR/settings.json` に保存します。

正式な編集元は `Assets/Content/Maps/Authoring/first_field.hwmap.json` です。`object_catalog.hwcatalog.json` はPaletteとSpriteプレビューのカタログです。`Assets/Content/Maps/first_field.asset` はUnityがJSONから生成するRuntime用キャッシュで、直接編集しません。

## 操作

| 操作 | 内容 |
|---|---|
| 左Palette + 配置 `[2]` | DirtまたはStone/Flower/Treeを配置。Dirtはドラッグで連続塗り。 |
| 選択 `[1]` | Objectを選択し、右InspectorでInstance IDとRoot Cellを確認。座標へ移動。 |
| 消去 `[3]` / 右クリック | セル上のObject、なければSurfaceを消去。Dirt削除後は通常Grassが自動で戻る。 |
| `Ctrl+Z` / `Ctrl+Y` | Undo / Redo。Dirtドラッグは1操作。 |
| `Ctrl+C` / `Ctrl+V` | 選択Objectの型をコピー。空セルを選択して貼り付けると新しいInstance ID。 |
| `Delete` | 選択Objectを削除。 |
| ホイール | カーソル中心Zoom。 |
| 中ボタン、Space+左ドラッグ | Pan。 |
| `F` / `G` | 全体表示 / Grid切替。 |
| `Ctrl+S` | 検証後にJSONを原子的に保存。前版は `.bak` へ退避。 |

Grid、Grass、Collision、座標、Markersは画面上部で切り替えられます。House、Player、Crow、道路終端はPreviewと保護セルです。House・室内・AIなどの自由編集は対象外です。検証結果をダブルクリックすると該当セルへ移動します。

## データ契約

JSON Schemaは `tools/HalkaWorldMapEditor/halka-world-map.schema.json`。座標は `{ "x": 0, "y": 0 }`、地面は `definitionId` と `cell`、Objectは `instanceId`、`definitionId`、`rootCell` を持ちます。ObjectのInstance IDは移動や再保存で保持し、コピー時だけ新規発行します。Grassは保存せず、Surface overrideと障害物から派生します。

カタログの `definitionId` はUnityの `SurfaceDefinition.StableId` / `WorldObjectDefinition.StableId` と一致させます。Unity Importerは全カタログ項目のSpriteパス・サイズ・footprint・通行とGrass除外設定を検証します。将来Objectを追加する場合はSprite、Unity Definition asset、Catalog entryを同じIDで追加してください。

UnityのAssetPostprocessorはAuthoring JSONの変更を検知して生成キャッシュを同期します。`ProjectBuilder.PrepareScene`、`ProjectBuilder.BuildWeb`、`Version20Checks`も開始時に強制同期します。JSONが壊れている場合はエラーとなり、正常なMapDefinitionを上書きしません。旧Unity Editor Windowの編集UIは撤去し、生成MapDefinitionのInspectorを読み取り専用にしました。

## 検証と配布

`dotnet run --project tools/HalkaWorldMapEditor/HalkaWorldMapEditor.Tests -c Release` でCoreの契約テストを実行します。Unity側は `HALKA WORLD > Validate Standalone Map Contract v0.2` またはバッチ `-executeMethod Halka.Game.Editor.MapAuthoringChecks.Run`。このテストは一時fixtureを使用し、正式Mapを変更しません。

配布時は `dist/win-x64` フォルダー全体をコピーしてください。JSONとSpriteはUnity Project内に残り、EXEがProjectを検出または選択して編集します。Map変更をWebGLへ反映するにはUnityで本番Buildとサイト公開が必要です。
