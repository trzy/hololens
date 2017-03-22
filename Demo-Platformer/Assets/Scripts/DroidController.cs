using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class DroidController: MonoBehaviour
{
  public float playerActivationDistance = 100f;
  public GameObject powerupPrefab;
  public GameObject wreckagePrefab;
  public Vector2[] path;
  private int m_pathIdx = -1;
  private GameObject m_player = null;
  private Vector3 m_target;
  private Animator m_anim;
  private Rigidbody m_rb;
  private bool m_active = false;

  public void SetPlayer(GameObject player)
  {
    m_player = player;
  }

  private void OnCollisionEnter(Collision collision)
  {
    if (collision.collider.gameObject.CompareTag("Bullet"))
    {
      FXManager.Instance.EmitTankExplosion(transform.position);
      FXManager.Instance.PlayExplosionSound();
      SpawnWreckage();
      Instantiate(powerupPrefab, transform.position + transform.up * 0.25f, Quaternion.identity);
      Hide();
    }
    Debug.Log("collided with " + collision.collider.gameObject.tag);
  }

  private void NextTarget()
  {
    if (0 == path.Length)
      return;
    m_pathIdx = (m_pathIdx + 1) % path.Length;
    m_target = transform.position + new Vector3(path[m_pathIdx].x, 0, path[m_pathIdx].y);
  }

  private void Hide()
  {
    foreach (Renderer renderer in GetComponentsInChildren<Renderer>())
    {
      renderer.enabled = false;
    }
    foreach (Rigidbody rb in GetComponentsInChildren<Rigidbody>())
    {
      rb.isKinematic = true;
      rb.detectCollisions = false;
    }
    foreach (Collider collider in GetComponentsInChildren<Collider>())
    {
      collider.enabled = false;
    }
  }

  private void SpawnWreckage()
  {
    GameObject wreckage = Instantiate(wreckagePrefab, transform.parent) as GameObject;
    wreckage.transform.position = transform.position;
    wreckage.transform.rotation = transform.rotation;
    foreach (Rigidbody rb in wreckage.GetComponentsInChildren<Rigidbody>())
    {
      rb.AddExplosionForce(100, wreckage.transform.position, 1f, 0.5f);
    }
  }

  private void FixedUpdate()
  {
    if (m_player == null)
      return;
    if (!m_active)
    {
      if (Vector3.Distance(m_player.transform.position, transform.position) < 1f)
        m_anim.SetBool("active", true);
      if (m_anim.GetCurrentAnimatorStateInfo(0).IsName("Active"))
      {
        m_active = true;
        NextTarget();
      }
      else
        return;
    }
    if (0 == path.Length)
      return;
    Vector3 toTarget = m_target - transform.position;
    float distanceToTarget = Vector3.Magnitude(toTarget);
    if (distanceToTarget <= .05f)
    {
      NextTarget();
      //m_rb.AddForce(Vector3.zero, ForceMode.VelocityChange);
    }
    else
    {
      m_rb.MovePosition(transform.position + 0.25f * Vector3.Normalize(toTarget) * Time.deltaTime);
      m_anim.SetFloat("horizontal", Vector3.Normalize(toTarget).x);
      m_anim.SetFloat("vertical", Vector3.Normalize(toTarget).y);
      //m_rb.AddForce(.1f * Vector3.Normalize(toTarget), ForceMode.VelocityChange);
    }
    
  }

  private void Start()
  {
    m_anim = GetComponent<Animator>();
    m_rb = GetComponent<Rigidbody>();
    NextTarget();
  }
}
