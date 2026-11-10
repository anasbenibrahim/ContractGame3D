using System.IO;
using Contract.Data;
using Contract.Interaction;
using Contract.UI;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using static Contract.Editor.World.UIKit;

namespace Contract.Editor.World
{
    /// <summary>
    /// Stage 6: progression data (Korean regions, bonuses, unlock chain, non-lethal wording), the
    /// MissionUI prefab (HUD money + contract tracker + toasts, contract board, result / victory
    /// screens) and GameWorld wiring. Run after the district, population and map builders.
    /// Headless: -executeMethod Contract.Editor.World.MissionUIBuilder.Build
    /// </summary>
    public static class MissionUIBuilder
    {
        private const string ScenePath = "Assets/Scenes/GameWorld.unity";
        private const string PrefabPath = "Assets/Prefabs/UI/MissionUI.prefab";
        private const string WhiteSpritePath = "Assets/UI/Sprites/White.png";

        [MenuItem("CONTRACT/World/Build Missions UI and Progression Data")]
        public static void Build()
        {
            UpdateProgressionData();
            Sprite white = WhiteSprite();
            GameObject prefab = BuildPrefab(white);
            GameObject board = BuildBoardPrefab();

            Scene scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            WireScene(prefab, board);
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            GameDatabaseBuilder.Build();
            AssetDatabase.SaveAssets();
            Debug.Log("[MissionUIBuilder] Done.");
        }

        // =====================================================================================
        // Data
        // =====================================================================================

        private static T Load<T>(string path) where T : Object => AssetDatabase.LoadAssetAtPath<T>(path);

        private static void Region(string id, string name, string description, int bonus, bool built)
        {
            RegionData region = Load<RegionData>($"Assets/ScriptableObjects/Regions/{id}.asset");
            region.regionName = name;
            region.description = description;
            region.completionBonus = bonus;
            region.districtBuilt = built;
            EditorUtility.SetDirty(region);
        }

        private static void Mission(string id, string name = null, string description = null, params (int index, string text)[] objectives)
        {
            MissionData mission = Load<MissionData>($"Assets/ScriptableObjects/Missions/{id}.asset");
            if (name != null) mission.missionName = name;
            if (description != null) mission.description = description;
            foreach ((int index, string text) in objectives) mission.objectives[index].description = text;
            EditorUtility.SetDirty(mission);
        }

        private static void UpdateProgressionData()
        {
            // Gangnam -> {Myeongdong, Bukchon} -> Haeundae -> Busan Harbor (prerequisites already in the assets).
            Region("region_downtown", "Gangnam 강남", "Seoul's glass-and-neon business district: 강남대로 towers, 강남역 and the food alley behind it.", 100000, true);
            Region("region_industrial", "Myeongdong 명동", "Seoul's shopping streets: cosmetics flagships, street-food stalls and crowded pedestrian lanes.", 150000, false);
            Region("region_residential", "Bukchon 북촌", "Hanok rooftops, stone lanes and quiet courtyards between Gyeongbokgung and Changdeokgung.", 150000, false);
            Region("region_luxury", "Haeundae 해운대", "Busan's beachfront: luxury hotels, the marina and glass towers over the sand.", 250000, false);
            Region("region_harbor", "Busan Harbor 부산항", "Container cranes, warehouses and ships along Busan's commercial port.", 400000, false);

            // Non-lethal, investigation-led wording (the world is fictional; no one is harmed).
            Mission("mission_harbor_03", "Loose Ends", "Recover the last pieces of evidence and hand the smuggling network to the press.",
                (0, "Search the warehouse district"));
            Mission("mission_harbor_01", null, "Document the final contraband shipment before it leaves the harbor.");
            Mission("mission_harbor_02", null, "Expose the mastermind behind the conspiracy. Final extraction.", (1, "Record the Director's confession"));
            Mission("mission_luxury_01", null, "Infiltrate an exclusive gala and place a listening device in a fraud ring leader's penthouse.",
                (1, "Locate the ring leader"));
            Mission("mission_luxury_02", "Star of Seoul", "Recover the stolen 'Star of Seoul' diamond from a private vault and return it to the museum.");
            Mission("mission_industrial_01", "Stockroom Job", "Recover a stolen cosmetics formula from a guarded Myeongdong stockroom.");
            Mission("mission_industrial_02", "Counterfeit Trail", "Track a counterfeit-goods smuggler through Myeongdong's market lanes.");

            ItemData pistol = Load<ItemData>("Assets/ScriptableObjects/Items/silenced_pistol.asset");
            if (pistol != null)
            {
                SerializedObject so = new SerializedObject(pistol);
                SetString(so, "displayName", "Signal Jammer");
                SetString(so, "description", "Briefly disables security cameras. Non-violent.");
                so.ApplyModifiedPropertiesWithoutUndo();
            }

            UpgradeData takedown = Load<UpgradeData>("Assets/ScriptableObjects/Upgrades/upgrade_silent_takedown_1.asset");
            if (takedown != null)
            {
                takedown.upgradeName = "Quiet Steps I";
                takedown.description = "Your footsteps make less noise.";
                EditorUtility.SetDirty(takedown);
            }
        }

