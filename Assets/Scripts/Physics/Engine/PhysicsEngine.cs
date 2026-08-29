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
    
    public static PhysicsEngine instance { get; private set; }
    public GameState gameState { get; private set; } = new GameState();
    void Awake()
    {
        instance = this;
        Time.fixedDeltaTime = 1f/refreshRate;
        RegionManager.SIZE = regionSize;
    }

    void FixedUpdate()
    {
        gameState.Step();
        gameState.Apply();
    }
}
