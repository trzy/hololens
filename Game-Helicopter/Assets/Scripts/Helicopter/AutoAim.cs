using UnityEngine;

public class AutoAim: MonoBehaviour
{
  [Tooltip("Turret component that swivels left/right (yaw). Parent's forward direction is taken to be resting pose.")]
  public Transform gunYaw;

  [Tooltip("Turret component that swivels up/down (pitch). Initial orientation on Awake() is taken as resting pose.")]
  public Transform gunPitch;

  [Tooltip("Muzzle point of gun.")]
  public Transform muzzle;

  [Tooltip("Targeting reticle object.")]
  public GameObject targetingReticle;

  [Tooltip("Default distance of reticle from muzzle when not locked onto a target.")]
  public float defaultReticleDistance = 1;

  [Tooltip("Angular velocity (degrees/sec) of gun (used when locking onto a target and returning to rest pose after losing target).")]
  public float angularVelocity = 180;

  public float maxYawAngle = 60;
  public float minPitchAngle = -10;
  public float maxPitchAngle = 65;

  private Destructible m_target = null;
  private bool m_lockedOn = false;

  private float GetYawAngle()
  {
    float angle = gunYaw.localRotation.eulerAngles.y;
    return angle > 180 ? angle - 360 : angle;
  }

  private float GetPitchAngle()
  {
    float angle = gunPitch.localRotation.eulerAngles.x;
    return angle > 180 ? angle - 360 : angle;
  }

  private float ComputeYawAngleToTarget(Vector3 targetPos)
  {
    // gunYaw's parent object is used to determine its resting pose (angle=0).
    // Target is rotated into parent's local coordinate system and the angle to
    // the target in the azimuthal (xz) plane is the required yaw.
    Vector3 localPoint = gunYaw.parent.InverseTransformPoint(targetPos);
    Vector3 toTarget = MathHelpers.Azimuthal(localPoint - gunYaw.parent.position);
    float direction = Mathf.Sign(MathHelpers.CrossY(Vector3.forward, toTarget));
    return direction * Vector3.Angle(Vector3.forward, toTarget);
  }

  private float ComputePitchAngleToTarget(Vector3 targetPos)
  {
    // Analogous to yaw, but using gunPitch and the vertical (yz) plane
    Vector3 localPoint = gunPitch.parent.InverseTransformPoint(targetPos);
    Vector3 toTarget = MathHelpers.Vertical(localPoint - gunPitch.parent.position);
    float direction = Mathf.Sign(MathHelpers.CrossX(Vector3.forward, toTarget));
    return direction * Vector3.Angle(Vector3.forward, toTarget);
  }

  private bool TryComputeAnglesToTarget(out float outYawAngle, out float outPitchAngle, Vector3 targetPos)
  {
    outYawAngle = 0;
    outPitchAngle = 0;
    bool succeeded = false;

    float yawAngle = ComputeYawAngleToTarget(targetPos);
    if (Mathf.Abs(yawAngle) <= maxYawAngle)
    {
      float pitchAngle = ComputePitchAngleToTarget(targetPos);
      if (pitchAngle <= maxPitchAngle && pitchAngle >= minPitchAngle)
      {
        outYawAngle = yawAngle;
        outPitchAngle = pitchAngle;
        succeeded = true;
      }
    }

    return succeeded;
  }

  private void Update()
  {
    Vector3 muzzlePos = muzzle.position;
    float yawAngle = GetYawAngle();
    float pitchAngle = GetPitchAngle();
    float targetYawAngle = 0;
    float targetPitchAngle = 0;

    if (m_target == null || !TryComputeAnglesToTarget(out targetYawAngle, out targetPitchAngle, m_target.GetAimPoint()))
    {
      // No target or lost target -- try to acquire a new one
      m_lockedOn = false;
      m_target = null;
      Destructible[] enemies = GameObject.FindObjectsOfType<Destructible>();
      MathHelpers.Cone aimingCone = new MathHelpers.Cone(muzzlePos, muzzle.forward, 1.5f, 45);
      float minDistance = float.PositiveInfinity;

      foreach (Destructible enemy in enemies)
      {
        if (!enemy.gameObject.CompareTag("Enemy") || !enemy.Alive)
          continue;
        Vector3 enemyPos = enemy.GetAimPoint();
        float distanceFromAxis;
        if (aimingCone.Contains(enemyPos) && (distanceFromAxis = aimingCone.DistanceFromAxis(enemyPos)) < minDistance)
        {
          minDistance = distanceFromAxis;
          m_target = enemy;
        }
      }

      // Try to compute aiming angles for new target
      if (m_target != null && !TryComputeAnglesToTarget(out targetYawAngle, out targetPitchAngle, m_target.GetAimPoint()))
        m_target = null;
    }

    // If we are not locked on, take a step toward new angles
    if (!m_lockedOn)
    {
      float angleStep = angularVelocity * Time.deltaTime;
      float deltaYawAngle = targetYawAngle - yawAngle;
      float deltaPitchAngle = targetPitchAngle - pitchAngle;
      yawAngle += deltaYawAngle >= 0 ? Mathf.Min(angleStep, deltaYawAngle) : Mathf.Max(-angleStep, deltaYawAngle);
      pitchAngle += deltaPitchAngle >= 0 ? Mathf.Min(angleStep, deltaPitchAngle) : Mathf.Max(-angleStep, deltaPitchAngle);
      if (m_target != null && Mathf.Abs(targetPitchAngle - pitchAngle) < 1e-1f && Mathf.Abs(targetYawAngle - yawAngle) < 1e-1f)
        m_lockedOn = true;
    }
    else
    {
      // When locked on, instaneously track target
      yawAngle = targetYawAngle;
      pitchAngle = targetPitchAngle;
    }

    // Update gun orientation
    if (gunYaw == gunPitch)
      gunYaw.localRotation = Quaternion.Euler(pitchAngle, yawAngle, 0);
    else
    {
      gunYaw.localRotation = Quaternion.Euler(0, yawAngle, 0);
      gunPitch.localRotation = Quaternion.Euler(pitchAngle, 0, 0);
    }

    targetingReticle.transform.position = muzzlePos + muzzle.forward * defaultReticleDistance;
  }

  private void Awake()
  {
  }
}
