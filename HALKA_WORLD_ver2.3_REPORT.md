# HALKA WORLD ver2.3 実装レポート

作成日: 2026-10-07、最終確認: 2026-10-08。テーマは「家の中の家具」とゲーム内文字の変更。MAP EDITORはv0.6、Map JSONはv4のまま。

## 1. 開始状態・Backup・Version

- 開始HEAD `5284eae`。開始時の変更は`first_field.hwmap.json`の改行差によるGit上の`M`（内容差分なし）と既存未追跡`backup-v05-20261006-174557/`。両者を今回のcommitへ入れない。
- `origin/main`の別件`a48ff12`を差分確認後にfast-forward。repo外`C:\Users\owner\Documents\ChatGPT\HALKA-v23-backup-20261007`へbundle、開始時status/diff、正式Map、Scene、UI/Runtime Script、WebGL一覧、HP HTMLを保存。bundle検証成功。遊びメモ書きのRaw FontはBackupへ保存していない。
- 正式Map開始時SHA-256: `first_field`=`20A8B5AA77E5090644C701038BB51F5B40F812B90E48E91DB490A711A96A0938`、`halka_house`=`D72AC15286BF5BDDFE714895F03F75F5C26834E48D24EA7B4578B132CCA78D6D`。作業後にも比較する。
- `GameVersion.Value`とWebGL productVersionを2.3、HP現在Versionをver2.3へ変更。更新履歴上Archiveへver2.3（2026-10-07）を最上位追加し、ver2.2/ver2.1と下Archiveは維持。

## 2. Furniture architectureと実装

- 正式`halka_house`に既存配置されているのは`bed_basic`のみ。`cushion_basic`と`desk_basic`は正式Mapへ追加しない。新しい`WorldObjectActionInteractable`はDefinitionのAction Pointと`WorldActionType`を読み、`MapRuntimeLoader2D`が配置ごとに生成する。Map座標や家具IDで反応を分岐しない。
- `examine`はAction Pointの文言を既存`GameHud`へ表示。`sleep`はver2.3では「ベッド。」だけを表示し、時間変更・暗転・Pose・Save Pointは行わない。`sit`/`none`は安全なno-op。既存の石・花・木・看板・井戸の小型Examine経路は維持。
- ベッドの旧`sleep_main`はRootから(0,+1)で、2×3 blocked footprintの内部にあり立てなかった。Catalog/Unity DefinitionのAction Pointを(0,-1)、Facing Upへ修正し、ベッド下からA/クリックで使えるようにした。`sleep`と`bed_sleep` Metadataは保持。正式MapのObject位置は変更なし。
- クッションは1×1、blocked offsetsなし。Playerより後ろのSorting Orderを保持。通常Stepで乗り降りでき、`StepCompleted`経由でLife Logへ各1歩加算する。`sit`と`cushion_sit`は将来用。
- 机は2×1 blocked。Action Pointから`examine`で「つくえ。」を表示する。追加C#編集なしでMAP EDITORの配置からRuntime生成可能。AUTOは家具Interactionを行わない。

## 3. ゲーム内Font・ライセンス

