using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// AgenticDecisionLogger — Records every Antigravity AI decision with full trace.
/// Shows last N entries on-screen and writes to a persistent log file.
///
/// LOG FORMAT (each entry):
///   [HH:MM:SS] OBSERVATION | INFERENCE | DECISION | ACTION | OUTCOME
///
/// SETUP:
/// - Attach to GameDirector GameObject.
/// - Optionally assign a UI Text element for on-screen display.
/// - Log file saved to: Application.persistentDataPath/antigravity_log.txt
///   (On Windows editor: C:/Users/<user>/AppData/LocalLow/<company>/<product>/)
/// </summary>
public class AgenticDecisionLogger : MonoBehaviour
{
    [Header("On-Screen Display")]
    [Tooltip("Optional UI Text element to show recent log entries. If left empty, uses a foolproof IMGUI overlay.")]
    public Text onScreenLogText;

    [Tooltip("How many log entries to display on screen")]
    [Range(1, 100)]
    public int maxVisibleEntries = 50;

    [Header("File Logging")]
    [Tooltip("Write log to disk (for hackathon judges to review)")]
    public bool writeToFile = true;

    // ─── Singleton ─────────────────────────────────────────────────────────────

    public static AgenticDecisionLogger Instance { get; private set; }

    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
    }

    // ─── Private State ─────────────────────────────────────────────────────────

    private List<AgenticLogEntry> entries = new List<AgenticLogEntry>();
    private string logFilePath;

    // The last entry's index — updated with outcome next cycle
    private int pendingOutcomeIndex = -1;

    // IMGUI State
    private bool showLogs = false;
    private Vector2 scrollPos = Vector2.zero;

    void Start()
    {
        if (Instance != this) return; // Prevent duplicate singletons from creating multiple UIs

        logFilePath = Path.Combine(Application.persistentDataPath, "antigravity_log.txt");
        string header = $"=== Antigravity Game Director Log — {System.DateTime.Now} ===\n";
        WriteToFile(header, false);
        Debug.Log($"[AgenticDecisionLogger] Log file: {logFilePath}");

        LogDecision("System Boot", "Initializing Antigravity AI Engine", "Boot Sequence", "Monitoring Player Activity...");

        // Repair any broken EventSystem for the user's OTHER UI elements (like bounty buttons)
        try
        {
            var brokenModule = FindObjectOfType<UnityEngine.EventSystems.StandaloneInputModule>();
            if (brokenModule != null)
            {
                GameObject esGO = brokenModule.gameObject;
                Destroy(brokenModule);
                esGO.AddComponent<UnityEngine.InputSystem.UI.InputSystemUIInputModule>();
            }

            if (FindObjectOfType<UnityEngine.EventSystems.EventSystem>() == null)
            {
                GameObject eventSystem = new GameObject("EventSystem");
                eventSystem.AddComponent<UnityEngine.EventSystems.EventSystem>();
                eventSystem.AddComponent<UnityEngine.InputSystem.UI.InputSystemUIInputModule>();
            }
        }
        catch (System.Exception ex)
        {
            Debug.LogError("[AgenticDecisionLogger] EventSystem Creation Crashed: " + ex.Message);
        }
    }

    // ─── Public API ────────────────────────────────────────────────────────────

    public void LogRealTimeEvent(string observation, string action)
    {
        LogDecision(observation, "Real-time stream update", "Acknowledge Event", action);
    }

    public void LogDecision(
        string observation,
        string inference,
        string decision,
        string action,
        bool   qcRejected     = false,
        string rejectReason   = "")
    {
        AgenticLogEntry entry = new AgenticLogEntry
        {
            timestamp   = System.DateTime.Now.ToString("HH:mm:ss"),
            observation = observation,
            inference   = inference,
            decision    = decision,
            action      = action,
            outcome     = "Pending next evaluation...",
            qcRejected  = qcRejected,
            rejectReason = rejectReason
        };

        entries.Add(entry);
        pendingOutcomeIndex = entries.Count - 1;

        string logLine = FormatEntry(entry);
        Debug.Log($"[Antigravity] {logLine}");
        WriteToFile(logLine + "\n", true);
        RefreshOnScreenDisplay();
    }

    public void RecordOutcome(string outcomeText)
    {
        if (pendingOutcomeIndex < 0 || pendingOutcomeIndex >= entries.Count) return;

        entries[pendingOutcomeIndex].outcome = outcomeText;

        string outcomeLine = $"  └─ OUTCOME: {outcomeText}";
        Debug.Log($"[Antigravity] {outcomeLine}");
        WriteToFile(outcomeLine + "\n", true);
        RefreshOnScreenDisplay();
    }

    public List<AgenticLogEntry> GetAllEntries() => entries;

    // ─── Helpers ───────────────────────────────────────────────────────────────

    void RefreshOnScreenDisplay()
    {
        // Only updates Canvas UI if assigned
        if (onScreenLogText == null) return;

        int start = Mathf.Max(0, entries.Count - maxVisibleEntries);
        System.Text.StringBuilder sb = new System.Text.StringBuilder();
        sb.AppendLine("<b>── Antigravity AI Log ──</b>");

        for (int i = start; i < entries.Count; i++)
        {
            AgenticLogEntry e = entries[i];
            
            sb.AppendLine($"<color=cyan>[{e.timestamp}]</color>");
            sb.AppendLine($"<b>Observation:</b> {e.observation}");
            sb.AppendLine($"<b>Inference:</b> {e.inference}");
            
            if (e.qcRejected)
                sb.AppendLine($"<b>Decision:</b> <color=yellow>QC REJECT: {e.rejectReason}</color>");
            else
                sb.AppendLine($"<b>Decision:</b> {e.decision}");
            
            sb.AppendLine($"<b>Action:</b> {e.action}");
            sb.AppendLine($"<b>Outcome:</b> <color=grey>{e.outcome}</color>");
            sb.AppendLine("──────────────────────────────────────────");
        }

        onScreenLogText.text = sb.ToString();
    }

    string FormatEntry(AgenticLogEntry e)
    {
        return $"[{e.timestamp}]\n" +
               $"  OBSERVATION: {e.observation}\n" +
               $"  INFERENCE:   {e.inference}\n" +
               $"  DECISION:    {e.decision}\n" +
               $"  ACTION:      {e.action}" +
               (e.qcRejected ? $"\n  QC REJECTED: {e.rejectReason}" : "");
    }

    void WriteToFile(string content, bool append)
    {
        if (!writeToFile) return;
        try
        {
            if (append)
                File.AppendAllText(logFilePath, content);
            else
                File.WriteAllText(logFilePath, content);
        }
        catch (System.Exception ex)
        {
            Debug.LogWarning($"[AgenticDecisionLogger] Could not write to file: {ex.Message}");
        }
    }

    // ─── Foolproof IMGUI ──────────────────────────────────────────────────────

    void OnGUI()
    {
        // Skip if Canvas Text is assigned or if game hasn't started yet
        if (onScreenLogText != null) return;
        if (GameUIManager.Instance != null && !GameUIManager.IsGamePlaying) return;

        // Button at top right below skull counter
        float btnWidth = 140f;
        float btnHeight = 35f;
        Rect btnRect = new Rect(Screen.width - btnWidth - 30f, 90f, btnWidth, btnHeight);

        if (GUI.Button(btnRect, "Brain Logs"))
        {
            showLogs = !showLogs;
        }

        if (showLogs)
        {
            // Panel on left side
            float panelWidth = Screen.width * 0.4f; // 40% of screen width
            float panelHeight = Screen.height * 0.8f;
            Rect panelRect = new Rect(20, Screen.height * 0.1f, panelWidth, panelHeight);

            // Draw a dark background
            GUI.Box(panelRect, "");
            GUI.Box(panelRect, "ANTIGRAVITY BRAIN LOGS");

            // Scroll View Area
            Rect viewRect = new Rect(panelRect.x + 10, panelRect.y + 30, panelRect.width - 20, panelRect.height - 40);
            
            // Calculate content height dynamically
            GUIStyle style = new GUIStyle(GUI.skin.label) { wordWrap = true, richText = true };
            float currentY = 0f;
            int start = Mathf.Max(0, entries.Count - maxVisibleEntries);
            
            // Pass 1: measure height
            for (int i = start; i < entries.Count; i++)
            {
                string txt = FormatRichText(entries[i]);
                currentY += style.CalcHeight(new GUIContent(txt), viewRect.width - 20f) + 15f;
            }

            Rect contentRect = new Rect(0, 0, viewRect.width - 20f, currentY);

            // Draw ScrollView
            scrollPos = GUI.BeginScrollView(viewRect, scrollPos, contentRect);

            currentY = 0f;
            for (int i = start; i < entries.Count; i++)
            {
                string txt = FormatRichText(entries[i]);
                float h = style.CalcHeight(new GUIContent(txt), viewRect.width - 20f);
                GUI.Label(new Rect(5, currentY, viewRect.width - 20f, h), txt, style);
                currentY += h + 15f; // Add padding between entries
            }

            GUI.EndScrollView();
        }
    }

    string FormatRichText(AgenticLogEntry e)
    {
        return $"<color=cyan>[{e.timestamp}]</color>\n" +
               $"<b>Observation:</b> {e.observation}\n" +
               $"<b>Inference:</b> {e.inference}\n" +
               $"<b>Decision:</b> {(e.qcRejected ? $"<color=yellow>QC REJECT: {e.rejectReason}</color>" : e.decision)}\n" +
               $"<b>Action:</b> {e.action}\n" +
               $"<b>Outcome:</b> <color=silver>{e.outcome}</color>\n" +
               $"────────────────────────────────────────";
    }
}

/// <summary>Single log entry struct.</summary>
[System.Serializable]
public class AgenticLogEntry
{
    public string timestamp;
    public string observation;
    public string inference;
    public string decision;
    public string action;
    public string outcome;
    public bool   qcRejected;
    public string rejectReason;
}
