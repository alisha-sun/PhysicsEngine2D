using System;
using System.Collections;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

public class DynamicObject : PhysicsObject
{
    private static int nextId = 0;

    public readonly int NetworkId;
    private readonly GameObject _instance;                      public override GameObject Instance => _instance;
    private List<IPhysicsCollider> _colliders;                  public override IReadOnlyList<IPhysicsCollider> Colliders => _colliders;
    private Renderer[] _renderers; //only used to calculate bounds

    private Vector2 _position;                                  public override Vector2 Position => _position;              // (m,m)
    private float _rotation;                                    public override float Rotation => _rotation;                // radians
    private Vector2 _scale;                                     public override Vector2 Scale => _scale;

    private Vector2 _velocity;                                  public override Vector2 Velocity => _velocity;              // m/s
    private Vector2 _maxVelocity;                               public Vector2 MaxVelocity => _maxVelocity;
    private float _drag = 0f;                                   public float Drag => _drag;                                 // not sure what unit lol
    private float _mass = 1f;                                   public override float Mass => _mass;                        // kg

    private float _angularVelocity;                             public override float AngularVelocity => _angularVelocity;  // radians/s CCW
    private float _maxAngularVelocity;                          public float MaxAngularVelocity => _maxAngularVelocity;
    private float _angularDrag = 0f;                            public float AngularDrag => _angularDrag;                   // not sure what unit lol
    public float _inertia = 1f;                                 public override float Inertia => _inertia;                  // we didnt study rotational motion in-depth in Physics 12... units are kgm^2 ? I'll do my research later.

    private float _gravityScale;                                public float GravityScale => _gravityScale;
    private float _terminalVelocity;                            public float TerminalVelocity => _terminalVelocity;

    private bool _freezeX;                                      public bool FreezeX => _freezeX;                            // prevents changes in position and velocity for X
    private bool _freezeY;                                      public bool FreezeY => _freezeY;                            // prevents changes in position and velocity for Y
    private bool _freezeRotation;                               public bool FreezeRotation => _freezeRotation;              // prevents changes in rotation and angular velocity
    private Vector2 _anchor;                                    public Vector2 Anchor => _anchor;                           // in local coordinates
    private Vector2 _anchorVelocity;

    private (Vector2 min, Vector2 max) _boundingBox;
    private BoundingRegions _boundingRegions;                   internal BoundingRegions BoundingRegions => _boundingRegions;


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

