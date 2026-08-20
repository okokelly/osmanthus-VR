using System.Collections;
using UnityEngine;
using UnityEngine.Video;

// Drives the guide-osmanthus flow:
//   waypoint A  -> aim + trigger -> "视频一" -> screen folds -> osmanthus drifts to B
//   waypoint B  -> aim + trigger -> "视频二" -> screen folds -> osmanthus drifts to the lake.
// The guide osmanthus (with its float animation + touch trigger) is a child of `guideMover`;
// this script moves `guideMover` between waypoints so the float animation is unaffected.
public class OsmanthusVideoSequence : MonoBehaviour
{
    public Transform guideMover;
    public OsmanthusTouchTrigger trigger;
    public VideoScreen screen;

    [Header("Waypoints (guideMover positions)")]
    public Vector3 waypointA = new Vector3(0f, 1.4f, -18f);
    public Vector3 waypointB = new Vector3(0f, 1.4f, -2f);
    public Vector3 waypointLake = new Vector3(-12f, 1.6f, 19f);

    [Header("Videos (leave empty for text placeholders)")]
    public VideoClip clip1;
    public VideoClip clip2;
    public string label1 = "VIDEO 1";
    public string label2 = "VIDEO 2";

    public float driftTime = 4f;

    private int _stage;

    private void Start()
    {
        if (guideMover != null) guideMover.position = waypointA;
        if (trigger != null)
        {
            trigger.Activated += OnActivated;
            trigger.Arm(true);
        }
    }

    private void OnDestroy()
    {
        if (trigger != null) trigger.Activated -= OnActivated;
    }

    private Transform Viewer => Camera.main != null ? Camera.main.transform : transform;

    private void OnActivated()
    {
        if (_stage == 0)
        {
            screen.onClosed = AfterVideo1;
            screen.PlayInFront(Viewer, clip1, label1);
        }
        else if (_stage == 1)
        {
            screen.onClosed = AfterVideo2;
            screen.PlayInFront(Viewer, clip2, label2);
        }
    }

    private void AfterVideo1()
    {
        _stage = 1;
        StartCoroutine(Drift(waypointA, waypointB, () => trigger.Arm(true)));
    }

    private void AfterVideo2()
    {
        _stage = 2;
        StartCoroutine(Drift(waypointB, waypointLake, null)); // guide leads to the lake, then rests
    }

    private IEnumerator Drift(Vector3 from, Vector3 to, System.Action done)
    {
        float t = 0f;
        while (t < driftTime)
        {
            t += Time.deltaTime;
            guideMover.position = Vector3.Lerp(from, to, Mathf.SmoothStep(0f, 1f, t / driftTime));
            yield return null;
        }
        guideMover.position = to;
        done?.Invoke();
    }
}
