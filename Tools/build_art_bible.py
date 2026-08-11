from pathlib import Path
from PIL import Image, ImageDraw, ImageFont


ROOT = Path(__file__).resolve().parents[1]
SOURCE = ROOT / "Assets/Art/SourceArt/AI_Concept"
OUTPUT = ROOT / "Assets/Art/ArtBible"

SCENE_1 = SOURCE / "CS_Scene01_SummerPalace_v2.png"
SCENE_2 = SOURCE / "CS_Scene02_CinematicCanvas_SummerPalace_v3.png"
SCENE_3 = SOURCE / "CS_Scene03_IconicCities_SummerPalace_v4.png"

FONT_EN = "/System/Library/Fonts/Avenir Next.ttc"
FONT_ZH = "/System/Library/Fonts/Hiragino Sans GB.ttc"

BG = (9, 14, 17)
CARD = (18, 25, 29)
CARD_2 = (23, 31, 35)
LINE = (51, 62, 65)
TEXT = (236, 235, 225)
MUTED = (151, 163, 165)
GOLD = (224, 169, 67)
FOG = (111, 139, 154)
CHARCOAL = (29, 37, 42)
GREEN = (31, 70, 61)
VERMILION = (116, 51, 41)
AZURE = (47, 85, 103)
IVORY = (224, 211, 182)
CITY_WHITE = (191, 205, 211)


def en(size, weight="regular"):
    index = {"bold": 0, "demi": 2, "medium": 5, "regular": 7, "heavy": 8}.get(weight, 7)
    return ImageFont.truetype(FONT_EN, size, index=index)


def zh(size):
    return ImageFont.truetype(FONT_ZH, size)


def cover(image, size, focus=(0.5, 0.5)):
    image = image.convert("RGB")
    width, height = size
    scale = max(width / image.width, height / image.height)
    resized = image.resize(
        (round(image.width * scale), round(image.height * scale)),
        Image.Resampling.LANCZOS,
    )
    left = round((resized.width - width) * focus[0])
    top = round((resized.height - height) * focus[1])
    left = max(0, min(left, resized.width - width))
    top = max(0, min(top, resized.height - height))
    return resized.crop((left, top, left + width, top + height))


