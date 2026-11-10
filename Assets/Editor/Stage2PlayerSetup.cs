using Contract.Data;
using Contract.Interaction;
using Contract.Player;
using UnityEditor;
using UnityEngine;

namespace Contract.Editor
{
    /// <summary>
    /// Stage 2 setup (idempotent, safe to re-run): authors each character's placeholder appearance
    /// and wires PlayerCharacterApplier onto the Player prefab. Does NOT rebuild any scene.
    /// Headless: -executeMethod Contract.Editor.Stage2PlayerSetup.Run
    /// </summary>
    public static class Stage2PlayerSetup
    {
        private const string PlayerPrefabPath = "Assets/Prefabs/Player/Player.prefab";
        private const string BaseMaterialPath = "Assets/Art/Materials/Player_Suit.mat";

        [MenuItem("CONTRACT/Setup/Stage 2 - Characters and Player")]
        public static void Run()
        {
            AuthorAppearances();
            WirePlayerPrefab();
            GameDatabaseBuilder.Build();
            AssetDatabase.SaveAssets();
            Debug.Log("[Stage2PlayerSetup] Done.");
        }

        private static void AuthorAppearances()
        {
            // Alex Morgan - Speed. Modern Seoul streetwear: oxblood bomber, light-wash denim, white sneakers.
            SetAppearance("char_alex_morgan", new CharacterAppearance
            {
                heightScale = 0.98f,
                shoulderWidth = 1f,
                skinTone = new Color(0.88f, 0.72f, 0.6f),
                hairStyle = HairStyle.ShortTextured,
                hairColor = new Color(0.12f, 0.08f, 0.06f),
                outfit = OutfitStyle.BomberJacket,
                topColor = new Color(0.42f, 0.06f, 0.08f),
                innerColor = new Color(0.94f, 0.94f, 0.92f),
                bottomColor = new Color(0.32f, 0.38f, 0.48f),
                shoeColor = new Color(0.95f, 0.95f, 0.95f),
                accentColor = new Color(0.92f, 0.88f, 0.8f),
                crossBodyBag = true
            });

            // Daniel Cross - Stealth. Dark minimalist techwear: black hoodie, black mask, undercut.
            SetAppearance("char_daniel_cross", new CharacterAppearance
            {
                heightScale = 1.02f,
                shoulderWidth = 1.03f,
                skinTone = new Color(0.84f, 0.68f, 0.56f),
                hairStyle = HairStyle.Undercut,
                hairColor = new Color(0.03f, 0.03f, 0.035f),
                outfit = OutfitStyle.Hoodie,
                topColor = new Color(0.06f, 0.06f, 0.07f),
                innerColor = new Color(0.5f, 0.5f, 0.52f),
                bottomColor = new Color(0.05f, 0.05f, 0.06f),
                shoeColor = new Color(0.08f, 0.08f, 0.09f),
                accentColor = new Color(0.45f, 0.05f, 0.07f),
                faceMask = true
            });

            // Ryan Blake - Intelligence. Charcoal business suit, white shirt, red tie, glasses.
            SetAppearance("char_ryan_blake", new CharacterAppearance
            {
                heightScale = 1f,
                shoulderWidth = 0.97f,
                skinTone = new Color(0.9f, 0.75f, 0.63f),
                hairStyle = HairStyle.SlickedBack,
                hairColor = new Color(0.07f, 0.05f, 0.04f),
                outfit = OutfitStyle.BusinessSuit,
                topColor = new Color(0.16f, 0.17f, 0.19f),
                innerColor = new Color(0.93f, 0.93f, 0.95f),
                bottomColor = new Color(0.16f, 0.17f, 0.19f),
                shoeColor = new Color(0.16f, 0.09f, 0.05f),
                accentColor = new Color(0.5f, 0.06f, 0.1f),
                glasses = true
            });
        }

        private static void SetAppearance(string characterId, CharacterAppearance appearance)
        {
            foreach (string guid in AssetDatabase.FindAssets("t:CharacterData"))
            {
                CharacterData data = AssetDatabase.LoadAssetAtPath<CharacterData>(AssetDatabase.GUIDToAssetPath(guid));
                if (data != null && data.characterId == characterId)
                {
                    data.appearance = appearance;
                    EditorUtility.SetDirty(data);
                    return;
                }
            }

            Debug.LogError($"[Stage2PlayerSetup] CharacterData '{characterId}' not found.");
        }

        private static void WirePlayerPrefab()
        {
            GameObject root = PrefabUtility.LoadPrefabContents(PlayerPrefabPath);
            try
            {
                PlayerCharacterApplier applier = root.GetComponent<PlayerCharacterApplier>();
                if (applier == null)
                {
                    applier = root.AddComponent<PlayerCharacterApplier>();
                }

                SerializedObject so = new SerializedObject(applier);
                so.FindProperty("controller").objectReferenceValue = root.GetComponent<ThirdPersonController>();
                so.FindProperty("animationDriver").objectReferenceValue = root.GetComponent<PlayerAnimationDriver>();
                so.FindProperty("interactionController").objectReferenceValue = root.GetComponent<InteractionController>();
                so.FindProperty("placeholderBaseMaterial").objectReferenceValue = AssetDatabase.LoadAssetAtPath<Material>(BaseMaterialPath);
                so.ApplyModifiedPropertiesWithoutUndo();

                PrefabUtility.SaveAsPrefabAsset(root, PlayerPrefabPath);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }
    }
}
