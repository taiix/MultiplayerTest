using FishNet.Managing;           // NetworkManager
using Steamworks;
using System.Collections.Generic;
using UnityEngine;
// using FishNet.Connection;      // If you later need NetworkConnection
// using FishNet.Object;          // If you later need NetworkObject

public class SteamLobbyManager : MonoBehaviour
{
    public static SteamLobbyManager Instance { get; private set; }

    private CSteamID currentLobbyID;
    private const string HostAddressKey = "HostAddress";

    [SerializeField] private FishySteamworks.FishySteamworks fishySteamworks;
    [SerializeField] private NetworkManager networkManager;

    // Player prefab should be a NetworkObject and registered in Spawnable Prefabs on NetworkManager
    [SerializeField] private PlayerObjectController playerPrefab;

    // Steam callbacks
    protected Callback<LobbyCreated_t> Callback_lobbyCreated;
    protected Callback<LobbyEnter_t> Callback_lobbyEnter;
    protected Callback<GameLobbyJoinRequested_t> Callback_gameLobbyJoinRequested_t;
    protected Callback<LobbyChatUpdate_t> Callback_lobbyMemberChanged;

    // Player tracking
    [SerializeField] private List<CSteamID> lobbyMembers = new List<CSteamID>();
    [SerializeField] private List<string> lobbyMemberNames = new List<string>();

