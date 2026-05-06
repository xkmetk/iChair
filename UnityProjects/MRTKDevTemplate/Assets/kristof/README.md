# `Assets/kristof` – eye-tracking ovládanie + BLE riadenie (Unity/MRTK/Pupil Labs)

Tento priečinok obsahuje prototypové komponenty pre ovládanie (vozíka / platformy) **pohľadom** a **zatvorením očí** cez Pupil Labs (gaze + eyelid) a odosielanie riadiacich hodnôt cez **BLE** (X/Y v rozsahu 0–255, neutrál \(128,128\)).

## Čo je tu dôležité

- **Dve hlavné UX vetvy ovládania**
  - **„Drive mode“ cez tlačidlá a submenu (Slow/Fast)**: `DriveModeGUIController_v2` + `SubmenuAutoHide` + `ButtonHighlightManager` + `BleVehicleController`
  - **„Gaze zones“ joystick (inkrementálne úrovne v smere)**: `GazeZoneJoystickV2` + `ArrowStackUI` + `TestBle`
- **Mapovanie GUI → skripty (aby bolo jasné „čo patrí ku čomu“)**
  - **`GUI_A`**: `MenuManager`, `BleVehicleController`
  - **`GUI_B`**: `BleVehicleController`, `DriveModeGUIController_v2`, `ButtonHighlightManager`
  - **`GUI_C`**: `Joystick`
  - **`GUI_D`**: `GazeZoneJoystickV2`
  - **Spoločné**:
    - všetky GUI používajú `BLE` (posielanie X/Y do `TestBle`)
    - `GUI_A` a `GUI_B` používajú `BleVehicleController` ako **wrapper** nad `BLE`/`TestBle`
    - `GUISwitcherPanel` ovláda **prepínanie GUI** (fixácia pohľadom + eye-close stop/switch)
- **Bezpečnostné STOP správanie**
  - `IEyeCloseStopper` je jednoduché rozhranie, ktoré umožní GUI scénam reagovať na zatvorenie očí jednotne.
  - `GUISwitcherPanel` po zatvorení očí:
    - po kratšom čase pošle STOP (volá `EyeCloseStop()` na aktívnom GUI alebo fallback `BleVehicleController.StopAll()`),
    - po dlhšom čase otvorí „switcher“ panel na prepnutie GUI.
- **Odosielanie BLE dát**
  - Skripty posielajú do `TestBle.SetXY(x,y)` (nie je v tomto priečinku).
  - Konvencia: **neutrál je \(128,128\)**.

## Štruktúra priečinka

- **`Scenes/`**
  - `BootScene.unity`: štartovacia scéna (typicky obsahuje init / switcher / základné objekty).
  - `PL_Calibration_Short.unity`: krátka kalibračná scéna (Pupil Labs).
- **`Scripts/`** (kľúčové)
  - `DriveModeGUIController_v2.cs`: hlavný stavový automat pre „drive mode“ UI (Idle → výber rýchlosti → jazda vpred/vzad, turn tlačidlá, STOP/Confirm).
  - `BleVehicleController.cs`: vysoká úroveň príkazov „dopredu/dozadu/ľavo/pravо“ + dynamické zatáčanie počas jazdy (ramp intenzity v čase).
  - `SubmenuAutoHide.cs`: submenu (Slow/Fast) s auto-hide timerom; po výbere sa „pinne“ a ostáva otvorené.
  - `ButtonHighlightManager.cs`: vizuálne zvýraznenie zvoleného tlačidla cez materiály (hľadá renderer cez MRTK hierarchiu).
  - `GUISwitcherPanel.cs`: prepínanie medzi GUI variantami pohľadom (fixácia) a eye-close logika (STOP + otvorenie switchera).
  - `GazeZoneJoystickV2.cs`: zónové ovládanie (Left/Right/Up/Down) s úrovňami intenzity, stop pri strate gaze a stop pri zavretí očí; posiela BLE X/Y a ukazuje úrovne cez šípky.
  - `Joystick.cs`: jednoduchší „follow gaze“ joystick (posiela kontinuálne X/Y); obsahuje auto-reset keď je gaze mimo zóny.
  - `BLE.cs` (namespace `PupilLabs`): jednoduché mapovanie pointer offsetu na X/Y; `FollowGaze` sa vypína ak nie je hit.
  - `ArrowStackUI.cs`: pomocný UI komponent – zobrazuje „stack“ šípok podľa úrovne (level).
  - `IEyeCloseStopper.cs`: rozhranie pre STOP pri zavretí očí.
