# HALKA WORLD ver2.4 作業レポート（座りSprite選択待ち）

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
- これらは**未承認の候補**であり、`Assets/Content/Character`には登録していない。GameVersion、Scene、正式Map、HP、WebGL、公開サイトはまだ変更していない。

## 次の工程

ユーザーによるA/B/Cの採用または修正指示を待つ。採用後に座り状態、A/クリックの左右Action Point、AUTO抑止、立ち上がり、Sprite切替とSorting、Version24Checks、テストMapでの実操作、WebGL検証、HP履歴、公開を進める。ユーザー承認前に候補を正式Spriteとして登録・公開しない。
