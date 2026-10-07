# HALKA WORLD MAP EDITOR v0.6 実装レポート

作成日: 2026-10-07。開発ツールは v0.6、HALKA WORLD 本編は ver2.0 のまま。HP 更新履歴は変更していない。検証用の Area A/B は正式 Map に配置していない。

## 1. 開始状態、Git、バックアップ

- v0.5 開始時の `git status` は `first_field.hwmap.json`、生成済み `first_field.asset`、`FirstDay.unity` の変更と、未追跡の `backup-v05-20261006-174557/` だった。作業前の記録は `C:\Users\owner\Documents\ChatGPT\HALKA-v06-backup-20261006-215426` に保存した。
- ユーザーの `first_field` 編集を `be85cef` (`Preserve current first field authoring edits`) として保全し、既存 main を取り込んだ `2086b23` を v0.6 開始点とした。この状態を main に push してから v0.6 を進めた。force push は使用していない。
- バックアップは同フォルダーの `repo.bundle`、正式 Map / 生成 Asset / Scene、作業前 SHA-256、status、diff を含む。`git bundle verify` は成功した。旧 `backup-v05-20261006-174557/` は追跡せず、削除もしていない。
- 作業前の正式 Map SHA-256: `first_field` = `3E35EB37A26C0D172FD39387ADCBC7D7586C982B7F78329E0C4BEBB9B9B4083D`、`halka_house` = `227D25A43D7898FC149FE570B05C7670A768F7A89E951937D580413B731310B9`。v4 後のファイル SHA-256 は順に `6D9D008E7A2EBCD71F15DAF4E26F6B06D77D5478632888FABF6C884B602DE531`、`D72AC15286BF5BDDFE714895F03F75F5C26834E48D24EA7B4578B132CCA78D6D`。JSON から `formatVersion` と `areaTransitions` を除いて比較すると両 Map とも開始点と一致した。配置・Surface・Entity・Marker は意味上変更していない。

## 2. Map v4 と Area Transition

- Map JSON / Schema を v4 にした。各 Map の `areaTransitions[]` は安定した `instanceId`、Map 内で識別する `transitionId`、`sourceCell`、`exitDirection`、`destinationMapId`、`destinationCell`、`arrivalFacing` を持つ。v3 読込時は空配列の v4 へ移行する。正式 `first_field` / `halka_house` の配列は空で、Road End Marker はそのまま残した。
- Trigger は `edge_exit`。Player が通行可能な Source Cell から指定方向へ**手動で Map 外へ移動入力**した時だけ発動する。Source はその方向の端セル、Destination は存在する Map の通行可能セルである必要がある。到着向きは上下左右から指定する。同一 Source / Direction や ID の重複、未知 Map、範囲外・閉塞 Destination、Crow Spawn との到着重複は検証で拒否する。逆方向は別 Transition として配置する。
- `Area Transition [4]` ツールで端セルを配置・選択できる。Destination Map を選び、Inspector で各フィールドを変更する。Destination Pick Mode は相手 Map のセルを一時表示して選択し、右クリックかキャンセルで戻る。Area Transitions Overlay は出口の矢印と行き先を示す。移動、削除、Undo / Redo、Save / Reload に対応する。Pick 中は編集を行わず、未保存 Map の切替も保護する。Map 一覧・接続手順はドキュメントに記載した。
- Runtime は既存の `PlayerMover` 入力経路で外向き手動入力を検出する。既存の Player を保持したまま、元 Map の Loader を Unload、Root を非表示、次 Map の Root / Loader を有効化し、到着 Cell / Facing を設定する。GrassField 参照、Camera bounds と size を更新して即時 Snap する。到着地点の Crow Spawn と重ならないことを確認する。AUTO からは起動せず、家の通常歩行による入退室は独立して維持する。エリア移動自体は歩数・足音を増やさない。

## 3. 検証

