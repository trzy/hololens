using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.VR.WSA.Input;
using HoloToolkit.Unity;
using HoloToolkit.Unity.SpatialMapping;
using System;

public class Demo: MonoBehaviour
{
  [Tooltip("Visualize spatial understanding mesh after scanning is complete; otherwise, occlude.")]
  public bool spatialUnderstandingMeshVisible = true;

  [Tooltip("Draw spatial mesh independently of the spatial understanding module while scanning (useful in editor)")]
  public bool visualizeSpatialMesh = false;

  [Tooltip("Material to use when spatial mesh is being visualized")]
  public Material spatialMeshVisibleMaterial = null;

  [Tooltip("Material to use when spatial understanding custom mesh is finished scanning and used for occlusion")]
  public Material spatialUnderstandingOcclusionMaterial = null;

  [Tooltip("Prefab for bullet hole decal.")]
  public GameObject m_bulletHolePrefab;

  [Tooltip("Material to use when rendering visual debugging aids")]
  public Material debugMaterial = null;

  private enum State
  {
    Scanning,
    FinalizeScan,
    WaitingForScanCompletion,
    WaitingForMeshImport,
    Playing
  };
  private State m_state;
  private GestureRecognizer m_gestureRecognizer;
  private SpatialMappingManager m_spatialMappingManager;
  private SpatialUnderstanding m_spatialUnderstanding;
  private SpatialUnderstandingDllTopology.TopologyResult[] m_queryResults = new SpatialUnderstandingDllTopology.TopologyResult[1024];
  private IntPtr m_queryResultsPtr = IntPtr.Zero;

  private void MakeCube(Vector3 position, Vector3 normal)
  {
    GameObject cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
    cube.transform.parent = null;
    cube.transform.localScale = new Vector3(1, 1, .25f);
    cube.transform.position = position;
    cube.transform.transform.rotation = Quaternion.LookRotation(normal, Vector3.up);
    cube.GetComponent<Renderer>().material = debugMaterial;
    cube.GetComponent<Renderer>().material.color = Color.blue;
    cube.SetActive(true);
  }

  private void QueryFloorPositions()
  {
    float minWidth = 1f;
    float minLength = 1f;
    int numPositions = SpatialUnderstandingDllTopology.QueryTopology_FindPositionsOnFloor(minLength, minWidth, m_queryResults.Length, m_queryResultsPtr);
    Debug.Log("Found " + numPositions + " positions");
    for (int i = 0; i < numPositions; i++)
    {
      var result = m_queryResults[i];
      Debug.Log("  Position " + i + ": pos=" + result.position.ToString("F5") + ", length=" + result.length.ToString("F3") + ", width=" + result.width.ToString("F3") + ", normal=" + result.normal.ToString("F3"));
      MakeCube(result.position, result.normal);
    }
  }

  private void HideSpatialMesh()
  {
    if (m_spatialMappingManager.IsObserverRunning())
      m_spatialMappingManager.StopObserver();
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

  private void CreateBulletHole(Vector3 position, Vector3 normal)
  {
    GameObject bulletHole = Instantiate(m_bulletHolePrefab, position, Quaternion.LookRotation(normal)) as GameObject;
    bulletHole.transform.parent = this.transform;
    OrientedBoundingBox obb = OBBMeshIntersection.CreateWorldSpaceOBB(bulletHole.GetComponent<BoxCollider>());
    SurfacePlaneDeformationManager.Instance.Embed(bulletHole, obb, position);
  }

  private void DoRaycast()
  {
    Vector3 rayPos = Camera.main.transform.position;
    Vector3 rayVec = Camera.main.transform.forward * 10f;
    IntPtr raycastResultPtr = SpatialUnderstanding.Instance.UnderstandingDLL.GetStaticRaycastResultPtr();
    int intersection = SpatialUnderstandingDll.Imports.PlayspaceRaycast(
        rayPos.x, rayPos.y, rayPos.z, rayVec.x, rayVec.y, rayVec.z,
        raycastResultPtr);
    if (intersection != 0)
    {
      SpatialUnderstandingDll.Imports.RaycastResult rayCastResult = SpatialUnderstanding.Instance.UnderstandingDLL.GetStaticRaycastResult();
      Debug.Log("hit detected=" + rayCastResult.SurfaceType.ToString());
      CreateBulletHole(rayCastResult.IntersectPoint, rayCastResult.IntersectNormal);
    }
    else
    {
      Debug.Log("no hit");
    }
  }

  private void OnTapEvent(InteractionSourceKind source, int tap_count, Ray head_ray)
  {
    switch (m_state)
    {
      case State.Scanning:
        // Finalize the scan
        m_state = State.FinalizeScan;
        break;
      case State.Playing:
        Debug.Log("Found " + m_spatialUnderstanding.UnderstandingCustomMesh.GetMeshFilters().Count + " meshes (import active=" + m_spatialUnderstanding.UnderstandingCustomMesh.IsImportActive + ")");
        DoRaycast();
        break;
      default:
        break;
    }
  }

  private void OnScanStateChanged()
  {
    if (m_spatialUnderstanding.ScanState == SpatialUnderstanding.ScanStates.Done)
    {
      m_state = State.WaitingForMeshImport;
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
    if (m_state == State.FinalizeScan)
    {
      if (!m_spatialUnderstanding.ScanStatsReportStillWorking)
      {
        Debug.Log("Finalizing scan...");
        m_state = State.WaitingForScanCompletion;
        m_spatialUnderstanding.RequestFinishScan();
      }
      //TODO: timeout and error handling?
    }
    else if (m_state == State.WaitingForMeshImport)
    {
      if (m_spatialUnderstanding.UnderstandingCustomMesh.IsImportActive == false)
      {
        Debug.Log("Found " + m_spatialUnderstanding.UnderstandingCustomMesh.GetMeshFilters().Count + " meshes (import active=" + m_spatialUnderstanding.UnderstandingCustomMesh.IsImportActive + ")");
        HideSpatialMesh();
        if (!spatialUnderstandingMeshVisible)
          SetSpatialUnderstandingMaterial(spatialUnderstandingOcclusionMaterial);
        SurfacePlaneDeformationManager.Instance.SetSpatialMeshFilters(m_spatialUnderstanding.UnderstandingCustomMesh.GetMeshFilters());
        //QueryFloorPositions();
        m_state = State.Playing;
      }
      //TODO: timeout?
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
    m_spatialUnderstanding.ScanStateChanged += OnScanStateChanged;
    m_spatialUnderstanding.RequestBeginScanning();

    // Subscribe to tap gesture
    m_gestureRecognizer = new GestureRecognizer();
    m_gestureRecognizer.SetRecognizableGestures(GestureSettings.Tap);
    m_gestureRecognizer.TappedEvent += OnTapEvent;
    m_gestureRecognizer.StartCapturingGestures();

    // Pin the query results memory and get a native pointer
    m_queryResultsPtr = SpatialUnderstanding.Instance.UnderstandingDLL.PinObject(m_queryResults);
  }
}
