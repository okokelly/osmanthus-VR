# Stylized Long Corridor + Octagonal Pavilion

This asset is a Blender-first blockout for the Osmanthus VR Unity project.

## Scope

- One double-eaved octagonal pavilion at the center
- Three 2.49 m corridor bays on each side
- 2.28 m corridor width and 2.52 m column height
- Walk-through scale for a VR player
- Simplified close-readable roof tiles, painted beam panels, seated rails, and pavilion brackets
- Three named interactive pillars: `Pillar_Interactive_01`, `02`, and `03`

## Art direction

The model follows the project visual plan: low-saturation green, dark vermilion, fog blue, teal, and osmanthus gold. It is a fragmented cultural-memory interpretation, not a conservation reconstruction.

## Unity target

- Unity 6000 / URP
- Meta Quest standalone VR
- FBX scale: 1 unit = 1 meter
- Forward: `-Z`, up: `Y`
- Use the source `.blend` for edits and the exported FBX under `Assets/Art/Models/LongCorridor/` for Unity.

The repeated tile ribs and bracket pieces are deliberately sparse. Before final Quest deployment, combine static geometry by material or use Unity static batching after interaction objects have been separated.

## Regeneration

Run Blender in background mode:

```bash
/Applications/Blender.app/Contents/MacOS/Blender --background --python ArtSource/LongCorridor/create_long_corridor.py
```

