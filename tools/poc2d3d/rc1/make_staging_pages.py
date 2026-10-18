"""Builds the RC1 staging parent page from the repository's public game page.

  python make_staging_pages.py

Reads ../../../halkaworld/index.html (the page published at https://halkaclub.com/halkaworld/,
never written) and writes staging/halkaworld/index.html: the same layout and CSS, the RC1 game
in the frame, the name shown as はるかわーるど. __BUILD__ is filled in by PocBuilder.
"""
import os

HERE = os.path.dirname(os.path.abspath(__file__))
SOURCE = os.path.join(HERE, "..", "..", "..", "halkaworld", "index.html")
OUT = os.path.join(HERE, "staging", "halkaworld")


def once(text, find, replacement):
    assert text.count(find) == 1, find
    return text.replace(find, replacement)


def page():
    html = open(SOURCE, encoding="utf-8").read().replace("\r\n", "\n")
    html = once(html, '<meta name="viewport" content="width=device-width, initial-scale=1" />',
                '<meta name="viewport" content="width=device-width, initial-scale=1" />\n'
                '    <meta name="robots" content="noindex, nofollow" />')
    # The user-facing name is fixed as はるかわーるど (URLs, IDs and file names keep HALKA).
    html = once(html, "<title>HALKA WORLD | HALKA</title>", "<title>はるかわーるど | HALKA</title>")
    html = once(html, '<meta property="og:title" content="HALKA WORLD | HALKA" />',
                '<meta property="og:title" content="はるかわーるど | HALKA" />')
    html = once(html, '<meta property="og:image:alt" content="HALKA WORLD" />',
                '<meta property="og:image:alt" content="はるかわーるど" />')
    html = once(html, "<span>HALKA WORLD</span>", "<span>はるかわーるど</span>")
    html = once(html, "<p>HALKA WORLD</p>", "<p>はるかわーるど</p>")
    html = once(html, '    <link rel="canonical" href="https://halkaclub.com/halkaworld/" />\n', "")
    html = once(html, '    <main id="top">',
                '    <div class="rc1-staging-note" role="note">ローカル検証用ステージング（はるかわーるど ver3.0 RC1）'
                '・公開ページと同じレイアウト。公開HPではありません。</div>\n    <main id="top">')
    html = once(html, '<h1 id="game-title">HALKA WORLD</h1>', '<h1 id="game-title">はるかわーるど</h1>')
    html = once(html, '<p class="game-current-version">ver2.9</p>', '<p class="game-current-version">ver3.0 RC1</p>')
    html = once(html,
                '<iframe src="webgl/?v=2.9&build=a5659b8e00cce110aca787eb15db1973-78a3b34151f63557cac46ba2bf930488" title="HALKA WORLD" allow="fullscreen" loading="eager"></iframe>',
                '<iframe src="webgl/?v=3.0-rc1&build=__BUILD__" title="はるかわーるど" allow="fullscreen" loading="eager"></iframe>')
    # Feedback after RC1: full screen from a button on the page (not inside the game). It puts
    # the game's frame in full screen; Esc (PC) or the back gesture (Android) leaves it. Browsers
    # without full screen for page elements (iPhone Safari) do not show the button.
    html = once(html, '          <p class="game-controls">',
                '          <p class="game-fullscreen"><button type="button" id="game-fullscreen-button" hidden>'
                '全画面にする</button></p>\n          <p class="game-controls">')
    script = '''    <script>
      (() => {
        const button = document.getElementById("game-fullscreen-button");
        const frame = document.querySelector(".unity-frame iframe");
        const request = frame && (frame.requestFullscreen || frame.webkitRequestFullscreen);
        if (!request || !(document.fullscreenEnabled || document.webkitFullscreenEnabled)) return;
        button.hidden = false;
        button.addEventListener("click", () => {
          const asked = request.call(frame);
          if (asked && asked.catch) asked.catch(() => {});
          if (frame.contentWindow) frame.contentWindow.focus();
        });
      })();
    </script>
'''
    html = once(html, "  </body>", script + "  </body>")
    # No analytics from a local page.
    start = html.index("    <!-- アクセス解析")
    end = html.index("</script>", start) + len("</script>\n")
    html = html[:start] + html[end:]
    style = ('    <style>.rc1-staging-note { position: sticky; top: 0; z-index: 5; padding: 6px 10px; '
             'background: #f6d55c; color: #2b2b2b; font: 700 12px/1.5 sans-serif; text-align: center; }\n'
             '      .game-fullscreen { margin: 10px 0 0; text-align: right; }\n'
             '      .game-fullscreen button { font: inherit; font-size: 14px; font-weight: 700; color: var(--ink); '
             'padding: 6px 14px; border: 2px solid var(--ink); border-radius: 4px; background: #fffdf4; cursor: pointer; '
             'transform: rotate(-0.6deg); }\n'
             '      .game-fullscreen button:hover { background: #f6d55c; }</style>\n')
    html = once(html, "  </head>", style + "  </head>")
    assert "HALKA WORLD" not in html, "a user-facing HALKA WORLD is left"
    return html


os.makedirs(OUT, exist_ok=True)
with open(os.path.join(OUT, "index.html"), "w", encoding="utf-8", newline="\n") as f:
    f.write(page())
print("staging page written to", OUT)
