# Stylized Celadon Tea Set

Blender-first Unity/Quest asset inspired by Longquan celadon.

## Contents

- Teapot body with tapered spout and loop handle
- Independent teapot lid and knob
- Two independent tea cups with tea surfaces
- Shallow celadon serving tray

All dimensions use meters. Interaction roots are separated for the teapot, lid, cups, and tray, with pivots placed at their local bases.

The Blender material is a presentation reference. Recreate it in Unity URP using a jade-green base color, moderate smoothness, subtle coat/specular, and a low-strength color variation or crackle mask if desired.

## Regeneration

```bash
/Applications/Blender.app/Contents/MacOS/Blender --background --python ArtSource/CeladonTeaSet/create_celadon_tea_set.py
```

