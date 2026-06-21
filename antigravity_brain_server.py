import json
import os
import sys
import socket
import datetime
import re
from http.server import HTTPServer, BaseHTTPRequestHandler

LOG_FILE_PATH = "antigravity_server_log.txt"
HISTORY_FILE_PATH = "antigravity_master_history.log"

# Global session buffer and in-memory metrics store
session_buffer = []

metrics_store = {
    "total_sessions": 0,
    "active_session": False,
    "session_start_time": None,
    "evaluations": [],  # list of evaluations in active/recent sessions
    "qc_rejections": 0,
    "label_distribution": {
        "Balanced": 0,
        "Frustrated": 0,
        "Struggling": 0,
        "Beginner": 0,
        "Kiter": 0,
        "Bored": 0,
        "Skilled": 0
    },
    "recent_outcomes": []
}

def append_to_session(message):
    global session_buffer
    timestamp = datetime.datetime.now().strftime("%Y-%m-%d %H:%M:%S")
    formatted_msg = f"[{timestamp}] {message}"
    session_buffer.append(formatted_msg)
    
    # Keep session buffer size in check
    if len(session_buffer) > 1000:
        session_buffer.pop(0)

def write_session_to_disk(reason):
    global session_buffer
    timestamp = datetime.datetime.now().strftime("%Y-%m-%d %H:%M:%S")
    session_buffer.append(f"[{timestamp}] === SESSION ENDED: {reason} ===")
    
    full_session_log = "\n".join(session_buffer) + "\n\n"
    
    try:
        with open(LOG_FILE_PATH, "w", encoding="utf-8") as f:
            f.write(full_session_log)
        print(f"[SERVER LOG] Consolidated session log written to: {LOG_FILE_PATH}")
    except Exception as e:
        print(f"Error writing session log to file: {e}")
        
    try:
        with open(HISTORY_FILE_PATH, "a", encoding="utf-8") as f:
            f.write(full_session_log)
        print(f"[SERVER LOG] Session appended to history: {HISTORY_FILE_PATH}")
    except Exception as e:
        print(f"Error appending session to master history file: {e}")

def load_historical_metrics():
    global metrics_store
    if not os.path.exists(HISTORY_FILE_PATH):
        return
    try:
        with open(HISTORY_FILE_PATH, 'r', encoding='utf-8') as f:
            content = f.read()
        
        # Simple regex and string counts to bootstrap stats
        starts = len(re.findall(r"=== NEW SESSION STARTED", content))
        metrics_store["total_sessions"] = starts
        
        # Count historical states
        for match in re.findall(r"Inference:\s+Player is '([^']+)'", content):
            if match in metrics_store["label_distribution"]:
                metrics_store["label_distribution"][match] += 1
                
        # Count QC rejections
        rejections = len(re.findall(r"QC WARN:", content))
        metrics_store["qc_rejections"] = rejections
        
        print(f"[METRICS] Successfully loaded historical metrics: {starts} sessions, {rejections} QC rejections.")
    except Exception as e:
        print(f"Error loading historical metrics: {e}")

