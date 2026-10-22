"""
Antigravity LAN Bridge — Peer-to-Peer AI Collaboration & Sync System
Enables two Antigravity instances on the same Wi-Fi network to:
  1. Notify each other of updates, handoffs, and instructions.
  2. Synchronize code via Git (GitHub) or directly via Wi-Fi git patches.
  3. Inspect real-time status and diffs via a built-in browser dashboard.
  4. Allow agents to invoke commands via CLI or REST API.

No external pip dependencies required (runs on pure Python standard library).
"""

import os
import sys
import json
import time
import socket
import argparse
import subprocess
import threading
from pathlib import Path
from http.server import HTTPServer, BaseHTTPRequestHandler
from urllib.request import Request, urlopen
from urllib.error import URLError
from urllib.parse import urlparse, parse_qs

# Ensure UTF-8 console output on Windows
if sys.stdout and hasattr(sys.stdout, "reconfigure"):
    try:
        sys.stdout.reconfigure(encoding="utf-8", errors="replace")
        sys.stderr.reconfigure(encoding="utf-8", errors="replace")
    except Exception:
        pass


PORT = 8765
REPO_ROOT = Path(__file__).resolve().parent
SYNC_DIR = REPO_ROOT / "_sync"
HANDOFF_FILE = SYNC_DIR / "INCOMING_HANDOFF.md"
HISTORY_FILE = SYNC_DIR / "sync_history.json"
CONFIG_FILE = SYNC_DIR / "bridge_config.json"


def ensure_sync_dirs():
    SYNC_DIR.mkdir(parents=True, exist_ok=True)
    if not HISTORY_FILE.exists():
        HISTORY_FILE.write_text(json.dumps([], indent=2), encoding="utf-8")
    if not HANDOFF_FILE.exists():
        HANDOFF_FILE.write_text(
            "# Incoming Antigravity Handoffs\n\n*No messages received yet.*\n",
            encoding="utf-8",
        )


def load_config():
    if CONFIG_FILE.exists():
        try:
            return json.loads(CONFIG_FILE.read_text(encoding="utf-8"))
        except Exception:
            pass
    return {"peer_ip": "", "peer_port": PORT, "username": os.getenv("USERNAME", "Developer")}


def save_config(cfg):
    ensure_sync_dirs()
    CONFIG_FILE.write_text(json.dumps(cfg, indent=2), encoding="utf-8")


def get_local_ip():
    """Detects the primary LAN IPv4 address (e.g. 192.168.1.x)."""
    s = socket.socket(socket.AF_INET, socket.SOCK_DGRAM)
    try:
        # Does not actually establish connection, just routes to local interface
        s.connect(("8.8.8.8", 80))
        ip = s.getsockname()[0]
    except Exception:
        ip = "127.0.0.1"
    finally:
        s.close()
    return ip


def run_git_cmd(args):
    """Executes a git command in REPO_ROOT safely."""
    try:
        res = subprocess.run(
            ["git"] + args,
            cwd=str(REPO_ROOT),
            stdout=subprocess.PIPE,
            stderr=subprocess.PIPE,
            text=True,
            timeout=30,
            encoding="utf-8",
            errors="replace",
        )
        return res.returncode == 0, res.stdout.strip(), res.stderr.strip()
    except Exception as e:
        return False, "", str(e)


def get_git_status():
    """Returns a dictionary with current Git status summary."""
    success, branch, _ = run_git_cmd(["rev-parse", "--abbrev-ref", "HEAD"])
    if not success:
        branch = "unknown"

    _, commit_hash, _ = run_git_cmd(["rev-parse", "--short", "HEAD"])
    _, commit_msg, _ = run_git_cmd(["log", "-1", "--pretty=%B"])
    _, status_raw, _ = run_git_cmd(["status", "--porcelain"])

    modified_files = []
    if status_raw:
        for line in status_raw.splitlines():
            line = line.strip()
            if line:
                modified_files.append(line)

    return {
        "branch": branch,
        "commit": commit_hash,
        "last_message": commit_msg.strip() if commit_msg else "",
        "modified_count": len(modified_files),
        "modified_files": modified_files[:20],
        "is_dirty": len(modified_files) > 0,
    }


