using System.Threading;
using System.Threading.Tasks;
using UnityEngine;
#if UNITY_ANDROID && !UNITY_EDITOR
using UnityEngine.Android;
#endif

public class BleGrafSipky : MonoBehaviour
{
    const string charServ = "69400001-b5a3-f393-e0a9-e50e24dcca99";
    const string charSend = "69400002-b5a3-f393-e0a9-e50e24dcca99";
    //const string charRecv = "69400003-b5a3-f393-e0a9-e50e24dcca99";

    private bool isConnected = false;

    private SimpleBleDevice device = null;

    private CancellationTokenSource cts = new CancellationTokenSource();

    private byte x = 128;
    private byte y = 128;

    private byte currentForwardSpeed = 128;
    private bool isForwardActive = false;

    // ---- Dynamic turning variables ----
    private bool isTurning = false;
    private int turnDirection = 0; // -1 = left, 1 = right
    private float turnHoldTime = 0f;

    [Header("Turning Settings")]
    public float maxTurnIntensity = 40f;   // max X offset
    public float rampUpTime = 1.5f;        // seconds needed to reach full turn
    public float turningSmooth = 8f;       // smoothing factor

    private bool isBackwardActive = false;
    private bool isBackwardLeftActive = false;
    private bool isBackwardRightActive = false;

    private byte currentBackwardSpeed = 128;


#if UNITY_ANDROID && !UNITY_EDITOR
    public static Task<bool> WaitForPermissionsAsync(string[] permissions, CancellationToken token)
    {
        var tcs = new TaskCompletionSource<bool>();
        int remaining = permissions.Length;
        bool allGranted = true;

        var callbacks = new PermissionCallbacks();

        void HandleResponse(string perm, bool granted)
        {
            if (!granted)
            {
                allGranted = false;
            }
            remaining--;
            if (remaining == 0)
            {
                tcs.TrySetResult(allGranted);
            }
        }

        callbacks.PermissionGranted += perm => HandleResponse(perm, true);
        callbacks.PermissionDenied += perm => HandleResponse(perm, false);
        callbacks.PermissionDeniedAndDontAskAgain += perm => HandleResponse(perm, false);

        Permission.RequestUserPermissions(permissions, callbacks);

        token.Register(() => tcs.TrySetCanceled());

        return tcs.Task;
    }
#endif

    // Start is called before the first frame update
    async void Start()
    {
        CancellationToken token = cts.Token;
#if UNITY_ANDROID && !UNITY_EDITOR
        await WaitForPermissionsAsync(new string[] { Permission.FineLocation, "android.permission.BLUETOOTH_SCAN", "android.permission.BLUETOOTH_CONNECT" }, token);
        using (var unityPlayer = new AndroidJavaClass("com.unity3d.player.UnityPlayer"))
        using (var activity = unityPlayer.GetStatic<AndroidJavaObject>("currentActivity"))
        {
            using (var classLoader = activity.Call<AndroidJavaObject>("getClassLoader"))
            {
                SimpleBleNative.SetRegistryClassLoader(classLoader.GetRawObject());
            }
        }
#endif

        device = new SimpleBleDevice();
        await Task.Run(() =>
        {
            while (token.IsCancellationRequested == false)
            {
                if (device.ScanAndConnectByName("R1D78FEFE") == true)
                {
                    break;
                }
                Thread.Sleep(1000);
            }
        }, token);

        // let's do this after sync so we are sure isConnected reflects the real state
        device?.WriteCharacteristic(charServ, charSend, Crc16Modbus.AddCrcBytes(new byte[] { 242, 1, 0, 1, 0, 100 })); //TODO ensure send until success
        device?.WriteCharacteristic(charServ, charSend, Crc16Modbus.AddCrcBytes(new byte[] { 242, 8, 6, 1, 0, 4 }));

        cts.Dispose();
        cts = null;

        isConnected = true;

        while (isConnected)
        {
            await Task.Run(() => WriteXY(x, y));
            await Task.Delay(100);
            Debug.Log($"[TestBle] writing x:{x}, y:{y}");
        }

    }

    private void Update()
    {
        if (isForwardActive && isTurning)
        {
            // rastie podľa toho, ako dlho sa pozeráme na šípku
            turnHoldTime += Time.deltaTime;

            // 0..1 podľa rampUpTime (napr. 0.0 po 0s, 1.0 po 1.5s)
            float intensity01 = Mathf.Clamp01(turnHoldTime / rampUpTime);

            // premeníme na 0..maxTurnIntensity
            float targetOffset = intensity01 * maxTurnIntensity * turnDirection;

            // plynulé hýbanie X hodnoty
            float newX = Mathf.Lerp(x, 128 + targetOffset, Time.deltaTime * turningSmooth);

            // nastavíme nové XY
            SetXY((byte)newX, currentForwardSpeed);

            Debug.Log($"Steering X: {x}, intensity: {(turnHoldTime / rampUpTime):0.00}");

        }
    }


