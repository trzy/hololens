public struct SelectionTile
{
  public const int SIDE_CM = 10; // width and height of each square tile in centimeters
  public enum Edge
  {
    Top = 0x1,
    Right = 0x2,
    Bottom = 0x4,
    Left = 0x8
  }
  public IVector3 center; // in local, centimeter units
  public byte neighbors;  // bitmask indicating which sides have neighbors

  public void AddNeighbor(ref SelectionTile other)
  {
    IVector3 delta = other.center - center;
    // Left-handed coordinate system means +x is to the left when +z points out of surface
    if (delta.x == -SIDE_CM && delta.y == 0)
    {
      neighbors |= (byte)Edge.Right;
      other.neighbors |= (byte)Edge.Left;
    }
    else if (delta.x == +SIDE_CM && delta.y == 0)
    {
      neighbors |= (byte)Edge.Left;
      other.neighbors |= (byte)Edge.Right;
    }
    else if (delta.x == 0 && delta.y == +SIDE_CM)
    {
      neighbors |= (byte)Edge.Top;
      other.neighbors |= (byte)Edge.Bottom;
    }
    else if (delta.x == 0 && delta.y == -SIDE_CM)
    {
      neighbors |= (byte)Edge.Bottom;
      other.neighbors |= (byte)Edge.Top;
    }
  }

  public void RemoveNeighbor(IVector3 other)
  {
    IVector3 delta = other - center;
    if (delta.x == -SIDE_CM && delta.y == 0)
      neighbors &= ~(byte)Edge.Right & 0xff;
    else if (delta.x == +SIDE_CM && delta.y == 0)
      neighbors &= ~(byte)Edge.Left & 0xff;
    else if (delta.x == 0 && delta.y == +SIDE_CM)
      neighbors &= ~(byte)Edge.Top & 0xff;
    else if (delta.x == 0 && delta.y == -SIDE_CM)
      neighbors &= ~(byte)Edge.Bottom & 0xff;
  }

  public SelectionTile(IVector3 centerPosition)
  {
    center = centerPosition;
    neighbors = 0;
  }
}
