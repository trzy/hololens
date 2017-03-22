using UnityEngine;

public class PlatformPowerup: MonoBehaviour
{
  public Material selectionMaterial;
  public Material platformMaterial;
  public GeoMaker.PlatformType platformType = GeoMaker.PlatformType.Raised;
  public float height = 0.3f;
  public AudioClip sound;
}
