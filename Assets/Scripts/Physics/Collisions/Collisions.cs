using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Unity.Profiling;
public interface IPhysicsCollider
{
    PhysicsObject Owner { get; }
    public (Vector2 min, Vector2 max) GetBoundingBox();
    internal void PositionUpdate(Vector2 deltaP);
    internal void FullUpdate();
}
public struct Collision
{
    static readonly ProfilerMarker resolveMarker = new("Resolve");
    static readonly ProfilerMarker detectMarker = new("Detect");
    
    private const float OVERLAP_EPSILON = 1e-5f;

    public IPhysicsCollider reference;
    public IPhysicsCollider incident;

    public float penetration;
    public Vector2[] contactPoints;
    public Vector2 normal;

    public static void DoCollisions(DynamicObject obj, GameState gameState)
    {
        var candidates = gameState.regions.GetPotentialCollisions(obj);
        Vector2 deltaP = Vector2.zero;

        foreach (IPhysicsCollider collider in obj.Colliders)
        {
            foreach (PhysicsObject other_obj in candidates)
            {
                foreach (IPhysicsCollider other_collider in other_obj.Colliders)
                {
                    GameState.pairs++;
                    Collision collision = Collision.Check(collider, other_collider);
                    if (collision.contactPoints != null)
                    {
                        using (resolveMarker.Auto())
                            deltaP += collision.Resolve(obj);
                        GameState.collisions++;
                    }
                }
            }
        }
        PhysicsEngine.instance.gameState.regions.UpdateRegions(obj, deltaP);
    }
    public static void DoCollisions(StaticObject obj, GameState gameState)
    {
        var candidates = gameState.regions.GetPotentialCollisions(obj);
        foreach (IPhysicsCollider collider in obj.Colliders)
        {
            foreach (PhysicsObject other_obj in candidates)
            {
                foreach (IPhysicsCollider other_collider in other_obj.Colliders)
                {
                    Collision collision = Collision.Check(collider, other_collider);
                    if (collision.contactPoints != null)
                    {
                        collision.Resolve(obj);
                    }
                }
            }
        }
    }

