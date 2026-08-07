using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Unity.Netcode;
using System;

public class ConnectionController : MonoBehaviour
{
    void Start()
    {
        NetworkManager.Singleton.OnClientConnectedCallback += OnClientConnected;
    }

    void OnClientConnected(ulong clientId)
    {
        if (clientId == NetworkManager.Singleton.LocalClientId) return;

        if (NetworkManager.Singleton.ConnectedClients.Count > 2)
        {
            Debug.Log("disconnecting client");
            NetworkManager.Singleton.DisconnectClient(clientId);
        }
    }
}
