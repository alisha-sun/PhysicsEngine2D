using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class PhysicsEngine : MonoBehaviour
{
    public Transform simulationCenter;
    public float simulationDistance = 40f;
    public float refreshRate = 100f;
    public float regionSize = 4f;

    public float gravity = -9.8f; // m/s^2

    public bool paused = false;
    
    public static PhysicsEngine instance { get; private set; }
    public GameState gameState { get; private set; } = new GameState();
    void Awake()
    {
        instance = this;
        Time.fixedDeltaTime = 1f/refreshRate;
        RegionManager.SIZE = regionSize;
    }

    private void Start()
    {
        PhysicsObjectComponent[] objects = FindObjectsByType<PhysicsObjectComponent>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
        foreach (PhysicsObjectComponent obj in objects) {
            obj.Initialize();
            PhysicsEngine.instance.gameState.Register(obj.self);
        }
        Debug.Log("FINISHED INITIALIZATION");
    }

    void FixedUpdate()
    {
        if (!paused)
        {
            gameState.Step();
            gameState.Apply();
        }
    }
}