    private void WriteXY(byte x, byte y)
    {
        device.WriteCharacteristic(charServ, charSend, Crc16Modbus.AddCrcBytes(new byte[] { 242, 8, 40, 2, 0, x, y }));
    }

    public void SetXY(int x, int y)
    {
        this.x = (byte)x;
        this.y = (byte)y;
    }

    public void ForwardSlow()
    {
        Debug.Log("PRÍKAZ: VpredS");
        SetXY(128, 158);
        isForwardActive = true;
        currentForwardSpeed = 158;
    }

    public void ForwardFast()
    {
        Debug.Log("PRÍKAZ: VpredF");
        SetXY(128, 168);
        isForwardActive = true;
        currentForwardSpeed = 168;
    }

// aktivuje jemné zatáčanie doľava
    public void StartForwardLeft()
    {
        if (!isForwardActive) return;

        Debug.Log("Start turning LEFT");
        isTurning = true;
        turnDirection = -1;
        turnHoldTime = 0f;
    }


// aktivuje jemné zatáčanie doprava
    public void StartForwardRight()
    {
        if (!isForwardActive) return;

        Debug.Log("Start turning RIGHT");
        isTurning = true;
        turnDirection = 1;
        turnHoldTime = 0f;
    }


    public void StopTurning()
    {
        if (!isForwardActive) return;

        Debug.Log("Stop turning");
        isTurning = false;
        turnDirection = 0;
        turnHoldTime = 0f;

        // return to straight
        SetXY(128, currentForwardSpeed);
    }

    public void BackwardSlow()
    {
        Debug.Log("PRÍKAZ: VzadS");
        SetXY(128, 98);

        isBackwardActive = true;
        currentBackwardSpeed = 98;

        // deaktivujeme forward mód
        isForwardActive = false;
    }

    public void BackwardFast()
    {
        Debug.Log("PRÍKAZ: VzadF");
        SetXY(128, 88);

        isBackwardActive = true;
        currentBackwardSpeed = 88;

        isForwardActive = false;
    }


    public void StartBackwardLeft()
    {
        if (!isBackwardActive) return;

        Debug.Log("PRÍKAZ: Cúvam a zatáčam DOĽAVA");
        isBackwardLeftActive = true;
        isBackwardRightActive = false;

        byte turnX = 118; // rovnaké ako forward
        SetXY(turnX, currentBackwardSpeed);
    }

    public void StopBackwardLeft()
    {
        if (!isBackwardActive) return;

        Debug.Log("Prestávam zatáčať (cúvam rovno)");
        isBackwardLeftActive = false;

        if (!isBackwardRightActive)
            SetXY(128, currentBackwardSpeed);
    }

    public void StartBackwardRight()
    {
        if (!isBackwardActive) return;

        Debug.Log("PRÍKAZ: Cúvam a zatáčam DOPRAVA");
        isBackwardRightActive = true;
        isBackwardLeftActive = false;

        byte turnX = 138; // rovnaké ako forward
        SetXY(turnX, currentBackwardSpeed);
    }

    public void StopBackwardRight()
    {
        if (!isBackwardActive) return;

        Debug.Log("Prestávam zatáčať (cúvam rovno)");
        isBackwardRightActive = false;

        if (!isBackwardLeftActive)
            SetXY(128, currentBackwardSpeed);
    }

    public void LeftSlow()
    {
        Debug.Log("PRÍKAZ: VľavoS");
        SetXY(88, 128);
        isForwardActive = false;
        isBackwardActive = false;
    }
    public void LeftFast()
    {
        Debug.Log("PRÍKAZ: VľavoF");
        SetXY(78, 128);
        isForwardActive = false;
        isBackwardActive = false;

    }

    public void RightSlow()
    {
        Debug.Log("PRÍKAZ: VpravoS");
        SetXY(168, 128);
        isForwardActive = false;
        isBackwardActive = false;

    }
    public void RightFast()
    {
        Debug.Log("PRÍKAZ: VpravoF");
        SetXY(178, 128);
        isForwardActive = false;
        isBackwardActive = false;

    }

    public void Stop()
    {
        Debug.Log("PRÍKAZ: Stop");
        SetXY(128, 128);

        isForwardActive = false;
        //isForwardLeftActive = false;
        //isForwardRightActive = false;

        isBackwardActive = false;
        isBackwardLeftActive = false;
        isBackwardRightActive = false;
    }


    private void OnDestroy()
    {
        cts?.Cancel();
        if (isConnected)
        {
            device.WriteCharacteristic(charServ, charSend, Crc16Modbus.AddCrcBytes(new byte[] { 242, 8, 7, 1, 0, 4 }));
            isConnected = false;
        }
        device.Dispose();
        device = null;
    }
}
