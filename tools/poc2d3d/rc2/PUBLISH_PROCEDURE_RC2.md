# はるかわーるど ver3.0 公開手順書（案）

**この手順は、ユーザーが公開を承認した後にだけ行います。** Phase 6 では、この手順書を作っただけで、どの手順も実行していません。

対象: `https://halkaclub.com/halkaworld/`（GitHub Pages、リポジトリ `HALKA HP` の `main`）

## 0. 事前の確認（承認前に済ませる）

- [ ] RC2 を実機（iPhone・Android）で確認した（`DEVICE_TEST_PROCEDURE_RC2.md`）
- [ ] エクスポート・インポートを実機で確認した
- [ ] 公開ページの文章（名前「はるかわーるど」、更新履歴、操作説明）を承認してもらった
- [ ] ロールバック手順（`ROLLBACK_PROCEDURE_RC2.md`）を読んだ

## 1. 最新の main との差分確認・別件の保護

1. `git fetch origin` を行う（取得だけで、何も書き換えない）。
2. `git log --oneline origin/main -20` で、別件の更新（みねはうす、Site Editor など）がないか確かめる。
3. `git diff origin/main...claude/ver3.0-2d3d-phase6 --stat` で、変更されるファイルを確かめる。
4. 別件のファイル（`minehouse/` など）を、ver3.0 側で変えていないことを確かめる。
5. `HALKA HP` の作業ツリーにある、ユーザーの変更（`GrassDecoration.prefab`）と未追跡の `backup-v05-*` には触れない。

## 2. Git の統合（承認後）

1. 公開用のブランチ `release/ver3.0` を、最新の `origin/main` から作る（main を古い状態に戻さない）。
2. `claude/ver3.0-2d3d-phase6` を merge する（rebase や force push はしない）。衝突したら作業を止めて相談する。
3. すべての検査を行う（Phase 1〜6、Version30Checks、MapAuthoringChecks、MAP EDITOR）。

## 3. Production Build

1. RC2 と同じ RC Scene（`HARUKA_WORLD_ver3.0_RC2.unity` を正式名にした Scene）を、`halkaworld/webgl/` に Build する。
2. **ゲームのパスは `/halkaworld/webgl/` から変えない**（保存先が変わるため）。
3. 版の表示を `ver3.0` にする（`PocDebugPanel.ReleaseCandidateVersionLabel`、セーブの `gameVersion`）。

## 4. 公開ページ（halkaworld/index.html）

- 名前を「はるかわーるど」に（タイトル、OGP、見出し、帯、ヘッダー）
- 版の表示を `ver3.0` に
- 更新履歴に ver3.0 を追加（2D/3D、自動セーブと続きから再開、セーブデータの書き出し・復元、全画面 など）
- 操作説明に「ひょうじ」「全画面にする」「セーブデータ」を追加
- ゲームの枠の下の「全画面にする」ボタン、画面いっぱい表示、そのスクリプトとスタイルを入れる（`tools/poc2d3d/rc2/make_staging_pages.py` と同じもの）
- iframe の `allow="fullscreen"` を残す
- `?v=3.0&build=<新しい build>` に更新する（キャッシュ対策）
- アクセス解析のスクリプトと canonical は、公開ページのものを残す（ステージングで外したもの）

## 5. セーブ移行の確認（公開の直前）

- 公開と同じパスの別のオリジン（例: ローカルの `/halkaworld/webgl/`）で、ver2.9 → ver3.0 の引き継ぎをもう一度確かめる（Phase 6 レポート 24章と同じ手順）。

## 6. GitHub Pages

1. ユーザーの承認を得てから `main` に merge し、push する。
2. GitHub Pages の更新が終わるのを待つ（Actions、または数分）。

## 7. 公開後の確認

- [ ] `https://halkaclub.com/halkaworld/` が開き、名前が「はるかわーるど」、版が ver3.0
- [ ] 以前から遊んでいたブラウザで、歩いた数・プレイ時間が残っている（ver2.9 からの引き継ぎ）
- [ ] 少し歩いて再読み込み → 同じ場所から始まる
- [ ] エクスポート → ファイルができる、インポート → 確認画面
- [ ] スマホ（縦・横）、全画面、画面いっぱい表示
- [ ] ほかのページ（トップ、みねはうす など）が前と同じ

## 8. 問題があったら

`ROLLBACK_PROCEDURE_RC2.md` に従って ver2.9 に戻します。
