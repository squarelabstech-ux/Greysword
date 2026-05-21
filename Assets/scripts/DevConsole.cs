using UnityEngine;
using System.IO;

public class DevConsole : MonoBehaviour
{
    private bool showConsole = false;
    private string commandInput = "";

    // Antigravity IPC (Inter-Process Communication) fields
    private float fileCheckTimer = 0f;
    private const float fileCheckInterval = 0.5f;
    private string commandFilePath;
    private string responseFilePath;

    // Mobile touchscreen keyboard reference
    private TouchScreenKeyboard mobileKeyboard;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    static void AutoCreate()
    {
        GameObject go = new GameObject("DevConsole_Auto");
        go.AddComponent<DevConsole>();
        DontDestroyOnLoad(go);
    }

    void Start()
    {
        commandFilePath = Path.Combine(Application.persistentDataPath, "antigravity_command.txt");
        responseFilePath = Path.Combine(Application.persistentDataPath, "antigravity_response.txt");

        // Clean up any stale files at start
        try
        {
            if (File.Exists(commandFilePath)) File.Delete(commandFilePath);
            if (File.Exists(responseFilePath)) File.Delete(responseFilePath);
        }
        catch {}
    }

    void Update()
    {
        // Don't poll external commands if game hasn't started
        if (GameUIManager.Instance != null && !GameUIManager.IsGamePlaying) return;

        fileCheckTimer += Time.deltaTime;
        if (fileCheckTimer >= fileCheckInterval)
        {
            fileCheckTimer = 0f;
            CheckForExternalCommands();
        }

        // Handle TouchScreenKeyboard input on mobile devices
        if (mobileKeyboard != null)
        {
            commandInput = mobileKeyboard.text;
            if (mobileKeyboard.status == TouchScreenKeyboard.Status.Done)
            {
                if (!string.IsNullOrEmpty(commandInput))
                {
                    ExecuteCommand(commandInput.Trim().ToLower());
                }
                commandInput = "";
                showConsole = false;
                mobileKeyboard = null;
            }
            else if (mobileKeyboard.status == TouchScreenKeyboard.Status.Canceled)
            {
                commandInput = "";
                showConsole = false;
                mobileKeyboard = null;
            }
        }
    }

    void CheckForExternalCommands()
    {
        try
        {
            if (File.Exists(commandFilePath))
            {
                string command = File.ReadAllText(commandFilePath).Trim();
                // Delete file immediately to prevent looping
                File.Delete(commandFilePath);

                if (!string.IsNullOrEmpty(command))
                {
                    Debug.Log($"[Antigravity IPC] Received external command: {command}");
                    // Standardize command formatting to start with a slash
                    string processedCmd = command.ToLower();
                    if (!processedCmd.StartsWith("/"))
                    {
                        processedCmd = "/" + processedCmd;
                    }
                    
                    ExecuteCommand(processedCmd);
                    
                    // Write response file for external agent handshake
                    File.WriteAllText(responseFilePath, $"Success: Executed '{processedCmd}' at {System.DateTime.Now}\n");
                }
            }
        }
        catch (System.Exception ex)
        {
            Debug.LogWarning($"[Antigravity IPC] Error reading external command: {ex.Message}");
        }
    }

    void OnGUI()
    {
        // Don't render console trigger if game hasn't started or is paused
        if (GameUIManager.Instance != null && !GameUIManager.IsGamePlaying) return;
        if (GameUIManager.IsGamePaused) return;

        // Toggle button at top right below brain logs
        float btnWidth = 140f;
        float btnHeight = 35f;
        Rect btnRect = new Rect(Screen.width - btnWidth - 30f, 135f, btnWidth, btnHeight);

        GUI.color = new Color(0.2f, 0.8f, 0.8f, 1f); // Cyan
        if (GUI.Button(btnRect, "<b>DEV CONSOLE</b>", new GUIStyle(GUI.skin.button) { richText = true }))
        {
            showConsole = !showConsole;
            if (showConsole && (Application.platform == RuntimePlatform.Android || Application.platform == RuntimePlatform.IPhonePlayer))
            {
                mobileKeyboard = TouchScreenKeyboard.Open(commandInput, TouchScreenKeyboardType.Default, false, false, false, false, "Enter Command");
            }
        }
        GUI.color = Color.white;

        if (showConsole)
        {
            Rect panelRect = new Rect((Screen.width - 400f) / 2f, 100f, 400f, 60f);
            GUI.Box(panelRect, "Developer Commands");

            GUI.SetNextControlName("DevInput");

            Rect fieldRect = new Rect(panelRect.x + 10, panelRect.y + 25, 280, 25);
            
            // On mobile, tapping the text field manually will also open/focus the touch keyboard
            if (Application.platform == RuntimePlatform.Android || Application.platform == RuntimePlatform.IPhonePlayer)
            {
                if (Event.current.type == EventType.MouseDown && fieldRect.Contains(Event.current.mousePosition))
                {
                    if (mobileKeyboard == null)
                    {
                        mobileKeyboard = TouchScreenKeyboard.Open(commandInput, TouchScreenKeyboardType.Default, false, false, false, false, "Enter Command");
                    }
                }
            }

            commandInput = GUI.TextField(fieldRect, commandInput);

            if (GUI.Button(new Rect(panelRect.x + 300, panelRect.y + 25, 80, 25), "Execute") || 
                (Event.current.isKey && Event.current.keyCode == KeyCode.Return && GUI.GetNameOfFocusedControl() == "DevInput"))
            {
                ExecuteCommand(commandInput.Trim().ToLower());
                commandInput = "";
                showConsole = false; // Auto close after execute
                if (mobileKeyboard != null)
                {
                    mobileKeyboard.active = false;
                    mobileKeyboard = null;
                }
            }
        }
    }

