#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using TMPro;
using SlotMachine.Config;
using SlotMachine.Core;
using SlotMachine.Reel;
using SlotMachine.UI;
using SlotMachine.Audio;

namespace SlotMachine.Editor
{
    /// <summary>
    /// Editor automation tool to construct and wire the pixel-perfect 816x624 classic slot cabinet,
    /// layered reel backing, masked apertures, interactive mechanical lever, and bottom HUD dock (Phase 4).
    /// </summary>
    public static class SlotMachineSceneBuilder
    {
        private const string ScenePath = "Assets/Scenes/SampleScene.unity";
        private const string PayoutTablePath = "Assets/Data/PayoutTable.asset";
        private const string SymbolPrefabPath = "Assets/Prefabs/SlotSymbolItem.prefab";

        [InitializeOnLoadMethod]
        private static void OnScriptsReloaded()
        {
            EditorApplication.delayCall += () =>
            {
                if (EditorApplication.isPlayingOrWillChangePlaymode) return;

                var scene = EditorSceneManager.GetActiveScene();
                if (scene.isLoaded && scene.path == ScenePath)
                {
                    var canvas = Object.FindFirstObjectByType<Canvas>();
                    var controller = Object.FindFirstObjectByType<SlotMachineController>();
                    if (canvas != null && controller != null)
                    {
                        bool dirty = false;
                        if (Object.FindFirstObjectByType<AudioManager>() == null)
                        {
                            EnsureAudioManager(canvas.transform);
                            dirty = true;
                        }
                        if (Object.FindFirstObjectByType<WinCelebrationView>() == null)
                        {
                            CreateWinCelebrationModal(canvas.transform, controller);
                            dirty = true;
                        }
                        if (dirty)
                        {
                            EditorSceneManager.MarkSceneDirty(scene);
                            EditorSceneManager.SaveScene(scene);
                            Debug.Log("[SlotMachine] Phase 5 AudioManager & WinCelebrationView successfully added to scene!");
                        }
                    }
                }
            };
        }

