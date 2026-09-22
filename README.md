# RDPWrangler 🖥️

**RDPWrangler** is a lightweight, responsive Remote Desktop Connection Manager for Windows written in C# and .NET 8 (Windows Forms). It enables managing multiple RDP connections organized by custom groups, hosting remote desktop sessions directly inside an embedded panel, and persisting all connection profiles and layout state in a standard `servers.ini` file.

---

## Features

- **Embedded Remote Desktop**:
  - Direct embedded RDP session using Microsoft's native Windows Terminal Services ActiveX control (`AxMSTSCLib` via `Devolutions.MsRdpEx`).
  - Supports **Smart Sizing** (desktop automatically fits the embedded container even when resized).
  - Status indicators (Disconnected, Connecting, Connected, Error details).
  - Quick action controls: **Connect**, **Disconnect**, and **Reconnect**.

- **Collapsible & Resizable Sidebar**:
  - Left panel contains the grouped server list and connection tools.
  - Click the **`◀`** button in the sidebar header or press <kbd>Ctrl</kbd>+<kbd>B</kbd> to collapse the sidebar completely, allowing the remote desktop session to expand to the full window.
  - When collapsed, click **`▶ Servers`** or **`☰ Sidebar`** in the top bar to expand the sidebar back to its previous width.
  - Resizable splitter bar between the sidebar and remote desktop.

- **Connection Grouping & Organization**:
  - Organize connections into logical categories (e.g. `Production`, `Staging`, `Development`, `Office Workstations`).
  - Hierarchical tree structure with group folders that can be expanded or collapsed.
  - Real-time search filter: Filter connections by display name, host/IP, username, or group name.

- **Full Management (Add, Edit, Delete, Save)**:
  - **Add Connection**: Configure Display Name, Host/IP, Port (default 3389), Group, Username, Domain, Password, and Smart Sizing.
  - **Edit Connection**: Select any server and click **Edit** (or press <kbd>F2</kbd>) to modify settings.
  - **Delete Connection**: Select any server or group and click **Delete** (or press <kbd>Del</kbd>) with confirmation safeguards.
  - **Right-Click Context Menu**: Right-click on any server or group for quick actions (*Connect*, *Edit*, *Delete*, *Add Server to this Group*, *Expand All*, *Collapse All*).

- **INI File Persistence (`servers.ini`)**:
  - Reads connections and settings automatically on startup.
  - All additions, edits, deletions, and window layout adjustments are immediately saved.
  - Can also be directly edited with any text editor (Notepad, VS Code, etc.).
  - Generates sensible sample servers if `servers.ini` is missing on initial launch.

---

## Keyboard Shortcuts

| Shortcut | Action |
| --- | --- |
| <kbd>Ctrl</kbd> + <kbd>B</kbd> | Toggle Sidebar (Collapse / Expand) |
| <kbd>Enter</kbd> or Double-Click | Connect to selected server |
| <kbd>F2</kbd> | Edit selected connection |
| <kbd>Del</kbd> | Delete selected connection or group |
| <kbd>F5</kbd> | Reconnect session |

---

## Configuration File Format (`servers.ini`)

`servers.ini` is stored in the application directory:

```ini
; ===================================================================
; RDPWrangler Server Connections & Settings INI File
; ===================================================================

[Settings]
SplitterDistance=280
SidebarCollapsed=False
LastConnectedServerId=sample_prod
WindowWidth=1280
WindowHeight=800
WindowMaximized=False

[Server.sample_prod]
DisplayName=Production Web 01
Group=Production
Host=10.0.0.21
Port=3389
Username=deploy
Domain=PROD
Password=
SmartSizing=True
Notes=Primary production web frontend

[Server.sample_dev]
DisplayName=Dev Database Node
Group=Development
Host=192.168.1.150
Port=3389
Username=dbadmin
Domain=CORP
Password=
SmartSizing=True
Notes=Development environment database server
```

---

## Running the Application

### From Command Line / Terminal:
```bash
dotnet run
```

### Or Run the Executable Directly:
```bash
bin\Release\net8.0-windows\RDPWrangler.exe
```
or
```bash
bin\Debug\net8.0-windows\RDPWrangler.exe
```

---

## Building and Testing

Build the solution:
```bash
dotnet build
```

Run automated tests:
```bash
dotnet test
```