    public static Collision Check(IPhysicsCollider a, IPhysicsCollider b)
    {
        using (detectMarker.Auto()) {
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
    }

    public readonly Vector2 Resolve(PhysicsObject obj) {
        if (contactPoints == null || contactPoints.Length == 0) return Vector2.zero;

        float e = Restitution();

        int contactCount = contactPoints.Length;

        Vector2 point0 = contactPoints[0];
        float impulse0 = 0;
        Vector2 point1 = default;
        float impulse1 = 0;

        if (contactCount == 2) {
            point1 = contactPoints[1];

            float rv_n0 = Vector2.Dot(
                  incident.Owner.GetVelocityAtPoint(point0, true) - reference.Owner.GetVelocityAtPoint(point0, true)
                , normal); //relative velocity along the collision normal at the first contact point

            float rv_n1 = Vector2.Dot(
                  incident.Owner.GetVelocityAtPoint(point1, true) - reference.Owner.GetVelocityAtPoint(point1, true)
                , normal); //relative velocity along the collision normal at the second contact point

            Vector2 impulseMagnitudes;
            if (rv_n0 > 0 && rv_n1 > 0) return Vector2.zero;
            else if (rv_n0 > 0)
            {
                impulseMagnitudes = Vectors2D.SolveSystem(CalculateContactVelocityCoefficient(0, 0), CalculateContactVelocityCoefficient(0, 1),
                                                          CalculateContactVelocityCoefficient(1, 0), CalculateContactVelocityCoefficient(1, 1),
                                                          new Vector2(0, -(1 + e) * rv_n1));
            }
            else if (rv_n1 > 0)
            {
                impulseMagnitudes = Vectors2D.SolveSystem(CalculateContactVelocityCoefficient(0, 0), CalculateContactVelocityCoefficient(0, 1),
                                                          CalculateContactVelocityCoefficient(1, 0), CalculateContactVelocityCoefficient(1, 1),
                                                          new Vector2(-(1 + e) * rv_n0, 0));
            }
            else
            {
                impulseMagnitudes = Vectors2D.SolveSystem(CalculateContactVelocityCoefficient(0, 0), CalculateContactVelocityCoefficient(0, 1),
                                                          CalculateContactVelocityCoefficient(1, 0), CalculateContactVelocityCoefficient(1, 1),
                                                          new Vector2(-(1 + e) * rv_n0, -(1 + e) * rv_n1));
            } //these matrcies are singular if the two contact points are aligned along the collision normal

            if (!Vectors2D.IsFinite(impulseMagnitudes) || (impulseMagnitudes.x < 0 && impulseMagnitudes.y < 0))
            {
                Debug2.Draw(this, Time.fixedDeltaTime);
                Debug.Log(this);
                Debug.LogWarning("Unable to solve for impulse in double contact point collision between " + reference.Owner.Instance.name + " and " + incident.Owner.Instance.name);
                point0 = (point0 + point1)/2;
                contactCount = 1;
            }
            else if (impulseMagnitudes.x < 0) {
                point0 = point1;
                contactCount = 1;
            }
            else if (impulseMagnitudes.y < 0)
            {
                contactCount = 1;
            }
            else
            {
                impulse0 = impulseMagnitudes.x;
                impulse1 = impulseMagnitudes.y;
            }
        }

        if (contactCount == 1) {
            Edge centerToContact_reference = new Edge(reference.Owner.Position, point0);
            Edge centerToContact_incident = new Edge(incident.Owner.Position, point0);

            float rv_n = Vector2.Dot(
                  incident.Owner.GetVelocityAtPoint(point0, true) - reference.Owner.GetVelocityAtPoint(point0, true)
                , normal); //relative velocity along the collision normal (negative if the space between the two objects is decreasing)

            if (rv_n > 0) return Vector2.zero;

            float j = (-(1 + e) * rv_n) /
                ((1 / reference.Owner.Mass) + (1 / incident.Owner.Mass) +
                (Mathf.Pow(Vectors2D.Cross(centerToContact_reference.tangent, normal), 2) / reference.Owner.Inertia) +
                (Mathf.Pow(Vectors2D.Cross(centerToContact_incident.tangent, normal), 2) / incident.Owner.Inertia)   );

            if (!float.IsFinite(j) || j < 0)
            {
                Debug2.Draw(this, Time.fixedDeltaTime);
                Debug.Log(this);
                Debug.LogError("Unable to solve for impulse in single contact point collision between " + reference.Owner.Instance.name + " and " + incident.Owner.Instance.name);
            }
            else impulse0 = j;
        }


        Vector2 deltaP_obj = Vector2.zero;
        float ratio = PercentMass_Reference();
        if (reference.Owner is DynamicObject d1)
        {
            d1.ApplyImpulse(-impulse0 * normal, d1.WorldToLocal_Point(point0));
            if (contactCount == 2) d1.ApplyImpulse(-impulse1 * normal, d1.WorldToLocal_Point(point1));

            Vector2 deltaP = -penetration * (1 - ratio) * normal;

            d1.SetPosition_Internal(d1.Position + deltaP);

            if (d1 == obj)
            {
                deltaP_obj = deltaP;
                d1.OnTranslation(deltaP, false);
            }
            else {
                d1.OnTranslation(deltaP, true);
            }
        }

            if (incident.Owner is DynamicObject d2)
        {
            d2.ApplyImpulse(impulse0 * normal, d2.WorldToLocal_Point(point0));
            if (contactCount == 2) d2.ApplyImpulse(impulse1 * normal, d2.WorldToLocal_Point(point1));

            Vector2 deltaP = penetration * ratio * normal;

            d2.SetPosition_Internal(d2.Position + deltaP);

            if (d2 == obj)
            {
                deltaP_obj = deltaP;
                d2.OnTranslation(deltaP, false);
            }
            else
            {
                d2.OnTranslation(deltaP, true);
            }
        }

        /*Debug2.DrawX(point0, Color.red, Time.fixedDeltaTime);
        Debug2.DrawArrow(point0, normal * impulse0, Color.green, Time.fixedDeltaTime);
        if (contactCount == 2) {
            Debug2.DrawX(point1, Color.red, Time.fixedDeltaTime);
            Debug2.DrawArrow(point1, normal * impulse1, Color.green, Time.fixedDeltaTime);
        }*/

        return deltaP_obj;
    }

    private readonly float PercentMass_Reference()
    {
        if (reference.Owner.Mass == float.PositiveInfinity)
        {
            if (incident.Owner.Mass == float.PositiveInfinity) return .5f;
            else return 1f;
        }
        else if (incident.Owner.Mass == float.PositiveInfinity) return 0;
        else return (reference.Owner.Mass / (reference.Owner.Mass + incident.Owner.Mass));
    }

    private readonly float Restitution()
    {
        return 0; //consider physics materials later
    }

    private readonly float CalculateContactVelocityCoefficient(int v_index, int j_index) { //i worked through the algebra myself :/
        if (v_index > 1 || j_index > 1 || v_index < 0 || j_index < 0) return float.NaN;
        Edge a_j = new Edge(incident.Owner.Position, contactPoints[j_index]);
        Edge a_v = new Edge(incident.Owner.Position, contactPoints[v_index]);
        Edge b_j = new Edge(reference.Owner.Position, contactPoints[j_index]);
        Edge b_v = new Edge(reference.Owner.Position, contactPoints[v_index]);
        return Vector2.Dot(
            (normal / incident.Owner.Mass) + (Vectors2D.Cross(a_j.tangent, normal) * a_v.tangent.magnitude * a_v.normal / incident.Owner.Inertia) +
            (normal / reference.Owner.Mass) + (Vectors2D.Cross(b_j.tangent, normal) * b_v.tangent.magnitude * b_v.normal / reference.Owner.Inertia)
            , normal);
    }


    /* CONSTRUCTORS */

    private Collision(PolygonCollider polyA, PolygonCollider polyB) //using Separating Axis Theorem
    {
        reference = polyA;
        incident = polyB;
        penetration = float.PositiveInfinity; //minimize this
        normal = new Vector2(float.NaN, float.NaN);
        contactPoints = null;

        IReadOnlyList<Vector2> pathA = polyA.GetPath();
        IReadOnlyList<Vector2> pathB = polyB.GetPath();

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
        }
    }
    private Edge FindMinimumOverlapEdge(PolygonCollider poly, IReadOnlyList<Vector2> path, PolygonCollider other_poly, IReadOnlyList<Vector2> other_path) {
        Edge referenceEdge = new Edge(float.NaN);

        for (int i = 0; i < path.Count; i++) 
        {
            Edge edge = new Edge(path[i], path[(i + 1) % path.Count]); //looping through all the edges (CW)

            //Debug2.DrawArrow(edge.start + edge.tangent*.5f, edge.normal, Color.magenta, Time.fixedDeltaTime);

            float p = FindProjectionOverlap(path, other_path, edge);

            if (float.IsNaN(p)) { //found separating axis
                normal = new Vector2(float.NaN, float.NaN);
                penetration = float.NaN; // <---
                return new Edge(float.NaN);
            }
            else if (p < penetration) {
                reference = poly;
                incident = other_poly;
                
                normal = edge.normal;
                penetration = p;

                referenceEdge = edge;
                //Debug2.Draw(edge,Color.gray,Time.fixedDeltaTime);
            }
        }
        return referenceEdge;
    }
    private readonly Edge FindIncidentEdge(IReadOnlyList<Vector2> path) {
        Edge incidentEdge = new Edge(new Vector2(float.NaN, float.NaN), new Vector2(float.NaN, float.NaN));
        float minDot = float.PositiveInfinity;

        for (int i = 0; i < path.Count; i++)
        {
            Vector2 tangent = path[(i + 1) % path.Count] - path[i]; //looping through all the edges (CW)

            float dot = Vectors2D.Cross(tangent.normalized, normal); //same as dot product between the edge normal and the collision normal, just less computation
            if (dot < minDot)
            {
                minDot = dot;
                incidentEdge = new Edge(path[i], path[(i + 1) % path.Count]);
            }
        }
        return incidentEdge;
    }
    private static Vector2[] GetContactPoints(Edge referenceEdge, Edge incidentEdge) {
        //Debug2.Draw(referenceEdge, Color.cyan, Time.fixedDeltaTime);
        //Debug2.Draw(incidentEdge, Color.blue, Time.fixedDeltaTime);

        incidentEdge = ClipIncidentEdge(incidentEdge, referenceEdge);

        //Debug2.Draw(incidentEdge, Color.red, Time.fixedDeltaTime);

        List<Vector2> l = new List<Vector2>();
        if (Vector2.Dot(incidentEdge.start - referenceEdge.start, referenceEdge.normal) < 0) l.Add(incidentEdge.start);
        if (Vector2.Dot(incidentEdge.end - referenceEdge.start, referenceEdge.normal) < 0) l.Add(incidentEdge.end);

        return l.ToArray();
    }
    private static Edge ClipIncidentEdge(Edge incidentEdge, Edge referenceEdge) { //the dot product between these two should be negative
        Vector2 start = Vectors2D.SolveSystem(referenceEdge.end, referenceEdge.normal, incidentEdge.start, incidentEdge.tangent); //this ordering relies on the dot product being negative
        Vector2 end = Vectors2D.SolveSystem(referenceEdge.start, referenceEdge.normal, incidentEdge.start, incidentEdge.tangent);

        Edge clipped = new Edge(
            Vectors2D.MaxByDot(incidentEdge.tangent, start, incidentEdge.start),
            Vectors2D.MinByDot(incidentEdge.tangent, end, incidentEdge.end)
        );

        if (Vector2.Dot(clipped.tangent, incidentEdge.tangent) > 0) return clipped;
        else return new Edge(float.NaN); //shouldnt happen
    }

