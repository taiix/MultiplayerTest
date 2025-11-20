using FishNet.Connection;
using FishNet.Managing.Scened;
using FishNet.Object;
using System.Linq;
using UnityEngine;

public class LobbyGameStarter : NetworkBehaviour
{
    public void ChangeGameScene(string sceneName)
    {
        if (!IsServer)
        {
            Debug.Log("Client requesting scene change from server...");
            RequestSceneChangeServerRpc(sceneName);
            return;
        }

        // Only server executes this
        LoadGameScene(sceneName);
    }

    [ServerRpc(RequireOwnership = false)]
    private void RequestSceneChangeServerRpc(string sceneName, NetworkConnection sender = null)
    {
        Debug.Log($"Server received scene change request from connection {sender?.ClientId}");
        LoadGameScene(sceneName);
    }

    private void LoadGameScene(string sceneName)
    {
        // Double check we're server and managers are available
        if (!IsServer)
        {
            Debug.LogError("Only server can load scenes!");
            return;
        }

        if (ServerManager == null)
        {
            Debug.LogError("ServerManager is null!");
            return;
        }

        if (SceneManager == null)
        {
            Debug.LogError("SceneManager is null!");
            return;
        }

        if (ServerManager.Clients == null || ServerManager.Clients.Count == 0)
        {
            Debug.LogError("No clients connected!");
            return;
        }

        Debug.Log($"Server loading scene: {sceneName} for {ServerManager.Clients.Count} clients");

        SceneLoadData sceneLoadData = new SceneLoadData(sceneName);
        sceneLoadData.ReplaceScenes = ReplaceOption.All;

        NetworkConnection[] connections = ServerManager.Clients.Values.ToArray();
        SceneManager.LoadConnectionScenes(connections, sceneLoadData);
    }

    // Keep your existing methods but fix the observer
    [ServerRpc(RequireOwnership = false)]
    void OpenGameScene(string sceneName)
    {
        OpenScenesObserver(sceneName);
    }

    [ObserversRpc]
    void OpenScenesObserver(string sceneName)
    {
        // This runs on all clients when server calls it
        Debug.Log($"Observer received scene change: {sceneName}");
    }
}