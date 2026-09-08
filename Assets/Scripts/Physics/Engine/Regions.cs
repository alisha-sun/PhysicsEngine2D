using System;
using System.Collections;
using System.Collections.Generic;
using Unity.Profiling;
using UnityEngine;

internal class RegionManager
{
    static readonly ProfilerMarker getMarker = new("Get");

    public static float SIZE;
    private const float EPSILON = 0.000001f;

    private Dictionary<Vector2Int, HashSet<PhysicsObject>> regions = new Dictionary<Vector2Int, HashSet<PhysicsObject>>();

    /* PUBLIC INTERFACE */

    public IReadOnlyCollection<PhysicsObject> TryGetValue(Vector2Int key) {
        regions.TryGetValue(key, out HashSet<PhysicsObject> region);
        return region;
    }
    
    public void UpdateRegions(DynamicObject obj) { //update regions based on new bounding box
        RemoveFromRegions(obj);
        obj.RecalculateBoundingBox();
        AddToRegions(obj);
    }
    internal void UpdateRegions(DynamicObject obj, Vector2 deltaP) //update regions based on translation
    {
        IEnumerable<Vector2Int> oldBounds = obj.Regions;
        if (obj.TranslateBoundingBox(deltaP))
        {
            RemoveFromRegions(obj, oldBounds);
            AddToRegions(obj);
        }
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

    public HashSet<PhysicsObject> GetPotentialCollisions(DynamicObject obj)
    {
        using (getMarker.Auto())
        {
            var candidates = new HashSet<PhysicsObject>();

            foreach (Vector2Int key in obj.Regions)
            {
                if (regions.TryGetValue(key, out HashSet<PhysicsObject> region))
                {
                    foreach (PhysicsObject other_obj in region)
                    {
                        if (other_obj != obj && !candidates.Contains(obj)) candidates.Add(other_obj);
                    }
                }
            }

            return candidates;
        }
    }
    public HashSet<DynamicObject> GetPotentialCollisions(StaticObject obj)
    {
        var candidates = new HashSet<DynamicObject>();

        foreach (Vector2Int key in obj.Regions) {
            if (regions.TryGetValue(key, out HashSet<PhysicsObject> region))
            {
                foreach (PhysicsObject other_obj in region)
                {
                    if (other_obj != obj)
                    {
                        if (other_obj is DynamicObject d && !candidates.Contains(d)) candidates.Add(d);
                    }
                }
            }
        }
        
        return candidates;
    }

    public static BoundingRegions GetBoundingRegions((Vector2 min, Vector2 max) bounds) { //bounds: bottom-left (min), top-right (max)
        BoundingRegions boundingRegions = new BoundingRegions();

        boundingRegions.min.x = Mathf.FloorToInt((bounds.min.x - EPSILON) / SIZE);
        boundingRegions.min.y = Mathf.FloorToInt((bounds.min.y - EPSILON) / SIZE);

        boundingRegions.max.x = Mathf.FloorToInt((bounds.max.x + EPSILON) / SIZE);
        boundingRegions.max.y = Mathf.FloorToInt((bounds.max.y + EPSILON) / SIZE);

        return boundingRegions;
    }

    /* PRIVATE */

    private void AddToRegions(DynamicObject obj)
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
    private void RemoveFromRegions(DynamicObject obj)
    {
        foreach (Vector2Int key in obj.Regions) {
            regions[key].Remove(obj);
            if (regions[key].Count == 0) regions.Remove(key);
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
    private void RemoveFromRegions(DynamicObject obj, IEnumerable<Vector2Int> targetRegions) 
    {
        foreach (Vector2Int key in targetRegions)
        {
            regions[key].Remove(obj); //not safe but its private so whatever
            if (regions[key].Count == 0) regions.Remove(key);
        }
    }
}

internal class BoundingRegions : IEnumerable<Vector2Int> {
    public Vector2Int max;
    public Vector2Int min;

    public BoundingRegions() { 
        this.min = new Vector2Int(0, 0);
        this.max = new Vector2Int(-1, -1); //invalid on purpose
    }
    public BoundingRegions(Vector2Int min, Vector2Int max) {
        this.min = min;
        this.max = max;
    }

    public bool IsOverlapping(BoundingRegions other) {
        if (!this.IsValid() || !other.IsValid()) return false;
        return Vectors2D.IsOverlapping((this.min.x, this.max.x), (other.min.x, other.max.x)) &&
               Vectors2D.IsOverlapping((this.min.y, this.max.y), (other.min.y, other.max.y));
    }

    public bool IsValid() {
        return min.x <= max.x && min.y <= max.y;
    }

    public IEnumerator<Vector2Int> GetEnumerator()
    {
        for (int y = min.y; y <= max.y; y++)
        {
            for (int x = min.x; x <= max.x; x++)
            {
                yield return new Vector2Int(x, y);
            }
        }
    }
    IEnumerator IEnumerable.GetEnumerator()
    {
        return GetEnumerator();
    }
}
