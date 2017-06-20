using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.VR.WSA;
using UnityEngine.EventSystems;
using HoloToolkit.Unity.InputModule;

public class AppController: MonoBehaviour, IInputClickHandler
{
  [Tooltip("An agent that can navigate around a NavMesh")]
  public Agent agentPrefab;

  enum State
  {
    Init,
    Scanning,
    FinalizeScan,
    Playing
  }

  private State m_state = State.Init;
  private Agent m_agent;

  private void PlaceAgent()
  {
    Vector3 position;
    if (PlayspaceManager.Instance.TryPlaceOnFloor(out position, 0.1f * Vector3.one))
      m_agent = Instantiate(agentPrefab, position, Quaternion.identity);
    else
      Debug.Log("ERROR: could not place agent on floor!");
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
          PlaceAgent();
          SetState(State.Playing);
        };
        PlayspaceManager.Instance.OnScanComplete += OnScanComplete;
        PlayspaceManager.Instance.StopScanning();
        break;
      case State.Playing:
        Debug.Log("State: Playing");
        break;
    }
  }

  public void OnInputClicked(InputClickedEventData eventData)
  {
    switch (m_state)
    {
      case State.Scanning:
        SetState(State.FinalizeScan);
        break;
      case State.Playing:
        RaycastHit hitInfo;
        if (Physics.Raycast(new Ray(Camera.main.transform.position, Camera.main.transform.forward), out hitInfo, 100))
          m_agent.MoveTo(hitInfo.point);
        break;
    }
  }

#if UNITY_EDITOR
  private void Update()
  {
    // Simulate air tap
    if (Input.GetKeyDown(KeyCode.Return))
      OnInputClicked(new InputClickedEventData(EventSystem.current));
  }
#endif

  private void Start()
  {
    InputManager.Instance.AddGlobalListener(this.gameObject);
    SetState(State.Scanning);
  }
}
