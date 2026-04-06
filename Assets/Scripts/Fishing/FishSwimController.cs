using System;
using UnityEngine;
using Random = UnityEngine.Random;

/// <summary>
/// Simulates a fish swimming randomly within a defined radius.
/// Attach this script to a fish GameObject. The fish will wander
/// smoothly within the boundary, picking new targets periodically
/// and respecting a configurable turning radius.
/// </summary>
public class FishSwimController : MonoBehaviour
{
    [Header("Boundary Settings")]
    [Tooltip("The centre of the swim area in world space.")]
    public Transform areaCenter;

    [Tooltip("Maximum distance from the area centre the fish may swim.")]
    public float swimRadius = 10f;

    [Header("Movement Settings")]
    [Tooltip("Forward swim speed (units per second).")]
    public float swimSpeed = 2f;

    [Tooltip("How quickly the fish can turn (degrees per second). Lower = wider turning circle.")]
    public float turnSpeed = 90f;

    [Tooltip("Minimum time (seconds) before choosing a new wander target.")]
    public float minWanderInterval = 2f;

    [Tooltip("Maximum time (seconds) before choosing a new wander target.")]
    public float maxWanderInterval = 5f;

    [Header("3D / 2D Mode")]
    [Tooltip("If true the fish moves on the XZ plane (3D). If false it moves on the XY plane (2D).")]
    public bool use3D = true;

    [Header("Debug")]
    public bool drawGizmos = true;

    // ── Private state ──────────────────────────────────────────────
    private bool m_wandering = true;
    private Vector3 m_targetPosition;
    private float   m_wanderTimer;
    private float   m_wanderInterval;
    private bool m_hasReachedTarget;
    private float m_originalTurnSpeed;

    public Action ReachedTarget;

    private void Start()
    {
        // Resolve the area centre to this object's position if not assigned.
        if (areaCenter == null)
        {
            GameObject pivot = new GameObject($"{name}_SwimAreaCenter");
            pivot.transform.position = transform.position;
            areaCenter = pivot.transform;
        }

        m_originalTurnSpeed = turnSpeed;
        PickNewTarget();
    }

    private void Update()
    {
        if (m_wandering)
        {
            // Count down to the next forced target pick.
            m_wanderTimer -= Time.deltaTime;
            if (m_wanderTimer <= 0f)
                PickNewTarget();

            // If we are close enough to the current target, pick a new one early.
            if (Vector3.Distance(transform.position, m_targetPosition) < 0.3f)
                PickNewTarget();
        }

        MoveTowardTarget();
    }

    // ── Core Logic ─────────────────────────────────────────────────

    /// <summary>
    /// Choose a random destination that lies within <see cref="swimRadius"/>
    /// of <see cref="areaCenter"/>.
    /// </summary>
    private void PickNewTarget()
    {
        m_wanderInterval = Random.Range(minWanderInterval, maxWanderInterval);
        m_wanderTimer    = m_wanderInterval;

        // Random point inside a unit circle/sphere, then scale.
        Vector3 offset;
        if (use3D)
        {
            // Flat XZ plane wandering.
            Vector2 rnd = Random.insideUnitCircle * swimRadius;
            offset = new Vector3(rnd.x, 0f, rnd.y);
        }
        else
        {
            // XY plane wandering (2D games / top-down).
            Vector2 rnd = Random.insideUnitCircle * swimRadius;
            offset = new Vector3(rnd.x, rnd.y, 0f);
        }

        m_targetPosition = areaCenter.position + offset;
    }

    /// <summary>
    /// Rotate toward the target at <see cref="turnSpeed"/> deg/s,
    /// then move forward at <see cref="swimSpeed"/>.
    /// </summary>
    private void MoveTowardTarget()
    {
        Vector3 direction = (m_targetPosition - transform.position);

        if (use3D)
            direction.y = 0f; // Keep fish level on XZ plane.
        else
            direction.z = 0f; // Keep fish on XY plane.

        if (direction.sqrMagnitude < 0.001f || m_hasReachedTarget)
        {
            if (m_wandering == false && m_hasReachedTarget == false)
            {
                ReachedTarget?.Invoke();
                m_hasReachedTarget = true;
            }
            return;
        }

        direction.Normalize();

        // Smoothly rotate toward the target direction.
        Quaternion targetRotation = use3D
            ? Quaternion.LookRotation(direction, Vector3.up)
            : Quaternion.FromToRotation(Vector3.right, direction);

        transform.rotation = Quaternion.RotateTowards(
            transform.rotation,
            targetRotation,
            turnSpeed * Time.deltaTime
        );

        // Always swim forward (along local forward / local right in 2D).
        transform.position += transform.forward * (swimSpeed * Time.deltaTime);
    }

    public void SetWanderActive(bool isActive)
    {
        m_hasReachedTarget = false;
        m_wandering = isActive;
        turnSpeed = isActive ? m_originalTurnSpeed : m_originalTurnSpeed * 50f;
    }

    public void SetTarget(Vector3 targetPosition)
    {
        m_hasReachedTarget = false;
        m_targetPosition = targetPosition;
    }

    // ── Gizmos ─────────────────────────────────────────────────────

    private void OnDrawGizmosSelected()
    {
        if (!drawGizmos) return;

        Vector3 centre = areaCenter != null ? areaCenter.position : transform.position;

        // Draw boundary circle.
        Gizmos.color = new Color(0f, 0.8f, 1f, 0.35f);
        DrawCircleGizmo(centre, swimRadius);

        // Draw line to current target.
        if (Application.isPlaying)
        {
            Gizmos.color = Color.yellow;
            Gizmos.DrawSphere(m_targetPosition, 0.15f);
            Gizmos.DrawLine(transform.position, m_targetPosition);
        }
    }

    private void DrawCircleGizmo(Vector3 centre, float radius, int segments = 64)
    {
        float step = 360f / segments;
        Vector3 prev = centre + CirclePoint(0f, radius);

        for (int i = 1; i <= segments; i++)
        {
            Vector3 next = centre + CirclePoint(step * i, radius);
            Gizmos.DrawLine(prev, next);
            prev = next;
        }
    }

    private Vector3 CirclePoint(float angleDeg, float radius)
    {
        float rad = angleDeg * Mathf.Deg2Rad;
        return use3D
            ? new Vector3(Mathf.Sin(rad) * radius, 0f, Mathf.Cos(rad) * radius)
            : new Vector3(Mathf.Cos(rad) * radius, Mathf.Sin(rad) * radius, 0f);
    }
}
