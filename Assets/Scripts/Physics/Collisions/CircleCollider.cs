using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class CircleCollider : IPhysicsCollider
{
    public PhysicsObject Owner { get; }

    public (Vector2 min, Vector2 max) GetBoundingBox()
    {
        return (new Vector2(float.NaN, float.NaN), new Vector2(float.NaN, float.NaN));
    }
}