        [MenuItem("Tools/Slot Machine/Setup Scene UI (Phase 4)")]
        public static void BuildSceneUI()
        {
            var activeScene = EditorSceneManager.GetActiveScene();
            if (activeScene.path != ScenePath)
            {
                EditorSceneManager.OpenScene(ScenePath);
            }

            // 1. Ensure EventSystem exists with InputSystemUIInputModule
            EnsureEventSystem();

            // 2. Setup Canvas with 1920x1080 reference resolution
            Canvas canvas = EnsureCanvas(out CanvasScaler scaler);

            // 3. Clear existing SlotMachineRoot if re-running
            Transform existingRoot = canvas.transform.Find("SlotMachineRoot");
            if (existingRoot != null)
            {
                Object.DestroyImmediate(existingRoot.gameObject);
            }

            // 4. Create Root UI Container
            GameObject rootObj = new GameObject("SlotMachineRoot", typeof(RectTransform));
            RectTransform rootRect = rootObj.GetComponent<RectTransform>();
            rootRect.SetParent(canvas.transform, false);
            rootRect.anchorMin = Vector2.zero;
            rootRect.anchorMax = Vector2.one;
            rootRect.sizeDelta = Vector2.zero;

            // 5. Background Gradient (full screen 1920x1080)
            CreateBackground(rootRect);

            // 6. Cabinet Container: Exact 1:1 Pixel Scale (816x624) Centered at (0, 0)
            GameObject cabinetContainer = new GameObject("CabinetContainer", typeof(RectTransform));
            RectTransform cabinetRect = cabinetContainer.GetComponent<RectTransform>();
            cabinetRect.SetParent(rootRect, false);
            cabinetRect.anchorMin = new Vector2(0.5f, 0.5f);
            cabinetRect.anchorMax = new Vector2(0.5f, 0.5f);
            cabinetRect.anchoredPosition = Vector2.zero;
            cabinetRect.sizeDelta = new Vector2(816, 624);

            // Layer 1: Reel Backing (slot-machine5.png - cyan/white striped backing)
            CreateCabinetLayer(cabinetRect, "ReelBacking", "Assets/UI/slot-machine5.png");

            // Layer 2: Masked Reel Viewport & 3 Reels
            CreateReelsViewport(cabinetRect, out ReelGroup reelGroup);

            // Layer 3: Cabinet Front Frame with transparent reel cutouts (slot-machine4.png)
            CreateCabinetLayer(cabinetRect, "CabinetFrontFrame", "Assets/UI/slot-machine4.png");

            // Layer 4: Mechanical Lever Base (slot-machine3.png)
            GameObject leverBaseObj = CreateCabinetLayer(cabinetRect, "LeverBase", "Assets/UI/slot-machine3.png");
            leverBaseObj.SetActive(false);

            // Layer 5: Interactive Lever Arm (slot-machine2.png)
            SlotMachineLever lever = CreateInteractiveLever(cabinetRect, leverBaseObj);

            // Layer 6: Bottom Screen HUD Bar
            CreateBottomHUD(rootRect, out Button spinBtn, out Button betUpBtn, out Button betDownBtn, out Button maxBetBtn,
                            out TMP_Text balanceTxt, out TMP_Text betTxt, out TMP_Text winTxt, out TMP_Text statusTxt);

            // 7. Attach and wire Controller & View
            PayoutTable payoutTable = AssetDatabase.LoadAssetAtPath<PayoutTable>(PayoutTablePath);

            var controller = rootObj.AddComponent<SlotMachineController>();
            var view = rootObj.AddComponent<SlotMachineView>();

            // Wire Controller fields via SerializedObject
            SerializedObject ctrlSO = new SerializedObject(controller);
            ctrlSO.FindProperty("_reelGroup").objectReferenceValue = reelGroup;
            ctrlSO.FindProperty("_payoutTable").objectReferenceValue = payoutTable;
            ctrlSO.FindProperty("_startingBalance").intValue = 1000;
            ctrlSO.FindProperty("_defaultBet").intValue = 10;
            ctrlSO.FindProperty("_minBet").intValue = 5;
            ctrlSO.FindProperty("_maxBet").intValue = 100;
            ctrlSO.FindProperty("_betStep").intValue = 5;
            ctrlSO.FindProperty("_winProbability").floatValue = 0.5f;
            ctrlSO.FindProperty("_winCelebrationDuration").floatValue = 1.5f;
            ctrlSO.ApplyModifiedProperties();

            // Wire View fields via SerializedObject
            SerializedObject viewSO = new SerializedObject(view);
            viewSO.FindProperty("_controller").objectReferenceValue = controller;
            viewSO.FindProperty("_spinButton").objectReferenceValue = spinBtn;
            viewSO.FindProperty("_betUpButton").objectReferenceValue = betUpBtn;
            viewSO.FindProperty("_betDownButton").objectReferenceValue = betDownBtn;
            viewSO.FindProperty("_maxBetButton").objectReferenceValue = maxBetBtn;
            viewSO.FindProperty("_balanceText").objectReferenceValue = balanceTxt;
            viewSO.FindProperty("_betText").objectReferenceValue = betTxt;
            viewSO.FindProperty("_lastWinText").objectReferenceValue = winTxt;
            viewSO.FindProperty("_statusText").objectReferenceValue = statusTxt;
            viewSO.FindProperty("_leverArmObject").objectReferenceValue = lever != null ? lever.gameObject : null;
            viewSO.FindProperty("_leverBaseObject").objectReferenceValue = leverBaseObj;
            viewSO.ApplyModifiedProperties();

            // Wire Lever Controller
            if (lever != null)
            {
                SerializedObject leverSO = new SerializedObject(lever);
                leverSO.FindProperty("_controller").objectReferenceValue = controller;
                leverSO.ApplyModifiedProperties();
            }

            // Wire ReelGroup PayoutTable
            if (reelGroup != null && payoutTable != null)
            {
                SerializedObject rgSO = new SerializedObject(reelGroup);
                rgSO.FindProperty("_payoutTable").objectReferenceValue = payoutTable;
                rgSO.ApplyModifiedProperties();
            }

            // Phase 5: Audio Manager & Win Celebration Modal
            EnsureAudioManager(canvas.transform);
            CreateWinCelebrationModal(canvas.transform, controller);

            EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
            EditorSceneManager.SaveOpenScenes();

            Debug.Log("[SlotMachine] Pixel-perfect 816x624 Classic Cabinet and Scene UI successfully assembled!");
        }

