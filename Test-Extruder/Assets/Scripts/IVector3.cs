using UnityEngine;

public struct IVector3
{
  public int x;
  public int y;
  public int z;

  public override string ToString()
  {
    return string.Format("({0}, {1}, {2})", x, y, z);
  }

  public static bool operator ==(IVector3 lhs, IVector3 rhs)
  {
    return lhs.x == rhs.x && lhs.y == rhs.y && lhs.z == rhs.z;
  }

  public static bool operator !=(IVector3 lhs, IVector3 rhs)
  {
    return lhs.x != rhs.x || lhs.y != rhs.y || lhs.z != rhs.z;
  }

  public static IVector3 operator *(int scalar, IVector3 v)
  {
    return new IVector3(scalar * v.x, scalar * v.y, scalar * v.z);
  }

  public static IVector3 operator *(IVector3 v, int scalar)
  {
    return scalar * v;
  }

  public static IVector3 operator +(IVector3 a, IVector3 b)
  {
    return new IVector3(a.x + b.x, a.y + b.y, a.z + b.z);
  }

  public static IVector3 operator -(IVector3 a, IVector3 b)
  {
    return new IVector3(a.x - b.x, a.y - b.y, a.z - b.z);
  }

  public IVector3(Vector3 v)
  {
    x = (int) v.x;
    y = (int) v.y;
    z = (int) v.z;
  }

  public IVector3(int ix, int iy)
  {
    x = ix;
    y = iy;
    z = 0;
  }

  public IVector3(int ix, int iy, int iz)
	{
    x = ix;
    y = iy;
    z = iz;
	}
}