def contain(image, size, background=BG):
    image = image.convert("RGB")
    width, height = size
    scale = min(width / image.width, height / image.height)
    resized = image.resize(
        (round(image.width * scale), round(image.height * scale)),
        Image.Resampling.LANCZOS,
    )
    result = Image.new("RGB", size, background)
    result.paste(resized, ((width - resized.width) // 2, (height - resized.height) // 2))
    return result


def add_vertical_gradient(image, box, top_alpha=0, bottom_alpha=210):
    x0, y0, x1, y1 = box
    overlay = Image.new("RGBA", image.size, (0, 0, 0, 0))
    d = ImageDraw.Draw(overlay)
    height = max(1, y1 - y0)
    for y in range(y0, y1):
        t = (y - y0) / height
        alpha = round(top_alpha + (bottom_alpha - top_alpha) * t)
        d.line((x0, y, x1, y), fill=(5, 8, 10, alpha))
    image.alpha_composite(overlay)


def wrap_text(draw, text, font, max_width):
    lines = []
    for paragraph in text.split("\n"):
        if not paragraph:
            lines.append("")
            continue
        current = ""
        for char in paragraph:
            candidate = current + char
            if current and draw.textbbox((0, 0), candidate, font=font)[2] > max_width:
                lines.append(current)
                current = char
            else:
                current = candidate
        if current:
            lines.append(current)
    return lines


def draw_wrapped(draw, xy, text, font, fill, max_width, line_gap=10, max_lines=None):
    x, y = xy
    lines = wrap_text(draw, text, font, max_width)
    if max_lines:
        lines = lines[:max_lines]
    bbox = draw.textbbox((0, 0), "Ag国", font=font)
    line_height = bbox[3] - bbox[1] + line_gap
    for line in lines:
        draw.text((x, y), line, font=font, fill=fill)
        y += line_height
    return y


def panel(draw, box, radius=22, fill=CARD, outline=LINE, width=2):
    draw.rounded_rectangle(box, radius=radius, fill=fill, outline=outline, width=width)


def label(draw, xy, text, color=GOLD):
    x, y = xy
    draw.text((x, y), text.upper(), font=zh(27), fill=color)


def bullet_list(draw, x, y, items, max_width, font_size=29, gap=18, bullet_color=GOLD):
    body = zh(font_size)
    for item in items:
        draw.ellipse((x, y + 13, x + 9, y + 22), fill=bullet_color)
        y = draw_wrapped(draw, (x + 26, y), item, body, TEXT, max_width - 26, line_gap=8) + gap
    return y


def scene_card(canvas, image, box, number, cn_title, en_title, caption, ratio_text):
    x0, y0, x1, y1 = box
    card = cover(image, (x1 - x0, y1 - y0), focus=(0.5, 0.5)).convert("RGBA")
    canvas.alpha_composite(card, (x0, y0))
    add_vertical_gradient(canvas, (x0, y0 + (y1 - y0) // 2, x1, y1), 0, 225)
    d = ImageDraw.Draw(canvas)
    d.rounded_rectangle(box, radius=22, outline=(95, 104, 104), width=2)
    d.text((x0 + 34, y0 + 28), f"0{number}", font=en(28, "demi"), fill=GOLD)
    d.text((x0 + 34, y1 - 145), cn_title, font=zh(43), fill=TEXT)
    d.text((x0 + 34, y1 - 93), en_title.upper(), font=en(22, "demi"), fill=(207, 210, 205))
    d.text((x1 - 34, y1 - 93), ratio_text, font=en(21, "demi"), fill=GOLD, anchor="ra")
    d.text((x0 + 34, y1 - 54), caption, font=zh(25), fill=(205, 209, 202))


def build_color_script(scene_1, scene_2, scene_3):
    canvas = Image.new("RGBA", (3840, 1080), BG + (255,))
    d = ImageDraw.Draw(canvas)

    d.text((88, 48), "SUMMER PALACE OSMANTHUS VR", font=en(45, "bold"), fill=TEXT)
    d.text((88, 103), "三幕色彩脚本 / THREE-SCENE COLOR SCRIPT", font=zh(27), fill=MUTED)
    d.text(
        (3750, 62),
        "气味唤起 → 记忆接管 → 身份重组",
        font=zh(27),
        fill=IVORY,
        anchor="ra",
    )
    d.line((88, 151, 3752, 151), fill=LINE, width=2)

    margin = 88
    gap = 24
    panel_width = (3840 - margin * 2 - gap * 2) // 3
    top = 184
    bottom = 872
    panels = [
        (scene_1, 1, "进入记忆", "Entering Memory", "冷、远、残缺；桂花是唯一暖色", "85% COOL / 15% WARM"),
        (scene_2, 2, "影像接管", "Cinematic Memory", "环境压暗；一张连续电影画布展开", "65% DARK COOL / 35% WARM"),
        (scene_3, 3, "重组身份", "Reconstructed Identity", "颐和园长廊与四座城市记忆共存", "55% WARM / 45% COOL"),
    ]
    for i, data in enumerate(panels):
        x0 = margin + i * (panel_width + gap)
        scene_card(canvas, data[0], (x0, top, x0 + panel_width, bottom), *data[1:])

    footer_y = 923
    d.text((88, footer_y), "VISUAL ARC", font=en(22, "demi"), fill=GOLD)
    bar_x = 280
    bar_y = footer_y + 3
    bar_w = 2420
    d.rounded_rectangle((bar_x, bar_y, bar_x + bar_w, bar_y + 24), radius=12, fill=CHARCOAL)
    segments = [
        (0.00, 0.32, FOG),
        (0.32, 0.66, (108, 77, 48)),
        (0.66, 1.00, GOLD),
    ]
    for start, end, color in segments:
        d.rounded_rectangle(
            (bar_x + round(bar_w * start), bar_y, bar_x + round(bar_w * end), bar_y + 24),
            radius=12,
            fill=color,
        )
    d.text((2745, footer_y - 5), "冷雾蓝灰", font=zh(23), fill=FOG)
    d.text((2925, footer_y - 5), "→", font=zh(27), fill=MUTED)
    d.text((2980, footer_y - 5), "局部桂花金", font=zh(23), fill=(197, 154, 88))
    d.text((3190, footer_y - 5), "→", font=zh(27), fill=MUTED)
    d.text((3245, footer_y - 5), "暖金 × 城市冷白", font=zh(23), fill=IVORY)

    d.text(
        (88, 1007),
        "核心判断：不是回到过去，而是让过去、距离和当下在同一空间里重新组合。",
        font=zh(29),
        fill=TEXT,
    )
    d.text((3752, 1007), "ART DIRECTION v1.0", font=en(22, "demi"), fill=MUTED, anchor="ra")

    path = OUTPUT / "OSMANTHUS_VR_COLOR_SCRIPT_v1.png"
    canvas.convert("RGB").save(path, quality=96)
    return path


def build_art_bible(scene_1, scene_2, scene_3):
    canvas = Image.new("RGBA", (3840, 2160), BG + (255,))
    d = ImageDraw.Draw(canvas)

    d.text((120, 54), "SUMMER PALACE OSMANTHUS VR", font=en(64, "bold"), fill=TEXT)
    d.text((120, 128), "ART BIBLE · v1.0", font=en(31, "demi"), fill=GOLD)
    d.text(
        (820, 122),
        "被气味唤起的文化记忆：因距离而碎片化，在当下持续重组。",
        font=zh(31),
        fill=IVORY,
    )
    d.text((3718, 70), "META QUEST · URP · 4 MIN", font=en(25, "demi"), fill=MUTED, anchor="ra")
    d.text((3718, 118), "MEMORY / DISTANCE / PRESENT", font=en(22, "medium"), fill=(112, 129, 132), anchor="ra")
    d.line((120, 188, 3720, 188), fill=LINE, width=2)

    hero_y0, hero_y1 = 230, 850
    gap = 28
    hero_w = (3600 - gap * 2) // 3
    hero_data = [
        (scene_1, 1, "进入记忆", "ENTERING MEMORY", "冷雾 / 残缺 / 稀疏桂花", "85 / 15"),
        (scene_2, 2, "影像接管", "CINEMATIC MEMORY", "压暗环境 / 单一画布 / 视频平面", "65 / 35"),
        (scene_3, 3, "重组身份", "RECONSTRUCTED IDENTITY", "长廊 × 四城 / 暖冷共存", "55 / 45"),
    ]
    for i, data in enumerate(hero_data):
        x0 = 120 + i * (hero_w + gap)
        scene_card(canvas, data[0], (x0, hero_y0, x0 + hero_w, hero_y1), *data[1:])

    left = (120, 900, 1135, 2065)
    middle = (1165, 900, 2425, 2065)
    right = (2455, 900, 3720, 2065)
    panel(d, left)
    panel(d, middle)
    panel(d, right)

    # Left column: core idea, palette, shape language.
    lx = left[0] + 34
    label(d, (lx, 936), "Core Idea / 核心命题")
    draw_wrapped(
        d,
        (lx, 982),
        "旧世界不是历史复原，而是一段由气味触发、被距离打散，并与当下生活重新组合的文化记忆。",
        zh(32),
        TEXT,
        left[2] - left[0] - 68,
        line_gap=12,
    )
    d.line((lx, 1140, left[2] - 34, 1140), fill=LINE, width=2)
    label(d, (lx, 1172), "Color System / 色彩系统")
    swatches = [
        ("冷雾蓝灰", FOG, "#6F8B9A"),
        ("炭黑木影", CHARCOAL, "#1D252A"),
        ("长廊青绿", GREEN, "#1F463D"),
        ("暗朱红", VERMILION, "#743329"),
        ("彩画青蓝", AZURE, "#2F5567"),
        ("桂花金", GOLD, "#E0A943"),
        ("夕照米白", IVORY, "#E0D3B6"),
        ("城市冷白", CITY_WHITE, "#BFCED3"),
    ]
    sx, sy = lx, 1225
    sw = 437
    for i, (name, color, code) in enumerate(swatches):
        col, row = i % 2, i // 2
        x = sx + col * 475
        y = sy + row * 83
        d.rounded_rectangle((x, y, x + 72, y + 54), radius=9, fill=color, outline=(90, 98, 98), width=1)
        d.text((x + 88, y - 1), name, font=zh(25), fill=TEXT)
        d.text((x + 88, y + 31), code, font=en(18, "medium"), fill=MUTED)
    d.line((lx, 1572, left[2] - 34, 1572), fill=LINE, width=2)
    label(d, (lx, 1604), "Shape Language / 形状语言")
    d.text((lx, 1660), "OLD WORLD", font=en(20, "demi"), fill=MUTED)
    d.line((lx + 190, 1674, lx + 850, 1674), fill=GREEN, width=17)
    for x in range(lx + 220, lx + 850, 80):
        d.line((x, 1650, x, 1704), fill=VERMILION, width=7)
    d.text((lx, 1737), "低矮、水平、重复开间", font=zh(24), fill=TEXT)
    d.text((lx, 1790), "PRESENT", font=en(20, "demi"), fill=MUTED)
    for i, height in enumerate((60, 110, 78, 136, 92)):
        x = lx + 210 + i * 86
        d.rectangle((x, 1855 - height, x + 38, 1855), fill=CITY_WHITE)
    d.text((lx, 1872), "垂直、标志性、局部残片", font=zh(24), fill=TEXT)
    d.text((lx, 1924), "SCENT", font=en(20, "demi"), fill=MUTED)
    points = [(lx + 205, 1981), (lx + 320, 1948), (lx + 455, 2005), (lx + 610, 1962), (lx + 835, 1989)]
    d.line(points, fill=GOLD, width=8, joint="curve")
    for x, y in points:
        d.ellipse((x - 7, y - 7, x + 7, y + 7), fill=IVORY)
    d.text((lx, 2010), "曲线、漂浮、连接与导航", font=zh(24), fill=TEXT)

    # Middle column: architecture and interaction.
    mx = middle[0] + 34
    label(d, (mx, 936), "Architecture DNA / 长廊识别")
    arch = cover(scene_1, (middle[2] - middle[0] - 68, 300), focus=(0.5, 0.25)).convert("RGBA")
    canvas.alpha_composite(arch, (mx, 982))
    d.rounded_rectangle((mx, 982, middle[2] - 34, 1282), radius=18, outline=(89, 98, 99), width=2)
    d.rectangle((mx, 1234, middle[2] - 34, 1282), fill=(7, 11, 13, 205))
    d.text((mx + 20, 1244), "低屋面 · 绿细柱 · 红低栏 · 苏式彩画 · 连续开间", font=zh(24), fill=TEXT)
    bullet_list(
        d,
        mx,
        1317,
        [
            "以可重复的标准开间为主体，亭子只作为次要节点。",
            "先建立颐和园识别度，再通过缺失开间、断屋面和雾恢复碎片感。",
            "近景保留彩画；中远景简化为色带和轮廓，适配 Quest。",
        ],
        middle[2] - middle[0] - 68,
        font_size=27,
        gap=12,
    )
    d.line((mx, 1538, middle[2] - 34, 1538), fill=LINE, width=2)
    label(d, (mx, 1570), "Memory Interaction / 影像交互")
    interaction = cover(scene_2, (550, 330), focus=(0.56, 0.46)).convert("RGBA")
    canvas.alpha_composite(interaction, (mx, 1616))
    d.rounded_rectangle((mx, 1616, mx + 550, 1946), radius=18, outline=(89, 98, 99), width=2)
    ix = mx + 585
    bullet_list(
        d,
        ix,
        1620,
        [
            "触摸柱体后，周边环境先压暗。",
            "只展开一张连续、无边框、轻微弯曲的视频画布。",
            "画布是单面 Mesh + 视频材质，不把影像内容做成 3D。",
            "桂花连接触点与画布前缘；结束后画布溶解，环境恢复。",
        ],
        middle[2] - ix - 34,
        font_size=26,
        gap=11,
    )

    # Right column: reconstruction, materials, guardrails.
    rx = right[0] + 34
    label(d, (rx, 936), "Reconstruction / 重组世界")
    recon = cover(scene_3, (right[2] - right[0] - 68, 370), focus=(0.52, 0.46)).convert("RGBA")
    canvas.alpha_composite(recon, (rx, 982))
    d.rounded_rectangle((rx, 982, right[2] - 34, 1352), radius=18, outline=(89, 98, 99), width=2)
    city_names = ["HONG KONG", "PARIS", "LONDON", "NEW YORK"]
    city_width = 272
    for i, city in enumerate(city_names):
        x = rx + i * (city_width + 18)
        d.rounded_rectangle((x, 1380, x + city_width, 1435), radius=12, fill=CARD_2, outline=LINE, width=1)
        d.text((x + city_width / 2, 1394), city, font=en(18, "demi"), fill=IVORY, anchor="ma")
    bullet_list(
        d,
        rx,
        1470,
        [
            "香港斜向几何、埃菲尔铁塔、伦敦钟楼、帝国大厦提供城市记忆锚点。",
            "地标必须与长廊互相穿插、支撑和打断；不能排成旅游海报式 skyline。",
            "最终空间既不是完整颐和园，也不是普通现代城市；保留未拼合边缘。",
        ],
        right[2] - right[0] - 68,
        font_size=27,
        gap=13,
    )
    d.line((rx, 1708, right[2] - 34, 1708), fill=LINE, width=2)
    label(d, (rx, 1740), "Material + VFX / 材质与特效")
    material_items = [
        ("OPAQUE", "木构、栏杆、屋面", GREEN),
        ("EMISSION", "彩画触点、桂花、城市冷光", GOLD),
        ("TRANSPARENT", "仅视频画布、雾片、少量记忆层", CITY_WHITE),
        ("WATER", "简化渐变与倒影剪影，不做写实反射", AZURE),
    ]
    y = 1790
    for tag, desc, color in material_items:
        d.rounded_rectangle((rx, y, rx + 175, y + 49), radius=11, fill=color)
        tag_color = (10, 15, 18) if color in (GOLD, CITY_WHITE, IVORY) else TEXT
        d.text((rx + 87, y + 13), tag, font=en(16, "bold"), fill=tag_color, anchor="ma")
        d.text((rx + 198, y + 8), desc, font=zh(23), fill=TEXT)
        y += 65

    # Bottom guardrail strip.
    guard_y = 2092
    d.line((120, guard_y, 3720, guard_y), fill=LINE, width=2)
    d.text((120, 2111), "QUEST GUARDRAILS", font=en(19, "demi"), fill=GOLD)
    d.text(
        (370, 2107),
        "共享材质 · 512/1K 纹理 · 透明层 ≤ 3 · 少量 Quad 粒子 · 不依赖 Bloom · Scene 3 分批显现 · 目标 72 FPS",
        font=zh(23),
        fill=(199, 205, 200),
    )
    d.text((3720, 2110), "NOT PERFUME ADVERTISING", font=en(18, "demi"), fill=MUTED, anchor="ra")

    path = OUTPUT / "OSMANTHUS_VR_ART_BIBLE_v1.png"
    canvas.convert("RGB").save(path, quality=96)
    return path


def main():
    OUTPUT.mkdir(parents=True, exist_ok=True)
    scene_1 = Image.open(SCENE_1)
    scene_2 = Image.open(SCENE_2)
    scene_3 = Image.open(SCENE_3)
    color_script = build_color_script(scene_1, scene_2, scene_3)
    art_bible = build_art_bible(scene_1, scene_2, scene_3)
    print(color_script)
    print(art_bible)


if __name__ == "__main__":
    main()
