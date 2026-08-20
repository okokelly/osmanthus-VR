using UnityEngine;

// Forces a fixed eye height at runtime. Without this, if the headset uses Floor-level tracking the
// real head height stacks on top of the rig offset and the viewpoint ends up near the ceiling.
// Setting Eye-level tracking means the head pose is ~0 after recenter, so eye height == the rig's Y.
public class FixedEyeHeight : MonoBehaviour
{
    public float eyeHeight = 1.5f;
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
        Vector3 p = cameraRig.localPosition;
        if (!Mathf.Approximately(p.y, eyeHeight))
        {
            p.y = eyeHeight;
            cameraRig.localPosition = p;
        }
    }
}
