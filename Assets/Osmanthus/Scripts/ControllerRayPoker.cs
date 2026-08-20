using UnityEngine;

// Emits a ray from a controller anchor. When it's aimed at an OsmanthusTouchTrigger and the index
// trigger is pulled, it activates that osmanthus. Draws a thin aim line that highlights on a target.
public class ControllerRayPoker : MonoBehaviour
{
    public float maxDistance = 10f;
    public OVRInput.Controller controller = OVRInput.Controller.RTouch;
    public LineRenderer line;

    private readonly Color _idle = new Color(1f, 1f, 1f, 0.35f);
    private readonly Color _hot = new Color(1f, 0.8f, 0.35f, 0.95f);

    private void Update()
    {
        Ray ray = new Ray(transform.position, transform.forward);
        Vector3 end = transform.position + transform.forward * maxDistance;
        bool onTarget = false;

        if (Physics.Raycast(ray, out RaycastHit hit, maxDistance, ~0, QueryTriggerInteraction.Collide))
        {
            end = hit.point;
            OsmanthusTouchTrigger trigger = hit.collider.GetComponentInParent<OsmanthusTouchTrigger>();
            if (trigger != null && trigger.IsArmed)
            {
                onTarget = true;
                if (OVRInput.GetDown(OVRInput.Button.PrimaryIndexTrigger, controller))
                    trigger.TryActivate();
            }
        }

        if (line != null)
        {
            line.positionCount = 2;
            line.SetPosition(0, transform.position);
            line.SetPosition(1, end);
            line.startColor = line.endColor = onTarget ? _hot : _idle;
        }
    }
}
