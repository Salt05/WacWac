using UnityEngine;

// Base class for scrolling UI tiles (RectTransform + Image).
// Attach to each tile (River_1, River_2, Land_1, Land_2).
// Usage: set `otherTile` to the paired tile so MoveAndLoop can reposition when off-screen.
public class Scroller : MonoBehaviour
{
    [Tooltip("Scrolling speed in units per second. Can be set to 0 to pause.")]
    public float speed = 1f;

    [Tooltip("The other tile that this tile will loop relative to. Set in the Inspector.")]
    public RectTransform otherTile;

    // New: trigger point A (when this tile's anchoredPosition.x reaches this value, it will be moved)
    [Tooltip("Trigger RectTransform A: when this tile's anchored X reaches A.x (within epsilon or within triggerRange), it will be moved to destination B.")]
    public RectTransform triggerA;

    // New: destination B (target anchored position to move this tile to)
    [Tooltip("Destination RectTransform B: teleport this tile to B's anchored position when trigger condition met.")]
    public RectTransform destinationB;

    [Tooltip("Tolerance in anchored units when comparing X positions for the trigger check (small epsilon).")]
    public float triggerEpsilon = 0.5f;

    [Tooltip("Range in anchored units around triggerA.x that will also cause activation (e.g. 100 means [A.x-100, A.x+100]).")]
    public float triggerRange = 100f;

    protected RectTransform rect;
    protected float width;
    // starting anchored x used as a reference so looping works regardless of initial placement
    private float startAnchoredX;

    // preserve default speed so Resume can restore it
    protected float defaultSpeed;

    // paused flag to prevent movement until explicitly resumed
    protected bool paused = true;

    // reference to RaceController to check running/remainingTime
    private RaceController raceController;

    protected virtual void Awake()
    {
        rect = GetComponent<RectTransform>();
        if (rect == null)
        {
            Debug.LogError("Scroller requires a RectTransform on the same GameObject.");
            enabled = false;
            return;
        }

        width = rect.rect.width;
        startAnchoredX = rect.anchoredPosition.x;
        defaultSpeed = speed;
    }

    // Ensure defaultSpeed reflects any programmatic changes to `speed` made after AddComponent
    protected virtual void Start()
    {
        defaultSpeed = speed;
        // start paused by default; Resume must be called to begin movement
        paused = true;
        speed = 0f;

        // cache RaceController if available; tolerate null and try again in Update
        raceController = FindObjectOfType<RaceController>();
    }

    private void Update()
    {
        if (paused) return;

        // ensure we have up-to-date raceController reference
        if (raceController == null)
            raceController = FindObjectOfType<RaceController>();

        // If a RaceController exists, require it to be running and have time remaining.
        if (raceController != null)
        {
            if (!raceController.IsRunning() || raceController.remainingTime <= 0f)
                return;
        }

        // Move every frame using anchoredPosition for UI elements
        MoveAndLoop(otherTile);

        // After movement, check trigger condition: if this tile's anchored X reached triggerA.x -> teleport to destinationB
        if (triggerA != null && destinationB != null && rect != null)
        {
            float thisX = rect.anchoredPosition.x;
            float targetX = triggerA.anchoredPosition.x;

            bool withinEpsilon = Mathf.Abs(thisX - targetX) <= Mathf.Abs(triggerEpsilon);
            bool withinRange = thisX >= (targetX - Mathf.Abs(triggerRange)) && thisX <= (targetX + Mathf.Abs(triggerRange));

            if (withinEpsilon || withinRange)
            {
                rect.anchoredPosition = new Vector2(destinationB.anchoredPosition.x, destinationB.anchoredPosition.y);
            }
        }
    }

    // Pause the scroller (set speed to 0 while preserving defaultSpeed)
    public void Pause()
    {
        paused = true;
        speed = 0f;
    }

    // Resume scroller to previously configured default speed
    public void Resume()
    {
        paused = false;
        speed = defaultSpeed;
    }

    // Reset this tile to its original anchored X and pause movement.
    public void ResetToStart()
    {
        if (rect == null) rect = GetComponent<RectTransform>();
        if (rect == null) return;
        Vector2 p = rect.anchoredPosition;
        p.x = startAnchoredX;
        rect.anchoredPosition = p;

        // pause and clear runtime speed so Resume will restore defaultSpeed
        paused = true;
        speed = 0f;
    }

    // Move the tile left by speed*dt and loop seamlessly: when this tile's right edge
    // is <= other tile's left edge, reposition this tile so its left edge == other.right
    protected void MoveAndLoop(RectTransform other)
    {
        if (rect == null) return;

        // delta move (UI anchoredPosition coordinates typically pixels)
        Vector2 pos = rect.anchoredPosition;
        pos.x -= speed * Time.deltaTime;

        // compute this tile edges in anchored coordinates
        float thisLeft = pos.x - rect.pivot.x * width;
        float thisRight = pos.x + (1f - rect.pivot.x) * width;

        if (other == null)
        {
            // fallback: simple wrap using startAnchoredX and width
            if (pos.x <= startAnchoredX - width)
            {
                pos.x += width * 2f;
            }
            rect.anchoredPosition = pos;
            return;
        }

        float otherWidth = other.rect.width;
        float otherLeft = other.anchoredPosition.x - other.pivot.x * otherWidth;
        float otherRight = other.anchoredPosition.x + (1f - other.pivot.x) * otherWidth;

        // If this tile fully left of other (no overlap), snap to right of other
        if (thisRight <= otherLeft + 0.01f)
        {
            pos.x = otherRight + rect.pivot.x * width;
        }

        rect.anchoredPosition = pos;
    }
}


