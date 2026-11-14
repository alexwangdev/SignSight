// Based on FixRocketboxMaxImport.cs from Microsoft Rocketbox (MIT License):
// https://github.com/microsoft/Microsoft-Rocketbox/blob/master/Assets/Editor/FixRocketboxMaxImport.cs
//
// Changes for SignSight (kept minimal):
// - Only processes assets under Assets/Models/Rocketbox/ (the original ran on every imported asset,
//   recolouring all model materials and throwing on models without a Bip01 rig).
// - Imports as Humanoid instead of Generic (the teacher is driven through a Humanoid avatar).
// - Prefers the "lowpoly" mesh when a file has several poly levels, otherwise keeps what the file has
//   (the original kept "hipoly"; the facial FBX ships only a hipoly mesh).
// - Null checks around the Bip01 hierarchy.
using UnityEditor;
using UnityEngine;

public class FixRocketboxMaxImport : AssetPostprocessor
{
    private const string RocketboxFolder = "Assets/Models/Rocketbox/";
    private static readonly string[] PolyLevels = { "lowpoly", "midpoly", "hipoly", "ultralowpoly" }; // preference order

    private bool IsRocketbox => assetPath.Replace('\\', '/').StartsWith(RocketboxFolder);

    private void OnPostprocessMaterial(Material material)
    {
        if (!IsRocketbox) return;
        // 3ds Max materials assumed the diffuse colour came from the texture; Unity multiplies texture and
        // colour, so reset the colour to white.
        material.color = Color.white;
        // Legacy Standard-shader fix from the original: transparent "Fade" instead of glass-like hair.
        if (material.HasProperty("_Mode") && material.GetFloat("_Mode") == 3f) material.SetFloat("_Mode", 2f);
    }

    private void OnPreprocessTexture()
    {
        if (!IsRocketbox) return;
        // Textures named "normal" are normal maps (avoids Unity's fix-it dialog).
        if (assetPath.ToLower().Contains("normal"))
        {
            var textureImporter = (TextureImporter)assetImporter;
            textureImporter.textureType = TextureImporterType.NormalMap;
            textureImporter.convertToNormalmap = false;
        }
    }

    private void OnPreprocessModel()
    {
        if (!IsRocketbox) return;
        var importer = (ModelImporter)assetImporter;
        importer.animationType = ModelImporterAnimationType.Human;
        importer.avatarSetup = ModelImporterAvatarSetup.CreateFromThisModel;
        importer.importAnimation = false;
        importer.importBlendShapes = true;
    }

    private void OnPostprocessModel(GameObject g)
    {
        if (!IsRocketbox) return;
        if (g.transform.Find("Bip02") != null) RenameBip(g);
        KeepOnePolyLevel(g);
    }

    private static void KeepOnePolyLevel(GameObject g)
    {
        string chosen = null;
        foreach (var level in PolyLevels)
        {
            foreach (Transform child in g.transform)
            {
                if (child.name.ToLower().Contains(level)) { chosen = level; break; }
            }
            if (chosen != null) break;
        }
        if (chosen == null) return;
        foreach (Transform child in g.transform)
        {
            string n = child.name.ToLower();
            bool isMesh = n.Contains("poly");
            // "lowpoly" is also a substring of "ultralowpoly"; match the chosen level exactly.
            bool keep = chosen == "lowpoly" ? n.Contains("lowpoly") && !n.Contains("ultralowpoly") : n.Contains(chosen);
            if (isMesh && !keep) child.gameObject.SetActive(false);
        }
    }

    private static void RenameBip(GameObject currentBone)
    {
        currentBone.name = currentBone.name.Replace("Bip02", "Bip01");
        for (int i = 0; i < currentBone.transform.childCount; i++)
        {
            RenameBip(currentBone.transform.GetChild(i).gameObject);
        }
    }
}
