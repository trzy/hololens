using UnityEngine;
using UnityEngine.UI;
using System.Collections;
using System.Collections.Generic;
using HoloLensXboxController;

public class Helicopter: MonoBehaviour
{
  public Bullet bulletPrefab = null;

  public enum ControlMode
  {
    Player,
    Program
  };

  public float heading
  {
    get { return transform.eulerAngles.y; } // Euler angle is safe here; equivalent to manual projection of forward onto xz plane
  }

  class Controls
  {
    public float longitudinal = 0;  // [-1,1] forward/back in xz plane relative to current heading (local z projected onto xz)
    public float lateral = 0;       // [-1,1] right/left in xz plane relative to current heading (local x projected onto xz)
    public float rotational = 0;    // [-1,1] counter/clockwise rotation in xz plane
    public float altitude = 0;      // [-1,1] rotor force along local up (-1=free-fall, 0=hover)
    public void Clear()
    {
      longitudinal = 0;
      lateral = 0;
      rotational = 0;
      altitude = 0;
    }
  }

  private ControllerInput m_xboxController = null;
  private IEnumerator m_controlCoroutine = null;
  private IEnumerator m_rotorSpeedCoroutine = null;
  private ControlMode m_controlMode = ControlMode.Player;
  private Controls m_playerControls = new Controls();
  private Controls m_programControls = new Controls();
  private bool m_movingLastFrame = false;
  private Vector3 m_joypadLateralAxis;
  private Vector3 m_joypadLongitudinalAxis;
  private Vector3 m_targetForward;
  private float m_gunLastFired;
  private AutoAim m_autoAim = null;
  private Transform m_muzzleTransform = null;

  private AudioSource m_gunAudioSource = null;
  private AudioSource m_rotorAudioSource = null;

  private const float SCALE = .06f;
  private const float MAX_TILT_DEGREES = 30;
  private const float MAX_TORQUE = 25000 * SCALE; //TODO: reformulate in terms of mass?
  private const float PITCH_ROLL_CORRECTIVE_TORQUE = MAX_TORQUE / 5.0f;
  private const float YAW_CORRECTIVE_TORQUE = MAX_TORQUE * 4;
  private const float ACCEPTABLE_DISTANCE = 5 * SCALE;
  private const float ACCEPTABLE_HEADING_ERROR = 5; // in degrees
  private const float GUN_FIRE_PERIOD = 1f / 8f;//1f / 5f;

  private void SetControlProgram(IEnumerator coroutine)
  {
    if (m_controlCoroutine != coroutine)
      StopCoroutine(m_controlCoroutine);
    m_controlCoroutine = coroutine;
    if (m_controlCoroutine != null)
      StartCoroutine(m_controlCoroutine);
  }

  public void SetControlMode(ControlMode controlMode, IEnumerator coroutine = null)
  {
    m_controlMode = controlMode;
    SetControlProgram(coroutine);
  }

  private float CrossY(Vector3 a, Vector3 b)
  {
    return -(a.x * b.z - a.z * b.x);
  }

  private float HeadingError(Vector3 targetForward)
  {
    Vector3 target = ProjectXZ(targetForward);
    Vector3 forward = ProjectXZ(transform.forward);
    // Minus sign because error defined as how much we have overshot and need
    // to subtract, assuming positive rotation is clockwise.
    return -Mathf.Sign(CrossY(forward, target)) * Vector3.Angle(forward, target);
  }

  private float HeadingErrorTo(Vector3 targetPoint)
  {
    return HeadingError(targetPoint - transform.position);
  }

  private bool GoTo(Vector3 targetPosition)
  {
    Vector3 to_target = targetPosition - transform.position;
    float distance = Vector3.Magnitude(to_target);
    float headingError = HeadingErrorTo(targetPosition);
    float absHeadingError = Mathf.Abs(headingError);
    if (absHeadingError > ACCEPTABLE_HEADING_ERROR)
      m_programControls.rotational = -Mathf.Sign(headingError) * Mathf.Lerp(0.5F, 1.0F, Mathf.Abs(headingError) / 360.0F);
    else
      m_programControls.rotational = 0;
    if (distance > ACCEPTABLE_DISTANCE)
    {
      //TODO: reduce intensity once closer? Gradual roll-off within some event horizon.
      Vector3 toTargetNorm = to_target / distance;
      m_programControls.longitudinal = Vector3.Dot(toTargetNorm, transform.forward);
      m_programControls.lateral = Vector3.Dot(toTargetNorm, transform.right);
      m_programControls.altitude = toTargetNorm.y;
    }
    else
      return false;
    return true;
  }

