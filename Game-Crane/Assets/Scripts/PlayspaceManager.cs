using UnityEngine;
using UnityEngine.VR.WSA;
using HoloToolkit.Unity;
using HoloToolkit.Unity.SpatialMapping;
using System;
using System.Collections;
using System.Collections.Generic;

public class PlayspaceManager: HoloToolkit.Unity.Singleton<PlayspaceManager>
{
  public enum VisualizationMode
  {
    None,
    Occluding,
    Visible
  }

  [Tooltip("How to render Spatial Understanding meshes after scanning (or None to disable rendering).")]
  public VisualizationMode visualizationMode = VisualizationMode.Occluding;

  [Tooltip("Visualize the spatial meshes produced by Spatial Mapping by inhibiting use of the occlusion material after scanning (can be used with Spatial Understanding)")]
  public bool visualizeSpatialMeshes = false;

  [Tooltip("Material used for spatial mesh occlusion during game play")]
  public Material occlusionMaterial = null;

  [Tooltip("Spatial Mapping mode only: Material used spatial mesh visualization during scanning")]
  public Material renderingMaterial = null;

  // Called when scanning is complete and playspace is finalized
  public Action OnScanComplete = null;

  public struct Rule
  {
    public enum TypeFlags
    {
      None = 0,
      Rule = 1,
      Constraint = 2,
      Both = 3
    }

    SpatialUnderstandingDllObjectPlacement.ObjectPlacementRule rule;
    SpatialUnderstandingDllObjectPlacement.ObjectPlacementConstraint constraint;
    byte type;

    public void AddTo(List<SpatialUnderstandingDllObjectPlacement.ObjectPlacementRule> rules)
    {
      if ((type & (byte)TypeFlags.Rule) != 0)
        rules.Add(rule);
    }

    public void AddTo(List<SpatialUnderstandingDllObjectPlacement.ObjectPlacementConstraint> constraints)
    {
      if ((type & (byte)TypeFlags.Constraint) != 0)
        constraints.Add(constraint);
    }

    public static Rule AwayFromOthers(float minDistanceFromOtherObjects)
    {
      Rule rule = new Rule();
      rule.rule = SpatialUnderstandingDllObjectPlacement.ObjectPlacementRule.Create_AwayFromOtherObjects(minDistanceFromOtherObjects);
      rule.type = (byte)TypeFlags.Rule;
      return rule;
    }

    public static Rule Nearby(Vector3 position, float minDistance = 0, float maxDistance = 0)
    {
      Rule rule = new Rule();
      rule.constraint = SpatialUnderstandingDllObjectPlacement.ObjectPlacementConstraint.Create_NearPoint(position, minDistance, maxDistance);
      rule.type = (byte)TypeFlags.Constraint;
      return rule;
    }

    public static Rule AwayFrom(Vector3 position, float minDistance = 0)
    {
      Rule rule = new Rule();
      rule.rule = SpatialUnderstandingDllObjectPlacement.ObjectPlacementRule.Create_AwayFromPosition(position, minDistance);
      rule.type = (byte)TypeFlags.Rule;
      return rule;
    }
  }

  private SpatialMappingManager m_spatialMappingManager = null;
  private SpatialUnderstanding m_spatialUnderstanding = null;
  private bool m_scanningComplete = false;

  private enum SpatialUnderstandingState
  {
    Halted,
    StartScanning,
    Scanning,
    FinalizeScan,
    WaitingForScanCompletion,
    WaitingForMeshImport,
    WaitingForPlacementSolverInit,
    Finished
  }

  private SpatialUnderstandingState m_spatialUnderstandingState = SpatialUnderstandingState.Halted;
  private bool m_placementSolverInitialized = false;
  private int m_uniquePlacementID = 0;
  private SpatialUnderstandingDllTopology.TopologyResult[] m_topologyResults = new SpatialUnderstandingDllTopology.TopologyResult[2048];
  private List<Tuple<Vector3, Vector3>> m_platformPlacements = new List<Tuple<Vector3, Vector3>>();