- **Core**: v0.5 の 158 件から 23 件追加し、181/181 成功。v3→v4 移行、公式 Map の空配列、追加・変更・削除と Undo / Redo、端判定、Destination、往復接続、安定した保存を確認。既存テストを削除していない。
- **Unity Contract**: `MapAuthoringChecks.Run` 成功。テスト専用 A/B Map で、`PlayerMover.ApplyDirection` による手動 A→B→A、AUTO では移動しないこと、到着 Cell / Facing、Camera Snap、Map 切替を確認。ログ: `C:\Users\owner\Documents\ChatGPT\HALKA-v06-backup-20261006-215426\validation\mapchecks-final.log`。
- **Game regression**: `Version20Checks.Run` 成功。家・Entity / Crow・Surface / Grass・World Object の正式 Map 回帰を確認。ログ: 同 `validation\version20-final.log`。WPF Release build は警告 0、エラー 0。
- **実 EXE GUI**: 正式 Map のコピーを使う `C:\Users\owner\Documents\ChatGPT\HALKA-v06-gui-test-20261007` で A 北端→B、B 南端→A を追加。Destination Pick、Save / Reload、Source Move と Undo、削除と Undo、Arrival Facing と Undo、無効 Destination `(99,99)` の拒否を確認。正式 Map の配置には触れていない。
- **ブラウザ**: localhost の本番 WebGL を実ブラウザでロードし、ver2.0、屋外の家・草・土・木・花・石・カラス、AUTO UI、PC キー入力と `touchControls=1` の十字キー / A 表示を目視。ブラウザ上の A→B→A はテスト Map を公開 WebGL に組み込んでいないため**未確認**。家の入退室も今回ブラウザで成功を断定できる操作結果は得ていない。Unity の Runtime Fixture と区別する。
- **MutationObserver**: ローカルブラウザの Console に `Failed to execute 'observe' on 'MutationObserver': parameter 1 is not of type 'Node'` が一度出た。`halkaworld` HTML と WebGL Template に該当呼出しは見つからなかった。発生元は特定できておらず、本件の Area Transition 起因とも断定しない。
- **実スマホ**: 物理端末では未確認。`touchControls=1` はブラウザのスマホ操作プレビューのみ。

## 4. 配布と公開

- `publish-win-x64.ps1` を実行し、`tools/HalkaWorldMapEditor/dist/win-x64/HALKA WORLD MAP EDITOR.exe` を Release 再発行した。最新版 EXE の起動、`HALKA WORLD MAP EDITOR v0.6` タイトル、応答を確認した。配布時は `dist/win-x64` 全体を使用する。
- 本番 WebGL Build 成功。Development Build OFF、Unity Splash / Logo OFF、`stripEngineCode=false` を維持。Wasm `b678e2250cb547416bf6261f05ef806f.wasm` は 24,088,440 bytes、data `f221a286f5a520dbfe4c4f2e8eac9ced.data` は 4,854,012 bytes。公開先は [HALKA WORLD](https://halkaclub.com/halkaworld/)。
- 実装コミットと main push、最終 `git status` は公開後の記録を参照。

## 5. 今後の候補

新しい Map の Editor 内作成、より大きい Map の描画最適化、接続グラフの俯瞰、実ブラウザでのテスト Map 往復・物理スマホ操作の検証。今回の v0.6 と本編 ver2.0 には含めない。

## 公開後の記録

- 実装コミット: `079ccc8` (`Add Map Editor v0.6 area transition authoring`)。
- 既存 main のサイト更新を衝突なく取り込んだ統合コミット: `7f772fd`。その時点で `git push origin HEAD:main` 成功 (`9652737..7f772fd`)。`halkaworld/index.html` の OGP 設定、ver2.0 更新履歴、新 WebGL build URL が共存することを確認した。
- このレポート追記時点の `git status`: 未追跡 `backup-v05-20261006-174557/` のみ。これは作業開始前から存在するユーザーバックアップで、コミット・削除しない。レポート自身の追記は別コミットで main へ送る。
