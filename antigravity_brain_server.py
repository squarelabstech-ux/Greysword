import json
import socket
import datetime
from http.server import HTTPServer, BaseHTTPRequestHandler

LOG_FILE_PATH = "antigravity_server_log.txt"
HISTORY_FILE_PATH = "antigravity_master_history.log"

# Global session buffer
session_buffer = []

def append_to_session(message):
    global session_buffer
    timestamp = datetime.datetime.now().strftime("%Y-%m-%d %H:%M:%S")
    session_buffer.append(f"[{timestamp}] {message}")

def write_session_to_disk(reason):
    global session_buffer
    timestamp = datetime.datetime.now().strftime("%Y-%m-%d %H:%M:%S")
    session_buffer.append(f"[{timestamp}] === SESSION ENDED: {reason} ===")
    
    full_session_log = "\n".join(session_buffer) + "\n\n"
    
    # Overwrite the session log file so it contains exactly this session's logs
    try:
        with open(LOG_FILE_PATH, "w", encoding="utf-8") as f:
            f.write(full_session_log)
        print(f"[SERVER LOG] Consolidated session log written to: {LOG_FILE_PATH}")
    except Exception as e:
        print(f"Error writing session log to file: {e}")
        
    # Append to the master history file so past session logs are preserved
    try:
        with open(HISTORY_FILE_PATH, "a", encoding="utf-8") as f:
            f.write(full_session_log)
        print(f"[SERVER LOG] Session appended to history: {HISTORY_FILE_PATH}")
    except Exception as e:
        print(f"Error appending session to master history file: {e}")

class AntigravityBrainHandler(BaseHTTPRequestHandler):
    def do_POST(self):
        global session_buffer
        
        # Read content if present
        content_length = int(self.headers.get('Content-Length', 0))
        post_data = self.rfile.read(content_length) if content_length > 0 else b""
        
        if self.path == '/session_start':
            try:
                session_buffer = []
                start_time = datetime.datetime.now().strftime("%Y-%m-%d %H:%M:%S")
                append_to_session(f"=== NEW SESSION STARTED at {start_time} ===")
                print(f"\n[SESSION START] Cleared buffer, starting new session recording...")
                
                self.send_response(200)
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
                
                print(f"\n[SESSION END] Session ended. Reason: {reason}")
                write_session_to_disk(reason)
                
                self.send_response(200)
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
                
                # Extract snapshot metrics
                kills_per_minute = snapshot.get('killsPerMinute', 0.0)
                damage_per_minute = snapshot.get('damagePerMinute', 0.0)
                avg_health = snapshot.get('avgHealthAfterFight', 1.0)
                accuracy = snapshot.get('accuracy', 1.0)
                death_count = snapshot.get('deathCount', 0)
                time_survived = snapshot.get('timeSurvived', 0.0)
                is_kiting = snapshot.get('isKiting', False)
                avg_dist = snapshot.get('avgDistFromZombies', 0.0)

                # Proposed difficulty profile parameters
                proposed = current_profile.copy()
                
                # Signal Scores
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
    run_server()