        private static void SetString(SerializedObject so, string field, string value)
        {
            SerializedProperty p = so.FindProperty(field) ?? so.FindProperty($"<{field}>k__BackingField");
            if (p != null) p.stringValue = value;
        }

        // =====================================================================================
        // Prefab
        // =====================================================================================

        private static Sprite WhiteSprite()
        {
            if (!File.Exists(WhiteSpritePath))
            {
                Directory.CreateDirectory(Path.GetDirectoryName(WhiteSpritePath));
                Texture2D tex = new Texture2D(4, 4);
                tex.SetPixels32(new Color32[16].Populate(new Color32(255, 255, 255, 255)));
                File.WriteAllBytes(WhiteSpritePath, tex.EncodeToPNG());
                AssetDatabase.ImportAsset(WhiteSpritePath);
                TextureImporter importer = (TextureImporter)AssetImporter.GetAtPath(WhiteSpritePath);
                importer.textureType = TextureImporterType.Sprite;
                importer.SaveAndReimport();
            }

            return Load<Sprite>(WhiteSpritePath);
        }

        private static Image Bar(Transform parent, string name, Vector2 anchor, Vector2 pivot, Vector2 position, Vector2 size, Sprite white)
        {
            RectTransform bg = Node(parent, name, anchor, anchor, pivot, position, size);
            Img(bg, PanelLight);
            RectTransform fillRect = Stretch(bg, "Fill");
            Image fill = Img(fillRect, Accent);
            fill.sprite = white;
            fill.type = Image.Type.Filled;
            fill.fillMethod = Image.FillMethod.Horizontal;
            fill.fillAmount = 0f;
            return fill;
        }

        private static TextMeshProUGUI Wrapped(TextMeshProUGUI t)
        {
            t.textWrappingMode = TextWrappingModes.Normal;
            t.alignment = TextAlignmentOptions.TopLeft;
            return t;
        }

        // ---- Terminal: upgrades page ----------------------------------------------------------

