using UnityEngine;

/// <summary>
/// A throw-away sprite that plays the four damage frames of a crate and then deletes
/// itself. It is a separate object on purpose: the tile is destroyed the instant the
/// ball breaks it (so the ball can fly on), but the picture of it breaking lingers.
/// </summary>
public class BlockBurst : MonoBehaviour
{
    public Sprite[] frames;
    // the source GIFs are 200 ms per frame; 20 fps made the break invisible
    public float fps = 5f;

    SpriteRenderer sr;
    float t;
    int shown;

    public static void Spawn(Sprite[] frames, Vector3 position, int sortingOrder)
    {
        if (frames == null || frames.Length == 0) return;

        var go = new GameObject("BlockBurst");
        go.transform.position = position;

        var sr = go.AddComponent<SpriteRenderer>();
        sr.sprite = frames[0];
        sr.sortingOrder = sortingOrder;

        var burst = go.AddComponent<BlockBurst>();
        burst.frames = frames;
    }

    void Awake()
    {
        sr = GetComponent<SpriteRenderer>();
        shown = 0;
    }

    void Update()
    {
        if (frames == null || frames.Length == 0 || sr == null)
        {
            Destroy(gameObject);
            return;
        }

        t += Time.deltaTime;
        int index = Mathf.FloorToInt(t * Mathf.Max(1f, fps));
        if (index >= frames.Length)
        {
            Destroy(gameObject);
            return;
        }

        if (index != shown)
        {
            shown = index;
            sr.sprite = frames[Mathf.Min(index, frames.Length - 1)];
        }
    }
}
