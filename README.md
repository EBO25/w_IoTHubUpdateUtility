# IoT Hub Update Utility

A .NET 8 / Avalonia desktop tool for updating IoT Hub deployments on Linux (Raspberry Pi / Ubuntu).

---

## Prerequisites

- [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8)
- VS Code with the **C# Dev Kit** extension
- `unrar` system package (only if you need `.rar` support):
  ```
  sudo apt install unrar
  ```

---

## Run in VS Code

1. Open the `IoTHubUpdateUtility/` folder in VS Code.
2. Press **F5** (or use Run → Start Debugging).
3. The app builds and launches.

---

## Publish a single-file Linux binary

```bash
# From the project folder:
dotnet publish -c Release -r linux-x64 --self-contained true \
  -p:PublishSingleFile=true --output ./publish
```

Copy `./publish/IoTHubUpdateUtility` and `settings.json` to the target machine.

---

## Project structure

```
IoTHubUpdateUtility/
├── Program.cs                   Entry point
├── App.cs                       Avalonia app bootstrap
├── IoTHubUpdateUtility.csproj   Project file
├── settings.json                Runtime config (auto-saved by app)
│
├── Models/                      Plain data objects — no logic
│   ├── AppConfig.cs             Root config persisted to JSON
│   ├── SourceInfo.cs            Selected source (archive or folder)
│   ├── UpdateRule.cs            Single upgrade rule object
│   ├── BackupPlan.cs            Backup settings
│   ├── ServiceConfig.cs         Start/stop command settings
│   └── UpgradePlan.cs           Dry-run result (list of planned actions)
│
├── Services/                    One class, one job
│   ├── ArchiveService.cs        Detect + extract archives
│   ├── ConfigService.cs         Load/save config.json
│   ├── ValidationService.cs     Dry-run check (no filesystem writes)
│   └── UpdateService.cs         Execute: backup, copy, chmod, service cmds
│
├── App/
│   └── AppService.cs            Orchestrator — only thing the UI calls
│
└── UI/
    ├── MainWindow.axaml         Layout (matches wireframe)
    ├── MainWindow.axaml.cs      Button handlers
    ├── MainViewModel.cs         All observable state
    └── BoolNegConverter.cs      Helper for RadioButton binding
```

---

## How to add a new rule type

1. Add a value to the `OperationType` enum in `Models/UpdateRule.cs`.
2. Handle the new case in `ValidationService.cs` → `EvaluateRule()`.
3. Handle the new case in `UpdateService.cs` → `ApplyAction()` (if it needs a new action kind, add it to `ActionKind` in `UpgradePlan.cs` too).
4. Add the new option to the `ComboBox` in `MainWindow.axaml`.

---

## settings.json fields

| Field | Default | Description |
|---|---|---|
| `SourceType` | `0` | 0=CompressedFile, 1=Folder |
| `ExtractionPath` | `/tmp` | Base path for extracting archives |
| `Backup.Enabled` | `true` | Whether to back up before updating |
| `Backup.Path` | `/home/pi` | Root folder for timestamped backups |
| `Services.StopServices` | `[]` | Array of commands to stop services |
| `Services.StartServices` | `[]` | Array of commands to start services |
| `Services.StopBeforeUpdate` | `true` | Run stop commands before executing rules |
| `Services.StartAfterUpdate` | `false` | Run start commands after executing rules |
| `LastSourcePath` | `""` | Remembered across restarts |
| `LastDestinationPath` | `/home/pi` | Remembered across restarts |
| `Rules` | `[]` | Saved rule list |
