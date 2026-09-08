using Codice.Client.BaseCommands;
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class PolygonCollider : IPhysicsCollider //only works for simple convex polygons
{
    public PhysicsObject Owner { get; }
    public (Vector2 min, Vector2 max) BoundingBox { get; private set; }

    private readonly Vector2[] path_local;

    internal readonly Vector2[] path_world;
    internal readonly Vector2[] edgeNormals; //edge normals of (path[i], path[i+1])

    private int minProjIndex = 0;
    private int maxProjIndex = 0;

    public PolygonCollider(PhysicsObject owner, UnityEngine.PolygonCollider2D collider) {
        if (collider.pathCount > 1) throw new ArgumentException("Not a proper convex polygon");

        path_local = collider.GetPath(0);
        ValidatePath();

        Owner = owner;

        path_world = new Vector2[path_local.Length];
        for (int i = 0; i < path_world.Length; i++) path_world[i] = Owner.LocalToWorld_Point(path_local[i]);

        edgeNormals = new Vector2[path_local.Length];
        SetEdgeNormals();

        RecalculateBoundingBox();
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
        edgeNormals = new Vector2[4];
        for (int i = 0; i < 4; i++)
        {
            path_world[i] = Owner.LocalToWorld_Point(path_local[i]);
        }
        SetEdgeNormals();

        RecalculateBoundingBox();
    }

    internal float GetMinDot_1(Vector2 axis) //O(n), but can be made O(log(n))
    {
        float dot = Vector2.Dot(path_world[minProjIndex], axis);
        float dot_next = Vector2.Dot(path_world[(minProjIndex + 1) % path_world.Length], axis);
        float dot_prev = Vector2.Dot(path_world[(minProjIndex - 1 + path_world.Length) % path_world.Length], axis);
        while (dot > dot_next || dot > dot_prev)
        {
            minProjIndex = (minProjIndex + 1) % path_world.Length;
            dot_prev = dot;
            dot = dot_next;
            dot_next = Vector2.Dot(path_world[(minProjIndex + 1) % path_world.Length], axis);
        }
        return dot;
    }
    internal float GetMinDot_2(Vector2 axis) { //over all edges, this should do O(n) work
        float dot = Vector2.Dot(path_world[minProjIndex], axis);
        float dot_next = Vector2.Dot(path_world[(minProjIndex + 1) % path_world.Length], axis);
        while (dot > dot_next) {
            minProjIndex = (minProjIndex + 1) % path_world.Length;
            dot = dot_next;
            dot_next = Vector2.Dot(path_world[(minProjIndex + 1) % path_world.Length], axis);
        }
        return dot;
    }

    /* UPDATE METHODS */

    void IPhysicsCollider.PositionUpdate(Vector2 deltaP)
    {
        for (int i = 0; i < path_world.Length; i++) path_world[i] += deltaP;
        BoundingBox = (BoundingBox.min + deltaP, BoundingBox.max + deltaP);
    }
    void IPhysicsCollider.FullUpdate()
    {
        for (int i = 0; i < path_world.Length; i++) path_world[i] = Owner.LocalToWorld_Point(path_local[i]);
        RecalculateBoundingBox();
        SetEdgeNormals();
    }

    private void RecalculateBoundingBox()
    {
        Vector2 min = path_world[0];
        Vector2 max = path_world[0];

        for (int i = 1; i < path_world.Length; i++)
        {
            Vector2 point = path_world[i];

            if (point.x < min.x) min.x = point.x;
            if (point.y < min.y) min.y = point.y;
            if (point.x > max.x) max.x = point.x;
            if (point.y > max.y) max.y = point.y;
        }

        BoundingBox = (min, max);
    }

    private void SetEdgeNormals() {
        for (int i = 0; i < path_world.Length; i++)
        {
            Vector2 tangent = path_world[(i+1)%path_world.Length] - path_world[i];
            edgeNormals[i] = new Vector2(-tangent.y, tangent.x).normalized;
        }
    }

    /* INITIALIZATION */

    private void ValidatePath() {
        Vector2 prevTangent = path_local[1] - path_local[0];
        float cross = Vectors2D.Cross(path_local[0] - path_local[path_local.Length - 1], prevTangent);
        for (int i = 1; i < path_local.Length; i++)
        {
            Vector2 tangent = path_local[(i + 1) % path_local.Length] - path_local[i]; //looping through all the edges. May be clockwise or counterclockwise

            if (Vectors2D.Cross(prevTangent, tangent) * cross <= 0) throw new ArgumentException("Not a proper convex polygon");

            prevTangent = tangent;
        }
        if (cross > 0) Array.Reverse(path_local); //force CW winding
    }
}
