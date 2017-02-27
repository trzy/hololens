using UnityEngine;
using UnityEngine.VR.WSA;
using HoloToolkit.Unity;
using HoloToolkit.Unity.SpatialMapping;
using System;
using System.Collections;
using System.Collections.Generic;

public class PlayspaceManager: HoloToolkit.Unity.Singleton<PlayspaceManager>
{
  [Tooltip("Use SpatialUnderstanding instead of SpatialMapping")]
  public bool useSpatialUnderstanding = true;

  [Tooltip("Spatial Understanding mode only: Visualize the meshes produced by Spatial Understanding by inhibiting use of the occlusion material after scanning")]
  public bool visualizeSpatialUnderstandingMeshes = false;

  [Tooltip("Spatial Mapping mode only: Draw surface planes when running in Unity editor")]
  public bool planesVisibleInEditor = true;

  [Tooltip("Visualize the spatial meshes produced by Spatial Mapping by inhibiting use of the occlusion material after scanning (can be used with Spatial Understanding)")]
  public bool visualizeSpatialMeshes = false;

  [Tooltip("Render queue value to use for drawing freshly-embeded objects before spatial mesh")]
  public int highPriorityRenderQueueValue = 1000;

  [Tooltip("Spatial Mapping mode only: Remove triangles that are inside of surfaces detected by plane finding algo")]
  public bool removeSurfaceTriangles = false;

  [Tooltip("Material used for spatial mesh occlusion during game play")]
  public Material occlusionMaterial = null;

