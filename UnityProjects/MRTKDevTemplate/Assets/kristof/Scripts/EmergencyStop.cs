using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.UI;
#if UNITY_ANDROID && !UNITY_EDITOR
using UnityEngine.Android;
#endif

public class EmergencyStop : MonoBehaviour
{
    [Header("Bluetooth nastavenia")]
    [SerializeField] private string deviceName = "R1D78FEFE";

    [Header("UI komponenty")]
    [SerializeField] private Button stopButton;
    [SerializeField] private Text connectionStatusText;
    [SerializeField] private Image connectionIndicator;

    [Header("Farby indikátora")]
    [SerializeField] private Color connectedColor = Color.green;
    [SerializeField] private Color disconnectedColor = Color.red;

    [Header("TESTOVACÍ REŽIM")]
    [SerializeField] private bool testMode = true;  // TEST MODE ZAPNUTÝ
    [SerializeField] private bool simulateConnection = true; // Simulujeme pripojenie

    private const string charServ = "69400001-b5a3-f393-e0a9-e50e24dcca99";
    private const string charSend = "69400002-b5a3-f393-e0a9-e50e24dcca99";

    private SimpleBleDevice device = null;
    private CancellationTokenSource cts = new CancellationTokenSource();
    private bool isConnected = false;

    private void Awake()
    {
        // Nastavenie UI
        if (stopButton != null)
        {
            stopButton.onClick.AddListener(ExecuteEmergencyStop);

            // Veľké tlačidlo cez celú obrazovku
            var rectTransform = stopButton.GetComponent<RectTransform>();
            rectTransform.anchorMin = Vector2.zero;
            rectTransform.anchorMax = Vector2.one;
            rectTransform.offsetMin = Vector2.zero;
            rectTransform.offsetMax = Vector2.zero;

            // Pridať text na tlačidlo ak nie je
            var buttonText = stopButton.GetComponentInChildren<Text>();
            if (buttonText != null)
            {
                buttonText.text = "STOP";
                buttonText.fontSize = 100;
                buttonText.alignment = TextAnchor.MiddleCenter;
            }
        }

        if (testMode)
        {
            Debug.Log("=== TESTOVACÍ REŽIM ===");
            Debug.Log("Aplikácia beží v testovacom režime bez BLE");
            if (simulateConnection)
            {
                isConnected = true;
                UpdateConnectionStatus(true);
                UpdateStatusText("TESTOVACÍ REŽIM");
            }
            else
            {
                UpdateConnectionStatus(false);
                UpdateStatusText("TEST - Odpojené");
            }
        }
        else
        {
            UpdateConnectionStatus(false);
            UpdateStatusText("Nepripojené");
        }
    }

    private async void Start()
    {
        if (testMode)
        {
            Debug.Log("Testovací režim - BLE pripojenie preskočené");
            return;
        }

        #if UNITY_ANDROID && !UNITY_EDITOR
        await RequestPermissions();
        #endif

        _ = ConnectToDeviceAsync();
    }

    private async Task ConnectToDeviceAsync()
    {
        UpdateStatusText("Pripájam sa...");

        while (!cts.IsCancellationRequested && !isConnected)
        {
            await Task.Run(() =>
            {
                device = new SimpleBleDevice();
                if (device.ScanAndConnectByName(deviceName))
                {
                    isConnected = true;

                    // Inicializácia
                    device.WriteCharacteristic(charServ, charSend,
                        Crc16Modbus.AddCrcBytes(new byte[] { 242, 1, 0, 1, 0, 100 }));
                    device.WriteCharacteristic(charServ, charSend,
                        Crc16Modbus.AddCrcBytes(new byte[] { 242, 8, 6, 1, 0, 4 }));
                }
            }, cts.Token);

            if (isConnected)
            {
                UpdateConnectionStatus(true);
                UpdateStatusText("Pripojené");
            }
            else
            {
                UpdateStatusText("Nepripojené - skúšam znova...");
                await Task.Delay(2000, cts.Token);
            }
        }
    }

