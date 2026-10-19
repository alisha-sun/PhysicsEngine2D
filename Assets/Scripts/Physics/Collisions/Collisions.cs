using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using static UnityEngine.GraphicsBuffer;
public interface IPhysicsCollider
{
    public PhysicsObject Owner { get; }
    public (Vector2 min, Vector2 max) BoundingBox { get; }
    internal void PositionUpdate(Vector2 deltaP); //cheap updates to cached data after a collision
    internal void FullUpdate(); //updates to cached data after a transformation
}
internal struct Collision
{
    private const float OVERLAP_EPSILON = 1e-5f;

    public IPhysicsCollider reference;
    public IPhysicsCollider incident;

    public float penetration;
    public Vector2[] contactPoints;
    public Vector2 normal;

    internal static void DoCollisions(DynamicObject obj, GameState gameState)
    {
        Dictionary<PhysicsObject, Vector2> done = new Dictionary<PhysicsObject, Vector2>(); // <object, positional correction>
        done.Add(obj, Vector2.zero);

        foreach (Vector2Int key in obj.Regions) {
            IReadOnlyCollection<PhysicsObject> region = gameState.regions.TryGetValue(key);
            foreach (PhysicsObject other_obj in region) {
                if (!done.ContainsKey(other_obj)) {
                    done.Add(other_obj, Vector2.zero);
                    foreach (IPhysicsCollider collider in obj.Colliders)
                    {
                        foreach (IPhysicsCollider other_collider in other_obj.Colliders)
                        {
                            Collision collision = Collision.Check(collider, other_collider);

                            if (collision.contactPoints != null)
                            {
                                (Vector2 deltaP_reference, Vector2 deltaP_incident) = collision.Resolve();

                                if (collision.reference.Owner == obj)
                                {
                                    done[obj] += deltaP_reference;
                                    done[other_obj] += deltaP_incident;
                                }
                                else
                                {
                                    done[obj] += deltaP_incident;
                                    done[other_obj] += deltaP_reference;
                                }
                            }
                        }
                    }
                }
            }
        }

        foreach (PhysicsObject key in done.Keys) {
            if (key is DynamicObject d) gameState.regions.UpdateRegions(d, done[key]);
        }
    }

    internal static void DoCollisions(StaticObject obj, GameState gameState)
    {
        Dictionary<DynamicObject, Vector2> done = new Dictionary<DynamicObject, Vector2>(); // <object, positional correction>

        foreach (Vector2Int key in obj.Regions)
        {
            IReadOnlyCollection<PhysicsObject> region = gameState.regions.TryGetValue(key);
            foreach (PhysicsObject other_obj in region)
            {
                if (other_obj is DynamicObject d && !done.ContainsKey(d))
                {
                    done.Add(d, Vector2.zero);
                    foreach (IPhysicsCollider collider in obj.Colliders)
                    {
                        foreach (IPhysicsCollider other_collider in other_obj.Colliders)
                        {
                            Collision collision = Collision.Check(collider, other_collider);

                            if (collision.contactPoints != null)
                            {
                                (Vector2 deltaP_reference, Vector2 deltaP_incident) = collision.Resolve();

                                if (collision.reference.Owner == d) done[d] += deltaP_reference;
                                else done[d] += deltaP_incident;
                            }
                        }
                    }
                }
            }
        }

        foreach (DynamicObject key in done.Keys) gameState.regions.UpdateRegions(key, done[key]);
    }

    internal static Collision Check(IPhysicsCollider a, IPhysicsCollider b)
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