  private bool TryPlaceObject(
    out SpatialUnderstandingDllObjectPlacement.ObjectPlacementResult placementResult,
    string placementName,
    SpatialUnderstandingDllObjectPlacement.ObjectPlacementDefinition placementDefinition,
    List<Rule> rules = null)
  {
    placementResult = null;
    List<SpatialUnderstandingDllObjectPlacement.ObjectPlacementRule> placementRules = new List<SpatialUnderstandingDllObjectPlacement.ObjectPlacementRule>();
    List<SpatialUnderstandingDllObjectPlacement.ObjectPlacementConstraint> placementConstraints = new List<SpatialUnderstandingDllObjectPlacement.ObjectPlacementConstraint>();
    if (rules != null)
    {
      foreach (Rule rule in rules)
      {
        rule.AddTo(placementRules);
        rule.AddTo(placementConstraints);
      }
    }
    int result = SpatialUnderstandingDllObjectPlacement.Solver_PlaceObject(
      placementName,
      SpatialUnderstanding.Instance.UnderstandingDLL.PinObject(placementDefinition),
      placementRules.Count,
      placementRules.Count > 0 ? SpatialUnderstanding.Instance.UnderstandingDLL.PinObject(placementRules.ToArray()) : IntPtr.Zero,
      placementConstraints.Count,
      placementConstraints.Count > 0 ? SpatialUnderstanding.Instance.UnderstandingDLL.PinObject(placementConstraints.ToArray()) : IntPtr.Zero,
      SpatialUnderstanding.Instance.UnderstandingDLL.GetStaticObjectPlacementResultPtr());
    if (result > 0)
    {
      placementResult = SpatialUnderstanding.Instance.UnderstandingDLL.GetStaticObjectPlacementResult();
      return true;
    }
    return false;
  }

  public bool TryPlaceOnFloor(out Vector3 position, Vector3 size, List<Rule> rules = null)
  {
    position = Vector3.zero;
    string token = "Floor-" + m_uniquePlacementID++;
    SpatialUnderstandingDllObjectPlacement.ObjectPlacementDefinition placementDefinition = SpatialUnderstandingDllObjectPlacement.ObjectPlacementDefinition.Create_OnFloor(0.5f * size);
    SpatialUnderstandingDllObjectPlacement.ObjectPlacementResult placementResult;
    if (TryPlaceObject(out placementResult, token, placementDefinition, rules))
    {
      position = placementResult.Position;
      return true;
    }
    return false;
  }

  public bool TryPlaceOnPlatformEdge(out Vector3 position, Vector3 size, List<Rule> rules = null)
  {
    position = Vector3.zero;
    string token = "Edge-" + m_uniquePlacementID++;
    //TODO: unsure of what halfDimsBottom really means
    SpatialUnderstandingDllObjectPlacement.ObjectPlacementDefinition placementDefinition = SpatialUnderstandingDllObjectPlacement.ObjectPlacementDefinition.Create_OnEdge(0.5f * size, 0.5f * new Vector3(size.x, size.y, size.z));
    SpatialUnderstandingDllObjectPlacement.ObjectPlacementResult placementResult;
    if (TryPlaceObject(out placementResult, token, placementDefinition, rules))
    {
      position = placementResult.Position;
      return true;
    }
    return false;
  }

  // We only crudely check overlap in xz plane
  private bool Overlaps(SpatialUnderstandingDllTopology.TopologyResult result, float width)
  {
    float halfWidth = 0.5f * width;
    foreach (Tuple<Vector3, Vector3> placement in m_platformPlacements)
    {
      Vector3 position = placement.first;
      Vector3 halfSize = 0.5f * placement.second;
      if (result.position.x + halfWidth < (position.x - halfSize.x))
        continue;
      if (result.position.x - halfWidth > (position.x + halfSize.x))
        continue;
      if (result.position.x + halfWidth < (position.x - halfSize.x))
        continue;
      if (result.position.z - halfWidth > (position.z + halfSize.z))
        continue;
      if (result.position.z + halfWidth < (position.z - halfSize.z))
        continue;
      return true;
    }
    return false;
  }

