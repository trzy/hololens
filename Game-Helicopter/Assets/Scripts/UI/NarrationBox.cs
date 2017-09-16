using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class NarrationBox: MonoBehaviour
{
  public TextMesh textMeshObject;

  private IEnumerator Wait(float seconds, System.Action OnTimeReached)
  {
    float start = Time.time;
    yield return new WaitForSeconds(seconds);
    OnTimeReached();
  }

  public void SetLine(string text, AudioClip clip, System.Action OnFinished)
  {
    textMeshObject.text = text;
    StartCoroutine(Wait(5, OnFinished));
  }
}
