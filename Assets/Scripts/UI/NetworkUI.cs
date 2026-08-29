using System.Net;
using System.Net.Sockets;
using TMPro;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using UnityEngine;
using UnityEngine.UI;

public class NetworkUI : MonoBehaviour
{
    [Header("UI References")]
    public Canvas networkUI;
    public Toggle hostToggle;
    public TMP_InputField IPInput;
    public TMP_Text IPText;
    public TMP_InputField PortInput;
    public TMP_Text PortText;
    public TMP_Text statusText;
    public Canvas inputDisplay;

    void Start()
    {
        NetworkManager.Singleton.OnClientConnectedCallback += OnClientConnected;
        NetworkManager.Singleton.OnClientDisconnectCallback += OnClientDisconnected;
        IPText.text = "IP: " + getLocalIP();
    }

    void OnClientConnected(ulong clientId){
        if (NetworkManager.Singleton.IsServer && clientId == NetworkManager.Singleton.LocalClientId) return;
        statusText.text = "Connected!";
        if (NetworkManager.Singleton.IsServer) inputDisplay.gameObject.SetActive(true); //TEMPORARY
        networkUI.gameObject.SetActive(false);
    }

    void OnClientDisconnected(ulong clientId)
    {
        statusText.text = "Disconnected.";
    }
    public void OnHostToggleChanged()
    {
        if (hostToggle.isOn)
        {
            IPInput.gameObject.SetActive(false);
            IPText.text = "IP: " + getLocalIP();
        }
        else {
            IPInput.gameObject.SetActive(true);
            IPText.text = "IP:";
        }
    }
    public void StartGame()
    {
        var transport = NetworkManager.Singleton.GetComponent<UnityTransport>();

        if (ushort.TryParse(PortInput.text, out ushort port))
        {
            transport.ConnectionData.Port = port;
        }
        else
        {
            statusText.text = "Invalid port.";
            return;
        }

        if (hostToggle.isOn)
        {
            NetworkManager.Singleton.StartHost();
            statusText.text = "Waiting for player...";
        }
        else
        {
            transport.ConnectionData.Address = IPInput.text;
            NetworkManager.Singleton.StartClient();
            statusText.text = "Connecting...";
        }
    }

    string getLocalIP()
    {
        using (Socket socket = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, 0))
        {
            socket.Connect("8.8.8.8", 65530);
            IPEndPoint endPoint = socket.LocalEndPoint as IPEndPoint;
            return endPoint.Address.ToString();
        }
    }
}
