using UnityEngine;

public class AutoAim: MonoBehaviour
{
  [Tooltip("Muzzle point of gun.")]
  public Transform muzzle;

  [Tooltip("Targeting reticle object.")]
  public GameObject targetingReticle;

  [Tooltip("Default distance of reticle from muzzle when not locked onto a target.")]
  public float defaultReticleDistance = 1;

  private void Update()
  {
    targetingReticle.transform.position = muzzle.position + muzzle.forward * defaultReticleDistance;
  }
}