  [Tooltip("Spatial Mapping mode only: Material used spatial mesh visualization during scanning")]
  public Material renderingMaterial = null;

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
    WaitingForMeshImport
  };
  private SpatialUnderstandingState m_spatialUnderstandingState = SpatialUnderstandingState.Halted;

  private delegate bool SurfacePlaneConstraint(SurfacePlane surfacePlane);

  private List<GameObject> GetSurfacePlanes(PlaneTypes desiredTypes, SurfacePlaneConstraint ExtraConstraints, SortOrder sortOrder)
  {
    List<GameObject> planes = new List<GameObject>();
    foreach (GameObject plane in SurfaceMeshesToPlanes.Instance.ActivePlanes)
    {
      SurfacePlane surfacePlane = plane.GetComponent<SurfacePlane>();
      if ((surfacePlane.PlaneType & desiredTypes) == surfacePlane.PlaneType && ExtraConstraints(surfacePlane))
        planes.Add(plane);
    }
    planes.Sort((plane1, plane2) =>
    {
      BoundedPlane bp1 = plane1.GetComponent<SurfacePlane>().Plane;
      BoundedPlane bp2 = plane2.GetComponent<SurfacePlane>().Plane;
      // Sort descending
      int result = 0;
      switch (sortOrder)
      {
      case SortOrder.None:
        result = 0;
        break;
      case SortOrder.Descending:
        result = bp2.Area.CompareTo(bp1.Area);
        break;
      case SortOrder.Ascending:
        result = bp1.Area.CompareTo(bp2.Area);
        break;
      }
      return result;
    });
    return planes;
  }

  // SpatialMapping pathway only
  public List<GameObject> GetFloors(SortOrder sortOrder = SortOrder.None)
  {
    return GetSurfacePlanes(PlaneTypes.Floor, (surfacePlane) => surfacePlane.transform.position.y < 0 && surfacePlane.Plane.Plane.normal.y > 0, sortOrder);
  }

  // SpatialMapping pathway only
  public List<GameObject> GetPlatforms(SortOrder sortOrder = SortOrder.None)
  {
    // Only tables below eye level. SurfacePlane has the unfortunate problem
    // that it creates tables with planes oriented downwards. We ignore these
    // (but maybe we should rotate?).
    return GetSurfacePlanes(PlaneTypes.Table, (surfacePlane) => surfacePlane.transform.position.y < 0 && surfacePlane.Plane.Plane.normal.y > 0, sortOrder);
  }

  private void OnScanStateChanged()
  {
    if (m_spatialUnderstanding.ScanState == SpatialUnderstanding.ScanStates.Done)
    {
      m_spatialUnderstandingState = SpatialUnderstandingState.WaitingForMeshImport;
    }
  }

  //TODO: if time limited, take an optional callback
  public void StartScanning()
  {
    // Start spatial mapping (SpatialUnderstanding requires this, too)
    if (!m_spatialMappingManager.IsObserverRunning())
      m_spatialMappingManager.StartObserver();

    // If we are only using spatial mapping, visualize the meshes during the scanning
    // phase
    if (useSpatialUnderstanding)
    {
      m_spatialMappingManager.DrawVisualMeshes = visualizeSpatialMeshes;
      m_spatialUnderstanding.ScanStateChanged += OnScanStateChanged;
      m_spatialUnderstanding.RequestBeginScanning();
      m_spatialUnderstandingState = SpatialUnderstandingState.Scanning;
    }
    else
    {
      m_spatialMappingManager.DrawVisualMeshes = true;
    }
    m_spatialMappingManager.SetSurfaceMaterial(renderingMaterial);
  }

  public void StopScanning()
  {
    if (m_spatialMappingManager.IsObserverRunning())
      m_spatialMappingManager.StopObserver();
    if (useSpatialUnderstanding)
    {
      m_spatialUnderstandingState = SpatialUnderstandingState.FinalizeScan;
    }
    else
    {
      CreatePlanes();
      m_scanningComplete = true;
    }
  }

  private void SetPlanesVisible(bool visible)
  {
    foreach (GameObject obj in SurfaceMeshesToPlanes.Instance.ActivePlanes)
    {
      SurfacePlane plane = obj.GetComponent<SurfacePlane>();
      plane.IsVisible = visible;
    }
  }

  private void SetPlaneTags(string tag)
  {
    foreach (GameObject obj in SurfaceMeshesToPlanes.Instance.ActivePlanes)
    {
      SurfacePlane plane = obj.GetComponent<SurfacePlane>();
      plane.tag = tag;
    }
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
        m_spatialUnderstandingState = SpatialUnderstandingState.Halted;
        if (OnScanComplete != null)
          OnScanComplete();
        m_scanningComplete = true;
      }
      break;
    default:
      break;
    }
  }

  // Handler for the SurfaceMeshesToPlanes MakePlanesComplete event
  private void SurfaceMeshesToPlanes_MakePlanesComplete(object source, System.EventArgs args)
  {
    if (removeSurfaceTriangles)
      RemoveVertices(SurfaceMeshesToPlanes.Instance.ActivePlanes);
    if (!visualizeSpatialMeshes)
      m_spatialMappingManager.SetSurfaceMaterial(occlusionMaterial);
    SurfacePlaneDeformationManager.Instance.SetSpatialMeshFilters(m_spatialMappingManager.GetMeshFilters());
#if UNITY_EDITOR
    if (planesVisibleInEditor)
      SetPlanesVisible(true);
#endif
    SetPlaneTags(Layers.Instance.surfacePlaneTag);
    if (OnScanComplete != null)
      OnScanComplete();
  }

  private void CreatePlanes()
  {
    // Generate planes based on the spatial map
    SurfaceMeshesToPlanes surfaceToPlanes = SurfaceMeshesToPlanes.Instance;
    if (surfaceToPlanes != null && surfaceToPlanes.enabled)
      surfaceToPlanes.MakePlanes();
  }

  private void RemoveVertices(IEnumerable<GameObject> boundingObjects)
  {
    TriangleRemoval removeVerts = TriangleRemoval.Instance;
   // RemoveSurfaceVertices removeVerts = RemoveSurfaceVertices.Instance;
    if (removeVerts != null && removeVerts.enabled)
    {
      Debug.Log("Removing vertices from spatial meshes...");
      removeVerts.RemoveSurfaceVerticesWithinBounds(boundingObjects);
    }
  }

  private void Start()
  {
    m_spatialMappingManager = SpatialMappingManager.Instance;
    m_spatialUnderstanding = SpatialUnderstanding.Instance;
#if UNITY_EDITOR
    // An ObjectSurfaceObserver should be attached and will be used in Unity
    // editor mode to generate spatial meshes from a pre-loaded file
    m_spatialMappingManager.SetSpatialMappingSource(GetComponent<ObjectSurfaceObserver>());
#endif
    // Register for the MakePlanesComplete event
    SurfaceMeshesToPlanes.Instance.MakePlanesComplete += SurfaceMeshesToPlanes_MakePlanesComplete;
  }



  private void OnDestroy()
  {
    if (SurfaceMeshesToPlanes.Instance != null)
      SurfaceMeshesToPlanes.Instance.MakePlanesComplete -= SurfaceMeshesToPlanes_MakePlanesComplete;
  }
}