  private IEnumerator TraverseWaypointsCoroutine(List<GameObject> waypointsParam)
  {
    List<GameObject> waypoints = new List<GameObject>(waypointsParam);
    foreach (GameObject waypoint in waypoints)
    {
      while (GoTo(waypoint.transform.position))
        yield return null;
    }
    m_programControls.Clear();
  }

  private IEnumerator FlyToPositionCoroutine(Vector3 targetPosition)
  {
    while (GoTo(targetPosition))
      yield return null;
    m_programControls.Clear();
  }

  public void TraverseWaypoints(List<GameObject> waypoints)
  {
    SetControlMode(ControlMode.Program, TraverseWaypointsCoroutine(waypoints));
  }

  public void FlyToPosition(Vector3 targetPosition)
  {
    SetControlMode(ControlMode.Program, FlyToPositionCoroutine(targetPosition));
  }

  private void FireGun(Vector3 aim)
  {
    //Bullet bullet = Instantiate(bulletPrefab, transform.position + transform.up * -0.05f + transform.forward * 0.25f, Quaternion.identity) as Bullet;
    Bullet bullet = Instantiate(bulletPrefab, m_muzzleTransform.position, Quaternion.identity) as Bullet;
    bullet.transform.forward = aim; 
    m_gunLastFired = Time.time;
    m_gunAudioSource.Play();
  }

  private IEnumerator ChangeRotorSpeedCoroutine(string rpsParamName, float targetRPS, float rampTime)
  {
    var animator = GetComponent<Animator>();
    float startRPS = animator.GetFloat(rpsParamName);
    float currentRPS = startRPS;
    float timeElapsed = 0.0F;
    while (Mathf.Abs(currentRPS - targetRPS) > 0.0F)
    {
      currentRPS = startRPS + (targetRPS - startRPS) * Mathf.Min(timeElapsed / rampTime, 1.0F);
      animator.SetFloat(rpsParamName, currentRPS);
      timeElapsed += Time.deltaTime;
      yield return null;
    }
    //TODO: optimization: disable animation if rotation speed reaches 0
  }

  private void ChangeRotorSpeed(ref IEnumerator coroutine, string rpsParamName, float targetRPS, float rampTime)
  {
    if (coroutine != null)
      StopCoroutine(coroutine);
    coroutine = ChangeRotorSpeedCoroutine(rpsParamName, targetRPS, rampTime);
    StartCoroutine(coroutine);
  }

  void OnTriggerEnter(Collider other)
  {
    /*
    GameObject obj = other.gameObject;
    if (obj.CompareTag("Waypoint"))
      obj.SetActive(false);
    */
  }

  private Vector3 ProjectXZ(Vector3 v)
  {
    return new Vector3(v.x, 0, v.z);
  }

