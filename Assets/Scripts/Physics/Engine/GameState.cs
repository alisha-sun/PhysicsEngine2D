using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class GameState
{
    public int tick { get; private set; } = 0;
    public RegionManager regions { get; private set; } = new RegionManager();
    private List<DynamicObject> objects = new List<DynamicObject>();

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

            obj.SetPosition(obj.Position + obj.Velocity * time);
            obj.SetRotation(obj.Rotation + obj.AngularVelocity * time);
            regions.UpdateRegions(obj);
        }
        foreach (DynamicObject obj in objects)
        {
            List<Collision> collisions = Collision.GetCollisions(obj, this);

            SpriteRenderer rend = obj.Instance.GetComponent<SpriteRenderer>(); // <-- debug
            if (rend != null) {
                if (collisions.Count > 0) rend.color = Color.yellow;
                else rend.color = Color.white;
            }

            Vector2 impulse = new Vector2(0, 0);
            foreach (Collision collision in collisions) { 
                
            }

            regions.UpdateRegions(obj);
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
        regions.AddToRegions(obj);
    }

    public void Unregister(PhysicsObject obj)
    {
        if (obj is DynamicObject dynamicObject) objects.Remove(dynamicObject);
        regions.RemoveFromRegions(obj);
    }
}
