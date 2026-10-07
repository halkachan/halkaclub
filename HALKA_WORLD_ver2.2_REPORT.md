# HALKA WORLD ver2.2 実装レポート

作成日: 2026-10-07。テーマは「せいかつきろく」。MAP EDITORはv0.6、Map JSON v4と正式Map配置は維持。

## 1. 開始状態・Backup・Version

- 作業開始HEADは `bb244f9`。未追跡の既存 `backup-v05-20261006-174557/` と、`first_field.hwmap.json` の改行差によるGit変更扱いがあった。MapのGit内容差分はなく、この変更は今回ステージしない。
- `origin/main`に別件のサイト編集 `2cc6501` が先行。差分を確認しfast-forwardした。
- repo外 `C:\Users\owner\Documents\ChatGPT\HALKA-v22-backup-20261007-2208` にbundle、開始時status / diff / HEAD、Scene、重要Script、Map、HP HTML、WebGL Build一覧を保存。`git bundle verify`成功。
- 正式Map開始時SHA-256: `first_field.hwmap.json` = `20A8B5AA77E5090644C701038BB51F5B40F812B90E48E91DB490A711A96A0938`、`halka_house.hwmap.json` = `D72AC15286BF5BDDFE714895F03F75F5C26834E48D24EA7B4578B132CCA78D6D`。作業後も同一を確認する。
- `GameVersion.Value`、WebGL `productVersion`、HPの現在Version表示とiframe build指定をver2.2へ更新した。

## 2. Life Logと保存

- 既存コードには本編の汎用SaveやPlayerPrefs利用はなかった。新しい`LifeLogController`と`LifeLogData`に記録と保存を分離した。`GameMenuController`は表示のみを担う。
- `totalSteps`と`totalPlaySeconds`は64-bit整数。WebGL PlayerPrefsの単一キー`HALKA_WORLD_life_log_v1`へ、`v1|steps|seconds`というversion付き文字列で保存する。float変換を行わない。未知versionや初回起動は0。旧ver2.1以前の値は推測しない。
- 歩数の唯一の加算地点は`PlayerMover.StepCompleted`。手動とAUTOが同じイベントを通り、成功した1セル移動の完了時だけ+1。開始・キャンセル・Blocked・向き変更・Interaction・Spawn・Teleport・Crow移動は加算しない。House入口・出口の通常Stepは各1回で、Map切替のTeleportは加算しない。edge_exitはStepCompletedを出さない。
- 時間はGameplay Ready後の`realtimeSinceStartupAsDouble`差分を使用し、`Time.timeScale`に依存しない。Menu中とAUTO中も加算。Unity focus/pause lifecycleとWebGLの`document.visibilityState`で非Foregroundを除外し、復帰時に時刻基準を再設定する。
- Memoryで増分を持ち、約30秒ごと、focus喪失、pause、終了時に保存。毎Frame/毎Stepのflushはしない。保存キーを将来のBuildでも維持することで記録を引き継ぐ。Reset UIは設けない。
- 表示は歩数3桁区切り、時間は累積時間`H:MM:SS`（100時間を超えてもリセットしない）。

## 3. Menuと入力

- ver2.1のIMGUI左上メニューを拡張。通常MenuにAUTOと「せいかつきろく」を表示。Life Log画面では「あるいたかず」「プレイじかん」「もどる」を表示する。既存k8x12L Fontを使用し、新画像や音は追加しない。
- Life Logから「もどる」は通常Menuへ戻り、入力遮断とAUTO一時停止は継続。Menu Buttonで全体を閉じるとGameplay入力復帰とIdle Timer reset。Mouse/Touchの同一入力を背後のWorldやD-pad/Aに通さない。
- 通常Menuは小パネル、Life Logは左上の少し大きいパネル。World全体のtimeScaleとCameraは変更しない。

## 4. 更新履歴Archive

- 変更前は単一の閉じた`details`内に12件のver2.1～ver1.0履歴が新しい順で並んでいた。
- 変更後は見出し「更新履歴」の下に、初期閉状態の`details`を2つ配置。上が「ver2.1 ～ ver3.0」（ver2.2、ver2.1）、下が「ver0.1 ～ ver2.0」（既存ver2.0以下）。Versionごとの二重detailsやJavaScriptは追加しない。
- 既存12件のVersion・日付・文章は維持。新規ver2.2（2026-10-07「せいかつきろくを追加。」）を加え、合計13件。Archive名に含まれる未公開VersionのEntryは作らない。
- `summary`には既存のKeyboard操作とfocus-visible輪郭を維持し、スマホ幅で折り返せる簡潔なCSSを適用。

## 5. 検証

- `Version22Checks.Run` 成功（`HALKA-v22-backup-20261007-2208/version22-check-3.log`）。`Version21Checks.Run`と`Version20Checks.Run`を継承。初期0、64-bit精度、保存形式、Menu/Back/入力遮断、完了Stepだけの加算、Blocked/Teleportの非加算、Foreground時間、保存復元を確認した。AUTOも`PlayerMover.TryStep`と同じ完了イベントを使用する。
- `MapAuthoringChecks.Run` 成功（同Backupの`map-authoring-check.log`）。MAP EDITOR v0.6とMap v4の契約を維持。
- 正式Mapの最終SHA-256は開始時と同一。`first_field` = `20A8B5AA77E5090644C701038BB51F5B40F812B90E48E91DB490A711A96A0938`、`halka_house` = `D72AC15286BF5BDDFE714895F03F75F5C26834E48D24EA7B4578B132CCA78D6D`。配置の変更なし。
- ローカルProduction WebGLでver2.2、Menu、AUTO、Life Log、もどるを実操作。初期歩数0から右へ成功Step後1、石へのBlocked入力後も1。Reload後に歩数1と累積時間を保持した。60秒無操作後のAUTO歩行で歩数が増加し、Menuを開くとPlayerの移動が止まった。
- 家へ通常Stepで入室し、室内でもLife Logを開けた。入室Stepと退出Stepは各1回だけ加算され、Map切替の座標移動は追加計上されなかった。屋外で家とカラスの表示・移動を目視した。
- 幅390px・高さ844pxのMobile PreviewでLife Logが画面内に収まり、Menu中のD-pad入力は歩行へ通らず、閉じた後は復帰した。物理スマホ、実タッチAボタンは未確認。
- HPはPCと390px幅で両Archiveが初期Closedであること、開閉、ver2.2と旧12件の表示、横はみ出しがないことを確認。既存12件を保持し、合計13件。
- Unity Editor GUI Play Modeは未確認。WebGLを別タブへ切り替えても検証ブラウザが`document.visibilityState=visible`を返したため、実ブラウザのBackground停止は未確認。非Foregroundの加算停止と復帰時の差分除外は`Version22Checks`で確認した。

## 6. Production Build・公開

- `ProjectBuilder.BuildWeb` 成功（同Backupの`webgl-build.log`）。Development Build OFF、Unity Splash/Logo OFF、`stripEngineCode=false`。Wasm 24,109,100 bytes、data 4,860,193 bytes。
- 公開実装commit: 公開後に記入。
- GitHub push / Pages deployment / 公開HTTP: 公開後に記入。
- 公開URL: `https://halkaclub.com/halkaworld/`。
- 最終git status: 公開後に記入。
- ver2.3候補は家の中の家具（ベッド、クッション、机）。今回は未実装。
