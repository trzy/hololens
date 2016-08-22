using UnityEngine;
using UnityEngine.VR.WSA.Input;
using System.Collections;
using System.Collections.Generic;
using System.Linq;

public class PlayerHolographic : MonoBehaviour
{
  public Helicopter m_helicopter;
  public Material   m_reticle_material;
  public GameObject m_waypoints;
  public GameObject m_waypoint_prefab;
  public SpatialMap m_spatial_map;

  enum State
  {
    Scanning,
    Playing
  };

  private GestureRecognizer m_gesture_recognizer = null;
  private List<GameObject>  m_waypoint_list = new List<GameObject>();
  private bool              m_music_played = false;
  private GameObject        m_gaze_target = null;
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
      m_spatial_map.Scan();
      break;
    case State.Playing:
      m_spatial_map.Occlude();
      break;
    }
  }

  void Start()
  {
    m_gesture_recognizer = new GestureRecognizer();
    m_gesture_recognizer.SetRecognizableGestures(GestureSettings.Tap);
    m_gesture_recognizer.TappedEvent += OnTapEvent;
    m_gesture_recognizer.StartCapturingGestures();
    m_reticle = new Reticle(m_reticle_material);
    SetState(State.Scanning);
    //StartCoroutine(BlinkGazeTargetCoroutine());
  }

  void Update()
  {
    UnityEngine.VR.WSA.HolographicSettings.SetFocusPointForFrame(m_helicopter.transform.position, -Camera.main.transform.forward);
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
    if (Physics.Raycast(Camera.main.transform.position, Camera.main.transform.forward, out hit, 20.0f, Physics.DefaultRaycastLayers))
    {
      GameObject gaze_target = hit.collider.transform.parent.gameObject;
      if (gaze_target.activeSelf)
        m_gaze_target = gaze_target;
    }
    else
      m_gaze_target = null;
  }

  void OnPostRender()
  {
    m_reticle.Draw(m_gaze_target);
  }
}
