# HALKA WORLD MAP EDITOR v0.5 実装レポート

作業日: 2026-10-06。開発ツールは **v0.5**、HALKA WORLD 本編は **ver2.0** のまま。HP更新履歴は変更していない。

## 1. 概要と作業開始状態

v0.4 の Surface、World Object、Action Point に、動的 Entity の開始位置を編集する機能を加えた。共通 Palette に「ﾊﾙｶﾁｬﾝ開始位置」と「カラス」を追加し、位置、向き、カラスの相対行動範囲を Map JSON から Unity Runtime まで接続した。新しい画像や Entity の複製生成機構は追加していない。

開始時は `codex/unity-world-v1`、HEAD `f970530bb9cd956073cd38594d52fd0787a14d9b`、`origin/main` より2コミット先だった。`first_field.hwmap.json` にユーザーの未コミット編集があり、`backup-v05-20261006-174557/` は未追跡だった。後者には作業前の `repo.bundle`、両正式 Map、Catalog、開始時状態を保存した。最初の Core Tests は 137/137。

作業前の SHA-256 は `first_field.hwmap.json` が `AE174E1B7C8F720530D51D181EEB1C5A788688CF7AB5CC3DCD5122227C4B8E37`、`halka_house.hwmap.json` が `2980AED2B38063CF302B38A52A681FF249619FB6ED8668156D3ED5122A8FA1F2`。屋外は Surface 62、Object 15、Marker 6、Player `(0,0)`、Crow `(7,2)`。室内は Surface 50、Object 1、Marker 2。

## 2. Map v3 / Entity Catalog / Definition

Map v3 の `entitySpawns` は `instanceId`、`definitionId`、`cell`、`facing` を保持する。配列とプロパティ順を安定化し、Surface / Object / 既存 Marker を保持する。旧 `player_start` と `crow_spawn` Marker は Entity Spawn へ移行し、道路端と室内入退室 Marker は残した。移行スクリプトは `tools/HalkaWorldMapEditor/scripts/migrate-v04-to-v05.py`。

共有 `entity_catalog.hwentitycatalog.json` とその schema を追加した。Unity の `EntityDefinition` と `player_main.asset` / `crow_main.asset` は、この Catalog の Player / Crow の既存 Sprite、既定向き、Runtime Behavior、最大数、相対 Wander Region を反映する。`player_main` は Persistent Player の game-start Spawn、`crow_main` は map-local の Crow Spawn。Crow の相対範囲は X `-3..+1`、Y `-2..+2` で、初期 `(7,2)` なら従来の X `4..8`、Y `0..4` になる。

正式 Map の作業ツリーはユーザー編集を保持した Schema Migration で、最終 SHA-256 は屋外 `3E35EB37A26C0D172FD39387ADCBC7D7586C982B7F78329E0C4BEBB9B9B4083D`、室内 `227D25A43D7898FC149FE570B05C7670A768F7A89E951937D580413B731310B9`。実装コミットには開始時の未コミット配置変更を含めず、HEAD 側の Map を v3 に移行したものだけを記録した。そのコミット内の屋外 Map SHA-256 は `2077F60CCFA6C6102C83B88D502AF33808B77B9B83F8A5C057FC3697148ABD2C`。作業ツリーのユーザー編集は別途保持している。

## 3. Standalone Editor の操作

両 Map 共通の「キャラクター / 生き物」Palette、Entity 表示 Overlay、Crow Wander Region の枠、選択時の Inspector 座標・Facing を追加した。Entity の配置、移動、向き変更、削除、Undo/Redo、Save/Reload に対応する。Player Start は World 全体で1個、Copy禁止、Delete確認あり。Crow は現行 Runtime 上限1羽。Spawn は Map 内の通行可能セルだけに置ける。範囲が Map 外へ出る場合は Warning とし、実移動は Bounds で制限する。

