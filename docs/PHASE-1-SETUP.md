> **Superseded.** This was the original manual setup guide. The toolchain is now installed in `.tools/` and every step below is automated by `GameBuilder.BuildAllBatch` (see the README).

# Phase 1 — Unity Foundation: setup and test

Time needed: about 45–90 minutes, mostly downloads.

What Phase 1 delivers:
- a Unity 6.3 LTS URP project with Input System, TextMesh Pro and Photon Fusion 2.1.4
- your Photon App ID configured
- mobile build settings (landscape, IL2CPP/ARM64, Android API 26+, iOS 15+, INTERNET permission)
- the project folder layout, 4 named physics layers
- 4 scenes in a fixed build order, with a working splash screen that loads the main menu
- `Tools > Warehouse Duel > Validate Project`, a health check you can re-run any time

The files written so far are in `unity-source/Assets/`. Step 3 copies them into the real Unity project.

---

## Step 1 — Install Unity 6.3 LTS with mobile modules

1. Install **Unity Hub** from unity.com/download and sign in (a free Personal licence is fine).
2. Hub → **Installs → Install Editor** → choose the latest **Unity 6.3 LTS (6000.3.x)**.
3. Tick these modules:
   - **Android Build Support**, including **OpenJDK** and **Android SDK & NDK Tools**
   - **iOS Build Support** (optional on Windows: it exports an Xcode project, but a Mac is needed to build)
   - Microsoft Visual Studio Community, or use VS Code with the Unity extension

## Step 2 — Create the project from the URP template

1. Hub → **Projects → New project** → editor **6000.3.x**.
2. Template: **Universal 3D** (URP). Download the template if Hub asks.
3. Project name: `WarehouseDuel`
4. Location: `D:\Work\Projects\App-DoomShooter-Project`
5. **Create project** and wait for the editor to open. You should end up with `D:\Work\Projects\App-DoomShooter-Project\WarehouseDuel\Assets`.

Check in the editor:
- **Window → Package Manager → In Project** lists *Universal RP* and *Input System*. If Input System is missing:
  **+ → Install package by name → `com.unity.inputsystem`**, accept the restart prompt.
- **Edit → Project Settings → Player → Other Settings → Active Input Handling** = *Input System Package (New)* (or *Both*).

## Step 3 — Copy the Phase 1 files into the project

With the editor open, run in PowerShell:

```powershell
robocopy "D:\Work\Projects\App-DoomShooter-Project\unity-source\Assets" "D:\Work\Projects\App-DoomShooter-Project\WarehouseDuel\Assets" /E
```

Switch back to Unity; it imports and compiles. **The Console must show no red errors.** If it does, copy them to me.

(After this step `unity-source/` is no longer needed; later phases write directly into `WarehouseDuel/`.)

## Step 4 — Import TextMesh Pro essentials

**Window → TextMeshPro → Import TMP Essential Resources** → **Import**.
(The scene builder in step 6 refuses to run until this is done, because otherwise every label would have no font.)

## Step 5 — Photon Fusion 2.1.4 and App ID

1. Create a free account at **dashboard.photonengine.com**.
2. **Create a new app** → Application type *Multiplayer Game* → Photon SDK **Fusion** → SDK version **Fusion 2** → name it "Warehouse Duel" → Create.
   Copy the **App ID** (a GUID like `1a2b3c4d-....`).
3. Download the **Fusion 2.1.4 Stable** `.unitypackage` from `doc.photonengine.com/fusion/current/getting-started/sdk-download` (login required).
4. Unity → **Assets → Import Package → Custom Package…** → select the file → **Import** everything.
5. The **Fusion Hub** opens (otherwise **Tools → Fusion → Fusion Hub**). Paste the App ID into the App ID field.
6. Leave **Fixed Region empty**.

Keep your App ID private: it is tied to your Photon account and CCU limit. Don't post it publicly.

## Step 6 — Run the project setup

**Tools → Warehouse Duel → Phase 1 - Run Project Setup**

