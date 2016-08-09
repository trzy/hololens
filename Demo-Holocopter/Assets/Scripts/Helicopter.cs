using UnityEngine;
using UnityEngine.UI;
using System.Collections;
using System.Collections.Generic;

public class Helicopter : MonoBehaviour
{
  public Text         m_ui_inputs = null;
  public Text         m_ui_control_program = null;

  private IEnumerator m_rotor_speed_coroutine = null;

  private const float SCALE = .06f;
  private const float MAX_TILT_DEGREES = 30;
  private const float MAX_TORQUE = 25000 * SCALE; //TODO: reformulate in terms of mass?
  private const float PITCH_ROLL_CORRECTIVE_TORQUE = MAX_TORQUE / 5.0f;
  private const float YAW_CORRECTIVE_TORQUE = MAX_TORQUE * 4;
  private const float ACCEPTABLE_DISTANCE = 5 * SCALE;
  private const float ACCEPTABLE_HEADING_ERROR = 10;

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

  enum ControlMode
  {
    Player,
    Program
  };

  private ControlMode m_control_mode = ControlMode.Player;
  private Controls m_player_controls = new Controls();
  private Controls m_program_controls = new Controls();
  private IEnumerator m_control_coroutine = null;

  private void SetControlProgram(IEnumerator coroutine)
  {
    if (m_control_coroutine != null)
      StopCoroutine(m_control_coroutine);
    m_control_coroutine = coroutine;
    if (m_control_coroutine != null)
      StartCoroutine(m_control_coroutine);
  }

  private void SetControlMode(ControlMode control_mode, IEnumerator coroutine = null)
  {
    m_control_mode = control_mode;
    SetControlProgram(coroutine);
  }

  private float CrossY(Vector3 a, Vector3 b)
  {
    return -(a.x * b.z - a.z * b.x);
  }

  private float HeadingError(Vector3 target_point)
  {
    Vector3 to_point = ProjectXZ(target_point - transform.position);
    Vector3 forward = ProjectXZ(transform.forward);
    // Minus sign because error defined as how much we have overshot and need
    // to subtract, assuming positive rotation is clockwise.
    return -Mathf.Sign(CrossY(forward, to_point)) * Vector3.Angle(forward, to_point);
  }

  private bool GoToWaypoint(GameObject waypoint)
  {
    Vector3 to_waypoint = waypoint.transform.position - transform.position;
    float distance = Vector3.Magnitude(to_waypoint);
    float heading_error = HeadingError(waypoint.transform.position);
    float abs_heading_error = Mathf.Abs(heading_error);
    if (abs_heading_error > ACCEPTABLE_HEADING_ERROR)
      m_program_controls.rotational = -Mathf.Sign(heading_error) * Mathf.Lerp(0.5F, 1.0F, Mathf.Abs(heading_error) / 360.0F);
    else
      m_program_controls.rotational = 0;
    if (distance > ACCEPTABLE_DISTANCE)
    {
      //TODO: reduce intensity once closer? Gradual roll-off within some event horizon.
      Vector3 to_waypoint_norm = to_waypoint / distance;
      m_program_controls.longitudinal = Vector3.Dot(to_waypoint_norm, transform.forward);
      m_program_controls.lateral = Vector3.Dot(to_waypoint_norm, transform.right);
      m_program_controls.altitude = to_waypoint_norm.y;
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
      while (GoToWaypoint(waypoint))
        yield return null;
    }
    m_program_controls.Clear();
  }

  public void TraverseWaypoints(List<GameObject> waypoints)
  {
    SetControlMode(ControlMode.Program, TraverseWaypointsCoroutine(waypoints));
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

  // Update is called once per frame
  void Update()
  {
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
  }
  
  // Use this for initialization
  void Start()
  {
    SetControlMode(ControlMode.Program);
    ChangeRotorSpeed(ref m_rotor_speed_coroutine, "RotorSpeed", 3, 0);
  }
}