def show_toast(title, message):
    """Triggers an audio beep and Windows notification if available."""
    try:
        # Standard system beep
        import winsound
        winsound.MessageBeep(winsound.MB_ICONASTERISK)
    except Exception:
        pass

    # Optional powershell toast notification
    def _toast():
        ps_script = f"""
        [Windows.UI.Notifications.ToastNotificationManager, Windows.UI.Notifications, ContentType = WindowsRuntime] | Out-Null
        [Windows.Data.Xml.Dom.XmlDocument, Windows.Data.Xml.Dom.XmlDocument, ContentType = WindowsRuntime] | Out-Null
        $template = [Windows.UI.Notifications.ToastNotificationManager]::GetTemplateContent([Windows.UI.Notifications.ToastTemplateType]::ToastText02)
        $textNodes = $template.GetElementsByTagName('text')
        $textNodes.Item(0).AppendChild($template.CreateTextNode('{title}')) | Out-Null
        $textNodes.Item(1).AppendChild($template.CreateTextNode('{message}')) | Out-Null
        $toast = [Windows.UI.Notifications.ToastNotification]::new($template)
        $notifier = [Windows.UI.Notifications.ToastNotificationManager]::CreateToastNotifier('Antigravity LAN Bridge')
        $notifier.Show($toast)
        """
        try:
            subprocess.run(
                ["powershell", "-NoProfile", "-NonInteractive", "-Command", ps_script],
                timeout=5,
                creationflags=subprocess.CREATE_NO_WINDOW if os.name == "nt" else 0,
            )
        except Exception:
            pass

    threading.Thread(target=_toast, daemon=True).start()


def record_history(entry):
    ensure_sync_dirs()
    try:
        data = json.loads(HISTORY_FILE.read_text(encoding="utf-8"))
    except Exception:
        data = []
    data.insert(0, entry)
    # Keep last 50
    data = data[:50]
    HISTORY_FILE.write_text(json.dumps(data, indent=2), encoding="utf-8")


def append_handoff_md(sender, title, message, git_info=None):
    ensure_sync_dirs()
    timestamp = time.strftime("%Y-%m-%d %H:%M:%S")
    git_block = ""
    if git_info:
        git_block = (
            f"\n- **Branch:** `{git_info.get('branch', 'main')}`\n"
            f"- **Commit:** `{git_info.get('commit', 'n/a')}`\n"
        )

    entry = (
        f"### 🚀 Update from `{sender}` — {timestamp}\n\n"
        f"**{title}**\n\n"
        f"{message}\n"
        f"{git_block}\n"
        f"---\n\n"
    )

    try:
        current = HANDOFF_FILE.read_text(encoding="utf-8")
    except Exception:
        current = ""
def get_unity_diagnostics():
    """Inspects Unity Editor logs, running processes, and package resolution status."""
    local_app = os.getenv("LOCALAPPDATA", "")
    log_path = Path(local_app) / "Unity" / "Editor" / "Editor.log"

    recent_errors = []
    package_events = []
    last_lines = []

    if log_path.exists():
        try:
            with open(log_path, "r", encoding="utf-8", errors="replace") as f:
                all_lines = f.readlines()
                last_lines = [l.strip() for l in all_lines[-100:] if l.strip()]
                for line in all_lines[-400:]:
                    l_str = line.strip()
                    l_lower = l_str.lower()
                    if any(k in l_lower for k in ["error cs", "exception:", "failed to", "could not load", "fatal:", "package manager error"]):
                        recent_errors.append(l_str)
                    if any(k in l_lower for k in ["[package manager]", "resolving packages", "downloading", "com.coplaydev", "com.unity."]):
                        package_events.append(l_str)
        except Exception as e:
            recent_errors.append(f"Log read error: {e}")

    # Check if Unity.exe is running
    unity_running = False
    try:
        p = subprocess.run(["tasklist", "/FI", "IMAGENAME eq Unity.exe"], capture_output=True, text=True, timeout=5)
        unity_running = "Unity.exe" in p.stdout
    except Exception:
        pass

    # Check if Unity Hub is running
    hub_running = False
    try:
        p = subprocess.run(["tasklist", "/FI", "IMAGENAME eq Unity Hub.exe"], capture_output=True, text=True, timeout=5)
        hub_running = "Unity Hub.exe" in p.stdout
    except Exception:
        pass

    lib_path = REPO_ROOT / "collage game" / "Library"

    return {
        "unity_running": unity_running,
        "unity_hub_running": hub_running,
        "library_folder_rebuilt": lib_path.exists(),
        "log_file_found": log_path.exists(),
        "recent_errors": recent_errors[-20:],
        "package_events": package_events[-20:],
        "last_lines": last_lines[-25:],
        "timestamp": time.strftime("%Y-%m-%d %H:%M:%S"),
    }


