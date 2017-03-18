using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.VR.WSA;
using UnityEngine.VR.WSA.Input;
using HoloLensXboxController;

public class Main: MonoBehaviour
{
  [Tooltip("Player prefab")]
  public PlayerController playerPrefab = null;

  [Tooltip("Assortment of prefabs to place")]
  public GameObject[] placeablePrefabs = null;

  enum State
  {
    Init,
    Scanning,
    FinalizeScan,
    Playing
  }

  enum PlacementMode
  {
    Free = 0,
    Surface,
    NumPlacementModes
  }

  private ControllerInput   m_xboxController = null;
  private GestureRecognizer m_gestureRecognizer = null;
  private PlayerController  m_player = null;
  private GameObject[]      m_placeableObjects = null;
  private int               m_objectIdx = 0;
  private float             m_eulerY = 0;
  private List<GameObject>  m_objects = new List<GameObject>();
  private State             m_state = State.Init;
  private PlacementMode     m_placementMode = PlacementMode.Free;

  private void OnTapEvent(InteractionSourceKind source, int tapCount, Ray headRay)
  {
    switch (m_state)
    {
    case State.Scanning:
      SetState(State.FinalizeScan);
      break;
    case State.Playing:
      break;
    }
  }

  void SetState(State state)
  {
    m_state = state;
    switch (state)
    {
    case State.Scanning:
      PlayspaceManager.Instance.StartScanning();
      break;
    case State.FinalizeScan:
      System.Action OnScanComplete = () => 
      {
        //LevelManager.Instance.GenerateLevel();
        SetState(State.Playing);
      };
      PlayspaceManager.Instance.OnScanComplete += OnScanComplete;
      PlayspaceManager.Instance.StopScanning();
      break;
    case State.Playing:
      Debug.Log("Entered play state");
      m_player = Instantiate(playerPrefab, Camera.main.transform.position + Camera.main.transform.forward, Quaternion.identity);
      GetComponent<AudioSource>().Play();
      break;
    }
  }

  void CreatePrefabPreviews()
  {
    m_placeableObjects = new GameObject[placeablePrefabs.Length];
    for (int i = 0; i < placeablePrefabs.Length; i++)
    {
      if (placeablePrefabs[i] != null)
      {
        GameObject g = Instantiate(placeablePrefabs[i]) as GameObject;
        g.SetActive(false);
        m_placeableObjects[i] = g;
      }
      else
        m_placeableObjects[i] = null;
    }
    m_objectIdx = 0;
  }

  void Start()
  {
#if !UNITY_EDITOR
    m_xboxController = new ControllerInput(0, 0.19f);
#endif
    m_gestureRecognizer = new GestureRecognizer();
    m_gestureRecognizer.SetRecognizableGestures(GestureSettings.Tap);
    m_gestureRecognizer.TappedEvent += OnTapEvent;
    m_gestureRecognizer.StartCapturingGestures();
    CreatePrefabPreviews();
  }

  private void UnityEditorUpdate()
  {
#if UNITY_EDITOR
    // Simulate air tap
    if (Input.GetKeyDown(KeyCode.Return))
    {
      Ray headRay = new Ray(transform.position, transform.forward);
      OnTapEvent(InteractionSourceKind.Hand, 1, headRay);
    }
#endif
  }

  private bool FindGazeTarget(out RaycastHit hit)
  {
    if (Physics.Raycast(Camera.main.transform.position, Camera.main.transform.forward, out hit, 5))
    {
      Debug.Log("Hit: " + hit.collider.gameObject.name);
      return true;
    }
    return false;
  }

  private void PlacementUpdate()
  {
#if UNITY_EDITOR
    float horAxis = Input.GetAxis("Horizontal");
    bool pressedLeft = Input.GetKeyDown(KeyCode.LeftBracket);
    bool pressedRight = Input.GetKeyDown(KeyCode.RightBracket);
    bool pressedUp = Input.GetKeyDown(KeyCode.Semicolon);
    bool pressedDown = Input.GetKeyDown(KeyCode.Comma);
    bool pressedX = Input.GetKeyDown(KeyCode.LeftShift);
    bool pressedY = Input.GetKeyDown(KeyCode.Tab);
#else
    m_xboxController.Update();
    float horAxis = m_xboxController.GetAxisLeftThumbstickX();
    bool pressedLeft = m_xboxController.GetButtonDown(ControllerButton.DPadLeft);
    bool pressedRight = m_xboxController.GetButtonDown(ControllerButton.DPadRight);
    bool pressedUp = m_xboxController.GetButtonDown(ControllerButton.DPadUp);
    bool pressedDown = m_xboxController.GetButtonDown(ControllerButton.DPadDown);
    bool pressedX = m_xboxController.GetButtonDown(ControllerButton.X);
    bool pressedY = m_xboxController.GetButtonDown(ControllerButton.Y);
#endif

    // Y toggles placement mode
    if (pressedY)
      m_player.gameObject.SetActive(!m_player.gameObject.activeSelf);
    if (m_player.gameObject.activeSelf)
    {
      m_placeableObjects[m_objectIdx].SetActive(false);
      return;
    }

    // Up/down change the placement mode
    if (pressedUp)
    {
      uint mode = (uint)m_placementMode;
      mode = (mode + 1) % (int)PlacementMode.NumPlacementModes;
      m_placementMode = (PlacementMode)mode;
    }
    if (pressedDown)
    {
      uint mode = (uint)m_placementMode;
      mode = (mode - 1) % (uint)PlacementMode.NumPlacementModes;
      m_placementMode = (PlacementMode)mode;
    }

    // Digital left/right change the object
    m_placeableObjects[m_objectIdx].SetActive(false);
    int next = (pressedLeft ? -1 : 0) + (pressedRight ? 1 : 0);
    m_objectIdx += next;
    if (m_objectIdx >= m_placeableObjects.Length)
      m_objectIdx = 0;
    else if (m_objectIdx < 0)
      m_objectIdx = m_placeableObjects.Length - 1;

    // Analog left/right changes rotation about the Y axis
    float rotateBy = (horAxis > 0.25f ? 90 : 0) + (horAxis < -0.25f ? -90 : 0);
    m_eulerY += rotateBy * Time.deltaTime;

    // Update object position based on placement mode
    GameObject preview = m_placeableObjects[m_objectIdx];
    preview.SetActive(true);
    switch (m_placementMode)
    {
      case PlacementMode.Free:
        preview.transform.position = Camera.main.transform.position + Camera.main.transform.forward;
        preview.transform.rotation = Quaternion.LookRotation(Camera.main.transform.forward, Camera.main.transform.up);
        break;
      case PlacementMode.Surface:
        preview.SetActive(false); // don't want raycast to hit self
        RaycastHit hit;
        if (FindGazeTarget(out hit))
        {
          preview.transform.position = hit.point;
          preview.transform.rotation = Quaternion.FromToRotation(Vector3.up, hit.normal) * Quaternion.Euler(new Vector3(0, m_eulerY, 0));
        }
        preview.SetActive(true);
        break;
    }

    // Place object
    if (pressedX)
    {
      GameObject newObj = Instantiate(placeablePrefabs[m_objectIdx], preview.transform.position, preview.transform.rotation);
      m_objects.Add(newObj);
    }
  }

  void Update()
  {
    UnityEditorUpdate();
    if (m_state == State.Playing)
      PlacementUpdate();
    else if (m_state == State.Init)
      SetState(State.Scanning);
  }
}
