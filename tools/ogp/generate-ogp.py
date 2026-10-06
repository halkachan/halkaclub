"""SNSにURLを貼ったときに出るカード画像（1200x630）をまとめて作ります。

必要なもの: Python + Pillow + fontTools + brotli
    py -m pip install pillow fonttools brotli
使いかた:
    py tools/ogp/generate-ogp.py
出力先: assets/ogp/

ノート調のカードはサイト同梱の Yomogi（assets/fonts）を使います。
うたまぜ！だけはページの見た目に合わせて、暗い背景＋ミントで作ります。
"""

from __future__ import annotations

import sys
import tempfile
from dataclasses import dataclass, field
from pathlib import Path

from PIL import Image, ImageDraw, ImageFont

W, H = 1200, 630

# サイト共通（ノート調）
PAPER = (255, 254, 246)
INK = (22, 21, 15)
YELLOW = (255, 220, 24)
RULE = (76, 133, 171)
RED_LINE = (232, 84, 84)

# うたまぜ！
UTA_BG = (18, 19, 22)
UTA_MINT = (99, 215, 182)
UTA_WHITE = (255, 255, 255)
UTA_MUTED = (123, 128, 136)

ROOT = Path(__file__).resolve().parents[2]
OUT = ROOT / "assets" / "ogp"

WINDOWS_FONTS = [
    (r"C:\Windows\Fonts\YuGothB.ttc", 0),
    (r"C:\Windows\Fonts\YuGothM.ttc", 0),
    (r"C:\Windows\Fonts\meiryob.ttc", 0),
    (r"C:\Windows\Fonts\meiryo.ttc", 0),
]


@dataclass
class Card:
    name: str
    kicker: str
    title: str
    lines: list[str]
    badge: str | None = None
    art: str | None = None          # 右に置く絵（リポジトリからの相対パス）
    art_pixel: bool = True          # ドット絵として拡大するか
    tape_right: str = "halkaclub.com"
    tape_left: str = "HALKA"
    style: str = "note"             # note / utamaze
    subtitle: str = ""
    chips: list[str] = field(default_factory=list)


CARDS = [
    Card("home", "LINKS", "HALKA",
         ["歌ってみた・オリジナル曲・イラスト・ゲーム。", "HALKAの入口です。"],
         art="assets/profile/halgif1.gif", tape_left="HALKA LINKS"),
    Card("works", "WORKS ARCHIVE", "作品一覧",
         ["歌ってみた、ひらがな一文字シリーズ、", "方向シリーズ、そのほか。"],
         art="assets/profile/halgif1.gif", tape_left="HALKA WORKS"),
    Card("club", "HARUKA CLUB", "はるかくらぶ",
         ["ステムデータ、デモ音源、制作資料。", "支援してくれる方への置き場です。"],
         art="assets/profile/halgif1.gif", tape_left="HARUKA CLUB"),
    Card("game", "GAMES", "ゲーム集",
         ["ブラウザでそのまま遊べる、", "ゲームとはいえないような物たち。"],
         art="assets/profile/halgif1.gif", tape_left="HALKA GAMES"),
    Card("halkaworld", "HALKA WORLD", "HALKA WORLD",
         ["ﾊﾙｶﾁｬﾝの小さな世界を歩く、", "ブラウザで遊べるゲームです。"],
         art="unity-project/Assets/Content/Character/front_idle/00.png",
         tape_left="HALKA WORLD"),
    Card("commission", "COMMISSION", "依頼について",
         ["歌ってみたMIX・サムネイラスト・インスト制作",
          "オリジナル曲制作・HALKAへの歌唱依頼"],
         badge="ご相談受付中", art="assets/profile/halgif1.gif",
         tape_left="HALKA COMMISSION"),
    Card("commission-en", "COMMISSION", "Commissions",
         ["Vocal cover mixing / Thumbnail illustration",
          "Instrumental & original song production"],
         badge="Open for inquiries", art="assets/profile/halgif1.gif",
         tape_left="HALKA COMMISSION"),
    Card("utamaze", "VOCAL MIXING PLUGIN", "うたまぜ！",
         [], style="utamaze", subtitle="歌のMIXを、もっとわかりやすく。",
         chips=["Windows", "VST3", "FREE + PRO"],
         art="utamaze/assets/utamaze-ui.png", art_pixel=False),
]


