using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>Smooth side-scrolling camera that follows the player and clamps to the level.</summary>
public class CameraFollow : MonoBehaviour
{
    public Transform target;
    public float minX = -16f;
    public float maxX = 16f;
    public float minY = 0.4f;
    public float maxY = 9.6f;
    public float smoothTime = 0.16f;
    public float boxSmoothTime = 0.08f;
    public float verticalOffset = 0.4f;
    public float lookAhead = 0.9f;

    Vector3 velocity;

    void Awake()
    {
        if (boxSmoothTime <= 0.001f) boxSmoothTime = 0.08f;

        // Old scenes may have lost the serialized target; fall back to the player.
        if (target == null)
        {
            var player = FindAnyObjectByType<PlayerController>();
            if (player != null) target = player.transform;
        }
    }

    void LateUpdate()
    {
        Transform desiredTarget = target;
        float followSmooth = smoothTime;

        // Hold Q to watch the most recently thrown ball; release to return to the player.
        var keyboard = Keyboard.current;
        if (keyboard != null && keyboard.qKey.isPressed)
        {
            var ball = BoxProjectile.Latest;
            if (ball != null)
            {
                desiredTarget = ball.transform;
                followSmooth = boxSmoothTime;
            }
        }

        if (desiredTarget == null) return;

        float desiredX = Mathf.Clamp(desiredTarget.position.x + lookAhead, minX, maxX);
        float desiredY = Mathf.Clamp(desiredTarget.position.y + verticalOffset, minY, maxY);

        Vector3 current = transform.position;
        Vector3 goal = new Vector3(desiredX, desiredY, current.z);
        transform.position = Vector3.SmoothDamp(current, goal, ref velocity, followSmooth);
    }
}
