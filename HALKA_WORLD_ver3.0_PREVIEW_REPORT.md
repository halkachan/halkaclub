# HALKA WORLD ver3.0 Preview 開発レポート

作業日: 2026-10-12～13（日本時間）
対象: 本編 ver3.0 Preview / 公開版 ver2.9維持 / MAP EDITOR v0.6 / Map JSON v4

## 1. 開始時Git状態

開始時のHEADと`origin/main`はともに`13462feb0dc8078e988bfbc96d3c4c4c9048eb17`。既存の`GrassDecoration.prefab`改行差と未追跡`backup-v05-20261006-174557/`を確認し、変更・削除・Commit対象にしていない。

## 2. Backup

repo外の`C:\Users\owner\Documents\ChatGPT\HALKA_WORLD_ver3.0_PREVIEW_BACKUP_20261012`へGit bundle、開始時status/diff、正式Map、Scene、Crow/Player Script、Sprite、Font、WebGL Template、公開WebGL、HP HTML、設定と検証画像・ログを保存した。既存バックアップは残した。

## 3. ver2.9 Crow構造

`CrowWander2D`は地上Grid Step、自然飛行、木のPerch、地上復帰を管理する。`ExamineInteractable`が既存の「カァ。」と`crow_voice.mp3`を担当し、`DynamicGridOccupancy2D`が地上占有を管理する。

## 4. Crow Interaction変更内容

任意の調査後反応を受け取る`IExamineReaction`を追加した。`ExamineInteractable`で既存Messageと音声要求を処理した後、対応するCrowへ`OnExamined()`を通知する。操作ボタンやPlayer入力は変更していない。

## 5. Treeへの飛行開始処理

地上Idleで有効な木がある場合、Interactionから0.45秒後に既存`TryStartTakeoff`へ入る。Tree候補はRuntimeから取得し、最も近い距離から2セル幅以内の候補を選ぶ。木のMap座標をCrowコードへ固定していない。

## 6. 自然飛行との共存

20～40秒程度で自然離陸を試みる既存タイマーは維持した。Interactionの予約だけを追加し、両方が同じ離陸・飛行・Perch・地上復帰処理を使う。

## 7. 飛行中Interaction制御

離陸予約後と空中状態ではCrowへの再操作を受け付けない。0.9秒の反応間隔を設け、連打による重複音声・重複飛行を抑える。木0本なら既存のMessageと声だけを処理し、地上に残る実装。

## 8. Crow Occupancy検証

離陸から地上復帰までの占有状態はver2.9の処理を再使用する。`Version30Checks`でInteractionからTakeoff→Flying→Perchedを通し、`Version29Checks`の着陸・占有・Map切替回帰も継承した。

## 9. 時間システム設計

`WorldTimeController`がHALKA WORLD全体の単一時計を保持する。`WorldTimeCycle`が純粋計算を担当し、UTCの経過ミリ秒から朝/夜を求める。歩数、Frame Rate、AUTOとは独立。

## 10. 初回起動時刻

保存値がない初回起動は`DateTimeOffset.UtcNow`を起点として保存し、朝から始める。現実の午前・午後とは連動しない。

## 11. 10分周期の計算

起点から600,000ms未満は朝、次の600,000msは夜、以後1,200,000ms周期で交互に切り替える。599.9/600/1199.9/1200/1800秒の境界を注入時刻で検査した。

## 12. 時間Save方式

独立したPlayerPrefsキー`HALKA_WORLD_time_cycle_anchor_v1`へUTC起点を文字列で1回保存する。毎Frameの保存は行わない。既存Life LogとAUTOキーは変更していない。

## 13. Reload / Offline経過

再読込時は保存起点から現在UTCまでの差でPhaseを再計算する。ブラウザで夜の再読込後も夜が維持され、数時間後の再起動では周期に応じて朝だった。破損値・時計の巻き戻しは安全に朝へ戻す自動検証済み。

## 14. 朝の描画仕様

朝のOverlay alphaは0。ver2.9のWorld Sprite、Player Sprite、Crow Spriteの色・動きは変更していない。