        private static GameObject BuildUpgradesPage(RectTransform board, Vector2 topLeft)
        {
            RectTransform page = Stretch(board, "UpgradesPage");
            RectTransform categories = Node(page, "Categories", topLeft, topLeft, topLeft, new Vector2(40f, -104f), new Vector2(1300f, 44f));
            HorizontalLayoutGroup layout = categories.gameObject.AddComponent<HorizontalLayoutGroup>();
            layout.spacing = 8f;
            layout.childControlWidth = layout.childControlHeight = false;
            layout.childForceExpandWidth = false;
            TextMeshProUGUI balance = Text(page, "Balance", "", 22f, TextColor, TextAlignmentOptions.Right, Vector2.one, Vector2.one, Vector2.one, new Vector2(-40f, -108f), new Vector2(500f, 36f), true);

            RectTransform cards = Node(page, "Cards", topLeft, topLeft, topLeft, new Vector2(40f, -170f), new Vector2(560f, 760f));
            VerticalLayoutGroup cardLayout = cards.gameObject.AddComponent<VerticalLayoutGroup>();
            cardLayout.spacing = 8f;
            cardLayout.childControlWidth = true;
            cardLayout.childControlHeight = false;
            cardLayout.childForceExpandHeight = false;

            RectTransform detail = Node(page, "Detail", topLeft, topLeft, topLeft, new Vector2(640f, -170f), new Vector2(1240f, 560f));
            Img(detail, Panel);
            Node(detail, "Accent", topLeft, Vector2.one, new Vector2(0.5f, 1f), Vector2.zero, new Vector2(0f, 4f)).gameObject.AddComponent<Image>().color = Accent;
            TextMeshProUGUI title = Wrapped(Text(detail, "Title", "", 34f, TextColor, TextAlignmentOptions.TopLeft, topLeft, topLeft, topLeft, new Vector2(30f, -28f), new Vector2(1180f, 50f), true));
            TextMeshProUGUI body = Wrapped(Text(detail, "Body", "", 20f, TextColor, TextAlignmentOptions.TopLeft, topLeft, topLeft, topLeft, new Vector2(30f, -96f), new Vector2(1100f, 90f)));
            Text(detail, "EffectTitle", "효과  <size=70%><color=#9AA3AE>EFFECT</color></size>", 20f, Accent, TextAlignmentOptions.Left, topLeft, topLeft, topLeft, new Vector2(30f, -200f), new Vector2(600f, 30f), true);
            TextMeshProUGUI effect = Wrapped(Text(detail, "Effect", "", 21f, TextColor, TextAlignmentOptions.TopLeft, topLeft, topLeft, topLeft, new Vector2(30f, -240f), new Vector2(1100f, 140f)));
            effect.lineSpacing = 14f;
            TextMeshProUGUI status = Text(detail, "Status", "", 18f, new Color(1f, 0.69f, 0.13f), TextAlignmentOptions.Left, Vector2.zero, Vector2.zero, Vector2.zero, new Vector2(30f, 44f), new Vector2(700f, 28f));
            Button buy = ButtonAt(detail, "Buy", "", new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(-30f, 30f), new Vector2(380f, 58f), true);
            Button equip = ButtonAt(detail, "Equip", "", new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(-30f, 30f), new Vector2(380f, 58f));
            Text(page, "Note", "업그레이드는 모든 요원에게 적용됩니다 · Upgrades apply to every operative.", 16f, Muted, TextAlignmentOptions.Left,
                topLeft, topLeft, topLeft, new Vector2(640f, -750f), new Vector2(1240f, 26f));

            UpgradeShopView view = page.gameObject.AddComponent<UpgradeShopView>();
            SetRefs(view, ("categoryTabs", categories), ("cardList", cards), ("balanceText", balance), ("detailTitle", title), ("detailBody", body),
                ("detailEffect", effect), ("statusText", status), ("buyButton", buy), ("buyLabel", buy.GetComponentInChildren<TextMeshProUGUI>()),
                ("equipButton", equip), ("equipLabel", equip.GetComponentInChildren<TextMeshProUGUI>()));
            page.gameObject.SetActive(false);
            return page.gameObject;
        }

        // ---- Terminal: finance dashboard -------------------------------------------------------

