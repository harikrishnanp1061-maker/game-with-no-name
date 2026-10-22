---
name: lan-sync
description: Coordinate and synchronize changes with peer developer's Antigravity instance over local Wi-Fi LAN using antigravity_bridge.py
---

# Antigravity LAN Peer Synchronization Skill

Use this workflow whenever the user wants to:
- Send or push updates to a teammate / friend's laptop.
- Check what changes were received from a teammate.
- Pull recent changes from the GitHub remote repository or apply a direct Wi-Fi patch.

## 1. Local Wi-Fi Bridge Architecture
- Script: `antigravity_bridge.py` located at repository root.
- Port: `8765` (HTTP REST API and Dashboard).
- Incoming Handoff Notes: `_sync/INCOMING_HANDOFF.md`.

## 2. Common Agent Commands

### A. Push Changes & Notify Teammate
When the user asks to push or send changes to their friend:
```powershell
python antigravity_bridge.py push --peer <FRIEND_IP> --msg "Description of changes"
```
*(If `--peer` is omitted, it will commit and push to GitHub without sending a direct network packet).*

### B. Send Teammate a Quick Notification / Handoff
Without pushing, to inform the teammate's Antigravity of progress or instructions:
```powershell
python antigravity_bridge.py notify --peer <FRIEND_IP> --title "Task Finished" --msg "Details of what was done"
```

### C. Pull Teammate's Changes
When asked to sync or pull latest work:
```powershell
python antigravity_bridge.py pull
```

### D. Check Incoming Teammate Handoffs
Read `_sync/INCOMING_HANDOFF.md` to see what the other machine just completed.
