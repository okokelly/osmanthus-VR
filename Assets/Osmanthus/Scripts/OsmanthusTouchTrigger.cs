using UnityEngine;

// Put on a touchable osmanthus (it needs a collider so the controller ray can hit it).
// The controller ray poker calls TryActivate() when the player aims at it and pulls the trigger.
[RequireComponent(typeof(Collider))]
public class OsmanthusTouchTrigger : MonoBehaviour
{
    public System.Action Activated;
    private bool _armed;

    public bool IsArmed => _armed;
    public void Arm(bool on) { _armed = on; }

    // Returns true if it fired (was armed). Disarms so it only triggers once until re-armed.
    public bool TryActivate()
    {
        if (!_armed) return false;
        _armed = false;
        Activated?.Invoke();
        return true;
    }

    [ContextMenu("Force Activate (test)")]
    public void ForceActivate()
    {
        _armed = false;
        Activated?.Invoke();
    }
}
