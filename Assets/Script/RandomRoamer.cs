using UnityEngine;
using UnityEngine.AI;
using System.Collections;

[RequireComponent(typeof(NavMeshAgent))]
public class RandomRoamer : MonoBehaviour
{
    [Header("Roaming")]
    public float roamRadius = 15f;          // How far from the start it can wander
    public float arrivalDistance = 0.25f;   // Considered arrived when within this distance
    public float minWait = 0.5f;            // Min wait time at each stop
    public float maxWait = 2f;              // Max wait time at each stop

    [Header("Stuck Check (optional)")]
    public float stuckSpeed = 0.05f;        // Below this speed counts toward "stuck"
    public float stuckTime = 2f;            // Seconds of low speed before repathing

    private NavMeshAgent agent;
    private Vector3 home;
    private bool waiting;
    private float stuckTimer;

    void Awake()
    {
        agent = GetComponent<NavMeshAgent>();
    }

    void Start()
    {
        home = transform.position;

        // If spawned a bit off the mesh, snap to the closest valid point
        if (!agent.isOnNavMesh)
        {
            if (NavMesh.SamplePosition(transform.position, out var hit, 2f, NavMesh.AllAreas))
                agent.Warp(hit.position);
        }

        GoToRandomPoint();
    }

    void Update()
    {
        if (waiting) return;

        // Arrived?
        if (!agent.pathPending &&
            agent.remainingDistance <= Mathf.Max(arrivalDistance, agent.stoppingDistance))
        {
            StartCoroutine(WaitThenMove());
            return;
        }

        // Stuck repath
        if (agent.hasPath && agent.remainingDistance > agent.stoppingDistance)
        {
            if (agent.velocity.sqrMagnitude < stuckSpeed * stuckSpeed)
            {
                stuckTimer += Time.deltaTime;
                if (stuckTimer >= stuckTime)
                {
                    stuckTimer = 0f;
                    GoToRandomPoint();
                }
            }
            else
            {
                stuckTimer = 0f;
            }
        }
        else
        {
            stuckTimer = 0f;
        }
    }

    IEnumerator WaitThenMove()
    {
        waiting = true;
        agent.isStopped = true;
        yield return new WaitForSeconds(Random.Range(minWait, maxWait));
        agent.isStopped = false;
        waiting = false;
        GoToRandomPoint();
    }

    void GoToRandomPoint()
    {
        if (!TryGetNavmeshPoint(home, roamRadius, out var target))
        {
            // Fallback: smaller radius
            TryGetNavmeshPoint(home, Mathf.Max(2f, roamRadius * 0.5f), out target);
        }
        agent.SetDestination(target);
    }

    static bool TryGetNavmeshPoint(Vector3 center, float radius, out Vector3 result)
    {
        for (int i = 0; i < 20; i++)
        {
            Vector3 random = center + Random.insideUnitSphere * radius;
            if (NavMesh.SamplePosition(random, out var hit, 2f, NavMesh.AllAreas))
            {
                result = hit.position;
                return true;
            }
        }
        result = center;
        return false;
    }

    void OnDrawGizmosSelected()
    {
        Gizmos.color = new Color(0f, 0.6f, 1f, 0.25f);
        Vector3 c = Application.isPlaying ? home : transform.position;
        Gizmos.DrawWireSphere(c, roamRadius);
    }
}