## 15. 夜の描画仕様

Camera子の`SpriteRenderer`をWorldの最前面に置き、IMGUIより前に描画する。ColliderとUI Raycastを持たず、Camera範囲に合わせて拡大する。Player/World/Camera/Canvas/Fontの既存構造は変更していない。

## 16. 夜Overlayの色と強さ

色は紺青`(0.08, 0.12, 0.38)`。屋外alphaは0.33、屋内は0.16。屋内の家具とPlayerは夜も視認できた。

## 17. Fade仕様

Phase判定時刻は実時間で決まり、表示alphaを2.5秒かけて補間する。2026-10-13 19:31頃（日本時間）の夜→朝では中間色のフレームをブラウザで取得した。全2.5秒にわたる連続的な変化は未記録。詳細は31項。

## 18. 屋内と屋外の明るさ

Map IDが`halka_house`のとき薄い夜色、それ以外の屋外は濃い夜色を使う。正式MapのHouseへ実際に入室し、屋内で朝→夜を確認した。夜のまま屋外へ退出してもPhaseは維持された。

同じ画面領域のRGB平均は屋外の草地で朝`(181,197,156)`→夜`(129,143,136)`、屋内床で朝`(211,139,64)`→夜`(180,121,70)`だった。屋内の変化を屋外より弱くしたことも画像から確認できる。

## 19. UIへの影響

IMGUIのMenu、AUTO、Life Log、Message、D-pad、A、VersionはOverlayの後に描画する。390×844の夜画面でMenu、Life Logと操作ボタンが読み取れた。House内のVersion文字は従来の濃い文字色が暗い背景上で低コントラストに見えるため、承認前の確認事項として残す。

## 20. AUTOとの整合

AUTO Scriptは変更していない。ブラウザでONのまま無操作後に屋外Playerが歩くことを確認した。屋内外AUTOの既存回帰は`Version28Checks`以降で継承する。

## 21. Player描画維持

Player PNG、`CharacterVisual`、`PlayerMover`、歩行補間、PPU、Camera設定、Canvas倍率は変更していない。PCとモバイルPreviewで通常Playerを表示した。

## 22. Font維持

遊びメモ書きBitmap Atlas、Point Filter、Fallback、既存IMGUI描画を変更していない。夜のモバイルMenuとLife Logを目視した。

## 23. 正式Map差分

正式JSONは無変更。前後SHA-256は`first_field`: `E1F0E579A70FD8113A573AB0B16DD2CC71CE9070746F1C70D5A824A564994961`、`halka_house`: `CE451E4B5E7380B5E99F1BCB148E265FBACD9041F3512C8F7E91BF7F6174448E`で一致。Semantic Diffなし。

## 24. Version30Checks

Unity 6000.3.10f1のBatchmodeで成功。時刻境界、保存/破損値、Scene配線、屋内外alpha、正式Tree、実際の`ExamineInteractable.Interact()`からMessage表示・音声要求・離陸予約、連打防止、Takeoff→Flying→Perchedを検査した。ログはBackup内`version30-unity-final.log`。

## 25. Version29以下のRegression

`Version30Checks.Run`内から`Version29Checks.Run`以下を実行して成功。旧Version検査は3.0 Previewを後継版として許容し、既存のGameplay条件を維持した。

## 26. MapAuthoringChecks

`MapAuthoringChecks.Run`成功。ログはBackup内`map-authoring-unity.log`。MAP EDITOR v0.6とMap JSON v4は変更していない。

## 27. MAP EDITOR Core Tests

`dotnet run --project tools/HalkaWorldMapEditor/HalkaWorldMapEditor.Tests -c Release`で181件すべて成功。ログはBackup内`map-editor-core-tests.txt`。

## 28. PC WebGL検証

repo外のProduction設定WebGLをChromeの1280×720で起動し、朝と夜、Player歩行、家への入退室、Menu、AUTO、Crow自然飛行、夜の視認性を確認した。Crowに話しかける瞬間のブラウザ記録は、巡回と自然飛行で接近が合わず未取得。Unityの実Interaction経路テストは成功しているが、ブラウザ目視成功とは区別する。