    private Collision(PolygonCollider polyA, CircleCollider circleB)
    {
        reference = polyA;
        incident = circleB;
        penetration = float.PositiveInfinity; //minimize this
        normal = new Vector2(float.NaN, float.NaN);
        contactPoints = null;

        IReadOnlyList<Vector2> path = polyA.GetPath();

        FindMinimumOverlapEdge(path, circleB.Center, circleB.Radius);
        if (float.IsNaN(penetration)) return; //found separating axis

        Vector2 p = CheckClosestVertex(path, polyA, circleB);
        if (float.IsNaN(penetration)) return; //found separating axis

        if (reference == circleB) contactPoints = new Vector2[] { p };
        else contactPoints = new Vector2[] { circleB.Center - circleB.Radius*normal };
        //Debug2.DrawX(contactpoint0, Color.cyan, Time.fixedDeltaTime, .3f);
    }
    private void FindMinimumOverlapEdge(IReadOnlyList<Vector2> path, Vector2 center, float radius)
    {
        for (int i = 0; i < path.Count; i++)
        {
            Edge edge = new Edge(path[i], path[(i + 1) % path.Count]); //looping through all the edges (CCW)

            //Debug2.DrawArrow(edge.start + edge.tangent * .5f, edge.normal, Color.magenta, Time.fixedDeltaTime);

            float p = FindProjectionOverlap(path, center, radius, edge);

            if (float.IsNaN(p)) { //found separating axis
                normal = new Vector2(float.NaN, float.NaN);
                penetration = float.NaN; // <---
            }
            else if (p < penetration) {
                normal = edge.normal;
                penetration = p;
                //Debug2.Draw(edge, Color.gray, Time.fixedDeltaTime);
            }
        }
    }
    private Vector2 CheckClosestVertex(IReadOnlyList<Vector2> path, PolygonCollider polyA, CircleCollider circleB)
    {
        Vector2 closest = Vectors2D.FindClosestPoint(path, circleB.Center);
        Edge centerToClosest = new Edge(circleB.Center, closest);
        float p = -Vector2.Dot(closest - (circleB.Center + centerToClosest.tangent.normalized * circleB.Radius), // vector from circle edge to closest vertex
                               centerToClosest.tangent.normalized);                                              // direction from center to closest vertex

        //
        //Line(closest, closest + centerToClosest.tangent.normalized * p, Color.yellow, Time.fixedDeltaTime);

        if (p < 0) //vertex is not touching the circle
        {
            if (IsGapBetweenProjections(path, circleB.Center, circleB.Radius, centerToClosest.tangent.normalized)) { // centerToClosest.tangent is orthogonal to the "edge" of the circle
                normal = new Vector2(float.NaN, float.NaN);
                penetration = float.NaN; //found seperating axis
            }
            return new Vector2(float.NaN, float.NaN);
        }
        else if (p < penetration) //vertex is touching the circle and the penetration is less than previously
        {
            reference = circleB;
            incident = polyA;
            penetration = p;
            normal = centerToClosest.tangent.normalized;
            return closest;
        }
        else return new Vector2(float.NaN, float.NaN); //do nothing otherwise
    }

