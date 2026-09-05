using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class PolygonCollider : IPhysicsCollider //only works for simple convex polygons
{
    public PhysicsObject Owner { get; }
    private readonly Vector2[] path_local;
    private readonly Vector2[] path_world;

    public PolygonCollider(PhysicsObject owner, UnityEngine.PolygonCollider2D collider) {
        if (collider.pathCount > 1) throw new ArgumentException("Not a proper convex polygon");

        path_local = collider.GetPath(0);

        Vector2 prevTangent = path_local[1] - path_local[0];
        float cross = Vectors2D.Cross(path_local[0] - path_local[path_local.Length - 1], prevTangent);

        for (int i = 1; i < path_local.Length; i++)
        {
            Vector2 tangent = path_local[(i + 1) % path_local.Length] - path_local[i]; //looping through all the edges. May be clockwise or counterclockwise

            if (Vectors2D.Cross(prevTangent, tangent) * cross <= 0) throw new ArgumentException("Not a proper convex polygon");

            prevTangent = tangent;
        }

        if (cross > 0) Array.Reverse(path_local); //force CW winding

        Owner = owner;

        path_world = new Vector2[path_local.Length];
        for (int i = 0; i < path_world.Length; i++) path_world[i] = Owner.LocalToWorld_Point(path_local[i]);
    }
    public PolygonCollider(PhysicsObject owner, UnityEngine.BoxCollider2D collider)
    {
        Owner = owner;

        Vector2 halfSize = collider.size * 0.5f;

        path_local = new Vector2[4];
        path_local[0] = collider.offset + new Vector2(-halfSize.x, halfSize.y);
        path_local[1] = collider.offset + new Vector2(halfSize.x, halfSize.y);
        path_local[2] = collider.offset + new Vector2(halfSize.x, -halfSize.y);
        path_local[3] = collider.offset + new Vector2(-halfSize.x, -halfSize.y);

        path_world = new Vector2[4];
        for (int i = 0; i < 4; i++) path_world[i] = Owner.LocalToWorld_Point(path_local[i]);
    }
    public (Vector2 min, Vector2 max) GetBoundingBox()
    {
        if (path_local.Length == 0) return (new Vector2(float.NaN,float.NaN), new Vector2(float.NaN, float.NaN));
        Vector2 first = Owner.LocalToWorld_Point(path_local[0]);

        Vector2 min = first;
        Vector2 max = first;

        for (int i = 1; i < path_local.Length; i++)
        {
            Vector2 point = Owner.LocalToWorld_Point(path_local[i]);

            if (point.x < min.x) min.x = point.x;
            if (point.y < min.y) min.y = point.y;
            if (point.x > max.x) max.x = point.x;
            if (point.y > max.y) max.y = point.y;
        }

        return (min, max);
    }

    public IReadOnlyList<Vector2> GetPath()
    {
        return path_world;
    }

    void IPhysicsCollider.PositionUpdate(Vector2 deltaP)
    {
        for (int i = 0; i < path_world.Length; i++) path_world[i] += deltaP;
    }
    void IPhysicsCollider.FullUpdate()
    {
        for (int i = 0; i < path_world.Length; i++) path_world[i] = Owner.LocalToWorld_Point(path_local[i]);
    }
}
