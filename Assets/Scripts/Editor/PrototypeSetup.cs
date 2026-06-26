#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace LearnToSpin
{
    /// <summary>One-click: drops a GameBootstrap into the open scene so you can just press Play.</summary>
    static class PrototypeSetup
    {
        const string PrefabRoot = "Assets/Dreamplex/RetroBarnFarmAndBirches/Prefabs";

        [MenuItem("LearnToSpin/Setup Prototype In Current Scene")]
        static void Setup()
        {
            if (Object.FindFirstObjectByType<GameBootstrap>() != null)
            {
                Debug.LogWarning("LearnToSpin: GameBootstrap already present in this scene. " +
                                 "Use \"Rewire Art Prefabs\" to refresh its art references.");
                return;
            }
            var go = new GameObject("GameBootstrap");
            var boot = go.AddComponent<GameBootstrap>();
            WireArt(boot);
            WireTires(boot);
            Selection.activeGameObject = go;
            Undo.RegisterCreatedObjectUndo(go, "Setup LearnToSpin Prototype");
            Debug.Log("LearnToSpin: GameBootstrap added and art wired. " +
                      "Press Play to launch the tire (hold Space, release).");
        }

        [MenuItem("LearnToSpin/Rewire Art Prefabs")]
        static void Rewire()
        {
            var boot = Object.FindFirstObjectByType<GameBootstrap>();
            if (boot == null)
            {
                Debug.LogWarning("LearnToSpin: no GameBootstrap in the scene — run \"Setup Prototype\" first.");
                return;
            }
            WireArt(boot);
            WireTires(boot);
            Debug.Log("LearnToSpin: art prefabs + tire catalog rewired on GameBootstrap.");
        }

        const string CatalogPath = "Assets/TireCatalog.asset";

        /// <summary>
        /// Ensures a TireCatalog asset exists (seeded from the in-code defaults), fills each tire's
        /// wheel prefab + shop icon from the Assets/wheel pack, and assigns it on the bootstrap.
        /// </summary>
        static void WireTires(GameBootstrap boot)
        {
            var cat = AssetDatabase.LoadAssetAtPath<TireCatalog>(CatalogPath);
            if (cat == null)
            {
                cat = TireCatalog.CreateDefault();
                AssetDatabase.CreateAsset(cat, CatalogPath);
            }
            else
            {
                // Re-seed the balance fields from the current defaults so tuning changes (and newly
                // added fields like upgradePotency/earnMultiplier, which deserialize to 0 on an old
                // asset) take effect — art is re-fetched below regardless. The upgrade tracks and
                // payout rates are plain serialized fields on the asset (NOT inside `tires`), so they
                // must be copied across too or asset edits would silently shadow the code defaults.
                var def = TireCatalog.CreateDefault();
                cat.tires = def.tires;
                cat.boostUpgrade = def.boostUpgrade;
                cat.spinUpgrade = def.spinUpgrade;
                cat.bounceUpgrade = def.bounceUpgrade;
                cat.moneyPerMetre = def.moneyPerMetre;
                cat.moneyPerTopSpeed = def.moneyPerTopSpeed;
                cat.moneyPerHeight = def.moneyPerHeight;
                cat.moneyPerAirTime = def.moneyPerAirTime;
            }

            for (int i = 0; i < cat.tires.Length; i++)
            {
                string n = $"wheel_{i + 1:00}";
                cat.tires[i].prefab = FindAsset<GameObject>(n, "t:Prefab");
                cat.tires[i].icon = FindAsset<Texture2D>($"{n}_icon", "t:Texture2D");
            }

            boot.tireCatalog = cat;
            EditorUtility.SetDirty(cat);
            EditorUtility.SetDirty(boot);
            AssetDatabase.SaveAssets();
        }

        /// <summary>Find an asset by exact file name (dodges the wheel İcon folder's special char).</summary>
        static T FindAsset<T>(string assetName, string typeFilter) where T : Object
        {
            var guids = AssetDatabase.FindAssets($"{assetName} {typeFilter}");
            foreach (var g in guids)
            {
                string p = AssetDatabase.GUIDToAssetPath(g);
                if (System.IO.Path.GetFileNameWithoutExtension(p) == assetName)
                    return AssetDatabase.LoadAssetAtPath<T>(p);
            }
            if (guids.Length > 0)
                return AssetDatabase.LoadAssetAtPath<T>(AssetDatabase.GUIDToAssetPath(guids[0]));
            Debug.LogWarning($"LearnToSpin: wheel asset not found: {assetName}");
            return null;
        }

        /// <summary>Loads the RetroBarnFarmAndBirches prefabs and assigns them on the bootstrap.</summary>
        static void WireArt(GameBootstrap boot)
        {
            boot.roadPrefabs = Load("sand_road_7", "sand_road_9");

            boot.hazardPrefabs = Load(
                // trees
                "Birches/OneModelBirches/birch_small_2_green_leaves",
                "Birches/OneModelBirches/birch_medium_orange_leaves",
                "birch_trunk_1",
                "birch_trunk_4",
                // rocks
                "rock medium",
                "rock_small",
                "rocks",
                // barrels
                "barrel_close_red",
                "barrel_open_blue",
                "barrel_blue_lid",
                "barrel_damaged_blue",
                // other props
                "woodchips_7",
                "cow_drinker",
                "Bush/bush orange leaves");

            boot.fencePrefab = LoadOne("white_fence_side");
            boot.lampPrefab = LoadOne("lamp");

            // Decorative scenery placed outside the fences by SceneryScatter.
            boot.sceneryTreePrefabs = Load(
                "Birches/OneModelBirches/birch_small_2_green_leaves",
                "Birches/OneModelBirches/birch_medium_orange_leaves",
                "Birches/Tree Ivy Half Green Variants/birch_small_2_green_leaves Variant");
            boot.sceneryBushPrefabs = Load("Bush/bush orange leaves");
            boot.sceneryIvyPrefabs = Load(
                "Ivy/ground ivy green medium leaves 2",
                "Ivy/ivy_wall_small green leaves medium");

            EditorUtility.SetDirty(boot);
        }

        static GameObject LoadOne(string relativePath)
        {
            string path = $"{PrefabRoot}/{relativePath}.prefab";
            var go = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (go == null) Debug.LogWarning($"LearnToSpin: missing art prefab at {path}");
            return go;
        }

        static GameObject[] Load(params string[] relativePaths)
        {
            var list = new List<GameObject>(relativePaths.Length);
            foreach (var p in relativePaths)
            {
                var go = LoadOne(p);
                if (go != null) list.Add(go);
            }
            return list.ToArray();
        }
    }
}
#endif