    private Collision(CircleCollider circleA, CircleCollider circleB)
    {
        reference = circleA;
        incident = circleB;
        normal = (circleB.Center - circleA.Center);
        penetration = -normal.magnitude + circleA.Radius + circleB.Radius;

        if (penetration < 0) { 
            normal = new Vector2(float.NaN,float.NaN); 
            penetration = float.NaN;
            contactPoints = null;
            return; 
        }
        normal = normal.normalized;
        contactPoints = new Vector2[] { (circleA.Center + normal*circleA.Radius + circleB.Center - normal * circleB.Radius)/2 };

        //Debug2.DrawArrow(circleA.Center, normal, Color.magenta, Time.fixedDeltaTime);
        //Debug.DrawLine(contactpoint0, contactpoint0 - normal*penetration, Color.yellow, Time.fixedDeltaTime);
        //Debug2.DrawX(contactpoint0, Color.blue, Time.fixedDeltaTime, .2f);
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

        return FindOverlap(proj, other_proj, edge);
    }
    private static float FindProjectionOverlap(IReadOnlyList<Vector2> path, Vector2 center, float radius, Edge edge)
    {
        (float min, float max) proj = ProjectPolygon(path, edge.normal);
        (float min, float max) other_proj = ProjectCircle(center, radius, edge.normal);

        return FindOverlap(proj, other_proj, edge);
    }
    private static bool IsGapBetweenProjections(IReadOnlyList<Vector2> path, Vector2 center, float radius, Vector2 normal) {
        (float min, float max) proj = ProjectPolygon(path, normal);
        (float min, float max) other_proj = ProjectCircle(center, radius, normal);

        return Mathf.Max(proj.min, other_proj.min) - Mathf.Min(proj.max, other_proj.max) > 0;
    }

