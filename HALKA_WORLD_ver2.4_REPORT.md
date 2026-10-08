# HALKA WORLD ver2.4 作業レポート

開始日: 2026-10-08。今回のテーマは「ベンチに座る」。本編の公開版は作業開始時ver2.3、MAP EDITORはv0.6、Map JSONはv4。

## 開始状態と保全

- 開始HEADおよび`origin/main`: `fa85fcd80067a7eff70767ef9ec5382fb4f62705`。作業ブランチ: `codex/ver2.4-bench`。
- 開始時から`first_field.hwmap.json`に改行差によるGit上の`M`、`backup-v05-20261006-174557/`が未追跡。両者は変更・削除・Commitしない。
- repo外`C:\Users\owner\Documents\ChatGPT\HALKA-v24-backup-20261008`へ検証済みGit bundle、status/diff、正式Map SHA-256、Scene、bench Sprite、HP HTML、WebGL Build一覧を保全。
- 正式Map開始時SHA-256: `first_field`=`20A8B5AA77E5090644C701038BB51F5B40F812B90E48E91DB490A711A96A0938`、`halka_house`=`D72AC15286BF5BDDFE714895F03F75F5C26834E48D24EA7B4578B132CCA78D6D`。

## 既存ベンチとPlayerの監査

- `bench_basic`: 64×32px、visual/blocked footprint=2×1、root anchor=`bottom-left`、`seat_left`のPlayer offset=(0,-1)、`seat_right`=(1,-1)、どちらもFacing Up、Action Type=`sit`、Pose Key=`bench_sit`。CatalogとUnity Definitionは一致。
- 既存Playerの正面Spriteは64×64px、透明背景、22色。明るい髪、青い目、黒い頭飾り、黒と青の衣装が識別点。既存benchは暗い木製の64×32px、12色。
- `WorldObjectActionInteractable`は現状`sit`をno-opとする。`InteractionRouter.TryInteractAhead`にはroot cell一致判定があり、2セル幅ベンチの右座席はそのままではA操作できない。正式実装時にAction Pointで到達判定する必要がある。
- Playerの通常Spriteは`CharacterVisual`が方向と歩行状態で選択。`PlayerMover`は論理セルとStepを管理し、`AutoModeController`は60秒無操作後に歩行する。座り中は論理セルを維持し、見た目だけ座面へ移動する設計とする。

## GPT ImageによるSprite候補

- 既存`front_idle/00.png`と`bench.png`をNearest Neighborで8倍にした画像を参照として、GPT Image内蔵生成機能で透明背景の正面座り姿を作成。ベンチ自体は生成Spriteへ含めていない。
- A: 足をそろえた対称座り。最初の候補は立ち姿に近かったため、膝と腰を強調して1回再生成。B: 両足を少し前へ出す。C: 足の位置を少しずらした自然な姿。
- 元画像は`unity-project/SourceGeneratedArt/v24/candidates/candidate_[A-C]_source.png`。Aの初回案は`candidate_A_initial_source.png`として保存。
- 後処理は透明領域Crop、Nearest Neighbor縮小、24色へのパレット整理、Alpha二値化、64×64キャンバスへの中央配置のみ。Pillowで人物を描画していない。候補A/B/Cはすべて64×64 RGBA、Alpha値は0/255のみ、色数は各25/24/25色。
- 拡大比較:`unity-project/SourceGeneratedArt/v24/candidate_comparison.png`。既存benchの左右座席へ重ねたプレビュー:`unity-project/SourceGeneratedArt/v24/bench_comparison.png`。個別の64px Spriteと拡大・左右プレビューも同フォルダーに保存。
- ユーザーが2026-10-08に提示した採用画像は候補C原本とSHA-256で完全一致。原本=`B821C8B8881B7FBB3BB35657FDC23F5135331FB0BA86569730CEFB0508B74BD2`。採用Sprite=`Assets/Content/Character/bench_sit.png`、SHA-256=`2CDBE00892815A3B3B303D14EAB2A73887484BE84B0D4A74EB76CC76FAA2C5E5`。64×64 RGBA、Alphaは0/255、PPU64、中央Pivot、Point、MipMap OFF、Uncompressed。ベンチ画像はSpriteに含めない。

## 座る・立つ動作

