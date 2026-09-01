using System.Collections;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using UnityEngine;

public class PhysicsObjectComponent : MonoBehaviour
{
    [SerializeField] private bool isStatic = false;

    [SerializeField] private Vector2 velocity = new Vector2(0f,0f);
    [SerializeField] private float maxVelocityX = float.PositiveInfinity;
    [SerializeField] private float maxVelocityY = float.PositiveInfinity;
    [SerializeField] private float drag = 0f;
    [SerializeField] private float mass = 1f;

    [SerializeField] private float angularVelocity = 0f;
    [SerializeField] private float maxAngularVelocity = float.PositiveInfinity;
    [SerializeField] private float angularDrag = 0f;
    [SerializeField] private float inertia = 1f;

    [SerializeField] private float gravityScale = 1f;
    [SerializeField] private float terminalVelocity = float.PositiveInfinity;

    [SerializeField] private bool freezeX = false;
    [SerializeField] private bool freezeY = false;
    [SerializeField] private bool freezeRotation = false;
    [SerializeField] private Vector2 anchor = new Vector2(float.NaN, float.NaN);


    public PhysicsObject self; //might not even need this
    void Start()
    {
        if (isStatic)
        {
            self = new StaticObject(gameObject);
        }
        else {
            self = new DynamicObject(gameObject, velocity, maxVelocityX, maxVelocityY, drag, mass, angularVelocity, maxAngularVelocity, angularDrag, inertia, gravityScale, terminalVelocity, freezeX, freezeY, freezeRotation, anchor);
        }
    }
    public static List<IPhysicsCollider> GetColliders(PhysicsObject obj) //get and disable colliders
    {
        List<IPhysicsCollider> colliders = new List<IPhysicsCollider>();

        BoxCollider2D[] boxColliders = obj.Instance.GetComponents<BoxCollider2D>();
        foreach (BoxCollider2D box in boxColliders)
        {
            colliders.Add(new PolygonCollider(obj, box));
            box.isTrigger = true;
        }

        PolygonCollider2D[] polygonColliders = obj.Instance.GetComponents<PolygonCollider2D>();
        foreach (PolygonCollider2D poly in polygonColliders)
        {
            colliders.Add(new PolygonCollider(obj, poly));
            poly.isTrigger = true;
        }

        return colliders;
    }
}
public abstract class PhysicsObject
{
    public abstract GameObject Instance { get; }
    public abstract IReadOnlyList<IPhysicsCollider> Colliders { get; }

    public abstract Vector2 Position { get; }
    public abstract float Rotation { get; }
    public abstract Vector2 Scale { get; }

    public abstract Vector2 Velocity { get; }
    public abstract float Mass { get; }

    public abstract float AngularVelocity { get; }
    public abstract float Inertia { get; }

    public Vector2 LocalToWorld_Point(Vector2 point)
    {
        Vector2 scaled = Vector2.Scale(point, Scale);

        float cos = Mathf.Cos(Rotation);
        float sin = Mathf.Sin(Rotation);

        Vector2 rotated = new Vector2(
            scaled.x * cos - scaled.y * sin,
            scaled.x * sin + scaled.y * cos
        );

        return Position + rotated;
    }

    public Vector2 WorldToLocal_Point(Vector2 point)
    {
        Vector2 translated = point - Position;

        float cos = Mathf.Cos(Rotation);
        float sin = Mathf.Sin(Rotation);

        Vector2 rotated = new Vector2(
            translated.x * cos + translated.y * sin,
            -translated.x * sin + translated.y * cos
        );

        return new Vector2(
            rotated.x / Scale.x,
            rotated.y / Scale.y
        );
    }

    public Vector2 LocalToWorld_Direction(Vector2 direction)
    {
        float cos = Mathf.Cos(Rotation);
        float sin = Mathf.Sin(Rotation);

        return new Vector2(
            direction.x * cos - direction.y * sin,
            direction.x * sin + direction.y * cos
        );
    }

    public Vector2 WorldToLocal_Direction(Vector2 direction)
    {
        float cos = Mathf.Cos(Rotation);
        float sin = Mathf.Sin(Rotation);

        return new Vector2(
            direction.x * cos + direction.y * sin,
            -direction.x * sin + direction.y * cos
        );
    }

    public abstract Vector2 GetVelocityAtPoint(Vector2 point, bool givenWorldCoorinates = false);
}
