# HALKA WORLD ver2.6 実装レポート

作業日: 2026-10-10（JST）
本編: ver2.6 / MAP EDITOR: v0.6 / Map JSON: v4

## 1. 開始状態と保全

- 開始HEADとorigin/mainはともに `d039acf7e10f326a1ded38f23b2158d9d608d960`。開始時の作業ツリーには `M unity-project/Assets/Content/World/GrassDecoration.prefab` と `?? backup-v05-20261006-174557/` があった。両方を本作業の変更として扱わない。
- repo外バックアップ: `C:\Users\owner\Documents\ChatGPT\HALKA-v26-backup-20261010`。Git bundle、開始時status/diff、正式Map、Catalog、Definition、Scene、主要Script、井戸PNG、旧WebGLファイル一覧、HP HTML、SHA-256を保存。
- `GrassDecoration.prefab` の開始時SHA-256は `AF2C0D69096B25658261B299BB7A8173DEBEC1B94BD599339615359E92857D09`。最終状態で再照合する。

## 2. 既存井戸と実装方針

- 既存Catalogの `well_basic` は `Assets/Content/World/well.png`、32×32px、1×1セル、Root `(0,0)` のみ通行不可、草除外。Sprite SHA-256は `4D39D4944D629335A3CDF9F0287F07F543D9DBDE4269CA453CA2082B61FC1C8C`。画像自体は変更していない。
- 既存Unity Definition `well_basic.asset` には `examine_front`、Player offset `(0,-1)`、Facing `up`、Action Type `examine`、文章「いど。」が同期済み。Sprite importはPoint、PPU64、MipMapなし、非圧縮で、他のWorld Objectと同じ。
- 従来のRuntime Loaderでは1セルの通常Examineに `ExamineInteractable` を付けるため、井戸のAction Point Metadataが操作方向の制約に使われていなかった。`MapRuntimeLoader2D` が井戸を既存の `WorldObjectActionInteractable` 経路で生成するようにし、Message UIへDefinitionの「いど。」を渡した。専用UI、座標固定、Sprite再生成はない。
- 衝突はDefinitionのBlocked Footprintから既存 `GridObstacle` を作る。`PlayerMover` の入力・移動補間、Life Log、AUTO、Font、Camera、Player Sprite、WebGL表示設定は変更していない。

## 3. 正式Mapへの初期配置

| Object | Map | Root Cell | Instance ID | Action Cell |
| --- | --- | --- | --- | --- |
| well_basic | first_field | (-2, -2) | `obj_well_basic_v26_first_field` | (-2, -3), Facing up |

- 家の東側から数セル離れた草地で、道上のセル `(-2,-3)` から調べられる。井戸Rootは既存の土の道・花・石・木・家・ベンチ・看板・Player/Crow Spawnと重ならない。
- `first_field.hwmap.json` はObject Instance 17→18件。追加された1件を除外して作業前JSONと比較した結果、残るすべてのプロパティが同じ。`halka_house.hwmap.json` のSHA-256は作業前と同じ `CE451E4B5E7380B5E99F1BCB148E265FBACD9041F3512C8F7E91BF7F6174448E`。
- Map v4の `instanceId`、`definitionId`、`rootCell` を使った通常配置なので、MAP EDITORで後から移動・保存できる。井戸に固有のMap Schema変更はない。

## 4. 自動検証

- `Version26Checks.Run` は `Version254Checks.Run` とその下位のGameplay回帰を実行したうえで、GameVersion、井戸Sprite/Import、Definition、Blocked Footprint、Action Point、正式配置の一意性、重なりなし、Player Spawnからの到達可能性、House入口と4方向の道、Crow巡回範囲との分離を確認する。
- Runtime Fixtureで同一Spriteの生成、正面からのAction、井戸セルへの侵入失敗、StepCompletedなし、既存Message UIの「いど。」を確認。AUTOが使う `GridPathfinder2D` も井戸セルを経路に採用しないことを検査する。
- `Version26Checks.Run`: AUTO経路探索の追加検査を含む最終実行成功。`Version26Checks-final.log` に `HALKA ver2.6 well placement, interaction, collision and inherited checks passed.` と終了コード0を記録。ver2.5.4以下の回帰も実行された。
- 公開版の家具操作を追跡するため、室内Mapの正式な机配置を読み込み、`InteractionRouter.TryInteractAhead` を通して「つくえ。」がHUDへ届く検査を追加した。現行Root `(2,1)`、手前セル `(2,0)` で追加後の `Version26Checks.Run` も終了コード0で成功（`Version26Checks-final-desk.log`）。
- `MapAuthoringChecks.Run`: `HALKA WORLD Standalone Map Contract v0.6: passed.`
- MAP EDITOR Standalone Core Tests: 181/181 passed。

## 5. MAP EDITOR v0.6

