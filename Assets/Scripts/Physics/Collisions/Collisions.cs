using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;
public interface IPhysicsCollider
{
    PhysicsObject Owner { get; }
    public (Vector2 min, Vector2 max) GetBoundingBox();
}
public struct Collision
{
    public IPhysicsCollider reference;
    public IPhysicsCollider incident;

    public float penetration;
    public Vector2[] contactPoints;
    public Vector2 normal;

    public static List<Collision> GetCollisions(DynamicObject obj, List<PhysicsObject> candidates) {
        List<Collision> collisions = new List<Collision>();
        foreach (IPhysicsCollider collider in obj.Colliders)
        {
            foreach (PhysicsObject other_obj in candidates)
            {
                foreach (IPhysicsCollider other_collider in other_obj.Colliders)
                {
                    Collision collision = Collision.Check(collider, other_collider);
                    if (collision.contactPoints != null)
                    {
                        collisions.Add(collision);
                        Debug.Log("Collision between " + obj.Instance + " and " + other_obj.Instance);
                    }
                }
            }
        }
        return collisions;
    }

    public static Collision Check(IPhysicsCollider a, IPhysicsCollider b)
    {
        if (a is PolygonCollider polyA)
        {
            if (b is PolygonCollider polyB) return new Collision(polyA, polyB);
            if (b is CircleCollider circleB) return new Collision(polyA, circleB);
        }
        else if (a is CircleCollider circleA)
        {
            if (b is PolygonCollider polyB) return new Collision(polyB, circleA);
            if (b is CircleCollider circleB) return new Collision(circleA, circleB);
        }
        return new Collision(float.NaN);
    }

    private Collision(PolygonCollider polyA, PolygonCollider polyB) //using Separating Axis Theorem
    {
        reference = polyA;
        incident = polyB;
        penetration = float.PositiveInfinity;
        normal = new Vector2(float.NaN, float.NaN);
        contactPoints = null;

        Vector2[] pathA = polyA.GetPath();
        Vector2[] pathB = polyB.GetPath();
        DoEdgeNormalProjections(polyA, pathA, polyB, pathB);
        if (!float.IsNaN(penetration)) //have not found a sepataing axis
            DoEdgeNormalProjections(polyB, pathB, polyA, pathA); 
    }
    private void DoEdgeNormalProjections(PolygonCollider poly, IReadOnlyList<Vector2> path, PolygonCollider other_poly, IReadOnlyList<Vector2> other_path) {
        Vector2 prevEdge = path[0] - path[path.Count - 1];
        for (int i = 0; i < path.Count; i++) //for each vertex in the polygon
        {
            Vector2 edgeTangent = path[(i + 1) % path.Count] - path[i % path.Count];
            Vector2 edgeNormal = new Vector2(-edgeTangent.y, edgeTangent.x).normalized;
            if (Vector2.Dot(edgeNormal, prevEdge) < 0) edgeNormal = -edgeNormal; //force the normal vector to face outward. This doesn't matter for collision detection but does for resolution

                Debug.DrawLine(path[i], path[i] + edgeTangent, Color.red, Time.fixedDeltaTime);
                Debug2.DrawArrow(path[i] + edgeTangent*.5f, edgeNormal, Color.magenta, Time.fixedDeltaTime);

            float p = FindProjectionOverlap(path, other_path, edgeNormal);

            if (float.IsNaN(p)) {
                contactPoints = null;
                penetration = float.NaN; // <-
                return;
            }
            else if (p < penetration)
            {
                reference = poly;
                incident = other_poly;
                
                normal = edgeNormal;
                penetration = p;
                contactPoints = new Vector2[0];
            }

                prevEdge = edgeTangent;
        }
    }
    private static float FindProjectionOverlap(IReadOnlyList<Vector2> pathA, IReadOnlyList<Vector2> pathB, Vector2 dir) {
        (float min, float max) projA = ProjectPolygon(pathA, dir);
        (float min, float max) projB = ProjectPolygon(pathB, dir);

        float overlap = Mathf.Min(projA.max, projB.max) - Mathf.Max(projA.min, projB.min);

        if (overlap < 0) return float.NaN;
        else return overlap;
    }
    private static (float max, float min) ProjectPolygon(IReadOnlyList<Vector2> path, Vector2 dir) {
        (float min, float max) interval = (float.PositiveInfinity, float.NegativeInfinity);
        foreach (Vector2 vertex in path) {
            interval.min = Mathf.Min(interval.min, Vector2.Dot(dir, vertex));
            interval.max = Mathf.Max(interval.max, Vector2.Dot(dir, vertex));
        }
        return interval;
    }

    private Collision(PolygonCollider polyA, CircleCollider circleB)
    {
        reference = null;
        incident = null;
        normal = new Vector2(float.NaN, float.NaN);
        penetration = float.NaN;
        contactPoints = null;
    }

    private Collision(CircleCollider circleA, CircleCollider circleB)
    {
        reference = null;
        incident = null;
        normal = new Vector2(float.NaN, float.NaN);
        penetration = float.NaN;
        contactPoints = null;
    }


    private Collision(float f) {
        reference = null;
        incident = null;
        normal = new Vector2(f, f);
        penetration = f;
        contactPoints = null;
    }
}