    private static (float min, float max) ProjectPolygon(IReadOnlyList<Vector2> path, Vector2 dir)
    {
        (float min, float max) interval = (float.PositiveInfinity, float.NegativeInfinity);
        foreach (Vector2 vertex in path)
        {
            interval.min = Mathf.Min(interval.min, Vector2.Dot(vertex, dir));
            interval.max = Mathf.Max(interval.max, Vector2.Dot(vertex, dir));
        }
        return interval;
    }
    private static (float min, float max) ProjectCircle(Vector2 center, float radius, Vector2 dir) {
        float c = Vector2.Dot(center, dir);
        return (c - radius, c + radius);
    }

    private static float FindOverlap((float min, float max) proj, (float min, float max) other_proj, Edge edge) { //the projections are supposed to be along edge.normal; proj is a polygon and edge belongs to proj; other_proj may be a circle or a polygon
        float overlap;
        float extreme = Vector2.Dot(edge.normal, edge.start);

        if (Mathf.Abs(extreme - proj.min) < OVERLAP_EPSILON) overlap = other_proj.max - proj.min; //if the reference edge is the reference polygon's min, move the other shape's max to the reference polygon's min
        else if (Mathf.Abs(extreme - proj.min) > OVERLAP_EPSILON) overlap = proj.max - other_proj.min; //otherwise the reference edge is the reference polygon's max, so we must move the other shape's min to the reference polygon's max
        else throw new Exception("Not a convex polygon.");

        if (overlap < 0) return float.NaN;
        else return overlap;
    }


    /* DEBUGGING */

    public readonly override string ToString()
    {
        String s =
            "Reference: " + reference.Owner.Instance.name +
            "\nIncident: " + incident.Owner.Instance.name +
            "\nPenetration: " + penetration +
            "\nNormal: " + normal +
            "\nContact points: ";
        foreach (Vector2 p in contactPoints) s += p + " ";
        return s;
    }
}