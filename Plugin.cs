using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using HarmonyLib;
using LethalCompanyInputUtils.Api;
using LethalCompanyInputUtils.BindingPathEnums;
using System;
using System.Linq;
using UnityEngine.InputSystem;

namespace FPSChanger;

public class FPSCInputClass : LcInputActions
{
    [InputAction(KeyboardControl.F6, Name = "ChangeFPS")]
    public InputAction ChangeFPS { get; set; }
}

[BepInPlugin(MyPluginInfo.PLUGIN_GUID, MyPluginInfo.PLUGIN_NAME, MyPluginInfo.PLUGIN_VERSION)]
[BepInDependency("com.rune580.LethalCompanyInputUtils", BepInDependency.DependencyFlags.HardDependency)]
public class FPSChanger : BaseUnityPlugin  // Change shitty logic
{
    public static FPSChanger Instance { get; private set; } = null!;
    internal static new ManualLogSource Logger;

    private ConfigEntry<string> fpsValues = null!;
    internal string filePath = "LCGeneralSaveData";
    internal int[] ingameFPSCaps = [-1, 250, 144, 120, 60, 30];
    internal int currentGameIndex = 0;
    internal int currentModIndex = 0;
    internal int currentSetting = 0;
    internal int[] FPSValues;

    internal static FPSCInputClass InputActionsInstance;

    private void Awake()
    {
        // Plugin startup logic
        Logger = base.Logger;
        Instance = this;
        Logger.LogInfo($"Plugin {MyPluginInfo.PLUGIN_GUID} is loading!");

        fpsValues = Config.Bind(
            "FPSChanger",
            "FPS Values",
            CommaJoin([30, 250]),
            "Values of fps that would be cycled, comma-separated. Available options: 30, 60, 120, 144, 250, 0. 250 is written as \"Uncapped\" ingame and -1 is for Vsync"
        );

        if (!CommaSplit(fpsValues.Value, out FPSValues))
        {
            Logger.LogInfo($"Was not able to parse the config. The mod will not work, shutting it down");
            return;
        }

        Logger.LogInfo($"Config values: {FPSValues.Select(i => i.ToString()).Join(delimiter: ", ")}");

        if (!FPSValues.Select(i => ingameFPSCaps.Contains(i)).All(i => i))
        {
            Logger.LogError($"Wrong option in your config was found. Shutting down.");
            return;
        }

        currentGameIndex = ES3.Load("FPSCap", filePath, 0);
        currentSetting = ingameFPSCaps[currentGameIndex];
        Logger.LogInfo($"Current game index: {currentGameIndex}");
        Logger.LogInfo($"Current FPS setting: {(currentSetting != -1 ? currentSetting : "Vsync")}");

        currentModIndex = Array.IndexOf(FPSValues, currentSetting);
        Logger.LogInfo($"Current mod index: {currentModIndex}");

        InputActionsInstance = new();
        SetupKeybindCallbacks();
    }

    private static string CommaJoin(int[] i) => i.Join(delimiter: ",").ToString();

    private static bool CommaSplit(string input, out int[] values)
    {
        var parts = input.Split(',');
        values = new int[parts.Length];

        for (int i = 0; i < parts.Length; i++)
        {
            if (!int.TryParse(parts[i].Trim(), out values[i]))
            {
                values = [];
                return false;
            }
        }

        return true;
    }


    public void SetupKeybindCallbacks()
    {
        InputActionsInstance.ChangeFPS.performed += OnChangeFPSPressed;
    }

    public void OnChangeFPSPressed(InputAction.CallbackContext changeFPSConext)
    {
        if (!changeFPSConext.performed) return;

        var settings = IngamePlayerSettings.Instance.settings;

        if (settings == null)
        {
            Logger.LogError("Settings not found!");
            HUDManager.Instance.DisplayTip("FPSChanger", $"Settings not found!", true);
            return;
        }

        currentGameIndex = settings.framerateCapIndex;
        currentSetting = ingameFPSCaps[currentGameIndex];
        if (!FPSValues.Contains(currentSetting))
        {
            Logger.LogInfo($"Current FPS setting: {currentSetting}, not in the FPSValues array -> setting index to -1");
            currentModIndex = -1;
        }

        currentModIndex = (currentModIndex + 1) % FPSValues.Length;
        currentSetting = FPSValues[currentModIndex];
        currentGameIndex = Array.IndexOf(ingameFPSCaps, currentSetting);

        settings.framerateCapIndex = currentGameIndex;
        IngamePlayerSettings.Instance.UpdateGameToMatchSettings();
        try
        {
            ES3.Save("FPSCap", currentGameIndex, filePath);
        }
        catch
        {
            Logger.LogWarning("Was not able to write into the settings file");
        }

        string displayedSetting = currentSetting == -1 ? "Vsync" : currentSetting.ToString();
        HUDManager.Instance.DisplayTip("FPSChanger", $"Changed FPS cap to {displayedSetting}");
        Logger.LogInfo($"FPS cap changed to: {displayedSetting}, index: {currentGameIndex}");
    }
}