        private static GameObject BuildFinancePage(RectTransform board, Vector2 topLeft)
        {
            RectTransform page = Stretch(board, "FinancePage");
            RectTransform Card(string name, Vector2 position, Vector2 size, string heading)
            {
                RectTransform card = Node(page, name, topLeft, topLeft, topLeft, position, size);
                Img(card, Panel);
                Node(card, "Accent", topLeft, Vector2.one, new Vector2(0.5f, 1f), Vector2.zero, new Vector2(0f, 3f)).gameObject.AddComponent<Image>().color = Accent;
                Text(card, "Heading", heading, 18f, Accent, TextAlignmentOptions.Left, topLeft, topLeft, topLeft, new Vector2(28f, -18f), new Vector2(800f, 28f), true);
                return card;
            }

            RectTransform summary = Card("Summary", new Vector2(40f, -104f), new Vector2(900f, 440f), "잔액  <size=75%><color=#9AA3AE>BALANCE</color></size>");
            TextMeshProUGUI balance = Text(summary, "Balance", "", 64f, TextColor, TextAlignmentOptions.Left, topLeft, topLeft, topLeft, new Vector2(28f, -52f), new Vector2(840f, 80f), true);
            RectTransform bar = Node(summary, "Goal", topLeft, topLeft, topLeft, new Vector2(28f, -142f), new Vector2(844f, 12f));
            Img(bar, PanelLight);
            Image fill = Img(Stretch(bar, "Fill"), Accent);
            fill.sprite = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/UI/Sprites/White.png");
            fill.type = Image.Type.Filled;
            fill.fillMethod = Image.FillMethod.Horizontal;
            TextMeshProUGUI goal = Text(summary, "GoalText", "", 18f, TextColor, TextAlignmentOptions.Left, topLeft, topLeft, topLeft, new Vector2(28f, -164f), new Vector2(844f, 28f));
            TextMeshProUGUI stats = Text(summary, "Stats", "", 19f, TextColor, TextAlignmentOptions.TopLeft, topLeft, topLeft, topLeft, new Vector2(28f, -208f), new Vector2(844f, 220f));
            stats.lineSpacing = 8f;

            RectTransform history = Card("History", new Vector2(980f, -104f), new Vector2(900f, 440f), "잔액 추이  <size=75%><color=#9AA3AE>BALANCE HISTORY</color></size>");
            RectTransform chart = Node(history, "Chart", topLeft, topLeft, topLeft, new Vector2(28f, -64f), new Vector2(844f, 300f));
            Img(chart, new Color(0.05f, 0.055f, 0.07f, 1f));
            TextMeshProUGUI caption = Text(history, "Caption", "", 15f, Muted, TextAlignmentOptions.Left, topLeft, topLeft, topLeft, new Vector2(28f, -380f), new Vector2(844f, 26f));

            RectTransform transactions = Card("Transactions", new Vector2(40f, -564f), new Vector2(900f, 476f), "최근 거래  <size=75%><color=#9AA3AE>RECENT TRANSACTIONS</color></size>");
            RectTransform ledger = Node(transactions, "Ledger", topLeft, topLeft, topLeft, new Vector2(28f, -60f), new Vector2(844f, 400f));
            VerticalLayoutGroup ledgerLayout = ledger.gameObject.AddComponent<VerticalLayoutGroup>();
            ledgerLayout.childControlWidth = true;
            ledgerLayout.childControlHeight = false;
            ledgerLayout.childForceExpandHeight = false;

            RectTransform districts = Card("Districts", new Vector2(980f, -564f), new Vector2(900f, 476f), "지역  <size=75%><color=#9AA3AE>DISTRICTS · COMPLETION BONUS</color></size>");
            TextMeshProUGUI regions = Text(districts, "Regions", "", 20f, TextColor, TextAlignmentOptions.TopLeft, topLeft, topLeft, topLeft, new Vector2(28f, -64f), new Vector2(844f, 300f));
            regions.lineSpacing = 22f;

            FinanceView view = page.gameObject.AddComponent<FinanceView>();
            SetRefs(view, ("balanceText", balance), ("goalFill", fill), ("goalText", goal), ("statsText", stats), ("chart", chart),
                ("chartCaption", caption), ("ledgerList", ledger), ("regionsText", regions));
            page.gameObject.SetActive(false);
            return page.gameObject;
        }

        public const string BoardPrefabPath = "Assets/Prefabs/UI/ContractBoardUI.prefab";