複製 Test Map 上で、最新版 EXE の v0.5 表示、Player/Crow の選択、Crow `(7,2)→(6,3)` と左向き変更、Save/Reload、Undo/Redo、Player `(0,0)→(0,-3)`、Delete 確認とキャンセル、室内への Crow 追加と描画を確認した。正式 Map は GUI の破壊操作に使っていない。ブロックセルへの GUI ドロップ自体は目視未確認で、Core Validation で拒否を検証した。

## 4. Runtime と既存機能

`EntityRuntimeFactory2D` が定義済み Behavior に従って、既存の Persistent Player と Crow へ Spawn を適用する。Player は Map 切替時に生成し直さず、室内に Start を置く場合の開始 Map 選択も可能。Crow は Spawn に相対した Wander Region を使い、従来の歩行補間、Dynamic Occupancy、Player との衝突防止、草の足元遮蔽、ガサゴソ、鳴き声を維持する。AUTO が Crow を目標にする場合も現在位置を参照する。旧 Crow 固定座標と旧 actor Marker 依存を Runtime から除いた。道路・家遷移など他の Marker は維持する。

Unity の一時 fixture では Crow を `(0,3)` に移し、相対 Wander Region が X `-3..1`、Y `1..5` へ追従することを検証した。室内 Crow `(3,0)` の Runtime Loader 配置と相対範囲も検証した。Player fixture は `(0,-3)` および室内 `(1,0)` と左向きの適用を検証した。移動後の Crow 専用 WebGL は作成していない。

## 5. 検証・ビルド

- Core Tests: **158/158**。開始時 137 から 21 増加。Entity Catalog、v2→v3 Migration、安定保存、位置・Facing・Undo/Redo、重複 Player/Crow、ブロックセル、室内配置、Runtime fixture などを追加した。
- Unity Contract: `MapAuthoringChecks.Run` 成功。正式 Map の Import と Entity Definition、移動 fixture、Persistent Player / Crow を検証。
- ver2.0 回帰: `Version20Checks.Run` 成功。`GameVersion.Value` は `2.0`。
- Standalone: `publish-win-x64.ps1` の Release Publish 成功。最新版 `tools/HalkaWorldMapEditor/dist/win-x64/HALKA WORLD MAP EDITOR.exe` を起動・操作確認。EXE は 169,984 bytes。
- WebGL: `ProjectBuilder.BuildWeb` 成功。Development Build OFF、Splash / Logo OFF、`stripEngineCode=false` の既存設定を維持。Wasm **24,055,387 bytes**、data **4,852,518 bytes**。公開用 build は、ユーザーの未コミット配置変更を含まない Map v3 から生成した。
- ローカルブラウザ: `http://127.0.0.1:8765/halkaworld/` で起動、ver2.0、家、Crow、道、Player の移動を目視確認。家入退室と Crow との Interaction をこのブラウザ回帰で再操作したとは記録しない。ブラウザ console に出所未特定の `MutationObserver` TypeError が1件あったが、Unity 画面は起動・描画した。
- 実スマホ: 未確認。

ビルド時の証跡は `backup-v05-20261006-174557/` の `core-tests-v05.log`、`unity-v05-checks-cleanmap.log`、`version20-checks-cleanmap.log`、`webgl-build-v05-cleanmap.log`。既存の `https://halkaclub.com/halkaworld/` 用ファイルをローカルで更新した。GitHub への push と公開 URL のライブ反映は行っていない。

## 6. Git と次の候補

実装コミット: `93fd61249fff1bda0510465535ac6b8fe0326dfa` (`Add Map Editor v0.5 entity spawn authoring`)。このレポートは別コミットで追加する。実装コミット後の作業ツリーには、保護したユーザー編集に由来する `first_field.hwmap.json`、生成済み `first_field.asset`、`FirstDay.unity` の差分と、未追跡のバックアップが残る。これは意図的であり、ユーザーの配置変更を失っていない。

v0.6 の候補は Area Transition Editor。今回 NPC 本体、Checkpoint、複数 Player Spawn、Runtime Live Edit は追加していない。
