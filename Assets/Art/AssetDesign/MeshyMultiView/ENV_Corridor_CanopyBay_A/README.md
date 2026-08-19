# ENV_Corridor_CanopyBay_A — Meshy Multi-View Source

This set describes one connected upper-canopy bay for Meshy Multi-Image-to-3D. It is intended to replace only the upper roof/beam/lattice blockout in Unity, not the entire corridor bay.

## Input order

1. `MV_CanopyBay_A_01_front.png`
2. `MV_CanopyBay_A_02_right_side.png`
3. `MV_CanopyBay_A_03_rear.png`
4. `MV_CanopyBay_A_04_underside_3q.png`

`MV_CanopyBay_A_Sheet_v1.png` is the review/contact sheet and should not be submitted together with the four crops.

## Asset boundary

Include as one connected mesh:

- shallow gray tiled roof with a gently concave long eave and subtly upturned ends
- dark underside rafters with restrained vermilion stripes
- front and rear transverse scenic-painted crossbeams
- geometric-painted long side beams with no scenic paintings
- compact corner bracket clusters
- one consistent three-panel vermilion upper lattice band on each long side
- four short green mounting stubs

Exclude:

- full-height pillars and stone bases
- floor and curb
- low railings
- lake, trees, people, labels, or environment dressing

Keep pillars, floor, bases, and low railings as exact ProBuilder/Blender geometry. If more junction detail is needed, generate `ENV_Corridor_BeamJoint_A` later as a separate reusable asset rather than enlarging this Meshy object.

## Architectural invariants

- Scenic landscape paintings occur only on the front and rear transverse crossbeams.
- The two long side beams use colored geometric painting only.
- Each long side has exactly three equal lattice panels using the same mirrored rectangular meander.
- The roof is shallow and curved, not flat, not a rigid triangular pediment, and not a semicircular barrel vault.
- All four images describe the same object, with the same brackets, lattice count, mounting stubs, and material placement.

## Unity handoff target

- Local `X`: along the corridor, snap length `2.60 m`
- Local `Z`: across the corridor, roof/eave depth approximately `3.20 m`
- Local `Y`: vertical
- Set the four mounting-stub bottoms to one common `Y = 0` plane.
- Recommended pivot: center of the four mounting points on the `Y = 0` plane.
- Rescale and straighten in Blender before export; the source images define design and silhouette, not survey-grade dimensions.
- Preserve the existing Unity blockout until the FBX passes scale, pivot, silhouette, and VR sightline checks.

## Planned Meshy settings

- Workflow: Multi-Image-to-3D
- Images: the four ordered crops above
- `ai_model`: `latest`
- Texture: enabled for the first evaluation so painted-region placement can be inspected
- Final inspection/download target: FBX for Blender cleanup and Unity import

Before running a paid task, check the current Meshy balance and exact quoted credit cost, then obtain user confirmation.
