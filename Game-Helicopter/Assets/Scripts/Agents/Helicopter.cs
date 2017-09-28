/*
 * TODO:
 * -----
 * - Throttle clamping logic really makes no sense. Probably should just
 *   multiply Vector2(lateral,longitudinal) directly by throttle value.
 */

using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(Rigidbody))]
public class Helicopter: MonoBehaviour
{
  public struct Controls
  {
    public float longitudinal;  // [-1,1] forward/back in xz plane relative to current heading (local z projected onto xz)
    public float lateral;       // [-1,1] right/left in xz plane relative to current heading (local x projected onto xz)
    public float rotational;    // [-1,1] counter/clockwise rotation in xz plane
    public float altitude;      // [-1,1] rotor force along local up (-1=free-fall, 0=hover)

    // Throttle can range from [0,Infinite]. Controls are normalized and then
    // scaled by the throttle.
    public void ApplyThrottle(float throttle)
    {
      // Throttle cannot be less than 0
      throttle = Mathf.Max(0, throttle);

      // Apply throttle to translational controls. Note that because each of the
      // two axes has a range of [-1,+1], the maximum magnitude of the vector is
      // sqrt(2).
      float sqrt2 = 1.414213562373f;
      Vector2 xzControls = Vector2.ClampMagnitude(new Vector2(lateral, longitudinal), 1) * (sqrt2 * throttle);
      lateral = xzControls.x;
      longitudinal = xzControls.y;
    }

    // Three axes for main engine, each ranging from [-1,1]. This computes the
    // engine power from [0,1] multipled by throttle ([0,throttle], 
    // effectively).
    public float EngineMagnitude()
    {
      // Altitude is special because -1 is basically freefall (no force),
      // whereas in other directions, -1 and 1 both indicate full power
      float altitudeMagnitude = (altitude + 1) * 0.5f;  // [-1,1] -> [0,1]

      // Maximum magnitude of vector is sqrt(3), so we divide by that to scale
      // to [0,1] (in the case of throttle=1).
      float invSqrt3 = 1 / 1.7320508075688772935274463415059f;
      return invSqrt3 * new Vector3(altitudeMagnitude, longitudinal, lateral).magnitude;
    }

    public void Clear()
    {
      longitudinal = 0;
      lateral = 0;
      rotational = 0;
      altitude = 0;
    }

    public Controls(float _longitudinal, float _lateral, float _rotational, float _altitude)
    {
      longitudinal = _longitudinal;
      lateral = _lateral;
      rotational = _rotational;
      altitude = _altitude;
    }
  }

  [Tooltip("Helicopter main rotor.")]
  public HelicopterRotor mainRotor;

  [Tooltip("Helicopter tail rotor.")]
  public HelicopterRotor tailRotor;

  [Tooltip("Muzzle (bullet spawn point).")]
  public Transform muzzle;

  [Tooltip("Gun audio source.")]
  public AudioSource gunAudioSource;

  [Tooltip("Rotor audio source.")]
  public AudioSource rotorAudioSource;

  [Tooltip("Bullet prefab to spawn.")]
  public Bullet bulletPrefab; 

  public float heading
  {
    get { return transform.eulerAngles.y; } // Euler angle is safe here; equivalent to manual projection of forward onto xz plane
  }

  public Controls controls
  {
    get { return m_controls; }
    set { m_controls = value; }
  }

  private Rigidbody m_rb;
  private Vector3 m_collisionForce = Vector3.zero;
  private float m_collisionReactionFinished = 0;

  private IEnumerator m_rotorSpeedCoroutine = null;
  private Controls m_controls = new Controls(0, 0, 0, 0);

  private Bullet[] m_bulletPool;
  private int m_nextBulletIdx = 0;
  private float m_gunLastFired;

  private const float TRANSLATIONAL_ACCELERATION = .06f * 9.81f;
  private const float ALTITUDE_ACCELERATION = .06f * 9.81f;

  private const float MAX_TILT_DEGREES = 30;

  private const float MAX_PITCH_ROLL_TORQUE = 0.1f / ((1f / 12) * (0.15f * 0.15f + 0.8f * 0.8f));
  private const float PITCH_ROLL_CORRECTIVE_TORQUE = MAX_PITCH_ROLL_TORQUE / 10;

  private const float MAX_YAW_TORQUE = .015f / ((1f / 12) * (0.15f * 0.15f + 0.4f * 0.4f));
  private const float YAW_CORRECTIVE_TORQUE = MAX_YAW_TORQUE * 4;

  private const float GUN_FIRE_PERIOD = 1f / 8f;//1f / 5f;

