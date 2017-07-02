/*
 * This is the main script of the surface plane deformation demo. It allows
 * small bullet holes to be placed in walls, ceilings, and floors. 
 * 
 * Instructions
 * ------------
 * - Upon start-up, the room is mapped and the spatial mesh is drawn as a wire-
 *   frame. When you are pleased with the results (walls should look flat), air
 *   tap to begin. The spatial meshes will disappear and you will enter the 
 *   "play" state.
 * - In the play state, air tapping on walls, floors, and ceilings should place
 *   bullet holes.
 *   
 * In the Unity editor
 * -------------------
 * - Press Enter key to simulate air tap.
 * - See EditorMotionControls.cs for keyboard mappings to allow movement around
 *   the room.
 *
 * Additional features
 * -------------------
 * - Spatial meshes can be visualized during play mode by deploying with
 *   "Visualize Spatial Meshes" checked in the inspector.
 * - Similarly, "Visualize Surface Planes" will render the surface planes that
 *   are detected.
 *
 * Known issues
 * ------------
 * - Sometimes bullets are not placed despite the presence of a surface plane.
 *   This is because the ray we cast intersected with the actual spatial mesh
 *   first. In practice, one may want to determine whether a surface plane is
 *   "close enough" to the vicinity of the hit point, expand the thickness of
 *   the surface planes, or use some other method to detect walls.
 */

using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.VR.WSA;
using UnityEngine.VR.WSA.Input;
using HoloToolkit.Unity.SpatialMapping;

public class SurfacePlaneDeformationController: MonoBehaviour
{
  [Tooltip("Prefab for bullet hole decal.")]
  public GameObject m_bulletHolePrefab;

  [Tooltip("Used to visualize spatial meshes while scanning")]
  public Material spatialMeshScanningMaterial;

  [Tooltip("Used for spatial mesh occlusion while playing")]
  public Material spatialMeshOcclusionMaterial;

  [Tooltip("Continue to visualize spatial meshes when playing")]
  public bool visualizeSpatialMeshes = false;

  [Tooltip("Draw detected surface planes")]
  public bool visualizeSurfacePlanes = false;

  enum State
  {
    Scanning,
    Playing
  };
  private State m_state;
  private GestureRecognizer m_gestureRecognizer;

  private void SetPlanesVisible(bool visible)
  {
    foreach (GameObject obj in SurfaceMeshesToPlanes.Instance.ActivePlanes)
    {
      SurfacePlane plane = obj.GetComponent<SurfacePlane>();
      plane.IsVisible = visible;
    }
  }

  private void OnPlanesComplete(object source, System.EventArgs args)
  {
    SpatialMappingManager mapper = SpatialMappingManager.Instance;
    if (!visualizeSpatialMeshes)
    {
      mapper.SetSurfaceMaterial(spatialMeshOcclusionMaterial);
    }
    SurfacePlaneDeformationManager.Instance.SetSpatialMeshFilters(mapper.GetMeshFilters());
    if (visualizeSurfacePlanes)
    {
      SetPlanesVisible(true);
    }
  }

  private void CreateBulletHole(Vector3 position, Vector3 normal, SurfacePlane plane)
  {
    GameObject bulletHole = Instantiate(m_bulletHolePrefab, position, Quaternion.LookRotation(normal)) as GameObject;
    bulletHole.AddComponent<WorldAnchor>(); // does this do anything?
    bulletHole.transform.parent = this.transform;
    OrientedBoundingBox obb = OBBMeshIntersection.CreateWorldSpaceOBB(bulletHole.GetComponent<BoxCollider>());
    SurfacePlaneDeformationManager.Instance.Embed(bulletHole, obb, plane);
  }

  private void OnTapEvent(InteractionSourceKind source, int tap_count, Ray head_ray)
  {
    switch (m_state)
    {
      case State.Scanning:
        // Stop scanning and detect planes
        if (SpatialMappingManager.Instance.IsObserverRunning())
        {
          SpatialMappingManager.Instance.StopObserver();
        }
        SurfaceMeshesToPlanes planeDetector = SurfaceMeshesToPlanes.Instance;
        planeDetector.MakePlanesComplete += OnPlanesComplete;
        planeDetector.MakePlanes();
        m_state = State.Playing;
        break;
      case State.Playing:
        RaycastHit hit;
        if (Physics.Raycast(Camera.main.transform.position, Camera.main.transform.forward, out hit))
        {
          // If a wall, floor, or ceiling was hit, embed a bullet hole
          GameObject target = hit.collider.gameObject;
          Debug.Log("Hit: " + target.name);
          SurfacePlane plane = target.GetComponent<SurfacePlane>();
          if (plane != null)
          {
            if (plane.PlaneType == PlaneTypes.Ceiling ||
              plane.PlaneType == PlaneTypes.Floor ||
              plane.PlaneType == PlaneTypes.Wall)
            {
              CreateBulletHole(hit.point, hit.normal, plane);
            }
          }
        }
        break;
    }
  }

  private void Update()
  {
#if UNITY_EDITOR
    // Simulate air tap with Enter key
    if (Input.GetKeyDown(KeyCode.Return))
    {
      Ray head_ray = new Ray(transform.position, transform.forward);
      OnTapEvent(InteractionSourceKind.Hand, 1, head_ray);
    }
#endif
  }

  private void Start()
  {
    // Start scanning
    m_state = State.Scanning;
    SpatialMappingManager mapper = SpatialMappingManager.Instance;
    if (!mapper.IsObserverRunning())
    {
      mapper.StartObserver();
    }
    mapper.SetSurfaceMaterial(spatialMeshScanningMaterial);

    // Subscribe to tap gesture
    m_gestureRecognizer = new GestureRecognizer();
    m_gestureRecognizer.SetRecognizableGestures(GestureSettings.Tap);
    m_gestureRecognizer.TappedEvent += OnTapEvent;
    m_gestureRecognizer.StartCapturingGestures();
  }

  private void OnDestroy()
  {
    if (SurfaceMeshesToPlanes.Instance != null)
    {
      SurfaceMeshesToPlanes.Instance.MakePlanesComplete -= OnPlanesComplete;
    }
  }
}