        /// <summary>Contract board as its own prefab: used in GameWorld (safehouse terminal) and as the Mission Selection screen.</summary>
        private static GameObject BuildBoardPrefab()
        {
            GameObject root = new GameObject("ContractBoardUI", typeof(RectTransform));
            Canvas canvas = root.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 16;
            CanvasScaler scaler = root.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.Expand; // never smaller than 1920x1080: no overflow on 20:9 phones or 16:10 screens
            root.AddComponent<GraphicRaycaster>();
            root.AddComponent<Contract.UI.SafeAreaFitter>();
            Vector2 topLeft = new Vector2(0f, 1f), top = new Vector2(0.5f, 1f);

            // ---- Contract board ------------------------------------------------------------
            RectTransform board = Stretch(root.transform, "ContractBoard");
            Animate(board);
            Img(board, new Color(0.025f, 0.028f, 0.035f, 0.97f), true);
            RectTransform header = Node(board, "Header", topLeft, Vector2.one, top, Vector2.zero, new Vector2(0f, 84f));
            Img(header, Panel);
            Node(header, "AccentLine", Vector2.zero, new Vector2(1f, 0f), new Vector2(0.5f, 0f), Vector2.zero, new Vector2(0f, 3f)).gameObject.AddComponent<Image>().color = Accent;
            Text(header, "Title", "안전가옥 단말기  <size=60%><color=#9AA3AE>SAFEHOUSE TERMINAL</color></size>", 34f, TextColor, TextAlignmentOptions.Left,
                new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(40f, 0f), new Vector2(700f, 60f), true);
            Button close = ButtonAt(header, "Close", "닫기 · Close   [ESC]", new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(-40f, 0f), new Vector2(260f, 46f), true);
            Button PageTab(string name, string label, float x) =>
                ButtonAt(header, name, label, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(x, 0f), new Vector2(210f, 46f));
            Button contractsTab = PageTab("ContractsTab", "의뢰 · Contracts", 760f);
            Button upgradesTab = PageTab("UpgradesTab", "업그레이드 · Upgrades", 980f);
            Button financeTab = PageTab("FinanceTab", "재정 · Finances", 1200f);

            RectTransform contracts = Stretch(board, "ContractsPage", 0f, 0f, 0f, 0f);
            RectTransform tabs = Node(contracts, "RegionTabs", topLeft, topLeft, topLeft, new Vector2(40f, -104f), new Vector2(1840f, 44f));
            HorizontalLayoutGroup tabLayout = tabs.gameObject.AddComponent<HorizontalLayoutGroup>();
            tabLayout.spacing = 8f;
            tabLayout.childControlWidth = tabLayout.childControlHeight = false;
            tabLayout.childForceExpandWidth = false;
            TextMeshProUGUI regionHeader = Text(contracts, "RegionHeader", "", 24f, TextColor, TextAlignmentOptions.Left, topLeft, topLeft, topLeft, new Vector2(40f, -166f), new Vector2(1400f, 36f), true);

            RectTransform list = Node(contracts, "Missions", topLeft, topLeft, topLeft, new Vector2(40f, -216f), new Vector2(520f, 600f));
            VerticalLayoutGroup listLayout = list.gameObject.AddComponent<VerticalLayoutGroup>();
            listLayout.spacing = 8f;
            listLayout.childControlWidth = true;
            listLayout.childControlHeight = false;
            listLayout.childForceExpandHeight = false;

            RectTransform detail = Node(contracts, "Detail", topLeft, topLeft, topLeft, new Vector2(600f, -216f), new Vector2(1280f, 800f));
            Img(detail, Panel);
            TextMeshProUGUI dTitle = Wrapped(Text(detail, "Title", "", 32f, TextColor, TextAlignmentOptions.TopLeft, topLeft, topLeft, topLeft, new Vector2(30f, -24f), new Vector2(1200f, 46f), true));
            TextMeshProUGUI dBody = Wrapped(Text(detail, "Body", "", 19f, TextColor, TextAlignmentOptions.TopLeft, topLeft, topLeft, topLeft, new Vector2(30f, -84f), new Vector2(760f, 220f)));
            Text(detail, "ObjectivesTitle", "목표  <size=70%><color=#9AA3AE>OBJECTIVES</color></size>", 20f, Accent, TextAlignmentOptions.Left, topLeft, topLeft, topLeft, new Vector2(30f, -320f), new Vector2(600f, 30f), true);
            TextMeshProUGUI dObjectives = Wrapped(Text(detail, "Objectives", "", 19f, TextColor, TextAlignmentOptions.TopLeft, topLeft, topLeft, topLeft, new Vector2(30f, -360f), new Vector2(760f, 220f)));
            dObjectives.lineSpacing = 16f;
            Text(detail, "RewardTitle", "보상  <size=70%><color=#9AA3AE>REWARD</color></size>", 20f, Accent, TextAlignmentOptions.Left, topLeft, topLeft, topLeft, new Vector2(840f, -84f), new Vector2(400f, 30f), true);
            TextMeshProUGUI dReward = Wrapped(Text(detail, "Reward", "", 20f, TextColor, TextAlignmentOptions.TopLeft, topLeft, topLeft, topLeft, new Vector2(840f, -124f), new Vector2(400f, 160f)));
            TextMeshProUGUI status = Text(detail, "Status", "", 18f, new Color(1f, 0.69f, 0.13f), TextAlignmentOptions.Left, Vector2.zero, Vector2.zero, Vector2.zero, new Vector2(30f, 100f), new Vector2(800f, 28f));
            Button start = ButtonAt(detail, "Start", "의뢰 수락 · Accept contract", new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(-30f, 30f), new Vector2(360f, 58f), true);
            Button abandon = ButtonAt(detail, "Abandon", "의뢰 포기 · Abandon contract", new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(-30f, 30f), new Vector2(360f, 58f));

            ContractBoardView boardView = root.AddComponent<ContractBoardView>();
            SetRefs(boardView, ("panel", board.gameObject), ("regionTabs", tabs), ("missionList", list), ("regionHeader", regionHeader), ("detailTitle", dTitle),
                ("detailBody", dBody), ("detailObjectives", dObjectives), ("detailReward", dReward), ("statusText", status), ("startButton", start),
                ("abandonButton", abandon), ("closeButton", close));

            GameObject upgradesPage = BuildUpgradesPage(board, topLeft);
            GameObject financePage = BuildFinancePage(board, topLeft);
            SetRefs(boardView, ("contractsPage", contracts.gameObject), ("upgradesPage", upgradesPage), ("financePage", financePage),
                ("contractsTab", contractsTab), ("upgradesTab", upgradesTab), ("financeTab", financeTab));


            GameObject saved = PrefabUtility.SaveAsPrefabAsset(root, BoardPrefabPath);
            Object.DestroyImmediate(root);
            return saved;
        }

