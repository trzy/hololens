/*
 * TODO:
 * 
 * Use my own technique -- rotate object into camera space and then project 8
 * bounding box points onto some plane and find a radius that encompasses them.
 * Perhaps only do this once when first setting up reticle and then simply keep
 * using that same sizing under the assumption that the reticle will not be there
 * for too long.
 * 
 */

using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class BoundsFinder: MonoBehaviour
{
  [Tooltip("Whether to render bounds")]
  public bool renderBounds = false;

  [Tooltip("Material to render bounds")]
  public Material boundsMaterial = null;

  private float m_boundingRadius = 0;

  private Vector3 ComponentMult(Vector3 a, Vector3 b)
  {
    return new Vector3(a.x * b.x, a.y * b.y, a.z * b.z);
  }

  private void Awake()
  {
    Vector3[] directions =
    {
      new Vector3(1, 1, 1),
      new Vector3(1, 1, -1),
      new Vector3(1, -1, 1),
      new Vector3(1, -1, -1),
      new Vector3(-1, 1, 1),
      new Vector3(-1, 1, -1),
      new Vector3(-1, -1, 1),
      new Vector3(-1, -1, -1)
    };

    foreach (BoxCollider collider in GetComponentsInChildren<BoxCollider>())
    {
      // For proper scale, transform to world coordinates but subtract parent
      // object position to center the object at origin
      float[] distances = new float[directions.Length];
      for (int i = 0; i < directions.Length; i++)
      {
        Vector3 point = collider.transform.TransformPoint(collider.center + ComponentMult(directions[i], 0.5f * collider.size)) - transform.position;
        distances[i] = Vector3.Magnitude(point);
      }
      System.Array.Sort(distances, (float a, float b) => (int)Mathf.Sign(b - a)); // sort descending
      m_boundingRadius = Mathf.Max(m_boundingRadius, distances[0]);
      Debug.Log(collider.gameObject.name + " collider radius=" + m_boundingRadius);
    }
  }

  private void Start()
  {
    if (renderBounds)
    {
      GameObject sphere = GameObject.CreatePrimitive(PrimitiveType.Sphere);
      sphere.transform.parent = null;// gameObject.transform;
      sphere.transform.localScale = 2 * new Vector3(m_boundingRadius, m_boundingRadius, m_boundingRadius);
      sphere.transform.position = transform.position;
      sphere.GetComponent<Renderer>().material = boundsMaterial;
      sphere.GetComponent<Renderer>().material.color = new Color(1, 0, 0, 0.5f); // equivalent to SetColor("_Color", color)
      sphere.GetComponent<SphereCollider>().enabled = false;
      sphere.SetActive(true);
    }
  }
}
