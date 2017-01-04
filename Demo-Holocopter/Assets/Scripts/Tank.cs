using UnityEngine;
using System.Collections;

public class Tank : MonoBehaviour
{
  [Tooltip("Tank hit points.")]
  public float lifePoints = 25;

  [Tooltip("Maximum angle of gun in degrees.")]
  public float maxGunAngle = -20;

  [Tooltip("Minimum angle of gun.")]
  public float minGunAngle = 0;

  [Tooltip("Rate of turret rotation in degrees/sec during scanning state.")]
  public float turretScanSpeed = 180 / 10;

  [Tooltip("Rate of turret rotation in degrees/sec during tracking state.")]
  public float turretTrackingSpeed = 180 / 10;

  [Tooltip("Distance from player in meters at which to track.")]
  public float playerTrackingDistance = 1.5f;

  [Tooltip("Bullet ricochet sound.")]
  public AudioClip soundRicochet;

  [Tooltip("Explosion sounds, chosen randomly.")]
  public AudioClip[] soundExplosion;

  [Tooltip("Wreckage model.")]
  public GameObject wreckagePrefab;

  private AudioSource m_audioSource = null;
  private IMissionHandler m_currentMission = null;
  private Transform m_turret = null;
  private Transform m_gun = null;
  private Quaternion m_turretStartRotation;
  private Quaternion m_turretEndRotation;
  private Quaternion m_gunZeroRotation;
  private Quaternion m_gunStartRotation;
  private Quaternion m_gunEndRotation;
  private float m_t0 = 0;
  private float m_t1 = 0;
  private bool m_dead = false;

  enum TurretState
  {
    ScanningSleep,
    ScanningSweep,
    TrackingStart,
    Tracking,
    TrackingEnd
  };

  private TurretState m_state = TurretState.ScanningSleep;
  private float[] m_angles = { 90, 180, 45, 270, 0, 135, 90, 30, 180, 45, 270, 60, 0, 120 };

  void Awake()
  {
    m_audioSource = GetComponent<AudioSource>();
    m_currentMission = LevelManager.Instance.currentMission;
        
    // Find bones
    Transform[] transforms = GetComponentsInChildren<Transform>();
    foreach (Transform xform in transforms)
    {
      if (xform.name == "TurretBone")
      {
        m_turret = xform;
      }
      else if (xform.name == "GunBone")
      {
        m_gun = xform;
      }
    }
    m_gunZeroRotation = m_gun.localRotation;
  }

  private void Start()
  {
    m_state = TurretState.ScanningSleep;
    m_t0 = Time.time;
    m_t1 = Time.time + 1;

    Debug.Log("TURRET=" + m_turret.localRotation.eulerAngles + " " + m_turret.up.ToString("F3"));
  }

