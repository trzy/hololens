using UnityEngine;
using UnityEngine.VR.WSA;
using UnityEngine.VR.WSA.Input;
using System.Collections;
using System.Collections.Generic;
using System.Linq;

public class Main: MonoBehaviour
{
  [Tooltip("Player prefab")]
  public PlayerController m_playerPrefab = null;

  enum State
  {
    Init,
    Scanning,
    FinalizeScan,
    Playing
  };

  private GestureRecognizer m_gestureRecognizer = null;
  private GameObject        m_gazeTarget = null;
  private RaycastHit        m_hit;
  private PlayerController  m_player = null;
  private State             m_state = State.Init;

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
      m_player = Instantiate(m_playerPrefab, Camera.main.transform.position + Camera.main.transform.forward, Quaternion.identity);
      break;
    }
  }

  void Start()
  {
    m_gestureRecognizer = new GestureRecognizer();
    m_gestureRecognizer.SetRecognizableGestures(GestureSettings.Tap);
    m_gestureRecognizer.TappedEvent += OnTapEvent;
    m_gestureRecognizer.StartCapturingGestures();
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

  void Update()
  {
    if (m_state == State.Playing)
    {
    }
    else if (m_state == State.Init)
    {
      SetState(State.Scanning);
    }
    UnityEditorUpdate();
  }
}
