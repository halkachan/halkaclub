"""Builds the ver3.0 game pages from the repository's public game page (ver2.9).

  python make_pages.py                          release page with the date left open (__RELEASE_DATE__)
  python make_pages.py --release-date 2026-10-25   the same page with the publishing date filled in

Reads ../../../halkaworld/index.html and game-page.css (the page published at
https://halkaclub.com/halkaworld/, never written here) and writes:

  release/halkaworld/index.html, game-page.css  the ver3.0 public page (for the release; not published)
  staging/halkaworld/index.html, game-page.css  the same page for the local RC3 staging (not indexed,
                                                no analytics, a staging note on top)

__BUILD__ is filled in by PocBuilder. The update history keeps every entry of the public page and
adds ver3.0 at the top of its "ver2.1 ～ ver3.0" group; its date stays __RELEASE_DATE__ until the
day of publishing is decided (no provisional date is put on the public page).
"""
import argparse
import os
import re

HERE = os.path.dirname(os.path.abspath(__file__))
PUBLIC = os.path.join(HERE, "..", "..", "..", "halkaworld")
DATE_TOKEN = "__RELEASE_DATE__"


def once(text, find, replacement):
    assert text.count(find) == 1, find
    return text.replace(find, replacement)


HOW_TO_PLAY = """          <section class="game-guide" aria-labelledby="game-guide-title">
            <h2 id="game-guide-title">あそびかた</h2>
            <dl class="game-guide-list">
              <dt>パソコン</dt>
              <dd>WASD・矢印キーで1マスずつ歩きます。近くのものをクリックすると、調べたり座ったりできます。3D では、ﾊﾙｶﾁｬﾝをクリックすると正面のものを調べます。</dd>
              <dt>スマホ</dt>
              <dd>十字キーで歩き、A ボタンで正面を調べます。縦向きでも横向きでも遊べます。</dd>
              <dt>2D と 3D</dt>
              <dd>左上の ☰ メニューの「ひょうじ」で、ドット絵の 2D と、立体の 3D を切り替えられます。どちらも同じ世界です。</dd>
              <dt>全画面</dt>
              <dd>ゲームの下の「全画面にする」で大きく遊べます。iPhone などでは画面いっぱいに広がり、下の「もどす」で元に戻ります。</dd>
              <dt>セーブ</dt>
              <dd>セーブは自動です。次に開いたときは、前回いた場所から続きます。セーブは、このブラウザの中に保存されます。</dd>
              <dt>バックアップ</dt>
              <dd>☰ メニューの「セーブデータ」から、セーブをファイルに書き出す（エクスポート）・ファイルから戻す（インポート）ことができます。ブラウザのデータを消す前や、ほかの端末で続きを遊びたいときに使ってください。</dd>
            </dl>
            <p class="game-note">ブラウザのサイトデータを消したり、プライベートブラウズで遊んだりすると、セーブは残りません。同じゲームを2つのタブで開いたときは、あとからセーブしたタブのデータが残ります。</p>
          </section>
"""

UPDATE_V30 = """            <article class="update-entry">
              <h3>ver3.0 <time datetime="__RELEASE_DATE__">__RELEASE_DATE__</time></h3>
              <ul>
                <li>2Dと3Dを切り替えて遊べるようになりました。3Dでは、ローポリの世界をﾊﾙｶﾁｬﾝやカラスと歩けます。</li>
                <li>朝と夜が切り替わるようになりました。</li>
                <li>自動でセーブされ、次は前回の場所から続きを遊べるようになりました。セーブはファイルに書き出して、バックアップできます。</li>
                <li>スマホの縦向き・横向きと、全画面表示に対応しました。</li>
              </ul>
            </article>
"""