# HTML Dashboard Template
DASHBOARD_HTML = """<!DOCTYPE html>
<html lang="en">
<head>
    <meta charset="UTF-8">
    <meta name="viewport" content="width=device-width, initial-scale=1.0">
    <title>GreySword // Antigravity AI Director Dashboard</title>
    <link href="https://fonts.googleapis.com/css2?family=Outfit:wght@300;400;600;700&family=JetBrains+Mono:wght@400;700&display=swap" rel="stylesheet">
    <script src="https://cdn.jsdelivr.net/npm/chart.js"></script>
    <style>
        :root {
            --bg-base: #0b0f19;
            --bg-card: rgba(17, 24, 39, 0.7);
            --border-color: rgba(255, 255, 255, 0.08);
            --accent-blue: #00f2fe;
            --accent-purple: #7f00ff;
            --text-main: #f3f4f6;
            --text-muted: #9ca3af;
            --success: #10b981;
            --warning: #f59e0b;
            --danger: #ef4444;
        }
        * {
            box-sizing: border-box;
            margin: 0;
            padding: 0;
        }
        body {
            background-color: var(--bg-base);
            color: var(--text-main);
            font-family: 'Outfit', sans-serif;
            padding: 24px;
            min-height: 100vh;
        }
        .container {
            max-width: 1300px;
            margin: 0 auto;
        }
        header {
            display: flex;
            justify-content: space-between;
            align-items: center;
            border-bottom: 1px solid var(--border-color);
            padding-bottom: 20px;
            margin-bottom: 24px;
        }
        .brand {
            display: flex;
            align-items: center;
            gap: 12px;
        }
        .brand h1 {
            font-size: 24px;
            font-weight: 700;
            letter-spacing: -0.5px;
            background: linear-gradient(135deg, var(--accent-blue) 0%, #4facfe 100%);
            -webkit-background-clip: text;
            -webkit-text-fill-color: transparent;
        }
        .badge {
            display: inline-flex;
            align-items: center;
            gap: 6px;
            padding: 6px 12px;
            border-radius: 20px;
            font-size: 13px;
            font-weight: 600;
            text-transform: uppercase;
            border: 1px solid rgba(255, 255, 255, 0.1);
        }
        .badge.active {
            background-color: rgba(16, 185, 129, 0.15);
            color: var(--success);
            border-color: rgba(16, 185, 129, 0.3);
        }
        .badge.idle {
            background-color: rgba(156, 163, 175, 0.1);
            color: var(--text-muted);
        }
        .pulse-dot {
            width: 8px;
            height: 8px;
            background-color: currentColor;
            border-radius: 50%;
            display: inline-block;
        }
        .badge.active .pulse-dot {
            animation: pulse 1.5s infinite;
        }
        @keyframes pulse {
            0% { transform: scale(0.9); opacity: 0.6; }
            50% { transform: scale(1.2); opacity: 1; }
            100% { transform: scale(0.9); opacity: 0.6; }
        }
        .grid-kpi {
            display: grid;
            grid-template-columns: repeat(auto-fit, minmax(220px, 1fr));
            gap: 20px;
            margin-bottom: 24px;
        }
        .card {
            background: var(--bg-card);
            backdrop-filter: blur(12px);
            border: 1px solid var(--border-color);
            border-radius: 12px;
            padding: 20px;
            position: relative;
            overflow: hidden;
            display: flex;
            flex-direction: column;
            justify-content: space-between;
        }
        .card::before {
            content: '';
            position: absolute;
            top: 0;
            left: 0;
            width: 100%;
            height: 3px;
            background: linear-gradient(90deg, var(--accent-blue), var(--accent-purple));
            opacity: 0.8;
        }
        .card h3 {
            font-size: 14px;
            font-weight: 600;
            color: var(--text-muted);
            text-transform: uppercase;
            letter-spacing: 0.5px;
            margin-bottom: 8px;
        }
        .card .value {
            font-size: 32px;
            font-weight: 700;
            color: #ffffff;
            margin-top: auto;
        }
        .grid-main {
            display: grid;
            grid-template-columns: 2fr 1fr;
            gap: 24px;
            margin-bottom: 24px;
        }
        @media(max-width: 900px) {
            .grid-main { grid-template-columns: 1fr; }
        }
        .panel {
            background: var(--bg-card);
            backdrop-filter: blur(12px);
            border: 1px solid var(--border-color);
            border-radius: 12px;
            padding: 24px;
        }
        .panel h2 {
            font-size: 18px;
            font-weight: 600;
            margin-bottom: 16px;
            border-bottom: 1px solid var(--border-color);
            padding-bottom: 8px;
            display: flex;
            justify-content: space-between;
            align-items: center;
        }
        .chart-container {
            position: relative;
            height: 300px;
            width: 100%;
        }
        .tabs {
            display: flex;
            gap: 12px;
            margin-bottom: 20px;
            border-bottom: 1px solid var(--border-color);
            padding-bottom: 8px;
        }
        .tab-btn {
            background: transparent;
            border: none;
            color: var(--text-muted);
            font-family: inherit;
            font-size: 15px;
            font-weight: 600;
            padding: 8px 16px;
            cursor: pointer;
            border-radius: 6px;
            transition: all 0.2s ease;
        }
        .tab-btn:hover {
            color: #ffffff;
            background: rgba(255, 255, 255, 0.05);
        }
        .tab-btn.active {
            color: var(--accent-blue);
            background: rgba(0, 242, 254, 0.1);
        }
        .tab-content {
            display: none;
        }
        .tab-content.active {
            display: block;
        }
        .stream-table {
            width: 100%;
            border-collapse: collapse;
            font-size: 14px;
            margin-top: 10px;
        }
        .stream-table th, .stream-table td {
            text-align: left;
            padding: 12px;
            border-bottom: 1px solid var(--border-color);
        }
        .stream-table th {
            color: var(--text-muted);
            font-weight: 600;
        }
        .stream-table tr:hover td {
            background-color: rgba(255, 255, 255, 0.02);
        }
        .badge-state {
            padding: 4px 8px;
            border-radius: 4px;
            font-size: 12px;
            font-weight: 700;
            text-transform: uppercase;
        }
        .state-balanced { background: rgba(16, 185, 129, 0.15); color: var(--success); }
        .state-frustrated { background: rgba(239, 68, 68, 0.15); color: var(--danger); }
        .state-struggling { background: rgba(245, 158, 11, 0.15); color: var(--warning); }
        .state-beginner { background: rgba(59, 130, 246, 0.15); color: #3b82f6; }
        .state-kiter { background: rgba(139, 92, 246, 0.15); color: #8b5cf6; }
        .state-bored { background: rgba(236, 72, 153, 0.15); color: #ec4899; }
        .state-skilled { background: rgba(16, 185, 129, 0.15); color: var(--success); }
        
        .console-box {
            background-color: #05070d;
            border: 1px solid var(--border-color);
            border-radius: 8px;
            padding: 16px;
            font-family: 'JetBrains Mono', monospace;
            font-size: 13px;
            color: #a7f3d0;
            height: 350px;
            overflow-y: auto;
            white-space: pre-wrap;
            line-height: 1.5;
        }
        .info-grid {
            display: grid;
            grid-template-columns: repeat(auto-fit, minmax(280px, 1fr));
            gap: 20px;
        }
        .info-item {
            border: 1px solid var(--border-color);
            padding: 16px;
            border-radius: 8px;
            background: rgba(255,255,255,0.01);
        }
        .info-item h4 {
            font-size: 15px;
            margin-bottom: 8px;
            color: var(--accent-blue);
        }
        .info-item p {
            font-size: 14px;
            color: var(--text-muted);
            line-height: 1.6;
        }
    </style>
</head>
<body>
    <div class="container">
        <header>
            <div class="brand">
                <svg width="28" height="28" viewBox="0 0 24 24" fill="none" xmlns="http://www.w3.org/2000/svg">
                    <path d="M12 2L2 22H22L12 2Z" stroke="#00f2fe" stroke-width="2" stroke-linecap="round" stroke-linejoin="round"/>
                    <path d="M12 6L4 20H20L12 6Z" fill="rgba(0, 242, 254, 0.2)"/>
                    <circle cx="12" cy="14" r="2" fill="#7f00ff"/>
                </svg>
                <h1>GreySword // Antigravity Director</h1>
            </div>
            <div id="session-badge" class="badge idle">
                <span class="pulse-dot"></span>
                <span id="session-status-text">Server Idle</span>
            </div>
        </header>

        <div class="grid-kpi">
            <div class="card">
                <h3>Total Sessions</h3>
                <div id="kpi-sessions" class="value">-</div>
            </div>
            <div class="card">
                <h3>Active Evaluations</h3>
                <div id="kpi-evals" class="value">-</div>
            </div>
            <div class="card">
                <h3>QC Rejections / Caps</h3>
                <div id="kpi-rejections" class="value">-</div>
            </div>
            <div class="card">
                <h3>Recent Survival Avg</h3>
                <div id="kpi-survival" class="value">-</div>
            </div>
        </div>

        <div class="grid-main">
            <div class="panel">
                <div class="tabs">
                    <button class="tab-btn active" onclick="switchTab('tab-live')">Live Session Stream</button>
                    <button class="tab-btn" onclick="switchTab('tab-history')">Decision History</button>
                    <button class="tab-btn" onclick="switchTab('tab-guardrails')">Guardrails & Responsible AI</button>
                </div>

                <div id="tab-live" class="tab-content active">
                    <h2>Session Runtime Console</h2>
                    <div id="console" class="console-box">Waiting for session logs...</div>
                </div>

                <div id="tab-history" class="tab-content">
                    <h2>Evaluations Trace Log</h2>
                    <div style="overflow-x: auto; max-height: 400px;">
                        <table class="stream-table">
                            <thead>
                                <tr>
                                    <th>Time</th>
                                    <th>State / Label</th>
                                    <th>Observation Snapshot</th>
                                    <th>Action Taken</th>
                                    <th>QC Status</th>
                                </tr>
                            </thead>
                            <tbody id="history-tbody">
                                <tr>
                                    <td colspan="5" style="text-align: center; color: var(--text-muted);">No evaluations recorded yet in this session.</td>
                                </tr>
                            </tbody>
                        </table>
                    </div>
                </div>

                <div id="tab-guardrails" class="tab-content">
                    <h2>Responsible AI Safety System</h2>
                    <div class="info-grid">
                        <div class="info-item">
                            <h4>🛡️ Health Veto Rule</h4>
                            <p>If the player's health drops below 30%, the system automatically flags a safety constraint. Any proposed difficulty spike or parameter increase is forcefully rejected, and the previous configuration is preserved to prevent frustration and rage-quitting.</p>
                        </div>
                        <div class="info-item">
                            <h4>🚫 Hard Param Caps</h4>
                            <p>Hard-coded guardrails in the server policy cap zombie parameters: Max speed multiplier is restricted to 1.8×, damage multiplier to 2.0×, and the maximum concurrent alive zombies is strictly capped at 14. This prevents the system from generating impossible play situations.</p>
                        </div>
                        <div class="info-item">
                            <h4>🔍 Audit Trails</h4>
                            <p>Every single observation, classification, decision, proposed change, and QC rejection is logged locally on the client (antigravity_log.txt), inside the server command interface, and stored persistently in the master history logs.</p>
                        </div>
                    </div>
                </div>
            </div>

            <div class="panel" style="display: flex; flex-direction: column; gap: 20px;">
                <div>
                    <h2>State Distribution</h2>
                    <div class="chart-container">
                        <canvas id="stateChart"></canvas>
                    </div>
                </div>
            </div>
        </div>
    </div>

    <script>
        let stateChartInstance = null;

        function switchTab(tabId) {
            document.querySelectorAll('.tab-content').forEach(c => c.classList.remove('active'));
            document.querySelectorAll('.tab-btn').forEach(b => b.classList.remove('active'));
            document.getElementById(tabId).classList.add('active');
            
            // Find button corresponding to this tab
            const indexMap = {
                'tab-live': 0,
                'tab-history': 1,
                'tab-guardrails': 2
            };
            document.querySelectorAll('.tab-btn')[indexMap[tabId]].classList.add('active');
        }

        async function updateDashboard() {
            try {
                const res = await fetch('/metrics');
                if (!res.ok) return;
                const data = await res.json();

                // Status Badge
                const badge = document.getElementById('session-badge');
                const statusText = document.getElementById('session-status-text');
                if (data.active_session) {
                    badge.className = 'badge active';
                    statusText.textContent = 'Active Session';
                } else {
                    badge.className = 'badge idle';
                    statusText.textContent = 'Server Idle';
                }

                // KPIs
                document.getElementById('kpi-sessions').textContent = data.total_sessions;
                document.getElementById('kpi-evals').textContent = data.evaluations.length;
                document.getElementById('kpi-rejections').textContent = data.qc_rejections;

                // Calculate survival avg or default
                let totalSurvival = 0;
                let survivalCount = 0;
                data.evaluations.forEach(ev => {
                    if (ev.time_survived) {
                        totalSurvival = Math.max(totalSurvival, ev.time_survived);
                        survivalCount = 1;
                    }
                });
                document.getElementById('kpi-survival').textContent = survivalCount > 0 ? Math.round(totalSurvival) + 's' : 'N/A';

                // Update Console
                const consoleBox = document.getElementById('console');
                if (data.session_buffer && data.session_buffer.length > 0) {
                    const scrollAtBottom = consoleBox.scrollHeight - consoleBox.clientHeight <= consoleBox.scrollTop + 1;
                    consoleBox.textContent = data.session_buffer.join('\\n');
                    if (scrollAtBottom) {
                        consoleBox.scrollTop = consoleBox.scrollHeight;
                    }
                } else {
                    consoleBox.textContent = 'Waiting for session logs...';
                }

                // Update History Table
                const tbody = document.getElementById('history-tbody');
                if (data.evaluations && data.evaluations.length > 0) {
                    tbody.innerHTML = '';
                    // Display latest first
                    const reversedEvals = [...data.evaluations].reverse();
                    reversedEvals.forEach(ev => {
                        const tr = document.createElement('tr');
                        
                        const tdTime = document.createElement('td');
                        tdTime.textContent = ev.timestamp || 'N/A';
                        tr.appendChild(tdTime);

                        const tdLabel = document.createElement('td');
                        const labelSpan = document.createElement('span');
                        labelSpan.className = 'badge-state state-' + (ev.label || 'balanced').toLowerCase();
                        labelSpan.textContent = ev.label || 'Balanced';
                        tdLabel.appendChild(labelSpan);
                        tr.appendChild(tdLabel);

                        const tdObs = document.createElement('td');
                        tdObs.textContent = ev.observation || '';
                        tr.appendChild(tdObs);

                        const tdAction = document.createElement('td');
                        tdAction.textContent = ev.action || '';
                        tr.appendChild(tdAction);

                        const tdQC = document.createElement('td');
                        if (ev.rejected) {
                            tdQC.style.color = 'var(--danger)';
                            tdQC.textContent = 'REJECTED';
                            tdQC.title = ev.rejectReason || '';
                        } else {
                            tdQC.style.color = 'var(--success)';
                            tdQC.textContent = 'PASS';
                        }
                        tr.appendChild(tdQC);

                        tbody.appendChild(tr);
                    });
                }

                // Update Chart
                const labels = Object.keys(data.label_distribution);
                const values = Object.values(data.label_distribution);
                
                if (!stateChartInstance) {
                    const ctx = document.getElementById('stateChart').getContext('2d');
                    stateChartInstance = new Chart(ctx, {
                        type: 'doughnut',
                        data: {
                            labels: labels,
                            datasets: [{
                                data: values,
                                backgroundColor: [
                                    '#10b981', // Balanced
                                    '#ef4444', // Frustrated
                                    '#f59e0b', // Struggling
                                    '#3b82f6', // Beginner
                                    '#8b5cf6', // Kiter
                                    '#ec4899', // Bored
                                    '#00f2fe'  // Skilled
                                ],
                                borderWidth: 1,
                                borderColor: 'rgba(255,255,255,0.08)'
                            }]
                        },
                        options: {
                            responsive: true,
                            maintainAspectRatio: false,
                            plugins: {
                                legend: {
                                    position: 'bottom',
                                    labels: { color: '#9ca3af', font: { family: 'Outfit' } }
                                }
                            }
                        }
                    });
                } else {
                    stateChartInstance.data.datasets[0].data = values;
                    stateChartInstance.update();
                }

            } catch (err) {
                console.error("Dashboard update failed: ", err);
            }
        }

        // Poll every 3 seconds
        setInterval(updateDashboard, 3000);
        updateDashboard();
    </script>
</body>
</html>
"""

