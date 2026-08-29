using System;
using System.Collections;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

public class DynamicObject : PhysicsObject
{
    private static int nextId = 0;
    public int NetworkId { get; }
    private GameObject _instance;                               public override GameObject Instance => _instance;
    private List<IPhysicsCollider> _colliders;                  public override IReadOnlyList<IPhysicsCollider> Colliders => _colliders;
    private Renderer[] _renderers;

    private Vector2 _position;                                  public override Vector2 Position => _position;              // (m,m)
    private float _rotation;                                    public override float Rotation => _rotation;                // radians
    private Vector2 _scale;                                     public override Vector2 Scale => _scale;

    private Vector2 _velocity;                                  public Vector2 Velocity => _velocity;                       // m/s
    private Vector2 _maxVelocity;                               public Vector2 MaxVelocity => _maxVelocity;
    private float _drag = 0f;                                   public float Drag => _drag;                                 // not sure what unit lol
    private float _mass = 1f;                                   public float Mass => _mass;                                 // kg

    private float _angularVelocity;                             public float AngularVelocity => _angularVelocity;           // radians/s
    private float _maxAngularVelocity;                          public float MaxAngularVelocity => _maxAngularVelocity;
    private float _angularDrag = 0f;                            public float AngularDrag => _angularDrag;                   // not sure what unit lol
    public float _inertia = 1f;                                 public float Inertia => _inertia;                           // we didnt study rotational motion in-depth in Physics 12... units are kgm^2 ? I'll do my research later.

    private float _gravityScale;                                public float GravityScale => _gravityScale;
    private float _terminalVelocity;                            public float TerminalVelocity => _terminalVelocity;

    private bool _freezeX;                                      public bool FreezeX => _freezeX;                            // prevents changes in position and velocity for X
    private bool _freezeY;                                      public bool FreezeY => _freezeY;                            // prevents changes in position and velocity for Y
    private bool _freezeRotation;                               public bool FreezeRotation => _freezeRotation;              // prevents changes in rotation and angular velocity
    private Vector2 _anchor;                                    public Vector2 Anchor => _anchor;                           // in local coordinates

    private (Vector2Int min, Vector2Int max) _boundingRegions;  public (Vector2Int min, Vector2Int max) BoundingRegions => _boundingRegions;

    public DynamicObject(GameObject instance, Vector2 velocity, float maxVelocityX, float maxVelocityY, float drag, float mass, float angularVelocity, float maxAngularVelocity, float angularDrag, float inertia, float gravityScale, float terminalVelocity, bool freezeX, bool freezeY, bool freezeRotation, Vector2 anchor) {
        NetworkId = nextId++;

        _position = instance.transform.position;
        _rotation = instance.transform.eulerAngles.z * Mathf.Deg2Rad;
        _scale = instance.transform.localScale;
        _instance = instance;
        _colliders = PhysicsObjectComponent.GetColliders(this);
        _renderers = _instance.GetComponentsInChildren<Renderer>();

        SetVelocity(velocity, true, true, true, true);
        SetMaxVelocityX(maxVelocityX);
        SetMaxVelocityY(maxVelocityY);
        SetDrag(drag);
        SetMass(mass);

        SetAngularVelocity(angularVelocity, true, true);
        SetMaxAngularVelocity(maxAngularVelocity);
        SetAngularDrag(angularDrag);
        SetInertia(inertia);

        SetGravityScale(gravityScale);
        SetTerminalVelocity(terminalVelocity);

        SetFreezeX(freezeX);
        SetFreezeY(freezeY);
        SetFreezeRotation(freezeRotation);
        SetAnchor(anchor);

        PhysicsEngine.instance.gameState.Register(this);
    }


    /* BASIC SETTERS */

    public void SetPosition(Vector2 pos, bool overrideFreezeX = false, bool overrideFreezeY = false) {
        if (overrideFreezeX || !_freezeX) _position.x = pos.x;
        if (overrideFreezeY || !_freezeY) _position.y = pos.y;
    }
    public void SetPosition_Anchor(Vector2 worldPos, bool overrideFreezeX = false, bool overrideFreezeY = false)
    {
        if (float.IsNaN(_anchor.x)) SetPosition(worldPos, overrideFreezeX, overrideFreezeY);
        else SetPosition(_position + worldPos - LocalToWorld_Point(Anchor), overrideFreezeX, overrideFreezeY);
    }

