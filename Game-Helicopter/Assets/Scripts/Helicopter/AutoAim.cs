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

  public float maxYawAngle = 60;
  public float minPitchAngle = -10;
  public float maxPitchAngle = 65;

  private Transform m_target = null;

  private float ComputeYawAngleToTarget()
  {
    // gunYaw's parent object is used to determine its resting pose (angle=0).
    // Target is rotated into parent's local coordinate system and the angle to
    // the target in the azimuthal (xz) plane is the required yaw.
    Vector3 localPoint = gunYaw.parent.InverseTransformPoint(m_target.position);
    Vector3 toTarget = MathHelpers.Azimuthal(localPoint - gunYaw.parent.position);
    float direction = Mathf.Sign(MathHelpers.CrossY(Vector3.forward, toTarget));
    return direction * Vector3.Angle(Vector3.forward, toTarget);
  }

  private float ComputePitchAngleToTarget()
  {
    // Analogous to yaw, but using gunPitch and the vertical (yz) plane
    Vector3 localPoint = gunPitch.parent.InverseTransformPoint(m_target.position);
    Vector3 toTarget = MathHelpers.Vertical(localPoint - gunPitch.parent.position);
    float direction = Mathf.Sign(MathHelpers.CrossX(Vector3.forward, toTarget));
    return direction * Vector3.Angle(Vector3.forward, toTarget);
  }

  private bool TryPointAtTarget()
  {
    bool succeeded = false;
    float newYawAngle = 0;
    float newPitchAngle = 0;

    float yawAngle = ComputeYawAngleToTarget();
    if (Mathf.Abs(yawAngle) <= maxYawAngle)
    {
      float pitchAngle = ComputePitchAngleToTarget();
      if (pitchAngle <= maxPitchAngle && pitchAngle >= minPitchAngle)
      {
        newYawAngle = yawAngle;
        newPitchAngle = pitchAngle;
        succeeded = true;
      }
    }

    if (gunYaw == gunPitch)
      gunYaw.localRotation = Quaternion.Euler(newPitchAngle, newYawAngle, 0);
    else
    {
      gunYaw.localRotation = Quaternion.Euler(0, newYawAngle, 0);
      gunPitch.localRotation = Quaternion.Euler(newPitchAngle, 0, 0);
    }

    return succeeded;
  }

  private void Update()
  {
    Vector3 muzzlePos = muzzle.position;

    // Check to see if current target is within gun aiming limits and, if so,
    // aim the gun at it
    if (m_target != null && TryPointAtTarget())
    {
      targetingReticle.transform.position = muzzlePos + muzzle.forward * defaultReticleDistance;
      return;
    }

    // Select a new target
    MathHelpers.Cone aimingCone = new MathHelpers.Cone(muzzlePos, muzzle.forward, 1.5f, 45);
    GameObject[] enemies = GameObject.FindGameObjectsWithTag("Enemy");
    float minDistance = float.PositiveInfinity;
    m_target = null;
    foreach (GameObject enemy in enemies)
    {
      Vector3 enemyPos = enemy.transform.position;
      float distanceFromAxis;
      if (aimingCone.Contains(enemyPos) && (distanceFromAxis = aimingCone.DistanceFromAxis(enemyPos)) < minDistance)
      {
        minDistance = distanceFromAxis;
        m_target = enemy.transform;
      }
    }

    // Point at target
    if (m_target != null && !TryPointAtTarget())
      m_target = null;
    targetingReticle.transform.position = muzzlePos + muzzle.forward * defaultReticleDistance;
  }

  private void Awake()
  {
  }
}