class AntigravityBrainHandler(BaseHTTPRequestHandler):
    def send_cors_headers(self):
        self.send_header('Access-Control-Allow-Origin', '*')
        self.send_header('Access-Control-Allow-Methods', 'GET, POST, OPTIONS')
        self.send_header('Access-Control-Allow-Headers', 'Content-Type')

    def do_OPTIONS(self):
        self.send_response(200)
        self.send_cors_headers()
        self.end_headers()

    def do_GET(self):
        global metrics_store
        
        # 1. Health check or dashboard index
        if self.path == '/' or self.path == '/dashboard':
            self.send_response(200)
            self.send_header('Content-Type', 'text/html; charset=utf-8')
            self.send_cors_headers()
            self.end_headers()
            self.wfile.write(DASHBOARD_HTML.encode('utf-8'))
            
        # 2. Metrics endpoint
        elif self.path == '/metrics':
            self.send_response(200)
            self.send_header('Content-Type', 'application/json')
            self.send_cors_headers()
            self.end_headers()
            
            # Pack metrics structure
            response_data = metrics_store.copy()
            response_data["session_buffer"] = session_buffer
            
            self.wfile.write(json.dumps(response_data).encode('utf-8'))
            
        # 3. Status endpoint
        elif self.path == '/status':
            self.send_response(200)
            self.send_header('Content-Type', 'application/json')
            self.send_cors_headers()
            self.end_headers()
            
            status_data = {
                "active_session": metrics_store["active_session"],
                "total_sessions": metrics_store["total_sessions"],
                "evaluations_count": len(metrics_store["evaluations"]),
                "qc_rejections": metrics_store["qc_rejections"]
            }
            self.wfile.write(json.dumps(status_data).encode('utf-8'))
        else:
            self.send_response(404)
            self.end_headers()

    def do_POST(self):
        global session_buffer, metrics_store
        
        content_length = int(self.headers.get('Content-Length', 0))
        post_data = self.rfile.read(content_length) if content_length > 0 else b""
        
        if self.path == '/session_start':
            try:
                session_buffer = []
                metrics_store["evaluations"] = []  # reset evaluations for this active session
                metrics_store["active_session"] = True
                metrics_store["session_start_time"] = datetime.datetime.now().strftime("%Y-%m-%d %H:%M:%S")
                
                # Increment session count
                metrics_store["total_sessions"] += 1
                
                start_time = datetime.datetime.now().strftime("%Y-%m-%d %H:%M:%S")
                append_to_session(f"=== NEW SESSION STARTED at {start_time} ===")
                print(f"\n[SESSION START] Cleared buffer, starting new session recording...")
                
                self.send_response(200)
                self.send_cors_headers()
                self.end_headers()
            except Exception as e:
                print(f"Session Start Error: {e}")
                self.send_response(500)
                self.end_headers()
                
        elif self.path == '/session_end':
            try:
                reason = "Unknown / Game Exited"
                if len(post_data) > 0:
                    try:
                        data = json.loads(post_data.decode('utf-8'))
                        reason = data.get('reason', reason)
                    except:
                        pass
                
                metrics_store["active_session"] = False
                print(f"\n[SESSION END] Session ended. Reason: {reason}")
                write_session_to_disk(reason)
                
                self.send_response(200)
                self.send_cors_headers()
                self.end_headers()
            except Exception as e:
                print(f"Session End Error: {e}")
                self.send_response(500)
                self.end_headers()
                
        elif self.path == '/evaluate':
            try:
                data = json.loads(post_data.decode('utf-8')) if len(post_data) > 0 else {}
                snapshot = data.get('snapshot', {})
                current_profile = data.get('currentProfile', {})
                
                kills_per_minute = snapshot.get('killsPerMinute', 0.0)
                damage_per_minute = snapshot.get('damagePerMinute', 0.0)
                avg_health = snapshot.get('avgHealthAfterFight', 1.0)
                accuracy = snapshot.get('accuracy', 1.0)
                death_count = snapshot.get('deathCount', 0)
                time_survived = snapshot.get('timeSurvived', 0.0)
                is_kiting = snapshot.get('isKiting', False)
                avg_dist = snapshot.get('avgDistFromZombies', 0.0)

                proposed = current_profile.copy()
                
                kill_score = min(1.0, kills_per_minute / 3.0)
                resist_score = min(1.0, max(0.0, 1.0 - (damage_per_minute / 40.0)))
                skill_score = (kill_score * 0.4) + (resist_score * 0.3) + (min(1.0, time_survived / 300.0) * 0.2) + (avg_health * 0.1)
                
                death_score = min(1.0, death_count / 3.0)
                damage_score = min(1.0, damage_per_minute / 40.0)
                frustration_score = (death_score * 0.6) + (damage_score * 0.4)
                
                boredom_score = (kill_score * 0.5) + (avg_health * 0.3) + (min(1.0, time_survived / 300.0) * 0.2)
                kiting_score = 1.0 if is_kiting else min(1.0, avg_dist / 15.0)
                
                label = "Balanced"
                decision = "Keep difficulty balanced."
                action = "No profile parameters modified."
                
                if frustration_score > 0.7:
                    label = "Frustrated"
                    proposed['zombieDamageMultiplier'] = max(0.6, proposed.get('zombieDamageMultiplier', 1.0) - 0.15)
                    proposed['maxAliveZombies'] = max(2, proposed.get('maxAliveZombies', 6) - 1)
                    proposed['spawnInterval'] = min(25.0, proposed.get('spawnInterval', 15.0) + 5.0)
                    proposed['foodDropChance'] = min(0.5, proposed.get('foodDropChance', 0.15) + 0.1)
                    decision = "Player shows high damage intake and frequent deaths. Frustrated state inferred."
                    action = f"Reduced difficulty: Damage={proposed['zombieDamageMultiplier']:.2f}, MaxAlive={proposed['maxAliveZombies']}, SpawnInterval={proposed['spawnInterval']:.1f}s"
                elif skill_score < 0.25 and frustration_score > 0.5:
                    label = "Struggling"
                    proposed['zombieDamageMultiplier'] = max(0.5, proposed.get('zombieDamageMultiplier', 1.0) - 0.1)
                    proposed['zombieSpeedMultiplier'] = max(0.6, proposed.get('zombieSpeedMultiplier', 1.0) - 0.1)
                    proposed['foodDropChance'] = min(0.45, proposed.get('foodDropChance', 0.15) + 0.08)
                    decision = "Low combat skill metrics. Player is struggling."
                    action = f"Easing pressure: Speed={proposed['zombieSpeedMultiplier']:.2f}, Damage={proposed['zombieDamageMultiplier']:.2f}, FoodDrop={proposed['foodDropChance']:.2f}"
                elif skill_score < 0.35:
                    label = "Beginner"
                    proposed['maxAliveZombies'] = min(14, proposed.get('maxAliveZombies', 6) + 1)
                    decision = "Beginner player. Ramping up slowly."
                    action = f"Gentle challenge ramp: MaxAlive={proposed['maxAliveZombies']}"
                elif kiting_score > 0.7:
                    label = "Kiter"
                    proposed['zombieDetectionRange'] = min(22.0, proposed.get('zombieDetectionRange', 12.0) + 2.0)
                    proposed['zombieChaseRange'] = min(30.0, proposed.get('zombieChaseRange', 18.0) + 3.0)
                    decision = "Player kiting detected. Countermeasures applied."
                    action = f"Increased pathfinding range: DetectionRange={proposed['zombieDetectionRange']:.0f}, ChaseRange={proposed['zombieChaseRange']:.0f}"
                elif skill_score > 0.65 and boredom_score > 0.6:
                    label = "Bored"
                    proposed['zombieSpeedMultiplier'] = min(1.8, proposed.get('zombieSpeedMultiplier', 1.0) + 0.08)
                    proposed['maxAliveZombies'] = min(14, proposed.get('maxAliveZombies', 6) + 2)
                    proposed['specialZombieChance'] = min(0.5, proposed.get('specialZombieChance', 0.1) + 0.1)
                    proposed['spawnInterval'] = max(5.0, proposed.get('spawnInterval', 15.0) - 3.0)
                    decision = "Player easily clearing waves without taking damage. Bored state inferred."
                    action = f"Increased engagement settings: Speed={proposed['zombieSpeedMultiplier']:.2f}, MaxAlive={proposed['maxAliveZombies']}, SpecialChance={proposed['specialZombieChance']:.2f}"
                elif skill_score > 0.65:
                    label = "Skilled"
                    proposed['zombieHealthMultiplier'] = min(2.5, proposed.get('zombieHealthMultiplier', 1.0) + 0.1)
                    proposed['maxAliveZombies'] = min(14, proposed.get('maxAliveZombies', 6) + 1)
                    decision = "Player exhibiting high efficiency. Skilled state inferred."
                    action = f"Increasing challenge parameters: HP={proposed['zombieHealthMultiplier']:.2f}, MaxAlive={proposed['maxAliveZombies']}"
                
                # Quality-Control (QC) Validation
                rejected = False
                reject_reason = ""
                
                if proposed.get('maxAliveZombies', 6) > 14:
                    reject_reason += " MaxAlive exceeds absolute cap 14."
                    proposed['maxAliveZombies'] = 14
                    rejected = True
                    
                if proposed.get('zombieDamageMultiplier', 1.0) > 2.0:
                    reject_reason += " DamageMult exceeds cap 2.0."
                    proposed['zombieDamageMultiplier'] = 2.0
                    rejected = True
 
                if proposed.get('zombieSpeedMultiplier', 1.0) > 1.8:
                    reject_reason += " SpeedMult exceeds escape cap 1.8."
                    proposed['zombieSpeedMultiplier'] = 1.8
                    rejected = True
                    
                if avg_health < 0.3:
                    if (proposed.get('maxAliveZombies', 6) > current_profile.get('maxAliveZombies', 6) or
                        proposed.get('zombieDamageMultiplier', 1.0) > current_profile.get('zombieDamageMultiplier', 1.0) or
                        proposed.get('zombieSpeedMultiplier', 1.0) > current_profile.get('zombieSpeedMultiplier', 1.0)):
                          reject_reason += " Reject difficulty increase due to critical player health."
                          proposed = current_profile.copy()
                          rejected = True
                          
                observation = f"Kills/Min={kills_per_minute:.1f} Dmg/Min={damage_per_minute:.1f} Health={avg_health*100:.0f}% Deaths={death_count}"
 
                # Print beautifully on host terminal
                log_border = "=================================================="
                eval_log = f"\n{log_border}\n[EVALUATION REQUEST]\nObservation: {observation}\nInference:   Player is '{label}'\nDecision:    {decision}\nAction:      {action}"
                if rejected:
                    eval_log += f"\nQC WARN:     Decision rejected/capped! Reason:{reject_reason}"
                eval_log += f"\n{log_border}"
                
                print(eval_log)
                append_to_session(eval_log)
                
                # Update in-memory metrics
                if label in metrics_store["label_distribution"]:
                    metrics_store["label_distribution"][label] += 1
                if rejected:
                    metrics_store["qc_rejections"] += 1
                    
                timestamp = datetime.datetime.now().strftime("%H:%M:%S")
                metrics_store["evaluations"].append({
                    "timestamp": timestamp,
                    "label": label,
                    "observation": observation,
                    "decision": decision,
                    "action": action,
                    "rejected": rejected,
                    "rejectReason": reject_reason,
                    "time_survived": time_survived
                })
 
                response_data = {
                    "profile": proposed,
                    "label": label,
                    "observation": observation,
                    "decision": decision,
                    "action": action,
                    "rejected": rejected,
                    "rejectReason": reject_reason
                }
 
                self.send_response(200)
                self.send_header('Content-Type', 'application/json')
                self.send_cors_headers()
                self.end_headers()
                self.wfile.write(json.dumps(response_data).encode('utf-8'))
            except Exception as e:
                print(f"Evaluation Error: {e}")
                self.send_response(500)
                self.end_headers()
                
        elif self.path == '/log':
            try:
                data = json.loads(post_data.decode('utf-8')) if len(post_data) > 0 else {}
                log_msg = data.get('log', '')
                log_line = f"[GAME RUNTIME OUTCOME] {log_msg}"
                print(log_line)
                append_to_session(log_line)
                self.send_response(200)
                self.send_cors_headers()
                self.end_headers()
            except Exception as e:
                self.send_response(500)
                self.end_headers()
        else:
            self.send_response(404)
            self.end_headers()
 
