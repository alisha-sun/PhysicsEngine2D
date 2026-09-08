using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Debug2
{
    private const float RATIO = .2f;
    public static void DrawArrow(Vector2 start, Vector2 direction, Color color, float duration = 0f)
    {
        Vector2 end = start + direction;
        Debug.DrawLine(start, end, color, duration);

        Debug.DrawLine(end, end - direction*RATIO + new Vector2(direction.y,-direction.x)*RATIO, color, duration);
        Debug.DrawLine(end, end - direction*RATIO + new Vector2(-direction.y, direction.x)*RATIO, color, duration);
    }

    public static void DrawX(Vector2 pos, Color color, float duration, float size = .2f) {
        Vector2 v = new Vector2(1, 1).normalized*size/2;
        Debug.DrawLine(pos - v, pos + v, color, duration);
        v = new Vector2(-v.y, v.x);
        Debug.DrawLine(pos - v, pos + v, color, duration);
    }

    public static void Draw(Edge edge, Color color, float duration) {
        Debug.DrawLine(edge.start,edge.end, color, duration);
    }

    internal static void Draw(Collision c, float duration) {
        foreach (Vector2 p in c.contactPoints) DrawX(p, Color.red, duration);
        DrawArrow(c.reference.Owner.Position, c.normal*c.penetration, Color.green, duration);
    }
}

public class Vectors2D {
    const float EPSILON = 1e-5f; 
    public static Vector2 SolveSystem(Vector2 point1, Vector2 dir1, Vector2 point2, Vector2 dir2)
    {
        if (dir1.normalized == dir2.normalized) return new Vector2(float.NaN, float.NaN);
        float t = Cross(point2 - point1, dir2) / Cross(dir1,dir2);
        return point1 + t * dir1;
    }

    public static Vector2 SolveSystem(float a, float b, float c, float d, Vector2 v) // [a,b ; c,d]x = v
    {
        float det = a * d - b * c;
        if (Mathf.Abs(det) < EPSILON)
        {
            return new Vector2(float.NaN, float.NaN);
        }
        else return new Vector2(d * v.x - b * v.y, -c * v.x + a * v.y) / det; // x = (1 / (a * d - b * c))[d,-b ; -c,a]v
    }

    public static float Cross(Vector2 a, Vector2 b) //same as Dot(a rotated 90 degrees CCW, b) or Dot(a, b rotated 90 degrees CW)
    {
        return a.x * b.y - a.y * b.x;
    }

    public static Vector2 MinByDot(Vector2 dir, params Vector2[] vectors)
    {
        if (vectors.Length == 0) return new Vector2(float.NaN, float.NaN);

        Vector2 minVector = vectors[0];
        float minDot = Vector2.Dot(vectors[0], dir);
        for (int i = 1; i < vectors.Length; i++)
        {
            float dot = Vector2.Dot(vectors[i], dir);
            if (dot < minDot)
            {
                minDot = dot;
                minVector = vectors[i];
            }
        }
        return minVector;
    }
    public static Vector2 MaxByDot(Vector2 dir, params Vector2[] vectors)
    {
        if (vectors.Length == 0) return new Vector2(float.NaN, float.NaN);

        Vector2 maxVector = vectors[0];
        float maxDot = Vector2.Dot(vectors[0], dir);
        for (int i = 1; i < vectors.Length; i++)
        {
            float dot = Vector2.Dot(vectors[i], dir);
            if (dot > maxDot)
            {
                maxDot = dot;
                maxVector = vectors[i];
            }
        }
        return maxVector;
    }
    public static Vector2 FindClosestPoint(Vector2[] points, Vector2 pos)
    {
        Vector2 closest = points[0];
        float minDistSq = (points[0] - pos).sqrMagnitude;
        for (int i = 0; i < points.Length; i++)
        {
            float distSq = (points[i] - pos).sqrMagnitude;

            if (distSq < minDistSq)
            {
                minDistSq = distSq;
                closest = points[i];
            }
        }
        return closest;
    }

    public static bool IsOverlapping((Vector2 min, Vector2 max) a, (Vector2 min, Vector2 max) b)
    {
        return IsOverlapping((a.min.x, a.max.x), (b.min.x, b.max.x)) && IsOverlapping((a.min.y, a.max.y), (b.min.y, b.max.y));
    }
    public static bool IsOverlapping((float min, float max) a, (float min, float max) b) {
        return Mathf.Max(a.min, b.min) - Mathf.Min(a.max, b.max) <= 0;
    }

    public static bool IsFinite(Vector2 v) { return float.IsFinite(v.x) && float.IsFinite(v.y); }
}

public struct Edge
{
    public Vector2 start { get; }
    public Vector2 end { get; }
    public Vector2 tangent { get; }
    public Vector2 normal { get; private set; }

    public Edge(Vector2 start, Vector2 end) {
        this.start = start;
        this.end = end;

        this.tangent = end - start;
        this.normal = new Vector2(-tangent.y, tangent.x).normalized; //rotated 90 degrees ccw
    }

    internal Edge(Vector2 start, Vector2 end, Vector2 normal)
    {
        this.start = start;
        this.end = end;

        this.tangent = end - start;
        this.normal = normal;
    }

    public Edge(float f) {
        this.start = new Vector2(f,f);
        this.end = new Vector2(f, f);
        this.tangent = new Vector2(f, f);
        this.normal = new Vector2(f, f);
    }
}
