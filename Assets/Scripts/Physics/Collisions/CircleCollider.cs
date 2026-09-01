using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class CircleCollider : IPhysicsCollider
{
    public PhysicsObject Owner { get; }
    private Vector2 _center;                        public Vector2 Center => Owner.LocalToWorld_Point(_center);
    private float _radius;                          public float Radius => Mathf.Max(_radius * Owner.Scale.x, _radius * Owner.Scale.y);

    public CircleCollider(PhysicsObject owner, UnityEngine.CircleCollider2D collider) { 
        Owner = owner;
        _center = collider.offset;
        _radius = collider.radius;
    }
    
    public (Vector2 min, Vector2 max) GetBoundingBox()
    {
        Vector2 r = new Vector2(Radius, Radius);
        return (Center - r, Center + r);
    }
}
