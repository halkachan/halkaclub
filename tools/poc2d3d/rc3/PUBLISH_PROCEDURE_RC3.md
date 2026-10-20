# はるかわーるど ver3.0 公開手順書（RC3）

**この手順は、ユーザーが公開を承認した後にだけ行います。**

Phase 7 では、この手順を、公開されない検証用ブランチ `claude/ver3.0-release-integration-test` で最後まで試しました（統合リハーサル）。`main` への merge、push、GitHub Pages の更新はしていません。

- 対象: `https://halkaclub.com/halkaworld/`（GitHub Pages、リポジトリ `HALKA HP` の `main`）
- 公開する Build: RC3（`HARUKA_WORLD_ver3.0_RC3/halkaworld/webgl/`、build は `BUILD_INFO.txt` を参照）。検証した Build をそのまま使い、作り直さない。

## 0. 公開の前に（承認の前に済ませる）

- [ ] RC3 を実機（iPhone・Android）で確認した（`DEVICE_TEST_PROCEDURE_RC3.md`）
- [ ] 公開ページの文章（名前「はるかわーるど」、あそびかた、更新履歴 ver3.0）を承認してもらった（`release/halkaworld/index.html`）
- [ ] 公開日を決めた（更新履歴の日付になる）
- [ ] ロールバック手順（`ROLLBACK_PROCEDURE_RC3.md`）を読んだ
- [ ] トップページのリンクの読み上げ名（`aria-label="HALKA WORLDを開く"`）と、共有画像（OGP）の文字をどうするか決めた（今回の手順では変えない。Phase 7 レポート 48章）
- [ ] トップページのﾊﾙｶﾁｬﾝの GIF（`assets/profile/halgif1.gif`）を、新しいドット絵（`hp_profile/halgif1_new.gif`）に替えるか決めた（替えるときは 3章の5）

## 1. 最新の main を確かめる（別件を守る）

1. `git fetch origin` を行う（取得だけで、何も書き換えない）。
2. `git log --oneline origin/main -20` で、別件の更新（みねはうす、Site Editor など）を確かめる。
3. Phase 7 のリハーサルの後に `origin/main` が進んでいたら、その差分を読む。
   - `git diff <リハーサルの元> origin/main --stat`
   - リハーサルの元は `1f43405` です。
4. `HALKA HP` の作業ツリーにある、ユーザーの変更（`GrassDecoration.prefab`）と未追跡の `backup-v05-*` には触れない。公開作業は別の作業ツリーで行う。

## 2. 公開用ブランチで統合する（承認後）

1. 公開用のブランチを、**最新の `origin/main` から**作る。
   ```
   git worktree add -b release/ver3.0 ../HALKA_HP_RELEASE origin/main
   ```
   main を古い状態に戻さない。
2. ver3.0 のソースを **merge** する（rebase、reset、force push はしない）。
   ```
   git merge --no-ff claude/ver3.0-2d3d-phase7
   ```
   - リハーサルでは衝突はなかった。
   - 衝突したら作業を止めて、両方の変更を読んでから相談する。片方を消して解決しない。
3. merge で HP のほかのファイルが変わっていないことを確かめる。
   ```
   git diff --name-only origin/main HEAD | grep -v "^unity-project/\|^blender/\|^tools/\|\.md$"
   ```
   何も出なければよい（リハーサルでも何も出なかった）。

## 3. 公開ファイルを置く

1. **Build**: `HARUKA_WORLD_ver3.0_RC3/halkaworld/webgl/` の中身を `halkaworld/webgl/` に置く。
   - `index.html` は置き換える。
   - `Build/` には RC3 の4ファイルを**足す**。
   - **ver2.9 の4ファイル（`a5659b8e…wasm`、`78a3b341…data`、`7bd4f0ed…framework.js`、`6fe21631…loader.js`）は消さずに残す**（理由は下）。
