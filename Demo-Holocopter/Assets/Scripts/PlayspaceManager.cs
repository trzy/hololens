using UnityEngine;
using UnityEngine.VR.WSA;
using HoloToolkit.Unity;
using System.Collections;
using System.Collections.Generic;

public class PlayspaceManager : MonoBehaviour
{
  public delegate void MakePlanesCompleteDelegate();

  [Tooltip("Draw surface planes when running in Unity editor")]
  public bool planesVisibleInEditor = true;

  [Tooltip("Remove triangles that are inside of surfaces detected by plane finding algo")]
  public bool removeSurfaceTriangles = false;

  public Material m_occlusion_material;
  public Material m_rendering_material;

  private uint m_scanning_time_limit = 0;
  private bool m_scanning_complete = false;
  private MakePlanesCompleteDelegate m_make_planes_complete_cb = null;

  public int GetPhysicsLayerBitmask()
  {
    return 1 << SpatialMappingManager.Instance.PhysicsLayer;
  }

  public List<GameObject> GetTables()
  {
    PlaneTypes desired_types = PlaneTypes.Table;
    List<GameObject> planes = new List<GameObject>();
    foreach (GameObject plane in SurfaceMeshesToPlanes.Instance.ActivePlanes)
    {
      SurfacePlane surfacePlane = plane.GetComponent<SurfacePlane>();
      // Only tables below eye level. SurfacePlane has the unfortunate problem
      // that it creates tables with planes oriented downwards. We ignore these
      // (but maybe we should rotate?).
      if ((surfacePlane.PlaneType & desired_types) == surfacePlane.PlaneType)
        Debug.Log("Found: " + surfacePlane.transform.position.ToString() + ", " + surfacePlane.Plane.Plane.normal.ToString("F2"));
      if ((surfacePlane.PlaneType & desired_types) == surfacePlane.PlaneType && surfacePlane.transform.position.y < 0 && surfacePlane.Plane.Plane.normal.y > 0)
        planes.Add(plane);
    }
    return planes;
  }

  public void SetMakePlanesCompleteCallback(MakePlanesCompleteDelegate cb)
  {
    m_make_planes_complete_cb = cb;
  }

  //TODO: if time limited, take an optional callback
  public void StartScanning(uint scanning_time_limit = 0)
  {
    m_scanning_time_limit = scanning_time_limit;
    if (!SpatialMappingManager.Instance.IsObserverRunning())
      SpatialMappingManager.Instance.StartObserver();
    SpatialMappingManager.Instance.SetSurfaceMaterial(m_rendering_material);
  }

  public void StopScanning()
  {
    if (SpatialMappingManager.Instance.IsObserverRunning())
      SpatialMappingManager.Instance.StopObserver();
    CreatePlanes();
    m_scanning_complete = true;
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

  private void Update()
  {
    if (m_scanning_complete)
      return;
    // If no time limit, or if time limit but still time left, keep scanning
    if (m_scanning_time_limit == 0 || (Time.time - SpatialMappingManager.Instance.StartTime) < m_scanning_time_limit)
      return;
    StopScanning();
  }

  // Handler for the SurfaceMeshesToPlanes MakePlanesComplete event
  private void SurfaceMeshesToPlanes_MakePlanesComplete(object source, System.EventArgs args)
  {
    if (removeSurfaceTriangles)
      RemoveVertices(SurfaceMeshesToPlanes.Instance.ActivePlanes);
    SpatialMappingManager.Instance.SetSurfaceMaterial(m_occlusion_material);
    if (m_make_planes_complete_cb != null)
      m_make_planes_complete_cb();
#if UNITY_EDITOR
    if (planesVisibleInEditor)
      SetPlanesVisible(true);
#endif
    SetPlaneTags(Layers.Instance.surfacePlaneTag);
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
    //RemoveSurfaceVertices removeVerts = RemoveSurfaceVertices.Instance;
    if (removeVerts != null && removeVerts.enabled)
      removeVerts.RemoveSurfaceVerticesWithinBounds(boundingObjects);
  }

  void Awake()
  {
  }

  void Start()
  {
#if UNITY_EDITOR
    // An ObjectSurfaceObserver should be attached and will be used in Unity
    // editor mode to generate spatial meshes from a pre-loaded file
    SpatialMappingManager.Instance.SetSpatialMappingSource(GetComponent<ObjectSurfaceObserver>());
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
