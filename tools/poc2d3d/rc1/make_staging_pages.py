"""Builds the RC1 staging parent pages from the repository's public game page.

  python make_staging_pages.py

Reads ../../../halkaworld/index.html (the page published at https://halkaclub.com/halkaworld/,
never written) and writes staging/halkaworld/index.html (same layout and CSS, the RC1 game in
the frame) and staging/halkaworld/index-mobilefix.html (the same plus the phone layout
proposal in game-page-rc1-proposal.css). __BUILD__ is filled in by PocBuilder.
"""
import os

HERE = os.path.dirname(os.path.abspath(__file__))
SOURCE = os.path.join(HERE, "..", "..", "..", "halkaworld", "index.html")
OUT = os.path.join(HERE, "staging", "halkaworld")


def once(text, find, replacement):
    assert text.count(find) == 1, find
    return text.replace(find, replacement)


def page(proposal):
    html = open(SOURCE, encoding="utf-8").read().replace("\r\n", "\n")
    html = once(html, '<meta name="viewport" content="width=device-width, initial-scale=1" />',
                '<meta name="viewport" content="width=device-width, initial-scale=1" />\n'
                '    <meta name="robots" content="noindex, nofollow" />')
    html = once(html, "<title>HALKA WORLD | HALKA</title>",
                "<title>はるかわーるど | HALKA（ローカル検証 RC1）</title>")
    html = once(html, '    <link rel="canonical" href="https://halkaclub.com/halkaworld/" />\n', "")
    if proposal:
        html = once(html, '    <link rel="stylesheet" href="game-page.css" />',
                    '    <link rel="stylesheet" href="game-page.css" />\n'
                    '    <link rel="stylesheet" href="game-page-rc1-proposal.css" />')
    html = once(html, '    <main id="top">',
                '    <div class="rc1-staging-note" role="note">ローカル検証用ステージング（はるかわーるど ver3.0 RC1）'
                + ('・スマホ表示の改善案つき' if proposal else '・公開ページと同じレイアウト')
                + '。公開HPではありません。</div>\n    <main id="top">')
    html = once(html, '<h1 id="game-title">HALKA WORLD</h1>', '<h1 id="game-title">はるかわーるど</h1>')
    html = once(html, '<p class="game-current-version">ver2.9</p>', '<p class="game-current-version">ver3.0 RC1</p>')
    html = once(html,
                '<iframe src="webgl/?v=2.9&build=a5659b8e00cce110aca787eb15db1973-78a3b34151f63557cac46ba2bf930488" title="HALKA WORLD" allow="fullscreen" loading="eager"></iframe>',
                '<iframe src="webgl/?v=3.0-rc1&build=__BUILD__" title="はるかわーるど" allow="fullscreen" loading="eager"></iframe>')
    # No analytics from a local page.
    start = html.index("    <!-- アクセス解析")
    end = html.index("</script>", start) + len("</script>\n")
    html = html[:start] + html[end:]
    style = ('    <style>.rc1-staging-note { position: sticky; top: 0; z-index: 5; padding: 6px 10px; '
             'background: #f6d55c; color: #2b2b2b; font: 700 12px/1.5 sans-serif; text-align: center; }</style>\n')
    html = once(html, "  </head>", style + "  </head>")
    if proposal:
        script = ('    <script>\n'
                  '      // RC1 proposal: a phone turned to landscape brings the whole game frame into view.\n'
                  '      (function () {\n'
                  '        var frame = document.querySelector(".unity-frame");\n'
                  '        var coarse = window.matchMedia && window.matchMedia("(pointer: coarse)").matches;\n'
                  '        function fit() {\n'
                  '          if (coarse && window.innerWidth > window.innerHeight && frame) frame.scrollIntoView({ block: "center" });\n'
                  '        }\n'
                  '        window.addEventListener("orientationchange", function () { setTimeout(fit, 350); });\n'
                  '      })();\n'
                  '    </script>\n')
        html = once(html, "  </body>", script + "  </body>")
    return html


os.makedirs(OUT, exist_ok=True)
for name, proposal in (("index.html", False), ("index-mobilefix.html", True)):
    with open(os.path.join(OUT, name), "w", encoding="utf-8", newline="\n") as f:
        f.write(page(proposal))
print("staging pages written to", OUT)
