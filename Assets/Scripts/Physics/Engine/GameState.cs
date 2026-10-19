using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
public class GameState
{
    public int tick { get; private set; } = 0;
    internal RegionManager regions { get; private set; } = new RegionManager();
    private List<DynamicObject> objects = new List<DynamicObject>();
    private List<StaticObject> staticObjects = new List<StaticObject>();

    private const float ITERATIONS = 10;

    public void Step(int ticks)
    {
        for (int t = 0; t < ticks; t++) Step();
    }
    public void Step() {
        Step(Time.fixedDeltaTime);
        tick++;
    }
    private void Step(float time)
    {
        foreach (DynamicObject obj in objects)
        {
            obj.ApplyGravity(time);
            obj.ApplyDrag(time);
            obj.ApplyAngularDrag(time);
            obj.EnforceAnchor();

            obj.SetPosition_Internal(obj.Position + obj.Velocity * time);
            obj.SetRotation_Internal(obj.Rotation + obj.AngularVelocity * time);
            obj.OnTransformation();
        }
        for (int i = 0; i < ITERATIONS; i++)
        {
            foreach (DynamicObject obj in objects)
            {
                Collision.DoCollisions(obj, this);
            }

            foreach (StaticObject obj in staticObjects) { //collisions with static objects are more important so they go last. Also i might implement continuous collision detection for static objects later
                Collision.DoCollisions(obj, this);
            }
        }
    }

    public void Apply()
    {
        foreach (DynamicObject obj in objects)
        {
            obj.ApplyTransform();
        }
    }

    public void Register(PhysicsObject obj)
    {
        if (obj is DynamicObject dynamicObject) objects.Add(dynamicObject);
        else staticObjects.Add((StaticObject)obj);
            regions.AddToRegions(obj);
    }

    public void Unregister(PhysicsObject obj)
    {
        if (obj is DynamicObject dynamicObject) objects.Remove(dynamicObject);
        else staticObjects.Remove((StaticObject)obj);
        regions.RemoveFromRegions(obj);
    }
}
