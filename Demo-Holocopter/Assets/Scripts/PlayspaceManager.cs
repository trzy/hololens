using UnityEngine;
using UnityEngine.VR.WSA;
using HoloToolkit.Unity;
using System.Collections;
using System.Collections.Generic;

public class PlayspaceManager : MonoBehaviour
{
  public delegate void MakePlanesCompleteDelegate();

  public Material m_occlusion_material;
  public Material m_rendering_material;

  private uint m_scanning_time_limit = 0;
  private bool m_scanning_complete = false;
  private MakePlanesCompleteDelegate m_make_planes_complete_cb = null;

  public List<GameObject> GetFloors()
  {
    return SurfaceMeshesToPlanes.Instance.GetActivePlanes(PlaneTypes.Table);
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
    //RemoveVertices(SurfaceMeshesToPlanes.Instance.ActivePlanes);
    SpatialMappingManager.Instance.SetSurfaceMaterial(m_occlusion_material);
    if (m_make_planes_complete_cb != null)
      m_make_planes_complete_cb();
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
    RemoveSurfaceVertices removeVerts = RemoveSurfaceVertices.Instance;
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
