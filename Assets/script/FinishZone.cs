using UnityEngine;

// FinishZone was used by an older design that triggered results when a Finish object entered point12.
// New design uses time-based sprint and finalization at time=0.
// This component is kept for compatibility but does not drive gameplay.
public class FinishZone : MonoBehaviour
{
    public RaceController raceController;

    private void OnTriggerEnter2D(Collider2D other)
    {
        // Intentionally left no-op.
        // (Avoid accidentally finalizing race early due to collider events.)
    }
}
