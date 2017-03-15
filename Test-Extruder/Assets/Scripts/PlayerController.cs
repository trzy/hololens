using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class PlayerController: MonoBehaviour
{
  private Rigidbody m_rb;
  private int m_animRunning = Animator.StringToHash("Running");
  private int m_animJump = Animator.StringToHash("Jump");
  private int m_animOnGround = Animator.StringToHash("OnGround");
  private int m_jumpFrames = 0;
  private Animator m_anim;

  private void OnCollisionEnter(Collision other)
  {
    for (int i = 0; i < other.contacts.Length; i++)
    {
      Debug.Log("contact point: " + other.contacts[i].point + " (" + transform.position + ")");
    }
    m_anim.SetBool(m_animOnGround, true);
    m_jumpFrames = 0;
  }

  private void OnCollisionExit(Collision collision)
  {
    m_anim.SetBool(m_animOnGround, false);
  }

  private void FixedUpdate()
  {
    float hor = Input.GetAxis("Horizontal");
    float ver = Input.GetAxis("Vertical");
    bool jump = Input.GetKey(KeyCode.LeftControl);
    hor = Mathf.Abs(hor) > 0.25f ? hor : 0;
    ver = Mathf.Abs(ver) > 0.25f ? ver : 0;
    bool running = hor != 0 || ver != 0;
    m_anim.SetBool(m_animRunning, running);
    if (running)
    {
      m_rb.MovePosition(transform.position + 5 * new Vector3(hor, 0, ver) * Time.deltaTime);
      Quaternion targetRotation = Quaternion.LookRotation(new Vector3(hor, 0, ver), Vector3.up);
      m_rb.MoveRotation(Quaternion.RotateTowards(transform.rotation, targetRotation, 10));
    }
    if (m_jumpFrames < 6 && jump)
    {
      if (m_jumpFrames == 0)
        m_anim.SetTrigger(m_animJump);
      m_rb.AddForce(Vector3.up, ForceMode.VelocityChange);
      ++m_jumpFrames;
    }
  }

  private void Awake()
  {
    m_rb = GetComponent<Rigidbody>();
    m_anim = GetComponent<Animator>();
  }
}
