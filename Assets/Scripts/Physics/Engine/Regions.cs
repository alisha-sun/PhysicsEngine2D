using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class RegionManager
{
    public static float SIZE;
    private const float EPSILON = 0.000001f;

    private Dictionary<Vector2Int, HashSet<PhysicsObject>> regions = new Dictionary<Vector2Int, HashSet<PhysicsObject>>();

    public void UpdateRegions(DynamicObject obj) { //update regions based on new bounding box
        RemoveFromRegions(obj);
        obj.RecalculateBoundingRegions();
        AddToRegions(obj);
    }

    public void AddToRegions(PhysicsObject obj) {
        if (obj is DynamicObject dynamicObject) AddToRegions(dynamicObject);
        else if (obj is StaticObject staticObject) AddToRegions(staticObject);
    }
    public void RemoveFromRegions(PhysicsObject obj)
    {
        if (obj is DynamicObject dynamicObject) RemoveFromRegions(dynamicObject);
        else if (obj is StaticObject staticObject) RemoveFromRegions(staticObject);
    }

    public List<PhysicsObject> GetPotentialCollisions(DynamicObject obj)
    {
        List<PhysicsObject> candidates = new List<PhysicsObject>();
        for (int i = obj.BoundingRegions.min.x; i <= obj.BoundingRegions.max.x; i++) 
        {
            for (int j = obj.BoundingRegions.min.y; j <= obj.BoundingRegions.max.y; j++) 
            {
                if (regions.TryGetValue(new Vector2Int(i,j), out HashSet<PhysicsObject> region))
                {
                    foreach (PhysicsObject other_obj in region)
                    {
                        if (other_obj != obj && !candidates.Contains(other_obj)) {
                            candidates.Add(other_obj);
                            Debug.Log(other_obj.Instance);
                        };
                    }
                }
            }
        }

        return candidates;
    }

    public static (Vector2Int min, Vector2Int max) GetBoundingRegions((Vector2 min, Vector2 max) bounds) { //bounds: bottom-left (min), top-right (max)
        (Vector2Int min, Vector2Int max) boundingRegions = (new Vector2Int(0,0), new Vector2Int(0,0));

        boundingRegions.min.x = Mathf.FloorToInt((bounds.min.x - EPSILON) / SIZE);
        boundingRegions.min.y = Mathf.FloorToInt((bounds.min.y - EPSILON) / SIZE);

        boundingRegions.max.x = Mathf.FloorToInt((bounds.max.x + EPSILON) / SIZE);
        boundingRegions.max.y = Mathf.FloorToInt((bounds.max.y + EPSILON) / SIZE);

        return boundingRegions;
    }


    private void AddToRegions(DynamicObject obj)
    {
        for (int x = obj.BoundingRegions.min.x; x <= obj.BoundingRegions.max.x; x++)
        {
            for (int y = obj.BoundingRegions.min.y; y <= obj.BoundingRegions.max.y; y++)
            {
                Vector2Int key = new Vector2Int(x, y);
                if (!regions.TryGetValue(key, out HashSet<PhysicsObject> region))
                {
                    region = new HashSet<PhysicsObject>();
                    regions[key] = region;
                }
                region.Add(obj);
            }
        }
    }
    private void RemoveFromRegions(DynamicObject obj)
    {
        for (int x = obj.BoundingRegions.min.x; x <= obj.BoundingRegions.max.x; x++)
        {
            for (int y = obj.BoundingRegions.min.y; y <= obj.BoundingRegions.max.y; y++)
            {
                Vector2Int key = new Vector2Int(x, y);
                regions[key].Remove(obj);
                if (regions[key].Count == 0) regions.Remove(key);
            }
        }
    }

    private void AddToRegions(StaticObject obj)
    {
        foreach (Vector2Int key in obj.Regions)
        {
            if (!regions.TryGetValue(key, out HashSet<PhysicsObject> region))
            {
                region = new HashSet<PhysicsObject>();
                regions[key] = region;
            }

            region.Add(obj);
        }
    }
    private void RemoveFromRegions(StaticObject obj)
    {
        foreach (Vector2Int key in obj.Regions)
        {
            regions[key].Remove(obj);
            if (regions[key].Count == 0) regions.Remove(key);
        }
    }
}