class BridgeRequestHandler(BaseHTTPRequestHandler):
    def _send_json(self, data, status_code=200):
        body = json.dumps(data, indent=2).encode("utf-8")
        self.send_response(status_code)
        self.send_header("Content-Type", "application/json")
        self.send_header("Access-Control-Allow-Origin", "*")
        self.send_header("Access-Control-Allow-Methods", "GET, POST, OPTIONS")
        self.send_header("Access-Control-Allow-Headers", "Content-Type")
        self.send_header("Content-Length", str(len(body)))
        self.end_headers()
        self.wfile.write(body)

    def _send_html(self, html_content, status_code=200):
        body = html_content.encode("utf-8")
        self.send_response(status_code)
        self.send_header("Content-Type", "text/html; charset=utf-8")
        self.send_header("Content-Length", str(len(body)))
        self.end_headers()
        self.wfile.write(body)

    def do_OPTIONS(self):
        self.send_response(204)
        self.send_header("Access-Control-Allow-Origin", "*")
        self.send_header("Access-Control-Allow-Methods", "GET, POST, OPTIONS")
        self.send_header("Access-Control-Allow-Headers", "Content-Type")
        self.end_headers()

    def do_GET(self):
        parsed = urlparse(self.path)
        path = parsed.path

        if path == "/" or path == "/dashboard":
            self.render_dashboard()
            return

        if path == "/api/status":
            cfg = load_config()
            status = {
                "machine": os.getenv("COMPUTERNAME", socket.gethostname()),
                "user": cfg.get("username", os.getenv("USERNAME", "Developer")),
                "ip": get_local_ip(),
                "port": PORT,
                "git": get_git_status(),
                "peer_ip": cfg.get("peer_ip", ""),
                "timestamp": time.time(),
            }
            self._send_json(status)
            return

        if path == "/api/history":
            ensure_sync_dirs()
            try:
                history = json.loads(HISTORY_FILE.read_text(encoding="utf-8"))
            except Exception:
                history = []
            self._send_json({"history": history})
            return

        if path == "/api/handoff":
            ensure_sync_dirs()
            try:
                content = HANDOFF_FILE.read_text(encoding="utf-8")
            except Exception:
                content = ""
            self._send_json({"content": content})
            return

        if path == "/api/unity/diagnostics":
            self._send_json(get_unity_diagnostics())
            return

        if path == "/api/unity/log":
            diag = get_unity_diagnostics()
            self._send_json({"lines": diag.get("last_lines", [])})
            return

        self._send_json({"error": "Not Found"}, status_code=404)

    def do_POST(self):
        parsed = urlparse(self.path)
        path = parsed.path
        length = int(self.headers.get("Content-Length", 0))
        raw_data = self.rfile.read(length).decode("utf-8") if length > 0 else "{}"
        try:
            body = json.loads(raw_data)
        except Exception:
            body = {}

        if path == "/api/query":
            diag = get_unity_diagnostics()
            git_st = get_git_status()
            resp = {
                "status": "success",
                "machine": os.getenv("COMPUTERNAME", socket.gethostname()),
                "user": load_config().get("username", os.getenv("USERNAME", "Developer")),
                "unity": diag,
                "git": git_st,
                "timestamp": time.strftime("%Y-%m-%d %H:%M:%S"),
            }
            self._send_json(resp)
            return

        if path == "/api/stop":
            self._send_json({"status": "shutting_down", "message": "Bridge listener terminating per instruction"})
            def _stop():
                time.sleep(0.5)
                os._exit(0)
            threading.Thread(target=_stop, daemon=True).start()
            return

        if path == "/api/restart":
            self._send_json({"status": "restarting", "message": "Bridge restarting"})
            def _restart():
                time.sleep(0.5)
                subprocess.Popen([sys.executable, str(Path(__file__).resolve())] + sys.argv[1:])
                os._exit(0)
            threading.Thread(target=_restart, daemon=True).start()
            return

        if path == "/api/config":
            cfg = load_config()
            if "peer_ip" in body:
                cfg["peer_ip"] = body["peer_ip"].strip()
            if "peer_port" in body:
                cfg["peer_port"] = int(body["peer_port"])
            if "username" in body:
                cfg["username"] = body["username"].strip()
            save_config(cfg)
            self._send_json({"status": "ok", "config": cfg})
            return

        if path == "/api/notify":
            # Incoming message from peer
            sender = body.get("sender", "Peer Agent")
            title = body.get("title", "New Update")
            message = body.get("message", "No message content")
            git_info = body.get("git", None)

            record_history({
                "type": "incoming_notification",
                "sender": sender,
                "title": title,
                "message": message,
                "git": git_info,
                "timestamp": time.strftime("%Y-%m-%d %H:%M:%S"),
            })
            append_handoff_md(sender, title, message, git_info)
            show_toast(f"Antigravity: {title}", f"From {sender}: {message[:100]}")

            print(f"\n[NOTIFY FROM {sender.upper()}]: {title}\n{message}\n")
            self._send_json({"status": "received", "time": time.time()})
            return

        if path == "/api/pull":
            # Pull changes from git remote
            success, stdout, stderr = run_git_cmd(["pull", "--rebase"])
            if not success:
                # Fallback to normal pull if rebase had conflict
                success, stdout, stderr = run_git_cmd(["pull"])

            record_history({
                "type": "git_pull",
                "success": success,
                "output": stdout or stderr,
                "timestamp": time.strftime("%Y-%m-%d %H:%M:%S"),
            })
            self._send_json({
                "success": success,
                "output": stdout,
                "error": stderr,
                "git": get_git_status(),
            })
            return

        if path == "/api/patch":
            # Incoming direct patch over Wi-Fi
            patch_text = body.get("patch", "")
            sender = body.get("sender", "Peer")
            if not patch_text.strip():
                self._send_json({"success": False, "error": "Empty patch"}, status_code=400)
                return

            patch_file = SYNC_DIR / "incoming.patch"
            patch_file.write_text(patch_text, encoding="utf-8")

            # Check if it applies cleanly
            ok, check_out, check_err = run_git_cmd(["apply", "--check", str(patch_file)])
            if not ok:
                self._send_json({
                    "success": False,
                    "error": f"Patch could not apply cleanly: {check_err}",
                    "details": check_out,
                }, status_code=409)
                return

            # Apply
            applied_ok, app_out, app_err = run_git_cmd(["apply", str(patch_file)])
            record_history({
                "type": "applied_patch",
                "sender": sender,
                "success": applied_ok,
                "timestamp": time.strftime("%Y-%m-%d %H:%M:%S"),
            })
            show_toast("Patch Applied via LAN!", f"Received direct patch from {sender}")
            self._send_json({
                "success": applied_ok,
                "output": app_out,
                "error": app_err,
                "git": get_git_status(),
            })
            return

        self._send_json({"error": "Unknown endpoint"}, status_code=404)

    def render_dashboard(self):
        cfg = load_config()
        local_ip = get_local_ip()
        html = f"""<!DOCTYPE html>
<html lang="en">
<head>
<meta charset="utf-8">
<meta name="viewport" content="width=device-width, initial-scale=1">
<title>Antigravity LAN Bridge — FLIP//FALL</title>
<link rel="preconnect" href="https://fonts.googleapis.com">
<link rel="preconnect" href="https://fonts.gstatic.com" crossorigin>
<link href="https://fonts.googleapis.com/css2?family=JetBrains+Mono:wght@400;600;700&family=Plus+Jakarta+Sans:wght@400;500;600;700&display=swap" rel="stylesheet">
<style>
:root {{
  --bg-primary: #0a0d14;
  --bg-card: #121824;
  --bg-card-sub: #171f2f;
  --accent-cyan: #00e5ff;
  --accent-blue: #3b82f6;
  --accent-emerald: #10b981;
  --accent-amber: #f59e0b;
  --accent-rose: #f43f5e;
  --text-main: #f1f5f9;
  --text-muted: #94a3b8;
  --border-subtle: rgba(255, 255, 255, 0.08);
  --border-active: rgba(0, 229, 255, 0.35);
  --glow-cyan: 0 0 25px rgba(0, 229, 255, 0.15);
}}
* {{ box-sizing: border-box; margin: 0; padding: 0; }}
body {{
  background-color: var(--bg-primary);
  color: var(--text-main);
  font-family: 'Plus Jakarta Sans', sans-serif;
  min-height: 100vh;
  padding: 24px;
}}
.container {{
  max-width: 1100px;
  margin: 0 auto;
}}
header {{
  display: flex;
  justify-content: space-between;
  align-items: center;
  padding-bottom: 24px;
  border-bottom: 1px solid var(--border-subtle);
  margin-bottom: 28px;
}}
.brand {{
  display: flex;
  align-items: center;
  gap: 14px;
}}
.badge-live {{
  background: rgba(16, 185, 129, 0.15);
  color: var(--accent-emerald);
  border: 1px solid rgba(16, 185, 129, 0.4);
  padding: 4px 10px;
  border-radius: 9999px;
  font-size: 11px;
  font-weight: 700;
  letter-spacing: 0.05em;
  text-transform: uppercase;
  display: inline-flex;
  align-items: center;
  gap: 6px;
}}
.badge-live::before {{
  content: "";
  width: 7px;
  height: 7px;
  background: var(--accent-emerald);
  border-radius: 50%;
  box-shadow: 0 0 8px var(--accent-emerald);
}}
h1 {{
  font-size: 22px;
  font-weight: 700;
  letter-spacing: -0.02em;
  color: #fff;
}}
.grid {{
  display: grid;
  grid-template-columns: 1fr 1fr;
  gap: 20px;
  margin-bottom: 24px;
}}
@media (max-width: 800px) {{
  .grid {{ grid-template-columns: 1fr; }}
}}
.card {{
  background: var(--bg-card);
  border: 1px solid var(--border-subtle);
  border-radius: 14px;
  padding: 20px;
  position: relative;
  box-shadow: 0 4px 20px rgba(0,0,0,0.3);
}}
.card.highlight {{
  border-color: var(--border-active);
  box-shadow: var(--glow-cyan);
}}
.card-header {{
  display: flex;
  justify-content: space-between;
  align-items: center;
  margin-bottom: 16px;
}}
.card-title {{
  font-size: 14px;
  font-weight: 700;
  text-transform: uppercase;
  letter-spacing: 0.06em;
  color: var(--accent-cyan);
}}
.ip-pill {{
  font-family: 'JetBrains Mono', monospace;
  font-size: 13px;
  padding: 6px 12px;
  background: var(--bg-card-sub);
  border: 1px solid var(--border-subtle);
  border-radius: 8px;
  color: var(--accent-cyan);
}}
.stat-row {{
  display: flex;
  justify-content: space-between;
  padding: 8px 0;
  border-bottom: 1px solid rgba(255,255,255,0.04);
  font-size: 13px;
}}
.stat-label {{ color: var(--text-muted); }}
.stat-val {{ font-family: 'JetBrains Mono', monospace; font-weight: 600; color: #fff; }}
.btn {{
  background: linear-gradient(135deg, #00e5ff 0%, #0099ff 100%);
  color: #041019;
  border: none;
  font-weight: 700;
  font-size: 13px;
  padding: 10px 18px;
  border-radius: 8px;
  cursor: pointer;
  transition: all 0.2s ease;
  display: inline-flex;
  align-items: center;
  gap: 8px;
}}
.btn:hover {{
  transform: translateY(-1px);
  box-shadow: 0 4px 15px rgba(0, 229, 255, 0.35);
}}
.btn-secondary {{
  background: var(--bg-card-sub);
  color: var(--text-main);
  border: 1px solid var(--border-subtle);
}}
.btn-secondary:hover {{
  border-color: var(--accent-cyan);
  color: var(--accent-cyan);
  box-shadow: none;
}}
.btn-danger {{
  background: rgba(244, 63, 94, 0.15);
  color: var(--accent-rose);
  border: 1px solid rgba(244, 63, 94, 0.3);
}}
.input-field {{
  width: 100%;
  background: var(--bg-card-sub);
  border: 1px solid var(--border-subtle);
  border-radius: 8px;
  color: #fff;
  padding: 10px 14px;
  font-family: inherit;
  font-size: 13px;
  margin-top: 6px;
  margin-bottom: 14px;
  transition: border 0.2s;
}}
.input-field:focus {{
  outline: none;
  border-color: var(--accent-cyan);
}}
textarea.input-field {{
  resize: vertical;
  min-height: 80px;
  font-family: 'JetBrains Mono', monospace;
}}
.history-feed {{
  max-height: 320px;
  overflow-y: auto;
  display: flex;
  flex-direction: column;
  gap: 10px;
}}
.feed-item {{
  background: var(--bg-card-sub);
  border-left: 3px solid var(--accent-cyan);
  padding: 12px 14px;
  border-radius: 6px;
  font-size: 13px;
}}
.feed-header {{
  display: flex;
  justify-content: space-between;
  margin-bottom: 4px;
  font-size: 11px;
  color: var(--text-muted);
}}
.feed-title {{ font-weight: 700; color: #fff; margin-bottom: 4px; }}
.feed-body {{ color: #cbd5e1; white-space: pre-wrap; font-size: 12px; }}
.actions-row {{
  display: flex;
  gap: 10px;
  flex-wrap: wrap;
  margin-top: 14px;
}}
.toast-notice {{
  display: none;
  position: fixed;
  bottom: 24px;
  right: 24px;
  background: #00e5ff;
  color: #000;
  font-weight: 700;
  padding: 12px 20px;
  border-radius: 8px;
  box-shadow: 0 6px 25px rgba(0,229,255,0.4);
  z-index: 1000;
}}
</style>
</head>
<body>
<div class="container">
  <header>
    <div class="brand">
      <div class="badge-live">LAN BRIDGE ACTIVE</div>
      <h1>Antigravity Peer Sync — FLIP//FALL</h1>
    </div>
    <div style="font-size: 12px; color: var(--text-muted);">
      Direct AI-to-AI Wi-Fi Bridge
    </div>
  </header>

  <div class="grid">
    <!-- LOCAL MACHINE CARD -->
    <div class="card highlight">
      <div class="card-header">
        <span class="card-title">This Machine (Local)</span>
        <span class="ip-pill">{local_ip}:{PORT}</span>
      </div>
      <div class="stat-row">
        <span class="stat-label">User / Device:</span>
        <span class="stat-val">{cfg.get('username', 'Developer')} ({os.getenv('COMPUTERNAME', socket.gethostname())})</span>
      </div>
      <div class="stat-row">
        <span class="stat-label">Git Branch:</span>
        <span class="stat-val" id="local-branch"></span>
      </div>
      <div class="stat-row">
        <span class="stat-label">Last Commit:</span>
        <span class="stat-val" id="local-commit"></span>
      </div>
      <div class="stat-row">
        <span class="stat-label">Working Directory:</span>
        <span class="stat-val" id="local-dirty"></span>
      </div>

      <div class="actions-row">
        <button class="btn btn-secondary" onclick="pullGit()">⬇️ Git Pull Remote</button>
        <button class="btn btn-secondary" onclick="refreshStatus()">🔄 Refresh Status</button>
      </div>
    </div>

    <!-- FRIEND MACHINE CARD -->
    <div class="card">
      <div class="card-header">
        <span class="card-title">Friend's Machine (Peer)</span>
        <span class="ip-pill" id="peer-ip-badge">{cfg.get('peer_ip', '') or ''}{(':' + str(cfg.get('peer_port', PORT))) if cfg.get('peer_ip', '') else 'Not connected'}</span>
      </div>
      <label class="stat-label" style="font-size:12px;">Friend's Wi-Fi IP Address:</label>
      <input type="text" id="peer-ip-input" class="input-field" value="{cfg.get('peer_ip', '')}">

      <div class="actions-row">
        <button class="btn" onclick="savePeerIp()">Save Peer IP</button>
        <button class="btn btn-secondary" onclick="pingPeer()">📡 Ping Friend</button>
      </div>
    </div>
  </div>

  <div class="grid">
    <!-- SEND HANDOFF / NOTIFICATION -->
    <div class="card">
      <div class="card-header">
        <span class="card-title">Send Handoff to Friend's Antigravity</span>
      </div>
      <label class="stat-label" style="font-size:12px;">Update Subject / Task:</label>
      <input type="text" id="msg-title" class="input-field">

      <label class="stat-label" style="font-size:12px;">Details / Agent Instructions:</label>
      <textarea id="msg-body" class="input-field"></textarea>

      <div class="actions-row">
        <button class="btn" onclick="sendHandoff()">🚀 Send to Friend's Screen</button>
        <button class="btn btn-secondary" onclick="sendDirectPatch()">⚡ Transfer Direct Wi-Fi Patch</button>
      </div>
    </div>

    <!-- RECENT MESSAGES / FEED -->
    <div class="card">
      <div class="card-header">
        <span class="card-title">Recent Activity & Handoffs</span>
        <button class="btn btn-secondary" style="padding:4px 10px; font-size:11px;" onclick="loadHistory()">Refresh</button>
      </div>
      <div class="history-feed" id="history-feed"></div>
    </div>
  </div>
</div>

<div id="toast" class="toast-notice"></div>

<script>
function showNotice(text) {{
  const toast = document.getElementById('toast');
  toast.innerText = text;
  toast.style.display = 'block';
  setTimeout(() => {{ toast.style.display = 'none'; }}, 3500);
}}

async function refreshStatus() {{
  try {{
    const res = await fetch('/api/status');
    const data = await res.json();
    document.getElementById('local-branch').innerText = data.git.branch;
    document.getElementById('local-commit').innerText = data.git.commit;
    document.getElementById('local-dirty').innerText = data.git.is_dirty 
      ? `Modified (${{data.git.modified_count}} files)`
      : 'Clean (Ready)';
    document.getElementById('local-dirty').style.color = data.git.is_dirty ? '#f59e0b' : '#10b981';
  }} catch (e) {{
    console.error(e);
  }}
}}

async function savePeerIp() {{
  const ip = document.getElementById('peer-ip-input').value.trim();
  if (!ip) return alert('Please enter your friend\\'s Wi-Fi IP!');
  const res = await fetch('/api/config', {{
    method: 'POST',
    headers: {{ 'Content-Type': 'application/json' }},
    body: JSON.stringify({{ peer_ip: ip }})
  }});
  if (res.ok) {{
    document.getElementById('peer-ip-badge').innerText = ip + ':{PORT}';
    showNotice('Peer IP saved: ' + ip);
  }}
}}

async function pingPeer() {{
  const ip = document.getElementById('peer-ip-input').value.trim();
  if (!ip) return alert('Set Friend\\'s IP first!');
  showNotice('Pinging ' + ip + '...');
  try {{
    const targetUrl = `http://${{ip}}:{PORT}/api/status`;
    const res = await fetch(targetUrl, {{ method: 'GET', mode: 'cors' }});
    if (res.ok) {{
      const d = await res.json();
      alert(`Connected to ${{d.user}} on ${{d.machine}}! Branch: ${{d.git.branch}}`);
    }} else {{
      alert('Could not connect. Ensure Antigravity LAN Bridge is running on their laptop!');
    }}
  }} catch (e) {{
    alert('Peer unreachable. Make sure both laptops are on the same Wi-Fi and the bridge is running at port {PORT}!');
  }}
}}

async function sendHandoff() {{
  const ip = document.getElementById('peer-ip-input').value.trim();
  if (!ip) return alert('Enter friend\\'s Wi-Fi IP first!');
  const title = document.getElementById('msg-title').value.trim();
  const message = document.getElementById('msg-body').value.trim();
  if (!title) return alert('Please provide a task title!');

  const statusRes = await fetch('/api/status');
  const myStatus = await statusRes.json();

  const payload = {{
    sender: myStatus.user + ' (' + myStatus.machine + ')',
    title: title,
    message: message,
    git: myStatus.git
  }};

  try {{
    const res = await fetch(`http://${{ip}}:{PORT}/api/notify`, {{
      method: 'POST',
      headers: {{ 'Content-Type': 'application/json' }},
      body: JSON.stringify(payload)
    }});
    if (res.ok) {{
      showNotice('Handoff sent directly to friend\\'s screen!');
      document.getElementById('msg-title').value = '';
      document.getElementById('msg-body').value = '';
      loadHistory();
    }} else {{
      alert('Failed to send handoff. Check connection.');
    }}
  }} catch (e) {{
    alert('Peer unreachable. Ensure friend\\'s bridge is running on ' + ip + ':{PORT}');
  }}
}}

async function sendDirectPatch() {{
  const ip = document.getElementById('peer-ip-input').value.trim();
  if (!ip) return alert('Enter friend\\'s Wi-Fi IP first!');
  if (!confirm('This will send your uncommitted local changes directly over Wi-Fi as a git patch to your friend\\'s laptop. Continue?')) return;
  
  // Ask local bridge to bundle and send patch to peer
  showNotice('Generating and transferring patch...');
  // Handled via local CLI or direct fetch
}}

async function pullGit() {{
  showNotice('Running Git Pull...');
  const res = await fetch('/api/pull', {{ method: 'POST' }});
  const data = await res.json();
  if (data.success) {{
    showNotice('Git Pull Successful!');
    refreshStatus();
  }} else {{
    alert('Git pull error: ' + (data.error || data.output));
  }}
}}

async function loadHistory() {{
  try {{
    const res = await fetch('/api/history');
    const data = await res.json();
    const feed = document.getElementById('history-feed');
    if (!data.history || data.history.length === 0) {{
      feed.innerHTML = '<div style="color:var(--text-muted); font-size:13px;">No recent handoffs recorded.</div>';
      return;
    }}
    feed.innerHTML = data.history.map(item => `
      <div class="feed-item">
        <div class="feed-header">
          <span>${{item.sender || 'System'}}</span>
          <span>${{item.timestamp || ''}}</span>
        </div>
        <div class="feed-title">${{item.title || item.type}}</div>
        <div class="feed-body">${{item.message || item.output || ''}}</div>
      </div>
    `).join('');
  }} catch (e) {{
    console.error(e);
  }}
}}

refreshStatus();
loadHistory();
setInterval(refreshStatus, 10000);
</script>
</body>
</html>"""
        self._send_html(html)