- `PlayerSeatController`が`bench_sit` Pose Keyだけを扱う。クッションの`cushion_sit`は引き続き未実装。ベンチIDや正式Map座標の分岐はない。
- 左右Action Pointの通行可能なLogical Cellを保持し、座り中は`PlayerMover.MovementLocked`を有効化。ベンチ方向の隣セル中心にPlayer Artworkを移し、座面中心から10/64 world unit上へ配置。左右の差はAction Pointの位置から算出。立つと元のArtwork localPositionへ戻る。ベンチSprite sorting order=2、Player=10で座り姿が前面に見える。
- `CharacterVisual`は座り中だけ承認Spriteを表示し、立ったら通常のFacing別Idleへ戻す。`PlayerGrassOcclusion`は座り中に足元Maskを解除。歩行Animationと足音はStepを開始しないため出ない。
- `WorldObjectActionInteractable`のAction Point判定を`InteractionRouter`で使い、2セル幅の右座席もA操作できる。同じベンチへの再クリック、またはスマホAの次の独立した押下で立つ。座り中は別ObjectへのクリックInteractionを無効化。GameInputのAは`JustPressed`なので押し続けても二重反応しない。
- AUTOは`MovementLocked`で歩行せず、ON/OFF設定値は変更しない。座る・立つ入力は既存`UserActed`からIdle Timerをリセットする。Menuは座り状態を維持して開閉でき、Life Logのプレイ時間は継続する。歩数は`StepCompleted`だけで増えるため座る・立つ操作では増えない。
- Map Unload時は`MapRuntimeLoader2D.Unload`が明示的に座り状態を解除し、ベンチの`OnDisable`でも解除する。

## 検証

- `Version24Checks.Run`: 成功。`Version23Checks.Run`を通じver2.0〜2.3の回帰も成功。承認SpriteのImport、ベンチ定義と左右Action Point、左右の座る・立つ状態、Logical Cell保持、Sprite切替と復元、移動ロック、Map Unload解除、クッションPose未実装を確認。ログ: repo外Backupの`version24-check-final-2.log`。
- `MapAuthoringChecks.Run`: 成功。MAP EDITOR v0.6の契約を維持。Standalone Core Tests: `dotnet run --configuration Release`で181/181成功。
- 正式Mapのコピーから使い捨てのベンチ検証用Mapを作り、ベンチRoot=(2,0)、Player開始=(2,-2)のWebGL Buildをrepo外へ出力。検証後、一時Map AssetとSceneは削除された。正式Mapにベンチは追加していない。
- 検証用WebGLをブラウザで操作。左座席・右座席で座りSpriteが座面に自然に重なり、再クリックで立った。右座席では座り中の十字キーで移動せず、Menuを開閉しても座り状態を保持。Aボタンで立ち上がった。通常の2歩、座り/立ち、blocked方向操作後のLife Log歩数は2だった。カラスは背景で継続して動いた。
- `touchControls=1`の検証用WebGLでD-padとAボタンをマウス模擬操作。物理スマホは未確認。
- 本番WebGLは`ProjectBuilder.BuildWeb`成功。`BuildOptions.None`、Unity Splash/Logo OFF、`stripEngineCode=false`、productVersion=2.4。wasm=24,143,640 bytes、data=5,048,119 bytes。wasm SHA-256=`443EF3AC0B286642C541DC3B311AC37BE4E385DB02950166473CC72337ACEF62`、data SHA-256=`E5DFD325366DA5A83BB594FBFF0FF11F10ABDA40E0505DBB3507FE886E1D188E`。
- ローカル本番ページでver2.4、World、Menu、Life Log、Archive初期Closed、ver2.4→ver2.3→ver2.2→ver2.1の順序を確認。390px幅ではMenuとLife Logの文字が収まることを確認。正式Mapにベンチがないため、本番画面のベンチ座りは検証用Buildで確認した。
- 正式Map SHA-256は作業前後で一致: `first_field`=`20A8B5AA77E5090644C701038BB51F5B40F812B90E48E91DB490A711A96A0938`、`halka_house`=`D72AC15286BF5BDDFE714895F03F75F5C26834E48D24EA7B4578B132CCA78D6D`。`first_field`の既存Git上の改行差とユーザーバックアップはCommit対象外。

## HPと公開

- HP現在表示ver2.4。上Archiveの先頭へ実公開予定日2026-10-08の`ベンチに座れるようになりました。`を追加。下Archiveは維持。
- 実装・本番Build commit: `448896f`。push / GitHub Pages / 公開URL / 最終git statusは公開確認後に追記。