## 29. 実時間600秒検証

初回ブラウザ起動後、UTC 06:51台から07:01台の朝→夜、および11:58台の朝表示から12:01台の屋内朝→夜を実時間で確認。UTC 12:11台には夜→朝もブラウザで確認した。テスト用高速周期はBuildに入れていない。

## 30. Mobile Preview

Chrome viewport 390×844、`touchControls=1`で朝・夜の表示、夜のD-pad移動、Menu、Life Log、Aボタンの表示を確認。朝画面はBackup内`mobile-morning.jpg`。CrowへのA操作成功は未確認。物理スマホは未確認。

## 31. 朝夜比較画像

Backup内`outdoor-morning-night-comparison.png`、`house-morning-night-comparison.png`、`mobile-preview-stable.png`、`mobile-night-menu.png`、`mobile-night-lifelog.png`、`mobile-morning.jpg`を保存した。2026-10-13 19:31頃の夜→朝の同一の草地領域RGB平均は、夜`(118,133,128)`、切替中`(136,154,134)`、朝`(165,183,143)`。`fade-night-before.jpg`、`fade2-00.jpg`、`fade2-01.jpg`に対応し、`fade-night-to-morning-contact.png`へ並べた。Fade中間値は確認できたが、撮影開始が境界後だったため、2.5秒全体の連続動画は未取得。ブラウザ撮影素材はJPEGとして返るため、PNG比較画像もその素材から作成した。

## 32. Crow動画

自然飛行の連続PNG`crow-natural-00.png`～`19.png`、10秒の`crow-natural-observation.mp4`、比較Contact SheetをBackupへ保存した。MP4は1280×720、20フレームでデコード可能と確認。Interactionを起点とする飛行動画は未取得であり、実画面の確認課題として残す。

## 33. Preview Buildの保存先

`C:\Users\owner\Documents\ChatGPT\HALKA_WORLD_ver3.0_PREVIEW`。Unity WebGLの非Development Build、Unity Splash/Logo OFF、`stripEngineCode=false`。BuildログはBackup内`preview-build-unity.log`。Buildはrepo外にあり、公開WebGLへコピーしていない。

## 34. Previewの起動方法

現在のローカルURLは`http://127.0.0.1:8877/`。停止時はBuildフォルダで`python -m http.server 8877 --bind 127.0.0.1`を実行する。`README_PREVIEW.txt`にも記載した。

## 35. 作業ブランチ

`codex/ver3.0-preview`。mainへのMergeはしていない。

## 36. Git Commit状況

実装は`00b3855 Add ver3.0 preview clock and crow interaction flight`としてPreviewブランチにCommit済み。このReportも別Commitに収める。開始時からあった`GrassDecoration.prefab`と未追跡BackupはCommitに含めない。

## 37. 公開環境が未変更である証拠

`halkaworld/index.html`はBackupとSHA-256一致。`halkaworld/webgl`の公開5ファイルもBackupと全件一致。該当パスにGit差分なし。公開版のver2.9を維持し、mainへのMerge、origin/mainへのPush、GitHub Pages更新はしていない。

## 38. 最終git status

Preview作業のCommit後に残る差分は、開始時から存在した` M unity-project/Assets/Content/World/GrassDecoration.prefab`と`?? backup-v05-20261006-174557/`。本版のファイルはCommit済み。正式Mapと公開HP/WebGLに差分はない。

## 39. 未確認事項

実ブラウザでのCrow Interaction起点の離陸・音声聴感、物理スマホ、公開環境でのver3.0（公開禁止につき対象外）。ブラウザでのCrow操作を確認するまでは、今回のプレビューを完全検証済みとは扱わない。

## 40. ユーザーに確認してほしい点

1. カラスに話しかけた後の離陸までの間と木への飛び方。
2. 朝の景色が従来どおりか。
3. 夜の青みと屋内の明るさ、House内のVersion文字の読みやすさ。
4. 10分ごとの2.5秒Fadeが自然か。

ユーザーの明示的な承認があるまで、正式公開しない。
