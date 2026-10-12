# HALKA WORLD ver2.9 実装レポート

公開日: 2026-10-12（日本時間）
対象: 本編 ver2.8 → ver2.9 / MAP EDITOR v0.6 / Map JSON v4

## 1. 開始時Git状態

開始時のブランチは `codex/ver2.8-indoor-auto`、HEAD と `origin/main` はともに `0d6e71178a0cc12712821075979ab4f25e9bc50d`。既存の `GrassDecoration.prefab` の改行差と未追跡の `backup-v05-20261006-174557/` を確認し、今回の変更には含めていない。

## 2. Backup

repo外の `C:\Users\owner\Documents\ChatGPT\HALKA-v29-backup-20261012` にGit bundle、開始時status、正式Map、Scene、CrowとAUTOの主要Script、Crow/Tree素材、HP、Build一覧、検証ログ、ブラウザ画像と動画を保存した。既存バックアップは削除していない。

## 3. 既存Crow構造

`CrowWander2D` は `GridStepMotion` による約0.27秒の1セル移動、2～4秒の待機、4方向Idle/Walk、Spawn相対の巡回範囲を持つ。`DynamicGridOccupancy2D` がPlayerとの重なりを防ぎ、`CrowGrassOcclusion` が草の足元表示を担当する。地上Interactionと既存音声は変更していない。

## 4. State Machine設計

地上待機・歩行、離陸、飛行、木での休憩、再離陸、地上着陸を `CrowActivity` で区別した。地上の移動処理は既存のまま使用し、空中だけCrow専用の連続座標補間を加えた。

## 5. Flight Sprite制作方法

既存Crowの色、白い目、灰色の嘴、赤いスカーフを基準に、32×32pxの羽上げ/羽下げを右向き・正面向きで各2枚作成した。制作時の候補画像はrepo外に保存し、採用PNGは透明度を整理して既存のパレットに合わせた。左向きは右向きの水平反転を使用する。

## 6. Sprite品質比較

原寸と最近傍拡大の比較はrepo外の `crow-original-contact.png` と `crow-flight-contact.png`。採用した4枚はいずれも32×32px、PPU64、Point、MipMap OFF、非圧縮。目・嘴・スカーフが見え、既存のCrowと配色がつながることを確認した。

## 7. 飛行アニメーション

飛行中は方向に応じた2コマを0.16秒周期で交互表示する。木で休憩中は地上Walkを使わず、静止したCrowを表示する。地上に戻ったら既存のIdle/Walkへ切り替える。

## 8. 飛行頻度

地上で20～40秒後に飛行を試み、木がなければ地上行動を続けて6秒後に再判定する。木での休憩は4～8秒。値は `CrowWander2D` のSerializeFieldで調整できる。

## 9. Tree Candidate検索

`MapRuntimeLoader2D` が正式Mapの `tree_basic` Instanceから作ったRuntime Objectへ `TreePerchAnchor2D` を追加する。Crowは飛行開始時に現在Mapの有効なTreeを列挙し、前回の木を避けながら候補を選ぶ。Map座標はCrowコードに固定していない。

## 10. Tree Perch Anchor算出

Treeの `SpriteRenderer.bounds` の幅・上端から樹冠寄りの点を計算する。既定値は中心から幅の-0.12、上端から高さの0.28。Treeを移動するとAnchorも追従する。

## 11. 木の変更への対応

木0本なら離陸しない。1本ならその木を使用でき、複数なら別候補を選べる。Fixture相当の検証でTree Transform移動後のAnchor更新を確認した。

## 12. 離陸処理

地上位置から0.45秒かけて0.32 Unity Unit上昇する。離陸中は元の地上Cellを予約し、空中へ移った時点で予約を解除する。

## 13. 飛行補間

Treeまでの距離に応じて飛行時間を0.55～3.5秒に調整し、`Vector3.Lerp` で連続移動する。木への瞬間移動はしない。

## 14. Treeへの着地

Anchorに到達したら `Perched` へ移り、羽ばたきと位置移動を停止する。ローカルWebGLの連続画像ではカラスが樹冠に見える状態で静止した。

## 15. 木での休憩

4～8秒間、Anchor位置に固定して休憩する。Treeが無効になった場合は安全な地上復帰へ進む。

## 16. 再離陸

休憩後に0.35秒かけて上昇し、安全な地上Cellへの飛行へ移る。木から地上へワープしない。

## 17. 地上への着陸

Spawn相対の既存Wander Regionから通行可能なCellを選び、近づいた後0.4秒で降りる。着地直前にも占有を再確認し、塞がれた場合は別候補を探す。候補がなければ1秒間隔で待機・再判定する。

## 18. Dynamic Occupancy

GroundIdle/GroundWalk/Takeoffのみ地上予約を有効にし、Flying/Perched/LeavingPerch/LandingGroundでは無効にした。着地時にCellと `GridStepMotion` を更新して地上予約を再開する。Map切替時はRuntime Crowの無効化・破棄により予約は残らない。

## 19. Playerとの衝突防止

