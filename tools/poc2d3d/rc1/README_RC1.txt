はるかわーるど ver3.0 Release Candidate 1（ローカル版）
================================================================

これは公開前の確認用にローカルで動かす Release Candidate（RC1）です。
公開HP（halkaclub.com）・GitHub Pages・公開中の ver2.9 には何も反映していません。


1. 起動
-------
  start_rc1.bat をダブルクリックします（Python 3 が必要です）。
  サーバーが起動し、少しするとブラウザで次のページが開きます。

    http://127.0.0.1:8895/halkaworld/

  手動で起動する場合（このフォルダで）:
    python serve_rc1.py

  止めるときは、黒いウィンドウで Ctrl+C を押すか、ウィンドウを閉じます。


2. ページ
---------
  http://127.0.0.1:8895/halkaworld/
      公開中のゲームページ（halkaworld/index.html）と同じレイアウト・同じCSSで、
      枠の中に RC1 を入れたステージングページです。
  http://127.0.0.1:8895/halkaworld/index-mobilefix.html
      上と同じページに、スマホ表示の改善案（game-page-rc1-proposal.css）を足したものです。
      公開HPの CSS は変更していません。
  http://127.0.0.1:8895/halkaworld/webgl/
      ゲームだけの画面です。
  http://127.0.0.1:8895/halkaworld/webgl/?debug=1
      検証用パネルを表示します（DEBUG_RC1.txt）。通常は表示されません。


3. 遊び方（操作は今までと同じです）
-----------------------------------
  PC:   WASD・矢印キーで1マスずつ移動 / 対象をクリック
  スマホ: 十字キーで移動、Aボタンで正面を調べる（横向きで遊びます）
  2D と 3D の切り替えは、左上メニューの「ひょうじ」です。新しいボタンは増えていません。


4. セーブデータについて
-----------------------
  せいかつきろく・時間は、ブラウザ内（IndexedDB）に保存されます。保存先は
  「ページの場所（オリジンとパス）」ごとに分かれます。
    - この RC1（http://127.0.0.1:8895/...）のデータは、公開版（https://halkaclub.com/...）の
      データとは別の場所です。RC1 を遊んでも公開版のデータは変わりません。
    - 公開版と同じ場所（/halkaworld/webgl/）に置けば、保存キーと形式が同じなので
      今までのデータを読み込む設計です（ローカルで ver2.9 → RC1 の引き継ぎを確認済み。
      詳しくは HARUKA_WORLD_ver3.0_PHASE5_REPORT.md の 32〜34 章）。
  保存キー（変更なし）: HALKA_WORLD_life_log_v1 / HALKA_WORLD_time_cycle_anchor_v1


5. フォルダ
-----------
  start_rc1.bat                 起動（lan を付けると同じWi-Fiのスマホからも開けます）
  serve_rc1.py                  ローカルサーバー（読み取りのみ）
  halkaworld/index.html         ステージングページ（公開ページと同じレイアウト）
  halkaworld/index-mobilefix.html  スマホ改善案つきのステージングページ
  halkaworld/game-page*.css     公開ページのCSS（コピー）と改善案のCSS
  halkaworld/webgl/             RC1 本体（WebGL）
  style.css, favicon.png        公開サイトのものをコピー（ステージングページ用）
  BUILD_INFO.txt                ビルドの識別子・日時
  DEBUG_RC1.txt                 検証用パネルの使い方
  VERIFICATION_SETTINGS_RC1.txt RC1 の確定設定（検証のときの基準値）
  DEVICE_TEST_PROCEDURE_RC1.md  スマホ実機での確認手順


6. 仮のもの・注意
-----------------
  - クッションに座った2D画像は、bench_sit.png から作った「仮」の画像です
    （unity-project/Assets/POC2D3D/Provisional/）。元の bench_sit.png は変更していません。
  - 3D のﾊﾙｶﾁｬﾝはまだ仮のブロック状モデルです（正式モデルは Phase 5 の対象外）。
  - 看板の「ここは HALKA WORLD。」はマップデータの文章なので、そのままです。