  public void FireGun()
  {
    if (Time.time - m_gunLastFired < GUN_FIRE_PERIOD)
      return;

    // Reuse a bullet from the pool
    Bullet bullet = m_bulletPool[m_nextBulletIdx];
    if (bullet.gameObject.activeSelf)
      return; // no free bullets currently
    m_nextBulletIdx = (m_nextBulletIdx + 1) % m_bulletPool.Length;

    // Sets its position and direction and fire
    bullet.transform.position = muzzle.position;
    bullet.transform.rotation = muzzle.rotation;
    bullet.gameObject.SetActive(true);
    m_gunLastFired = Time.time;
    //m_gunAudioSource.Play();
  }

  private IEnumerator RotorSpeedCoroutine(HelicopterRotor rotor, float targetVelocity, float rampTime)
  {
    float startVelocity = rotor.angularVelocity;
    float currentVelocity = startVelocity;
    float timeElapsed = 0;
    while (Mathf.Abs(currentVelocity - targetVelocity) > 0)
    {
      currentVelocity = startVelocity + (targetVelocity - startVelocity) * Mathf.Min(timeElapsed / rampTime, 1);
      rotor.angularVelocity = currentVelocity;
      timeElapsed += Time.deltaTime;
      yield return null;
    }
  }

  public void ChangeRotorSpeed(HelicopterRotor rotor, float targetVelocity, float rampTime)
  {
    if (rotor == null)
      return;
    if (m_rotorSpeedCoroutine != null)
      StopCoroutine(m_rotorSpeedCoroutine);
    m_rotorSpeedCoroutine = RotorSpeedCoroutine(rotor, targetVelocity, rampTime);
    StartCoroutine(m_rotorSpeedCoroutine);
  }

