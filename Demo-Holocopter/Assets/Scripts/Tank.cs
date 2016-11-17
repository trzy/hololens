using UnityEngine;
using System.Collections;

public class Tank : MonoBehaviour
{
  [Tooltip("Maximum angle of gun in degrees.")]
  public float maxGunAngle = -20;

  [Tooltip("Minimum angle of gun.")]
  public float minGunAngle = 0;

  [Tooltip("Rate of turret rotation in degrees/sec during scanning state.")]
  public float turretScanSpeed = 180 / 10;

  [Tooltip("Rate of turret rotation in degrees/sec during tracking state.")]
  public float turretTrackingSpeed = 180 / 3;

  [Tooltip("Distance from player in meters at which to track.")]
  public float playerTrackingDistance = 1.5f;

  private Transform m_turret = null;
  private Transform m_gun = null;
  private Quaternion m_turret_zero_rotation;
  private Quaternion m_turret_start_rotation;
  private Quaternion m_turret_end_rotation;
  private Quaternion m_gun_zero_rotation;
  private Quaternion m_gun_start_rotation;
  private Quaternion m_gun_end_rotation;
  private float m_t0 = 0;
  private float m_t1 = 0;

  enum TurretState
  {
    Dead,
    ScanningSweep,
    ScanningSleep,
    TrackingStart,
    Tracking,
    TrackingEnd
  };

  private TurretState m_state = TurretState.Dead;
  private float[] m_angles = { 90, 180, 45, 270, 0, 135, 90, 30, 180, 45, 270, 60, 0, 120 };
  private int m_scan_idx = 0;

  void Awake()
  {
    // Angles cannot be negative
    if (maxGunAngle < 0)
      maxGunAngle += 360;
    maxGunAngle %= 360;
    // Find bones
    Transform[] transforms = GetComponentsInChildren<Transform>();
    foreach (Transform xform in transforms)
    {
      if (xform.name == "TurretBone")
        m_turret = xform;
      else if (xform.name == "GunBone")
        m_gun = xform;
    }
    m_turret_zero_rotation = m_turret.localRotation;
    m_gun_zero_rotation = m_gun.localRotation;
  }

  void Start()
  {
    m_state = TurretState.ScanningSleep;
    m_t0 = Time.time;
    m_t1 = Time.time + 1;

    Debug.Log("TURRET=" + m_turret.localRotation.eulerAngles + " " + m_turret.up.ToString("F3"));
  }

  // Update is called once per frame
  void Update()
  {
    Vector3 to_player = Camera.main.transform.position - transform.position;
    to_player.y = 0;  // we only care about distance along ground plane
    float distance_to_player = Vector3.Magnitude(to_player);
    float now = Time.time;
    float delta = now - m_t0;
    switch (m_state)
    {
      case TurretState.Dead:
      default:
        break;
      case TurretState.ScanningSleep:
        if (distance_to_player <= playerTrackingDistance)
        {
          m_state = TurretState.TrackingStart;
        }
        else if (now >= m_t1)
        {
          Vector3 old_angles = m_turret.localRotation.eulerAngles;
          Vector3 new_angles = old_angles;
          new_angles.y = m_angles[m_scan_idx++ % m_angles.Length];
          m_turret_start_rotation = m_turret.localRotation;
          m_turret_end_rotation = Quaternion.Euler(new_angles);
          m_t0 = now;
          m_t1 = now + Mathf.Abs(new_angles.y - old_angles.y) / turretScanSpeed;
          m_state = TurretState.ScanningSweep;
        }
        break;
      case TurretState.ScanningSweep:
        if (distance_to_player <= playerTrackingDistance)
        {
          m_state = TurretState.TrackingStart;
        }
        else if (now < m_t1)
          m_turret.localRotation = Quaternion.Slerp(m_turret_start_rotation, m_turret_end_rotation, delta / (m_t1 - m_t0));
        else
        {
          m_t0 = now;
          m_t1 = now + 2;
          m_state = TurretState.ScanningSleep;
        }
        break;
      case TurretState.TrackingStart:
        if (distance_to_player > playerTrackingDistance)
        {
          m_state = TurretState.TrackingEnd;
          m_gun_start_rotation = m_gun.localRotation;
          m_gun_end_rotation = m_gun_zero_rotation;
          m_t0 = now;
          m_t1 = now + 2;
        }
        else
        {
          // Current turret forward direction transformed to world space
          Vector3 turret_forward_axis = m_turret.rotation * Vector3.forward;
          // Construct a rotation that places turret in current position, then
          // rotates to face the player
          to_player.y = 0;  // Want to rotate in xz plane only
          Quaternion target = Quaternion.FromToRotation(turret_forward_axis, to_player) * m_turret.rotation;
          float angle_to_player = Vector3.Angle(turret_forward_axis, to_player);
          if (angle_to_player > 2)
          {
            // Turn toward player
            float time = angle_to_player / turretTrackingSpeed;
            m_turret.rotation = Quaternion.Slerp(m_turret.rotation, target, Time.deltaTime / time);
          }
          else
          {
            // Raise gun
            Vector3 current_angles = m_gun.rotation.eulerAngles;
            Vector3 target_angles = current_angles;
            target_angles.z = maxGunAngle;
            float time = Mathf.Abs(target_angles.z - current_angles.z) / turretScanSpeed;
            m_gun.rotation = Quaternion.Lerp(m_gun.rotation, Quaternion.Euler(target_angles), Time.deltaTime / time);
          }
        }
        break;
      case TurretState.TrackingEnd:
        // Lower gun then return to scanning
        if (now < m_t1)
          m_gun.localRotation = Quaternion.Lerp(m_gun_start_rotation, m_gun_end_rotation, delta / (m_t1 - m_t0));
        else
        {
          m_t1 = now + 1;
          m_state = TurretState.ScanningSleep;
        }
        break;
    }
  }
}
