using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class PlayerListItemUI : MonoBehaviour
{
    [Header("UI References")]
    public Image avatarImage;
    public TextMeshProUGUI playerNameText;

    [Header("Colors")]
    public Color localPlayerColor = new Color(0.2f, 0.4f, 0.8f, 0.3f);
    public Color normalColor = new Color(0.1f, 0.1f, 0.1f, 0.3f);

    public void SetPlayerInfo(string name, bool isHost, bool isLocalPlayer)
    {
        playerNameText.text = name;

        if (isLocalPlayer)
        {
            playerNameText.text += " (You)";
        }

        if (isHost)
        {
            playerNameText.text += " (Host)";
        }
    }

    public void SetAvatar(Texture2D avatarTexture)
    {
        if (avatarImage != null && avatarTexture != null)
        {
            avatarImage.sprite = Sprite.Create(
                avatarTexture,
                new Rect(0, 0, -avatarTexture.width, -avatarTexture.height),
                new Vector2(0.5f, 0.5f)
            );
        }
    }
}