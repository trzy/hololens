using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.VR.WSA.Input;
using HoloToolkit.Unity;
using HoloToolkit.Unity.SpatialMapping;
using System;

public class Demo: MonoBehaviour
{
  [Tooltip("Draw spatial mesh independently of the spatial understanding module while scanning (useful in editor)")]
  public bool visualizeSpatialMesh = false;

  [Tooltip("Material to use when spatial mesh is being visualized")]
  public Material spatialMeshVisibleMaterial = null;

  [Tooltip("Material to use when rendering visual debugging aids")]
  public Material debugMaterial = null;

  private enum State
  {
    Scanning,
    FinalizingScan,
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
        QueryFloorPositions();
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
        Debug.Log("Finalizing scan...");
        m_spatialUnderstanding.RequestFinishScan();
        HideSpatialMesh();
//        QueryFloorPositions();
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

    // Pin the query results memory and get a native pointer
    m_queryResultsPtr = SpatialUnderstanding.Instance.UnderstandingDLL.PinObject(m_queryResults);
  }
}