  private void UpdateDynamics()
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
     * 
     * Note on torque calculation
     * --------------------------
     * This might be completely wrong -- I need to review my basic physics :) I
     * gleaned this from a cursory glance at the PhysX docs while trying to 
     * convert torque values I had specified as actual torques (N*m) into
     * angular acceleration values suitable for use with ForceMode.Acceleration.
     *
     * When adding torque in the default force mode (in units of N*m), the
     * value is multiplied by the inverse inertia in order to get acceleration.
     * Therfore, the adjustment from torque -> acceleration here multiplies by
     * the moment of inertia of a box: (m/12) * (x^2 + y^2), where x and y are
     * the dimensions along the two axes perpendicular to the rotation axis.
     *
     * For yaw, given a torque, it seems we can eliminate mass by converting to
     * acceleration using this approximate formula:
     *
     *  Acceleration = Torque / Inertia
     *  Inertia = (Mass / 12) * (Width^2 + Depth^2)
     *
     * The masses can then by canceled from torque and inertia.
     */

    Controls controls = m_controls;
    Vector3 torque = Vector3.zero;

    // Translational motion, rotated into absolute (world) coordinate space
    float currentHeadingDeg = heading;
    Vector3 translationalInput = new Vector3(controls.lateral, 0, controls.longitudinal);
    Vector3 translationalForce = Quaternion.Euler(new Vector3(0, currentHeadingDeg, 0)) * translationalInput * TRANSLATIONAL_ACCELERATION;

    // Pitch, roll, and yaw
    float targetPitchDeg = Mathf.Sign(controls.longitudinal) * Mathf.Lerp(0, MAX_TILT_DEGREES, Mathf.Abs(controls.longitudinal));
    float currentPitchDeg = Mathf.Sign(-transform.forward.y) * Vector3.Angle(transform.forward, MathHelpers.Azimuthal(transform.forward));
    float targetRollDeg = Mathf.Sign(-controls.lateral) * Mathf.Lerp(0, MAX_TILT_DEGREES, Mathf.Abs(controls.lateral));
    float currentRollDeg = Mathf.Sign(transform.right.y) * Vector3.Angle(transform.right, MathHelpers.Azimuthal(transform.right));    
    float pitchErrorDeg = targetPitchDeg - currentPitchDeg;
    float rollErrorDeg = targetRollDeg - currentRollDeg;
    torque += PITCH_ROLL_CORRECTIVE_TORQUE * new Vector3(pitchErrorDeg, 0, rollErrorDeg);
    torque += YAW_CORRECTIVE_TORQUE * controls.rotational * Vector3.up;

    // Acceleration from force exerted by rotors
    float hoverAcceleration = Mathf.Abs(Physics.gravity.y);
    float rotorAcceleration = ALTITUDE_ACCELERATION * controls.altitude;

    // Apply all forces
    m_rb.AddRelativeForce(Vector3.up * rotorAcceleration, ForceMode.Acceleration);
    m_rb.AddForce(Vector3.up * hoverAcceleration, ForceMode.Acceleration);
    m_rb.AddForce(translationalForce, ForceMode.Acceleration);
    m_rb.AddRelativeTorque(torque, ForceMode.Acceleration);

    // Pitch of engine sound is based on tilt, which is based on desired speed
    //float engineOutput = Mathf.Max(Mathf.Abs(controls.longitudinal), Mathf.Abs(controls.lateral));
    //m_rotorAudioSource.pitch = Mathf.Lerp(1.0f, 1.3f, engineOutput);

    // Rotor speed
    float mainRevolutionsPerSecond = Mathf.Lerp(2.5f, 6, Mathf.Clamp01(controls.EngineMagnitude()));
    float tailRevolutionsPerSecond = Mathf.Lerp(2.5f, 6, (Mathf.Clamp(controls.rotational, -1, 1) + 1) * 0.5f);
    if (mainRotor != null)
      mainRotor.angularVelocity = mainRevolutionsPerSecond * 360;
    if (tailRotor != null)
      tailRotor.angularVelocity = tailRevolutionsPerSecond * 360;

  }

  //private List<Collider> 
  private void FixedUpdate()
  {
    UpdateDynamics();


    BoxCollider box = GetComponent<BoxCollider>();
    Vector3 halfExtents = 0.5f * Footprint.Measure(gameObject);
    Collider[] overlapping = Physics.OverlapBox(transform.TransformPoint(box.center), halfExtents, transform.rotation, PlayspaceManager.spatialLayerMask);
    if (overlapping.Length > 0)
    {
      Vector3 resolution = Vector3.zero;
      foreach (Collider c in overlapping)
      {
        Vector3 direction = Vector3.zero;
        float distance = 0;
        Physics.ComputePenetration(box, transform.position, transform.rotation, c, c.transform.position, c.transform.rotation, out direction, out distance);
        resolution += distance * direction;
      }

      Vector3 reactionDirection = resolution.normalized;
      Vector3 velocity = m_rb.velocity;
      m_rb.velocity = Vector3.zero;
      m_rb.angularVelocity = Vector3.zero;
      m_rb.AddForce(reactionDirection * 1.5f, ForceMode.VelocityChange);
      Debug.Log("Velocity " + velocity + " -> " + m_rb.velocity);
    }

      return;

      // Layers must be set up such that helicopter cannot collide with itself

      /*
      BoxCollider box = GetComponent<BoxCollider>();
    Vector3 halfExtents = 0.5f * Footprint.Measure(gameObject);
    Collider[] overlapping = Physics.OverlapBox(transform.TransformPoint(box.center), halfExtents, transform.rotation, PlayspaceManager.spatialLayerMask);
    Vector3 reactionDirection = Vector3.zero;
    foreach (Collider c in overlapping)
    {
      //Debug.Log("overlapping " + c.name);
      //Debug.Log("closest point: " + c.ClosestPoint(transform.position));
      //reactionDirection += (transform.position - c.ClosestPoint(transform.position)).normalized;
    }
    if (overlapping.Length > 0)
      reactionDirection = -m_rb.velocity;
    m_rb.AddForce(reactionDirection.normalized * 0.5f, ForceMode.VelocityChange);
    LineRenderer lr = GetComponent<LineRenderer>();
    lr.positionCount = 2;
    lr.SetPosition(0, transform.position);
    lr.SetPosition(1, reactionDirection.normalized + transform.position);
    lr.useWorldSpace = true;
    */
  }

  private void OnCollisionEnter(Collision collision)
  {
    /*
    Debug.Log("COLLIDED");
    Vector3 normal = collision.contacts[0].normal;
    m_rb.velocity = Vector3.zero;
    m_rb.angularVelocity = Vector3.zero;
    m_rb.AddForce(normal * 0.75f, ForceMode.VelocityChange);

    //m_collisionForce = normal * 0.5f;

    LineRenderer lr = GetComponent<LineRenderer>();
    lr.positionCount = 2;
    lr.SetPosition(0, collision.contacts[0].point);
    lr.SetPosition(1, collision.contacts[0].point + normal);
    lr.useWorldSpace = true;
    */
  }

  private void Start()
  {
  }

  private void Awake()
  {
    m_rb = GetComponent<Rigidbody>();
    m_bulletPool = new Bullet[(int)Mathf.Ceil(bulletPrefab.lifeTime / GUN_FIRE_PERIOD) + 1];
    for (int i = 0; i < m_bulletPool.Length; i++)
    {
      m_bulletPool[i] = Instantiate(bulletPrefab, transform) as Bullet;
      Vector3 bulletScale = m_bulletPool[i].transform.localScale;
      Vector3 parentScale = transform.localScale;
      Vector3 undoScale = new Vector3(bulletScale.x / parentScale.x, bulletScale.y / parentScale.y, bulletScale.z / parentScale.z);
      m_bulletPool[i].transform.localScale = undoScale;
      m_bulletPool[i].gameObject.SetActive(false);
    }
    m_gunLastFired = Time.time;
  }
}
