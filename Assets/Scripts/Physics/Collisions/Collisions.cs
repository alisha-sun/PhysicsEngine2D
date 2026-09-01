using System.Collections;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
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

    public void Resolve() {
        float impulse0 = 0;
        float impulse1 = 0;
        float e = Restitution();

        if (contactPoints == null || contactPoints.Length == 0) return;
        else if (contactPoints.Length == 1) {
            Edge centerToContact_reference = new Edge(reference.Owner.Position, contactPoints[0]);
            Edge centerToContact_incident = new Edge(incident.Owner.Position, contactPoints[0]);

            float rv_n = Vector2.Dot(
                  incident.Owner.GetVelocityAtPoint(contactPoints[0], true) - reference.Owner.GetVelocityAtPoint(contactPoints[0], true)
                , normal); //relative velocity along the collision normal (negative if the space between the two objects is decreasing)

            if (rv_n < 0) impulse0 = (-(1 + e) * rv_n) /
                ( (1 / reference.Owner.Mass) + (1 / incident.Owner.Mass) +
                (Mathf.Pow(Vectors2D.Cross(centerToContact_reference.tangent, normal), 2) / reference.Owner.Inertia) +
                (Mathf.Pow(Vectors2D.Cross(centerToContact_incident.tangent, normal), 2) / incident.Owner.Inertia)   );
        }
        else
        {

            Edge centerToContact0_reference = new Edge(reference.Owner.Position, contactPoints[0]);
            Edge centerToContact0_incident = new Edge(incident.Owner.Position, contactPoints[0]);
            Edge centerToContact1_reference = new Edge(reference.Owner.Position, contactPoints[1]);
            Edge centerToContact1_incident = new Edge(incident.Owner.Position, contactPoints[1]);

            float rv_n0 = Vector2.Dot(
                  incident.Owner.GetVelocityAtPoint(contactPoints[0], true) - reference.Owner.GetVelocityAtPoint(contactPoints[0], true)
                , normal); //relative velocity along the collision normal at the first contact point

            float rv_n1 = Vector2.Dot(
                  incident.Owner.GetVelocityAtPoint(contactPoints[1], true) - reference.Owner.GetVelocityAtPoint(contactPoints[1], true)
                , normal); //relative velocity along the collision normal at the second contact point

            Vector2 impulseMagnitudes;
            if (rv_n0 >= 0 && rv_n1 >= 0) return;
            else if (rv_n0 >= 0)
            {
                impulseMagnitudes = Vectors2D.SolveSystem(CalculateContactVelocityCoefficient(0, 0), CalculateContactVelocityCoefficient(0, 1),
                                                          CalculateContactVelocityCoefficient(1, 0), CalculateContactVelocityCoefficient(1, 1),
                                                          new Vector2(0, -(1 + e) * rv_n1)                                                   );
            }
            else if (rv_n1 >= 0)
            {
                impulseMagnitudes = Vectors2D.SolveSystem(CalculateContactVelocityCoefficient(0, 0), CalculateContactVelocityCoefficient(0, 1),
                                                          CalculateContactVelocityCoefficient(1, 0), CalculateContactVelocityCoefficient(1, 1),
                                                          new Vector2(-(1 + e) * rv_n0, 0)                                                   );
            }
            else {
                impulseMagnitudes = Vectors2D.SolveSystem(CalculateContactVelocityCoefficient(0, 0), CalculateContactVelocityCoefficient(0, 1),
                                                          CalculateContactVelocityCoefficient(1, 0), CalculateContactVelocityCoefficient(1, 1),
                                                          new Vector2(-(1 + e) * rv_n0, -(1 + e) * rv_n1)                                    );
            }

            if (rv_n0 < 0) impulse0 = impulseMagnitudes.x;
            if (rv_n1 < 0) impulse1 = impulseMagnitudes.y;
        }

        float ratio = PercentMass_Reference();
        if (reference.Owner is DynamicObject d1)
        {
            d1.ApplyImpulse(-impulse0 * normal, d1.WorldToLocal_Point(contactPoints[0]));
            if (contactPoints.Length > 1) d1.ApplyImpulse(-impulse1 * normal, d1.WorldToLocal_Point(contactPoints[1]));
            d1.SetPosition(d1.Position - penetration * normal * (1 - ratio));
        }

        if (incident.Owner is DynamicObject d2)
        {
            d2.ApplyImpulse(impulse0 * normal, d2.WorldToLocal_Point(contactPoints[0]));
            if (contactPoints.Length == 2) d2.ApplyImpulse(impulse1 * normal, d2.WorldToLocal_Point(contactPoints[1]));
            d2.SetPosition(d2.Position + penetration * normal * ratio);
        }
    }

    private float PercentMass_Reference()
    {
        if (reference.Owner.Mass == float.PositiveInfinity)
        {
            if (incident.Owner.Mass == float.PositiveInfinity) return .5f;
            else return 1f;
        }
        else if (incident.Owner.Mass == float.PositiveInfinity) return 0;
        else return (reference.Owner.Mass / (reference.Owner.Mass + incident.Owner.Mass));
    }

    private float Restitution()
    {
        return 1; //consider physics materials later
    }

    private float CalculateContactVelocityCoefficient(int v_index, int j_index) { //i worked through the algebra myself :/
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