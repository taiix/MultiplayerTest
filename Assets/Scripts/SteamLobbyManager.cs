using Steamworks;
using UnityEngine;
using System.Collections.Generic;
using FishNet.Managing;
using System; // for Action

public class SteamLobbyManager : MonoBehaviour
{
    private CSteamID currentLobbyID;
    private const string HostAddressKey = "HostAddress";

    public NetworkManager manager;
    public FishySteamworks.FishySteamworks fishySteamworks;

    // UI events
    public event Action OnLobbyStateChanged;
    public event Action OnPlayerListChanged;

    // Steam callbacks
    protected Callback<LobbyCreated_t> Callback_lobbyCreated;
    protected Callback<LobbyEnter_t> Callback_lobbyEnter;
    protected Callback<GameLobbyJoinRequested_t> Callback_gameLobbyJoinRequested_t;
    protected Callback<LobbyChatUpdate_t> Callback_lobbyMemberChanged;

    // Player tracking
    public List<CSteamID> lobbyMembers = new List<CSteamID>();
    public List<string> lobbyMemberNames = new List<string>();

    // Public properties
    public bool IsInLobby => currentLobbyID.IsValid();
    public string LobbyName => IsInLobby ? SteamMatchmaking.GetLobbyData(currentLobbyID, "name") : "No Lobby";
    public int PlayerCount => IsInLobby ? SteamMatchmaking.GetNumLobbyMembers(currentLobbyID) : 0;
    public int MaxPlayers => IsInLobby ? SteamMatchmaking.GetLobbyMemberLimit(currentLobbyID) : 0;
    public CSteamID LobbyOwner => IsInLobby ? SteamMatchmaking.GetLobbyOwner(currentLobbyID) : CSteamID.Nil;

    private void Awake()
    {
        // Don't risk null parent NRE
        DontDestroyOnLoad(gameObject);
    }

    void Start()
    {
        if (!SteamManager.Initialized)
        {
            Debug.LogError("Steam not initialized!");
            return;
        }

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
        OnPlayerListChanged?.Invoke();
    }

    public void CreateLobby(ELobbyType lobbyType, int maxPlayers)
    {
        Debug.Log("Creating Steam lobby...");
        SteamMatchmaking.CreateLobby(lobbyType, maxPlayers);
    }

    // ===== STEAM CALLBACKS =====

    private void OnLobbyCreated(LobbyCreated_t result)
    {
        if (result.m_eResult == EResult.k_EResultOK)
        {
            currentLobbyID = new CSteamID(result.m_ulSteamIDLobby);

            string personalName = SteamFriends.GetPersonaName();

            // Store host steamID as numeric string to avoid parsing issues
            SteamMatchmaking.SetLobbyData(currentLobbyID, HostAddressKey, SteamUser.GetSteamID().m_SteamID.ToString());
            SteamMatchmaking.SetLobbyData(currentLobbyID, "name", personalName + "'s Game");

            Debug.Log("✅ Lobby created successfully: " + currentLobbyID);

            // Refresh players immediately for host
            UpdatePlayerList();
            OnLobbyStateChanged?.Invoke();
        }
        else
        {
            Debug.LogError("❌ Failed to create lobby: " + result.m_eResult);
        }
    }

    public void StartGame()
    {
        if (!IsInLobby) return;

        if (SteamUser.GetSteamID() == LobbyOwner)
        {
            // Host starts server
            fishySteamworks.SetClientAddress(SteamUser.GetSteamID().m_SteamID.ToString());
            fishySteamworks.StartConnection(true);
        }
        else
        {
            // Client connects to host
            string hostAddress = SteamMatchmaking.GetLobbyData(currentLobbyID, HostAddressKey);
            fishySteamworks.SetClientAddress(hostAddress);
            fishySteamworks.StartConnection(false);
        }
    }

    private void OnLobbyEntered(LobbyEnter_t result)
    {
        currentLobbyID = new CSteamID(result.m_ulSteamIDLobby);

        if (result.m_EChatRoomEnterResponse == (uint)EChatRoomEnterResponse.k_EChatRoomEnterResponseSuccess)
        {
            // Show full current roster immediately on join
            UpdatePlayerList();
            OnLobbyStateChanged?.Invoke();

            // Configure transport connection target
            fishySteamworks.SetClientAddress(SteamMatchmaking.GetLobbyData(currentLobbyID, HostAddressKey));
            fishySteamworks.StartConnection(false);
        }
        else
        {
            Debug.LogError($"❌ Failed to enter lobby: {(EChatRoomEnterResponse)result.m_EChatRoomEnterResponse}");
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
            OnPlayerListChanged?.Invoke();
            OnLobbyStateChanged?.Invoke();
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
        {
            SteamAPI.RunCallbacks();
        }
    }

    void OnDestroy()
    {
        if (IsInLobby)
        {
            LeaveLobby();
        }
    }
}