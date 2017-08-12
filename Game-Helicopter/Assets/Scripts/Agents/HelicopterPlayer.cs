using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(Helicopter))]
public class HelicopterPlayer: MonoBehaviour
{
  private Helicopter m_helicopter;
  private HoloLensXboxController.ControllerInput m_xboxController = null;
  private Vector3 m_joypadLateralAxis;
  private Vector3 m_joypadLongitudinalAxis;
  private bool m_movingLastFrame = false;
  private Helicopter.Controls m_controls = new Helicopter.Controls();

  private void UpdateControls(Vector3 aim)
  {
    // Determine angle between user gaze vector and helicopter forward, in xz
    // plane
    Vector3 view = new Vector3(Camera.main.transform.forward.x, 0, Camera.main.transform.forward.z);
    Vector3 forward = new Vector3(transform.forward.x, 0, transform.forward.z);
    float angle = Vector3.Angle(view, forward);

    // Get current joypad axis values
#if UNITY_EDITOR
    float hor = Input.GetAxis("Horizontal");
    float ver = Input.GetAxis("Vertical");
    float hor2 = Input.GetAxis("Horizontal2");
    float ver2 = -Input.GetAxis("Vertical2");
    float lt = Input.GetAxis("Axis9");
    float rt = Input.GetAxis("Axis10");
    bool buttonA = Input.GetKey(KeyCode.Joystick1Button0);
    bool buttonB = Input.GetKey(KeyCode.Joystick1Button1);
    bool fire = rt > 0.5f || buttonA;
#else
    m_xboxController.Update();
    float hor = m_xboxController.GetAxisLeftThumbstickX();
    float ver = m_xboxController.GetAxisLeftThumbstickY();
    float hor2 = m_xboxController.GetAxisRightThumbstickX();
    float ver2 = m_xboxController.GetAxisRightThumbstickY();
    float lt = m_xboxController.GetAxisLeftTrigger();
    float rt = m_xboxController.GetAxisRightTrigger();
    bool fire = rt > 0.5f;
    bool buttonA = m_xboxController.GetButton(ControllerButton.A);
    bool buttonB = m_xboxController.GetButton(ControllerButton.B);
    /*
    float hor = Input.GetAxis("Horizontal");
    float ver = Input.GetAxis("Vertical");
    float axis3 = Input.GetAxis("Axis3");
    float lt = Mathf.Max(axis3, 0);
    float rt = -Mathf.Min(axis3, 0);
    */
#endif

    // Any of the main axes (which are relative to orientation) pressed?
    bool movingThisFrame = (hor != 0) || (ver != 0);
    if (movingThisFrame && !m_movingLastFrame)
    {
      // Joypad was not pressed last frame, reorient based on current view position
      m_joypadLateralAxis = Vector3.Normalize(Camera.main.transform.right);
      m_joypadLongitudinalAxis = Vector3.Normalize(Camera.main.transform.forward);
    }
    m_movingLastFrame = movingThisFrame;

    // Apply longitudinal and lateral controls. Compute projection of joypad
    // lateral/longitudinal axes onto helicopter's.
    float joypadLongitudinalToHeliLongitudinal = Vector3.Dot(m_joypadLongitudinalAxis, transform.forward);
    float joypadLongitudinalToHeliLateral = Vector3.Dot(m_joypadLongitudinalAxis, transform.right);
    float joypadLateralToHeliLongitudinal = Vector3.Dot(m_joypadLateralAxis, transform.forward);
    float joypadLateralToHeliLateral = Vector3.Dot(m_joypadLateralAxis, transform.right);
    m_controls.longitudinal = joypadLongitudinalToHeliLongitudinal * ver + joypadLateralToHeliLongitudinal * hor;
    m_controls.lateral = joypadLongitudinalToHeliLateral * ver + joypadLateralToHeliLateral * hor;

    // Helicopter rotation
    m_controls.rotational = hor2;

    // Altitude control (trigger axes each range from 0 to 1)
    m_controls.altitude = ver2;

    // Gun
    /*
    if (fire && (Time.time - m_gunLastFired >= GUN_FIRE_PERIOD))
    {
      FireGun(aim);
    }
    */
  }

  private void UnityEditorUpdate()
  {
#if UNITY_EDITOR
    if (Input.GetKey("1"))
      m_helicopter.ChangeRotorSpeed(3, 5);
    if (Input.GetKey("2"))
      m_helicopter.ChangeRotorSpeed(0, 5);
    if (Input.GetKey(KeyCode.Z))
      m_controls.Clear();
    if (Input.GetKey(KeyCode.Comma))
      m_controls.longitudinal = Mathf.Clamp(m_controls.longitudinal - 0.1F, -1.0F, 1.0F);
    if (Input.GetKey(KeyCode.Period))
      m_controls.longitudinal = Mathf.Clamp(m_controls.longitudinal + 0.1F, -1.0F, 1.0F);
    if (Input.GetKey(KeyCode.Semicolon))
      m_controls.lateral = Mathf.Clamp(m_controls.lateral - 0.1F, -1.0F, 1.0F);
    if (Input.GetKey(KeyCode.Quote))
      m_controls.lateral = Mathf.Clamp(m_controls.lateral + 0.1F, -1.0F, 1.0F);
    if (Input.GetKey(KeyCode.LeftBracket))
      m_controls.rotational = Mathf.Clamp(m_controls.rotational - 0.1F, -1, 1);
    if (Input.GetKey(KeyCode.RightBracket))
      m_controls.rotational = Mathf.Clamp(m_controls.rotational + 0.1F, -1, 1);
    m_controls.altitude = Mathf.Clamp(m_controls.altitude + Input.GetAxis("Mouse ScrollWheel"), -1, 1);
#endif
  }

  private void FixedUpdate()
  {
    UnityEditorUpdate();
    UpdateControls(transform.forward);
    m_helicopter.controls = m_controls;
  }

  private void Start()
  {
    m_controls.Clear();
#if !UNITY_EDITOR
    m_xboxController = new ControllerInput(0, 0.19f);
#endif
  }

  private void Awake()
  {
    m_helicopter = GetComponent<Helicopter>();
  }
}
