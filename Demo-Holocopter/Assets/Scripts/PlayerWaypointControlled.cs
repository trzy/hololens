using UnityEngine;
using UnityEngine.VR.WSA.Input;
using System.Collections;
using System.Collections.Generic;
using System.Linq;

public class PlayerWaypointControlled: MonoBehaviour
{
  public Helicopter m_helicopter;
  public Material   m_reticle_material;
  public GameObject m_waypoints;
  public GameObject m_waypoint_prefab;
  public PlayspaceManager m_playspace_manager;
  public LevelManager m_level_manager;

  enum State
  {
    Scanning,
    Playing
  };

  private GestureRecognizer m_gesture_recognizer = null;
  private List<GameObject>  m_waypoint_list = new List<GameObject>();
  private bool              m_music_played = false;
  private GameObject        m_gaze_target = null;
  private int               m_object_layer = 0;
  private Reticle           m_reticle;
  private State             m_state;

  private void SetRenderEnable(GameObject obj, bool on)
  {
    Component[] renderers = obj.GetComponentsInChildren<Renderer>();
    foreach (Component renderer in renderers)
      renderer.GetComponent<Renderer>().enabled = on;
  }

  private IEnumerator BlinkGazeTargetCoroutine()
  {
    bool on = true;
    while (true)
    {
      on = !on;
      if (m_gaze_target)
        SetRenderEnable(m_gaze_target, on);
      yield return new WaitForSeconds(0.2f);
    }
  }

  private void OnTapEvent(InteractionSourceKind source, int tap_count, Ray head_ray)
  {
    switch (m_state)
    {
    case State.Scanning:
      m_playspace_manager.SetMakePlanesCompleteCallback(m_level_manager.GenerateLevel);
      SetState(State.Playing);
      break;
    case State.Playing:
      if (m_gaze_target == null)
      {
        GameObject waypoint = Instantiate(m_waypoint_prefab, transform.position + transform.forward * 1, Quaternion.identity) as GameObject;
        m_waypoint_list.Add(waypoint);
      }
      else if (m_gaze_target == m_helicopter.gameObject)
      {
        if (!m_music_played && m_waypoint_list.Any())
        {
          GetComponent<AudioSource>().Play();
          m_music_played = true;
        }
        m_helicopter.TraverseWaypoints(m_waypoint_list);
      }
      break;
    }
  }

  void SetState(State state)
  {
    m_state = state;
    switch (state)
    {
    case State.Scanning:
      m_playspace_manager.StartScanning();
      break;
    case State.Playing:
      m_playspace_manager.StopScanning();
      break;
    }
  }

  void Start()
  {
    m_gesture_recognizer = new GestureRecognizer();
    m_gesture_recognizer.SetRecognizableGestures(GestureSettings.Tap);
    m_gesture_recognizer.TappedEvent += OnTapEvent;
    m_gesture_recognizer.StartCapturingGestures();
    m_object_layer = 1 << LayerMask.NameToLayer("Default");
    m_reticle = new Reticle(m_reticle_material);
    SetState(State.Scanning);
    //StartCoroutine(BlinkGazeTargetCoroutine());
  }

  void Update()
  {
    //UnityEngine.VR.WSA.HolographicSettings.SetFocusPointForFrame(m_helicopter.transform.position, -Camera.main.transform.forward);
    /*
    GameObject old_gaze_target = m_gaze_target;
    m_gaze_target = null;
    RaycastHit hit;
    if (Physics.Raycast(Camera.main.transform.position, Camera.main.transform.forward, out hit, 20.0f, Physics.DefaultRaycastLayers))
    {
      GameObject gaze_target = hit.collider.transform.parent.gameObject;
      if (gaze_target.activeSelf)
        m_gaze_target = gaze_target;
    }
    if (old_gaze_target && old_gaze_target != m_gaze_target)
      SetRenderEnable(old_gaze_target, true);
    */
    RaycastHit hit;
    if (Physics.Raycast(Camera.main.transform.position, Camera.main.transform.forward, out hit, 20.0f, m_object_layer))//Physics.DefaultRaycastLayers))
    {
      GameObject gaze_target = hit.collider.transform.parent.gameObject;
      if (gaze_target.activeSelf)
        m_gaze_target = gaze_target;
    }
    else
      m_gaze_target = null;
    UnityEngineUpdate();
  }

  void OnPostRender()
  {
    m_reticle.Draw(m_gaze_target);
  }

#if UNITY_EDITOR
  private Vector3 m_euler = new Vector3();
#endif

  private void UnityEngineUpdate()
  {
#if UNITY_EDITOR
    // Simulate air tap
    if (Input.GetKeyDown(KeyCode.Return))
    {
      Ray head_ray = new Ray(transform.position, transform.forward);
      OnTapEvent(InteractionSourceKind.Hand, 1, head_ray);
    }

    // Rotate by maintaining Euler angles relative to world
    if (Input.GetKey("left"))
      m_euler.y -= 30 * Time.deltaTime;
    if (Input.GetKey("right"))
      m_euler.y += 30 * Time.deltaTime;
    if (Input.GetKey("up"))
      m_euler.x -= 30 * Time.deltaTime;
    if (Input.GetKey("down"))
      m_euler.x += 30 * Time.deltaTime;
    if (Input.GetKey("o"))
      m_euler.x = 0;
    transform.rotation = Quaternion.Euler(m_euler);

    // Motion relative to XZ plane
    float move_speed = 5.0F * Time.deltaTime;
    Vector3 forward = transform.forward;
    forward.y = 0.0F;
    forward.Normalize();  // even if we're looking up or down, will continue to move in XZ
    if (Input.GetKey("w"))
      transform.Translate(forward * move_speed, Space.World);
    if (Input.GetKey("s"))
      transform.Translate(-forward * move_speed, Space.World);
    if (Input.GetKey("a"))
      transform.Translate(-transform.right * move_speed, Space.World);
    if (Input.GetKey("d"))
      transform.Translate(transform.right * move_speed, Space.World);

    // Vertical motion
    if (Input.GetKey(KeyCode.KeypadMinus))  // up
      transform.Translate(new Vector3(0.0F, 1.0F, 0.0F) * move_speed, Space.World);
    if (Input.GetKey(KeyCode.KeypadPlus))   // down
      transform.Translate(new Vector3(0.0F, -1.0F, 0.0F) * move_speed, Space.World);
#endif
  }
}