FULLSCREEN_SCRIPT = """    <script>
      (() => {
        const button = document.getElementById("game-fullscreen-button");
        const box = document.querySelector(".unity-frame");
        const frame = box && box.querySelector("iframe");
        const bar = document.getElementById("game-fill-bar");
        if (!frame) return;
        const request = frame.requestFullscreen || frame.webkitRequestFullscreen;
        const real = !!request && !!(document.fullscreenEnabled || document.webkitFullscreenEnabled);
        const focusGame = () => { if (frame.contentWindow) frame.contentWindow.focus(); };
        const fit = () => { box.style.height = `${window.innerHeight - bar.offsetHeight}px`; };
        function fill(on, fromHistory) {
          if (document.body.classList.contains("game-filled") === on) return;
          document.body.classList.toggle("game-filled", on);
          if (on) {
            fit();
            window.addEventListener("resize", fit);
            if (!fromHistory) history.pushState({ halkaGameFilled: true }, "");
          } else {
            box.style.height = "";
            window.removeEventListener("resize", fit);
            if (!fromHistory && history.state && history.state.halkaGameFilled) history.back();
          }
          focusGame();
        }
        window.addEventListener("popstate", () => fill(false, true));
        document.getElementById("game-fill-back").addEventListener("click", () => fill(false, false));
        button.hidden = false;
        button.addEventListener("click", () => {
          if (!real) { fill(true, false); return; }
          const asked = request.call(frame);
          if (asked && asked.catch) asked.catch(() => fill(true, false));
          focusGame();
        });
      })();
    </script>
"""

CSS_V30 = """
/* ver3.0: the full screen button under the game, the page-filling view (iPhone Safari and other
   browsers without full screen for a page element), the guide. */
.game-fullscreen {
  margin: 10px 0 0;
  text-align: right;
}

.game-fullscreen button {
  font: inherit;
  font-size: 14px;
  font-weight: 700;
  color: var(--ink);
  padding: 6px 14px;
  border: 2px solid var(--ink);
  border-radius: 4px;
  background: #fffdf4;
  cursor: pointer;
  transform: rotate(-0.6deg);
}

.game-fullscreen button:hover,
.game-fill-bar button:hover {
  background: #f6d55c;
}

.game-fill-bar {
  display: none;
}

body.game-filled {
  overflow: hidden;
}

body.game-filled .unity-frame {
  position: fixed;
  left: 0;
  top: 0;
  width: 100vw;
  z-index: 1000;
  border: 0;
}

body.game-filled .game-fill-bar {
  display: flex;
  position: fixed;
  left: 0;
  right: 0;
  bottom: 0;
  z-index: 1001;
  justify-content: flex-end;
  align-items: center;
  margin: 0;
  min-height: 40px;
  box-sizing: border-box;
  padding: 4px 12px calc(4px + env(safe-area-inset-bottom, 0px));
  background: #fffdf4;
  border-top: 2px solid var(--ink);
}

.game-fill-bar button {
  font: inherit;
  font-size: 13px;
  font-weight: 700;
  color: var(--ink);
  padding: 4px 14px;
  border: 2px solid var(--ink);
  border-radius: 4px;
  background: #fffdf4;
  cursor: pointer;
}

.game-guide {
  margin-top: 26px;
  color: var(--ink);
}

.game-guide h2 {
  margin: 0 0 10px;
  font-size: 20px;
}

.game-guide-list {
  display: grid;
  grid-template-columns: max-content 1fr;
  gap: 6px 14px;
  margin: 0;
  font-size: 14px;
  line-height: 1.7;
}

.game-guide-list dt {
  font-weight: 700;
}

.game-guide-list dd {
  margin: 0;
}

.game-note {
  margin: 12px 0 0;
  color: var(--pencil);
  font-size: 13px;
  line-height: 1.7;
}

@media (max-width: 560px) {
  .game-guide-list {
    grid-template-columns: 1fr;
    gap: 2px;
  }

  .game-guide-list dd {
    margin-bottom: 8px;
  }
}
"""


