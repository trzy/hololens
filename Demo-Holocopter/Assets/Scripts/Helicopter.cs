using UnityEngine;
using UnityEngine.UI;
using System.Collections;
using System.Collections.Generic;

public class Helicopter: MonoBehaviour
{
  public Text                   m_ui_inputs = null;
  public Text                   m_ui_control_program = null;

  public Bullet                 m_bullet_prefab = null;
  public ParticleEffectsManager m_particle_fx_manager = null;

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

  private IEnumerator m_control_coroutine = null;
  private IEnumerator m_rotor_speed_coroutine = null;
  private ControlMode m_control_mode = ControlMode.Player;
  private Controls m_player_controls = new Controls();
  private Controls m_program_controls = new Controls();
  private bool m_joypad_axes_pressed_last_frame = false;
  private Vector3 m_joypad_lateral_axis;
  private Vector3 m_joypad_longitudinal_axis;
  private Vector3 m_target_forward;
  private float m_gun_last_fired;

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
    if (m_control_coroutine != coroutine)
      StopCoroutine(m_control_coroutine);
    m_control_coroutine = coroutine;
    if (m_control_coroutine != null)
      StartCoroutine(m_control_coroutine);
  }

  public void SetControlMode(ControlMode control_mode, IEnumerator coroutine = null)
  {
    m_control_mode = control_mode;
    SetControlProgram(coroutine);
  }

  private float CrossY(Vector3 a, Vector3 b)
  {
    return -(a.x * b.z - a.z * b.x);
  }

  private float HeadingError(Vector3 target_forward)
  {
    Vector3 target = ProjectXZ(target_forward);
    Vector3 forward = ProjectXZ(transform.forward);
    // Minus sign because error defined as how much we have overshot and need
    // to subtract, assuming positive rotation is clockwise.
    return -Mathf.Sign(CrossY(forward, target)) * Vector3.Angle(forward, target);
  }

  private float HeadingErrorTo(Vector3 target_point)
  {
    return HeadingError(target_point - transform.position);
  }

  private bool GoTo(Vector3 target_position)
  {
    Vector3 to_target = target_position - transform.position;
    float distance = Vector3.Magnitude(to_target);
    float heading_error = HeadingErrorTo(target_position);
    float abs_heading_error = Mathf.Abs(heading_error);
    if (abs_heading_error > ACCEPTABLE_HEADING_ERROR)
      m_program_controls.rotational = -Mathf.Sign(heading_error) * Mathf.Lerp(0.5F, 1.0F, Mathf.Abs(heading_error) / 360.0F);
    else
      m_program_controls.rotational = 0;
    if (distance > ACCEPTABLE_DISTANCE)
    {
      //TODO: reduce intensity once closer? Gradual roll-off within some event horizon.
      Vector3 to_target_norm = to_target / distance;
      m_program_controls.longitudinal = Vector3.Dot(to_target_norm, transform.forward);
      m_program_controls.lateral = Vector3.Dot(to_target_norm, transform.right);
      m_program_controls.altitude = to_target_norm.y;
    }
    else
      return false;
    return true;
  }

  private IEnumerator TraverseWaypointsCoroutine(List<GameObject> waypoints_param)
  {
    List<GameObject> waypoints = new List<GameObject>(waypoints_param);
    foreach (GameObject waypoint in waypoints)
    {
      while (GoTo(waypoint.transform.position))
        yield return null;
    }
    m_program_controls.Clear();
  }

  private IEnumerator FlyToPositionCoroutine(Vector3 target_position)
  {
    while (GoTo(target_position))
      yield return null;
    m_program_controls.Clear();
  }

  public void TraverseWaypoints(List<GameObject> waypoints)
  {
    SetControlMode(ControlMode.Program, TraverseWaypointsCoroutine(waypoints));
  }

  public void FlyToPosition(Vector3 target_position)
  {
    SetControlMode(ControlMode.Program, FlyToPositionCoroutine(target_position));
  }

  public void FireGun()
  {
    Bullet bullet = Instantiate(m_bullet_prefab, transform.position + transform.up * -0.05f + transform.forward * 0.25f, Quaternion.identity) as Bullet;
    bullet.transform.forward = transform.forward; 
    m_gun_last_fired = Time.time;
    GetComponent<AudioSource>().Play();
  }

  private IEnumerator ChangeRotorSpeedCoroutine(string rps_param_name, float target_rps, float ramp_time)
  {
    var animator = GetComponent<Animator>();
    float start_rps = animator.GetFloat(rps_param_name);
    float current_rps = start_rps;
    float time_elapsed = 0.0F;
    while (Mathf.Abs(current_rps - target_rps) > 0.0F)
    {
      current_rps = start_rps + (target_rps - start_rps) * Mathf.Min(time_elapsed / ramp_time, 1.0F);
      animator.SetFloat(rps_param_name, current_rps);
      time_elapsed += Time.deltaTime;
      yield return null;
    }
    //TODO: optimization: disable animation if rotation speed reaches 0
  }

  private void ChangeRotorSpeed(ref IEnumerator coroutine, string rps_param_name, float target_rps, float ramp_time)
  {
    if (coroutine != null)
      StopCoroutine(coroutine);
    coroutine = ChangeRotorSpeedCoroutine(rps_param_name, target_rps, ramp_time);
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

    Controls controls = m_control_mode == ControlMode.Player ? m_player_controls : m_program_controls;
    Rigidbody rb = GetComponent<Rigidbody>();
    Vector3 torque = new Vector3();

    float current_heading_deg = heading;
    Vector3 translational_input = new Vector3(controls.lateral, 0, controls.longitudinal);
    Vector3 translational_force = Quaternion.Euler(new Vector3(0, current_heading_deg, 0)) * translational_input * SCALE * rb.mass * Mathf.Abs(Physics.gravity.y);

    float target_pitch_deg = Mathf.Sign(controls.longitudinal) * Mathf.Lerp(0, MAX_TILT_DEGREES, Mathf.Abs(controls.longitudinal));
    float current_pitch_deg = Mathf.Sign(-transform.forward.y) * Vector3.Angle(transform.forward, ProjectXZ(transform.forward));
    float target_roll_deg = Mathf.Sign(-controls.lateral) * Mathf.Lerp(0, MAX_TILT_DEGREES, Mathf.Abs(controls.lateral));
    float current_roll_deg = Mathf.Sign(transform.right.y) * Vector3.Angle(transform.right, ProjectXZ(transform.right));

    float pitch_error_deg = target_pitch_deg - current_pitch_deg;
    float roll_error_deg = target_roll_deg - current_roll_deg;
    torque += SCALE * PITCH_ROLL_CORRECTIVE_TORQUE * new Vector3(pitch_error_deg, 0, roll_error_deg);
    torque += SCALE * YAW_CORRECTIVE_TORQUE * controls.rotational * Vector3.up;

    float up_world_local_deg = Vector3.Angle(Vector3.up, transform.up); // angle between world and local up
    float hover_force = Mathf.Abs(rb.mass * Physics.gravity.y / Mathf.Cos(up_world_local_deg * Mathf.Deg2Rad));
    float rotor_force = (controls.altitude + 1) * hover_force;

    hover_force = Mathf.Abs(rb.mass * Physics.gravity.y);
    rotor_force = SCALE * controls.altitude * rb.mass * Mathf.Abs(Physics.gravity.y);

    // Apply all forces
    rb.AddRelativeForce(Vector3.up * rotor_force);
    rb.AddForce(Vector3.up * hover_force);
    rb.AddForce(translational_force);
    rb.AddRelativeTorque(torque);
  }

  private void UpdateControls()
  {
    /*
    if (Input.GetKey(KeyCode.Joystick1Button0))
      Debug.Log("Joy A");
    if (Input.GetKey(KeyCode.Joystick1Button1))
      Debug.Log("Joy B");
    if (Input.GetKey(KeyCode.Joystick1Button2))
      Debug.Log("Joy X");
    if (Input.GetKey(KeyCode.Joystick1Button3))
      Debug.Log("Joy Y");
    if (Input.GetKey(KeyCode.Joystick1Button4))
      Debug.Log("Joy LB");
    if (Input.GetKey(KeyCode.Joystick1Button5))
      Debug.Log("Joy RB");
    if (Input.GetKey(KeyCode.Joystick1Button6))
      Debug.Log("Joy View");
    if (Input.GetKey(KeyCode.Joystick1Button7))
      Debug.Log("Joy Menu");
    if (Input.GetKey(KeyCode.Joystick1Button8))
      Debug.Log("Joy LeftStick");
    if (Input.GetKey(KeyCode.Joystick1Button9))
      Debug.Log("Joy RightStick");

    float val = 0;
    if ((val = Input.GetAxis("Horizontal")) != 0) // left stick
      Debug.Log("Horizontal: " + val.ToString());
    if ((val = Input.GetAxis("Vertical")) != 0)   // left stick
      Debug.Log("Vertical: " + val.ToString());
    if ((val = Input.GetAxis("Horizontal2")) != 0) // right stick (4th axis)
      Debug.Log("Horizontal2: " + val.ToString());
    if ((val = Input.GetAxis("Vertical2")) != 0) // right stick (5th axis)
      Debug.Log("Vertical2: " + val.ToString());
    if ((val = Input.GetAxis("LeftTrigger")) != 0)
      Debug.Log("LeftTrigger: " + val.ToString());
    if ((val = Input.GetAxis("RightTrigger")) != 0)
      Debug.Log("RightTrigger: " + val.ToString());

    m_player_controls.lateral = Input.GetAxis("Horizontal");
    m_player_controls.longitudinal = Input.GetAxis("Vertical");
    m_player_controls.rotational = Input.GetAxis("Horizontal2");
    m_player_controls.altitude = -Input.GetAxis("Vertical2");
    */

    // Determine angle between user gaze vector and helicopter forward, in xz
    // plane
    Vector3 view = new Vector3(Camera.main.transform.forward.x, 0, Camera.main.transform.forward.z);
    Vector3 forward = new Vector3(transform.forward.x, 0, transform.forward.z);
    float angle = Vector3.Angle(view, forward);

    // Get current joypad axis values
    float hor = Input.GetAxis("Horizontal");
    float ver = Input.GetAxis("Vertical");
#if UNITY_EDITOR
    float lt = Input.GetAxis("Axis9");
    float rt = Input.GetAxis("Axis10");
#else
    float axis3 = Input.GetAxis("Axis3");
    float lt = Mathf.Max(axis3, 0);
    float rt = -Mathf.Min(axis3, 0);
#endif

    // Any of the main axes (which are relative to orientation) pressed?
    bool pressed = (hor != 0) || (ver != 0);
    if (pressed && !m_joypad_axes_pressed_last_frame)
    {
      // Joypad was not pressed last frame, reorient based on current view position
      m_joypad_lateral_axis = Vector3.Normalize(Camera.main.transform.right);
      m_joypad_longitudinal_axis = Vector3.Normalize(Camera.main.transform.forward);

      // Compute desired orientation vector. Vector3.Angle() returns [0,180),
      // so to determine left vs. right relative to camera, dot with camera's
      // right vector.
      if (angle >= 45 && angle <= (90 + 45))
      {
        // Helicopter orientation is closer to being sideways
        m_target_forward = Camera.main.transform.right;
        if (Vector3.Dot(transform.forward, Camera.main.transform.right) < 0)
          m_target_forward *= -1;
      }
      else
      {
        // Helicopter orientation is closer to being along view vector
        m_target_forward = Camera.main.transform.forward;
      }
    }
    m_joypad_axes_pressed_last_frame = pressed;

    // Apply correction for heading (rotate toward desired orientation)
    if (pressed)
    {
      // Only perform update when user is applying input
      float heading_error = HeadingError(m_target_forward);
      float heading_error_magnitude = Mathf.Abs(heading_error);
      if (heading_error_magnitude > ACCEPTABLE_HEADING_ERROR)
        m_player_controls.rotational = -Mathf.Sign(heading_error) * Mathf.Lerp(0.8F, 1.0F, heading_error_magnitude / 360.0F);
      else
        m_player_controls.rotational = 0;
    }
    else
      m_player_controls.rotational = 0;

    // Apply longitudinal and lateral controls. Compute projection of joypad
    // lateral/longitudinal axes onto helicopter's.
    float joypad_longitudinal_to_heli_longitudinal = Vector3.Dot(m_joypad_longitudinal_axis, transform.forward);
    float joypad_longitudinal_to_heli_lateral = Vector3.Dot(m_joypad_longitudinal_axis, transform.right);
    float joypad_lateral_to_heli_longitudinal = Vector3.Dot(m_joypad_lateral_axis, transform.forward);
    float joypad_lateral_to_heli_lateral = Vector3.Dot(m_joypad_lateral_axis, transform.right);
    m_player_controls.longitudinal = joypad_longitudinal_to_heli_longitudinal * ver + joypad_lateral_to_heli_longitudinal * hor;
    m_player_controls.lateral = joypad_longitudinal_to_heli_lateral * ver + joypad_lateral_to_heli_lateral * hor;

    // Altitude control (trigger axes each range from 0 to 1)
    m_player_controls.altitude = -lt + rt;

    // Gun
    if (Input.GetKey(KeyCode.Joystick1Button0))
    {
      if (Time.time - m_gun_last_fired >= GUN_FIRE_PERIOD)
      {
        FireGun();
        //m_particle_fx_manager.CreateCloud(transform.position + transform.forward, 0.5f, 5);
      }
    }
    if (Input.GetButtonDown("Fire2"))
    {
      m_particle_fx_manager.CreateExplosionCloud(transform.position + transform.forward * 1.5f, 0.3f, 5);
    }
  }

  // Update is called once per frame
  void Update()
  {
    UpdateControls();
    UnityEditorUpdate();
  }
  
  // Use this for initialization
  void Start()
  {
    SetControlMode(ControlMode.Program);
    ChangeRotorSpeed(ref m_rotor_speed_coroutine, "RotorSpeed", 3, 0);
    m_gun_last_fired = Time.time;
    m_target_forward = transform.forward;
  }

  private void UnityEditorUpdate()
  {
#if UNITY_EDITOR
    if (Input.GetKey("1"))
      ChangeRotorSpeed(ref m_rotor_speed_coroutine, "RotorSpeed", 3, 5);
    if (Input.GetKey("2"))
      ChangeRotorSpeed(ref m_rotor_speed_coroutine, "RotorSpeed", 0, 5);
    if (Input.GetKey(KeyCode.Z))
    {
      SetControlMode(ControlMode.Player);
      m_player_controls.Clear();
    }
    if (Input.GetKey(KeyCode.Comma))
    {
      SetControlMode(ControlMode.Player);
      m_player_controls.longitudinal = Mathf.Clamp(m_player_controls.longitudinal - 0.1F, -1.0F, 1.0F);
    }
    if (Input.GetKey(KeyCode.Period))
    {
      SetControlMode(ControlMode.Player);
      m_player_controls.longitudinal = Mathf.Clamp(m_player_controls.longitudinal + 0.1F, -1.0F, 1.0F);
    }
    if (Input.GetKey(KeyCode.Semicolon))
    {
      SetControlMode(ControlMode.Player);
      m_player_controls.lateral = Mathf.Clamp(m_player_controls.lateral - 0.1F, -1.0F, 1.0F);
    }
    if (Input.GetKey(KeyCode.Quote))
    {
      SetControlMode(ControlMode.Player);
      m_player_controls.lateral = Mathf.Clamp(m_player_controls.lateral + 0.1F, -1.0F, 1.0F);
    }
    if (Input.GetKey(KeyCode.LeftBracket))
    {
      SetControlMode(ControlMode.Player);
      m_player_controls.rotational = Mathf.Clamp(m_player_controls.rotational - 0.1F, -1, 1);
    }
    if (Input.GetKey(KeyCode.RightBracket))
    {
      SetControlMode(ControlMode.Player);
      m_player_controls.rotational = Mathf.Clamp(m_player_controls.rotational + 0.1F, -1, 1);
    }
    if (m_control_mode == ControlMode.Player)
    {
      SetControlMode(ControlMode.Player);
      m_player_controls.altitude = Mathf.Clamp(m_player_controls.altitude + Input.GetAxis("Mouse ScrollWheel"), -1, 1);
    }
#endif
  }
}