def run_server(port=8080):
    # reset current server log session info
    try:
        with open(LOG_FILE_PATH, "w", encoding="utf-8") as f:
            f.write(f"=== Antigravity Server Log - Waiting for Game Session ===\n")
    except Exception as e:
        print(f"Error resetting log file: {e}")

    # Load stats from past sessions
    load_historical_metrics()

    # Find local network IP address
    s = socket.socket(socket.AF_INET, socket.SOCK_DGRAM)
    try:
        s.connect(('8.8.8.8', 80))
        local_ip = s.getsockname()[0]
    except Exception:
        local_ip = '127.0.0.1'
    finally:
        s.close()
 
    server_address = ('', port)
    httpd = HTTPServer(server_address, AntigravityBrainHandler)
    print(f"\n==================================================")
    print(f"   ANTIGRAVITY GAME BRAIN SERVER RUNNING")
    print(f"==================================================")
    print(f" Local Address:   http://localhost:{port}")
    print(f" LAN IP Address:  http://{local_ip}:{port}")
    print(f" Log File Path:   {LOG_FILE_PATH}")
    print(f" History Path:    {HISTORY_FILE_PATH}")
    print(f"")
    print(f" INSTRUCTION FOR MOBILE BUILD:")
    print(f" 1. Connect phone to the SAME Wi-Fi network as this PC.")
    print(f" 2. Open Dev Console in the game on your phone.")
    print(f" 3. Type command: /server http://{local_ip}:{port}")
    print(f" 4. The game will now query this PC server in real time!")
    print(f"==================================================\n")
    
    try:
        httpd.serve_forever()
    except KeyboardInterrupt:
        print("\nShutting down brain server...")
 
if __name__ == '__main__':
    # Render binds to PORT environment variable
    port_val = os.environ.get('PORT', 8080)
    try:
        port_num = int(port_val)
    except ValueError:
        port_num = 8080
    run_server(port=port_num)
