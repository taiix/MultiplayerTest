using FishNet.Object;
using FishNet.Object.Synchronizing;
using Steamworks;
using TMPro;
using UnityEngine;

public class PlayerSteamData : NetworkBehaviour
{
    [Header("Steam Info")]
    public readonly SyncVar<string> steamName = new SyncVar<string>();
    public readonly SyncVar<ulong> steamID = new SyncVar<ulong>();

    [Header("Visuals")]
    public TextMeshPro nameTag;
    public GameObject localPlayerIndicator;
    public Material localPlayerMaterial;
    public Material remotePlayerMaterial;

    private Renderer playerRenderer;

    void Awake()
    {
        playerRenderer = GetComponent<Renderer>();
    }

    public void SetSteamData(CSteamID steamId, string name)
    {
        steamName.Value = name;
        steamID.Value = steamId.m_SteamID;
        UpdateVisuals();
    }

    public override void OnStartClient()
    {
        base.OnStartClient();
        UpdateVisuals();

        if (IsOwner)
        {
            Debug.Log($"🎮 I am controlling: {steamName.Value}");
            EnableLocalPlayer();
        }
        else
        {
            Debug.Log($"👤 Other player in game: {steamName.Value}");
            EnableRemotePlayer();
        }
    }

    private void UpdateVisuals()
    {
        // Set name tag
        if (nameTag != null)
        {
            nameTag.text = steamName.Value;
        }

        // Show local player indicator
        if (localPlayerIndicator != null)
        {
            localPlayerIndicator.SetActive(IsOwner);
        }

        // Set different material for local vs remote players
        if (playerRenderer != null)
        {
            if (IsOwner && localPlayerMaterial != null)
            {
                playerRenderer.material = localPlayerMaterial;
            }
            else if (!IsOwner && remotePlayerMaterial != null)
            {
                playerRenderer.material = remotePlayerMaterial;
            }
        }

        // Set game object name for debugging
        gameObject.name = $"{steamName.Value} (Player)";
    }

    private void EnableLocalPlayer()
    {
        // Enable PlayerController for local player
        PlayerController controller = GetComponent<PlayerController>();
        if (controller != null)
        {
            controller.enabled = true;
            Debug.Log($"✅ Enabled PlayerController for {steamName.Value}");
        }

        // Enable camera and audio listener
        Camera playerCamera = GetComponentInChildren<Camera>();
        if (playerCamera != null)
        {
            playerCamera.enabled = true;
            AudioListener audioListener = playerCamera.GetComponent<AudioListener>();
            if (audioListener != null) audioListener.enabled = true;
        }
    }

    private void EnableRemotePlayer()
    {
        // Disable PlayerController for remote players (they control their own)
        PlayerController controller = GetComponent<PlayerController>();
        if (controller != null)
        {
            controller.enabled = false;
        }

        // Disable camera for remote players
        Camera playerCamera = GetComponentInChildren<Camera>();
        if (playerCamera != null)
        {
            playerCamera.enabled = false;
            AudioListener audioListener = playerCamera.GetComponent<AudioListener>();
            if (audioListener != null) audioListener.enabled = false;
        }
    }
}