  // Update is called once per frame
  private void Update()
  {
    if (m_dead)
    {
      // Wait until sounds have stopped playing then kill
      if (!m_audioSource.isPlaying)
      {
        Destroy(this.gameObject);
      }
      return;
    }
    Vector3 toPlayer = Camera.main.transform.position - transform.position;
    toPlayer.y = 0; // we only care about distance along ground plane
    float distanceToPlayer = Vector3.Magnitude(toPlayer);
    float now = Time.time;
    float delta = now - m_t0;
    switch (m_state)
    {
      default:
        break;
      case TurretState.ScanningSleep:
        if (distanceToPlayer <= playerTrackingDistance)
        {
          m_state = TurretState.TrackingStart;
        }
        else if (now >= m_t1)
        {
          Vector3 old_angles = m_turret.localRotation.eulerAngles;
          Vector3 new_angles = old_angles;
          new_angles.y = m_angles[Random.Range(0, m_angles.Length)];
          m_turretStartRotation = m_turret.localRotation;
          m_turretEndRotation = Quaternion.Euler(new_angles);
          m_t0 = now;
          m_t1 = now + Mathf.Abs(new_angles.y - old_angles.y) / turretScanSpeed;
          m_state = TurretState.ScanningSweep;
        }
        break;
      case TurretState.ScanningSweep:
        if (distanceToPlayer <= playerTrackingDistance)
        {
          m_state = TurretState.TrackingStart;
        }
        else if (now < m_t1)
        {
          m_turret.localRotation = Quaternion.Slerp(m_turretStartRotation, m_turretEndRotation, delta / (m_t1 - m_t0));
        }
        else
        {
          m_t0 = now;
          m_t1 = now + 2;
          m_state = TurretState.ScanningSleep;
        }
        break;
      case TurretState.TrackingStart:
        if (distanceToPlayer > playerTrackingDistance)
        {
          m_state = TurretState.TrackingEnd;
          m_gunStartRotation = m_gun.localRotation;
          m_gunEndRotation = m_gunZeroRotation;
          m_t0 = now;
          m_t1 = now + 2;
        }
        else
        {
          /*
           * Player position is transformed to turret-local space and then the
           * direction to rotate at the tracking speed is determined using a
           * cross product. 
           * 
           * We compare two vectors: turret aim direction and player in local
           * YZ (X is the axis we rotate the turret about in its local space).
           * The cross product produces a vector along X and the magnitude is:
           * |aimVector|*|playerLocalYZ|*sin(angle) = 1*1*sin(angle) = 
           * sin(angle).
           */
          Vector3 playerLocalPos = m_turret.transform.InverseTransformPoint(Camera.main.transform.position);
          Vector3 playerLocalYZ = Vector3.Normalize(new Vector3(0, playerLocalPos.y, playerLocalPos.z));
          Vector3 aimVector = Vector3.forward;  // direction turret is pointing
          float sinAngle = Vector3.Cross(aimVector, playerLocalYZ).x;
          float direction = Mathf.Sign(sinAngle);
          if (Mathf.Abs(sinAngle) > Mathf.Sin(2 * Mathf.Deg2Rad))
          {
            m_turret.Rotate(direction * Time.deltaTime * turretTrackingSpeed, 0, 0);
          }
          else
          {
            // Raise gun
            Vector3 targetAngles = m_gun.localRotation.eulerAngles;
            targetAngles.z = maxGunAngle;  // bone -- and gun -- axis is along x, so rotate about z
            Quaternion targetElevation = Quaternion.Euler(targetAngles);
            Vector3 currentGunVector = m_gun.localRotation * Vector3.right;
            Vector3 targetGunVector = targetElevation * Vector3.right;
            float angleToTarget = Mathf.Abs(Vector3.Angle(currentGunVector, targetGunVector));
            float time = angleToTarget / turretScanSpeed;
            m_gun.localRotation = Quaternion.Lerp(m_gun.localRotation, targetElevation, Time.deltaTime / time);
          }
        }
        break;
      case TurretState.TrackingEnd:
        // Lower gun then return to scanning
        if (now < m_t1)
        {
          m_gun.localRotation = Quaternion.Lerp(m_gunStartRotation, m_gunEndRotation, delta / (m_t1 - m_t0));
        }
        else
        {
          m_t1 = now + 1;
          m_state = TurretState.ScanningSleep;
        }
        break;
    }
  }

  private void Hide()
  {
    foreach (Renderer renderer in GetComponentsInChildren<Renderer>())
    {
      renderer.enabled = false;
    }
    foreach(Rigidbody rb in GetComponentsInChildren<Rigidbody>())
    {
      rb.isKinematic = true;
      rb.detectCollisions = false;
    }
    foreach (Collider collider in GetComponentsInChildren<Collider>())
    {
      collider.enabled = false;
    }
  }

  void OnCollisionEnter(Collision collision)
  {
    GameObject target = collision.collider.gameObject;
    if (target.CompareTag("Bullet"))
    {
      m_currentMission.OnEnemyHitByPlayer(this);
      m_audioSource.Stop();
      if (--lifePoints > 0)
      {
        m_audioSource.PlayOneShot(soundRicochet);
      }
      else
      {
        m_dead = true;
        Hide();
        if (soundExplosion.Length > 0)
        {
          m_audioSource.PlayOneShot(soundExplosion[Random.Range(0, soundExplosion.Length)]);
        }
        GameObject wreckage = Instantiate(wreckagePrefab, transform.parent) as GameObject;
        wreckage.transform.position = transform.position;
        wreckage.transform.rotation = transform.rotation;
        foreach (Rigidbody rb in wreckage.GetComponentsInChildren<Rigidbody>())
        {
          rb.AddExplosionForce(200, wreckage.transform.position, 0.1f, 0.1f);
        }
        ParticleEffectsManager.Instance.CreateExplosionBlastWave(transform.position, Vector3.up);
        /*
         * TODO: try using bindpose
        foreach (Transform original in transform)
        {
          foreach (Transform wrecked in wreckage.transform)
          {
            if (original.name == wrecked.name)
            {
              wrecked.position = original.position;
              wrecked.rotation = original.rotation;
            }
          }
        }
        */
      }
    }
  }
}