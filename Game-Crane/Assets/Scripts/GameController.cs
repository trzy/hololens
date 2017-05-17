using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.VR.WSA;
using UnityEngine.EventSystems;
using HoloToolkit.Unity.InputModule;

public class GameController: MonoBehaviour, IInputClickHandler
{
  [Tooltip("Link to magnet object.")]
  public GameObject magnet;

  [Tooltip("A solid cube, just for testing.")]
  public GameObject cubePrefab;

  [Tooltip("Enemy robot.")]
  public GameObject robotPrefab;

  [Tooltip("Bomb.")]
  public GameObject bombPrefab;

  [Tooltip("Objects to set stabilization plane at (average position is used).")]
  public GameObject[] stabilizationTargets;

  [Tooltip("A gizmo used to highlight debug items of interest.")]
  public GameObject gizmoPrefab;

  enum State
  {
    Init,
    Scanning,
    FinalizeScan,
    Playing
  }

  private State m_state = State.Init;
  private bool m_magnetOn = false;
  private List<GameObject> m_magnetObjects = new List<GameObject>();
  private GameObject m_gizmo = null;

  private void PlaceObjects()
  {
    Vector3 position;

    // A couple of test cubes
    if (PlayspaceManager.Instance.TryPlaceOnFloor(out position, cubePrefab.transform.localScale))
      Instantiate(cubePrefab, position, Quaternion.identity);
    if (PlayspaceManager.Instance.TryPlaceOnPlatform(out position, 0.25f, 1.5f, cubePrefab.transform.localScale.x))
      Instantiate(cubePrefab, position + cubePrefab.transform.localScale.y * Vector3.up, Quaternion.identity);

    // Chibi-robot!
    for (int i = 0; i < 3; i++)
    {
      if (PlayspaceManager.Instance.TryPlaceOnPlatform(out position, 0.25f, 1.5f, 0.25f))
      {
        GameObject bomb = Instantiate(bombPrefab);
        GameObject obj = Instantiate(robotPrefab, position, Quaternion.identity);
        RobotController robot = obj.GetComponent<RobotController>();
        robot.AddBomb(bomb);
      }
    }
  }

  private void SetState(State state)
  {
    m_state = state;
    switch (state)
    {
      case State.Scanning:
        Debug.Log("State: Scanning");
        PlayspaceManager.Instance.StartScanning();
        break;
      case State.FinalizeScan:
        Debug.Log("State: FinalizeScan");
        System.Action OnScanComplete = () =>
        {
          SetState(State.Playing);
        };
        PlayspaceManager.Instance.OnScanComplete += OnScanComplete;
        PlayspaceManager.Instance.StopScanning();
        break;
      case State.Playing:
        Debug.Log("State: Playing");
        PlaceObjects();
        break;
    }
  }

  private void AttachToMagnet(GameObject obj)
  {
    ConfigurableJoint joint = obj.AddComponent<ConfigurableJoint>();
    joint.connectedBody = magnet.GetComponent<Rigidbody>();
    joint.autoConfigureConnectedAnchor = false;
    joint.anchor = new Vector3(0, 0.5f, 0);
    joint.connectedAnchor = new Vector3(0, -1, 0);
    joint.xMotion = ConfigurableJointMotion.Limited;
    joint.yMotion = ConfigurableJointMotion.Limited;
    joint.zMotion = ConfigurableJointMotion.Limited;
    joint.linearLimitSpring = new SoftJointLimitSpring() { spring = 1e3f, damper = 0 };
    joint.linearLimit = new SoftJointLimit() { limit = .01f };  //TODO: determine limit from magnet size
  }

  private void DetachFromMagnet(GameObject obj)
  {
    GameObject.Destroy(obj.GetComponent<ConfigurableJoint>());
  }

