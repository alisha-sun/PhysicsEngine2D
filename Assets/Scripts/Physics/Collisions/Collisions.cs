using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Unity.VisualScripting;
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

    public static List<Collision> GetCollisions(DynamicObject obj, GameState gameState) {
        List<PhysicsObject> candidates = gameState.regions.GetPotentialCollisions(obj);
        List<Collision> collisions = new List<Collision>();
        foreach (IPhysicsCollider collider in obj.Colliders)
        {
            foreach (PhysicsObject other_obj in candidates)
            {
                foreach (IPhysicsCollider other_collider in other_obj.Colliders)
                {
                    Collision collision = Collision.Check(collider, other_collider);
                    if (collision.contactPoints != null) collisions.Add(collision);
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
        penetration = float.PositiveInfinity; //minimize this
        normal = new Vector2(float.NaN, float.NaN);
        contactPoints = null;

        Vector2[] pathA = polyA.GetPath();
        Vector2[] pathB = polyB.GetPath();

        Edge referenceEdge = FindMinimumOverlapEdge(polyA, pathA, polyB, pathB);

        if (!float.IsNaN(penetration)) //no separating axis found from polyA's edge normals
        {
            Edge e = FindMinimumOverlapEdge(polyB, pathB, polyA, pathA);
            if (!float.IsNaN(e.start.x)) referenceEdge = e;
        }
        else return;

        if (!float.IsNaN(penetration)) //there is no separating axis
        {
            if (incident == polyA) contactPoints = GetContactPoints(referenceEdge, FindIncidentEdge(pathA));
            else contactPoints = GetContactPoints(referenceEdge, FindIncidentEdge(pathB));

            foreach (Vector2 point in contactPoints) {
                Debug2.DrawX(point, Color.red, Time.fixedDeltaTime, .1f);
            }
        }
    }
    private Edge FindMinimumOverlapEdge(PolygonCollider poly, IReadOnlyList<Vector2> path, PolygonCollider other_poly, IReadOnlyList<Vector2> other_path) {
        Edge referenceEdge = new Edge(float.NaN);

        Vector2 prevEdgeTangent = path[0] - path[path.Count - 1];
        for (int i = 0; i < path.Count; i++) 
        {
            Edge edge = new Edge(path[i], path[(i + 1) % path.Count]); //looping through all the edges. May be clockwise or counterclockwise

            if (Vector2.Dot(edge.normal, prevEdgeTangent) < 0) edge.FlipNormal(); //force the normal vector to face outward, because this is required for finding the incident edge later

               Debug2.DrawArrow(edge.start + edge.tangent*.5f, edge.normal, Color.magenta, Time.fixedDeltaTime);

            float p = FindProjectionOverlap(path, other_path, edge);

            if (float.IsNaN(p)) { //found separating axis
                normal = new Vector2(float.NaN, float.NaN);
                penetration = float.NaN; // <---
                return new Edge(float.NaN);
            }
            else if (p < penetration)
            {
                reference = poly;
                incident = other_poly;
                
                normal = edge.normal;
                penetration = p;

                referenceEdge = edge;
                Debug2.Draw(edge,Color.gray,Time.fixedDeltaTime);
            }

            prevEdgeTangent = edge.tangent;
        }
        return referenceEdge;
    }
    private Edge FindIncidentEdge(IReadOnlyList<Vector2> path) {
        Edge incidentEdge = new Edge(new Vector2(float.NaN, float.NaN), new Vector2(float.NaN, float.NaN));
        float minDot = float.PositiveInfinity;
        Vector2 prevEdgeTangent = path[0] - path[path.Count - 1];
        for (int i = 0; i < path.Count; i++)
        {
            Edge edge = new Edge(path[i], path[(i + 1)%path.Count]); //looping through all the edges. May be clockwise or counterclockwise

            if (Vector2.Dot(edge.normal, prevEdgeTangent) < 0) edge.FlipNormal(); //force the normal vector to face outward, because this wont work reliably otherwise

            float dot = Vector2.Dot(edge.normal, normal);
            if (dot < minDot)
            {
                minDot = dot;
                incidentEdge = new Edge(path[i], path[(i + 1) % path.Count]);
            }

            prevEdgeTangent = edge.tangent;
        }
        return incidentEdge;
    }
    private static Vector2[] GetContactPoints(Edge referenceEdge, Edge incidentEdge) {
        Debug2.Draw(referenceEdge, Color.cyan, Time.fixedDeltaTime);
        Debug2.Draw(incidentEdge, Color.blue, Time.fixedDeltaTime);

        incidentEdge = ClipIncidentEdge(incidentEdge, referenceEdge);

        Debug2.Draw(incidentEdge, Color.red, Time.fixedDeltaTime);

        List<Vector2> l = new List<Vector2>();
        if (Vector2.Dot(incidentEdge.start - referenceEdge.start, referenceEdge.normal) < 0) l.Add(incidentEdge.start);
        if (Vector2.Dot(incidentEdge.end - referenceEdge.start, referenceEdge.normal) < 0) l.Add(incidentEdge.end);

        return l.ToArray();
    }
    private static Edge ClipIncidentEdge(Edge incidentEdge, Edge referenceEdge) {
        (Vector2 start, Vector2 end) range;
        range.start = Vectors2D.SolveSystem(referenceEdge.start, referenceEdge.normal, incidentEdge.start, incidentEdge.tangent);
        range.end = Vectors2D.SolveSystem(referenceEdge.end, referenceEdge.normal, incidentEdge.start, incidentEdge.tangent);

        if (Vector2.Dot(range.end - range.start, incidentEdge.tangent) < 0)
        {
            Vector2 temp = range.start; 
            range.start = range.end;
            range.end = temp;
        }

        Vector2 start = Vectors2D.MaxByDot(incidentEdge.tangent, range.start, incidentEdge.start);
        Vector2 end = Vectors2D.MinByDot(incidentEdge.tangent, range.end, incidentEdge.end);

        Edge clipped = new Edge(start,end);
        if (Vector2.Dot(clipped.tangent, incidentEdge.tangent) > 0) return clipped;
        else return new Edge(float.NaN); //shouldnt happen
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


    /* MATH-Y HELPER FUNCTIONS */

    private static float FindProjectionOverlap(IReadOnlyList<Vector2> path, IReadOnlyList<Vector2> other_path, Edge edge)
    {
        (float min, float max) proj = ProjectPolygon(path, edge.normal);
        (float min, float max) other_proj = ProjectPolygon(other_path, edge.normal);

        float overlap = float.NaN;
        float extreme = Vector2.Dot(edge.normal, edge.start);
        const float EPSILON = 1e-5f;
        if (Mathf.Abs(extreme - proj.min) < EPSILON) overlap = other_proj.max - proj.min; //if the reference edge is the reference polygon's min, move the incident polygon's max to the reference polygon's min
        else if (Mathf.Abs(extreme - proj.min) > EPSILON) overlap = proj.max - other_proj.min; //otherwise the reference edge is the reference polygon's max, so we must move the other polygon's min to the reference polygon's max
        else Debug.LogError("Not a convex polygon.");

        if (overlap < 0) return float.NaN;
        else return overlap;
    }
    private static (float min, float max) ProjectPolygon(IReadOnlyList<Vector2> path, Vector2 dir)
    {
        (float min, float max) interval = (float.PositiveInfinity, float.NegativeInfinity);
        foreach (Vector2 vertex in path)
        {
            interval.min = Mathf.Min(interval.min, Vector2.Dot(dir, vertex));
            interval.max = Mathf.Max(interval.max, Vector2.Dot(dir, vertex));
        }
        return interval;
    }
}