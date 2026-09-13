using BepInEx;
using BepInEx.Logging;
using BepInEx.Unity.IL2CPP;
using HarmonyLib;
using Motions;
using Il2CppInterop.Runtime.Injection;
using UnityEngine;

namespace ProduceExample;

[BepInPlugin(GUID, NAME, VERSION)]
public class Main : BasePlugin
{
    public const string GUID = $"{AUTHOR}.{NAME}";
    public const string NAME = "ProduceExample";
    public const string VERSION = "1.0.0";
    public const string AUTHOR = "Shnaenae";
    public static ManualLogSource log;

    public override void Load()
    {
        Harmony harmony = new Harmony(GUID);
        log = new ManualLogSource(NAME);
        BepInEx.Logging.Logger.Sources.Add(log);

        // Produce system
        ClassInjector.RegisterTypeInIl2Cpp<UniqueAddonTest>();
        ClassInjector.RegisterTypeInIl2Cpp<ProduceViewDriver>();
        ClassInjector.RegisterTypeInIl2Cpp<F9KeyScanner>();
        var scannerObject = new GameObject("F9KeyScannerObject");
        scannerObject.AddComponent<F9KeyScanner>();
        UnityEngine.Object.DontDestroyOnLoad(scannerObject);
        harmony.PatchAll(typeof(RegisterProduce));
        harmony.PatchAll(typeof(ProduceRuntimePatches));

        log.LogInfo($"[{NAME}] Loaded successfully.");

        System.Action<string, string, LogType> managedCallback = (condition, stackTrace, type) =>
        {
            if (type == LogType.Exception)
                Main.log?.LogError($"[EXCEPTION] {condition}\n{stackTrace}");
        };
        Application.LogCallback callback = managedCallback;
        UnityEngine.Application.add_logMessageReceived(callback);
    }
}
