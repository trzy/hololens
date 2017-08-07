//TODO: Rewrite PlayspaceManager Shuffle() to be thread-safe using a non-Unity randomizer.
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;
using HoloToolkit.Unity;

public class LevelManager: HoloToolkit.Unity.Singleton<LevelManager>
{
  public GuidanceArrow guidanceArrowPrefab;
  public HUDIndicator hudIndicatorPrefab;
  public NarrationBox narrationObject;
  public TextMesh narrationTextMesh;

  [System.Serializable]
  public class NarrationLine: System.Object
  {
    [TextAreaAttribute(5,10)]
    public string text = "The terrorists have cut off all access to the research facility!";
    public AudioClip clip = null;
  }

  public NarrationLine[] openingNarration;
  public NarrationLine[] besiegedBuildingNarration;
  public NarrationLine[] homeBaseNarration;

  public HiddenTunnel tunnelPrefab;
  public GameObject[] homeBasePrefab;
  public GameObject[] besiegedBuildingPrefab;

  public float idealDistanceApart = 4;
  public float minDistanceApart = 2;
  public float distanceStepSize = 0.25f;

  public GameObject agentPrefab1;
  public GameObject agentPrefab2;
  public GameObject agentPrefab3;

  private GameObject m_besiegedBuilding;
  private GameObject m_homeBase;

  private enum State
  {
    None,
    GenerateLevel,
    MissionBriefing
  }

  private State m_state = State.None;
  private GuidanceArrow m_guidanceArrow;
  private HUDIndicator m_hudIndicator;

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
                  m_besiegedBuilding = Instantiate(besiegedBuildingPrefab[i], position1, rotation1);
                  m_homeBase = Instantiate(homeBasePrefab[j], position2, rotation2);
                };
              }
              else if (placed1)
                PlayspaceManager.Instance.RemoveObject(name2);
              else if (placed2)
                PlayspaceManager.Instance.RemoveObject(name1);
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
    m_state = State.GenerateLevel;

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
  }

  private void FixedUpdate()
  {
    if (m_state == State.MissionBriefing)
    {

    }
  }

  private IEnumerator Wait(float seconds, Action OnTimeReached)
  {
    float start = Time.time;
    Debug.Log("Starting Wait coroutine...");
    yield return new WaitForSeconds(seconds);
    Debug.Log("Finished Wait coroutine!");
    OnTimeReached();
  }

  private void StartHomeBaseNarration()
  {
    m_guidanceArrow.target = m_homeBase.transform;
    m_guidanceArrow.gameObject.SetActive(true);
    int lineIdx = 0;
    Action NextLineCallback = null;
    NextLineCallback = () =>
    {
      if (lineIdx >= homeBaseNarration.Length)
      {
        narrationObject.gameObject.SetActive(false);
        m_hudIndicator.gameObject.SetActive(false);
        return;
      }
      narrationObject.SetLine(homeBaseNarration[lineIdx].text, homeBaseNarration[lineIdx].clip, NextLineCallback);
      ++lineIdx;
    };

    m_guidanceArrow.OnTargetAppeared = () =>
    {
      narrationObject.gameObject.SetActive(true);
      NextLineCallback();
      m_guidanceArrow.gameObject.SetActive(false);
      m_hudIndicator.target = m_homeBase.transform;
      m_hudIndicator.gameObject.SetActive(true);
    };

    m_guidanceArrow.OnTargetDisappeared = () =>
    {
    };
  }

  private void StartBesiegedBuildingNarration()
  {
    m_guidanceArrow.target = m_besiegedBuilding.transform;
    m_guidanceArrow.gameObject.SetActive(true);
    int lineIdx = 0;
    Action NextLineCallback = null;
    NextLineCallback = () =>
    {
      if (lineIdx >= besiegedBuildingNarration.Length)
      {
        narrationObject.gameObject.SetActive(false);
        m_hudIndicator.gameObject.SetActive(false);
        StartHomeBaseNarration();
        return;
      }
      narrationObject.SetLine(besiegedBuildingNarration[lineIdx].text, besiegedBuildingNarration[lineIdx].clip, NextLineCallback);
      ++lineIdx;
    };

    m_guidanceArrow.OnTargetAppeared = () =>
    {
      narrationObject.gameObject.SetActive(true);
      NextLineCallback();
      m_guidanceArrow.gameObject.SetActive(false);
      m_hudIndicator.target = m_besiegedBuilding.transform;
      m_hudIndicator.gameObject.SetActive(true);
    };

    m_guidanceArrow.OnTargetDisappeared = () =>
    {
    };
  }

  private void StartOpeningNarration()
  {
    narrationObject.gameObject.SetActive(true);
    int lineIdx = 0;
    Action NextLineCallback = null;
    NextLineCallback = () =>
    {
      if (lineIdx >= openingNarration.Length)
      {
        narrationObject.gameObject.SetActive(false);
        StartBesiegedBuildingNarration();
        return;
      }
      narrationObject.SetLine(openingNarration[lineIdx].text, openingNarration[lineIdx].clip, NextLineCallback);
      ++lineIdx;
    };
    NextLineCallback();
  }

  public void StartIntroSequence()
  {
    //TODO: need to add a setter to GuidanceArrow for target because if old target is visible and new target is visible, OnTargetAppeared will never be triggered!

    m_state = State.MissionBriefing;
    StartOpeningNarration();
  }

  private void Start()
  {
    narrationObject.gameObject.SetActive(false);
    m_guidanceArrow = Instantiate(guidanceArrowPrefab) as GuidanceArrow;
    m_guidanceArrow.gameObject.SetActive(false);
    m_hudIndicator = Instantiate(hudIndicatorPrefab) as HUDIndicator;
    m_hudIndicator.gameObject.SetActive(false);
  }
}
