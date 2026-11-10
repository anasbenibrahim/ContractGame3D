using System;
using UnityEngine;

namespace Contract.Data
{
    public enum HairStyle
    {
        ShortTextured,
        Undercut,
        SlickedBack,
        Bob,
        Long
    }

    public enum OutfitStyle
    {
        BomberJacket,
        Hoodie,
        BusinessSuit
    }

    /// <summary>
    /// Describes how a character looks when no rigged model is assigned. Used by the
    /// placeholder humanoid builder for both the in-game player and the selection preview.
    /// </summary>
    [Serializable]
    public sealed class CharacterAppearance
    {
        [Header("Body")]
        [Range(0.9f, 1.1f)] public float heightScale = 1f;
        [Range(0.85f, 1.15f)] public float shoulderWidth = 1f;
        public Color skinTone = new Color(0.86f, 0.70f, 0.58f);

        [Header("Hair")]
        public HairStyle hairStyle = HairStyle.ShortTextured;
        public Color hairColor = new Color(0.06f, 0.05f, 0.05f);

        [Header("Outfit")]
        public OutfitStyle outfit = OutfitStyle.BomberJacket;
        public Color topColor = new Color(0.12f, 0.12f, 0.14f);
        public Color innerColor = new Color(0.9f, 0.9f, 0.9f);
        public Color bottomColor = new Color(0.1f, 0.1f, 0.12f);
        public Color shoeColor = new Color(0.95f, 0.95f, 0.95f);
        public Color accentColor = new Color(0.86f, 0.12f, 0.16f);

        [Header("Accessories")]
        public bool glasses;
        public bool faceMask;
        public bool crossBodyBag;

        public CharacterAppearance Clone() => (CharacterAppearance)MemberwiseClone();

        /// <summary>This look wearing an unlocked outfit (body, hair and accessories are kept).</summary>
        public CharacterAppearance WithOutfit(UpgradeData outfitUpgrade)
        {
            CharacterAppearance dressed = Clone();
            if (outfitUpgrade == null || !outfitUpgrade.IsOutfit) return dressed;
            dressed.outfit = outfitUpgrade.outfitStyle;
            dressed.topColor = outfitUpgrade.outfitTopColor;
            dressed.bottomColor = outfitUpgrade.outfitBottomColor;
            dressed.accentColor = outfitUpgrade.outfitAccentColor;
            dressed.faceMask = outfitUpgrade.outfitFaceMask;
            return dressed;
        }
    }
}
