using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class StaticObject : PhysicsObject //should never need to be sent over the network
{
    private GameObject _instance;                               public override GameObject Instance => _instance;
    private IPhysicsCollider[] _colliders;                      public override IReadOnlyList<IPhysicsCollider> Colliders => _colliders;

    private Vector2 _position;                                  public override Vector2 Position => _position;
    private float _rotation;                                    public override float Rotation => _rotation; // radians
    private Vector2 _scale;                                     public override Vector2 Scale => _scale; // radians

    private Vector2Int[] _regions;                              public IReadOnlyList<Vector2Int> Regions => _regions;

    public StaticObject(GameObject instance) {
        _position = instance.transform.position;
        _rotation = instance.transform.eulerAngles.z * Mathf.Deg2Rad;
        _scale = instance.transform.localScale;
        _instance = instance;
        _colliders = PhysicsObjectComponent.GetColliders(this).ToArray();
        _regions = GetRegions();

        PhysicsEngine.instance.gameState.Register(this);
    }

    public Vector2Int[] GetRegions() { //temporary; will calculate exact regions later
        Vector2 min = new Vector2(float.PositiveInfinity, float.PositiveInfinity);
        Vector2 max = new Vector2(float.NegativeInfinity, float.NegativeInfinity);

        foreach (Renderer renderer in _instance.GetComponentsInChildren<Renderer>())
        {
            min.x = Mathf.Min(min.x, renderer.bounds.min.x);
            min.y = Mathf.Min(min.y, renderer.bounds.min.y);

            max.x = Mathf.Max(max.x, renderer.bounds.max.x);
            max.y = Mathf.Max(max.y, renderer.bounds.max.y);
        }

        foreach (IPhysicsCollider collider in _colliders)
        {
            (Vector2 min, Vector2 max) boundingBox = collider.GetBoundingBox();
            min.x = Mathf.Min(min.x, boundingBox.min.x);
            min.y = Mathf.Min(min.y, boundingBox.min.y);

            max.x = Mathf.Max(max.x, boundingBox.max.x);
            max.y = Mathf.Max(max.y, boundingBox.max.y);
        }

        (Vector2Int min, Vector2Int max) boundingRegions = RegionManager.GetBoundingRegions((min, max));

        Vector2Int[] arr = new Vector2Int[(boundingRegions.max.y-boundingRegions.min.y+1) * (boundingRegions.max.x-boundingRegions.min.x+1)];
        int i = 0;
        for (int x = boundingRegions.min.x; x <= boundingRegions.max.x; x++) {
            for (int y = boundingRegions.min.y; y <= boundingRegions.max.y; y++)
            {
                arr[i++] = new Vector2Int(x,y);
            }
        }

        return arr;
    }
}
