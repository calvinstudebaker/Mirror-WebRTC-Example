using System.Collections.Generic;
using System.Threading.Tasks;
using Mirror;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class GameManager : MonoBehaviour
{
    private Button createLobbyButton;
    private Button joinExistingButton;
    private Button refreshLobbiesButton;

    private string currentLobbyId;
    private bool isLobbyHost = false;
    private TMP_Text createButtonText;

    // Avatar bar
    private Canvas avatarCanvas;
    private RectTransform avatarContainer;
    private readonly Dictionary<string, Texture2D> avatarTextureCache = new Dictionary<string, Texture2D>();
    private readonly Dictionary<string, RawImage> avatarImages = new Dictionary<string, RawImage>();

    static GameManager instance;

    void Awake()
    {
        if (instance != null && instance != this)
        {
            Destroy(gameObject);
            return;
        }

        instance = this;
        DontDestroyOnLoad(gameObject);
    }

    void Start()
    {
        Wavedash.SDK.Init(new Dictionary<string, object>
        {
            { "debug", true },
            {
                "p2p",
                new Dictionary<string, object>
                {
                    { "maxPeers", 8 },
                    { "messageSize", 2 * 1024 },
                    { "maxIncomingMessages", 1024 }
                }
            }
        });
        Debug.Log("WavedashSDK Initialized");

        if (Wavedash.SDK.IsReady())
        {
            var user = Wavedash.SDK.GetUser();
            if (user != null)
            {
                Debug.Log($"Playing as: {user["username"]}");
            }
        }

        FindAndBindUI();

        Wavedash.SDK.OnLobbyJoined += OnLobbyJoined;
        Wavedash.SDK.OnLobbyKicked += OnLobbyKicked;
        Wavedash.SDK.OnLobbyUsersUpdated += OnLobbyUsersUpdated;

        if (NetworkManager.singleton != null)
        {
            Debug.Log("[GameManager] NetworkManager.singleton found");
            var transport = NetworkManager.singleton.GetComponent<WavedashTransport>();
            if (transport != null)
                transport.OnHostMigration += OnHostMigration;
        }
        else
        {
            Debug.LogWarning("[GameManager] NetworkManager.singleton is null!");
        }

        SceneManager.sceneLoaded += OnSceneLoaded;

        CreateAvatarBar();

        Debug.Log("[GameManager] Calling ReadyForEvents...");
        Wavedash.SDK.ReadyForEvents();
        Debug.Log("[GameManager] ReadyForEvents done, calling UpdateUI and CheckForLobbies");

        UpdateUI();
        CheckForLobbies();
    }

    void OnDestroy()
    {
        Wavedash.SDK.OnLobbyJoined -= OnLobbyJoined;
        Wavedash.SDK.OnLobbyKicked -= OnLobbyKicked;
        Wavedash.SDK.OnLobbyUsersUpdated -= OnLobbyUsersUpdated;
        SceneManager.sceneLoaded -= OnSceneLoaded;
        DestroyAvatarBar();

        if (instance == this)
            instance = null;
    }

    void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        if (scene.path.EndsWith("OfflineScene.unity"))
        {
            Debug.Log("[GameManager] Offline scene loaded, re-binding UI");
            FindAndBindUI();
            UpdateUI();
            CheckForLobbies();
        }
    }

    void FindAndBindUI()
    {
        createLobbyButton = null;
        joinExistingButton = null;
        refreshLobbiesButton = null;
        createButtonText = null;

        var buttons = FindObjectsByType<Button>(FindObjectsSortMode.None);
        foreach (var btn in buttons)
        {
            switch (btn.gameObject.name)
            {
                case "CreateLobbyButton":
                    createLobbyButton = btn;
                    break;
                case "JoinLobbyButton":
                    joinExistingButton = btn;
                    break;
                case "RefreshLobbiesButton":
                    refreshLobbiesButton = btn;
                    break;
            }
        }

        Debug.Log($"[GameManager] FindAndBindUI: create={createLobbyButton != null}, join={joinExistingButton != null}, refresh={refreshLobbiesButton != null}");

        if (createLobbyButton != null)
        {
            createButtonText = createLobbyButton.GetComponentInChildren<TMP_Text>();
            createLobbyButton.onClick.RemoveAllListeners();
            createLobbyButton.onClick.AddListener(OnCreateLobbyClicked);
        }

        if (joinExistingButton != null)
        {
            joinExistingButton.onClick.RemoveAllListeners();
            joinExistingButton.onClick.AddListener(OnJoinExistingClicked);
        }

        if (refreshLobbiesButton != null)
        {
            refreshLobbiesButton.onClick.RemoveAllListeners();
            refreshLobbiesButton.onClick.AddListener(OnRefreshLobbiesClicked);
        }
    }

    void OnLobbyJoined(Dictionary<string, object> lobbyData)
    {
        currentLobbyId = lobbyData["lobbyId"].ToString();
        isLobbyHost = lobbyData["hostId"].ToString() == Wavedash.SDK.GetUserId();
        UpdateUI();
        RefreshAvatars();

        if (isLobbyHost)
            NetworkManager.singleton.StartHost();
        else
            NetworkManager.singleton.StartClient();
    }

    void OnLobbyKicked(Dictionary<string, object> lobbyData)
    {
        Debug.Log($"Kicked from lobby: {currentLobbyId}");
        StopNetwork();
        currentLobbyId = null;
        isLobbyHost = false;
        UpdateUI();
        ClearAvatars();
    }

    void OnLobbyUsersUpdated(Dictionary<string, object> data)
    {
        if (currentLobbyId != null)
            RefreshAvatars();
    }

    void OnHostMigration(string newHostId)
    {
        bool wasHost = isLobbyHost;
        isLobbyHost = newHostId == Wavedash.SDK.GetUserId();

        Debug.Log($"[GameManager] Host migration: wasHost={wasHost}, isNowHost={isLobbyHost}, newHostId={newHostId}");

        if (wasHost)
            NetworkManager.singleton.StopHost();
        else
            NetworkManager.singleton.StopClient();

        if (isLobbyHost)
            NetworkManager.singleton.StartHost();
        else
            NetworkManager.singleton.StartClient();
    }

    void StopNetwork()
    {
        if (isLobbyHost)
            NetworkManager.singleton.StopHost();
        else
            NetworkManager.singleton.StopClient();
    }

    async void OnCreateLobbyClicked()
    {
        Debug.Log("[GameManager] Create/Leave button clicked");
        try
        {
            if (currentLobbyId != null)
            {
                StopNetwork();
                Debug.Log("[GameManager] Calling LeaveLobby...");
                string left = await Wavedash.SDK.LeaveLobby(currentLobbyId);
                if (left != null)
                {
                    Debug.Log($"Left lobby: {left}");
                    currentLobbyId = null;
                    isLobbyHost = false;
                    UpdateUI();
                    ClearAvatars();
                    CheckForLobbies();
                }
                else
                {
                    Debug.Log("Failed to leave lobby");
                }
            }
            else
            {
                Debug.Log("[GameManager] Calling CreateLobby...");
                string lobbyId = await Wavedash.SDK.CreateLobby(WavedashConstants.LobbyVisibility.PUBLIC);
                Debug.Log($"Created lobby: {lobbyId}");
            }
        }
        catch (System.Exception e)
        {
            Debug.LogError($"[GameManager] OnCreateLobbyClicked error: {e}");
        }
    }

    async void OnJoinExistingClicked()
    {
        Debug.Log("[GameManager] Join button clicked");
        try
        {
            Debug.Log("[GameManager] Calling ListAvailableLobbies...");
            var lobbies = await Wavedash.SDK.ListAvailableLobbies();
            if (lobbies != null && lobbies.Count > 0)
            {
                string lobbyId = lobbies[0]["lobbyId"].ToString();
                Debug.Log($"[GameManager] Calling JoinLobby({lobbyId})...");
                bool joined = await Wavedash.SDK.JoinLobby(lobbyId);
                Debug.Log(joined ? $"Joined lobby: {lobbyId}" : $"Failed to join lobby: {lobbyId}");
            }
            else
            {
                Debug.Log("No available lobbies to join");
            }
        }
        catch (System.Exception e)
        {
            Debug.LogError($"[GameManager] OnJoinExistingClicked error: {e}");
        }
    }

    void OnRefreshLobbiesClicked()
    {
        Debug.Log("[GameManager] Refresh button clicked");
        CheckForLobbies();
    }

    async void CheckForLobbies()
    {
        try
        {
            Debug.Log("[GameManager] Calling ListAvailableLobbies...");
            var lobbies = await Wavedash.SDK.ListAvailableLobbies();
            bool hasLobbies = lobbies != null && lobbies.Count > 0;
            Debug.Log($"Found {lobbies?.Count ?? 0} lobbies");
            if (joinExistingButton != null)
                joinExistingButton.interactable = hasLobbies && currentLobbyId == null;
        }
        catch (System.Exception e)
        {
            Debug.LogError($"[GameManager] CheckForLobbies error: {e}");
        }
    }

    void UpdateUI()
    {
        bool inLobby = currentLobbyId != null;

        if (createButtonText != null)
            createButtonText.text = inLobby ? "Leave Lobby" : "Create Lobby";

        if (joinExistingButton != null)
            joinExistingButton.interactable = !inLobby;

        if (refreshLobbiesButton != null)
            refreshLobbiesButton.interactable = !inLobby;
    }

    // --- Avatar Bar ---

    void CreateAvatarBar()
    {
        var canvasGo = new GameObject("AvatarBarCanvas");
        DontDestroyOnLoad(canvasGo);
        avatarCanvas = canvasGo.AddComponent<Canvas>();
        avatarCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
        avatarCanvas.sortingOrder = 500;
        canvasGo.AddComponent<CanvasScaler>();
        canvasGo.AddComponent<GraphicRaycaster>();

        var containerGo = new GameObject("AvatarContainer");
        containerGo.transform.SetParent(canvasGo.transform, false);
        avatarContainer = containerGo.AddComponent<RectTransform>();

        avatarContainer.anchorMin = new Vector2(0, 0.7f);
        avatarContainer.anchorMax = new Vector2(0, 0.7f);
        avatarContainer.pivot = new Vector2(0, 0);
        avatarContainer.anchoredPosition = new Vector2(10, 4);

        var layout = containerGo.AddComponent<HorizontalLayoutGroup>();
        layout.spacing = 6;
        layout.childAlignment = TextAnchor.MiddleLeft;
        layout.childForceExpandWidth = false;
        layout.childForceExpandHeight = false;

        var fitter = containerGo.AddComponent<ContentSizeFitter>();
        fitter.horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
        fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
    }

    void DestroyAvatarBar()
    {
        foreach (var tex in avatarTextureCache.Values)
            if (tex != null) Destroy(tex);
        avatarTextureCache.Clear();
        avatarImages.Clear();

        if (avatarCanvas != null)
            Destroy(avatarCanvas.gameObject);
    }

    void ClearAvatars()
    {
        foreach (var kvp in avatarImages)
            if (kvp.Value != null) Destroy(kvp.Value.gameObject);
        avatarImages.Clear();
    }

    async void RefreshAvatars()
    {
        if (currentLobbyId == null || avatarContainer == null) return;

        var users = Wavedash.SDK.GetLobbyUsers(currentLobbyId);
        var currentUserIds = new HashSet<string>();

        foreach (var user in users)
        {
            string userId = user["userId"].ToString();
            currentUserIds.Add(userId);

            if (avatarImages.ContainsKey(userId)) continue;

            var imgGo = new GameObject($"Avatar_{userId}");
            imgGo.transform.SetParent(avatarContainer, false);
            var rawImg = imgGo.AddComponent<RawImage>();
            rawImg.color = new Color(0.3f, 0.3f, 0.3f, 1f);

            var le = imgGo.AddComponent<LayoutElement>();
            le.preferredWidth = 48;
            le.preferredHeight = 48;

            avatarImages[userId] = rawImg;

            await FetchAndApplyAvatar(userId, rawImg);
        }

        var toRemove = new List<string>();
        foreach (var kvp in avatarImages)
        {
            if (!currentUserIds.Contains(kvp.Key))
            {
                if (kvp.Value != null) Destroy(kvp.Value.gameObject);
                toRemove.Add(kvp.Key);
            }
        }
        foreach (var id in toRemove)
            avatarImages.Remove(id);
    }

    async Task FetchAndApplyAvatar(string userId, RawImage target)
    {
        if (target == null) return;

        Texture2D tex;
        if (avatarTextureCache.TryGetValue(userId, out tex) && tex != null)
        {
            target.texture = tex;
            target.color = Color.white;
            return;
        }

        tex = await Wavedash.SDK.GetUserAvatar(userId, WavedashConstants.AvatarSize.SMALL);
        if (tex != null)
        {
            avatarTextureCache[userId] = tex;
            if (target != null)
            {
                target.texture = tex;
                target.color = Color.white;
            }
        }
    }
}