  void FixedUpdate()
  {
    /*
     * Axis convention
     * ---------------
     * 
     * Forward = blue  = +z
     * Right   = red   = +x
     * Up      = green = +y
     * 
     * Linear terminal velocity
     * ------------------------
     *
     * Velocity should be integrated like this:
     *
     *  A = F/m
     *  Vf = Vi + (A - drag*Vi)*dt
     * 
     * Solving for Vmax = Vi = Vf: Vmax = A/drag
     * But unfortunately, Unity implements drag incorrectly as:
     *
     *  Vt = Vi + A*dt
     *  Vf = Vt - Vt*drag*dt
     *
     * Or:
     * 
     *  Vf = (Vi + A*dt) * (1-drag*dt)
     * 
     * Which gives a different terminal velocity:
     * 
     *  Vmax = A/drag - A*dt = A*(1/drag - dt)
     *
     * Translational xz control
     * ------------------------
     * 
     * Translational motion is in xz plane and oriented relative to the 
     * helicopter's heading (forward vector projected onto xz plane). The input
     * is in units of g-force (-1 to 1 g).
     * 
     * Pitch and roll proportional to input strength (and therefore speed) are
     * induced, up to a maximum angle, simulating how a helicopter looks in 
     * flight without requiring accurate modeling of rotor force and torques.
     * 
     * Pitch and roll angles are relative to the xz plane and are computed by
     * projecting local forward and right onto the plane. Cannot use Euler
     * angles because Euler rotations are applied sequentially and each depends
     * on the previous.
     * 
     * Torques are applied to induce the desired orientation change, with
     * magnitude proportional to angular error (i.e., a P controller).
     * 
     * Rotational control
     * ------------------
     *
     * Rotational input rotates around *local* up axis (yaw) by applying a 
     * torque. Torque is a multiple of torque used to induce pitch and roll to
     * provide a faster response time because user may want to quickly rotate
     * full 360 degrees.
     * 
     * Altitude control
     * ----------------
     * 
     * Altitude input is relative to rotor force (along local up) needed to 
     * maintain constant altitude. Input of 0 hovers, -1 is free fall.
     *
     * Note: To hover, we need to control y component of rotor force. When 
     * helicopter is angled, the rotor force has to be larger, which also adds
     * additional translational force beyond the translational inputs, e.g.
     * causing the helicopter to travel faster forwards as it climbs.
     */

    Controls controls = m_controlMode == ControlMode.Player ? m_playerControls : m_programControls;
    Rigidbody rb = GetComponent<Rigidbody>();
    Vector3 torque = new Vector3();

    float currentHeadingDeg = heading;
    Vector3 translationalInput = new Vector3(controls.lateral, 0, controls.longitudinal);
    Vector3 translationalForce = Quaternion.Euler(new Vector3(0, currentHeadingDeg, 0)) * translationalInput * SCALE * rb.mass * Mathf.Abs(Physics.gravity.y);

    float targetPitchDeg = Mathf.Sign(controls.longitudinal) * Mathf.Lerp(0, MAX_TILT_DEGREES, Mathf.Abs(controls.longitudinal));
    float currentPitchDeg = Mathf.Sign(-transform.forward.y) * Vector3.Angle(transform.forward, ProjectXZ(transform.forward));
    float targetRollDeg = Mathf.Sign(-controls.lateral) * Mathf.Lerp(0, MAX_TILT_DEGREES, Mathf.Abs(controls.lateral));
    float currentRollDeg = Mathf.Sign(transform.right.y) * Vector3.Angle(transform.right, ProjectXZ(transform.right));

    float pitchErrorDeg = targetPitchDeg - currentPitchDeg;
    float rollErrorDeg = targetRollDeg - currentRollDeg;
    torque += SCALE * PITCH_ROLL_CORRECTIVE_TORQUE * new Vector3(pitchErrorDeg, 0, rollErrorDeg);
    torque += SCALE * YAW_CORRECTIVE_TORQUE * controls.rotational * Vector3.up;

    float upWorldLocalDeg = Vector3.Angle(Vector3.up, transform.up); // angle between world and local up
    float hoverForce = Mathf.Abs(rb.mass * Physics.gravity.y / Mathf.Cos(upWorldLocalDeg * Mathf.Deg2Rad));
    float rotorForce = (controls.altitude + 1) * hoverForce;

    hoverForce = Mathf.Abs(rb.mass * Physics.gravity.y);
    rotorForce = SCALE * controls.altitude * rb.mass * Mathf.Abs(Physics.gravity.y);

    // Apply all forces
    rb.AddRelativeForce(Vector3.up * rotorForce);
    rb.AddForce(Vector3.up * hoverForce);
    rb.AddForce(translationalForce);
    rb.AddRelativeTorque(torque);

    // Pitch of engine is based on tilt
    float engineOutput = Mathf.Max(Mathf.Abs(controls.longitudinal), Mathf.Abs(controls.lateral));
    m_rotorAudioSource.pitch = Mathf.Lerp(1.0f, 1.3f, engineOutput);
  }

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
    m_playerControls.longitudinal = joypadLongitudinalToHeliLongitudinal * ver + joypadLateralToHeliLongitudinal * hor;
    m_playerControls.lateral = joypadLongitudinalToHeliLateral * ver + joypadLateralToHeliLateral * hor;

