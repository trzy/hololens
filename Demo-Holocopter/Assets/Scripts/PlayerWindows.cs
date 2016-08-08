using UnityEngine;
using System.Collections;
using System.Collections.Generic;

public class PlayerWindows : MonoBehaviour
{
  public Helicopter g_helicopter;
  public GameObject g_waypoints;
  public GameObject g_waypoint_prefab;
  private Vector3 m_euler;
  private List<GameObject> m_waypoints = new List<GameObject>();

	void Start()
  {
    // Look directly toward helicopter
    transform.LookAt(transform.position + new Vector3(0, 0, 1));
	}

	void Update()
  {
    // Rotate by maintaining Euler angles relative to world
    if (Input.GetKey("left"))
      m_euler.y -= 30 * Time.deltaTime;
    if (Input.GetKey("right"))
      m_euler.y += 30 * Time.deltaTime;
    if (Input.GetKey("up"))
      m_euler.x -= 30 * Time.deltaTime;
    if (Input.GetKey("down"))
      m_euler.x += 30 * Time.deltaTime;
    transform.rotation = Quaternion.Euler(m_euler);

    // Motion relative to XZ plane
    float move_speed = 30.0F * Time.deltaTime;
    Vector3 forward = transform.forward;
    forward.y = 0.0F;
    forward.Normalize();  // even if we're looking up or down, will continue to move in XZ
    if (Input.GetKey("w"))
      transform.Translate(forward * move_speed, Space.World);
    if (Input.GetKey("s"))
      transform.Translate(-forward * move_speed, Space.World);
    if (Input.GetKey("a"))
      transform.Translate(-transform.right * move_speed, Space.World);
    if (Input.GetKey("d"))
      transform.Translate(transform.right * move_speed, Space.World);

    // Vertical motion
    if (Input.GetKey(KeyCode.KeypadMinus))  // up
      transform.Translate(new Vector3(0.0F, 1.0F, 0.0F) * move_speed, Space.World);
    if (Input.GetKey(KeyCode.KeypadPlus))   // down
      transform.Translate(new Vector3(0.0F, -1.0F, 0.0F) * move_speed, Space.World);

    // Set waypoint
    if (Input.GetKeyDown(KeyCode.P))
    {
      GameObject waypoint = Instantiate(g_waypoint_prefab, transform.position + transform.forward * 5, Quaternion.identity) as GameObject;
      m_waypoints.Add(waypoint);
      //waypoint.transform.parent = g_waypoints.transform;
    }
    // Helicopter
    if (Input.GetKey(KeyCode.G))
      g_helicopter.TraverseWaypoints(m_waypoints);
  }
}
