/*
 * How AddForce() works:
 * ---------------------
 * 
 * - Add force modifies the velocity during the physics update. The force mode
 *   determines *how* the velocity changes each time step.
 *   
 * - ForceMode.VelocityChange interprets the vector as a velocity directly.
 *   This is what the Unity documentation means when it says that "the unit of
 *   the force parameter is applied to the rigidbody as distance/time."
 *   AddForce(v, ForceMode.VelocityChange) will add v to the velocity for the
 *   current physics time step. If this is called each time step, the body's
 *   velocity will increase by v each time. Below, the magnitude of v is 0.15.
 *   Note that the velocity of the body increases by 0.15 each physics update
 *   and stays constant throughout the graphics updates.
 *
 *    Fixed Update: AddForce (0.13, 0.00, -0.07)
 *          Update: velocity magnitude=0.15
 *    Fixed Update: AddForce (0.13, 0.00, -0.07)
 *          Update: velocity magnitude=0.30
 *          Update: velocity magnitude=0.30
 *          Update: velocity magnitude=0.30
 *    Fixed Update: AddForce (0.13, 0.00, -0.07)
 *          Update: velocity magnitude=0.45
 *
 * - ForceMode.Acceleration interprets the vector as distance/time^2, which are
 *   the units of acceleration. To compute the velocity to be added in the
 *   current time step, acceleration must of course be multiplied by the time
 *   step, resulting in units of distance/time (a velocity, just as in 
 *   ForceMode.VelocityChange). Below, the time step is 1/50 = .02. The vector
 *   v is again 0.15, so each physics update that AddForce() is called, 
 *   0.15 * .02 = .003 is added to the velocity.
 *
 *    Fixed Update: AddForce (0.1482, 0.0000, -0.0231)
 *    Fixed Update: AddForce (0.1482, 0.0000, -0.0231)
 *    Fixed Update: AddForce (0.1482, 0.0000, -0.0231)
 *          Update: velocity magnitude=0.0090 <-- 3 * .003 = .009
 *          Update: velocity magnitude=0.0090
 *    Fixed Update: AddForce (0.1482, 0.0000, -0.0231)
 *          Update: velocity magnitude=0.0120
 *          Update: velocity magnitude=0.0120
 *          Update: velocity magnitude=0.0120
 *
 * - ForceMode.Impulse is the same as ForceMode.VelocityChange except that the
 *   units include mass: mass*distance/time. Therefore, the velocity change for
 *   the physics update is found simply by dividing by the rigid body's mass.
 *   
 * - ForceMode.Force is analogous to ForceMode.Acceleration but multipled by
 *   mass. It has units of mass*distance/time^2 -- in other words, units of 
 *   force :) Calling AddForce() on each physics frame is equivalent to 
 *   permanently applying the force to the object.
 */

