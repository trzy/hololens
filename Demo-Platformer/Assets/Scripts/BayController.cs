using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class BayController: MonoBehaviour
{
  private bool m_visible = true;
  private GameObject m_player = null;

  public void SetPlayer(GameObject player)
  {
    m_player = player;
  }

  private void Show(bool visible)
  {
    foreach (Renderer renderer in GetComponentsInChildren<Renderer>())
    {
      renderer.enabled = visible;
    }
    m_visible = visible;
  }

  private void FixedUpdate()
  {
    if (m_player == null)
      return;
    if (Vector3.Magnitude(m_player.transform.position - transform.position) < 1f)
    {
      if (!m_visible)
      {
        Show(true);
        FXManager.Instance.CreateExplosionBlastWave(transform.position, transform.forward);
        FXManager.Instance.EmitTankExplosion(transform.position);
        FXManager.Instance.EmitImpact(transform.position + transform.right * 0.1f);
        FXManager.Instance.EmitImpact(transform.position + transform.right * -0.1f);
        FXManager.Instance.PlayBigExplosionSound();
      }
    }
  }

  private void Start()
  {
    // Hack to align properly to wall
    Vector3 newForward = transform.up;
    Vector3 newUp = Vector3.up;
    transform.rotation = Quaternion.LookRotation(newForward, newUp);

    // Slight offset for deformation to work better
    transform.position += 0.01f * transform.forward;

    // Deform spatial mesh to make this visible
    SurfacePlaneDeformationManager.Instance.Embed(gameObject, transform.position);

    // Hide initially
    Show(false);
  }
}