class Yomogi:
    """
    同梱の Yomogi を描画に使えるようにします。
    Yomogi は字ごとに細かく分かれた woff2 で配られていて、
    **半角カナ（ﾊﾙｶﾁｬﾝ など）は「japanese」には入っていません**。
    足りない字は、入っているサブセットを探して補います。
    """

    MAIN = ("yomogi-japanese-400-normal", "yomogi-latin-400-normal")

    def __init__(self, workdir: Path, needed: str):
        self.workdir = workdir
        self.source = ROOT / "assets" / "fonts" / "files"
        self.files: dict[str, Path] = {}
        self.cmaps: dict[str, set[int]] = {}
        self.cache: dict[tuple[str, int], ImageFont.FreeTypeFont] = {}
        self.order: list[str] = []

        for name in self.MAIN:
            self._add(name)
        for name in self._subsets_for(needed):
            self._add(name)

    def _add(self, name: str) -> None:
        from fontTools.ttLib import TTFont

        font = TTFont(self.source / f"{name}.woff2")
        font.flavor = None
        target = self.workdir / f"{name}.ttf"
        font.save(target)
        self.files[name] = target
        self.cmaps[name] = set(font.getBestCmap())
        self.order.append(name)

    def _subsets_for(self, needed: str) -> list[str]:
        """主フォントに無い字を含むサブセットだけを選びます。"""
        from fontTools.ttLib import TTFont

        missing = {ord(c) for c in needed
                   if not any(ord(c) in self.cmaps[name] for name in self.MAIN)}
        missing -= {0x20, 0x0A}   # 空白と改行は無視します
        if not missing:
            return []

        picked = []
        for path in sorted(self.source.glob("yomogi-*-400-normal.woff2")):
            name = path.stem
            if name in self.MAIN:
                continue
            try:
                cmap = set(TTFont(path).getBestCmap())
            except Exception:
                continue
            if cmap & missing:
                picked.append(name)
                missing -= cmap
            if not missing:
                break
        return picked

    def font(self, name: str, size: int) -> ImageFont.FreeTypeFont:
        key = (name, size)
        if key not in self.cache:
            self.cache[key] = ImageFont.truetype(str(self.files[name]), size)
        return self.cache[key]

    def _name_for(self, char: str) -> str:
        code = ord(char)
        for name in self.order:
            if code in self.cmaps[name]:
                return name
        return self.MAIN[0]

    def draw(self, draw: ImageDraw.ImageDraw, xy, text: str, size: int, fill) -> None:
        x, y = xy
        chunk, current = "", None
        for char in text:
            name = self._name_for(char)
            if current is None or name == current:
                chunk += char
                current = name
            else:
                font = self.font(current, size)
                draw.text((x, y), chunk, font=font, fill=fill)
                x += draw.textlength(chunk, font=font)
                chunk, current = char, name
        if chunk and current is not None:
            draw.text((x, y), chunk, font=self.font(current, size), fill=fill)

    def width(self, draw: ImageDraw.ImageDraw, text: str, size: int) -> float:
        total = 0.0
        for char in text:
            total += draw.textlength(char, font=self.font(self._name_for(char), size))
        return total

    def latin(self, size: int) -> ImageFont.FreeTypeFont:
        return self.font(self.MAIN[1], size)


def windows_font(size: int) -> ImageFont.FreeTypeFont:
    for path, index in WINDOWS_FONTS:
        if Path(path).exists():
            return ImageFont.truetype(path, size, index=index)
    raise SystemExit("日本語のフォントが見つかりません（Yu Gothic か Meiryo が要ります）。")


def blend(base, color, alpha):
    return tuple(round(b + (c - b) * alpha) for b, c in zip(base, color))


def spaced(draw, xy, text, font, fill, extra=6):
    x, y = xy
    for char in text:
        draw.text((x, y), char, font=font, fill=fill)
        x += draw.textlength(char, font=font) + extra
    return x


def load_art(relative: str, size: int, pixel: bool) -> Image.Image:
    image = Image.open(ROOT / relative)
    if getattr(image, "is_animated", False):
        image.seek(0)
    image = image.convert("RGBA")
    if pixel:
        return image.resize((size, size), Image.NEAREST)
    ratio = size / image.width
    return image.resize((size, round(image.height * ratio)), Image.LANCZOS)


