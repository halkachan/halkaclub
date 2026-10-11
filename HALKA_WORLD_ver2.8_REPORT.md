# HALKA WORLD ver2.8 実装レポート

公開日: 2026-10-12

## 1. 開始時Git状態とBackup

開始HEADと`origin/main`は`e3215d8ff516ce10e262e025e73f7fd07f65a30a`。開始時からの`GrassDecoration.prefab`改行差と未追跡`backup-v05-20261006-174557/`は保持し、今回のコミットから除外する。repo外の`C:\Users\owner\Documents\ChatGPT\HALKA-v28-backup-20261011-2`にGit bundle、開始時status/diff、正式Map、Scene、AUTO・移動・遷移関連Script、Catalog、ProjectBuilder、HP、旧WebGL一覧を保存した。検証とBuildのログも同所に保存した。

## 2. 既存AUTOと変更箇所

従来の`AutoModeController`は、MenuのAUTO ON/OFFと60秒の無操作時間を共有し、屋外で`GridPathfinder2D`と`PlayerMover.TryStep`によって散歩していた。House内では無条件に停止していた。今回、`MapWorldController2D.ActiveMapId`による屋内・屋外判定を追加し、屋外の目的地候補とCrow回避は維持した。屋内候補の収集だけを`IndoorAutoRoutePlanner2D`へ分け、ProjectBuilderのScene生成時に既存Map ControllerとGameHudを接続した。新しい操作ボタン、Sprite、移動処理はない。

## 3. 屋内開始条件とIdle Timer

AUTO ON、Foreground、Menu・Message・着席によるロックなし、Playerが移動中でない状態で、最後のユーザー操作から60秒で開始する。House入退室でMap IDが変わるとIdle Timer、経路、直前の屋内目的地をリセットする。Menu閉鎖、手動入力、Focus Lost/Pauseからの復帰でも60秒を取り直す。WebGLでは既存のページ可視判定も使用する。AUTO OFFは両Map共通。

## 4. 屋内目的地と経路

正式`halka_house`のBounds、Blocked Footprint、現在の到達可能領域をBFSで取得する。出口Markerは候補と経路から除外する。3セル以上の散歩を優先し、直前の目的地の連続選択を避け、狭い部屋では条件を緩める。目的地ごとに既存Pathfinderを使用し、通常のGrid Stepで歩く。到着後は1〜4秒程度待つ。候補や経路がなければ2秒待って再評価する。家具の座標はハードコードしていない。

## 5. 家具、Area Transition、Interaction

正式MapとDefinitionからベッド・机は通行不可、クッションは通行可能として扱う。Map外や`interior_exit`へAUTO入力を送らず、自動退室しない。屋外では既存どおりHouse Doorを除外し、自動入室しない。AUTOから`InteractionRouter`を呼ばないため家具を調べたり座ったりしない。手動退室は既存操作のまま可能。

## 6. 手動入力、Menu、Message、Life Log

WASD/矢印、D-pad、A、既存クリックによる`GameInput.UserActed`でAUTO経路を停止してIdle Timerをリセットする。Menu中は設定値を保持して一時停止する。Message中は開始しない。屋内AUTOの歩数は既存`PlayerMover.StepCompleted`からLife Logへ反映され、専用カウンターや保存形式変更はない。プレイ時間のコードも変更していない。

## 7. 屋外回帰と正式Map・描画保護

屋外の散歩候補、Crow回避、通行不可Object回避、House/Area Transition除外を維持した。両正式MapはGit差分なしで、SHA-256は`first_field`: `E1F0E579A70FD8113A573AB0B16DD2CC71CE9070746F1C70D5A824A564994961`、`halka_house`: `CE451E4B5E7380B5E99F1BCB148E265FBACD9041F3512C8F7E91BF7F6174448E`とBackup時に一致。Map v4、MAP EDITOR v0.6のまま。Player Sprite、CharacterVisual、PlayerMoverの補間、Camera、WebGLの576px表示CSS、遊びメモ書きBitmap Fontは変更していない。

## 8. 自動検証

- `Version28Checks.Run`: 成功。`Version27Checks.Run`以下を継承し、59.9/60秒、ON/OFF、手動入力、Menu、Message、Background、正式Houseの家具と出口、複数目的地、通常Step、手動退室、屋外House禁止を検証。家具位置変更は正式Mapの複製Fixtureで検証した。ログ: Backup内`version28-checks.log`。
- `MapAuthoringChecks.Run`: 成功。ログ: Backup内`map-authoring-checks.log`。
- MAP EDITOR Standalone Core Tests: 181/181成功。ログ: Backup内`map-editor-core-tests.txt`。

## 9. ローカルProduction WebGL実操作

正式MapのローカルProduction WebGLで`ver2.8`、手動入室、AUTO ON、実時間60秒後の屋内歩行、複数位置への移動、家からの自動退室なし、Menu中の停止、Life Logの歩数増加を確認した。表示値は屋内散歩前1,272歩、後1,314歩で、AUTO専用加算ではなく通常Step経路で増加している。手動方向入力でAUTOを止め、手動で退室できた。AUTO OFF後に62秒以上静止し、ONへ戻した後は再び60秒以上で屋内散歩した。屋外へ出て60秒以上経過後もAUTOで位置が変わり、従来散歩の回帰を確認した。移動中のPlayer Spriteと遊びメモ書きに明らかな表示回帰は見られなかった。

## 10. Mobile Preview

390×844、`touchControls=1`のローカルWebGLでD-padから手動入室し、60秒後に屋内AUTOで移動することを確認。Aタップ後4秒間は同じ位置で停止した。Menu、AUTO表示、せいかつきろく、D-pad、Aボタンは画面に収まり読める。物理スマホは未確認。

## 11. Production BuildとHP

Unity 6000.3.10f1の`ProjectBuilder.BuildWeb`でProduction WebGL Build成功。`BuildOptions.None`、Development Build OFF、Unity Splash/Logo OFF、`stripEngineCode=false`を維持した。新しいWebGL Buildのハッシュ参照をHPに反映。GameVersion、Unity productVersion、HP現在Versionは2.8。上Archive最上段へ`ver2.8`、`2026-10-12`、「家の中でもAUTOで歩くようになりました。」を追加。既存履歴と下Archiveを維持し、両Archiveの初期Closedも維持した。ログ: Backup内`production-build.log`。

## 12. Git、Deployment、公開WebGL

公開作業の結果をここへ追記する。

## 13. 未確認事項

物理スマホでの操作は未確認。ローカルWebGLで全Objectを個別操作する網羅試験は行っていないが、Version27以下の回帰検証に継承されている。公開WebGLの実時間60秒検証は公開後に可能なら実施する。
