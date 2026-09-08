using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class CircleCollider : IPhysicsCollider
{
    public PhysicsObject Owner { get; }
    public (Vector2 min, Vector2 max) BoundingBox { get; private set; }
    private readonly Vector2 _center;                       public Vector2 Center;
    private readonly float _radius;                         public float Radius;

    public CircleCollider(PhysicsObject owner, UnityEngine.CircleCollider2D collider) { 
        Owner = owner;
        _center = collider.offset;
        Center = owner.LocalToWorld_Point(_center);
        _radius = collider.radius;
        Radius = Mathf.Max(_radius * Owner.Scale.x, _radius * Owner.Scale.y);
        RecalculateBoundingBox();
    }

    void IPhysicsCollider.PositionUpdate(Vector2 deltaP) {
        Center += deltaP;
        BoundingBox = (BoundingBox.min + deltaP, BoundingBox.max + deltaP);
    }
    void IPhysicsCollider.FullUpdate() { 
        Center = Owner.LocalToWorld_Point(_center);
        Radius = Mathf.Max(_radius * Owner.Scale.x, _radius * Owner.Scale.y);
        RecalculateBoundingBox();
    }

    internal void RecalculateBoundingBox()
    {
        Vector2 r = new Vector2(Radius, Radius);
        BoundingBox = (Center - r, Center + r);
    }
}
