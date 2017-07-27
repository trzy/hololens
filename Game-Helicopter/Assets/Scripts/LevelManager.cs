//TODO: TaskScheduler needs to replace Job (from which solver init is called)
//TODO: Rewrite PlayspaceManager Shuffle() to be thread-safe using a non-Unity randomizer.
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;
using HoloToolkit.Unity;

public class LevelManager: HoloToolkit.Unity.Singleton<LevelManager>
{
  public HiddenTunnel tunnelPrefab;
  public GameObject[] homeBasePrefab;
  public GameObject[] besiegedBuildingPrefab;
  public float distanceApart = 2;
  public GameObject agentPrefab1;
  public GameObject agentPrefab2;
  public GameObject agentPrefab3;

  private int m_numTasksPending = 0;

  private GameObject PlaceBuildingOnFloor(GameObject prefab, GameObject other)
  {
    Vector3 size = Footprint.Measure(prefab);
    Vector3 position;
    Quaternion rotation;
    List<PlayspaceManager.Rule> rules = new List<PlayspaceManager.Rule>();
    if (other != null)
      rules.Add(PlayspaceManager.Rule.AwayFrom(other.transform.position, distanceApart));
    if (PlayspaceManager.Instance.TryPlaceOnFloor(out position, out rotation, size, rules))
      return Instantiate(prefab, position, rotation);
    return null;
  }

  private void PlaceBuildings()
  {
    GameObject homeBase = null;
    GameObject besiegedBuilding = null;

    for (int i = 0; i < besiegedBuildingPrefab.Length && besiegedBuilding == null; i++)
    {
      besiegedBuilding = PlaceBuildingOnFloor(besiegedBuildingPrefab[i], null);

      for (int j = 0; j < homeBasePrefab.Length && besiegedBuilding != null && homeBase == null; j++)
      {
        homeBase = PlaceBuildingOnFloor(homeBasePrefab[j], besiegedBuilding);
      }
    }

    if (homeBase == null || besiegedBuilding == null)
      Debug.Log("ERROR: could not place buildings");
  }

  private void PlaceAgents()
  {
    PlayspaceManager pm = PlayspaceManager.Instance;

    Vector3 agent1Size = Footprint.Measure(agentPrefab1);
    float agent1Width = Mathf.Max(agent1Size.x, agent1Size.z);
    for (int i = 0; i < 3; i++)
    {
      TaskManager.Instance.Schedule(
        () =>
        {
          Vector3 position;
          if (pm.TryPlaceOnPlatform(out position, 0.25f, 1.5f, 1.5f * agent1Width))
          {
            return () =>
            {
              NavMeshHit hit;
              if (NavMesh.SamplePosition(position, out hit, 2, NavMesh.AllAreas))
                position = hit.position;
              Instantiate(agentPrefab1, position, Quaternion.identity);
            };
          }
          return null;
        });
      m_numTasksPending++;
    }

    Vector3 agent2Size = Footprint.Measure(agentPrefab2);
    float agent2Width = Mathf.Max(agent2Size.x, agent2Size.z);
    for (int i = 0; i < 1; i++)
    {
      TaskManager.Instance.Schedule(
        () =>
        {
          Vector3 position;
          if (pm.TryPlaceOnPlatform(out position, 0.25f, 1.5f, 1.5f * agent2Width))
          {
            return () =>
            {
              NavMeshHit hit;
              if (NavMesh.SamplePosition(position, out hit, 2, NavMesh.AllAreas))
                position = hit.position;
              GameObject agent = Instantiate(agentPrefab2, position, Quaternion.identity);
              agent.GetComponent<Follow>().target = Camera.main.transform;
            };
          }
          return null;
        });
      m_numTasksPending++;
    }

    Vector3 agent3Size = Footprint.Measure(agentPrefab3);
    float agent3Width = Mathf.Max(agent3Size.x, agent3Size.z);
    for (int i = 0; i < 1; i++)
    {
      TaskManager.Instance.Schedule(
        () =>
        {
          Vector3 position;
          if (pm.TryPlaceOnPlatform(out position, 0.25f, 1.5f, 1.5f * agent3Width))
          {
            return () =>
            {
              NavMeshHit hit;
              if (NavMesh.SamplePosition(position, out hit, 2, NavMesh.AllAreas))
                position = hit.position;
              GameObject agent = Instantiate(agentPrefab3, position, Quaternion.identity);
              agent.GetComponent<Follow>().target = Camera.main.transform;
            };
          }
          return null;
        });
      m_numTasksPending++;
    }
  }

  private void PlaceTunnels()
  {
    HiddenTunnel tunnel = Instantiate(tunnelPrefab) as HiddenTunnel;
    tunnel.Init();
    Vector3 placementSize = tunnel.GetPlacementDimensions();
    
    Vector3 position;
    Quaternion rotation;

    if (PlayspaceManager.Instance.TryPlaceOnFloor(out position, out rotation, placementSize))
      tunnel.Embed(position - 0.5f * Vector3.up * placementSize.y, rotation);
    else
      GameObject.Destroy(tunnel.gameObject);
  }

  public void GenerateLevel(Action OnComplete)
  {
    //PlaceBuildings();
    PlaceAgents();
    //PlaceTunnels();

    TaskManager.Instance.Schedule(
      () => 
      {
        return () =>
        {
          Debug.Log("Finished generating level.");
          OnComplete();
        };
      });
    m_numTasksPending++;
  }
}