    // Public properties
    public bool IsInLobby => currentLobbyID.IsValid();
    public string LobbyName => IsInLobby ? SteamMatchmaking.GetLobbyData(currentLobbyID, "name") : "No Lobby";
    public int PlayerCount => IsInLobby ? SteamMatchmaking.GetNumLobbyMembers(currentLobbyID) : 0;
    public int MaxPlayers => IsInLobby ? SteamMatchmaking.GetLobbyMemberLimit(currentLobbyID) : 0;
    public CSteamID LobbyOwner => IsInLobby ? SteamMatchmaking.GetLobbyOwner(currentLobbyID) : CSteamID.Nil;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
        DontDestroyOnLoad(gameObject);
    }

    void Start()
    {
        if (!SteamManager.Initialized)
        {
            Debug.LogError("Steam not initialized!");
            return;
        }

        if (networkManager == null)
            networkManager = FindFirstObjectByType<NetworkManager>();

        if (networkManager == null)
            Debug.LogWarning("NetworkManager not found. Assign it in the inspector.");

        SetupCallbacks();
        Debug.Log("Steam Lobby Manager initialized!");
    }

    void SetupCallbacks()
    {
        Callback_lobbyCreated = Callback<LobbyCreated_t>.Create(OnLobbyCreated);
        Callback_lobbyEnter = Callback<LobbyEnter_t>.Create(OnLobbyEntered);
        Callback_gameLobbyJoinRequested_t = Callback<GameLobbyJoinRequested_t>.Create(OnJoinRequest);
        Callback_lobbyMemberChanged = Callback<LobbyChatUpdate_t>.Create(OnLobbyMemberChanged);
    }

    private void OnLobbyMemberChanged(LobbyChatUpdate_t result)
    {
        UpdatePlayerList();
    }

    private void UpdatePlayerList()
    {
        lobbyMembers.Clear();
        lobbyMemberNames.Clear();

        if (!IsInLobby) return;

        int memberCount = SteamMatchmaking.GetNumLobbyMembers(currentLobbyID);
        for (int i = 0; i < memberCount; i++)
        {
            CSteamID memberID = SteamMatchmaking.GetLobbyMemberByIndex(currentLobbyID, i);
            string memberName = SteamFriends.GetFriendPersonaName(memberID);

            lobbyMembers.Add(memberID);
            lobbyMemberNames.Add(memberName);
        }

        Debug.Log($"Lobby updated: {lobbyMembers.Count} players");
        // Raise UI events here if needed.
    }

    public void CreateLobby(ELobbyType lobbyType, int maxPlayers)
    {
        Debug.Log("Creating Steam lobby...");
        SteamMatchmaking.CreateLobby(lobbyType, maxPlayers);
    }

    private void OnLobbyCreated(LobbyCreated_t result)
    {
        if (result.m_eResult == EResult.k_EResultOK)
        {
            currentLobbyID = new CSteamID(result.m_ulSteamIDLobby);

            string personalName = SteamFriends.GetPersonaName();

            SteamMatchmaking.SetLobbyData(currentLobbyID, HostAddressKey, SteamUser.GetSteamID().m_SteamID.ToString());
            SteamMatchmaking.SetLobbyData(currentLobbyID, "name", personalName + "'s Game");

            Debug.Log("✅ Lobby created successfully: " + currentLobbyID);

            UpdatePlayerList();
            StartHostNetworking();
        }
        else
        {
            Debug.LogError("❌ Failed to create lobby: " + result.m_eResult);
        }
    }

    private void StartHostNetworking()
    {
        if (networkManager == null) return;

        // Set address to self prior to start.
        fishySteamworks.SetClientAddress(SteamUser.GetSteamID().m_SteamID.ToString());

        if (!networkManager.IsServerStarted)
            networkManager.ServerManager.StartConnection();

        if (!networkManager.IsClientStarted)
            networkManager.ClientManager.StartConnection();

        Debug.Log("[SteamLobbyManager] Host networking started (Server + Client).");

        // Spawn host player via PlayerSpawnManager if needed.
        var spawner = FindFirstObjectByType<PlayerSpawnManager>();
        spawner?.Invoke("SpawnHostIfMissing", 0f);
    }

    private void StartClientNetworking()
    {
        if (networkManager == null) return;

        string hostAddress = SteamMatchmaking.GetLobbyData(currentLobbyID, HostAddressKey);
        if (string.IsNullOrWhiteSpace(hostAddress))
        {
            Debug.LogError("[SteamLobbyManager] Host address missing. Cannot start client.");
            return;
        }

        fishySteamworks.SetClientAddress(hostAddress);

        if (!networkManager.IsClientStarted)
            networkManager.ClientManager.StartConnection();

        Debug.Log($"[SteamLobbyManager] Client networking started to host {hostAddress}.");
    }

    private void OnLobbyEntered(LobbyEnter_t result)
    {
        currentLobbyID = new CSteamID(result.m_ulSteamIDLobby);

        if (result.m_EChatRoomEnterResponse != (uint)EChatRoomEnterResponse.k_EChatRoomEnterResponseSuccess)
        {
            Debug.LogError($"[SteamLobbyManager] Failed to enter lobby: {(EChatRoomEnterResponse)result.m_EChatRoomEnterResponse}");
            return;
        }

        UpdatePlayerList();
        Debug.Log($"[SteamLobbyManager] Entered lobby {currentLobbyID}.");

        if (networkManager.IsHostStarted)
        {
            // Host will get LobbyEnter after LobbyCreated (double callback). Ensure network started.
            if (!networkManager.IsServerStarted || !networkManager.IsClientStarted)
            {
                Debug.Log("[SteamLobbyManager] Host network not started yet, starting now.");
                StartHostNetworking();
            }
        }
        else
        {
            // Remote client auto-connects.
            StartClientNetworking();
        }
    }

    private void OnJoinRequest(GameLobbyJoinRequested_t callback)
    {
        SteamMatchmaking.JoinLobby(callback.m_steamIDLobby);
    }

    public void LeaveLobby()
    {
        if (IsInLobby)
        {
            SteamMatchmaking.LeaveLobby(currentLobbyID);
            currentLobbyID = CSteamID.Nil;
            lobbyMembers.Clear();
            lobbyMemberNames.Clear();
            Debug.Log("Left lobby");
        }
    }

    public void InviteFriends()
    {
        if (!IsInLobby) return;
        SteamFriends.ActivateGameOverlayInviteDialog(currentLobbyID);
    }

    public List<CSteamID> GetLobbyMembers()
    {
        return lobbyMembers;
    }

    void Update()
    {
        if (SteamManager.Initialized)
            SteamAPI.RunCallbacks();
    }

    void OnDestroy()
    {
        if (IsInLobby)
            LeaveLobby();
    }
}