def client_send_notify(peer_ip, title, message):
    cfg = load_config()
    git_st = get_git_status()
    payload = json.dumps({
        "sender": f"{cfg.get('username', os.getenv('USERNAME'))} ({os.getenv('COMPUTERNAME')})",
        "title": title,
        "message": message,
        "git": git_st,
    }).encode("utf-8")

    req = Request(
        f"http://{peer_ip}:{PORT}/api/notify",
        data=payload,
        headers={"Content-Type": "application/json"},
    )
    try:
        with urlopen(req, timeout=10) as resp:
            data = json.loads(resp.read().decode("utf-8"))
            print(f"[OK] Notification sent to {peer_ip}:{PORT} -> {data}")
            return True
    except Exception as e:
        print(f"[ERROR] Failed to reach peer at {peer_ip}:{PORT} -> {e}")
        return False


def client_send_patch(peer_ip):
    # Generates a git diff of working directory
    success, diff_text, err = run_git_cmd(["diff", "HEAD"])
    if not success or not diff_text.strip():
        # Check staged
        success, diff_text, err = run_git_cmd(["diff", "--staged"])
        if not success or not diff_text.strip():
            print("[INFO] Working directory is clean. No uncommitted diff to send!")
            return False

    cfg = load_config()
    payload = json.dumps({
        "sender": f"{cfg.get('username', os.getenv('USERNAME'))}",
        "patch": diff_text,
    }).encode("utf-8")

    req = Request(
        f"http://{peer_ip}:{PORT}/api/patch",
        data=payload,
        headers={"Content-Type": "application/json"},
    )
    try:
        with urlopen(req, timeout=15) as resp:
            data = json.loads(resp.read().decode("utf-8"))
            print(f"[OK] Patch transmitted and applied on {peer_ip}:{PORT} -> {data}")
            return True
    except Exception as e:
        print(f"[ERROR] Failed to apply patch on peer: {e}")
        return False


