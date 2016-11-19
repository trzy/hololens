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

  [Tooltip("Bullet ricochet sound.")]
  public AudioClip soundRicochet;

  private AudioSource m_audio_source;
  private IMissionHandler m_current_mission;
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
    m_audio_source = GetComponent<AudioSource>();
    m_current_mission = LevelManager.Instance.currentMission;
        
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
          Quaternion target_direction = Quaternion.FromToRotation(turret_forward_axis, to_player) * m_turret.rotation;
          float angle_to_player = Vector3.Angle(turret_forward_axis, to_player);
          if (angle_to_player > 2)
          {
            // Turn toward player
            float time = angle_to_player / turretTrackingSpeed;
            m_turret.rotation = Quaternion.Slerp(m_turret.rotation, target_direction, Time.deltaTime / time);
          }
          else
          {
            // Raise gun
            Vector3 target_angles = m_gun.localRotation.eulerAngles;
            target_angles.z = maxGunAngle;  // bone -- and gun -- axis is along x, so rotate about z
            Quaternion target_elevation = Quaternion.Euler(target_angles);
            Vector3 current_gun_vector = m_gun.localRotation * Vector3.right;
            Vector3 target_gun_vector = target_elevation * Vector3.right;
            float angle_to_target = Mathf.Abs(Vector3.Angle(current_gun_vector, target_gun_vector));
            float time = angle_to_target / turretScanSpeed;
            m_gun.localRotation = Quaternion.Lerp(m_gun.localRotation, target_elevation, Time.deltaTime / time);
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

  void OnCollisionEnter(Collision collision)
  {
    GameObject target = collision.collider.gameObject;
    Debug.Log("Collided with: " + target.tag);
    if (target.CompareTag("Bullet"))
    {
      m_audio_source.Stop();
      m_audio_source.PlayOneShot(soundRicochet);
      m_current_mission.OnEnemyHitByPlayer(this);
    }
  }
}