    // Helicopter rotation
    m_playerControls.rotational = hor2;

    // Altitude control (trigger axes each range from 0 to 1)
    m_playerControls.altitude = ver2;

    // Gun
    if (fire && (Time.time - m_gunLastFired >= GUN_FIRE_PERIOD))
    {
      FireGun(aim);
    }
  }

  // Update is called once per frame
  void Update()
  {
    Vector3 aim = m_autoAim == null ? transform.forward : Vector3.Normalize(m_autoAim.UpdateReticle() - m_muzzleTransform.position);
    UpdateControls(aim);
    UnityEditorUpdate();
  }
  
  // Use this for initialization
  void Start()
  {
#if !UNITY_EDITOR
    m_xboxController = new ControllerInput(0, 0.19f);
#endif
    m_gunAudioSource = GetComponent<AudioSource>();
    m_rotorAudioSource = transform.Find("RotorSound").gameObject.GetComponent<AudioSource>();
    SetControlMode(ControlMode.Program);
    ChangeRotorSpeed(ref m_rotorSpeedCoroutine, "RotorSpeed", 3, 0);
    m_gunLastFired = Time.time;
    m_targetForward = transform.forward;

    // Find muzzle and auto aim component
    m_autoAim = GetComponentInChildren<AutoAim>();
    Transform[] transforms = GetComponentsInChildren<Transform>();
    foreach (Transform xform in transforms)
    {
      if (xform.name == "Muzzle")
      {
        m_muzzleTransform = xform;
      }
    }
  }

  private void UnityEditorUpdate()
  {
#if UNITY_EDITOR
    if (Input.GetKey("1"))
      ChangeRotorSpeed(ref m_rotorSpeedCoroutine, "RotorSpeed", 3, 5);
    if (Input.GetKey("2"))
      ChangeRotorSpeed(ref m_rotorSpeedCoroutine, "RotorSpeed", 0, 5);
    if (Input.GetKey(KeyCode.Z))
    {
      SetControlMode(ControlMode.Player);
      m_playerControls.Clear();
    }
    if (Input.GetKey(KeyCode.Comma))
    {
      SetControlMode(ControlMode.Player);
      m_playerControls.longitudinal = Mathf.Clamp(m_playerControls.longitudinal - 0.1F, -1.0F, 1.0F);
    }
    if (Input.GetKey(KeyCode.Period))
    {
      SetControlMode(ControlMode.Player);
      m_playerControls.longitudinal = Mathf.Clamp(m_playerControls.longitudinal + 0.1F, -1.0F, 1.0F);
    }
    if (Input.GetKey(KeyCode.Semicolon))
    {
      SetControlMode(ControlMode.Player);
      m_playerControls.lateral = Mathf.Clamp(m_playerControls.lateral - 0.1F, -1.0F, 1.0F);
    }
    if (Input.GetKey(KeyCode.Quote))
    {
      SetControlMode(ControlMode.Player);
      m_playerControls.lateral = Mathf.Clamp(m_playerControls.lateral + 0.1F, -1.0F, 1.0F);
    }
    if (Input.GetKey(KeyCode.LeftBracket))
    {
      SetControlMode(ControlMode.Player);
      m_playerControls.rotational = Mathf.Clamp(m_playerControls.rotational - 0.1F, -1, 1);
    }
    if (Input.GetKey(KeyCode.RightBracket))
    {
      SetControlMode(ControlMode.Player);
      m_playerControls.rotational = Mathf.Clamp(m_playerControls.rotational + 0.1F, -1, 1);
    }
    if (m_controlMode == ControlMode.Player)
    {
      SetControlMode(ControlMode.Player);
      m_playerControls.altitude = Mathf.Clamp(m_playerControls.altitude + Input.GetAxis("Mouse ScrollWheel"), -1, 1);
    }
#endif
  }
}