        private static GameObject BuildPrefab(Sprite white)
        {
            GameObject root = new GameObject("MissionUI", typeof(RectTransform));
            Canvas canvas = root.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 15; // under the full map (20)
            CanvasScaler scaler = root.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.Expand; // never smaller than 1920x1080: no overflow on 20:9 phones or 16:10 screens
            root.AddComponent<GraphicRaycaster>();
            root.AddComponent<Contract.UI.SafeAreaFitter>();
            Vector2 topLeft = new Vector2(0f, 1f), top = new Vector2(0.5f, 1f), centre = new Vector2(0.5f, 0.5f);

            // ---- HUD: money + goal ------------------------------------------------------------
            // The top-left HUD is one group so it scales up as a whole with touch controls.
            RectTransform corner = Node(root.transform, "TopLeftHud", topLeft, topLeft, topLeft, Vector2.zero, new Vector2(600f, 420f));
            corner.gameObject.AddComponent<Contract.UI.MobileHudWidget>(); // larger with touch controls
            // Soft backing so the balance stays readable over bright signs and shopfronts.
            RectTransform backing = Node(corner, "HudBacking", topLeft, topLeft, topLeft, new Vector2(22f, -16f), new Vector2(560f, 194f));
            Img(backing, new Color(0.02f, 0.025f, 0.035f, 0.55f));
            RectTransform money = Node(corner, "Money", topLeft, topLeft, topLeft, new Vector2(40f, -30f), new Vector2(440f, 96f));
            TextMeshProUGUI moneyText = Text(money, "Balance", "$0", 40f, TextColor, TextAlignmentOptions.Left, topLeft, topLeft, topLeft, Vector2.zero, new Vector2(300f, 48f), true);
            TextMeshProUGUI delta = Text(money, "Delta", "", 24f, new Color(0.3f, 0.83f, 0.54f), TextAlignmentOptions.Left, topLeft, topLeft, topLeft, new Vector2(290f, -8f), new Vector2(160f, 34f), true);
            Image goalFill = Bar(money, "GoalBar", topLeft, topLeft, new Vector2(0f, -56f), new Vector2(360f, 6f), white);
            TextMeshProUGUI goalLabel = Text(money, "GoalLabel", "", 15f, Muted, TextAlignmentOptions.Left, topLeft, topLeft, topLeft, new Vector2(0f, -66f), new Vector2(440f, 22f));

            // ---- HUD: contract tracker ------------------------------------------------------
            RectTransform tracker = Node(corner, "Tracker", topLeft, topLeft, topLeft, new Vector2(40f, -140f), new Vector2(460f, 250f));
            Img(tracker, new Color(0.04f, 0.045f, 0.055f, 0.82f));
            Node(tracker, "Accent", topLeft, new Vector2(0f, 0f), topLeft, Vector2.zero, new Vector2(4f, 0f)).gameObject.AddComponent<Image>().color = Accent;
            TextMeshProUGUI title = Wrapped(Text(tracker, "Title", "", 21f, TextColor, TextAlignmentOptions.TopLeft, topLeft, topLeft, topLeft, new Vector2(18f, -12f), new Vector2(330f, 54f), true));
            TextMeshProUGUI timer = Text(tracker, "Timer", "", 17f, TextColor, TextAlignmentOptions.TopRight, Vector2.one, Vector2.one, Vector2.one, new Vector2(-14f, -14f), new Vector2(120f, 24f));
            TextMeshProUGUI objectives = Wrapped(Text(tracker, "Objectives", "", 17f, TextColor, TextAlignmentOptions.TopLeft, topLeft, topLeft, topLeft, new Vector2(18f, -72f), new Vector2(430f, 140f)));
            objectives.lineSpacing = 18f;
            TextMeshProUGUI reward = Text(tracker, "Reward", "", 16f, Muted, TextAlignmentOptions.Left, Vector2.zero, Vector2.zero, Vector2.zero, new Vector2(18f, 10f), new Vector2(420f, 24f));
            TextMeshProUGUI idle = Wrapped(Text(corner, "IdleHint", "의뢰 없음 · No active contract\n<size=80%><color=#9AA3AE>안가 단말기에서 의뢰를 고르세요 · Pick one at the safehouse terminal</color></size>",
                17f, TextColor, TextAlignmentOptions.TopLeft, topLeft, topLeft, topLeft, new Vector2(40f, -140f), new Vector2(520f, 60f)));

            // ---- HUD: toast -----------------------------------------------------------------
            RectTransform toast = Node(root.transform, "Toast", top, top, top, new Vector2(0f, -40f), new Vector2(640f, 74f));
            toast.gameObject.AddComponent<Contract.UI.MobileHudWidget>(); // larger with touch controls
            Img(toast, new Color(0.05f, 0.055f, 0.065f, 0.92f));
            Node(toast, "Accent", new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(0.5f, 0f), Vector2.zero, new Vector2(0f, 3f)).gameObject.AddComponent<Image>().color = Accent;
            CanvasGroup toastGroup = toast.gameObject.AddComponent<CanvasGroup>();
            toastGroup.blocksRaycasts = false;
            TextMeshProUGUI toastText = Text(toast, "Text", "", 21f, TextColor, TextAlignmentOptions.Center, Vector2.zero, Vector2.one, centre, Vector2.zero, Vector2.zero, true);

            MissionHudView hud = root.AddComponent<MissionHudView>();
            SetRefs(hud, ("moneyText", moneyText), ("moneyDelta", delta), ("goalFill", goalFill), ("goalLabel", goalLabel), ("tracker", tracker.gameObject),
                ("missionTitle", title), ("objectivesText", objectives), ("timerText", timer), ("rewardText", reward), ("idleHint", idle),
                ("toast", toastGroup), ("toastText", toastText));

            // ---- Result screen -------------------------------------------------------------
            RectTransform shade = Stretch(root.transform, "Result");
            Animate(shade);
            Img(shade, new Color(0f, 0f, 0f, 0.75f), true);
            RectTransform card = Node(shade, "Card", centre, centre, centre, Vector2.zero, new Vector2(820f, 640f));
            Img(card, Panel);
            Node(card, "AccentLine", topLeft, Vector2.one, top, Vector2.zero, new Vector2(0f, 4f)).gameObject.AddComponent<Image>().color = Accent;
            TextMeshProUGUI rTitle = Text(card, "Title", "", 40f, TextColor, TextAlignmentOptions.Left, topLeft, topLeft, topLeft, new Vector2(40f, -30f), new Vector2(740f, 56f), true);
            TextMeshProUGUI rSubtitle = Text(card, "Subtitle", "", 22f, Muted, TextAlignmentOptions.Left, topLeft, topLeft, topLeft, new Vector2(40f, -88f), new Vector2(740f, 32f));
            TextMeshProUGUI rBreakdown = Wrapped(Text(card, "Breakdown", "", 20f, TextColor, TextAlignmentOptions.TopLeft, topLeft, topLeft, topLeft, new Vector2(40f, -140f), new Vector2(740f, 230f)));
            rBreakdown.lineSpacing = 22f;
            TextMeshProUGUI rTotal = Text(card, "Total", "", 30f, TextColor, TextAlignmentOptions.Left, topLeft, topLeft, topLeft, new Vector2(40f, -390f), new Vector2(740f, 44f), true);
            TextMeshProUGUI rBalance = Text(card, "Balance", "", 20f, TextColor, TextAlignmentOptions.Left, topLeft, topLeft, topLeft, new Vector2(40f, -444f), new Vector2(740f, 30f));
            Image rGoal = Bar(card, "GoalBar", topLeft, topLeft, new Vector2(40f, -486f), new Vector2(740f, 8f), white);
            Button primary = ButtonAt(card, "Primary", "", new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(-40f, 36f), new Vector2(300f, 58f), true);
            Button secondary = ButtonAt(card, "Secondary", "", new Vector2(0f, 0f), new Vector2(0f, 0f), new Vector2(40f, 36f), new Vector2(360f, 58f));

            // ---- Victory screen -----------------------------------------------------------------
            RectTransform victory = Stretch(root.transform, "Victory");
            Animate(victory);
            Img(victory, new Color(0.02f, 0.02f, 0.025f, 0.96f), true);
            Text(victory, "Title", "$1,000,000", 110f, TextColor, TextAlignmentOptions.Center, centre, centre, centre, new Vector2(0f, 190f), new Vector2(1400f, 140f), true);
            Text(victory, "Subtitle", "목표 달성  <color=#FF4D5A>VICTORY</color>", 44f, TextColor, TextAlignmentOptions.Center, centre, centre, centre, new Vector2(0f, 90f), new Vector2(1400f, 60f), true);
            TextMeshProUGUI vStats = Wrapped(Text(victory, "Stats", "", 22f, Muted, TextAlignmentOptions.Top, centre, centre, centre, new Vector2(0f, -60f), new Vector2(1600f, 230f)));
            vStats.alignment = TextAlignmentOptions.Top;
            vStats.textWrappingMode = TextWrappingModes.Normal;
            Button vContinue = ButtonAt(victory, "Continue", "계속 플레이 · Keep playing", centre, centre, new Vector2(-200f, -230f), new Vector2(380f, 60f), true);
            Button vMenu = ButtonAt(victory, "MainMenu", "메인 메뉴 · Main menu", centre, centre, new Vector2(200f, -230f), new Vector2(380f, 60f));

            MissionResultView resultView = root.AddComponent<MissionResultView>();
            SetRefs(resultView, ("resultPanel", shade.gameObject), ("resultTitle", rTitle), ("resultSubtitle", rSubtitle), ("breakdown", rBreakdown), ("totalText", rTotal),
                ("balanceText", rBalance), ("goalFill", rGoal), ("primaryButton", primary), ("primaryLabel", primary.GetComponentInChildren<TextMeshProUGUI>()),
                ("secondaryButton", secondary), ("secondaryLabel", secondary.GetComponentInChildren<TextMeshProUGUI>()),
                ("victoryPanel", victory.gameObject), ("victoryStats", vStats), ("victoryContinue", vContinue), ("victoryMainMenu", vMenu));

            GameObject saved = PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
            Object.DestroyImmediate(root);
            return saved;
        }

