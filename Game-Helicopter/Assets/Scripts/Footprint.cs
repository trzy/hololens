using UnityEngine;

public static class Footprint
{
  public static Vector2 Measure(TextMesh textMesh)
  {
    float width = 0;
    float height = 0;
    foreach (char symbol in textMesh.text)
    {
      CharacterInfo info;
      if (textMesh.font.GetCharacterInfo(symbol, out info, textMesh.fontSize, textMesh.fontStyle))
      {
        width += info.advance;
        height = Mathf.Max(height, info.glyphHeight);
      }
    }
    return new Vector2(width, height) * textMesh.characterSize * textMesh.transform.lossyScale.x * 0.1f;
  }

  public static Vector3 Measure(GameObject obj)
  {
    BoxCollider box = obj.GetComponent<BoxCollider>();
    if (box != null)
    {
      Vector3 size = obj.GetComponent<BoxCollider>().size;
      size.x *= obj.transform.lossyScale.x;
      size.y *= obj.transform.lossyScale.y;
      size.z *= obj.transform.lossyScale.z;
      return size;
    }

    CapsuleCollider capsule = obj.GetComponent<CapsuleCollider>();
    if (capsule != null)
    {
      Vector3 size = Vector3.zero;
      float crossSectionScale;
      switch (capsule.direction)
      {
        case 0:
          size.x = capsule.height * obj.transform.lossyScale.x;
          size.y = capsule.radius * 2;
          size.z = size.y;
          crossSectionScale = Mathf.Max(obj.transform.lossyScale.y, obj.transform.lossyScale.z);
          size.y *= crossSectionScale;
          size.z *= crossSectionScale;
          break;
        case 1:
          size.y = capsule.height * obj.transform.lossyScale.y;
          size.x = capsule.radius * 2;
          size.z = size.x;
          crossSectionScale = Mathf.Max(obj.transform.lossyScale.x, obj.transform.lossyScale.z);
          size.x *= crossSectionScale;
          size.z *= crossSectionScale;
          break;
        default:
          size.z = capsule.height * obj.transform.lossyScale.z;
          size.x = capsule.radius * 2;
          size.y = size.x;
          crossSectionScale = Mathf.Max(obj.transform.lossyScale.x, obj.transform.lossyScale.y);
          size.x *= crossSectionScale;
          size.y *= crossSectionScale;
          break;
      }
      return size;
    }

    return Vector3.zero;
  }
}