    public void SetRotation(float rot, bool overrideFreeze = false)
    {
        if (overrideFreeze || !_freezeRotation) _rotation = Mathf.Repeat(rot, 2f * Mathf.PI);
    }

    public void SetScale(Vector2 scale)
    {
        _scale.x = scale.x;
        _scale.y = scale.y;
    }


    public void SetVelocity(Vector2 v, bool overrideMaxX = false, bool overrideMaxY = false, bool overrideFreezeX = false, bool overrideFreezeY = false) {
        SetVelocityX(v.x, overrideMaxX, overrideFreezeX);
        SetVelocityY(v.y, overrideMaxY, overrideFreezeY);
    }
    public void SetVelocityX(float v_x, bool overrideMax = false, bool overrideFreeze = false)
    {
        if (_freezeX && !overrideFreeze) return;
        if (overrideMax) _velocity.x = v_x;
        else if (v_x > 0) _velocity.x = Mathf.Min(v_x, _maxVelocity.x);
        else _velocity.x = Mathf.Max(v_x, -_maxVelocity.x);
    }
    public void SetVelocityY(float v_y, bool overrideMax = false, bool overrideFreeze = false)
    {
        if (_freezeY && !overrideFreeze) return;
        if (overrideMax) _velocity.y = v_y;
        else if (v_y > 0) _velocity.y = Mathf.Min(v_y, _maxVelocity.y);
        else _velocity.y = Mathf.Max(v_y, -_maxVelocity.y);
    }

    public void SetMaxVelocityX(float v) {
        _maxVelocity.x = Math.Abs(v);
    }
    public void SetMaxVelocityY(float v) {
        _maxVelocity.y = Math.Abs(v);
    }

    public void SetDrag(float drag)
    {
        if (drag < 0) throw new ArgumentException("Drag must be non-negative", nameof(drag));
        _drag = drag;
    }
    public void SetMass(float mass)
    {
        if (mass < 0.1f) throw new ArgumentException("Mass must be at least 0.1", nameof(mass));
        else _mass = mass;
    }


    public void SetAngularVelocity(float w, bool overrideMax = false, bool overrideFreeze = false) {
        if (_freezeRotation && !overrideFreeze) return;
        if (overrideMax) _angularVelocity = w;
        else if (w > 0) _angularVelocity = Mathf.Min(w, _maxAngularVelocity);
        else _angularVelocity = Mathf.Max(w, -_maxAngularVelocity);
    }

    public void SetMaxAngularVelocity(float w)
    {
        _maxAngularVelocity = Math.Abs(w);
    }

    public void SetAngularDrag(float angularDrag)
    {
        if (angularDrag < 0) throw new ArgumentException("Angular drag must be non-negative", nameof(angularDrag));
        _angularDrag = angularDrag;
    }
    public void SetInertia(float inertia)
    {
        if (inertia < 0.1f) throw new ArgumentException("Inertia must be at least 0.1", nameof(inertia));
        else _inertia = inertia;
    }


    public void SetGravityScale(float scale) {
        _gravityScale = scale;
    }

    public void SetTerminalVelocity(float v) {
        _terminalVelocity = Math.Abs(v);
    }


    public void SetFreezeX(bool freeze) {
        _freezeX = freeze;
    }
    public void SetFreezeY(bool freeze)
    {
        _freezeY = freeze;
    }
    public void SetFreezeRotation(bool freeze)
    {
        _freezeRotation = freeze;
    }

    public void SetAnchor(Vector2 anchor) {
        if (float.IsNaN(anchor.x) || float.IsNaN(anchor.y)) RemoveAnchor();
        else _anchor = anchor;
    }
    public void RemoveAnchor() {
        _anchor.x = float.NaN;
        _anchor.y = float.NaN;
    }


    /* PHYSICS METHODS */

