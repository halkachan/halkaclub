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
- Raw Fontの公開前scan結果、WebGL表示とUI layoutの結果は後述。公式規約はRaw TTFの組込み可否と、このような再利用可能なBitmap Glyph Atlasの配布可否を個別に明記していない。Raw FontをBuildへ渡していないことは確認済みだが、Atlasが規約中の「フォントデータ」に該当しないと断定できないため、公開は保留する。

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
- Raw Fontの再配布禁止は公式に明記されている一方、描画済み313文字と字間情報を配布してよいかは確認できなかった。ユーザーの「不明なら公開を止める」に従い、GitHub pushとGitHub Pagesへの公開は実施しない。現在の公開URL `https://halkaclub.com/halkaworld/` は旧版のまま。公開権利について作者から明示的な許諾または同等の確認が得られた後に公開可能。
- ローカル実装Commit: `4a64938`、Branch: `codex/ver2.3-furniture-font`。公開先mainへmerge/pushしていない。ローカルの公式ページはver2.3だが、2026-10-08の公開URLへのHTTP確認では200応答かつ現在表示はver2.2だった。公開WebGL/Font表示は未確認。
- 最終git status見込みは`M unity-project/Assets/Content/Maps/Authoring/first_field.hwmap.json`（開始時からの改行差）および`?? backup-v05-20261006-174557/`（既存の未追跡Backup）のみ。本レポートのCommit後に照合する。どちらも今回のCommit対象から除外する。
- ver2.4候補はベンチ。今回、座りPoseや時間システムは実装していない。
