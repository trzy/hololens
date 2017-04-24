using UnityEngine;
using UnityEngine.VR.WSA;
using HoloToolkit.Unity;
using HoloToolkit.Unity.SpatialMapping;
using System;
using System.Collections;
using System.Collections.Generic;

public class PlayspaceManager: HoloToolkit.Unity.Singleton<PlayspaceManager>
{
  [Tooltip("Spatial Understanding mode only: Visualize the meshes produced by Spatial Understanding by inhibiting use of the occlusion material after scanning")]
  public bool visualizeSpatialUnderstandingMeshes = false;

  [Tooltip("Visualize the spatial meshes produced by Spatial Mapping by inhibiting use of the occlusion material after scanning (can be used with Spatial Understanding)")]
  public bool visualizeSpatialMeshes = false;

  [Tooltip("Material used for spatial mesh occlusion during game play")]
  public Material occlusionMaterial = null;

  [Tooltip("Spatial Mapping mode only: Material used spatial mesh visualization during scanning")]
  public Material renderingMaterial = null;

  [Tooltip("Flat-shaded material (for debugging)")]
  public Material flatMaterial = null;

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

  public bool TryPlaceOnPlatform(out Vector3 position, float minHeight, float maxHeight, float minWidth)
  {
    position = Vector3.zero;
    IntPtr resultsTopologyPtr = SpatialUnderstanding.Instance.UnderstandingDLL.PinObject(m_topologyResults);
    int numResults = SpatialUnderstandingDllTopology.QueryTopology_FindLargePositionsSittable(minHeight, maxHeight, 1, minWidth, m_topologyResults.Length, resultsTopologyPtr);
    if (numResults == 0)
      return false;
    position = m_topologyResults[0].position;
    return true;
  }

  //TODO: add a function to remove individual placements based on token
  public void ClearPlacements()
  {
    SpatialUnderstandingDllObjectPlacement.Solver_RemoveAllObjects();
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
    // Start spatial mapping (SpatialUnderstanding requires this, too)
    if (!m_spatialMappingManager.IsObserverRunning())
      m_spatialMappingManager.StartObserver();
    m_spatialMappingManager.DrawVisualMeshes = visualizeSpatialMeshes;
    m_spatialUnderstanding.ScanStateChanged += OnScanStateChanged;
    m_spatialUnderstanding.RequestBeginScanning();
    m_spatialUnderstandingState = SpatialUnderstandingState.Scanning;
    m_spatialMappingManager.SetSurfaceMaterial(renderingMaterial);
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

  private void HideSpatialMappingMeshes()
  {
    var meshFilters = m_spatialMappingManager.GetMeshFilters();
    foreach (MeshFilter meshFilter in meshFilters)
    {
      meshFilter.gameObject.SetActive(false);
    }
  }

  private void SetSpatialUnderstandingMaterial(Material material)
  {
    List<MeshFilter> meshFilters = m_spatialUnderstanding.UnderstandingCustomMesh.GetMeshFilters();
    foreach (MeshFilter meshFilter in meshFilters)
    {
      meshFilter.gameObject.GetComponent<Renderer>().material = material;
    }
  }

  private void Update()
  {
    if (m_scanningComplete)
      return;
    switch (m_spatialUnderstandingState)
    {
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
          HideSpatialMappingMeshes();
        if (!visualizeSpatialUnderstandingMeshes)
          SetSpatialUnderstandingMaterial(occlusionMaterial);
        SurfacePlaneDeformationManager.Instance.SetSpatialMeshFilters(m_spatialUnderstanding.UnderstandingCustomMesh.GetMeshFilters());
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
#if UNITY_EDITOR
    // An ObjectSurfaceObserver should be attached and will be used in Unity
    // editor mode to generate spatial meshes from a pre-loaded file
    //m_spatialMappingManager.SetSpatialMappingSource(GetComponent<ObjectSurfaceObserver>());
#endif
  }

  private void OnDestroy()
  {
  }
}