def draw_note_card(card: Card, fonts: "Yomogi") -> Image.Image:
    image = Image.new("RGB", (W, H), PAPER)
    draw = ImageDraw.Draw(image)

    rule = blend(PAPER, RULE, 0.12)
    y = 96
    while y < H:
        draw.line([(0, y), (W, y)], fill=rule, width=2)
        y += 54
    draw.line([(78, 0), (78, H)], fill=blend(PAPER, RED_LINE, 0.22), width=2)

    tape = Image.new("RGBA", (W + 160, 108), (0, 0, 0, 0))
    tape_draw = ImageDraw.Draw(tape)
    tape_draw.rectangle([0, 18, W + 160, 96], fill=YELLOW + (255,))
    tape_font = fonts.latin(26)
    spaced(tape_draw, (116, 38), card.tape_left, tape_font, INK)
    right_width = sum(tape_draw.textlength(c, font=tape_font) + 6 for c in card.tape_right)
    spaced(tape_draw, (W + 30 - right_width, 38), card.tape_right, tape_font, INK)
    tape = tape.rotate(0.9, resample=Image.BICUBIC, expand=False)
    image.paste(tape, (-80, 24), tape)

    kicker_font = fonts.latin(28)

    kx, ky = 118, 196
    kw = draw.textlength(card.kicker, font=kicker_font)
    draw.rectangle([kx - 12, ky - 6, kx + kw + 12, ky + 40], fill=YELLOW)
    draw.text((kx, ky), card.kicker, font=kicker_font, fill=INK)

    fonts.draw(draw, (114, 252), card.title, 92, INK)

    ly = 392
    for line in card.lines:
        fonts.draw(draw, (118, ly), line, 32, INK)
        ly += 50

    if card.badge:
        bx, by = 118, 510
        bw = fonts.width(draw, card.badge, 28)
        draw.rounded_rectangle([bx, by, bx + bw + 56, by + 56], radius=28,
                               fill=YELLOW, outline=INK, width=3)
        fonts.draw(draw, (bx + 28, by + 10), card.badge, 28, INK)

    if card.art:
        art = load_art(card.art, 288, card.art_pixel)
        image.paste(art, (846, 206), art)
    return image


def draw_utamaze_card(card: Card) -> Image.Image:
    image = Image.new("RGB", (W, H), UTA_BG)
    draw = ImageDraw.Draw(image)

    if card.art:
        shot = load_art(card.art, 620, False)
        panel = Image.new("RGB", (shot.width, min(shot.height, 470)), UTA_BG)
        panel.paste(shot.convert("RGB"), (0, 0))
        image.paste(panel, (620, 96))
        draw.rounded_rectangle([620, 96, 620 + panel.width - 1, 96 + panel.height - 1],
                               radius=14, outline=(44, 47, 52), width=2)

    kicker_font = windows_font(24)
    title_font = windows_font(96)
    sub_font = windows_font(34)
    chip_font = windows_font(22)

    draw.ellipse([74, 150, 94, 170], fill=UTA_MINT)
    draw.text((108, 146), card.kicker, font=kicker_font, fill=UTA_MINT)

    draw.text((72, 212), card.title, font=title_font, fill=UTA_WHITE)
    draw.text((78, 340), "Utamaze!", font=sub_font, fill=UTA_MUTED)
    draw.text((74, 410), card.subtitle, font=sub_font, fill=UTA_WHITE)

    x = 74
    for chip in card.chips:
        width = draw.textlength(chip, font=chip_font) + 36
        draw.rounded_rectangle([x, 492, x + width, 540], radius=24,
                               outline=(60, 64, 70), width=2)
        draw.text((x + 18, 500), chip, font=chip_font, fill=UTA_MUTED)
        x += width + 12

    draw.text((74, 574), "halkaclub.com/utamaze/", font=chip_font, fill=UTA_MUTED)
    return image


def main() -> None:
    OUT.mkdir(parents=True, exist_ok=True)
    needed = "".join(card.title + "".join(card.lines) + (card.badge or "")
                     for card in CARDS if card.style != "utamaze")
    with tempfile.TemporaryDirectory() as temp:
        fonts = Yomogi(Path(temp), needed)
        for card in CARDS:
            image = draw_utamaze_card(card) if card.style == "utamaze" else draw_note_card(card, fonts)
            path = OUT / f"{card.name}.png"
            image.save(path, "PNG", optimize=True)
            print(f"{path.relative_to(ROOT)}  {path.stat().st_size:,} bytes")


if __name__ == "__main__":
    sys.exit(main())
