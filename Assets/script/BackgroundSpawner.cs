using UnityEngine;
using System.Collections.Generic;

// BackgroundSpawner manages UI background tiles: spawns a copy to the right when
// a tile enters the trigger area, and destroys a tile when it fully leaves the trigger.
// Designed to work with RectTransform UI Images.
[RequireComponent(typeof(BackgroundUITrigger))]
public class BackgroundSpawner : MonoBehaviour
{
    public string backgroundTag = "BackgroundTile";
    public RectTransform initialTile; // reference tile (Background_1)
    public Transform tilesParent; // parent for spawned tiles
    public float spawnOffset = 0f; // small offset to avoid overlap

    // track tiles by GameObject
    private List<RectTransform> tiles = new List<RectTransform>();
    private BackgroundUITrigger triggerArea;

    // track which tiles have already caused a spawn
    private HashSet<RectTransform> spawnedFrom = new HashSet<RectTransform>();

    // record original anchored X of the initial tile so we can reset later
    private float initialAnchoredX = 0f;

    private void Awake()
    {
        triggerArea = GetComponent<BackgroundUITrigger>();
        if (initialTile != null)
        {
            tiles.Add(initialTile);
            if (initialTile.tag != backgroundTag) initialTile.tag = backgroundTag;
            initialAnchoredX = initialTile.anchoredPosition.x;
        }
    }

    private void Update()
    {
        // for each tile, check overlap with trigger
        for (int i = tiles.Count - 1; i >= 0; i--)
        {
            var rt = tiles[i];
            if (rt == null) { tiles.RemoveAt(i); spawnedFrom.Remove(rt); continue; }

            Vector3[] corners = new Vector3[4];
            rt.GetWorldCorners(corners);
            Rect tileWorldRect = new Rect(corners[0].x, corners[0].y, corners[2].x - corners[0].x, corners[2].y - corners[0].y);

            bool overlaps = triggerArea.OverlapsWorldRect(tileWorldRect, triggerArea.enterMargin);

            if (overlaps)
            {
                // spawn a copy to the right if not already spawned from this tile
                if (!spawnedFrom.Contains(rt))
                {
                    SpawnRightOf(rt);
                    spawnedFrom.Add(rt);
                }
            }
            else
            {
                // check for fully outside (use exitMargin)
                bool stillOverlaps = triggerArea.OverlapsWorldRect(tileWorldRect, -triggerArea.exitMargin);
                if (!stillOverlaps)
                {
                    // tile is fully outside -> destroy it (but keep at least one)
                    if (tiles.Count > 1)
                    {
                        tiles.RemoveAt(i);
                        spawnedFrom.Remove(rt);
                        Destroy(rt.gameObject);
                    }
                }
            }
        }

        // cleanup spawnedFrom entries for destroyed tiles
        spawnedFrom.RemoveWhere(x => x == null);
    }

    private void SpawnRightOf(RectTransform source)
    {
        if (initialTile == null) return;

        // compute width in world units
        Vector3[] srcCorners = new Vector3[4];
        source.GetWorldCorners(srcCorners);
        float srcRight = srcCorners[2].x;

        Vector3[] initCorners = new Vector3[4];
        initialTile.GetWorldCorners(initCorners);
        float initWidth = initCorners[2].x - initCorners[0].x;

        // compute target anchoredPosition for new tile so its left edge aligns with srcRight + spawnOffset
        // We'll instantiate a copy of initialTile under tilesParent and then set its world position
        GameObject go = Instantiate(initialTile.gameObject, tilesParent == null ? initialTile.parent : tilesParent);
        var newRT = go.GetComponent<RectTransform>();
        if (newRT == null) { Destroy(go); return; }

        // set tag
        if (go.tag != backgroundTag) go.tag = backgroundTag;

        // position new tile so its left world x == srcRight + spawnOffset
        // get new tile width in world units (may be same as initWidth)
        Vector3[] newCorners = new Vector3[4];
        newRT.GetWorldCorners(newCorners);
        float newWidth = newCorners[2].x - newCorners[0].x;

        // compute world position shift required
        float targetLeft = srcRight + spawnOffset;
        // current left
        float currentLeft = newCorners[0].x;
        float worldShift = targetLeft - currentLeft;

        // apply shift in world space
        newRT.position = newRT.position + new Vector3(worldShift, 0f, 0f);

        tiles.Add(newRT);
    }

    // Public reset: destroy spawned tiles, clear tracking, and restore initial tile anchored X.
    public void ResetSpawner()
    {
        if (initialTile == null) return;

        // Destroy all tiles except initialTile
        for (int i = tiles.Count - 1; i >= 0; i--)
        {
            var rt = tiles[i];
            if (rt == null) continue;
            if (rt == initialTile) continue;
            tiles.RemoveAt(i);
            spawnedFrom.Remove(rt);
            Destroy(rt.gameObject);
        }

        // clear tracking sets and ensure the list contains only the initial tile
        spawnedFrom.Clear();
        tiles.Clear();
        tiles.Add(initialTile);

        // restore initial tile anchored X
        initialTile.anchoredPosition = new Vector2(initialAnchoredX, initialTile.anchoredPosition.y);
    }
}
