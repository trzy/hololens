using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.VR.WSA.Input;
using HoloToolkit.Unity;
using HoloToolkit.Unity.SpatialMapping;

public class Demo: MonoBehaviour
{
  [Tooltip("Draw spatial mesh independently of the spatial understanding module while scanning (useful in editor)")]
  public bool visualizeSpatialMesh = false;

  [Tooltip("Material to use when spatial mesh is being visualized")]
  public Material spatialMeshVisibleMaterial = null;

  private SpatialMappingManager m_spatialMappingManager;
  private SpatialUnderstanding m_spatialUnderstanding;
  private enum State
  {
    Scanning,
    FinalizingScan,
    Playing
  };
  private State m_state;
  private GestureRecognizer m_gestureRecognizer;

  private void HideSpatialMesh()
  {
    var meshFilters = m_spatialMappingManager.GetMeshFilters();
    foreach (MeshFilter meshFilter in meshFilters)
    {
      meshFilter.gameObject.SetActive(false);
    }
  }

  private void OnTapEvent(InteractionSourceKind source, int tap_count, Ray head_ray)
  {
    switch (m_state)
    {
      case State.Scanning:
        // Finalize the scan
        m_state = State.FinalizingScan;
        break;
      case State.Playing:
        /*
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
        */
        break;
      default:
        break;
    }
  }

  private void UnityEditorUpdate()
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

  private void Update()
  {
    UnityEditorUpdate();
    if (m_state == State.FinalizingScan)
    {
      if (!m_spatialUnderstanding.ScanStatsReportStillWorking)
      {
        m_spatialUnderstanding.RequestFinishScan();
        HideSpatialMesh();
        m_state = State.Playing;
      }
      //TODO: timeout and error handling
    }
    else if (m_state == State.Playing)
    {
    }
  }

  private void Start()
  {
    // Start scanning with the SpatialUnderstanding module
    m_state = State.Scanning;
    m_spatialMappingManager = SpatialMappingManager.Instance;
    if (!m_spatialMappingManager.IsObserverRunning())
    {
      m_spatialMappingManager.StartObserver();
    }
    if (visualizeSpatialMesh && spatialMeshVisibleMaterial != null)
    {
      m_spatialMappingManager.SetSurfaceMaterial(spatialMeshVisibleMaterial);
      m_spatialMappingManager.DrawVisualMeshes = true;
    }
    m_spatialUnderstanding = SpatialUnderstanding.Instance;
    m_spatialUnderstanding.RequestBeginScanning();

    // Subscribe to tap gesture
    m_gestureRecognizer = new GestureRecognizer();
    m_gestureRecognizer.SetRecognizableGestures(GestureSettings.Tap);
    m_gestureRecognizer.TappedEvent += OnTapEvent;
    m_gestureRecognizer.StartCapturingGestures();
  }
}
