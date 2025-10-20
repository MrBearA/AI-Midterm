using UnityEngine;
using UnityEngine.AI;
using System.Collections;

[RequireComponent(typeof(NavMeshAgent))]
[RequireComponent(typeof(Collider))] // We'll use a trigger on the AI
public class Basic_AI : MonoBehaviour
{
    [Header("Player Detection")]
    public string playerTag = "Player";
    public Renderer playerRendererOverride;   // optional: drag the player's Renderer here

    [Header("Roaming")]
    public float roamRadius = 15f;
    public float arrivalDistance = 0.25f;
    public float minWait = 0.5f;
    public float maxWait = 2f;

    [Header("Chase")]
    public float repathIntervalWhileChasing = 0.15f; // how often to update destination
    public float giveUpAfterSeconds = 0f;            // 0 = never give up

    [Header("Stuck Check (optional)")]
    public float stuckSpeed = 0.05f;
    public float stuckTime = 2f;

    private NavMeshAgent agent;
    private Vector3 home;
    private bool waiting;
    private float stuckTimer;

    private bool chasing;
    private Transform player;
    private float nextChaseRepathTime;
    private float chaseTimer;

    private Renderer[] aiRenderers;
    private Coroutine waitRoutine;

    void Awake()
    {
        agent = GetComponent<NavMeshAgent>();
        aiRenderers = GetComponentsInChildren<Renderer>(true);

        // Make the AI collider a trigger and add a kinematic RB so triggers fire reliably
        var col = GetComponent<Collider>();
        col.isTrigger = true;

        if (!TryGetComponent<Rigidbody>(out var rb))
            rb = gameObject.AddComponent<Rigidbody>();
        rb.isKinematic = true;
        rb.useGravity = false;
    }

    void Start()
    {
        home = transform.position;
        SnapToNavmesh();
        if (agent.isOnNavMesh) GoToRandomPoint();
    }

    void Update()
    {
        if (!IsAgentReady()) return;

        if (chasing)
        {
            if (player == null) { StopChasing(); return; }

            if (Time.time >= nextChaseRepathTime)
            {
                agent.SetDestination(player.position);
                nextChaseRepathTime = Time.time + repathIntervalWhileChasing;
            }

            if (giveUpAfterSeconds > 0f)
            {
                chaseTimer += Time.deltaTime;
                if (chaseTimer >= giveUpAfterSeconds) StopChasing();
            }
            return;
        }

        // Roaming arrival
        if (!waiting && agent.hasPath && !agent.pathPending &&
            agent.remainingDistance <= Mathf.Max(arrivalDistance, agent.stoppingDistance))
        {
            waitRoutine = StartCoroutine(WaitThenRoam());
            return;
        }

        // Roaming stuck detection
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
            else stuckTimer = 0f;
        }
        else stuckTimer = 0f;
    }

    void OnTriggerEnter(Collider other)
    {
        if (!other.CompareTag(playerTag)) return;

        player = other.transform;

        // Copy color from player
        var src = playerRendererOverride ? playerRendererOverride : other.GetComponentInChildren<Renderer>();
        if (src != null) ApplyColorToAI(GetColor(src));

        // Start chasing
        StartChasing();
    }

    void StartChasing()
    {
        chasing = true;
        chaseTimer = 0f;
        nextChaseRepathTime = 0f;
        if (waitRoutine != null) { StopCoroutine(waitRoutine); waiting = false; }
        agent.isStopped = false;
        agent.ResetPath();
    }

    void StopChasing()
    {
        chasing = false;
        player = null;
        GoToRandomPoint();
    }

    IEnumerator WaitThenRoam()
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
        if (!IsAgentReady()) return;

        if (!TryGetNavmeshPoint(home, roamRadius, out var target))
            TryGetNavmeshPoint(home, Mathf.Max(2f, roamRadius * 0.5f), out target);

        agent.SetDestination(target);
    }

    bool IsAgentReady() => agent != null && agent.enabled && agent.isOnNavMesh;

    void SnapToNavmesh()
    {
        if (!agent.enabled) agent.enabled = true;
        if (!agent.isOnNavMesh &&
            NavMesh.SamplePosition(transform.position, out var hit, 3f, NavMesh.AllAreas))
        {
            agent.Warp(hit.position); // use Warp for agents
        }
    }

    static bool TryGetNavmeshPoint(Vector3 center, float radius, out Vector3 result)
    {
        for (int i = 0; i < 20; i++)
        {
            var random = center + Random.insideUnitSphere * radius;
            if (NavMesh.SamplePosition(random, out var hit, 2f, NavMesh.AllAreas))
            {
                result = hit.position; return true;
            }
        }
        result = center; return false;
    }

    static Color GetColor(Renderer r)
    {
        var m = r.material;
        if (m == null) return Color.white;
        if (m.HasProperty("_BaseColor")) return m.GetColor("_BaseColor"); // URP
        if (m.HasProperty("_Color")) return m.GetColor("_Color");     // Standard
        return Color.white;
    }

    void ApplyColorToAI(Color c)
    {
        foreach (var r in aiRenderers)
            foreach (var m in r.materials)
            {
                if (m == null) continue;
                if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", c);
                if (m.HasProperty("_Color")) m.SetColor("_Color", c);
            }
    }

    void OnDrawGizmosSelected()
    {
        var c = Application.isPlaying ? home : transform.position;
        Gizmos.color = new Color(0f, 0.6f, 1f, 0.25f);
        Gizmos.DrawWireSphere(c, roamRadius);
    }
}