- repo外の複製Projectへ正式Mapをコピーし、実EXEで `first_field` を開くと井戸の絵が正式Map上に表示され、Map情報はObject 18件になった。複製時にCharacter画像を含めずValidationに不足表示が出たため、画像を複製Projectへ追加した。GUIでの井戸の移動・Undo/Redo・Save/Reloadは未確認。Core Tests 181件は成功しており、正式Mapのテスト操作保存は行っていない。

## 6. Production BuildとローカルWebGL

- `ProjectBuilder.BuildWeb` 成功、Unity batchmode終了コード0。Development Build OFF、Splash/Unity Logo OFF、`stripEngineCode=false` を維持。`ProjectSettings.bundleVersion` とゲーム画面は `2.6`。Build hashはWASM `2ea0cef4c9d3cf23230d0503c4367e92`、Data `4b068177e1ad7933a00d368330bb7445`。
- ローカルProduction WebGLの正式Mapをブラウザで起動。井戸のSpriteが家の東側に表示され、Playerが道から井戸正面 `(-2,-3)` へ移動できた。上入力では井戸Rootへの移動が止まり、Aボタンで既存の遊びメモ書きMessage UIに「いど。」を表示した。Crowの移動、Bench/Sign/Houseの表示、Menu、AUTO ON/OFF、Life Logの保存済み歩数・時間表示も確認。井戸操作時に歩数が増えないことはRuntime Fixtureで確認。UI実操作では一連の移動中に歩数が増えるため、操作前後の厳密な歩数差分は採取していない。
- `touchControls=1` と390×844 Mobile PreviewでD-pad・Aボタン・井戸Sprite・Menu・Life Logの表示を確認した。物理スマホは未確認。ベンチ着席、House入退室、クッション・机操作は今回ローカルブラウザで再操作していない。これらはVersion254以下の自動回帰で確認した。
- 確認画像: repo外バックアップの `local-well-interaction.png`、`mobile-well-preview.png`、`mobile-life-log.png`、`local-hp-v26.png`。Player、ベンチ座りSprite、Font、Camera、Canvas、WebGL解像度設定は変更していない。

## 7. HP、公開、最終状態

- HP上Archive最上段へ `ver2.6`、2026-10-10、「井戸を追加しました。」を追加した。旧履歴の文面と順序は維持。ローカル公式ページで `ver2.6`、2つのArchive初期Closed、上Archiveの順序を確認した。
- 実装Commit: `979e3dfe987a324c3852b68052bfef175cc8c1a0`（`Add well to HALKA WORLD ver2.6`）。`origin/main` へ通常pushし、GitHub Pagesの当該CommitのBuildは `built` となった。force pushは使用していない。
- 公開HP `https://halkaclub.com/halkaworld/` で現在Version `ver2.6`、新しいWASM/Dataの参照、2つのArchive初期Closedを確認。上Archiveを開くと最上段が `ver2.6 / 2026-10-10 / 井戸を追加しました。` で、旧履歴が新しい順に続く。
- 公開Production WebGLをブラウザで実操作した。Playerが井戸正面 `(-2,-3)` に到達し、井戸セルへ上入力しても位置は変わらず、Aボタンで「いど。」を表示した。公開版の操作画像はrepo外バックアップの `public-well-interaction.png`、HP履歴画像は `public-hp-v26.png`。
- 追加の公開ブラウザ操作では、ベンチの左・右座席でそれぞれ着席と立ち上がり、看板の「ここは HALKA WORLD。」、House入退室、クッションへの移動、室内MenuのAUTO OFF表示、Crowの移動を確認した。机のSpriteと通行不可も画面で確認した。
- 机のA/クリック後、公開ブラウザの短時間Messageをスクリーンショットへ記録できなかった。原因の切り分けとして、同じProduction設定のローカル診断Buildで机の正面 `(2,0)` からAを押し、入力方向 `(0,0)`、Facing up、机Collider、Action Point判定、`ShowMessage("つくえ。")` 到達をブラウザログで確認した（repo外 `desk-browser-diagnostic.log`）。一時的な開始位置変更と診断ログは削除し、通常Buildへ戻した。**公開画面での机Message目視は未確認**として残す。
- 報告書以外の実装・Build・HPは上記Commitに含めた。正式Map差分は井戸Instance 1件のみで、Editor v0.6 / Map v4を維持。
- 最終Git状態: 作業開始前からの `M unity-project/Assets/Content/World/GrassDecoration.prefab` と `?? backup-v05-20261006-174557/` のみを残した。両方ともCommitしていない。
- 物理スマホ、MAP EDITORでの井戸Move/Undo/Redo/Save/ReloadのGUI操作、公開画面での机Message目視は未確認。ver2.7の「4方向の道に個性を付ける」は今回実装していない。
