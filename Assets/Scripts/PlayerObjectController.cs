using FishNet.Object;
using FishNet.Object.Synchronizing;
using Steamworks;
using UnityEngine;

public class PlayerObjectController : NetworkBehaviour
{
    [Header("Steam Info")]
    public readonly SyncVar<string> steamName = new SyncVar<string>();
    public readonly SyncVar<ulong> steamID = new SyncVar<ulong>();

    public string SteamName => steamName.Value;
    public ulong SteamID => steamID.Value;

    // Server-only setter. Use this internally after validation or handshake.
    private void SetSteamDataInternal(CSteamID id, string name)
    {
        steamName.Value = name ?? string.Empty;
        steamID.Value = id.m_SteamID;
        gameObject.name = $"{steamName.Value} (Player)";
        Debug.Log($"[PlayerObjectController] Steam data set: {steamName.Value} ({steamID.Value})");
    }

    public override void OnStartClient()
    {
        base.OnStartClient();

        // Owner sends handshake if server hasn't populated SyncVars yet.
        if (IsOwner && IsClientStarted && steamID.Value == 0UL)
        {
            ulong myId = SteamUser.GetSteamID().m_SteamID;
            string myName = SteamFriends.GetPersonaName();
            SubmitSteamIdentityServerRpc(myId, myName);
        }
    }

    // Client -> Server handshake. Ownership required (the spawned player belongs to this connection).
    [ServerRpc(RequireOwnership = true)]
    private void SubmitSteamIdentityServerRpc(ulong rawSteamId, string persona)
    {
        if (rawSteamId == 0UL)
        {
            Debug.LogWarning("[PlayerObjectController] Received invalid SteamID from client.");
            return;
        }

        // (Optional) You could cross-check rawSteamId with a transport-provided mapping stored in sender.CustomData.
        SetSteamDataInternal(new CSteamID(rawSteamId), persona);
    }
}