using FishNet;
using Steamworks;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

public class SteamLobbyUI : MonoBehaviour
{
    [Header("Lobby Creation")]
    public Button createLobbyButton;
    public Button leaveLobbyButton;

    [Header("Player List")]
    public Transform playerListContainer;
    public GameObject playerListItemPrefab;

    [Header("Lobby Info")]
    public TextMeshProUGUI lobbyNameText;
    public TextMeshProUGUI playerCountText;
    public TextMeshProUGUI lobbyStatusText;

    [Header("Invite Section")]
    public Button inviteFriendsButton;
    public GameObject inviteSection;

    [Header("Game Control")]
    public Button startGameButton;
    public UnityEvent StartGameRequested;

    [SerializeField] private SteamLobbyManager lobbyManager;
    private Dictionary<CSteamID, GameObject> playerListItems = new Dictionary<CSteamID, GameObject>();

    [SerializeField] private GameObject uiToDisable;

    void Start()
    {
        lobbyManager = FindObjectOfType<SteamLobbyManager>();

        // Button listeners
        createLobbyButton.onClick.AddListener(OnCreateLobby);
        leaveLobbyButton.onClick.AddListener(OnLeaveLobby);
        inviteFriendsButton.onClick.AddListener(OnInviteFriends);
        startGameButton.onClick.AddListener(OnStartGame);

        UpdateUI();
    }

    void Update()
    {
        UpdateUI();
    }



    void OnCreateLobby()
    {
        lobbyManager.CreateLobby(ELobbyType.k_ELobbyTypeFriendsOnly, 4);

        createLobbyButton.interactable = false;
        lobbyStatusText.text = "Creating lobby...";
    }

    void OnLeaveLobby()
    {
        lobbyManager.LeaveLobby();
    }

    void OnInviteFriends()
    {
        lobbyManager.InviteFriends();
    }

    void OnStartGame()
    {
        // Host-only guard
        if (!(lobbyManager.IsInLobby && SteamUser.GetSteamID() == lobbyManager.LobbyOwner))
            return;

        lobbyStatusText.text = "Starting...";
        lobbyStatusText.color = Color.yellow;
        startGameButton.interactable = false;

        // Let game flow handle the actual start (e.g., load scene)
        StartGameRequested?.Invoke();
    }

    void UpdateUI()
    {
        if (InstanceFinder.IsServerStarted)
        {
            uiToDisable.SetActive(false);
            return;
        }

        bool inLobby = lobbyManager.IsInLobby;
        bool steamReady = SteamManager.Initialized;

        // Main buttons
        createLobbyButton.interactable = steamReady && !inLobby;
        leaveLobbyButton.gameObject.SetActive(inLobby);
        inviteSection.SetActive(inLobby);

        bool isHost = inLobby && SteamUser.GetSteamID() == lobbyManager.LobbyOwner;
        startGameButton.gameObject.SetActive(isHost);
        if (isHost)
        {
            startGameButton.interactable = lobbyManager.PlayerCount > 0;
        }

        // Lobby info
        if (inLobby)
        {
            lobbyNameText.text = lobbyManager.LobbyName;
            playerCountText.text = $"{lobbyManager.PlayerCount}/{lobbyManager.MaxPlayers}";
            lobbyStatusText.text = "In Lobby";
            lobbyStatusText.color = Color.green;

            UpdatePlayerList();
        }
        else
        {
            lobbyNameText.text = "No Lobby";
            playerCountText.text = "0/0";
            lobbyStatusText.text = steamReady ? "Ready to create lobby" : "Steam not connected";
            lobbyStatusText.color = steamReady ? Color.yellow : Color.red;

            ClearPlayerList();
        }

    }

    void UpdatePlayerList()
    {
        // Get current lobby members
        var lobbyMembers = lobbyManager.GetLobbyMembers();

        // Remove players who left
        List<CSteamID> playersToRemove = new List<CSteamID>();
        foreach (var playerItem in playerListItems)
        {
            if (!lobbyMembers.Contains(playerItem.Key))
            {
                playersToRemove.Add(playerItem.Key);
            }
        }

        foreach (var steamID in playersToRemove)
        {
            Destroy(playerListItems[steamID]);
            playerListItems.Remove(steamID);
        }

        // Add new players and update existing ones
        foreach (var member in lobbyMembers)
        {
            if (!playerListItems.ContainsKey(member))
            {
                // Create new player list item
                GameObject playerItem = Instantiate(playerListItemPrefab, playerListContainer);
                playerListItems[member] = playerItem;

                // Start loading avatar
                StartCoroutine(LoadPlayerAvatar(member, playerItem));
            }

            // Update player info
            UpdatePlayerListItem(member, playerListItems[member]);
        }
    }

    void UpdatePlayerListItem(CSteamID steamID, GameObject playerItem)
    {
        PlayerListItemUI itemUI = playerItem.GetComponent<PlayerListItemUI>();
        if (itemUI != null)
        {
            string playerName = SteamFriends.GetFriendPersonaName(steamID);
            bool isHost = steamID == lobbyManager.LobbyOwner;
            bool isLocalPlayer = steamID == SteamUser.GetSteamID();

            itemUI.SetPlayerInfo(playerName, isHost, isLocalPlayer);
        }
    }

    IEnumerator LoadPlayerAvatar(CSteamID steamID, GameObject playerItem)
    {
        int avatarHandle = SteamFriends.GetLargeFriendAvatar(steamID);

        // Wait for avatar to load
        while (avatarHandle == -1)
        {
            yield return new WaitForSeconds(0.1f);
            avatarHandle = SteamFriends.GetLargeFriendAvatar(steamID);
        }

        // Get avatar texture
        Texture2D avatarTexture = GetSteamImageAsTexture(avatarHandle);

        // Apply to UI
        PlayerListItemUI itemUI = playerItem.GetComponent<PlayerListItemUI>();
        if (itemUI != null && avatarTexture != null)
        {
            itemUI.SetAvatar(avatarTexture);
        }
    }

    Texture2D GetSteamImageAsTexture(int imageHandle)
    {
        if (imageHandle == -1) return null;

        bool success = SteamUtils.GetImageSize(imageHandle, out uint width, out uint height);
        if (!success || width == 0 || height == 0) return null;

        byte[] imageData = new byte[width * height * 4];
        success = SteamUtils.GetImageRGBA(imageHandle, imageData, (int)(width * height * 4));

        if (!success) return null;

        Texture2D texture = new Texture2D((int)width, (int)height, TextureFormat.RGBA32, false);
        texture.LoadRawTextureData(imageData);
        texture.Apply();

        return texture;
    }

    void ClearPlayerList()
    {
        foreach (var playerItem in playerListItems.Values)
        {
            Destroy(playerItem);
        }
        playerListItems.Clear();
    }
}