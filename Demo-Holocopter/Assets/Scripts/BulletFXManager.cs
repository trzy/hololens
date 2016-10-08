using UnityEngine;
using System.Collections;

public class BulletFXManager: MonoBehaviour
{
  public GroundFlash m_ground_flash_prefab;
  public GroundBlast m_ground_blast_prefab;

  public void CreateSurfaceHitFX(Vector3 hit_point, Vector3 hit_normal)
  {
    GroundFlash flash = Instantiate(m_ground_flash_prefab, hit_point + hit_normal * 0.01f, Quaternion.LookRotation(hit_normal)) as GroundFlash;
    GroundBlast blast = Instantiate(m_ground_blast_prefab, hit_point + hit_normal * 0.02f, Quaternion.LookRotation(hit_normal)) as GroundBlast;
    blast.transform.localScale = new Vector3(0.03f, 0.03f, 0.03f);  // scale down to real world size
  }

  void Start()
  {
  }

  void Update()
  {
/*
#if UNITY_EDITOR
    if (Input.GetKeyDown(KeyCode.Return))
      CreateGroundBlastEffect(transform.position, transform.up);
#endif
*/
  }
}