    void ExecuteCommand(string cmd)
    {
        Debug.Log($"[DevConsole] Executing: {cmd}");

        // Handle setting remote server URL (e.g. /server http://192.168.1.5:8080)
        if (cmd.StartsWith("/server "))
        {
            string url = cmd.Substring("/server ".Length).Trim();
            if (AntigravityGameDirector.Instance != null)
            {
                AntigravityGameDirector.Instance.serverUrl = url;
                PlayerPrefs.SetString("AntigravityServerUrl", url);
                PlayerPrefs.Save();
                Debug.Log($"[DevConsole] Remote Antigravity Server URL set to: {url}");
            }
            return;
        }

        // Handle parameters (like /setdifficulty <value>)
        if (cmd.StartsWith("/setdifficulty "))
        {
            string diff = cmd.Substring("/setdifficulty ".Length).Trim();
            DifficultyProfile newProfile = null;
            if (diff == "easy") newProfile = DifficultyProfile.Easy();
            else if (diff == "normal") newProfile = DifficultyProfile.Normal();
            else if (diff == "hard") newProfile = DifficultyProfile.Hard();
            else if (diff == "bored") newProfile = DifficultyProfile.Bored();

            if (newProfile != null && AntigravityGameDirector.Instance != null)
            {
                AntigravityGameDirector.Instance.currentProfile = newProfile;
                AntigravityGameDirector.Instance.ApplyProfileToGame(newProfile);
                Debug.Log($"[DevConsole] Set difficulty profile to {diff}.");
            }
            else
            {
                Debug.LogWarning($"[DevConsole] Invalid difficulty profile '{diff}' or Director not ready.");
            }
            return;
        }

        switch (cmd)
        {
            case "/level up":
            case "/levelup":
                if (XPManager.Instance != null) XPManager.Instance.LevelUp();
                break;

            case "/sumon":
            case "/summonboss":
                BossSummonManager bsm = FindObjectOfType<BossSummonManager>();
                if (bsm != null) bsm.SummonBoss();
                break;

            case "/bossloc":
                GameObject playerObj = GameObject.FindGameObjectWithTag("Player");
                if (playerObj != null)
                {
                    // Target boss coordinates: X=347.5605, Y=9.246637, Z=607.7576
                    // Offset slightly to X=353.5f, Y=9.25f, Z=613.5f so player doesn't spawn inside the boss collider
                    Vector3 targetPos = new Vector3(347.5605f + 6f, 9.246637f, 607.7576f + 6f);

                    CharacterController cc = playerObj.GetComponent<CharacterController>();
                    if (cc != null) cc.enabled = false;
                    playerObj.transform.position = targetPos;
                    if (cc != null) cc.enabled = true;
                    Debug.Log($"[DevConsole] Teleported player to {targetPos} near boss coordinates.");
                }
                break;

            case "/killboss":
                BossSummonManager bsm2 = FindObjectOfType<BossSummonManager>();
                if (bsm2 != null) bsm2.KillBoss();
                break;

            case "/killallzombies":
            case "/killallzombie":
                ZombieHealth[] zombies = FindObjectsOfType<ZombieHealth>();
                int count = 0;
                foreach (var z in zombies)
                {
                    if (!z.IsDead) 
                    {
                        z.TakeDamage(99999f);
                        count++;
                    }
                }
                Debug.Log($"[DevConsole] Killed {count} zombies.");
                break;

            case "/logs":
            case "/openlogs":
                Application.OpenURL("file://" + Application.persistentDataPath);
                Debug.Log($"[DevConsole] Opened log folder: {Application.persistentDataPath}");
                break;

            default:
                Debug.LogWarning($"[DevConsole] Unknown command: {cmd}");
                break;
        }
    }
}