  public void OnInputClicked(InputClickedEventData eventData)
  {
    switch (m_state)
    {
      case State.Scanning:
        SetState(State.FinalizeScan);
        break;
      case State.Playing:
        m_magnetOn = !m_magnetOn;
        if (m_magnetOn)
        {
          MonoBehaviour[] objects = FindObjectsOfType<MonoBehaviour>();
          foreach (MonoBehaviour obj in objects)
          {
            IMagnetic magnetic = obj as IMagnetic;
            if (magnetic != null)
            {
              float distance = (magnet.transform.position - obj.transform.position).magnitude;
              if (distance < 1)
              {
                AttachToMagnet(obj.gameObject);
                magnetic.OnMagnet(true);
                m_magnetObjects.Add(obj.gameObject);
              }
            }
          }
        }
        else
        {
          foreach (GameObject obj in m_magnetObjects)
          {
            DetachFromMagnet(obj);
            obj.GetComponent<IMagnetic>().OnMagnet(false);
          }
          m_magnetObjects.Clear();
        }
        break;
    }
  }

#if UNITY_EDITOR
  private void Update()
  {
    // Simulate air tap
    if (Input.GetKeyDown(KeyCode.Return))
    {
      Ray headRay = new Ray(transform.position, transform.forward);
      OnInputClicked(new InputClickedEventData(EventSystem.current));
    }
  }
#endif

  private void DebugDraw(GameObject obj)
  {
    if (obj == null)
    {
      if (m_gizmo != null)
        m_gizmo.SetActive(false);
      return;
    }

    if (m_gizmo == null)
      m_gizmo = Instantiate(gizmoPrefab);
    m_gizmo.SetActive(true);
    m_gizmo.transform.position = obj.transform.position;
  }

  private void LateUpdate()
  {
    /*
     * Notes:
     * 
     * For this to work, make sure things like InputManager are not assigned to Default layers.
     * We also don't want to stabilize on things that are too close (~1m). May want to refine this
     * by measuring distance *to* stabilization plane.
     * 
     * When tracking is used, it's not clear what the normal is. I think GazeManager is used,
     * however, which lead to incorrect settings.
     * 
     * TODO:
     * -----
     * Put game objects in an explicit Game layer and simply tell GazeManager to use that.
     */
    MonoBehaviour[] objects = FindObjectsOfType<MonoBehaviour>();
    GameObject bestCandidate = null;
    //List<MonoBehaviour> visibleObjects = new List<MonoBehaviour>(objects.Length);
    foreach (MonoBehaviour obj in objects)
    {
      Renderer[] renderers = obj.GetComponentsInChildren<Renderer>();
      foreach (Renderer renderer in renderers)
      //Renderer renderer = obj.gameObject.GetComponent<Renderer>();
      //if (renderer != null)
      {
        if (renderer.isVisible)
        {
          if (obj.gameObject.layer != LayerMask.NameToLayer("NeverStabilize") && obj.gameObject.layer != LayerMask.NameToLayer("SecondaryStabilize") && obj.gameObject.layer != HoloToolkit.Unity.SpatialMapping.SpatialMappingManager.Instance.PhysicsLayer && (obj.transform.position - Camera.main.transform.position).magnitude > 0.25f)
          {
            if (bestCandidate == null)
              bestCandidate = obj.gameObject;
            else
            {
              float distance = (Camera.main.transform.position - obj.transform.position).magnitude;
              float bestDistance = (Camera.main.transform.position - obj.transform.position).magnitude;
              if (distance > 1 && distance < bestDistance)
                bestCandidate = obj.gameObject;
            }
          }
          break;
        }
      }
    }
    HoloToolkit.Unity.StabilizationPlaneModifier.Instance.TargetOverride = (bestCandidate == null) ? null : bestCandidate.transform;
    //DebugDraw(bestCandidate);
    //Debug.Log("Best candidate=" + ((bestCandidate == null) ? "none" : bestCandidate.name));
  }

  private void Start()
  {
    InputManager.Instance.AddGlobalListener(this.gameObject);
    SetState(State.Scanning);

    // GazeManager should ignore head-attached objects (marked mostly as NeverStabilize but some may be SecondaryStabilize)
    // if anything else can first be found
    int ignoreMask = LayerMask.GetMask(new string[] { "NeverStabilize" });
    int secondaryMask = 1 << magnet.layer;  // GazeManager should ignore head-attached objects if anything else can be found first
    int primaryMask = ((Physics.DefaultRaycastLayers & ~PlayspaceManager.spatialLayerMask) & ~secondaryMask) & ~ignoreMask;
    if (ignoreMask != secondaryMask)
      GazeManager.Instance.RaycastLayerMasks = new LayerMask[] { primaryMask, secondaryMask };
    else
      GazeManager.Instance.RaycastLayerMasks = new LayerMask[] { primaryMask };
  }
}
