"""Make a labelled contact sheet from the seven Quest garden previews."""

from pathlib import Path
from PIL import Image, ImageDraw, ImageFont


ROOT = Path("/Users/kellyjia/Osmanthus/Assets/Osmanthus/Art/GardenPlanting/Previews")
NAMES = [
    "01_Osmanthus_Tree",
    "02_Taihu_Standing_Stone",
    "03_Weeping_Willow",
    "04_Rockery_Cluster",
    "05_Flowering_Shrub_Clump",
    "06_Bamboo_Clump",
    "07_Water_Edge_Clump",
]
CELL = 420
LABEL = 44
MARGIN = 16
COLS = 4
ROWS = 2

sheet = Image.new("RGB", (COLS * CELL, ROWS * (CELL + LABEL)), (24, 27, 24))
draw = ImageDraw.Draw(sheet)
try:
    font = ImageFont.truetype("/System/Library/Fonts/Helvetica.ttc", 20)
except OSError:
    font = ImageFont.load_default()

for index, name in enumerate(NAMES):
    image = Image.open(ROOT / f"{name}_Quest_Preview.png").convert("RGB")
    image.thumbnail((CELL - MARGIN * 2, CELL - MARGIN * 2), Image.Resampling.LANCZOS)
    col, row = index % COLS, index // COLS
    x = col * CELL + (CELL - image.width) // 2
    y = row * (CELL + LABEL) + (CELL - image.height) // 2
    sheet.paste(image, (x, y))
    label = name.replace("_", " ")
    box = draw.textbbox((0, 0), label, font=font)
    tx = col * CELL + (CELL - (box[2] - box[0])) // 2
    ty = row * (CELL + LABEL) + CELL + 8
    draw.text((tx, ty), label, fill=(235, 235, 225), font=font)

output = ROOT / "Garden_Assets_Quest_Previews.png"
sheet.save(output, optimize=True)
print(output)
