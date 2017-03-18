using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using HoloLensXboxController;

public class PlayerController: MonoBehaviour
{
  public Bullet bulletPrefab;
  public Material selectionMaterial;
  public Material extrudeMaterial;

  private ControllerInput m_xboxController = null;
  private GeoMaker m_geo = new GeoMaker();
  private Rigidbody m_rb;
  private int m_animRunning = Animator.StringToHash("Running");
  private int m_animJump = Animator.StringToHash("Jump");
  private int m_animOnGround = Animator.StringToHash("OnGround");
  private int m_animAttack = Animator.StringToHash("Attack");
  private int m_jumpFrames = 0;
  private Animator m_anim;
  private Vector3 m_horAxis = Vector3.zero;
  private Vector3 m_verAxis = Vector3.zero;
  private Vector3 m_motionInput = Vector3.zero;
  private bool m_jumpActive = false;
  private bool m_extrudePressed = false;
  private bool m_attackPressed = false;

  // Animation callback
  public void OnShotFired()
  {
    Instantiate(bulletPrefab, transform.position + transform.forward * 0.1f + transform.up * 0.11f + transform.right * -0.01f, Quaternion.LookRotation(transform.forward));
  }

  private void OnCollisionEnter(Collision other)
  {
    for (int i = 0; i < other.contacts.Length; i++)
    {
      //Debug.Log("contact point: " + (transform.position - other.contacts[i].point));
    }
    m_anim.SetBool(m_animOnGround, true);
    m_jumpFrames = 0;
  }

  private void OnCollisionExit(Collision collision)
  {
    m_anim.SetBool(m_animOnGround, false);
  }

  private Vector3 ProjectXZ(Vector3 v)
  {
    return new Vector3(v.x, 0, v.z);
  }

  private Vector3 GetMotionInput()
  {
#if UNITY_EDITOR
    float hor = Input.GetAxis("Horizontal");
    float ver = Input.GetAxis("Vertical");
#else
    float hor = m_xboxController.GetAxisLeftThumbstickX();
    float ver = m_xboxController.GetAxisLeftThumbstickY();
    bool buttonA = m_xboxController.GetButtonDown(ControllerButton.A);
    bool buttonB = m_xboxController.GetButtonDown(ControllerButton.B);
#endif
    hor = Mathf.Abs(hor) > 0.25f ? hor : 0;
    ver = Mathf.Abs(ver) > 0.25f ? ver : 0;
    bool pressed = hor != 0 || ver != 0;
    if (!pressed)
    {
      m_horAxis = Vector3.zero;
      m_verAxis = Vector3.zero;
      return Vector3.zero;
    }
    if (m_horAxis == Vector3.zero)
    {
      // Compute new motion axes
      m_verAxis = Vector3.Normalize(ProjectXZ(Camera.main.transform.forward));
      m_horAxis = Vector3.Normalize(Quaternion.Euler(0, 90, 0) * m_verAxis);
    }
    return hor * m_horAxis + ver * m_verAxis;
  }

  private void Update()
  {
#if UNITY_EDITOR
    m_jumpActive = Input.GetKey(KeyCode.Joystick1Button0);
    m_extrudePressed |= Input.GetKeyDown(KeyCode.Joystick1Button1);
    m_attackPressed |= Input.GetKeyDown(KeyCode.Joystick1Button3);
#else
    m_xboxController.Update();
    m_jumpActive = m_xboxController.GetButton(ControllerButton.A);
    m_extrudePressed |= m_xboxController.GetButtonDown(ControllerButton.B);
    m_attackPressed |= m_xboxController.GetButtonDown(ControllerButton.X);
#endif
    m_motionInput = GetMotionInput();
  }

  private void FixedUpdate()
  {
    bool running = m_motionInput != Vector3.zero;
    m_anim.SetBool(m_animRunning, running);

    if (running)
    {
      m_rb.MovePosition(transform.position + 0.5f * m_motionInput * Time.deltaTime);
      Quaternion targetRotation = Quaternion.LookRotation(m_motionInput, Vector3.up);
      m_rb.MoveRotation(Quaternion.RotateTowards(transform.rotation, targetRotation, 10));
    }

    if (m_jumpFrames < 3 && m_jumpActive)
    {
      if (m_jumpFrames == 0)
        m_anim.SetTrigger(m_animJump);
      m_rb.AddForce(Vector3.up, ForceMode.VelocityChange);
      ++m_jumpFrames;
    }

    if (m_attackPressed)
    {
      m_anim.SetTrigger(m_animAttack);
    }

    if (m_extrudePressed)
    {
      if (m_geo.state == GeoMaker.State.Idle)
        m_geo.StartSelection(selectionMaterial);
      else if (m_geo.state == GeoMaker.State.Select)
        m_geo.FinishSelection(extrudeMaterial, null);
    }
    if (0 == m_jumpFrames || m_geo.state == GeoMaker.State.AnimatedExtrude)
      m_geo.Update(transform.position + transform.up * 0.1f, -transform.up, 0.2f);

    // Clear single-press inputs
    m_extrudePressed = false;
    m_attackPressed = false;
  }

  private void Awake()
  {
    m_rb = GetComponent<Rigidbody>();
    m_anim = GetComponent<Animator>();
#if !UNITY_EDITOR
    m_xboxController = new ControllerInput(0, 0.19f);
#endif
  }
}