着陸候補は `world.CanEnter` と `occupancy.CanCrowEnter` を使い、Player、通行不可Object、House Door、Area Transitionセルを避ける。着地直前にも同じ条件を検査する。

## 20. Crow Interaction

地上待機中のみCrow Interactionが有効。飛行中と木での休憩中はクリックColliderも停止し、地上隣接操作が空中Crowへ届かない。Treeの既存Interactionは変更していない。

## 21. Crow鳴き声

地上での「カァ。」と `crow_voice.mp3` を既存のInteraction経路のまま維持した。空中で新しい音声は追加していない。

## 22. 屋外AUTO回帰

地上Crowは既存どおり障害物として扱い、飛行・木での休憩中は地上のAUTO経路を妨げない。Version28以下の継承チェックを通した。ブラウザではPlayer AUTOによる移動も観察した。

## 23. 屋内AUTO回帰

屋内AUTOのコードは変更していない。`Version29Checks.Run` が `Version28Checks.Run` を実行し、既存検証を通した。

## 24. Area Transition対応

屋外CrowはMap-local。House入室でCrowのGameObjectが無効化・破棄され、戻ると正式Spawnから地上状態で再生成される。飛行CoroutineやTweenは使用していない。

## 25. Map JSON差分

正式な `first_field.hwmap.json` と `halka_house.hwmap.json` は変更していない。前後SHA-256はそれぞれ `E1F0E579A70FD8113A573AB0B16DD2CC71CE9070746F1C70D5A824A564994961`、`CE451E4B5E7380B5E99F1BCB148E265FBACD9041F3512C8F7E91BF7F6174448E` で一致し、Git差分もない。

## 26. Player描画維持

Player PNG、`CharacterVisual`、`PlayerMover`、Camera、PPU、WebGL解像度・CSSには変更を加えていない。ローカルWebGLの画面でも既存のPlayer表示を確認した。

## 27. Font維持

遊びメモ書きBitmap Atlas、fallback、Font描画コードは変更していない。WebGL上のVersion/Menu文字が従来どおり表示された。

## 28. Version29Checks

`Version29Checks.Run` 成功。固定Seedを使い、状態遷移、飛行中の連続位置、樹冠Anchor、木0本/1本、Occupancy解除・復帰、地上Interaction復帰、2回目の離陸を確認した。最終ログはrepo外の `version29-final.log`。

## 29. MapAuthoringChecks

`MapAuthoringChecks.Run` 成功。ログはrepo外の `map-authoring.log`。

## 30. Core Tests

MAP EDITOR v0.6のStandalone Core Testsは181/181成功。ログはrepo外の `map-editor-core-tests.txt`。

## 31. PC WebGL実操作

正式 `first_field` をProduction WebGLでChrome起動し、地上歩行、離陸、羽ばたき、異なるTreeへの飛行と着地、休憩、再離陸、地上復帰を連続スクリーンショットで2サイクル確認した。1回目は `crow-browser-sequence-1.png`、2回目は `crow-fixed-45-contact.jpg` と `browser-frames/fixed_*.jpg` に記録。カメラ固定時も連続移動を確認した。

## 32. Mobile Preview

390×844、`touchControls=1` のアプリ内ブラウザで表示を確認。カラス、D-pad、A、Menu、Versionが画面内に収まる。D-pad入力でPlayerが1セル移動した。連続画像は `crow-mobile-contact.jpg` と `browser-frames/mobile_*.jpg`。物理スマホは未確認。タッチAによるCrow InteractionとLife Log画面は今回のモバイル実操作では未確認。

## 33. 飛行動画・比較画像

repo外に `crow-fixed-45s.mp4`、`crow-fixed-45-contact.jpg`、`crow-browser-sequence-1.png`、`crow-flight-contact.png` を保存した。MP4は約1秒間隔のブラウザスクリーンショットを時系列にした記録であり、連続60fps録画ではない。

## 34. Production Build

`ProjectBuilder.BuildWeb` 成功。WebGL productVersion 2.9、`BuildOptions.None`、Development Build OFF、Unity Splash/Logo OFF、`stripEngineCode=false`。ビルドログはrepo外の `unity-build.log`。

## 35. HP更新履歴

現在Versionをver2.9へ変更し、上Archiveの最上段に2026-10-12「カラスが飛んで木に止まるようになりました。」を追加した。既存履歴、下Archive、初期Closedは維持した。

## 36. Git Commit

公開時にCommit IDを追記する。

## 37. GitHub Deployment

公開時にPushとPages Deploymentの結果を追記する。

## 38. 公開WebGL確認

公開後にHP、Build、飛行表示を確認して結果を追記する。

## 39. 最終git status

CommitとPush後に、既存のユーザー変更を含めた最終状態を追記する。

## 40. 残る問題

物理スマホの実機確認は未実施。モバイルブラウザでAボタンによるCrow InteractionとLife Log画面の操作は未確認。飛行動画は1秒間隔のフレーム記録のため、羽ばたきの全フレームを動画単体から評価するには不足するが、Flight Spriteの2コマとWebGL上の切替は自動検証・連続画像で確認した。