- **`Prefabs/`**
  - Rôzne GUI prefaby (`GUI_A/B/C/D...`), šípky (`ArrowPrefab`, `ArrowHead`) a vizuálne assety pre UI.
- **`Material/`**
  - Materiály pre MRTK backplates a ikony používané v UI (napr. stavy idle/drive/stop).

## Ako to zapojiť (prakticky v Unity)

### 1) Drive mode (tlačidlá + submenu)

- **Scéna/prefab potrebuje**
  - `DriveModeGUIController_v2` s prepojenými referenciami na:
    - `BleVehicleController vehicle`
    - `ButtonHighlightManager highlight`
    - tlačidlá (Up/Down/Left/Right/Stop/Confirm, Slow/Fast pre smery, drive turn tlačidlá)
    - `SubmenuAutoHide` komponenty pre forward/left/right/backward submenu
  - `BleVehicleController` s prepojeným `TestBle ble`
- **Logika**
  - Najprv sa vyberie **rýchlosť** (Slow/Fast) pre smer → potom sa prejde do jazdy.
  - Pri jazde sú aktívne „drive turn“ tlačidlá, ktoré spúšťajú `StartTurnLeft/Right()` a rampujú intenzitu.
  - STOP vracia systém do neutrálneho stavu a posiela \(128,128\).

### 2) Gaze zones joystick (inkrementálne úrovne)

- `GazeZoneJoystickV2`:
  - potrebuje `pointer` (Transform), `TestBle ble`
  - voliteľne `ArrowStackUI` pre vizuálne šípky (Forward/Backward/Left/Right)
  - používa Pupil Labs `ServiceLocator` → `GazeDataVisualizer.onHit` a (mimo testMode) aj `GazeDataProvider` pre eyelid
- Správanie:
  - zóna sa aktivuje krátkou fixáciou na hrane (Up/Down/Left/Right), čím sa zvyšuje/znižuje úroveň v danom smere
  - pri **zatvorení očí** (prahová hodnota) sa vykoná STOP
  - voliteľne pri **strate gaze** sa vykoná STOP po krátkom oneskorení

## Testovanie bez eye-trackera

Niektoré skripty majú `testMode`:

- `GUISwitcherPanel` a `GazeZoneJoystickV2` v `testMode` simulujú „zatvorené oči“ klávesou `B` (konfigurovateľné cez `simulatedEyeCloseKey`).
- V `GazeZoneJoystickV2` je aj výpis do UI textu `eyelidSumText` (ak je prepojený), aby bolo vidieť prahy a hodnoty.

## Poznámky / gotchas

- Tento priečinok predpokladá existenciu komponentu `TestBle` s metódou `SetXY(...)`. Ak ho v projekte nevidíš, hľadaj mimo `Assets/kristof`.
- Mnohé skripty používajú Pupil Labs objekty cez `ServiceLocator` (napr. `GazeDataVisualizer`, `GazeDataProvider`). Bez nich sa komponenty vypnú (`enabled = false`).
- Renderery tlačidiel sa hľadajú cez typickú MRTK hierarchiu:
  - `UIBackplateOuterGeometry/UX.Button.BackplateOuterGeometry`
  - fallback `CompressibleButtonVisuals/FrontPlate`

