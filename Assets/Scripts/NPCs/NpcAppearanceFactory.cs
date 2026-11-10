using Contract.Data;
using UnityEngine;

namespace Contract.NPCs
{
    public enum NpcArchetype
    {
        Pedestrian,  // office workers & locals going somewhere
        Shopper,
        Diner,
        Tourist,
        Security,
        Subject      // mission character (behaviour scripted by MissionSubject)
    }

    /// <summary>Plausible Seoul street looks per archetype (deterministic for a given seed).</summary>
    public static class NpcAppearanceFactory
    {
        private static readonly Color[] SkinTones =
        {
            new Color(0.93f, 0.78f, 0.66f), new Color(0.88f, 0.72f, 0.6f), new Color(0.84f, 0.68f, 0.55f), new Color(0.78f, 0.62f, 0.5f)
        };

        private static readonly Color[] Hair =
        {
            new Color(0.04f, 0.035f, 0.035f), new Color(0.1f, 0.07f, 0.05f), new Color(0.2f, 0.13f, 0.08f), new Color(0.36f, 0.25f, 0.16f)
        };

        private static readonly Color[] Dark =
        {
            new Color(0.08f, 0.08f, 0.09f), new Color(0.14f, 0.15f, 0.18f), new Color(0.2f, 0.18f, 0.16f), new Color(0.1f, 0.12f, 0.2f)
        };

        private static readonly Color[] Bright =
        {
            new Color(0.85f, 0.85f, 0.82f), new Color(0.55f, 0.65f, 0.78f), new Color(0.75f, 0.55f, 0.45f), new Color(0.45f, 0.55f, 0.4f),
            new Color(0.7f, 0.25f, 0.3f), new Color(0.95f, 0.8f, 0.55f), new Color(0.35f, 0.3f, 0.55f)
        };

        public static CharacterAppearance Create(NpcArchetype archetype, System.Random rng)
        {
            T Pick<T>(T[] options) => options[rng.Next(options.Length)];
            float Range(float a, float b) => a + (float)rng.NextDouble() * (b - a);
            bool female = rng.NextDouble() < 0.5;

            var look = new CharacterAppearance
            {
                heightScale = female ? Range(0.9f, 0.97f) : Range(0.96f, 1.05f),
                shoulderWidth = female ? Range(0.86f, 0.92f) : Range(0.95f, 1.05f),
                skinTone = Pick(SkinTones),
                hairColor = Pick(Hair),
                hairStyle = female ? (rng.NextDouble() < 0.5 ? HairStyle.Long : HairStyle.Bob)
                                   : (HairStyle)rng.Next(0, 3),
                innerColor = Pick(Bright),
                shoeColor = rng.NextDouble() < 0.5 ? new Color(0.92f, 0.92f, 0.9f) : new Color(0.08f, 0.08f, 0.08f),
                accentColor = Pick(Bright)
            };

            switch (archetype)
            {
                case NpcArchetype.Security:
                    look.outfit = OutfitStyle.BusinessSuit;
                    look.topColor = look.bottomColor = new Color(0.07f, 0.09f, 0.16f); // navy uniform
                    look.innerColor = new Color(0.85f, 0.86f, 0.88f);
                    look.accentColor = new Color(0.12f, 0.14f, 0.25f);
                    look.shoeColor = new Color(0.05f, 0.05f, 0.05f);
                    look.hairStyle = female ? HairStyle.Bob : HairStyle.ShortTextured;
                    break;
                case NpcArchetype.Pedestrian:
                    bool office = rng.NextDouble() < 0.55;
                    look.outfit = office ? OutfitStyle.BusinessSuit : (OutfitStyle)rng.Next(0, 2);
                    look.topColor = office ? Pick(Dark) : Pick(Bright);
                    look.bottomColor = Pick(Dark);
                    look.glasses = rng.NextDouble() < 0.3;
                    break;
                case NpcArchetype.Tourist:
                    look.outfit = rng.NextDouble() < 0.5 ? OutfitStyle.BomberJacket : OutfitStyle.Hoodie;
                    look.topColor = Pick(Bright);
                    look.bottomColor = rng.NextDouble() < 0.5 ? new Color(0.3f, 0.36f, 0.48f) : new Color(0.8f, 0.76f, 0.66f);
                    look.crossBodyBag = true;
                    break;
                default: // shoppers, diners: casual streetwear, sometimes a mask
                    look.outfit = (OutfitStyle)rng.Next(0, 2);
                    look.topColor = rng.NextDouble() < 0.5 ? Pick(Dark) : Pick(Bright);
                    look.bottomColor = rng.NextDouble() < 0.5 ? new Color(0.3f, 0.36f, 0.48f) : Pick(Dark);
                    look.faceMask = rng.NextDouble() < 0.15;
                    look.crossBodyBag = rng.NextDouble() < 0.35;
                    break;
            }

            return look;
        }
    }
}
