using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/*public class BoxCollider : PolygonCollider
{
    public PhysicsObject Owner { get; }
    private (Vector2 min, Vector2 max) localBounds;

    public BoxCollider(PhysicsObject owner, UnityEngine.BoxCollider2D collider)
    {
        Owner = owner;

        Vector2 halfSize = collider.size * 0.5f;
        localBounds.min = collider.offset + new Vector2(-halfSize.x, -halfSize.y);
        localBounds.max = collider.offset + new Vector2(halfSize.x, halfSize.y);
    }
    public override (Vector2 min, Vector2 max) GetBoundingBox()
    {
        Vector2[] path = { new Vector2(localBounds.min.x, localBounds.max.y), localBounds.max, new Vector2(localBounds.max.x, localBounds.min.y), localBounds.min, };

        Vector2 first = Owner.LocalToWorldPoint(path[0]);

        Vector2 min = first;
        Vector2 max = first;

        for (int i = 1; i < 4; i++)
        {
            path[i] = Owner.LocalToWorldPoint(path[i]);

            if (path[i].x < min.x) min.x = path[i].x;
            if (path[i].y < min.y) min.y = path[i].y;
            if (path[i].x > max.x) max.x = path[i].x;
            if (path[i].y > max.y) max.y = path[i].y;
        }

        return (min, max);
    }

    public override List<(Vector2 a, Vector2 b)> GetPath()
    {
        List<(Vector2 a, Vector2 b)> edges = new List<(Vector2 a, Vector2 b)>();
        edges.Add((new Vector2(localBounds.min.x, localBounds.max.y), localBounds.max));
        edges.Add((localBounds.max, new Vector2(localBounds.max.x, localBounds.min.y)));
        edges.Add((new Vector2(localBounds.max.x, localBounds.min.y), localBounds.min));
        edges.Add((localBounds.min, new Vector2(localBounds.min.x, localBounds.max.y)));
        return edges;
    }
}
*/