        private static void EnsureEventSystem()
        {
            EventSystem es = Object.FindFirstObjectByType<EventSystem>();
            if (es == null)
            {
#if ENABLE_INPUT_SYSTEM
                GameObject esObj = new GameObject("EventSystem", typeof(EventSystem), typeof(UnityEngine.InputSystem.UI.InputSystemUIInputModule));
#else
                GameObject esObj = new GameObject("EventSystem", typeof(EventSystem), typeof(StandaloneInputModule));
#endif
                Undo.RegisterCreatedObjectUndo(esObj, "Create EventSystem");
            }
            else
            {
#if ENABLE_INPUT_SYSTEM
                var standalone = es.GetComponent<StandaloneInputModule>();
                if (standalone != null)
                {
                    Object.DestroyImmediate(standalone);
                    if (es.GetComponent<UnityEngine.InputSystem.UI.InputSystemUIInputModule>() == null)
                    {
                        es.gameObject.AddComponent<UnityEngine.InputSystem.UI.InputSystemUIInputModule>();
                    }
                }
#endif
            }
        }

        private static Canvas EnsureCanvas(out CanvasScaler scaler)
        {
            Canvas canvas = Object.FindFirstObjectByType<Canvas>();
            if (canvas == null)
            {
                GameObject canvasObj = new GameObject("Canvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
                canvas = canvasObj.GetComponent<Canvas>();
                canvas.renderMode = RenderMode.ScreenSpaceOverlay;
                Undo.RegisterCreatedObjectUndo(canvasObj, "Create Canvas");
            }

            scaler = canvas.GetComponent<CanvasScaler>();
            if (scaler == null) scaler = canvas.gameObject.AddComponent<CanvasScaler>();

            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 0.5f;

            return canvas;
        }

        private static void CreateBackground(RectTransform parent)
        {
            GameObject bgObj = new GameObject("Background", typeof(RectTransform), typeof(Image));
            RectTransform rt = bgObj.GetComponent<RectTransform>();
            rt.SetParent(parent, false);
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.sizeDelta = Vector2.zero;

            Image img = bgObj.GetComponent<Image>();
            img.sprite = LoadSpriteAtPath("Assets/UI/bg_gradient.png");
            img.color = Color.white;
            img.raycastTarget = false;
        }

        private static GameObject CreateCabinetLayer(RectTransform parent, string name, string spritePath)
        {
            GameObject layerObj = new GameObject(name, typeof(RectTransform), typeof(Image));
            RectTransform rt = layerObj.GetComponent<RectTransform>();
            rt.SetParent(parent, false);
            rt.anchorMin = new Vector2(0.5f, 0.5f);
            rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = Vector2.zero;
            rt.sizeDelta = new Vector2(816, 624);

            Image img = layerObj.GetComponent<Image>();
            img.sprite = LoadSpriteAtPath(spritePath);
            img.raycastTarget = false;
            return layerObj;
        }

        private static void CreateReelsViewport(RectTransform cabinetParent, out ReelGroup reelGroup)
        {
            // Viewport bounds: exact transparent window in slot-machine4.png
            // x: 229 to 596 (width = 368, center offset x = +4.5)
            // y: 246 to 455 (height = 210, center offset y = -38.5)
            GameObject viewportObj = new GameObject("ReelsViewport", typeof(RectTransform), typeof(RectMask2D));
            RectTransform viewportRect = viewportObj.GetComponent<RectTransform>();
            viewportRect.SetParent(cabinetParent, false);
            viewportRect.anchorMin = new Vector2(0.5f, 0.5f);
            viewportRect.anchorMax = new Vector2(0.5f, 0.5f);
            viewportRect.anchoredPosition = new Vector2(4.5f, -38.5f);
            viewportRect.sizeDelta = new Vector2(368, 210);

            reelGroup = viewportObj.AddComponent<ReelGroup>();

            // 3 Reel columns inside the aperture (Left: -130, Middle: 0, Right: +130)
            float[] reelXOffsets = new float[] { -130f, 0f, 130f };
            SlotReel[] createdReels = new SlotReel[3];
            GameObject symbolPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(SymbolPrefabPath);
            PayoutTable payoutTable = AssetDatabase.LoadAssetAtPath<PayoutTable>(PayoutTablePath);

            var symbolAssetsList = new List<SymbolData>(4);
            for (int i = 1; i <= 4; i++)
            {
                var s = AssetDatabase.LoadAssetAtPath<SymbolData>($"Assets/Data/SymbolData_{i}.asset");
                if (s != null) symbolAssetsList.Add(s);
            }
            var symbolAssets = symbolAssetsList.ToArray();

            for (int r = 0; r < 3; r++)
            {
                GameObject reelObj = new GameObject($"Reel_{r}", typeof(RectTransform));
                RectTransform reelRect = reelObj.GetComponent<RectTransform>();
                reelRect.SetParent(viewportRect, false);
                reelRect.anchorMin = new Vector2(0.5f, 0.5f);
                reelRect.anchorMax = new Vector2(0.5f, 0.5f);
                reelRect.anchoredPosition = new Vector2(reelXOffsets[r], 0);
                reelRect.sizeDelta = new Vector2(108, 210);

                SlotReel reel = reelObj.AddComponent<SlotReel>();

                // 5 symbol items per reel: spacing 70px perfectly fits 3 visible rows in 210px window!
                SlotSymbolItem[] items = new SlotSymbolItem[5];

                for (int i = 0; i < 5; i++)
                {
                    GameObject itemObj;
                    if (symbolPrefab != null)
                    {
                        itemObj = PrefabUtility.InstantiatePrefab(symbolPrefab) as GameObject;
                        itemObj.transform.SetParent(reelObj.transform, false);
                    }
                    else
                    {
                        itemObj = new GameObject($"SymbolItem_{i}", typeof(RectTransform), typeof(Image), typeof(SlotSymbolItem));
                        itemObj.transform.SetParent(reelObj.transform, false);
                    }

                    itemObj.name = $"Item_{i}";
                    RectTransform itemRect = itemObj.GetComponent<RectTransform>();
                    itemRect.sizeDelta = new Vector2(74, 66);

                    SlotSymbolItem symbolItem = itemObj.GetComponent<SlotSymbolItem>();
                    items[i] = symbolItem;
                }

                // Configure reel parameters
                SerializedObject reelSO = new SerializedObject(reel);
                reelSO.FindProperty("_reelGroup").objectReferenceValue = reelGroup;
                reelSO.FindProperty("_cruisingSpeed").floatValue = 1400f;
                reelSO.ApplyModifiedProperties();

                // Explicitly initialize item array in edit mode
                reel.InitializeItemArray();

                // User Decision: Initialize with matching 3x Sevens (Symbol1) on center payline!
                if (symbolAssets.Length > 0)
                {
                    reel.SnapToSymbol(SymbolType.Symbol1, symbolAssets);

                    // Setup top and bottom row neighbors to give authentic arcade look
                    // Item index 1 = bottom row (-70), Item index 2 = center (0, Seven), Item index 3 = top row (+70)
                    if (r == 0)
                    {
                        if (symbolAssets.Length > 2) items[1].SetSymbol(symbolAssets[2]);
                        if (symbolAssets.Length > 3) items[3].SetSymbol(symbolAssets[3]);
                    }
                    else if (r == 1)
                    {
                        if (symbolAssets.Length > 2) items[1].SetSymbol(symbolAssets[2]);
                        if (symbolAssets.Length > 3) items[3].SetSymbol(symbolAssets[3]);
                    }
                    else
                    {
                        if (symbolAssets.Length > 1) items[1].SetSymbol(symbolAssets[1]);
                        if (symbolAssets.Length > 2) items[3].SetSymbol(symbolAssets[2]);
                    }
                }

                createdReels[r] = reel;
            }

            // Set reels, shared parameters, and payout table in ReelGroup
            SerializedObject groupSO = new SerializedObject(reelGroup);
            groupSO.FindProperty("_itemSpacing").floatValue = 70f;
            groupSO.FindProperty("_spinUpDuration").floatValue = 0.2f;
            var stopModeProp = groupSO.FindProperty("_stopMode");
            if (stopModeProp != null) stopModeProp.enumValueIndex = 0; // MechanicalPawl
            groupSO.FindProperty("_payoutTable").objectReferenceValue = payoutTable;
            if (symbolPrefab != null)
            {
                groupSO.FindProperty("_symbolItemPrefab").objectReferenceValue = symbolPrefab.GetComponent<SlotSymbolItem>();
            }

            SerializedProperty reelsProp = groupSO.FindProperty("_reels");
            reelsProp.arraySize = 3;
            for (int r = 0; r < 3; r++)
            {
                reelsProp.GetArrayElementAtIndex(r).objectReferenceValue = createdReels[r];
            }
            groupSO.ApplyModifiedProperties();
        }

        private static SlotMachineLever CreateInteractiveLever(RectTransform cabinetParent, GameObject leverBaseObj)
        {
            GameObject leverObj = new GameObject("LeverArm", typeof(RectTransform), typeof(Image), typeof(SlotMachineLever));
            RectTransform rt = leverObj.GetComponent<RectTransform>();
            rt.SetParent(cabinetParent, false);

            // Pivot at bottom connection point to the lever base (x=719, y=60 in 816x624 coordinates)
            rt.anchorMin = new Vector2(0.5f, 0.5f);
            rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = new Vector2(0.881f, 0.096f);
            rt.anchoredPosition = new Vector2((0.881f - 0.5f) * 816f, (0.096f - 0.5f) * 624f);
            rt.sizeDelta = new Vector2(816, 624);

            Image img = leverObj.GetComponent<Image>();
            img.sprite = LoadSpriteAtPath("Assets/UI/slot-machine2.png");
            img.raycastTarget = true; // Enables click and drag interactions

            var lever = leverObj.GetComponent<SlotMachineLever>();
            SerializedObject leverSO = new SerializedObject(lever);
            leverSO.FindProperty("_leverArmObject").objectReferenceValue = leverObj;
            leverSO.FindProperty("_leverBaseObject").objectReferenceValue = leverBaseObj;
            leverSO.ApplyModifiedProperties();

            return lever;
        }

        private static void CreateBottomHUD(RectTransform rootParent,
            out Button spinBtn, out Button betUpBtn, out Button betDownBtn, out Button maxBetBtn,
            out TMP_Text balanceTxt, out TMP_Text betTxt, out TMP_Text winTxt, out TMP_Text statusTxt)
        {
            // Bottom Bar Container anchored to the bottom edge of the screen
            GameObject hudObj = new GameObject("BottomHUD_Bar", typeof(RectTransform), typeof(Image));
            RectTransform hudRect = hudObj.GetComponent<RectTransform>();
            hudRect.SetParent(rootParent, false);
            hudRect.anchorMin = new Vector2(0.5f, 0f);
            hudRect.anchorMax = new Vector2(0.5f, 0f);
            hudRect.pivot = new Vector2(0.5f, 0f);
            hudRect.anchoredPosition = new Vector2(0, 15f);
            hudRect.sizeDelta = new Vector2(1000, 140);

            // Sleek dark metallic backing
            Image hudBg = hudObj.GetComponent<Image>();
            hudBg.color = new Color(0.08f, 0.09f, 0.16f, 0.95f); // Rich dark blue-black

            // Top Status Banner on the HUD Bar
            statusTxt = CreateStatusBanner(hudRect, new Vector2(0, 52));

            // Readouts: Balance (left), Bet (center-left), Win (center-right)
            balanceTxt = CreateDisplayField(hudRect, new Vector2(-360, -12), "CREDITS", "1000");
            betTxt = CreateDisplayField(hudRect, new Vector2(-120, -12), "BET", "10");
            winTxt = CreateDisplayField(hudRect, new Vector2(120, -12), "LAST WIN", "0");

            // Bet Controls (+ / - / Max)
            betDownBtn = CreateButton(hudRect, new Vector2(-200, -12), new Vector2(46, 46),
                "Assets/UI/slot_machine_buttons-04.png", "-");

            betUpBtn = CreateButton(hudRect, new Vector2(-40, -12), new Vector2(46, 46),
                "Assets/UI/slot_machine_buttons-03.png", "+");

            maxBetBtn = CreateButton(hudRect, new Vector2(20, -12), new Vector2(62, 46),
                "Assets/UI/slot_machine_buttons-02.png", "MAX");

            // Big Round Spin Button on the far right
            spinBtn = CreateButton(hudRect, new Vector2(380, -10), new Vector2(100, 100),
                "Assets/UI/slot_machine_buttons-02.png", "SPIN");
        }

        private static TMP_Text CreateDisplayField(RectTransform parent, Vector2 anchoredPos, string label, string defaultValue)
        {
            GameObject container = new GameObject($"Display_{label}", typeof(RectTransform));
            RectTransform rt = container.GetComponent<RectTransform>();
            rt.SetParent(parent, false);
            rt.anchoredPosition = anchoredPos;
            rt.sizeDelta = new Vector2(140, 56);

            // Label
            GameObject lblObj = new GameObject("Label", typeof(RectTransform), typeof(TextMeshProUGUI));
            RectTransform lblRt = lblObj.GetComponent<RectTransform>();
            lblRt.SetParent(rt, false);
            lblRt.anchoredPosition = new Vector2(0, 16);
            lblRt.sizeDelta = new Vector2(140, 20);

            TextMeshProUGUI lblTmp = lblObj.GetComponent<TextMeshProUGUI>();
            lblTmp.text = label;
            lblTmp.fontSize = 13;
            lblTmp.fontStyle = FontStyles.Bold;
            lblTmp.alignment = TextAlignmentOptions.Center;
            lblTmp.color = new Color(0.95f, 0.82f, 0.25f, 1f); // Arcade Gold

            // Value
            GameObject valObj = new GameObject("Value", typeof(RectTransform), typeof(TextMeshProUGUI));
            RectTransform valRt = valObj.GetComponent<RectTransform>();
            valRt.SetParent(rt, false);
            valRt.anchoredPosition = new Vector2(0, -8);
            valRt.sizeDelta = new Vector2(140, 28);

            TextMeshProUGUI valTmp = valObj.GetComponent<TextMeshProUGUI>();
            valTmp.text = defaultValue;
            valTmp.fontSize = 22;
            valTmp.fontStyle = FontStyles.Bold;
            valTmp.alignment = TextAlignmentOptions.Center;
            valTmp.color = Color.white;

            return valTmp;
        }

        private static TMP_Text CreateStatusBanner(RectTransform parent, Vector2 anchoredPos)
        {
            GameObject bannerObj = new GameObject("StatusBanner", typeof(RectTransform), typeof(TextMeshProUGUI));
            RectTransform rt = bannerObj.GetComponent<RectTransform>();
            rt.SetParent(parent, false);
            rt.anchoredPosition = anchoredPos;
            rt.sizeDelta = new Vector2(700, 28);

            TextMeshProUGUI tmp = bannerObj.GetComponent<TextMeshProUGUI>();
            tmp.text = "PRESS SPIN OR PULL LEVER TO PLAY";
            tmp.fontSize = 17;
            tmp.fontStyle = FontStyles.Bold;
            tmp.alignment = TextAlignmentOptions.Center;
            tmp.color = new Color(1f, 0.95f, 0.45f, 1f);

            return tmp;
        }

        private static Button CreateButton(RectTransform parent, Vector2 anchoredPos, Vector2 size, string spritePath, string btnText)
        {
            GameObject btnObj = new GameObject($"Btn_{btnText}", typeof(RectTransform), typeof(Image), typeof(Button));
            RectTransform rt = btnObj.GetComponent<RectTransform>();
            rt.SetParent(parent, false);
            rt.anchoredPosition = anchoredPos;
            rt.sizeDelta = size;

            Image img = btnObj.GetComponent<Image>();
            Sprite sprite = LoadSpriteAtPath(spritePath);
            img.sprite = sprite;
            img.preserveAspect = true;

            Button btn = btnObj.GetComponent<Button>();
            btn.targetGraphic = img;

            if (!string.IsNullOrEmpty(btnText))
            {
                GameObject txtObj = new GameObject("Text", typeof(RectTransform), typeof(TextMeshProUGUI));
                RectTransform txtRt = txtObj.GetComponent<RectTransform>();
                txtRt.SetParent(rt, false);
                txtRt.anchorMin = Vector2.zero;
                txtRt.anchorMax = Vector2.one;
                txtRt.sizeDelta = Vector2.zero;

                TextMeshProUGUI tmp = txtObj.GetComponent<TextMeshProUGUI>();
                tmp.text = btnText;
                tmp.fontSize = size.x > 80 ? 22 : 16;
                tmp.fontStyle = FontStyles.Bold;
                tmp.alignment = TextAlignmentOptions.Center;
                tmp.color = Color.white;
            }

            return btn;
        }

        private static Sprite LoadSpriteAtPath(string path)
        {
            var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(path);
            if (sprite != null) return sprite;

            var all = AssetDatabase.LoadAllAssetsAtPath(path);
            for (int i = 0; i < all.Length; i++)
            {
                if (all[i] is Sprite s) return s;
            }

            return null;
        }

        private static Sprite LoadSpriteSlice(string path, string sliceName)
        {
            var all = AssetDatabase.LoadAllAssetsAtPath(path);
            for (int i = 0; i < all.Length; i++)
            {
                if (all[i] is Sprite s && s.name == sliceName) return s;
            }
            return LoadSpriteAtPath(path);
        }

        private static void EnsureAudioManager(Transform parent)
        {
            var existing = Object.FindFirstObjectByType<AudioManager>();
            GameObject audioObj = existing != null ? existing.gameObject : null;
            if (audioObj == null)
            {
                audioObj = new GameObject("AudioManager", typeof(AudioManager));
                audioObj.transform.SetParent(parent, false);
            }

            var audioManager = audioObj.GetComponent<AudioManager>();
            AudioClip scrollClip = AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Sounds/scrolling.wav");
            if (scrollClip != null && audioManager != null)
            {
                SerializedObject audioSO = new SerializedObject(audioManager);
                var prop = audioSO.FindProperty("_reelSpinLoopClip");
                if (prop != null && prop.objectReferenceValue == null)
                {
                    prop.objectReferenceValue = scrollClip;
                    audioSO.ApplyModifiedProperties();
                }
            }
        }

        private static void CreateWinCelebrationModal(Transform canvasTransform, SlotMachineController controller)
        {
            var existing = Object.FindFirstObjectByType<WinCelebrationView>();
            if (existing != null) return;

            GameObject modalObj = new GameObject("WinCelebrationModal", typeof(RectTransform), typeof(CanvasGroup), typeof(WinCelebrationView));
            RectTransform modalRt = modalObj.GetComponent<RectTransform>();
            modalRt.SetParent(canvasTransform, false);
            modalRt.anchorMin = Vector2.zero;
            modalRt.anchorMax = Vector2.one;
            modalRt.sizeDelta = Vector2.zero;

            // Semi-transparent backdrop blocker
            Image backdrop = modalObj.AddComponent<Image>();
            backdrop.color = new Color(0f, 0f, 0f, 0.65f);

            // Dialog Frame
            GameObject popupObj = new GameObject("PopupDialog", typeof(RectTransform), typeof(Image));
            RectTransform popupRt = popupObj.GetComponent<RectTransform>();
            popupRt.SetParent(modalRt, false);
            popupRt.anchorMin = new Vector2(0.5f, 0.5f);
            popupRt.anchorMax = new Vector2(0.5f, 0.5f);
            popupRt.anchoredPosition = Vector2.zero;
            popupRt.sizeDelta = new Vector2(500, 360);

            Image popupImg = popupObj.GetComponent<Image>();
            popupImg.sprite = LoadSpriteSlice("Assets/UI/popup.png", "popup_0");
            popupImg.preserveAspect = true;

            // Title Text
            GameObject titleObj = new GameObject("TitleText", typeof(RectTransform), typeof(TextMeshProUGUI));
            RectTransform titleRt = titleObj.GetComponent<RectTransform>();
            titleRt.SetParent(popupRt, false);
            titleRt.anchorMin = new Vector2(0.5f, 0.5f);
            titleRt.anchorMax = new Vector2(0.5f, 0.5f);
            titleRt.anchoredPosition = new Vector2(0, 80);
            titleRt.sizeDelta = new Vector2(400, 50);

            TextMeshProUGUI titleTmp = titleObj.GetComponent<TextMeshProUGUI>();
            titleTmp.text = "BIG WIN!";
            titleTmp.fontSize = 38;
            titleTmp.fontStyle = FontStyles.Bold;
            titleTmp.alignment = TextAlignmentOptions.Center;
            titleTmp.color = new Color(1f, 0.85f, 0.2f, 1f);

            // Payout Text
            GameObject payoutObj = new GameObject("PayoutText", typeof(RectTransform), typeof(TextMeshProUGUI));
            RectTransform payoutRt = payoutObj.GetComponent<RectTransform>();
            payoutRt.SetParent(popupRt, false);
            payoutRt.anchorMin = new Vector2(0.5f, 0.5f);
            payoutRt.anchorMax = new Vector2(0.5f, 0.5f);
            payoutRt.anchoredPosition = new Vector2(0, 10);
            payoutRt.sizeDelta = new Vector2(400, 60);

            TextMeshProUGUI payoutTmp = payoutObj.GetComponent<TextMeshProUGUI>();
            payoutTmp.text = "+500";
            payoutTmp.fontSize = 46;
            payoutTmp.fontStyle = FontStyles.Bold;
            payoutTmp.alignment = TextAlignmentOptions.Center;
            payoutTmp.color = Color.white;

            // Claim Button
            Button claimBtn = CreateButton(popupRt, new Vector2(0, -90), new Vector2(160, 56), "Assets/UI/Yes_No_Btn.png", "CLAIM");

            // Wire WinCelebrationView
            WinCelebrationView celebView = modalObj.GetComponent<WinCelebrationView>();
            SerializedObject celebSO = new SerializedObject(celebView);
            celebSO.FindProperty("_controller").objectReferenceValue = controller;
            celebSO.FindProperty("_popupRoot").objectReferenceValue = modalObj;
            celebSO.FindProperty("_popupTransform").objectReferenceValue = popupRt;
            celebSO.FindProperty("_canvasGroup").objectReferenceValue = modalObj.GetComponent<CanvasGroup>();
            celebSO.FindProperty("_titleText").objectReferenceValue = titleTmp;
            celebSO.FindProperty("_payoutText").objectReferenceValue = payoutTmp;
            celebSO.FindProperty("_claimButton").objectReferenceValue = claimBtn;
            celebSO.ApplyModifiedProperties();

            // Modal remains active; CanvasGroup alpha and interactable handle visibility
            modalObj.SetActive(true);
        }
    }
}
#endif
