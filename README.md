# HALKA Links：VS Code編集版

現在のリンク集を、特別な制作ソフトやビルド作業なしで編集できるHTML・CSSへ移したものです。

## ファイルの役割

- `index.html`：一般公開するリンク集
- `club/index.html`：はるかくらぶの案内とKoofrへの入口
- `style.css`：色、文字サイズ、余白、スマホ表示などのデザイン
- `script.js`：最新動画のサムネイル・リンクを自動設定する仕組み
- `works/index.html`：作品一覧ページ
- `works/works-data.js`：作品のタイトル・YouTube URLを追加する場所
- `works/works.css`：作品一覧ページ専用のデザイン
- `works/works.js`：作品カードとサムネイルを自動生成する仕組み
- `assets/fonts`：現在使用している手書きフォント

## VS Codeで編集する

1. このフォルダを右クリックして、VS Codeで開きます。
2. トップページの文章やURLは `index.html` を編集します。
3. はるかくらぶの更新日やKoofr URLは `club/index.html` を編集します。
4. 色や配置は `style.css` を編集します。
5. 保存後、`index.html` をブラウザで開くと確認できます。

HTML内に `<!-- ～ -->` の形で、主な編集箇所へ日本語の目印を入れています。

## よく変更する場所

### SNSのURL

`index.html` 内で対象サービスを検索し、次の `href="..."` の中だけを書き換えます。

```html
<a href="https://変更後のURL">
```

### 最新動画のURL

`script.js` の先頭にある次の行のURLを、新しい動画のURLに書き換えます。

```js
const latestVideoUrl = "https://youtu.be/RFQw7HejNZ0";
```

`youtube.com/watch?v=...`、`youtu.be/...`、YouTube Shorts (`youtube.com/shorts/...`) のいずれの形式でも書き換えるだけで、サムネイル画像とクリック時のリンクが自動的に更新されます。

### 作品の追加

`works/works-data.js` 内の該当カテゴリー（歌ってみた／ひらがな一文字シリーズ／方向シリーズ／その他）の `works` 配列に、次の形をコピーして追加します。

```js
{
  title: "作品タイトル",
  publishedAt: "年-月-日",
  youtubeUrl: "YouTube動画URL"
},
```

タイトル・投稿日・URLを追加するだけで、作品一覧ページ（`works/index.html`）にカードとサムネイルが自動で表示されます。データが空のカテゴリーには「準備中」と表示されます。

### はるかくらぶの更新履歴

`club/index.html` 内の `【最新更新】` を検索し、日付と項目を書き換えます。

### KoofrのURL

`club/index.html` 内で `https://k00.fr/halkaclub` を検索し、新しいURLへ置き換えます。

Koofrのパスワードは、このサイトのファイルやGitHubには書かないでください。パスワード入力はKoofr側だけで行います。

### 基本色

`style.css` の先頭にある次の値を変更します。

```css
--paper: #fffef6; /* 紙の白 */
--desk: #e9e8df;  /* 外側の灰色 */
--ink: #16150f;   /* 黒 */
--yellow: #ffdc18; /* 黄色 */
```

## 壊してしまったとき

GitHubへ保存した後は、GitHub Desktopの「History」から正常だった変更へ戻せます。大きく触る前に、GitHub Desktopで一度コミットしておくと安全です。

## 公開について

このままGitHub PagesやCloudflare Pagesへ公開できます。公開設定と `halkaclub.com` の接続は、ドメイン取得後に行います。
