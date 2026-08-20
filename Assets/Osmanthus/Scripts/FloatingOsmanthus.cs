using UnityEngine;

// Idle wind motion for a floating osmanthus: bobs up/down, sways/turns gently as if in a breeze,
// and tilts slightly. Every instance gets randomized phases/speeds so a field never moves in lockstep.
// Rotation oscillates (plus a slow drift) around the instance's base orientation, so the flower keeps
// facing the way the placer set it (bloom up-and-out) instead of tumbling.
public class FloatingOsmanthus : MonoBehaviour
{
    public float bobAmplitude = 0.05f;
    public float bobSpeed = 1.0f;
    public float yawSwayDeg = 14f;     // gentle back-and-forth turn (wind)
    public float yawSwaySpeed = 0.55f;
    public float yawDriftSpeed = 4f;   // slow continuous turn, deg/sec
    public float tiltSwayDeg = 6f;     // gentle nodding
    public float tiltSwaySpeed = 0.9f;

    private Vector3 _basePos;
    private Quaternion _baseRot;
    private float _pBob, _pYaw, _pTilt, _speedJitter, _driftDir;

    private void OnEnable()
    {
        _basePos = transform.localPosition;
        _baseRot = transform.localRotation;
        _pBob = Random.value * Mathf.PI * 2f;
        _pYaw = Random.value * Mathf.PI * 2f;
        _pTilt = Random.value * Mathf.PI * 2f;
        _speedJitter = Random.Range(0.8f, 1.2f);
        _driftDir = Random.value < 0.5f ? -1f : 1f;
    }

    private void Update()
    {
        float t = Time.time * _speedJitter;

        transform.localPosition = _basePos + new Vector3(0f, Mathf.Sin(t * bobSpeed + _pBob) * bobAmplitude, 0f);

        float yaw = Mathf.Sin(t * yawSwaySpeed + _pYaw) * yawSwayDeg + _driftDir * yawDriftSpeed * Time.time;
        float tilt = Mathf.Sin(t * tiltSwaySpeed + _pTilt) * tiltSwayDeg;
        transform.localRotation = _baseRot * Quaternion.Euler(tilt, yaw, tilt * 0.5f);
    }
}
