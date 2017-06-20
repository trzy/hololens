using UnityEngine;
using UnityEngine.AI;
using System.Collections;

[RequireComponent(typeof(NavMeshAgent))]
public class Agent: MonoBehaviour
{
  public void MoveTo(Vector3 position)
  {
    GetComponent<NavMeshAgent>().destination = position;
  }
}