  private void Shuffle(int[] a)
  {
    for (int i = 0; i < a.Length; i++)
    {
      int tmp = a[i];
      int r = UnityEngine.Random.Range(0, a.Length);
      a[i] = a[r];
      a[r] = tmp;
    }
  }

  public bool TryPlaceOnPlatform(out Vector3 position, float minHeight, float maxHeight, float minWidth)
  {
    position = Vector3.zero;
    IntPtr resultsTopologyPtr = SpatialUnderstanding.Instance.UnderstandingDLL.PinObject(m_topologyResults);
    int numResults = SpatialUnderstandingDllTopology.QueryTopology_FindLargePositionsSittable(minHeight, maxHeight, 1, minWidth, m_topologyResults.Length, resultsTopologyPtr);
    if (numResults == 0)
      return false;

    // Pick randomly by shuffling indices
    int[] placementIdxs = new int[numResults];
    for (int i = 0; i < placementIdxs.Length; i++)
    {
      placementIdxs[i] = i;
    }
    Shuffle(placementIdxs);

    // We have to keep track of our own placements
    foreach (int idx in placementIdxs)
    {
      SpatialUnderstandingDllTopology.TopologyResult result = m_topologyResults[idx];
      if (!Overlaps(result, minWidth))
      {
        position = result.position;
        Vector3 size = new Vector3(minWidth, 0, minWidth);  // we ignore height for now
        m_platformPlacements.Add(new Tuple<Vector3, Vector3>(position, size));
        return true;
      }
    }
    return false;
  }

  //TODO: add a function to remove individual placements based on token
  public void ClearPlacements()
  {
    SpatialUnderstandingDllObjectPlacement.Solver_RemoveAllObjects();
    m_platformPlacements.Clear();
  }

  public bool IsSpatialLayer(GameObject obj)
  { 
    return obj.layer == HoloToolkit.Unity.SpatialMapping.SpatialMappingManager.Instance.PhysicsLayer;
  }

  private void OnScanStateChanged()
  {
    if (m_spatialUnderstanding.ScanState == SpatialUnderstanding.ScanStates.Done)
    {
      m_spatialUnderstandingState = SpatialUnderstandingState.WaitingForMeshImport;
    }
  }

  public void StartScanning()
  {
    // So that this can be called from within another object's Start() method
    // (possibly before our own Start() function is called)
    m_spatialUnderstandingState = SpatialUnderstandingState.StartScanning;
  }

  public void StopScanning()
  {
    if (m_spatialMappingManager.IsObserverRunning())
      m_spatialMappingManager.StopObserver();
    m_spatialUnderstandingState = SpatialUnderstandingState.FinalizeScan;
  }

  public bool IsScanningComplete()
  {
    return m_scanningComplete;
  }

  // Completely disable spatial meshes
  private void DisableSpatialMappingMeshes()
  {
    var meshFilters = m_spatialMappingManager.GetMeshFilters();
    foreach (MeshFilter meshFilter in meshFilters)
    {
      meshFilter.gameObject.SetActive(false);
    }
  }

  // Turn off rendering of spatial understanding meshes
  private void HideSpatialUnderstandingMeshes()
  {
    List<MeshFilter> meshFilters = m_spatialUnderstanding.UnderstandingCustomMesh.GetMeshFilters();
    foreach (MeshFilter meshFilter in meshFilters)
    {
      meshFilter.gameObject.GetComponent<MeshRenderer>().enabled = false;
    }
  }

  private void SetSpatialUnderstandingMaterial(Material material)
  {
    List<MeshFilter> meshFilters = m_spatialUnderstanding.UnderstandingCustomMesh.GetMeshFilters();
    foreach (MeshFilter meshFilter in meshFilters)
    {
      meshFilter.gameObject.GetComponent<Renderer>().sharedMaterial = material;
    }
  }

