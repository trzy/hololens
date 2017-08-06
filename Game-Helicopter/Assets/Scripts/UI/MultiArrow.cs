//TODO: ensure SharedMaterialHelper runs before all child OnEnable
//TODO: add a little bounce to the final arrow to give it a more kinetic feel
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class MultiArrow: MonoBehaviour
{
  public float timeNextArrowOn = 0.035f;
  public float timeAllArrowsOn = 0;
  public float timeNextArrowOff = .035f;
  public float timeFirstArrowOn = .3f;
  public float timeToNextSequence = 1;
  public GameObject[] arrows;

  private IEnumerator m_coroutine = null;

  private IEnumerator Animation()
  {
    while (true)
    {
      if (arrows.Length > 0)
      {
        // Turn arrows on from last to first
        for (int i = arrows.Length - 1; i >= 0; i--)
        {
          arrows[i].SetActive(true);
          yield return new WaitForSeconds(timeNextArrowOn);
        }

        // Linger with all arrows on
        yield return new WaitForSeconds(timeAllArrowsOn);

        // Turn arrows off from last to second (leaving first on)
        for (int i = arrows.Length - 1; i >= 1; i--)
        {
          arrows[i].SetActive(false);
          yield return new WaitForSeconds(timeNextArrowOff);
        }

        // Linger with first arrow on then turn off
        yield return new WaitForSeconds(timeFirstArrowOn);
        arrows[0].SetActive(false);
      }

      // Wait for next cycle
      yield return new WaitForSeconds(timeToNextSequence);
    }
  }

  private void OnEnable()
  {
    m_coroutine = Animation();
    StartCoroutine(m_coroutine);
  }

  private void OnDisable()
  {
    if (m_coroutine != null)
      StopCoroutine(m_coroutine);

    // Reset all children to disabled state
    foreach (GameObject arrow in arrows)
    {
      arrow.SetActive(false);
    }
  }

  private void Awake()
  {
    // Parent all arrows to us because if arrows are specified hierarchically,
    // the enable/disable logic will not work (a disabled parent will disable
    // all children regardless of their individual state)
    foreach (GameObject arrow in arrows)
    {
      arrow.transform.parent = transform;
      arrow.SetActive(false);
    }
  }
}