        RecalculateBoundingBox();
    }


    /* BASIC SETTERS/GETTERS */

    public void AddPosition(Vector2 deltaP, bool overrideFreezeX = false, bool overrideFreezeY = false)
    {
        SetPosition_Internal(_position + deltaP, overrideFreezeX, overrideFreezeY);
        OnTransformation();
    }
    public void SetPosition(Vector2 pos, bool overrideFreezeX = false, bool overrideFreezeY = false) {
        SetPosition_Internal(pos, overrideFreezeX, overrideFreezeY);
        OnTransformation();
    }
    internal void SetPosition_Internal(Vector2 pos, bool overrideFreezeX = false, bool overrideFreezeY = false)
    {
        if (!Vectors2D.IsFinite(pos)) throw new ArgumentOutOfRangeException(nameof(pos), pos, "Position must be finite");

        if (overrideFreezeX || !_freezeX) _position.x = pos.x;
        if (overrideFreezeY || !_freezeY) _position.y = pos.y;
    }

    public void AddRotation(float deltaR, bool overrideFreeze = false)
    {
        SetRotation_Internal(_rotation + deltaR, overrideFreeze);
        OnTransformation();
    }
    public void SetRotation(float rot, bool overrideFreeze = false)
    {
        SetRotation_Internal(rot, overrideFreeze);
        OnTransformation();
    }
    internal void SetRotation_Internal(float rot, bool overrideFreeze = false)
    {
        if (!float.IsFinite(rot)) throw new ArgumentOutOfRangeException(nameof(rot), rot, "Rotation must be finite");

        if (overrideFreeze || !_freezeRotation) _rotation = Mathf.Repeat(rot, 2f * Mathf.PI);
    }

    public void SetScale(Vector2 scale)
    {
        SetScale_Internal(scale);
        OnTransformation();
    }
    internal void SetScale_Internal(Vector2 scale)
    {
        if (!Vectors2D.IsFinite(scale)) throw new ArgumentOutOfRangeException(nameof(scale), scale, "Scale must be finite");

        _scale.x = scale.x;
        _scale.y = scale.y;
    }


    public void AddVelocity(Vector2 deltaV, bool overrideMaxX = false, bool overrideMaxY = false, bool overrideFreezeX = false, bool overrideFreezeY = false) {
        SetVelocity(_velocity + deltaV, overrideMaxX, overrideMaxY, overrideFreezeX, overrideFreezeY);
    }
    public void SetVelocity(Vector2 v, bool overrideMaxX = false, bool overrideMaxY = false, bool overrideFreezeX = false, bool overrideFreezeY = false) {
        if (!Vectors2D.IsFinite(v)) throw new ArgumentOutOfRangeException(nameof(v), v, "Velocity must be finite");

        SetVelocityX(v.x, overrideMaxX, overrideFreezeX);
        SetVelocityY(v.y, overrideMaxY, overrideFreezeY);
    }
    private void SetVelocityX(float v_x, bool overrideMax = false, bool overrideFreeze = false)
    {
        if (_freezeX && !overrideFreeze) return;
        if (overrideMax) _velocity.x = v_x;
        else if (v_x > 0) _velocity.x = Mathf.Min(v_x, _maxVelocity.x);
        else _velocity.x = Mathf.Max(v_x, -_maxVelocity.x);
    }
    private void SetVelocityY(float v_y, bool overrideMax = false, bool overrideFreeze = false)
    {
        if (_freezeY && !overrideFreeze) return;
        if (overrideMax) _velocity.y = v_y;
        else if (v_y > 0) _velocity.y = Mathf.Min(v_y, _maxVelocity.y);
        else _velocity.y = Mathf.Max(v_y, -_maxVelocity.y);
    }
    public override Vector2 GetVelocityAtPoint(Vector2 point, bool givenWorldCoordinates = false) {
        Edge centerToPoint_world;
        if (givenWorldCoordinates) centerToPoint_world = new Edge(_position, point);
        else centerToPoint_world = new Edge(_position, LocalToWorld_Point(point));
        return _velocity + _angularVelocity * centerToPoint_world.tangent.magnitude * centerToPoint_world.normal;
    }

    public void SetMaxVelocityX(float v) {
        if (float.IsNaN(v)) throw new ArgumentException("MaxVelocityX cannot be NaN", nameof(v));

        _maxVelocity.x = Math.Abs(v);
    }
    public void SetMaxVelocityY(float v) {
        if (float.IsNaN(v)) throw new ArgumentException("MaxVelocityY cannot be NaN", nameof(v));

        _maxVelocity.y = Math.Abs(v);
    }

    public void SetDrag(float drag)
    {
        if (float.IsNaN(drag)) throw new ArgumentException("Drag cannot be NaN", nameof(drag));
        if (drag < 0) throw new ArgumentOutOfRangeException("Drag must be non-negative", nameof(drag));

        _drag = drag;
    }
    public void SetMass(float mass)
    {
        if (float.IsNaN(mass)) throw new ArgumentException("Mass cannot be NaN", nameof(mass));
        if (mass < 0.1f) throw new ArgumentOutOfRangeException("Mass must be at least 0.1", nameof(mass));

        _mass = mass;
    }


    public void AddAngularVelocity(float deltaW, bool overrideMax = false, bool overrideFreeze = false)
    {
        SetAngularVelocity(_angularVelocity + deltaW, overrideMax, overrideFreeze);
    }
    public void SetAngularVelocity(float w, bool overrideMax = false, bool overrideFreeze = false) {
        if (!float.IsFinite(w)) throw new ArgumentOutOfRangeException(nameof(w), w, "Angular velocity must be finite");

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
        if (float.IsNaN(angularDrag)) throw new ArgumentException("Angular drag cannot be NaN", nameof(angularDrag));
        if (angularDrag < 0) throw new ArgumentOutOfRangeException("Angular drag must be non-negative", nameof(angularDrag));

        _angularDrag = angularDrag;
    }
    public void SetInertia(float inertia)
    {
        if (float.IsNaN(inertia)) throw new ArgumentException("Inertia cannot be NaN", nameof(inertia));
        if (inertia < 0.1f) throw new ArgumentOutOfRangeException("Inertia must be at least 0.1", nameof(inertia));

        _inertia = inertia;
    }


    public void SetGravityScale(float scale) {
        if (!float.IsFinite(scale)) throw new ArgumentOutOfRangeException(nameof(scale), scale, "Gravity scale must be finite");

        _gravityScale = scale;
    }

    public void SetTerminalVelocity(float v) {
        if (float.IsNaN(v)) throw new ArgumentException("Terminal veloctiy cannot be NaN", nameof(v));

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
        else if (!Vectors2D.IsFinite(anchor)) throw new ArgumentOutOfRangeException(nameof(anchor), anchor, "Anchor must be finite");
        else {
            _anchor = anchor;
            _anchorVelocity = Vector2.zero;
        }
    }
    public void RemoveAnchor() {
        _anchor.x = float.NaN;
        _anchor.y = float.NaN;
    }
    public void SetPosition_Anchor(Vector2 worldPos, bool overrideFreezeX = false, bool overrideFreezeY = false)
    {
        if (!Vectors2D.IsFinite(worldPos)) throw new ArgumentOutOfRangeException(nameof(worldPos), worldPos, "Anchor position must be finite");
        else if (float.IsNaN(_anchor.x)) SetPosition_Internal(worldPos, overrideFreezeX, overrideFreezeY);
        else SetPosition_Internal(_position + worldPos - LocalToWorld_Point(_anchor), overrideFreezeX, overrideFreezeY);
        OnTransformation();
    }
    public void SetVelocity_Anchor(Vector2 v) {
        if (!Vectors2D.IsFinite(v)) throw new ArgumentOutOfRangeException(nameof(v), v, "Anchor velocity must be finite");

        _anchorVelocity = v;
    }


    /* PHYSICS METHODS */

    internal void ApplyGravity(float time) {
        Debug.Assert(float.IsFinite(PhysicsEngine.instance.gravity));
        if (!float.IsFinite(time)) throw new ArgumentOutOfRangeException(nameof(time), time, "Time must be finite");
        if (time < 0) throw new ArgumentOutOfRangeException(nameof(time), time, "Time must be non-negative");

        if (_velocity.y > -_terminalVelocity) SetVelocityY(Mathf.Max(_velocity.y + PhysicsEngine.instance.gravity * _gravityScale * time, -_terminalVelocity));
    }

    internal void ApplyDrag(float time) {
        if (!float.IsFinite(time)) throw new ArgumentOutOfRangeException(nameof(time), time, "Time must be finite");
        if (time < 0) throw new ArgumentOutOfRangeException(nameof(time), time, "Time must be non-negative");

        Vector2 dragForce = -_velocity * _drag;

        if (Vector2.Dot(_velocity, _velocity + dragForce / _mass * time) < 0)
        {
            _velocity.y = 0;
            _velocity.x = 0;
        }
        else _velocity += dragForce / _mass * time;
    }

    internal void ApplyAngularDrag(float time) {
        if (!float.IsFinite(time)) throw new ArgumentOutOfRangeException(nameof(time), time, "Time must be finite");
        if (time < 0) throw new ArgumentOutOfRangeException(nameof(time), time, "Time must be non-negative");

        float angularDrag = -_angularVelocity * _angularDrag / _inertia;

        if (_angularVelocity * _angularVelocity + angularDrag * time < 0) _angularVelocity = 0;
        else AddAngularVelocity(angularDrag * time);
    }

    internal void EnforceAnchor() {
        if (float.IsNaN(_anchor.x)) return;
        Vector2 r = LocalToWorld_Point(_anchor) - _position;

        Vector2 v = -_velocity + new Vector2(_angularVelocity*r.y, -_angularVelocity*r.x) + _anchorVelocity;

        float a = 1/_mass + r.y*r.y / _inertia;
        float b = -r.y * r.x / _inertia;
        float d = 1/_mass + r.x*r.x / _inertia;

        Vector2 j = Vectors2D.SolveSystem(a, b, b, d, v);

        if (!Vectors2D.IsFinite(j)) { //can happen if mass is very high and the anchor is near the center of mass
            Debug.LogWarning("Unable to solve for impulse to enforce anchor for " + _instance.name);
            SetVelocity(_anchorVelocity);
        }
        else ApplyImpulse(j, _anchor, true, true, true);
    }

    public void ApplyForce(Vector2 force, float time, Vector2 applicationPoint_local, bool overrideMaxX = false, bool overrideMaxY = false, bool overrideMaxAngularVelocity = false, bool overrideFreezeX = false, bool overrideFreezeY = false, bool overrideFreezeRotation = false)
    {
        ApplyTorque(force, time, applicationPoint_local, overrideMaxAngularVelocity, overrideFreezeRotation);
        AddVelocity(force / _mass * time, overrideMaxX, overrideMaxY, overrideFreezeX, overrideFreezeY);
    }
    public void ApplyForce(Vector2 force, float time, bool overrideMaxX = false, bool overrideMaxY = false, bool overrideMaxAngularVelocity = false, bool overrideFreezeX = false, bool overrideFreezeY = false, bool overrideFreezeRotation = false) {
        ApplyTorque(force, time, Vector2.zero, overrideMaxAngularVelocity, overrideFreezeRotation); //apply to the center of mass by default
        AddVelocity(force / _mass * time, overrideMaxX, overrideMaxY, overrideFreezeX, overrideFreezeY);
    }

    public void ApplyTorque(Vector2 force, float time, Vector2 applicationPoint_local, bool overrideMaxAngularVelocity = false, bool overrideFreezeRotation = false)
    {
        if (!float.IsFinite(time)) throw new ArgumentOutOfRangeException(nameof(time), time, "Time must be finite");
        if (time < 0) throw new ArgumentOutOfRangeException(nameof(time), time, "Time must be non-negative");
        if (!Vectors2D.IsFinite(force)) throw new ArgumentOutOfRangeException(nameof(force), force, "Force must be finite");
        if (!Vectors2D.IsFinite(applicationPoint_local)) throw new ArgumentOutOfRangeException(nameof(applicationPoint_local), applicationPoint_local, "Application point must be finite");

        Vector2 applicationPoint = LocalToWorld_Point(applicationPoint_local);
        float deltaW = time * Vectors2D.Cross(applicationPoint - _position, force) / _inertia; // s * Nm counterclockwise / kgm^2 = Ns/kgm = ((kgm/s^2)s / kgm) / kgm^2 = 1/s = radians per seconds

        AddAngularVelocity(deltaW, overrideMaxAngularVelocity, overrideFreezeRotation);
    }

    public void ApplyImpulse(Vector2 impulse, Vector2 applicationPoint_local, bool overrideMaxX = false, bool overrideMaxY = false, bool overrideMaxAngularVelocity = false, bool overrideFreezeX = false, bool overrideFreezeY = false, bool overrideFreezeRotation = false) {
        ApplyForce(impulse, 1f, applicationPoint_local, overrideMaxX, overrideMaxY, overrideMaxAngularVelocity, overrideFreezeX, overrideFreezeY, overrideFreezeRotation);
    }


    /* OTHER */

    internal void OnTransformation() {
        foreach (IPhysicsCollider collider in _colliders)
        {
            collider.FullUpdate();
        }

        PhysicsEngine.instance.gameState.regions.UpdateRegions(this);
    }
    internal void RecalculateBoundingBox()
    {
        Vector2 min = new Vector2(float.PositiveInfinity, float.PositiveInfinity);
        Vector2 max = new Vector2(float.NegativeInfinity, float.NegativeInfinity);

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

        _boundingBox = (min, max);
        _boundingRegions = RegionManager.GetBoundingRegions(_boundingBox);
    }

    internal void OnTranslation(Vector2 deltaP, bool updateRegions, bool overrideFreezeX = false, bool overrideFreezeY = false) {
        if (_freezeX && !overrideFreezeX) deltaP.x = 0;
        if (_freezeY && !overrideFreezeY) deltaP.y = 0;

        foreach (IPhysicsCollider collider in _colliders)
        {
            collider.PositionUpdate(deltaP);
        }

        if (updateRegions) PhysicsEngine.instance.gameState.regions.UpdateRegions(this, deltaP);
    }
    internal bool TranslateBoundingBox(Vector2 deltaP)
    {
        _boundingBox.min += deltaP;
        _boundingBox.max += deltaP;

        BoundingRegions newRegions = RegionManager.GetBoundingRegions(_boundingBox);
        if (newRegions.max != _boundingRegions.max || newRegions.min != _boundingRegions.min) {
            _boundingRegions = newRegions;
            return true;
        }
        return false;
    }

    public void ApplyTransform() {
        _instance.transform.position = _position;
        _instance.transform.rotation = Quaternion.Euler(0, 0, _rotation * Mathf.Rad2Deg);
        _instance.transform.localScale = _scale;
    }
}
