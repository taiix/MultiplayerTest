using FishNet.Connection;
using FishNet.Managing;
using UnityEngine;
using FishNet.Transporting;

public class PlayerSpawnManager : MonoBehaviour
{
    [SerializeField] private NetworkManager networkManager;
    [SerializeField] private PlayerObjectController playerPrefab;

    void Awake()
    {
        if (networkManager == null)
            networkManager = FindFirstObjectByType<NetworkManager>();
        if (networkManager == null)
            Debug.LogError("[PlayerSpawnManager] NetworkManager not found.");
        DontDestroyOnLoad(gameObject);
    }

    void OnEnable()
    {
        if (networkManager == null) return;
        networkManager.ServerManager.OnServerConnectionState += OnServerConnectionState;
        networkManager.ServerManager.OnRemoteConnectionState += OnRemoteConnectionState;
    }

    void OnDisable()
    {
        if (networkManager == null) return;
        networkManager.ServerManager.OnServerConnectionState -= OnServerConnectionState;
        networkManager.ServerManager.OnRemoteConnectionState -= OnRemoteConnectionState;
    }

    private void OnServerConnectionState(ServerConnectionStateArgs args)
    {
        if (args.ConnectionState == LocalConnectionState.Started)
            SpawnHostIfMissing();
    }

    private void OnRemoteConnectionState(NetworkConnection conn, RemoteConnectionStateArgs args)
    {
        if (args.ConnectionState == RemoteConnectionState.Started)
            SpawnFor(conn);
    }

    private void SpawnHostIfMissing()
    {
        if (!networkManager.IsServerStarted || !networkManager.IsClientStarted) return;
        var hostConn = networkManager.ClientManager.Connection;
        if (hostConn == null) return;

        foreach (var nob in hostConn.Objects)
            if (nob.GetComponent<PlayerObjectController>() != null)
                return;

        SpawnFor(hostConn);
    }

    private void SpawnFor(NetworkConnection conn)
    {
        if (!networkManager.IsServerStarted) return;

        foreach (var nob in conn.Objects)
            if (nob.GetComponent<PlayerObjectController>() != null)
                return; // already spawned

        var instance = Instantiate(playerPrefab);
        networkManager.ServerManager.Spawn(instance.NetworkObject, conn);
        conn.SetFirstObject(instance.NetworkObject);
        Debug.Log($"[PlayerSpawnManager] Spawned player for connection {conn.ClientId}");
    }
}