using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class RobotController: MonoBehaviour, IMagnetic
{
  [Tooltip("Left hand bone.")]
  public Transform leftHand;

  [Tooltip("Right hand bone.")]
  public Transform rightHand;

  [Tooltip("Walking speed (m/s).")]
  public float walkSpeed = 0.25f;

  [Tooltip("Turning speed (deg/s)")]
  public float turnSpeed = 360 / 5;

  [Tooltip("Number of seconds of continuous contact with a single collider (OnCollisionStay) required after a fall before waking up again.")]
  public float wakeTimePostCollisionStay = 3.25f;

  [Tooltip("Number of seconds after we started to fall to force a wake up, regardless of collision state.")]
  public float wakeTimeout = 10;

  [Tooltip ("Maximum directional error when heading toward target (degrees).")]
  public float maxHeadingError = 1;

  private enum State
  {
    None,
    Idle,
    WalkToTarget,
    CarryToTarget,
    StuckToMagnet,
    FreeFall,
    StandUp
  }

  private struct Target
  {
    public delegate Vector3 PositionCallback();

    public Vector3 position
    {
      get
      {
        if (m_positionCallback != null)
          return m_positionCallback();
        if (m_transform != null)
          return m_transform.position;
        return m_position;
      }
    }

    public Target(PositionCallback cb)
    {
      m_positionCallback = cb;
      m_transform = null;
      m_position = Vector3.zero;
    }

    public Target(Transform t)
    {
      m_positionCallback = null;
      m_transform = t;
      m_position = Vector3.zero;
    }

    public Target(Vector3 p)
    {
      m_positionCallback = null;
      m_transform = null;
      m_position = p;
    }

    private PositionCallback m_positionCallback;
    private Transform m_transform;
    private Vector3 m_position;
  }

  private Rigidbody m_rb;
  private Animator m_anim;
  private int m_animIdle = Animator.StringToHash("Idle");
  private int m_animWalking = Animator.StringToHash("Walking");
  private int m_animFalling = Animator.StringToHash("Falling");
  private int m_animStandUp = Animator.StringToHash("StandUp");
  private int m_animCarry = Animator.StringToHash("Carry");
  private State m_state = State.None;
  private bool m_waitForNextAnimationState = false;
  private Target m_target = new Target(Vector3.zero);
  private bool m_collisionStay = false;
  private float m_collisionStayTime;
  private float m_stateBeginTime;
  private Quaternion m_startingPose = Quaternion.identity;
  private Quaternion m_targetPose = Quaternion.identity;
  private GameObject m_bomb = null;

  public void OnMagnet(bool on)
  {
    Debug.Log("Magnet: " + on);
    StuckToMagnetState();
  }

  private void OnCollisionEnter(Collision collision)
  {
    GameObject other = collision.collider.gameObject;
    if (other.name == "WreckingBall" && m_state != State.StuckToMagnet && !m_rb.isKinematic)
    {
      if (m_waitForNextAnimationState)
        return; // guard against multiple transitions that leave us stuck
      Rigidbody ball = collision.rigidbody;
      m_rb.AddForce(ball.velocity.magnitude * Vector3.up, ForceMode.VelocityChange);
      FreeFallState();
    }
    m_collisionStay = false;
  }

  private void OnCollisionStay(Collision collision)
  {
    if (!m_collisionStay)
    {
      m_collisionStay = true;
      m_collisionStayTime = Time.time;
    }
  }

  private void OnCollisionExit(Collision collision)
  {
    m_collisionStay = false;
  }

  private Vector3 Ground(Vector3 v)
  {
    return new Vector3(v.x, 0, v.z);
  }

  private float HeadingError(Vector3 targetPosition)
  {
    Vector3 targetDir = Ground(targetPosition - transform.position);
    Vector3 currentDir = Ground(transform.forward);
    // Minus sign because error defined as how much we have overshot and need
    // to subtract, assuming positive rotation is clockwise.
    return -Mathf.Sign(MathHelpers.CrossY(currentDir, targetDir)) * Vector3.Angle(currentDir, targetDir);
  }

  private void FixedUpdate()
  {
    // When we change states, we detect a change in animation state by waiting
    // for a callback to lower this flag. This is to prevent us from trying to
    // change states multiple times while an animation is still playing, which
    // can make subsequent state transitions fail.
    if (m_waitForNextAnimationState)
      return;

    if (m_state == State.FreeFall)
    {
      // Get up after we've been colliding continuously for a given time or
      // seemingly stuck in this state for too long
      float timeColliding = Time.time - m_collisionStayTime;
      float timeFreeFalling = Time.time - m_stateBeginTime;
      if ((m_collisionStay && timeColliding > wakeTimePostCollisionStay) || timeFreeFalling > wakeTimeout)
        StandUpState();
    }
    else if (m_state == State.Idle || m_state == State.WalkToTarget || m_state == State.CarryToTarget)
    {
      float closeEnough = .01f;

      // Track the target
      float headingError = HeadingError(m_target.position);
      float targetDirection = -Mathf.Sign(headingError);
      float targetTurnSpeed = targetDirection * (Mathf.Abs(headingError) > maxHeadingError ? (Mathf.Deg2Rad * turnSpeed) : 0);
      float currentTurnSpeed = m_rb.angularVelocity.y;
      float angularError = targetTurnSpeed - currentTurnSpeed;
      m_rb.AddTorque(angularError * Vector3.up, ForceMode.VelocityChange);

      // Maintain velocity
      float distance = Ground(m_target.position - transform.position).magnitude;
      Vector3 currentVelocity = Ground(m_rb.velocity);
      Vector3 targetVelocity = Ground(transform.forward) * ((distance > closeEnough) ? walkSpeed : 0);
      Vector3 error = targetVelocity - currentVelocity;
      m_rb.AddForce(error, ForceMode.VelocityChange);

      // Update state if needed
      if (m_state != State.CarryToTarget)
      {
        if (distance > closeEnough)
          WalkToTargetState(m_target);
        else
          IdleState();
      }
      else
      {
        if (distance <= closeEnough)
          WalkToTargetState(new Target(Ground(Camera.main.transform.position + Camera.main.transform.forward * 2f)));
      }
    }
  }

  private void Update()
  {
    if (m_state == State.CarryToTarget && m_bomb != null)
    {
      m_bomb.transform.position = 0.5f * (leftHand.position + rightHand.position);
    }
    else if (m_state == State.StandUp)
    {
      float t = (Time.time - m_stateBeginTime) / 0.25f;
      if (t < 1)
      {
        // Turn over
        transform.rotation = Quaternion.Lerp(m_startingPose, m_targetPose, t);
      }
      else if (m_anim.GetBool(m_animStandUp) == false)
      {
        // Once we have finished flipping over, we can stand up. The stand up
        // animation is in the normal, standing orientation with y == up. At
        // this point, we are lying down, so to stand up, we want the axis
        // our feet are pointing into to become the forward direction.
        m_anim.SetBool(m_animStandUp, true);
        transform.rotation = Quaternion.LookRotation(-transform.up, Vector3.up);
      }
    }
  }

  // Called from StateMachineBehaviour script
  public void OnAnimationStateExit(Animator animator, AnimatorStateInfo stateInfo, int layerIndex)
  {
    m_waitForNextAnimationState = false;
    if (stateInfo.shortNameHash == m_animCarry)
    {
      // When we leave the carry state for any reason, bomb is detached
      BombKinematic(false);
      m_bomb.transform.parent = null;
    }
  }

  // Animation callback: fired when "get-up" animation is finished
  //TODO: this could actually be done using OnAnimationStateExit() and an
  // unconditional transition out of StandUp. We could detect IdleState there
  // and wait for OnStateEnter().
  public void OnStandUpComplete()
  {
    IdleState();
  }

  private void IdleState()
  {
    m_anim.SetBool(m_animIdle, true);
    m_anim.SetBool(m_animWalking, false);
    m_anim.SetBool(m_animFalling, false);
    m_anim.SetBool(m_animStandUp, false);
    m_anim.SetBool(m_animCarry, false);
    Kinematic(false);
    LockRotation(true);
    if (m_state != State.Idle)
    {
      m_state = State.Idle;
      m_waitForNextAnimationState = true;
    }
  }

  private void WalkToTargetState(Target target)
  {
    m_anim.SetBool(m_animIdle, false);
    m_anim.SetBool(m_animWalking, true);
    m_anim.SetBool(m_animFalling, false);
    m_anim.SetBool(m_animStandUp, false);
    m_anim.SetBool(m_animCarry, false);
    Kinematic(false);
    LockRotation(true);
    if (m_state != State.WalkToTarget)
    {
      m_state = State.WalkToTarget;
      m_waitForNextAnimationState = true;
    }
    m_target = target;
  }

  private void StuckToMagnetState()
  {
    m_anim.SetBool(m_animIdle, true);
    m_anim.SetBool(m_animWalking, false);
    m_anim.SetBool(m_animFalling, false);
    m_anim.SetBool(m_animStandUp, false);
    m_anim.SetBool(m_animCarry, false);
    Kinematic(false);
    LockRotation(false);
    if (m_state != State.StuckToMagnet)
    {
      m_state = State.StuckToMagnet;
      m_waitForNextAnimationState = true;
    }
  }

  private void FreeFallState()
  {
    m_anim.SetBool(m_animIdle, false);
    m_anim.SetBool(m_animWalking, false);
    m_anim.SetBool(m_animFalling, true);
    m_anim.SetBool(m_animStandUp, false);
    m_anim.SetBool(m_animCarry, false);
    Kinematic(false);
    LockRotation(false);
    if (m_state != State.FreeFall)
    {
      m_stateBeginTime = Time.time;
      m_state = State.FreeFall;
      m_waitForNextAnimationState = true;
    }
  }

  private void StandUpState()
  {
    m_anim.SetBool(m_animIdle, false);
    m_anim.SetBool(m_animWalking, false);
    m_anim.SetBool(m_animFalling, false);
    m_anim.SetBool(m_animStandUp, false); // not yet
    m_anim.SetBool(m_animCarry, false);
    Kinematic(true);
    LockRotation(true);
    if (m_state != State.StandUp)
    {
      m_stateBeginTime = Time.time;
      m_state = State.StandUp;
      m_waitForNextAnimationState = true;
    }

    // First, flip over so we are on our back
    m_startingPose = transform.rotation;
    m_targetPose = Quaternion.FromToRotation(transform.forward, Vector3.up) * transform.rotation;
  }

  private void CarryToTargetState(Target target)
  {
    m_anim.SetBool(m_animIdle, false);
    m_anim.SetBool(m_animWalking, false);
    m_anim.SetBool(m_animFalling, false);
    m_anim.SetBool(m_animStandUp, false);
    m_anim.SetBool(m_animCarry, true);
    Kinematic(false);
    LockRotation(true);
    if (m_state != State.CarryToTarget)
    {
      m_state = State.CarryToTarget;
      m_waitForNextAnimationState = true;
    }
    m_target = target;
    BombKinematic(true);
  }

  private void Kinematic(bool kinematic)
  {
    m_rb.isKinematic = kinematic;
  }

  private void LockRotation(bool lockRotation)
  {
    m_rb.freezeRotation = lockRotation;
  }

  private void Start()
  {
    m_rb = GetComponent<Rigidbody>();
    m_anim = GetComponent<Animator>();
    WalkToTargetState(new Target(Camera.main.transform));
    if (m_bomb)
      CarryToTargetState(new Target(Camera.main.transform));
  }

  private void BombKinematic(bool kinematic)
  {
    if (m_bomb == null)
      return;

    // If kinematic, make rigid body kinematic and disable collider
    Collider collider = m_bomb.GetComponent<Collider>();
    if (collider != null)
      collider.enabled = !kinematic;
    Rigidbody rb = m_bomb.GetComponent<Rigidbody>();
    if (rb != null)
      rb.isKinematic = kinematic;
  }

  // Call after instantiation (executes after Awake() but before Start())
  public void AddBomb(GameObject bomb)
  {
    m_bomb = bomb;
  }
}