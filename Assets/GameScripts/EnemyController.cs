using UnityEngine;
using UnityEngine.AI;

public class EnemyController : MonoBehaviour
{
    public Transform Target;
    private NavMeshAgent Agent;
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        Agent = gameObject.GetComponent<NavMeshAgent>();
    }

    // Update is called once per frame
    void Update()
    {
        Agent.SetDestination(Target.position);
    }
}
