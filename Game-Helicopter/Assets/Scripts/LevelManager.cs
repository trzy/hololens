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

  public float idealDistanceApart = 4;
  public float minDistanceApart = 2;
  public float distanceStepSize = 0.25f;

  public float distanceApart = 2;

  public GameObject agentPrefab1;
  public GameObject agentPrefab2;
  public GameObject agentPrefab3;

  private int m_numTasksPending = 0;

  private void PlaceBuildings()
  {
    Vector3[] homeBaseSizes = new Vector3[homeBasePrefab.Length];
    Vector3[] besiegedBuildingSizes = new Vector3[besiegedBuildingPrefab.Length];
    for (int i = 0; i < homeBasePrefab.Length; i++)
    {
      homeBaseSizes[i] = Footprint.Measure(homeBasePrefab[i]);
    }
    for (int i = 0; i < besiegedBuildingPrefab.Length; i++)
    {
      besiegedBuildingSizes[i] = Footprint.Measure(besiegedBuildingPrefab[i]);
    }
    
    //TODO: query playspace alignment and iterate over a large radius from playspace center for first building
    //      in order to ensure that it is placed somewhere on the edge of playspace.

    TaskManager.Instance.Schedule(
      () =>
      {
        for (float distanceApart = idealDistanceApart; distanceApart >= minDistanceApart; distanceApart -= distanceStepSize)
        {
          for (int i = 0; i < besiegedBuildingPrefab.Length; i++)
          {
            for (int j = 0; j < homeBasePrefab.Length; j++)
            {
              string name1;
              string name2;
              Vector3 position1 = Vector3.zero;
              Vector3 position2 = Vector3.zero;
              Quaternion rotation1;
              Quaternion rotation2;
              bool placed1 = PlayspaceManager.Instance.TryPlaceOnFloor(out name1, out position1, out rotation1, besiegedBuildingSizes[i]);
              List<PlayspaceManager.Rule> rules = new List<PlayspaceManager.Rule>() { PlayspaceManager.Rule.AwayFrom(position1, distanceApart) };
              bool placed2 = PlayspaceManager.Instance.TryPlaceOnFloor(out name2, out position2, out rotation2, homeBaseSizes[j], rules);
              if (placed1 && placed2)
              {
                return () =>
                {
                  Debug.Log("Building placement results: i=" + i + ", j=" + j + ", distanceApart=" + distanceApart);
                  Instantiate(besiegedBuildingPrefab[i], position1, rotation1);
                  Instantiate(homeBasePrefab[j], position2, rotation2);
                };
              }
              else if (placed1)
                PlayspaceManager.Instance.RemoveObject(name1);
              else if (placed2)
                PlayspaceManager.Instance.RemoveObject(name2);
            }
          }
        }

        return () => Debug.Log("Failed to place buildings!");
      });

    //TODO: failed -- try placing anywhere and then if that also fails, need to re-scan.
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

    string name;
    Vector3 position;
    Quaternion rotation;

    if (PlayspaceManager.Instance.TryPlaceOnFloor(out name, out position, out rotation, placementSize))
      tunnel.Embed(position - 0.5f * Vector3.up * placementSize.y, rotation);
    else
      GameObject.Destroy(tunnel.gameObject);
  }

  public void GenerateLevel(Action OnComplete)
  {
    PlaceBuildings();
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