    public void ApplyGravity(float time) {
        if (!float.IsNaN(_anchor.x)) ApplyTorque(new Vector2(0, PhysicsEngine.instance.gravity * _mass * _gravityScale), time, Vector2.zero);
        if (_velocity.y > -_terminalVelocity) SetVelocityY(Mathf.Max(_velocity.y + PhysicsEngine.instance.gravity * _gravityScale * time, -_terminalVelocity));
    }

    public void ApplyDrag(float time) {
        Vector2 dragForce = -_velocity * _drag;

        ApplyTorque(dragForce, time, Vector2.zero);

        if (Vector2.Dot(_velocity, _velocity + dragForce / _mass * time) < 0)
        {
            _velocity.y = 0;
            _velocity.x = 0;
        }
        else _velocity += dragForce / _mass * time;
    }

    public void ApplyAngularDrag(float time) {
        float angularDrag = -_angularVelocity * _angularDrag / _inertia;

        if (_angularVelocity * _angularVelocity + angularDrag * time < 0) _angularVelocity = 0;
        else _angularVelocity += + angularDrag * time;
    }

    public void ApplyForce(Vector2 force, float time, Vector2 applicationPointLocal, bool overrideMaxX = false, bool overrideMaxY = false, bool overrideMaxAngularVelocity = false, bool overrideFreezeX = false, bool overrideFreezeY = false, bool overrideFreezeRotation = false)
    {
        ApplyTorque(force, time, applicationPointLocal, overrideMaxAngularVelocity, overrideFreezeRotation); //apply to the center of mass by default
        SetVelocity(force / _mass * time, overrideMaxX, overrideMaxY, overrideFreezeX, overrideFreezeY);
    }
    public void ApplyForce(Vector2 force, float time, bool overrideMaxX = false, bool overrideMaxY = false, bool overrideMaxAngularVelocity = false, bool overrideFreezeX = false, bool overrideFreezeY = false, bool overrideFreezeRotation = false) {
        ApplyTorque(force, time, Vector2.zero, overrideMaxAngularVelocity, overrideFreezeRotation); //apply to the center of mass by default
        SetVelocity(force / _mass * time, overrideMaxX, overrideMaxY, overrideFreezeX, overrideFreezeY);
    }

    public void ApplyTorque(Vector2 force, float time, Vector2 applicationPointLocal, bool overrideMaxAngularVelocity = false, bool overrideFreezeRotation = false)
    {
        Vector2 pivot = _anchor;
        if (float.IsNaN(pivot.x)) pivot = Vector2.zero;
        pivot = LocalToWorld_Point(pivot);


        Vector2 applicationPoint = LocalToWorld_Point(applicationPointLocal);
        Vector2 arm = applicationPoint - pivot;
        Vector2 normal = new Vector2(-arm.y, arm.x).normalized;
        float w = (arm.magnitude * Vector2.Dot(force, normal)) / _inertia * time; // Nm counterclockwise / kgm^2 * s = N/kgm = (kgm^2/s^2) / kgm^2 * s = 1/s = radians per seconds

        SetAngularVelocity(_angularVelocity + w, overrideMaxAngularVelocity, overrideFreezeRotation);
    }

    public void ApplyImpulse(Vector2 impulse, Vector2 applicationPointLocal, bool overrideMaxX = false, bool overrideMaxY = false, bool overrideMaxAngularVelocity = false, bool overrideFreezeX = false, bool overrideFreezeY = false, bool overrideFreezeRotation = false) {
        ApplyForce(impulse, 1f, applicationPointLocal, overrideMaxX, overrideMaxY, overrideMaxAngularVelocity, overrideFreezeX, overrideFreezeY, overrideFreezeRotation);
    }


    /* OTHER */

    public void RecalculateBoundingRegions() {
        Vector2 min = new Vector2(float.PositiveInfinity,float.PositiveInfinity);
        Vector2 max = new Vector2(float.NegativeInfinity,float.NegativeInfinity);

        foreach (Renderer renderer in _renderers)
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

        _boundingRegions = RegionManager.GetBoundingRegions((min, max));
    }

    public void ApplyTransform() {
        _instance.transform.position = _position;
        _instance.transform.rotation = Quaternion.Euler(0, 0, _rotation * Mathf.Rad2Deg);
        _instance.transform.localScale = _scale;
    }
}