    public void ExecuteEmergencyStop()
    {
        Debug.Log("=== STOP TLAČIDLO STLAČENÉ ===");

        if (testMode)
        {
            // V testovacom režime len vypíšeme čo by sa poslalo
            byte[] stopCommand = Crc16Modbus.AddCrcBytes(new byte[] { 242, 8, 40, 2, 0, 128, 128 });
            Debug.Log($"[TEST] Posielam STOP príkaz: {ByteArrayToString(stopCommand)}");
            Debug.Log("[TEST] Vozík by mal zastaviť");

            UpdateStatusText("STOP príkaz odoslaný (TEST)");
            return;
        }

        if (isConnected && device != null)
        {
            // Poslať neutrálne hodnoty (zastavenie)
            device.WriteCharacteristic(charServ, charSend,
                Crc16Modbus.AddCrcBytes(new byte[] { 242, 8, 40, 2, 0, 128, 128 }));

            UpdateStatusText("STOP vykonaný!");
            Debug.Log("[Emergency] Príkaz STOP odoslaný");
        }
        else
        {
            UpdateStatusText("Nie je pripojené!");
            Debug.LogWarning("[Emergency] Nie je pripojené k vozíku");
        }
    }

    // Pomocná funkcia na výpis byte poľa
    private string ByteArrayToString(byte[] bytes)
    {
        return string.Join(" ", bytes.Select(b => b.ToString("X2")));
    }

    private void UpdateConnectionStatus(bool connected)
    {
        if (connectionIndicator != null)
        {
            connectionIndicator.color = connected ? connectedColor : disconnectedColor;
        }
    }

    private void UpdateStatusText(string status)
    {
        if (connectionStatusText != null)
        {
            connectionStatusText.text = status;
        }
    }

    #if UNITY_ANDROID && !UNITY_EDITOR
    private async Task RequestPermissions()
    {
        var permissions = new string[]
        {
            Permission.FineLocation,
            "android.permission.BLUETOOTH_SCAN",
            "android.permission.BLUETOOTH_CONNECT"
        };

        bool granted = await WaitForPermissionsAsync(permissions, cts.Token);

        if (granted)
        {
            using (var unityPlayer = new AndroidJavaClass("com.unity3d.player.UnityPlayer"))
            using (var activity = unityPlayer.GetStatic<AndroidJavaObject>("currentActivity"))
            using (var classLoader = activity.Call<AndroidJavaObject>("getClassLoader"))
            {
                SimpleBleNative.SetRegistryClassLoader(classLoader.GetRawObject());
            }
        }
    }

    private Task<bool> WaitForPermissionsAsync(string[] permissions, CancellationToken token)
    {
        var tcs = new TaskCompletionSource<bool>();
        int remaining = permissions.Length;
        bool allGranted = true;

        var callbacks = new PermissionCallbacks();

        void HandleResponse(string perm, bool granted)
        {
            if (!granted) allGranted = false;
            remaining--;
            if (remaining == 0) tcs.TrySetResult(allGranted);
        }

        callbacks.PermissionGranted += perm => HandleResponse(perm, true);
        callbacks.PermissionDenied += perm => HandleResponse(perm, false);
        callbacks.PermissionDeniedAndDontAskAgain += perm => HandleResponse(perm, false);

        Permission.RequestUserPermissions(permissions, callbacks);
        token.Register(() => tcs.TrySetCanceled());

        return tcs.Task;
    }
    #endif

    private void Update()
    {
        // Len na test - keď stlačíš medzerník, spustí sa STOP
        if (Input.GetKeyDown(KeyCode.Space))
        {
            Debug.Log("Medzerník stlačený - volám ExecuteEmergencyStop");
            ExecuteEmergencyStop();
        }
    }

    private void OnDestroy()
    {
        cts?.Cancel();
        cts?.Dispose();

        if (device != null)
        {
            if (isConnected)
            {
                device.WriteCharacteristic(charServ, charSend,
                    Crc16Modbus.AddCrcBytes(new byte[] { 242, 8, 7, 1, 0, 4 }));
            }
            device.Dispose();
            device = null;
        }
    }

    private void OnApplicationQuit()
    {
        OnDestroy();
    }
}
