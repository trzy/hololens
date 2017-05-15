using System.Collections.Generic;
using UnityEngine;

namespace Verlet
{
  public interface IConstraint
  {
    void Solve();
  }

  public interface IBody
  {
    float mass { get; }
    Vector3 position { get; set; }
    Vector3 GetFramePosition(float lerpFactor);
    void Update(float deltaTime);
    void SolveConstraints();
    void AddForce(Vector3 force);
    void AddConstraint(IConstraint constraint);
  }

  public class LinkConstraint: IConstraint
  {
    private IBody m_body1;
    private IBody m_body2;
    private float m_maxLength;
    private float m_p1;
    private float m_p2;

    public void Solve()
    {
      Vector3 delta = m_body1.position - m_body2.position;
      float distance = delta.magnitude;
      Vector3 dir = delta / distance;
      float adjustment1 = m_p1 * (distance - m_maxLength);
      float adjustment2 = m_p2 * (distance - m_maxLength);
      m_body1.position = m_body1.position - adjustment1 * dir;
      m_body2.position = m_body2.position + adjustment2 * dir;
    }

    public LinkConstraint(IBody body1, IBody body2, float length, float stiffness = 1)
    {
      m_body1 = body1;
      m_body2 = body2;
      m_maxLength = length;

      // Precompute
      m_p1 = stiffness * (1 / body1.mass) / ((1 / body1.mass) + (1 / body2.mass));
      m_p2 = stiffness * (1 / body2.mass) / ((1 / body1.mass) + (1 / body2.mass));
    }
  }

  public class ConstraintSolver
  {
    private List<IConstraint> m_constraints;

    public void SolveConstraints()
    {
      foreach (IConstraint constraint in m_constraints)
      {
        constraint.Solve();
      }
    }

    public void AddConstraint(IConstraint constraint)
    {
      m_constraints.Add(constraint);
    }

    public ConstraintSolver()
    {
      m_constraints = new List<IConstraint>();
    }
  }

  public class Anchor: ConstraintSolver, IBody
  {
    private Transform m_transform;
    private Joint m_joint;
    private Vector3 m_position;

    public float mass
    {
      get { return float.PositiveInfinity; }
    }

    public Vector3 position
    {
      get { return m_position; }
      set { }
    }

    public Vector3 GetFramePosition(float lerpFactor)
    {
      return m_position;
    }

    public void Update(float deltaTime)
    {
      if (m_joint)
        m_position = m_transform.TransformPoint(m_joint.anchor);
      else if (m_transform)
        m_position = m_transform.position;
    }

    public void AddForce(Vector3 force)
    {
    }

    public Anchor(Vector3 p)
    {
      m_transform = null;
      m_joint = null;
      m_position = p;
    }

    public Anchor(Transform anchoredTo)
    {
      m_transform = anchoredTo;
      m_joint = null;
      m_position = anchoredTo.position;
    }

    public Anchor(Joint anchoredTo)
    {
      m_transform = anchoredTo.gameObject.transform;
      m_joint = anchoredTo;
      m_position = m_transform.TransformPoint(m_joint.anchor);
    }
  }

  public class PointMass: ConstraintSolver, IBody
  {
    private Vector3 m_position;
    private Vector3 m_lastPosition;
    private Vector3 m_acceleration = Vector3.zero;
    private float m_mass;
    private float m_damping;

    public float mass
    {
      get { return m_mass; }
    }

    public Vector3 position
    {
      get { return m_position; }
      set { m_position = value; }
    }

    public Vector3 GetFramePosition(float lerpFactor)
    {
      return Vector3.Lerp(m_lastPosition, m_position, lerpFactor);
    }

    public void Update(float deltaTime)
    {
      float deltaTime2 = deltaTime * deltaTime;
      Vector3 nextPosition = m_position + (m_position - m_lastPosition) * m_damping + m_acceleration * deltaTime2;
      m_lastPosition = m_position;
      m_position = nextPosition;
    }

    public void AddForce(Vector3 force)
    {
      m_acceleration += force / mass;
    }

    public PointMass(Vector3 p, float m, float damping = 0)
    {
      position = p;
      m_lastPosition = p;
      m_mass = m;
      m_damping = 1 - damping;
    }
  }

  public class System
  {
    private List<IBody> m_bodies;
    private float m_timeStep;
    private int m_constraintIterations;
    private float m_extraTime = 0;
    private float m_lerpFactor = 0;

    public float lerp
    {
      get { return m_lerpFactor; }
    }

    public void Update(float timeSinceLastCalled)
    {
      float timeSimulated = m_extraTime;
      while (timeSimulated <= timeSinceLastCalled)
      {
        for (int i = 0; i < m_constraintIterations; i++)
        {
          foreach (IBody body in m_bodies)
          {
            body.SolveConstraints();
          }
        }
        foreach (IBody body in m_bodies)
        {
          body.Update(m_timeStep);
        }
        timeSimulated += m_timeStep;
      }

      // Time over-simulated and interpolation factor
      m_extraTime = timeSimulated - timeSinceLastCalled;
      float timeLastIteration = timeSimulated - m_timeStep;
      m_lerpFactor = (timeSinceLastCalled - timeLastIteration) / m_timeStep;
    }

    public void AddBody(IBody body)
    {
      m_bodies.Add(body);
    }

    public System(float timeStep = 1f / 60, int constraintIterations = 3)
    {
      m_bodies = new List<IBody>();
      m_timeStep = timeStep;
      m_constraintIterations = constraintIterations;
    }
  }
}