def start_server(port=PORT):
    ensure_sync_dirs()
    local_ip = get_local_ip()
    server_address = ("0.0.0.0", port)
    httpd = HTTPServer(server_address, BridgeRequestHandler)
    print("=================================================================")
    print(f"[ANTIGRAVITY LAN BRIDGE ACTIVE]")
    print(f"   Local IP:    http://{local_ip}:{port}")
    print(f"   Dashboard:   http://localhost:{port}")
    print(f"   Repo:        {REPO_ROOT}")
    print("=================================================================")
    print("Keep this terminal open, or run in background.")
    try:
        httpd.serve_forever()
    except KeyboardInterrupt:
        print("\nShutting down bridge...")
    finally:
        httpd.server_close()


def main():
    parser = argparse.ArgumentParser(description="Antigravity LAN Collaboration Bridge")
    subparsers = parser.add_subparsers(dest="command")

    # Start server
    start_parser = subparsers.add_parser("start", help="Start the LAN Bridge HTTP server")
    start_parser.add_argument("--port", type=int, default=PORT, help="Port to listen on")

    # Send notification
    send_parser = subparsers.add_parser("notify", help="Send a task update/notification to a peer")
    send_parser.add_argument("--peer", required=True, help="Peer Wi-Fi IP address")
    send_parser.add_argument("--title", required=True, help="Update title/summary")
    send_parser.add_argument("--msg", default="", help="Detailed message or instructions")

    # Push & Notify
    push_parser = subparsers.add_parser("push", help="Git commit, push to GitHub, and notify peer")
    push_parser.add_argument("--peer", help="Peer Wi-Fi IP address (optional)")
    push_parser.add_argument("--msg", required=True, help="Commit message")

    # Direct Wi-Fi Patch
    patch_parser = subparsers.add_parser("patch", help="Send uncommitted git diff directly over Wi-Fi")
    patch_parser.add_argument("--peer", required=True, help="Peer Wi-Fi IP address")

    # Git Pull
    pull_parser = subparsers.add_parser("pull", help="Pull latest changes from remote Git")

    # Diagnostics
    diag_parser = subparsers.add_parser("diag", help="Fetch Unity diagnostics and errors from peer")
    diag_parser.add_argument("--peer", required=True, help="Peer Wi-Fi IP")

    # Stop peer listener
    stop_parser = subparsers.add_parser("stop", help="Stop bridge listener on peer")
    stop_parser.add_argument("--peer", required=True, help="Peer Wi-Fi IP")

    # Restart peer listener
    restart_parser = subparsers.add_parser("restart", help="Restart bridge listener on peer")
    restart_parser.add_argument("--peer", required=True, help="Peer Wi-Fi IP")

    args = parser.parse_args()

    if args.command == "start" or args.command is None:
        port = getattr(args, "port", PORT) or PORT
        start_server(port)

    elif args.command == "notify":
        client_send_notify(args.peer, args.title, args.msg)

    elif args.command == "patch":
        client_send_patch(args.peer)

    elif args.command == "pull":
        success, out, err = run_git_cmd(["pull"])
        print(out if success else f"Error: {err}")

    elif args.command == "push":
        # Add all, commit, push, notify
        run_git_cmd(["add", "-A"])
        ok_c, out_c, err_c = run_git_cmd(["commit", "-m", args.msg])
        print(out_c or err_c)
        ok_p, out_p, err_p = run_git_cmd(["push"])
        print(out_p or err_p)
        if args.peer:
            client_send_notify(args.peer, "Pushed Updates to GitHub", args.msg)

    elif args.command == "diag":
        url = f"http://{args.peer}:{PORT}/api/unity/diagnostics"
        try:
            with urlopen(url, timeout=10) as resp:
                data = json.loads(resp.read().decode("utf-8"))
                print(json.dumps(data, indent=2))
        except Exception as e:
            print(f"Error querying {url}: {e}")

    elif args.command == "stop":
        url = f"http://{args.peer}:{PORT}/api/stop"
        req = Request(url, data=b"{}", headers={"Content-Type": "application/json"})
        try:
            with urlopen(req, timeout=5) as resp:
                print(resp.read().decode("utf-8"))
        except Exception as e:
            print(f"Error sending stop to {url}: {e}")

    elif args.command == "restart":
        url = f"http://{args.peer}:{PORT}/api/restart"
        req = Request(url, data=b"{}", headers={"Content-Type": "application/json"})
        try:
            with urlopen(req, timeout=5) as resp:
                print(resp.read().decode("utf-8"))
        except Exception as e:
            print(f"Error sending restart to {url}: {e}")


if __name__ == "__main__":
    main()
