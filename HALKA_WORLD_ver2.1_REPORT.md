# HALKA WORLD ver2.1 実装レポート

作成日: 2026-10-07。テーマは「左上メニューとHUD整理」。HALKA WORLD MAP EDITOR は v0.6 のまま、Map JSON v4 / Schema / 正式配置は変更していない。

## 開始状態と保全

- 開始時 HEAD は `22b8a55`、`git status` は既存の未追跡 `backup-v05-20261006-174557/` のみ。`origin/main` にサイト編集2件が先行していたため、差分を確認して `78c19c9` へ fast-forward した。
- 変更前のrepo bundle、status / diff / HEAD、WebGL Build一覧、`FirstDay.unity`、HUD・AUTO・入力・Builder、正式MapのSHA-256を `C:\Users\owner\Documents\ChatGPT\HALKA-v21-backup-20261007-212205` に保存した。`git bundle verify` 成功。旧バックアップは削除もコミットもしていない。
- 正式 `first_field.hwmap.json` のSHA-256は `6D9D008E7A2EBCD71F15DAF4E26F6B06D77D5478632888FABF6C884B602DE531`、`halka_house.hwmap.json` は `D72AC15286BF5BDDFE714895F03F75F5C26834E48D24EA7B4578B132CCA78D6D`。Unity検証後も同一。Map Semantic Diffはなし。

## 既存HUDと実装

- 既存HUDはCanvasではなくIMGUI。`ProjectBuilder.PrepareScene` が正式SceneのSource of Truthで、`GameHud`がver表示・約2秒のMessage、`TouchDpad`と`TouchActionButton`がスマホ操作、`AutoModeController.OnGUI`が右上の旧AUTOボタンを描いていた。`GameInput`がKeyboard / Mouse / Touchをまとめ、`InteractionRouter`へ渡す。
- `GameVersion.Value` を `2.1` に変更。左上Safe Area内に52×48pxの3本線ボタンを置いた。3本線はUnity UIの矩形描画で、特殊文字や新画像を使わない。開くと200×104px以内の小さな「メニュー」パネルを表示する。AUTOのON/OFFのみを掲載し、将来項目を縦方向へ追加できる余白を持たせた。起動時は閉じる。
- 新しい`GameMenuController`は表示・ポインター遮断を担当し、AUTO状態は既存`AutoModeController.AutoEnabled`だけを参照する。旧Standalone AUTOボタン描画は撤去し、AUTOロジック、60秒、経路探索、Crow回避、家とAreaへの自動入室禁止は変更していない。AUTO設定の保存も追加していない。
- Menu Openは`GameInput`の移動・World Interactionを遮断し、AUTOを一時停止して進行中のAUTO Stepを安全に止める。設定ON/OFF自体は変えない。閉じると既存入力を復帰させ、無操作タイマーをリセットする。World全体の`timeScale`は変更せず、Crowは動ける。
- Button再押下、パネル外クリック・Tapで閉じる。Menu上の操作と閉じた直後のPointerを短く捕捉し、背後のObject / D-pad / Aへ同じ入力が通らないようにした。Touch開始から指を離すまでGameplayを遮断する。Messageが表示中ならMenu Openを拒否し、Messageは消さない。MenuはMap Rootの外に置くPersistent HUDで、CameraとWorld Transformを変更しない。
- フォントは既存の`k8x12L`を再利用。Safe Areaの左上Anchor相当から位置を決め、D-pad / Aの既存位置は変更していない。新Shortcut、UI音、アニメーションは追加していない。

## 検証結果

- `Version21Checks.Run` 成功。内部で`Version20Checks.Run`を実行し、ver2.0での家、室内、Crow、Grass / Dirt、World Object、Camera、AUTO・Map Runtimeの回帰を維持。ver2.1固有ではMenuの初期閉状態、参照、AUTO単一状態、Open / Close、入力遮断、一時停止、設定保持、Close時のIdle reset、Message競合、室内HUDを検証。ログ: バックアップ内 `version21-check.log`。
- `MapAuthoringChecks.Run` 成功。Map Editor v0.6のMap v4 / Surface / Object / Entity / Area Transition契約を維持。ログ: 同 `map-authoring-check.log`。
- `ProjectBuilder.BuildWeb` 成功。Development Build OFF、Unity Splash / Logo OFF、`stripEngineCode=false`。Wasm `b4901b15ff1677c50a4e95bd6ba35e65.wasm` は 24,096,080 bytes、data `f48cef6d82229f3298fe5243a172496e.data` は 4,855,701 bytes。`halkaworld/webgl/index.html` の productVersion とiframeのキャッシュIDは2.1へ更新された。ログ: 同 `webgl-build.log`。
- **PCローカルWebGL目視**: ver2.1、左上ボタン、Menu Open / Close、AUTO ON→OFF→ON、パネル外クリックでClose、Menu中RightでPlayerが動かないこと、Close後Rightで移動することを確認。AUTOをONにしたままMenuを60秒以上開いてもPlayerは停止し、Crowは移動した。Close後は60秒未満ではPlayerが停止し、約60秒後にAUTO歩行を再開した。AUTO歩行中にMenuを開くとPlayerは停止し、設定ONは保持された。ゲーム表示に家、草・土・木・花・石、Crowが存在する。家の入口へ通常歩行して室内へ移動し、室内でも同じMenuとAUTO設定を確認。下へ通常歩行して屋外へ戻った。House遷移はこの1往復を目視確認。World ObjectのMessage文言とCrow Interactionは今回のブラウザ操作では未確認で、Version20Checksの回帰検証に留まる。
- **Touch Control Preview**: `touchControls=1`でD-pad / AとMenuを表示。Menu上でAUTOを変更できた。Menu Open中のD-pad / AクリックはMenuを閉じ、Playerの移動やMessage発生を起こさなかった。次のD-pad操作でPlayerが動いた。これはMouseによるスマホUIプレビューであり、物理スマホでのTap確認ではない。
- **Unity実操作**: Unity Editor Play Modeでの目視操作は未実施。UnityのEditor検証とローカルWebGL実操作を実施した。
- **実スマホ**: 未確認。

## HP、公開、今後

- HP更新履歴の先頭へ `ver2.1 / 2026-10-07 / 左上メニューを追加し、AUTO設定をメニュー内へ移動。` を追加。既存ver2.0以下とdetails初期閉状態を維持した。
- 公開コミット、GitHub push、Pages deployment、最終`git status`は公開後に追記する。
- ver2.2候補はMenu内の「せいかつきろく」。歩数・プレイ時間・生活記録の本体は今回未実装。Settings、Save拡張、新Map、MAP EDITOR v0.7も今回含めない。
