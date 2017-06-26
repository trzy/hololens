/*
 * Attach this script to the main camera to walk around rooms when running in
 * the Unity editor. The controls are:
 * 
 * Up Arrow     = Look up
 * Down Arrow   = Look down
 * Left Arrow   = Turn left
 * Right Arrow  = Turn right
 * W            = Move forward
 * S            = Move backward
 * A            = Move left
 * D            = Move right
 * O            = Reset view orientation
 * / (Keypad)   = Tilt head left
 * * (Keypad)   = Tilt head right
 * - (Keypad)   = Move up
 * + (Keypad)   = Move down
 */

using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class EditorMotionControls: MonoBehaviour
{
#if UNITY_EDITOR
  private Vector3 m_euler = new Vector3();

  private void Update()
  {
    // Rotate by maintaining Euler angles relative to world
    if (Input.GetKey("left"))
    {
      m_euler.y -= 30 * Time.deltaTime;
    }
    if (Input.GetKey("right"))
    {
      m_euler.y += 30 * Time.deltaTime;
    }
    if (Input.GetKey("up"))
    {
      m_euler.x -= 30 * Time.deltaTime;
    }
    if (Input.GetKey("down"))
    {
      m_euler.x += 30 * Time.deltaTime;
    }
    if (Input.GetKey("o"))
    {
      m_euler.x = 0;
      m_euler.z = 0;
    }
    if (Input.GetKey(KeyCode.KeypadDivide))
    {
      m_euler.z -= 30 * Time.deltaTime;
    }
    if (Input.GetKey(KeyCode.KeypadMultiply))
    {
      m_euler.z += 30 * Time.deltaTime;
    }
    transform.rotation = Quaternion.Euler(m_euler);

    // Motion relative to XZ plane
    float move_speed = 1.0F * Time.deltaTime;
    Vector3 forward = transform.forward;
    forward.y = 0.0F;
    forward.Normalize();  // even if we're looking up or down, will continue to move in XZ
    if (Input.GetKey("w"))
    {
      transform.Translate(forward * move_speed, Space.World);
    }
    if (Input.GetKey("s"))
    {
      transform.Translate(-forward * move_speed, Space.World);
    }
    if (Input.GetKey("a"))
    {
      transform.Translate(-transform.right * move_speed, Space.World);
    }
    if (Input.GetKey("d"))
    {
      transform.Translate(transform.right * move_speed, Space.World);
    }

    // Vertical motion
    if (Input.GetKey(KeyCode.KeypadMinus))  // up
    {
      transform.Translate(new Vector3(0.0F, 1.0F, 0.0F) * move_speed, Space.World);
    }
    if (Input.GetKey(KeyCode.KeypadPlus))   // down
    {
      transform.Translate(new Vector3(0.0F, -1.0F, 0.0F) * move_speed, Space.World);
    }
  }
#endif
}
