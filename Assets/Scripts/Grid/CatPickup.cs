using UnityEngine;

/// <summary>
/// The second (orange) cat standing on the extra-box pickup tile. It walks in
/// place at the GIF's native 200 ms per frame until the player collects it.
/// Only added at runtime, so the editor scene is never polluted.
/// </summary>
[RequireComponent(typeof(SpriteRenderer))]
public class CatPickup : MonoBehaviour
{
    SpriteRenderer sr;
    Sprite[] walk;
    float timer;
    int frame;

    void Awake()
    {
        sr = GetComponent<SpriteRenderer>();
    }

    void Start()
    {
        walk = CatArt.Walk(true);
        if (sr != null && walk != null && walk.Length > 0) sr.sprite = walk[0];
    }

    void Update()
    {
        if (sr == null || walk == null || walk.Length == 0) return;

        timer += Time.deltaTime;
        while (timer >= CatArt.FrameTime)
        {
            timer -= CatArt.FrameTime;
            frame = (frame + 1) % walk.Length;
        }

        var target = walk[frame];
        if (target != null && sr.sprite != target) sr.sprite = target;
    }
}
