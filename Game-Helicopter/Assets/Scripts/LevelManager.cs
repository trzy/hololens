using System;
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
    Vector3 agentSize = Footprint.Measure(agentPrefab1);
    float agentWidth = Mathf.Max(agentSize.x, agentSize.z);
    Vector3 position;
    NavMeshHit hit;
    for (int i = 0; i < 3; i++)
    {
      if (PlayspaceManager.Instance.TryPlaceOnPlatform(out position, 0.25f, 1.5f, 1.5f * agentWidth))
      {
        if (NavMesh.SamplePosition(position, out hit, 2, NavMesh.AllAreas))
          position = hit.position;
        Instantiate(agentPrefab1, position, Quaternion.identity);
      }
    }

    agentSize = Footprint.Measure(agentPrefab2);
    agentWidth = Mathf.Max(agentSize.x, agentSize.z);
    for (int i = 0; i < 1; i++)
    {
      if (PlayspaceManager.Instance.TryPlaceOnPlatform(out position, 0.25f, 1.5f, 1.5f * agentWidth))
      {
        if (NavMesh.SamplePosition(position, out hit, 2, NavMesh.AllAreas))
          position = hit.position;
        GameObject agent = Instantiate(agentPrefab2, position, Quaternion.identity);
        agent.GetComponent<Follow>().target = Camera.main.transform;
      }
    }

    agentSize = Footprint.Measure(agentPrefab3);
    agentWidth = Mathf.Max(agentSize.x, agentSize.z);
    for (int i = 0; i < 1; i++)
    {
      if (PlayspaceManager.Instance.TryPlaceOnPlatform(out position, 0.25f, 1.5f, 1.5f * agentWidth))
      {
        if (NavMesh.SamplePosition(position, out hit, 2, NavMesh.AllAreas))
          position = hit.position;
        GameObject agent = Instantiate(agentPrefab3, position, Quaternion.identity);
        agent.GetComponent<Follow>().target = Camera.main.transform;
      }
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

  public void GenerateLevel()
  {
    //PlaceBuildings();
    PlaceAgents();
    PlaceTunnels();
  }

	public LevelManager()
	{
	}
}