2. **ページ**: 公開日を入れてページを作り、`halkaworld/` に置く。
   ```
   python tools/poc2d3d/rc3/make_pages.py --release-date YYYY-MM-DD --out <作業用フォルダ>
   ```
   - `<作業用フォルダ>/release/halkaworld/index.html` の `__BUILD__` を RC3 の build に置き換えたものを、`halkaworld/index.html` にする。
   - `game-page.css` も同じ場所から置く。
   - 仮の日付は入れない（`__RELEASE_DATE__` が残っていたら公開しない）。
3. **バイトの確認**: Git の改行の設定で、ファイルが変わっていないことを確かめる。
   - 調べ方: `git show HEAD:halkaworld/webgl/<file> | sha256sum` と、RC3 フォルダのファイルの SHA-256 が同じになること。
   - リハーサルでは5ファイルとも一致した。
4. **検査**: Phase 1〜7、Version30Checks、MapAuthoringChecks、MAP EDITOR を、公開用の作業ツリーで実行する。
5. **トップページのﾊﾙｶﾁｬﾝの GIF**（替えると決めたときだけ）: **ゲームとは別のコミットにする**（ロールバックでゲームだけを戻せるように）。
   - `HARUKA_WORLD_ver3.0_RC3/hp_profile/halgif1_new.gif` を `assets/profile/halgif1.gif` に上書きする。
   - `index.html` と `script.js` の `assets/profile/halgif1.gif?v=2` を `?v=3` にする（古い絵がキャッシュに残らないように）。
   - トップページのほかのところは変えない。

**ver2.9 の Build を残す理由**（Phase 7 で確かめた）:

- GitHub Pages のページは最大10分キャッシュされます。その間、前に遊んだ人のブラウザは古い ver2.9 のページを出し、ver2.9 のゲームを読み込みます。
- このとき ver2.9 の Build ファイルが消えていると、次のことが起きます。
  - ゲームが「Unknown data format」で始まりません（セーブは壊れません）。
  - その「見つからない」という返事も、キャッシュに残ります。
- ファイル名に中身の識別子が入っているので、両方の Build を並べても混ざりません。
- ver2.9 の Build は、公開から1日以上たってから、別のコミットで消します。

## 4. 公開前の最終確認（ローカル）

- 公開用の作業ツリーを、本番と同じ形（サイトの一番上から）でローカルに出して、次を確かめる。
  - ver2.9 → ver3.0 の引き継ぎ
  - 家・3D・再読み込み
  - エクスポート・インポート
  - ほかのページ（トップ、みねはうす）
  - リンク
  - 全画面
  - スマホ表示
- Phase 7 の統合リハーサル（レポート 26章）と同じ手順です。
- 本番の halkaclub.com の保存データには、この段階では触れない。

## 5. 公開（ユーザーの承認の後）

1. ユーザーに最終の承認をもらう。
2. `main` に merge して push する（force push はしない）。
3. GitHub Pages の更新が終わるのを待つ（Actions、または数分）。

## 6. 公開後の確認

- [ ] `https://halkaclub.com/halkaworld/` が開き、名前が「はるかわーるど」、版が ver3.0、更新履歴の ver3.0 に公開日
- [ ] iframe が `webgl/?v=3.0&build=<RC3 の build>`、ゲームの版の表示が ver3.0
- [ ] 以前から遊んでいたブラウザで、歩いた数・プレイ時間が残っている（ver2.9 からの引き継ぎ。**本番のオリジンでの移行は、ここで初めて確かめる**）
- [ ] 少し歩いて、家に入り、3D にして再読み込み → 同じ場所から始まる
- [ ] エクスポート → ファイルができる、インポート → 確認画面
- [ ] スマホ（縦・横）、全画面、画面いっぱい表示
- [ ] ほかのページ（トップ、みねはうす など）が前と同じ（GIF を替えたときは、トップのﾊﾙｶﾁｬﾝが新しい絵で、つつく・増える動きが前と同じ）
- [ ] 公開から10分以内に、古いページを開いていたブラウザでもゲームが動く（ver2.9 の Build を残しているため）

## 7. 問題があったら

`ROLLBACK_PROCEDURE_RC3.md` に従って ver2.9 に戻します。
