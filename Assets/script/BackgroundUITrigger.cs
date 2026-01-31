using UnityEngine;

// Attach to the BackgroundLayer GameObject. This acts as the "trigger area" in world space
// for UI background tiles. It detects when a background tile enters or leaves the area
// by comparing world corners of RectTransforms (since UI RectTransforms don't support
// physics triggers).
[RequireComponent(typeof(RectTransform))]
public class BackgroundUITrigger : MonoBehaviour
{
    // You can tune the margin used to consider "entered" vs "exited" (in world units)
    public float enterMargin = 0.1f;
    public float exitMargin = 0.1f;

    private RectTransform rt;
    private Rect worldRect;

    private void Awake()
    {
        rt = GetComponent<RectTransform>();
        UpdateWorldRect();
    }

    private void OnEnable()
    {
        UpdateWorldRect();
    }

    private void Update()
    {
        UpdateWorldRect();
    }

    private void UpdateWorldRect()
    {
        Vector3[] corners = new Vector3[4];
        rt.GetWorldCorners(corners);
        // worldRect: x,y = bottom-left
        Vector3 bl = corners[0];
        Vector3 tr = corners[2];
        worldRect = new Rect(bl.x, bl.y, tr.x - bl.x, tr.y - bl.y);
    }

    // Expose a method to test intersection against this trigger
    public bool OverlapsWorldRect(Rect other, float margin = 0f)
    {
        Rect r = worldRect;
        r.xMin -= margin; r.yMin -= margin; r.xMax += margin; r.yMax += margin;
        return r.Overlaps(other);
    }

    // Overload for passing world corners directly
    public bool OverlapsWorldCorners(Vector3[] corners, float margin = 0f)
    {
        Rect other = new Rect(corners[0].x, corners[0].y, corners[2].x - corners[0].x, corners[2].y - corners[0].y);
        return OverlapsWorldRect(other, margin);
    }
}