def release_page(date):
    html = open(os.path.join(PUBLIC, "index.html"), encoding="utf-8").read().replace("\r\n", "\n")
    # The user-facing name is はるかわーるど (the URL, folders and save keys keep HALKA / halkaworld).
    html = once(html, "<title>HALKA WORLD | HALKA</title>", "<title>はるかわーるど | HALKA</title>")
    html = once(html, '<meta property="og:title" content="HALKA WORLD | HALKA" />',
                '<meta property="og:title" content="はるかわーるど | HALKA" />')
    html = once(html, '<meta property="og:image:alt" content="HALKA WORLD" />',
                '<meta property="og:image:alt" content="はるかわーるど" />')
    # The card with the new name has its own file, so SNS caches of the old card are not reused.
    html = once(html, '<meta property="og:image" content="https://halkaclub.com/assets/ogp/halkaworld.png?v=1" />',
                '<meta property="og:image" content="https://halkaclub.com/assets/ogp/harukaworld_v3.png" />')
    html = once(html, "<span>HALKA WORLD</span>", "<span>はるかわーるど</span>")
    html = once(html, "<p>HALKA WORLD</p>", "<p>はるかわーるど</p>")
    html = once(html, '<h1 id="game-title">HALKA WORLD</h1>', '<h1 id="game-title">はるかわーるど</h1>')
    html = once(html, '<p class="game-current-version">ver2.9</p>', '<p class="game-current-version">ver3.0</p>')
    html = re.sub(r'<iframe src="webgl/\?v=2\.9&build=[0-9a-f-]+" title="HALKA WORLD"',
                  '<iframe src="webgl/?v=3.0&build=__BUILD__" title="はるかわーるど"', html, count=1)
    assert 'src="webgl/?v=3.0&build=__BUILD__"' in html
    # A new stylesheet version, so a browser holding the ver2.9 game-page.css loads this one.
    html = once(html, '<link rel="stylesheet" href="game-page.css" />', '<link rel="stylesheet" href="game-page.css?v=3.0" />')
    # The full screen button and the page-filling bar under the game, then the guide in place of
    # the one-line controls.
    start = html.index('          <p class="game-controls">')
    end = html.index("</p>\n", start) + len("</p>\n")
    html = (html[:start] +
            '          <p class="game-fullscreen"><button type="button" id="game-fullscreen-button" hidden>全画面にする</button></p>\n'
            '          <p class="game-fill-bar" id="game-fill-bar"><button type="button" id="game-fill-back">もどす</button></p>\n' +
            HOW_TO_PLAY + html[end:])
    # ver3.0 heads the "ver2.1 ～ ver3.0" group; every older entry stays as it is.
    group = html.index("<summary>ver2.1 ～ ver3.0</summary>")
    entries = html.index('              <div class="update-history-entries">\n', group) + len('              <div class="update-history-entries">\n')
    html = html[:entries] + UPDATE_V30 + html[entries:]
    html = once(html, "  </body>", FULLSCREEN_SCRIPT + "  </body>")
    if date:
        assert re.fullmatch(r"\d{4}-\d{2}-\d{2}", date), date
        html = html.replace(DATE_TOKEN, date)
    assert "HALKA WORLD" not in html, "a user-facing HALKA WORLD is left"
    return html


def staging_page(release):
    html = once(release, '<meta name="viewport" content="width=device-width, initial-scale=1" />',
                '<meta name="viewport" content="width=device-width, initial-scale=1" />\n'
                '    <meta name="robots" content="noindex, nofollow" />')
    html = once(html, '    <link rel="canonical" href="https://halkaclub.com/halkaworld/" />\n', "")
    html = once(html, '    <main id="top">',
                '    <div class="rc3-staging-note" role="note">ローカル検証用ステージング（はるかわーるど ver3.0 RC3）'
                '・公開予定のページと同じ内容。公開HPではありません。</div>\n    <main id="top">')
    html = once(html, "  </head>",
                '    <style>.rc3-staging-note { position: sticky; top: 0; z-index: 5; padding: 6px 10px; background: #f6d55c; '
                'color: #2b2b2b; font: 700 12px/1.5 sans-serif; text-align: center; } body.game-filled .rc3-staging-note { display: none; }</style>\n'
                "  </head>")
    html = html.replace(DATE_TOKEN, "公開日")
    # No analytics from a local page.
    start = html.index("    <!-- アクセス解析")
    end = html.index("</script>", start) + len("</script>\n")
    return html[:start] + html[end:]


def write(path, text):
    os.makedirs(os.path.dirname(path), exist_ok=True)
    with open(path, "w", encoding="utf-8", newline="\n") as f:
        f.write(text)


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument("--release-date", default="")
    parser.add_argument("--out", default=HERE)
    args = parser.parse_args()
    css = open(os.path.join(PUBLIC, "game-page.css"), encoding="utf-8").read().replace("\r\n", "\n")
    assert ".game-fullscreen" not in css, "game-page.css already has the ver3.0 part"
    css = css.rstrip("\n") + "\n" + CSS_V30
    release = release_page(args.release_date)
    write(os.path.join(args.out, "release", "halkaworld", "index.html"), release)
    write(os.path.join(args.out, "release", "halkaworld", "game-page.css"), css)
    write(os.path.join(args.out, "staging", "halkaworld", "index.html"), staging_page(release))
    write(os.path.join(args.out, "staging", "halkaworld", "game-page.css"), css)
    print("pages written to", os.path.join(args.out, "release"), "and", os.path.join(args.out, "staging"),
          "(date:", args.release_date or DATE_TOKEN + ")")


if __name__ == "__main__":
    main()
