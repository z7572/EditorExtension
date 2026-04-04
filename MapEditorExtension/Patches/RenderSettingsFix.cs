using HarmonyLib;
using UnityEngine;
using UnityEngine.Rendering;
using LevelEditor;

namespace EditorExtension.Patches;

// QOL-Ex feature
public static class RenderSettingsFix
{
    [HarmonyPatch(typeof(LevelCreator))]
    private static class LevelCreatorPatches
    {
        [HarmonyPatch("Start")]
        [HarmonyPostfix]
        private static void StartPostfix(LevelCreator __instance)
        {
            // Below are MainScene's render settings, apply them to LevelEditor so bullet (magic)color (in WeaponPatches) works properly
            RenderSettings.ambientEquatorColor = Color.white;
            RenderSettings.ambientGroundColor = Color.white;
            RenderSettings.ambientLight = Color.white;
            RenderSettings.ambientMode = AmbientMode.Trilight;
            RenderSettings.ambientProbe = new SphericalHarmonicsL2();
            RenderSettings.ambientSkyColor = Color.white;
            RenderSettings.defaultReflectionMode = DefaultReflectionMode.Custom; // Different from MainScene
            RenderSettings.skybox = Helper.skyBoxMat;
            RenderSettings.sun.enabled = false;
        }
    }

    [HarmonyPatch(typeof(GameManager))]
    private static class GameManagerPatches
    {
        [HarmonyPatch("Start")]
        public static void StartPostfix(GameManager __instance)
        {
            foreach (var mat in Resources.FindObjectsOfTypeAll<Material>())
            {
                if (mat.name == "blueSky")
                {
                    Object.DontDestroyOnLoad(mat);
                    Helper.skyBoxMat = mat;
                }
            }
        }
    }
}