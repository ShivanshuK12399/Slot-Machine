#if UNITY_EDITOR
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;
using SlotMachine.Config;

namespace SlotMachine.Editor
{
    /// <summary>
    /// Editor utility to generate Phase 1 ScriptableObject assets (SymbolData and PayoutTable).
    /// </summary>
    public static class SlotMachineAssetGenerator
    {
        private const string DataFolder = "Assets/Data";

        [MenuItem("Tools/Slot Machine/Generate Phase 1 Data Assets")]
        public static void GenerateDataAssets()
        {
            if (!AssetDatabase.IsValidFolder(DataFolder))
            {
                AssetDatabase.CreateFolder("Assets", "Data");
            }

            var symbolConfigs = new[]
            {
                (Type: SymbolType.Symbol1, Name: "Symbol 1", Multiplier: 5,  Path: "Assets/UI/slot-symbol1.png"),
                (Type: SymbolType.Symbol2, Name: "Symbol 2", Multiplier: 10, Path: "Assets/UI/slot-symbol2.png"),
                (Type: SymbolType.Symbol3, Name: "Symbol 3", Multiplier: 20, Path: "Assets/UI/slot-symbol3.png"),
                (Type: SymbolType.Symbol4, Name: "Symbol 4", Multiplier: 50, Path: "Assets/UI/slot-symbol4.png")
            };

            var createdSymbols = new List<SymbolData>(4);
            var payoutRules = new List<PayoutRule>(4);

            for (int i = 0; i < symbolConfigs.Length; i++)
            {
                var cfg = symbolConfigs[i];
                string assetPath = $"{DataFolder}/SymbolData_{i + 1}.asset";

                var symbolAsset = AssetDatabase.LoadAssetAtPath<SymbolData>(assetPath);
                if (symbolAsset == null)
                {
                    symbolAsset = ScriptableObject.CreateInstance<SymbolData>();
                    AssetDatabase.CreateAsset(symbolAsset, assetPath);
                }

                // Load sprite from path
                Sprite sprite = LoadSpriteAtPath(cfg.Path);

                symbolAsset.SetInitialData(cfg.Type, cfg.Name, sprite, cfg.Multiplier, isBonus: false);
                EditorUtility.SetDirty(symbolAsset);
                createdSymbols.Add(symbolAsset);

                payoutRules.Add(new PayoutRule
                {
                    Symbol = cfg.Type,
                    MatchCount = 3,
                    PayoutMultiplier = cfg.Multiplier,
                    WinDescription = cfg.Multiplier >= 50 ? "JACKPOT!" : cfg.Multiplier >= 20 ? "MEGA WIN!" : cfg.Multiplier >= 10 ? "SUPER WIN!" : "BIG WIN!"
                });
            }

            // Create or update PayoutTable
            string tablePath = $"{DataFolder}/PayoutTable.asset";
            var payoutTable = AssetDatabase.LoadAssetAtPath<PayoutTable>(tablePath);
            if (payoutTable == null)
            {
                payoutTable = ScriptableObject.CreateInstance<PayoutTable>();
                AssetDatabase.CreateAsset(payoutTable, tablePath);
            }

            payoutTable.Configure(createdSymbols, payoutRules);
            EditorUtility.SetDirty(payoutTable);

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Debug.Log($"[SlotMachine] Phase 1 Data Assets successfully generated in '{DataFolder}'.");
        }

        private static Sprite LoadSpriteAtPath(string path)
        {
            // Try loading sprite directly
            var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(path);
            if (sprite != null) return sprite;

            // If it's a multiple sprite sheet, load the first sub-sprite
            var allAssets = AssetDatabase.LoadAllAssetsAtPath(path);
            for (int i = 0; i < allAssets.Length; i++)
            {
                if (allAssets[i] is Sprite subSprite)
                {
                    return subSprite;
                }
            }

            return null;
        }
    }
}
#endif

