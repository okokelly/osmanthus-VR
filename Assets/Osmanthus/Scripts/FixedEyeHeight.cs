using UnityEngine;

// Forces a fixed eye height at runtime. Without this, if the headset uses Floor-level tracking the
// real head height stacks on top of the rig offset and the viewpoint ends up near the ceiling.
// Setting Eye-level tracking means the head pose is ~0 after recenter, so eye height is whatever
// this script parks the rig at.
//
// The rig offset is NOT the eye height. The OVRPlayerController is a 2 m capsule standing on the
// safety floor, so its own origin already floats ~0.95 m above the ground; anything added to the
// rig stacks on top of that. Treating the offset as the eye height put the viewpoint at y = 2.45
// against a corridor floor at y = 0.20 -- a 2.25 m eye height, with the head up among the roof
// beams (lattice tops sit at 2.28). So the target is expressed against the visible floor and the
// controller's drift is subtracted out every frame.
public class FixedEyeHeight : MonoBehaviour
{
    [Tooltip("Metres above the visible floor. ~1.6 is a normal adult standing eye height.")]
    public float eyeHeight = 1.6f;

    [Tooltip("World Y of the walkable surface. Corridor floor inset sits at 0.20, pavilion at 0.24.")]
    public float floorY = 0.20f;

    public Transform cameraRig; // the OVRCameraRig transform

    private void Start()
    {
        try
        {
            if (OVRManager.instance != null)
                OVRManager.instance.trackingOriginType = OVRManager.TrackingOrigin.EyeLevel;
        }
        catch { /* OVRManager not ready yet; LateUpdate still holds the height */ }
    }

    private void LateUpdate()
    {
        if (cameraRig == null) return;

        // Whatever the capsule is doing, land the rig on the same world height.
        float baseY = cameraRig.parent != null ? cameraRig.parent.position.y : 0f;
        float target = floorY + eyeHeight - baseY;

        Vector3 p = cameraRig.localPosition;
        if (!Mathf.Approximately(p.y, target))
        {
            p.y = target;
            cameraRig.localPosition = p;
        }
    }
}
