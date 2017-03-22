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

  public enum SortOrder
  {
    None,
    Descending,
    Ascending
  };

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

  private struct PlacementQuery
  {
    public SpatialUnderstandingDllObjectPlacement.ObjectPlacementDefinition placementDefinition;
    public List<SpatialUnderstandingDllObjectPlacement.ObjectPlacementRule> placementRules;
    public List<SpatialUnderstandingDllObjectPlacement.ObjectPlacementConstraint> placementConstraints;

    public PlacementQuery(
        SpatialUnderstandingDllObjectPlacement.ObjectPlacementDefinition placementDefinition_,
        List<SpatialUnderstandingDllObjectPlacement.ObjectPlacementRule> placementRules_ = null,
        List<SpatialUnderstandingDllObjectPlacement.ObjectPlacementConstraint> placementConstraints_ = null)
    {
      placementDefinition = placementDefinition_;
      placementRules = placementRules_;
      placementConstraints = placementConstraints_;
    }
  }

  private bool TryPlaceObject(
    out SpatialUnderstandingDllObjectPlacement.ObjectPlacementResult placementResult,
    string placementName,
    PlacementQuery query)
  {
    placementResult = null;
    int result = SpatialUnderstandingDllObjectPlacement.Solver_PlaceObject(
      placementName,
      SpatialUnderstanding.Instance.UnderstandingDLL.PinObject(query.placementDefinition),
        (query.placementRules != null) ? query.placementRules.Count : 0,
        ((query.placementRules != null) && (query.placementRules.Count > 0)) ? SpatialUnderstanding.Instance.UnderstandingDLL.PinObject(query.placementRules.ToArray()) : IntPtr.Zero,
        (query.placementConstraints != null) ? query.placementConstraints.Count : 0,
        ((query.placementConstraints != null) && (query.placementConstraints.Count > 0)) ? SpatialUnderstanding.Instance.UnderstandingDLL.PinObject(query.placementConstraints.ToArray()) : IntPtr.Zero,
        SpatialUnderstanding.Instance.UnderstandingDLL.GetStaticObjectPlacementResultPtr());
    if (result > 0)
    {
      placementResult = SpatialUnderstanding.Instance.UnderstandingDLL.GetStaticObjectPlacementResult();
      return true;
    }
    return false;
  }

  public bool TryPlaceOnFloor(out Vector3 position, Vector3 size, Vector3 nearPosition, float minDistance, float maxDistance, float minDistanceFromOtherObjects = 0.25f)
  {
    position = Vector3.zero;
    PlacementQuery query = new PlacementQuery(
      SpatialUnderstandingDllObjectPlacement.ObjectPlacementDefinition.Create_OnFloor(0.5f * size),
      minDistanceFromOtherObjects <= 0 ? null : new List<SpatialUnderstandingDllObjectPlacement.ObjectPlacementRule>()
      {
        SpatialUnderstandingDllObjectPlacement.ObjectPlacementRule.Create_AwayFromOtherObjects(minDistanceFromOtherObjects)
      },
      new List<SpatialUnderstandingDllObjectPlacement.ObjectPlacementConstraint>()
      {
        SpatialUnderstandingDllObjectPlacement.ObjectPlacementConstraint.Create_NearPoint(nearPosition, minDistance, maxDistance)
      });
    SpatialUnderstandingDllObjectPlacement.ObjectPlacementResult placementResult;
    if (TryPlaceObject(out placementResult, "changeMeToSomethingUnique", query))
    {
      position = placementResult.Position;
      return true;
    }
    return false;
  }

  public void SpawnObject(GameObject prefab, Vector3 position)
  {
    GameObject obj = Instantiate(prefab) as GameObject;
    obj.transform.parent = gameObject.transform;
    obj.transform.position = position;
    obj.SetActive(true);
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
