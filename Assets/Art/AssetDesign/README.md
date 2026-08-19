# Asset Design — First Production Batch

These sheets translate the approved Art Bible into buildable assets. They are design references, not final Unity textures or baked orthographic drawings.

## Design sheets

- `AD_01_Corridor_ModularKit_v1.png` — repeatable Summer Palace Long Corridor bay, isolated modules, intact and fragmented variants.
- `AD_02_MemoryPillar_Interaction_v1.png` — shared pillar geometry, dormant/hover/activated states, emission studies, and one continuous cinematic canvas.
- `AD_03_Reconstruction_Fragments_v1.png` — garden fragments plus recognizable Hong Kong, Paris, London, and New York city fragments.
- `AD_04_Pillar_ContentVariants_v1.png` — Place, Everyday, and Distance/Present pillar skins, memory fragments, and particle behaviors.
- `AD_05_Corridor_InteriorSystem_v1.png` — initial interior ceiling/rafter system study.
- `AD_05_Corridor_InteriorSystem_v2.png` — revised production direction with one consistent three-panel mirrored rectangular lattice system across upper friezes and lower railings.
- `MeshyMultiView/ENV_Corridor_CanopyBay_A/` — four corrected multi-view source images for one connected upper-canopy bay; side beams have geometric painting only, scenic paintings remain on transverse beams, and the roof uses a shallow curved profile.
- `PROMPTS.md` — full GPT Image prompt set used to generate this batch.

## Recommended blockout scale

- Standard corridor bay width: `2.6 m`
- Walkable floor depth: `2.4 m`
- Floor slab thickness: `0.12 m`
- Pillar visible height to beam: `3.0 m`
- Pillar section: `0.28 x 0.28 m`
- Railing height: `0.85 m`
- Roof/eave total depth: approximately `3.2 m`
- Cinematic canvas: single-sided mesh, approximately `4.8 x 2.1 m`, adjustable after VR readability testing

Treat these as consistent production targets rather than claims of exact historic dimensions.

## Model and material split

```text
ENV_Corridor_Floor_A
ENV_Corridor_Pillar_A
ENV_Corridor_Beam_A
ENV_Corridor_Railing_A
ENV_Corridor_Roof_A
ENV_Corridor_Roof_Fragment_A
ENV_Corridor_CeilingBay_A
ENV_Corridor_RafterStrip_A
ENV_Corridor_PaintedBeam_A
ENV_Corridor_PaintedBeam_B
ENV_Corridor_PaintedBeam_C
ENV_Corridor_UpperLattice_A
ENV_Corridor_BeamJoint_A
ENV_Corridor_CornerBracket_A
ENV_Corridor_CurveBay_A

PROP_MemoryPillar_Base
PROP_MemoryCanvas_A
FRAG_Place_Set_A
FRAG_Everyday_Set_A
FRAG_Present_Set_A

FRAG_Garden_Roof_A
FRAG_City_HongKong_A
FRAG_City_Paris_A
FRAG_City_London_A
FRAG_City_NewYork_A
```

- Keep one shared pillar model and use three texture/emission-mask variants.
- Use one shared opaque corridor material plus one atlas for painted panels.
- Use transparency only for the cinematic canvas and selected memory fragments.
- Put corridor pivots on the floor at a bay edge for snapping.
- Put reconstruction-fragment pivots near their visual center for Timeline animation.
- The cinematic canvas must remain one thin, single-sided surface driven by a video material; do not build it as a portal or full 3D memory scene.
- Validate silhouette, scale, and interaction distance in Quest before adding decorative detail.
- Prioritize the player's interior view: the layered painted ceiling, dark rafter rhythm, continuous red upper lattice, and beam-column junctions carry more Long Corridor identity than the exterior roof surface.
- Lattice rule: each 2.6 m bay uses three equal rigid panels; every panel repeats the same mirrored rectangular meander with one fixed square bar thickness. Curved corridor sections rotate whole panels at bay joints and never deform the pattern.

Final models, materials, textures, VFX, animations, and prefabs should follow `_team reference/OSMANTHUS_VR_VISUAL_SYSTEM_PLAN.md`.

## Current Meshy split — canopy replacement

- Send only `ENV_Corridor_CanopyBay_A` to Meshy: roof, underside rafters, upper beams, brackets, upper lattice, transverse painted panels, and four short mounting stubs.
- Keep full pillars, stone bases, floor, curb, and low railings as dimensionally exact ProBuilder/Blender modules.
- Treat `ENV_Corridor_BeamJoint_A` as a possible second reusable generation set if the canopy result does not preserve the bracket/column transition.
- Do not replace the whole corridor bay with one AI mesh; retain independent snapping, repetition, LOD, and material control in Unity.
- Multi-view source and submission order are documented in `MeshyMultiView/ENV_Corridor_CanopyBay_A/README.md`.

## Meshy Blender drafts — 2026-08-12

> Project rule for future Meshy generation: explicitly use `ai_model: "latest"` by default. Do not use `meshy-t1` or `meshy-t2` unless the user specifically requests Smart Topology. Verify what `latest` currently maps to and confirm the current credit cost before each paid run.

Three untextured Smart Topology drafts were generated as Blender starting points. They are stored under `Assets/Art/Models/MeshyDrafts/` for direct Unity inspection and should not be treated as final production assets.

| Asset | Meshy task | FBX |
|---|---|---|
| Roof/eave module | `019ff2d3-7454-798c-a6bb-acf427261d77` | `Assets/Art/Models/MeshyDrafts/20260812_005650_summer-palace-roof-eave_019ff2d3/ENV_Corridor_RoofEave_MeshyDraft_v1.fbx` |
| Dougong bracket | `019ff2d3-7433-7182-8d9f-ba62ae848093` | `Assets/Art/Models/MeshyDrafts/20260812_005650_summer-palace-dougong_019ff2d3/ENV_Corridor_Dougong_MeshyDraft_v1.fbx` |
| Lattice railing | `019ff2d3-73d8-7abb-ab37-3f6c7a142d5a` | `Assets/Art/Models/MeshyDrafts/20260812_005650_summer-palace-railing_019ff2d3/ENV_Corridor_Railing_MeshyDraft_v1.fbx` |

Generation settings:

- Image-to-3D, `smart-topology`, `meshy-t2`
- Untextured FBX only
- Target polycounts: roof `12k`, dougong `8k`, railing `6k`
- Actual cost: `5 + 5 + 5 = 15 credits`
- Remaining balance after generation: `1085 credits`

Blender cleanup priorities:

- Roof: regularize tile spacing, flatten the modular back seam, separate roof and painted fascia, set a repeatable edge pivot.
- Dougong: rebuild penetrations as clean joined volumes, enforce symmetry, square the beam interfaces, remove unsupported internal faces.
- Railing: square the bar profiles, restore consistent bar thickness, correct the center join, make both end posts snap cleanly to a 2.6 m bay.
- Apply rotation and scale, normalize dimensions, clean normals, generate UVs, and export only the final low-poly objects.
