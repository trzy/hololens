using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class ForceUniformScale: MonoBehaviour
{
  private Vector3 m_originalScale;

  private void LateUpdate()
  {
    transform.localScale = m_originalScale;
  }

  private void Start()
  {
    m_originalScale = transform.localScale;
  }
}