  private void ApplyVisualizationSettings()
  {
    bool staticBatching = true;

    switch (visualizationMode)
    {
      case VisualizationMode.None:
        staticBatching = false;
        HideSpatialUnderstandingMeshes();
        break;
      case VisualizationMode.Occluding:
        SetSpatialUnderstandingMaterial(occlusionMaterial);
        break;
      case VisualizationMode.Visible:
        SetSpatialUnderstandingMaterial(renderingMaterial);
        break;
    }

    if (staticBatching)
    {
      List<MeshFilter> meshFilters = m_spatialUnderstanding.UnderstandingCustomMesh.GetMeshFilters();
      if (meshFilters.Count > 0)
      {
        GameObject root = meshFilters[0].transform.parent.gameObject;
        StaticBatchingUtility.Combine(root);
      }
    }
  }

  private void Update()
  {
    if (m_scanningComplete)
      return;
    switch (m_spatialUnderstandingState)
    {
      case SpatialUnderstandingState.StartScanning:
        // Start spatial mapping (SpatialUnderstanding requires this, too)
        if (!m_spatialMappingManager.IsObserverRunning())
          m_spatialMappingManager.StartObserver();
        m_spatialMappingManager.DrawVisualMeshes = visualizeSpatialMeshes;
        m_spatialUnderstanding.ScanStateChanged += OnScanStateChanged;
        m_spatialUnderstanding.RequestBeginScanning();
        m_spatialMappingManager.SetSurfaceMaterial(renderingMaterial);
        m_spatialUnderstandingState = SpatialUnderstandingState.Scanning;
        break;
      case SpatialUnderstandingState.FinalizeScan:
        //TODO: timeout?
        if (!m_spatialUnderstanding.ScanStatsReportStillWorking)
        {
          Debug.Log("Finalizing scan...");
          m_spatialUnderstanding.RequestFinishScan();
          m_spatialUnderstandingState = SpatialUnderstandingState.WaitingForScanCompletion;
        }
        break;
      case SpatialUnderstandingState.WaitingForMeshImport:
        //TODO: timeout?
        if (m_spatialUnderstanding.UnderstandingCustomMesh.IsImportActive == false)
        {
          Debug.Log("Found " + m_spatialUnderstanding.UnderstandingCustomMesh.GetMeshFilters().Count + " meshes (import active=" + m_spatialUnderstanding.UnderstandingCustomMesh.IsImportActive + ")");
          if (!visualizeSpatialMeshes)
            DisableSpatialMappingMeshes();
          ApplyVisualizationSettings();
          //SurfacePlaneDeformationManager.Instance.SetSpatialMeshFilters(m_spatialUnderstanding.UnderstandingCustomMesh.GetMeshFilters());
          m_spatialUnderstandingState = SpatialUnderstandingState.WaitingForPlacementSolverInit;
        }
        break;
      case SpatialUnderstandingState.WaitingForPlacementSolverInit:
        //TODO: error checking and timeout?
        if (!m_placementSolverInitialized)
        {
          m_placementSolverInitialized = (SpatialUnderstandingDllObjectPlacement.Solver_Init() == 1);
          Debug.Log("Placement Solver initialization " + (m_placementSolverInitialized ? "succeeded" : "FAILED"));
          if (m_placementSolverInitialized)
          {
            if (OnScanComplete != null)
              OnScanComplete();
            m_scanningComplete = true;
            m_spatialUnderstandingState = SpatialUnderstandingState.Finished;
          }
        }
        break;
      default:
        break;
    }
  }

  private void Start()
  {
    m_spatialMappingManager = SpatialMappingManager.Instance;
    m_spatialUnderstanding = SpatialUnderstanding.Instance;
  }

  // So that PlayspaceManager is available by the time Start() is called for
  // game objects
  protected override void Awake()
  {
    base.Awake();
  }

  private void OnDestroy()
  {
  }
}