It does the following, and prints each step in the Console:

| Step | Result |
|---|---|
| Folders | Creates every folder in `docs/ARCHITECTURE.md §6` that is missing |
| Editor | Asset Serialization = Force Text (Fusion requirement), Color Space = Linear |
| Identity | Product name "Warehouse Duel"; company and bundle id **only if still Unity defaults** (placeholder `com.warehouseduelstudio.warehouseduel`; replace before publishing) |
| Orientation | Landscape Left/Right auto-rotate, no portrait |
| Android | IL2CPP, ARM64, min API 26, target API = highest installed, INTERNET permission forced |
| iOS | Minimum iOS 15.0 |
| Misc | Run In Background on (two local instances keep running when unfocused) |
| Layers | 8 Environment, 9 Player, 10 Hitbox, 11 FirstPersonView |
| Scenes | Creates `Assets/Scenes/SplashScreen, MainMenu, Lobby, WarehouseMap` if they don't exist; existing scenes are never overwritten |
| Build list | 0 SplashScreen, 1 MainMenu, 2 Lobby, 3 WarehouseMap; other scenes (e.g. SampleScene) kept but disabled |

Running it again is safe; it only changes what isn't already correct.

## Step 7 — Validate

**Tools → Warehouse Duel → Validate Project.** Expected result: **0 errors**. Warnings you can accept for now:
- *bundle id set to PLACEHOLDER*: expected until you choose your own id
- *iOS Build Support is not installed*: only if you skipped that module
- *Active build target is StandaloneWindows64* is shown as INFO, not a warning

## Step 8 — Switch to Android (optional now, required by Phase 3 device tests)

**File → Build Profiles → Android → Switch Platform.** The first switch reimports assets and can take several minutes.
Re-run **Validate Project** afterwards.

---

## Phase 1 test checklist

| # | Test | Expected |
|---|---|---|
| 1 | Console after import | No compile errors |
| 2 | Validate Project | 0 errors |
| 3 | Open `Assets/Scenes/SplashScreen`, press **Play** | Fades in "WAREHOUSE DUEL", "1V1 ONLINE ARENA", animated "LOADING…" dots, the bar fills over about 2.5 s |
| 4 | Wait | Fades out and switches to the **MAIN MENU** scaffold scene |
| 5 | Console during Play | Exactly one line `[AppBootstrap] Warehouse Duel v… target 60 FPS…`, no errors |
| 6 | Game view → set a phone resolution (e.g. 2400×1080 and 1024×768) and press Play | Splash layout stays centred and readable on both |
| 7 | Open MainMenu / Lobby / WarehouseMap | Each opens without missing-script warnings. WarehouseMap contains the Environment / Spawn Points / Gameplay / Lighting roots, a Sun and an Overview Camera |
| 8 | Optional: Android device, **File → Build Profiles → Android → Build And Run** | App launches in landscape, splash → main menu, screen doesn't dim |

What is **not** in Phase 1 (so its absence is expected): menu buttons, networking, the map, the player.
The MainMenu and Lobby scenes are labelled scaffolds until Phases 5 and 7.

## Troubleshooting

| Symptom | Fix |
|---|---|
| `The type or namespace 'TMPro' could not be found` | Package Manager → make sure *uGUI* is installed (Unity 6 includes TMP in it) |
| `'InputSystem' does not exist in the namespace` | Install Input System (step 2) and set Active Input Handling |
| `UniversalRenderPipelineAsset could not be found` | The project wasn't created from the URP template; install *Universal RP* and assign a URP asset in Graphics/Quality settings |
| Setup says layer slot already used | Another layer occupies 8–11: rename it in **Project Settings → Tags and Layers**, re-run setup |
| Splash shows "SCENE 'MainMenu' MISSING…" | Run the setup again (build list not applied) |
| Validator can't find `AppIdFusion` field | The SDK layout differs; check the App ID visually in Fusion Hub and tell me the field name so the validator can be updated |
