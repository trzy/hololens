using UnityEngine;

interface IProjectile
{
  int HitPoints { get; }
  float Lifetime { get; }
  void IgnoreCollisions(GameObject obj);
}