- 変更前はGameHud MessageとMenuで`k8x12L.ttf`、VersionやTouch AではGUI既定Fontも使用していた。`GameBitmapFont`をゲーム本編のPrimaryにし、GameHud（Version/Message）、Menu（AUTO/Life Log含む）、Touch Aに適用した。MAP EDITORとHPのFontは変更なし。
- 正式名称: 遊びメモ書き、作者: すもももじ / Sumomomoji (Do-Font)。[公式配布・利用規約](https://font.sumomo.ne.jp/asobi.html)を2026-10-07確認。公式記載は商用利用可能、フォントデータ再配布禁止、収録はかな・英数字・記号・第一水準漢字。公式ページ記載の最新版はver.1.02。
- 公式ZIPを一時領域へ取得し、ZIP内TTFをメモリからPillowでRasterize。Raw TTF/ZIPはUnity Assets、Git、Backup、Public WebGLへ入れない。取得用ZIPは生成後に一時領域から削除した。再生成用Scriptは私有ZIPパスを引数に取り、Raw Fontを出力しない。
- ローカル候補Buildに含まれるのは固定UIと現在のInteraction向け313文字の描画済みRGBA Atlas（2048×640 PNG）と字間数値のTextAsset。グリフ画像は通常のアンチエイリアスを維持し、Pixel Font風に無理に潰さない。対象文字のcoverageは生成時に公式TTFのcmapで確認。
- 初回のブラウザ表示では線が細く読みにくかった。利用者の指摘に従い、Rasterize時に同色2pxの軽いStrokeを加え、Menuを18/20→20/22、Messageを24→26、Versionを16→18、Touch Aを26→28へ調整した。390×844の再起動後、Menu・AUTO・Life Log・数字・Version・Aの文字切れがないことを目視確認。
- Dynamic Sign TextがAtlas未収録文字を含む場合は文字列全体を既存k8x12LへFallback。Fallbackにも字形がなければ`?`へ置き換えて空白化を避ける。既存k8x12Lは削除しない。
- Raw Fontの公開前scan結果、WebGL表示とUI layoutの結果は後述。2026-10-07時点ではBitmap Glyph Atlasの扱いが規約に明記されていないことを理由に公開を保留した。その後、ユーザーはRaw Font Dataを配布せず現在のBitmap Atlas方式で公開する方針を明示した。2026-10-08に公式規約を再確認し、Rasterizeした画像利用を明示的に禁止する記載は確認できなかったため、この方針で公開作業を再開した。

## 4. 検証とローカルProduction Build

- `Version23Checks.Run`: Unity 6000.3.10f1で成功。`Version22Checks.Run`を通じver2.0/2.1/2.2回帰も成功。ベッド・クッション・机の定義、複製の一時MapからのRuntime生成、クッションへの侵入と通常Step、机2セルの遮蔽と「つくえ。」、ベッドの「ベッド。」、sit no-op、Atlas/Fallbackを検証。
- `MapAuthoringChecks.Run`: 成功。MAP EDITOR v0.6 Core Testsは181/181成功。寝床Action Pointの到達可能化に合わせFixtureを更新。MAP EDITOR Window TitleとSchema v4は変更なし。
- 正式Mapの作業後SHA-256は開始時と完全一致（`first_field`=`20A8B5AA77E5090644C701038BB51F5B40F812B90E48E91DB490A711A96A0938`、`halka_house`=`D72AC15286BF5BDDFE714895F03F75F5C26834E48D24EA7B4578B132CCA78D6D`）。クッションと机を正式Mapへ追加していない。
- `ProjectBuilder.BuildWeb`の最終実行は成功。Unity WebGL `BuildOptions.None`（Development Build OFF）、Unity Splash/Logo OFF、`stripEngineCode=false`。`productVersion=2.3`。出力はwasm 24,139,697 bytes、data 5,042,097 bytes、loader.js 26,982 bytes、framework.js 429,553 bytes。旧ハッシュファイルは出力内から除去し、HP iframeのBuild IDを更新。
- ローカルWebGL PC目視: ver2.3、Menu/Life Log、World、家への通常移動、室内BedでAとPCクリック両方による「ベッド。」、家からの通常退出を確認。ベッドInteractionで時間・場所・Player Poseは変化しない。Crow、草、道、花、石、木も外マップで表示。机・クッションは正式Mapへ存在しないため、ブラウザ上の実操作はしていない。Unity一時Map Runtime Checkで確認した。
- 390×844のMobile Preview（`touchControls=1`）で新FontのMenu/Life Log/数値/Touch Aを目視。物理スマホは未確認。Life Logの既存Save・Reloadは今回のブラウザ実操作では未確認だが、継承したVersion22Checksで回帰を実施。
- Raw Font scan: `unity-project`と`halkaworld`にAsobi/遊びメモ書き名の`.ttf/.otf/.woff/.woff2`なし。WebGL `.data`に`AsobiMemogaki`、`Sumomomoji`、`asobi3.ttf`の名前なし。`.data`内の`asobi`という文字列はAtlas Resource名に由来する。これはRawファイルの不在確認であり、Bitmap Atlasの配布許諾確認を意味しない。

## 5. HP・Git・公開状態

- HP現在Versionはver2.3。上Archiveはver2.3（指定日2026-10-07）→ver2.2→ver2.1、下Archiveはver2.0以前のまま。両Archiveに`open`属性はない。HP全体Fontは変更なし。
- 2026-10-07にはRaw Fontの再配布禁止とBitmap Atlasに関する明文の不在を理由に公開を保留した。2026-10-08、ユーザーはRaw Font非配布を守ったうえで現行Atlas方式による公開を明示的に指示した。公式規約の再確認でRasterize画像利用の明示的禁止は確認できず、公開手順を再開した。
- ローカル実装Commit: `4a64938`、Branch: `codex/ver2.3-furniture-font`。公開先mainのSite Editor v1.6〜v1.9を含む最新`099e4be`を取り込み、merge commit `6f024f0`を作成。公開前のURLはver2.2だった。公開結果は以下の追記へ記録する。
- 最終git status見込みは`M unity-project/Assets/Content/Maps/Authoring/first_field.hwmap.json`（開始時からの改行差）および`?? backup-v05-20261006-174557/`（既存の未追跡Backup）のみ。本レポートのCommit後に照合する。どちらも今回のCommit対象から除外する。
- ver2.4候補はベンチ。今回、座りPoseや時間システムは実装していない。

## 6. 2026-10-08 公開再開時の検証

- `Version23Checks.Run`、`MapAuthoringChecks.Run`を最新main統合後のUnity 6000.3.10f1で再実行し成功。ログはrepo外Backupの`version23-publish-check.log`と`map-authoring-publish-check.log`に保存。UnityによるScene/Prefab再生成差分は開始時に存在しなかったため、検証後に復元して公開差分から除外した。
- SITE EDITOR v1.9のテストは301/301成功。Windows改行で作品データ末尾のCRが失われる処理と依頼ページのRaw文章の比較を修正し、ver2.3履歴に合わせてWorld履歴テストの期待値を更新した。HALKA WORLD履歴の無変更再構築は元のHTMLを返すようにした。これらは別件Site Editorを最新mainから取り込んだ際の共存確認であり、ゲーム機能は変更していない。
- 正式Map SHA-256は前記の開始時値と一致。`first_field.hwmap.json`の開始時からの改行差と既存未追跡BackupはCommitへ含めない。MAP EDITOR v0.6、GameVersion 2.3、HPのver2.3履歴を維持。
- 公開前Raw Font scan: Git追跡ファイルとUnity Assets/WebGL内に「遊びメモ書き」のRaw TTF/OTF/WOFF/WOFF2/公式ZIPはなし。WebGL `.data`内にもRaw Font名・拡張子の一致はなし。Bitmap Atlas/metricsは保持。
- Production WebGLは既存の最終成功Buildを使用。今回の公開再開でUnityのゲームソースを変更していないため再Buildしない。

## 7. main公開と公開後確認

- `origin/main`の`099e4be`を取り込んだ後、公開前検証の修正を`3419a8d`へCommit。`git fetch origin main`後に`origin/main`がHEADの祖先であることを確認し、`git push origin HEAD:main`で`099e4be..3419a8d`を通常push。force pushは使用していない。ゲーム本編はver2.3、MAP EDITORはv0.6のまま。
- GitHub Pages `pages-build-deployment` run `37697683372`はHEAD `3419a8d2e6570c01e7986c54a9813e1165bd8bc8`でbuild/deployとも成功。[公開URL](https://halkaclub.com/halkaworld/)はHTTP 200。公開HPの現在Versionはver2.3で、上Archiveは初期Closed、開くとver2.3→ver2.2→ver2.1の順と2026-10-07の日付を確認。下Archiveも初期Closed。
- 公開WebGLはHTTP 200で起動し、canvas内にver2.3が表示された。`280a90b18ae4463ce46d09d032f98222.wasm`は24,139,697 bytes・SHA-256 `9389827198E768A168F4158506FF8B7759D873B8D4606CFDAB896ADA0D550878`、`2b032dd69e5d490196584ba044397927.data`は5,042,097 bytes・SHA-256 `E52E74EF82B94F0732E4911F8C65B4A2B69BBD83087C99B81148BF0B9C25EB81`。いずれもローカル最終Buildと一致。wasmのMIMEは`application/wasm`、dataは`application/octet-stream`。
- 公開WebGLのブラウザ目視でGame Version、Menu、AUTO、せいかつきろく、あるいたかず、プレイじかん、もどるに太さ調整後の遊びメモ書きBitmap Fontが表示された。公開版のMessageとTouch Aは今回未操作で、ローカルWebGLでは前記のとおり確認済み。物理スマホは未確認。
- 公開`origin/main`の追跡ファイル一覧に遊びメモ書きのRaw `.ttf/.otf/.woff/.woff2/.zip`なし。公開wasm/dataはローカルBuildと同一ハッシュで、前記Raw Font scan結果がそのまま適用される。Rasterize済みAtlasとmetricsのみ配布。
- 最終Git状態は作業開始時からの`M unity-project/Assets/Content/Maps/Authoring/first_field.hwmap.json`（改行差、配置内容とSHA-256は不変）と`?? backup-v05-20261006-174557/`のみ。いずれも今回のCommitとpushには含めていない。