        // =====================================================================================
        // Scene
        // =====================================================================================

        private static void WireScene(GameObject prefab, GameObject board)
        {
            foreach (string name in new[] { "MissionUI", "ContractBoardUI" })
            {
                GameObject existing = GameObject.Find(name);
                if (existing != null) Object.DestroyImmediate(existing);
            }

            PrefabUtility.InstantiatePrefab(prefab);
            PrefabUtility.InstantiatePrefab(board);

            // Stale prototype labels ("DOWNTOWN CITY", a hard-coded keycard objective...) and the old
            // money / objective widgets are superseded by the mission HUD. Hidden, not deleted (Stage 7 rebuilds the HUD).
            foreach (string path in new[] { "UI Canvas/Objective Label", "UI Canvas/Title", "UI Canvas/Subtitle", "UI Canvas/Accent Bar",
                                             "UI Canvas/Stage Label", "UI Canvas/HUD Panel/Money Text", "UI Canvas/HUD Panel/Objective Panel" })
            {
                GameObject old = GameObject.Find(path);
                if (old != null) old.SetActive(false);
            }

            // The safehouse terminal becomes the contract board.
            GameObject terminal = GameObject.Find("Interactables/SafehouseTerminal");
            if (terminal != null)
            {
                SafehouseInteractable legacy = terminal.GetComponent<SafehouseInteractable>();
                if (legacy != null) Object.DestroyImmediate(legacy);
                if (terminal.GetComponent<ContractBoardInteractable>() == null) terminal.AddComponent<ContractBoardInteractable>();
            }

            // Every contract ends at the parking extraction: no keycard lock.
            ExtractionInteractable extraction = Object.FindAnyObjectByType<ExtractionInteractable>();
            if (extraction != null)
            {
                SerializedObject so = new SerializedObject(extraction);
                so.FindProperty("requiredItemId").stringValue = string.Empty;
                so.FindProperty("readyPrompt").stringValue = "[E] 탈출 · Extract";
                so.ApplyModifiedPropertiesWithoutUndo();
            }
        }
    }

    internal static class ArrayExtensions
    {
        public static T[] Populate<T>(this T[] array, T value)
        {
            for (int i = 0; i < array.Length; i++) array[i] = value;
            return array;
        }
    }
}
