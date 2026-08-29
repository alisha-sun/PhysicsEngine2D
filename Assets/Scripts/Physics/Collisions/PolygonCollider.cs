using System.Collections;
using System.Collections.Generic;
using Unity.VisualScripting;
using UnityEngine;

public class PolygonCollider : IPhysicsCollider //only works for simple convex polygons
{
    public PhysicsObject Owner { get; }
    private Vector2[] path;

    public PolygonCollider(PhysicsObject owner, UnityEngine.PolygonCollider2D collider) { 
        Owner = owner;

        path = collider.GetPath(0);
    }
    public PolygonCollider(PhysicsObject owner, UnityEngine.BoxCollider2D collider)
    {
        Owner = owner;

        Vector2 halfSize = collider.size * 0.5f;

        path = new Vector2[4];
        path[0] = collider.offset + new Vector2(-halfSize.x, halfSize.y);
        path[1] = collider.offset + new Vector2(halfSize.x, halfSize.y);
        path[2] = collider.offset + new Vector2(halfSize.x, -halfSize.y);
        path[3] = collider.offset + new Vector2(-halfSize.x, -halfSize.y);
    }
    public (Vector2 min, Vector2 max) GetBoundingBox()
    {
        if (path.Length == 0) return (new Vector2(float.NaN,float.NaN), new Vector2(float.NaN, float.NaN));
        Vector2 first = Owner.LocalToWorld_Point(path[0]);

        Vector2 min = first;
        Vector2 max = first;

        for (int i = 1; i < path.Length; i++)
        {
            Vector2 point = Owner.LocalToWorld_Point(path[i]);

            if (point.x < min.x) min.x = point.x;
            if (point.y < min.y) min.y = point.y;
            if (point.x > max.x) max.x = point.x;
            if (point.y > max.y) max.y = point.y;
        }

        return (min, max);
    }

    public Vector2[] GetPath()
    {
        Vector2[] pathWorld = new Vector2[path.Length];
        for (int i = 0; i < path.Length; i++) pathWorld[i] = Owner.LocalToWorld_Point(path[i]);
        return pathWorld;
    }
}