    internal readonly (Vector2, Vector2) Resolve() {
        if (contactPoints == null || contactPoints.Length == 0) return (Vector2.zero, Vector2.zero);

        float e = Restitution();

        int contactCount = contactPoints.Length;

        Vector2 point0 = contactPoints[0];
        float impulse0 = 0;
        Vector2 point1 = default;
        float impulse1 = 0;

        if (contactCount == 2)
        {
            point1 = contactPoints[1];

            float rv_n0 = Vector2.Dot(
                    incident.Owner.GetVelocityAtPoint(point0, true) - reference.Owner.GetVelocityAtPoint(point0, true)
                , normal); //relative velocity along the collision normal at the first contact point

            float rv_n1 = Vector2.Dot(
                    incident.Owner.GetVelocityAtPoint(point1, true) - reference.Owner.GetVelocityAtPoint(point1, true)
                , normal); //relative velocity along the collision normal at the second contact point

            Vector2 impulseMagnitudes;
            if (rv_n0 > 0 && rv_n1 > 0) return (Vector2.zero, Vector2.zero);
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
                /*Debug2.Draw(this, Time.fixedDeltaTime);
                Debug.Log(this);
                Debug.LogWarning("Unable to solve for impulse in double contact point collision between " + reference.Owner.Instance.name + " and " + incident.Owner.Instance.name);*/
                point0 = (point0 + point1) / 2;
                contactCount = 1;
            }
            else if (impulseMagnitudes.x < 0)
            {
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

        if (contactCount == 1)
        {
            float rv_n = Vector2.Dot(
                    incident.Owner.GetVelocityAtPoint(point0, true) - reference.Owner.GetVelocityAtPoint(point0, true)
                , normal); //relative velocity along the collision normal (negative if the space between the two objects is decreasing)

            if (rv_n > 0) return (Vector2.zero, Vector2.zero);

            float j = (-(1 + e) * rv_n) /
                ((1 / reference.Owner.Mass) + (1 / incident.Owner.Mass) +
                (Mathf.Pow(Vectors2D.Cross(point0 - reference.Owner.Position, normal), 2) / reference.Owner.Inertia) +
                (Mathf.Pow(Vectors2D.Cross(point0 - incident.Owner.Position, normal), 2) / incident.Owner.Inertia));

            if (!float.IsFinite(j) || j < 0)
            {
                Debug2.Draw(this, Time.fixedDeltaTime);
                Debug.Log(this);
                Debug.LogError("Unable to solve for impulse in single contact point collision between " + reference.Owner.Instance.name + " and " + incident.Owner.Instance.name);
            }
            else impulse0 = j;
        }


        Vector2 deltaP_reference = Vector2.zero;
        Vector2 deltaP_incident = Vector2.zero;
        float ratio = PercentMass_Reference();
        if (reference.Owner is DynamicObject d1)
        {
            d1.ApplyImpulse(-impulse0 * normal, d1.WorldToLocal_Point(point0));
            if (contactCount == 2) d1.ApplyImpulse(-impulse1 * normal, d1.WorldToLocal_Point(point1));

            deltaP_reference = -penetration * (1 - ratio) * normal;

            d1.SetPosition_Internal(d1.Position + deltaP_reference);
            d1.OnTranslation(deltaP_reference, false);
        }

        if (incident.Owner is DynamicObject d2)
        {
            d2.ApplyImpulse(impulse0 * normal, d2.WorldToLocal_Point(point0));
            if (contactCount == 2) d2.ApplyImpulse(impulse1 * normal, d2.WorldToLocal_Point(point1));

            deltaP_incident = penetration * ratio * normal;

            d2.SetPosition_Internal(d2.Position + deltaP_incident);
            d2.OnTranslation(deltaP_incident, false);
        }

        Debug2.DrawX(point0, Color.red, Time.fixedDeltaTime);
        Debug2.DrawArrow(point0, normal * impulse0, Color.green, Time.fixedDeltaTime);
        if (contactCount == 2) {
            Debug2.DrawX(point1, Color.red, Time.fixedDeltaTime);
            Debug2.DrawArrow(point1, normal * impulse1, Color.green, Time.fixedDeltaTime);
        }

        return (deltaP_reference, deltaP_incident);
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
        return 1; //consider physics materials later
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

        Edge referenceEdge = FindMinimumOverlapEdge(polyA, polyB);

        if (!float.IsNaN(penetration)) //no separating axis found from polyA's edge normals
        {
            Edge e = FindMinimumOverlapEdge(polyB, polyA);
            if (!float.IsNaN(e.start.x)) referenceEdge = e;
        }
        else return;

        if (!float.IsNaN(penetration)) //there is no separating axis
        {
            if (incident == polyA) contactPoints = GetContactPoints(referenceEdge, FindIncidentEdge(polyA));
            else contactPoints = GetContactPoints(referenceEdge, FindIncidentEdge(polyB));
        }
    }
    private Edge FindMinimumOverlapEdge(PolygonCollider poly, PolygonCollider other_poly) {
        int index = -1;
        Vector2 edgeNormal = poly.edgeNormals[poly.path_world.Length - 1];
        //Debug2.DrawArrow(poly.path_world[poly.path_world.Length - 1], edgeNormal, Color.blue, Time.fixedDeltaTime);

        /*(float min, float max) proj = (
            Vector2.Dot(poly.GetOpposite(poly.path_world.Length - 1), edgeNormal),
            Vector2.Dot(poly.path_world[poly.path_world.Length - 1], edgeNormal) //normal is facing outward so this edge must be the max
        );
        (float min, float max) other_proj = (
            other_poly.GetMinDot_1(edgeNormal),
            other_poly.GetMaxDot_1(edgeNormal)
        );*/
        float p = Vector2.Dot(poly.path_world[poly.path_world.Length - 1], edgeNormal) - other_poly.GetMinDot_1(edgeNormal);
        if (p < 0) p = float.NaN;

        if (float.IsNaN(p))
        { //found separating axis
            normal = new Vector2(float.NaN, float.NaN);
            penetration = float.NaN;
            return new Edge(float.NaN);
        }
        else if (p < penetration)
        {
            normal = edgeNormal;
            penetration = p;

            index = poly.path_world.Length - 1;
        }

        for (int i = 0; i <= poly.path_world.Length - 2; i++) //looping through all the edges (CW)
        {
            edgeNormal = poly.edgeNormals[i];
            //Debug2.DrawArrow(poly.path_world[i], edgeNormal, Color.magenta, Time.fixedDeltaTime);

            p = FindProjectionOverlap(poly, other_poly, i, edgeNormal);

            if (float.IsNaN(p)) { //found separating axis
                normal = new Vector2(float.NaN, float.NaN);
                penetration = float.NaN; // <---
                return new Edge(float.NaN);
            }
            else if (p < penetration) {
                normal = edgeNormal;
                penetration = p;

                index = i;
            }
        }

        if (index != -1)
        {
            reference = poly;
            incident = other_poly;
            return new Edge(poly.path_world[index], poly.path_world[(index + 1) % poly.path_world.Length], normal);
        }
        else return new Edge(float.NaN);
    }
    private readonly Edge FindIncidentEdge(PolygonCollider poly) {
        int index = poly.path_world.Length - 1;
        float minDot = Vector2.Dot(poly.edgeNormals[poly.path_world.Length - 1], normal);

        for (int i = 0; i <= poly.path_world.Length - 2; i++)
        {
            float dot = Vector2.Dot(poly.edgeNormals[i], normal);
            if (dot < minDot)
            {
                minDot = dot;
                index = i;
            }
        }
        return new Edge(poly.path_world[index], poly.path_world[(index + 1)%poly.path_world.Length], poly.edgeNormals[index]);
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
            Vectors2D.MinByDot(incidentEdge.tangent, end, incidentEdge.end),
            incidentEdge.normal
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

        Vector2[] path = polyA.path_world;

        FindMinimumOverlapEdge(polyA, circleB);
        if (float.IsNaN(penetration)) return; //found separating axis

        Vector2 p = CheckClosestVertex(polyA, circleB);
        if (float.IsNaN(penetration)) return; //found separating axis

        if (reference == circleB) contactPoints = new Vector2[] { p };
        else contactPoints = new Vector2[] { circleB.Center - circleB.Radius*normal };
        //Debug2.DrawX(contactpoint0, Color.cyan, Time.fixedDeltaTime, .3f);
    }
    private void FindMinimumOverlapEdge(PolygonCollider poly, CircleCollider circle)
    {
        for (int i = 0; i < poly.path_world.Length; i++)
        {
            Vector2 edgeNormal = poly.edgeNormals[i];

            //Debug2.DrawArrow(edge.start + edge.tangent * .5f, edge.normal, Color.magenta, Time.fixedDeltaTime);

            float p = FindProjectionOverlap(poly, circle, i, edgeNormal);

            if (float.IsNaN(p)) { //found separating axis
                normal = new Vector2(float.NaN, float.NaN);
                penetration = float.NaN; // <---
            }
            else if (p < penetration) {
                normal = edgeNormal;
                penetration = p;
            }
        }
    }
    private Vector2 CheckClosestVertex(PolygonCollider poly, CircleCollider circle)
    {
        Vector2 closest = Vectors2D.FindClosestPoint(poly.path_world, circle.Center);
        Vector2 centerToClosest = (closest - circle.Center).normalized;
        float p = -Vector2.Dot(closest - (circle.Center + centerToClosest * circle.Radius), // vector from circle edge to closest vertex
                               centerToClosest);                                            // direction from center to closest vertex

        //Debug.DrawLine(closest, closest + centerToClosest.tangent.normalized * p, Color.yellow, Time.fixedDeltaTime);

        if (p < 0) //vertex is not touching the circle
        {
            if (IsGapBetweenProjections(poly.path_world, circle.Center, circle.Radius, centerToClosest)) { // centerToClosest.tangent is orthogonal to the "edge" of the circle
                normal = new Vector2(float.NaN, float.NaN);
                penetration = float.NaN; //found seperating axis
            }
            return new Vector2(float.NaN, float.NaN);
        }
        else if (p < penetration) //vertex is touching the circle and the penetration is less than previously
        {
            reference = circle;
            incident = poly;
            penetration = p;
            normal = centerToClosest;
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

    private static float FindProjectionOverlap(PolygonCollider poly, PolygonCollider other_poly, int edgeStartIndex, Vector2 axis)
    {
        /*(float min, float max) proj = (
            Vector2.Dot(poly.GetOpposite(edgeStartIndex), axis),
            Vector2.Dot(poly.path_world[edgeStartIndex], axis) //normal is facing outward so this edge must be the max
        );
        (float min, float max) other_proj = (
            other_poly.GetMinDot_2(axis),
            other_poly.GetMaxDot_2(axis)
        );*/

        float overlap = Vector2.Dot(poly.path_world[edgeStartIndex], axis) - other_poly.GetMinDot_2(axis);
        if (overlap < 0) return float.NaN;
        else return overlap;
    }
    private static float FindProjectionOverlap(PolygonCollider poly, CircleCollider circle, int edgeStartIndex, Vector2 axis)
    {
        /*(float min, float max) proj = (
            Vector2.Dot(poly.GetOpposite(edgeStartIndex), axis),
            Vector2.Dot(poly.path_world[edgeStartIndex], axis)
        );
        (float min, float max) other_proj = ProjectCircle(circle.Center, circle.Radius, axis);*/

        float overlap = Vector2.Dot(poly.path_world[edgeStartIndex], axis) - (Vector2.Dot(circle.Center, axis) - circle.Radius);
        if (overlap < 0) return float.NaN;
        else return overlap;
    }
    private static bool IsGapBetweenProjections(Vector2[] path, Vector2 center, float radius, Vector2 normal) {
        (float min, float max) proj = ProjectPolygon(path, normal);
        (float min, float max) other_proj = ProjectCircle(center, radius, normal);

        return Mathf.Max(proj.min, other_proj.min) - Mathf.Min(proj.max, other_proj.max) > 0;
    }

    private static (float min, float max) ProjectPolygon(Vector2[] path, Vector2 dir)
    {
        (float min, float max) interval = (float.PositiveInfinity, float.NegativeInfinity);
        for (int i = 0; i < path.Length; i++)
        {
            float projection = Vector2.Dot(path[i], dir);
            interval.min = Mathf.Min(interval.min, projection);
            interval.max = Mathf.Max(interval.max, projection);
        }
        return interval;
    }
    private static (float min, float max) ProjectCircle(Vector2 center, float radius, Vector2 dir) {
        float c = Vector2.Dot(center, dir);
        return (c - radius, c + radius);
    }

    /*private static float FindOverlap((float min, float max) proj, (float min, float max) other_proj) {
        float overlap = proj.max - other_proj.min;
        if (overlap < 0 || proj.min - other_proj.max > 0) return float.NaN;
        else return proj.max - other_proj.min;
        // As previously established, the reference edge is the reference polygon's projection's max
        // Therefore we must move the other shape's min to the reference polygon's max
    }*/


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