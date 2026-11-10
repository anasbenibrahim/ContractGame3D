using System.Collections.Generic;
using Contract.Data;
using UnityEngine;
using UnityEngine.Rendering;

namespace Contract.Player
{
    /// <summary>
    /// Builds a stylised, jointed placeholder humanoid from a <see cref="CharacterAppearance"/>.
    /// Joints are empty pivots (so the procedural animator can rotate them); meshes are children
    /// using Unity's built-in primitive meshes with no colliders. Materials are cloned from one
    /// URP Lit base per colour slot (6-8 materials per character, SRP-Batcher friendly).
    /// Dimensions are metres for a ~1.8 m figure with feet at local y = 0, facing +Z.
    /// </summary>
    public static class PlaceholderHumanoidBuilder
    {
        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
        private static readonly int SmoothnessId = Shader.PropertyToID("_Smoothness");

        private static Mesh cube, sphere, capsule, cylinder;

        // Part sizes in this file are written against Unity's standard primitives: cube 1x1x1,
        // sphere 1 m diameter, capsule & cylinder 1 m diameter x 2 m tall. Each mesh is normalised
        // against its measured bounds so the authored sizes are exact metres whatever mesh is used.
        private static readonly System.Collections.Generic.Dictionary<Mesh, Vector3> Normalisation =
            new System.Collections.Generic.Dictionary<Mesh, Vector3>();

        /// <summary>Crowd mode: share materials between identical colours and merge parts per joint.</summary>
        public struct Options
        {
            public bool ShareMaterials;
            public bool CombineMeshes;
            public static Options Crowd => new Options { ShareMaterials = true, CombineMeshes = true };
        }

        private static readonly Dictionary<string, Material> SharedMaterials = new Dictionary<string, Material>();

        private sealed class Palette
        {
            public Material Skin, Hair, Top, Inner, Bottom, Shoes, Accent, Dark;
            public readonly List<Material> All = new List<Material>();
            public bool Shared;

            public Material Make(Material baseMaterial, string name, Color color, float smoothness)
            {
                string key = $"{name}_{ColorUtility.ToHtmlStringRGB(color)}_{smoothness:F2}";
                if (Shared && SharedMaterials.TryGetValue(key, out Material cached) && cached != null)
                {
                    return cached;
                }

                Material material = new Material(baseMaterial) { name = name };
                material.SetColor(BaseColorId, color);
                material.SetFloat(SmoothnessId, smoothness);
                if (Shared) SharedMaterials[key] = material; // lives for the session, never owned by one rig
                else All.Add(material);
                return material;
            }
        }

        public static PlaceholderRig Build(Transform parent, CharacterAppearance look, Material baseMaterial, string rootName = "PlaceholderHumanoid",
            Options options = default)
        {
            LoadMeshes();
            look ??= new CharacterAppearance();

            Palette p = new Palette { Shared = options.ShareMaterials };
            p.Skin = p.Make(baseMaterial, "Skin", look.skinTone, 0.35f);
            p.Hair = p.Make(baseMaterial, "Hair", look.hairColor, look.hairStyle == HairStyle.SlickedBack ? 0.65f : 0.25f);
            p.Top = p.Make(baseMaterial, "Top", look.topColor, look.outfit == OutfitStyle.BomberJacket ? 0.45f : 0.15f);
            p.Inner = p.Make(baseMaterial, "Inner", look.innerColor, 0.2f);
            p.Bottom = p.Make(baseMaterial, "Bottom", look.bottomColor, 0.15f);
            p.Shoes = p.Make(baseMaterial, "Shoes", look.shoeColor, 0.5f);
            p.Accent = p.Make(baseMaterial, "Accent", look.accentColor, 0.4f);
            p.Dark = p.Make(baseMaterial, "Dark", new Color(0.03f, 0.03f, 0.035f), 0.85f);

            GameObject root = new GameObject(rootName);
            root.transform.SetParent(parent, false);
            root.transform.localScale = Vector3.one * look.heightScale;

            PlaceholderRig.Joints j = new PlaceholderRig.Joints();
            float sw = look.shoulderWidth;

            // ---- Pelvis / torso -------------------------------------------------------------
            j.Hips = Joint(root.transform, "Hips", new Vector3(0f, 0.96f, 0f));
            Part(j.Hips, capsule, p.Bottom, new Vector3(0f, -0.01f, 0f), new Vector3(0.31f, 0.11f, 0.2f));
            Part(j.Hips, cube, p.Dark, new Vector3(0f, 0.045f, 0f), new Vector3(0.31f, 0.03f, 0.205f)); // belt

            j.Spine = Joint(j.Hips, "Spine", new Vector3(0f, 0.04f, 0f));
            Part(j.Spine, capsule, p.Top, new Vector3(0f, 0.12f, 0f), new Vector3(0.3f * sw, 0.16f, 0.19f));

            j.Chest = Joint(j.Spine, "Chest", new Vector3(0f, 0.24f, 0f));
            Part(j.Chest, capsule, p.Top, new Vector3(0f, 0.11f, 0f), new Vector3(0.41f * sw, 0.17f, 0.235f));

            j.Neck = Joint(j.Chest, "Neck", new Vector3(0f, 0.28f, 0f));
            Part(j.Neck, cylinder, p.Skin, new Vector3(0f, 0.03f, 0f), new Vector3(0.115f, 0.045f, 0.115f));

            // ---- Head ----------------------------------------------------------------------
            j.Head = Joint(j.Neck, "Head", new Vector3(0f, 0.06f, 0f));
            Part(j.Head, sphere, p.Skin, new Vector3(0f, 0.11f, 0f), new Vector3(0.195f, 0.235f, 0.215f));
            Part(j.Head, sphere, p.Skin, new Vector3(0f, 0.04f, 0.025f), new Vector3(0.15f, 0.1f, 0.15f)); // jaw
            Part(j.Head, capsule, p.Skin, new Vector3(0f, 0.098f, 0.104f), new Vector3(0.026f, 0.022f, 0.03f), new Vector3(-12f, 0f, 0f)); // nose
            Part(j.Head, cube, p.Dark, new Vector3(0f, 0.052f, 0.1f), new Vector3(0.045f, 0.006f, 0.006f)); // mouth
            for (int side = -1; side <= 1; side += 2)
            {
                Part(j.Head, sphere, p.Skin, new Vector3(side * 0.098f, 0.11f, 0f), new Vector3(0.035f, 0.06f, 0.03f)); // ear
                Part(j.Head, sphere, p.Dark, new Vector3(side * 0.043f, 0.128f, 0.1f), new Vector3(0.03f, 0.018f, 0.012f)); // eye
                Part(j.Head, cube, p.Hair, new Vector3(side * 0.045f, 0.152f, 0.101f), new Vector3(0.05f, 0.01f, 0.012f), new Vector3(0f, 0f, side * -6f)); // brow
            }

            BuildHair(j.Head, look.hairStyle, p);

            if (look.glasses)
            {
                for (int side = -1; side <= 1; side += 2)
                {
                    Part(j.Head, cube, p.Dark, new Vector3(side * 0.046f, 0.128f, 0.113f), new Vector3(0.058f, 0.036f, 0.008f));
                    Part(j.Head, cube, p.Dark, new Vector3(side * 0.09f, 0.13f, 0.06f), new Vector3(0.006f, 0.008f, 0.1f)); // temple arm
                }

                Part(j.Head, cube, p.Dark, new Vector3(0f, 0.132f, 0.113f), new Vector3(0.03f, 0.007f, 0.008f));
            }

            if (look.faceMask)
            {
                Part(j.Head, sphere, p.Inner, new Vector3(0f, 0.065f, 0.062f), new Vector3(0.18f, 0.12f, 0.135f)); // mask, nose to chin
                Part(j.Head, cube, p.Inner, new Vector3(0f, 0.065f, 0.128f), new Vector3(0.07f, 0.004f, 0.004f)); // centre pleat
                for (int side = -1; side <= 1; side += 2)
                {
                    Part(j.Head, cube, p.Inner, new Vector3(side * 0.094f, 0.1f, 0.02f), new Vector3(0.004f, 0.006f, 0.08f), new Vector3(20f, 0f, 0f)); // ear loop
                }
            }

            // ---- Outfit details on the torso ----------------------------------------------
            BuildOutfit(j, look, p, sw);

            if (look.crossBodyBag)
            {
                Part(j.Chest, cube, p.Accent, new Vector3(0f, 0.1f, 0.121f), new Vector3(0.035f, 0.5f, 0.012f), new Vector3(0f, 0f, 42f)); // strap
                Part(j.Spine, cube, p.Dark, new Vector3(0.15f, 0.02f, 0.12f), new Vector3(0.18f, 0.12f, 0.06f), new Vector3(0f, -15f, 0f));
            }

            // ---- Arms ----------------------------------------------------------------------
            for (int side = -1; side <= 1; side += 2)
            {
                string s = side < 0 ? "L" : "R";
                Transform shoulder = Joint(j.Chest, "UpperArm" + s, new Vector3(side * 0.225f * sw, 0.2f, 0f));
                Part(shoulder, sphere, p.Top, Vector3.zero, new Vector3(0.13f, 0.12f, 0.13f)); // deltoid
                Part(shoulder, capsule, p.Top, new Vector3(0f, -0.15f, 0f), new Vector3(0.115f, 0.16f, 0.115f));

                Transform forearm = Joint(shoulder, "Forearm" + s, new Vector3(0f, -0.3f, 0f));
                Part(forearm, capsule, p.Top, new Vector3(0f, -0.13f, 0f), new Vector3(0.1f, 0.14f, 0.1f));
                Material cuff = look.outfit == OutfitStyle.BusinessSuit ? p.Inner : (look.outfit == OutfitStyle.BomberJacket ? p.Accent : p.Top);
                Part(forearm, cylinder, cuff, new Vector3(0f, -0.245f, 0f), new Vector3(0.095f, 0.015f, 0.095f));
                Part(forearm, capsule, p.Skin, new Vector3(0f, -0.31f, 0f), new Vector3(0.075f, 0.06f, 0.04f)); // hand

                if (look.outfit == OutfitStyle.BomberJacket)
                {
                    Part(shoulder, cube, p.Accent, new Vector3(side * 0.058f, -0.15f, 0f), new Vector3(0.01f, 0.27f, 0.025f)); // sleeve stripe
                }

                if (side < 0) { j.UpperArmL = shoulder; j.ForearmL = forearm; }
                else { j.UpperArmR = shoulder; j.ForearmR = forearm; }
            }

            // ---- Legs ----------------------------------------------------------------------
            for (int side = -1; side <= 1; side += 2)
            {
                string s = side < 0 ? "L" : "R";
                Transform thigh = Joint(j.Hips, "Thigh" + s, new Vector3(side * 0.095f, -0.02f, 0f));
                Part(thigh, capsule, p.Bottom, new Vector3(0f, -0.22f, 0f), new Vector3(0.155f, 0.24f, 0.155f));

                Transform shin = Joint(thigh, "Shin" + s, new Vector3(0f, -0.44f, 0f));
                Part(shin, capsule, p.Bottom, new Vector3(0f, -0.21f, 0f), new Vector3(0.125f, 0.23f, 0.125f));

                Transform foot = Joint(shin, "Foot" + s, new Vector3(0f, -0.44f, 0f));
                Part(foot, cube, p.Shoes, new Vector3(0f, -0.02f, 0.02f), new Vector3(0.105f, 0.08f, 0.19f));
                Part(foot, sphere, p.Shoes, new Vector3(0f, -0.025f, 0.12f), new Vector3(0.105f, 0.07f, 0.1f)); // toe cap
                Part(foot, cube, look.outfit == OutfitStyle.BusinessSuit ? p.Dark : p.Inner, new Vector3(0f, -0.057f, 0.055f), new Vector3(0.116f, 0.018f, 0.27f)); // sole

                if (side < 0) { j.ThighL = thigh; j.ShinL = shin; j.FootL = foot; }
                else { j.ThighR = thigh; j.ShinR = shin; j.FootR = foot; }
            }

            var owned = new List<Object>(p.All);
            if (options.CombineMeshes)
            {
                foreach (Transform joint in new[] { j.Hips, j.Spine, j.Chest, j.Neck, j.Head, j.UpperArmL, j.ForearmL, j.UpperArmR, j.ForearmR,
                                                    j.ThighL, j.ShinL, j.FootL, j.ThighR, j.ShinR, j.FootR })
                {
                    Mesh merged = CombineParts(joint);
                    if (merged != null) owned.Add(merged);
                }
            }

            PlaceholderRig rig = root.AddComponent<PlaceholderRig>();
            rig.Initialize(j, owned);
            return rig;
        }

        /// <summary>Merges a joint's direct mesh parts into one renderer (one sub-mesh per material).</summary>
        private static Mesh CombineParts(Transform joint)
        {
            var parts = new List<MeshFilter>();
            foreach (Transform child in joint)
            {
                MeshFilter filter = child.GetComponent<MeshFilter>();
                if (filter != null && child.childCount == 0) parts.Add(filter);
            }

            if (parts.Count < 2) return null;

            var byMaterial = new Dictionary<Material, List<CombineInstance>>();
            foreach (MeshFilter part in parts)
            {
                Material material = part.GetComponent<MeshRenderer>().sharedMaterial;
                if (!byMaterial.TryGetValue(material, out List<CombineInstance> list)) byMaterial[material] = list = new List<CombineInstance>();
                list.Add(new CombineInstance { mesh = part.sharedMesh, transform = joint.worldToLocalMatrix * part.transform.localToWorldMatrix });
            }

            var subMeshes = new List<CombineInstance>();
            var materials = new List<Material>();
            foreach (var pair in byMaterial)
            {
                Mesh sub = new Mesh();
                sub.CombineMeshes(pair.Value.ToArray(), true, true);
                subMeshes.Add(new CombineInstance { mesh = sub });
                materials.Add(pair.Key);
            }

            Mesh merged = new Mesh { name = joint.name + "_Merged" };
            merged.CombineMeshes(subMeshes.ToArray(), false, false);
            foreach (CombineInstance ci in subMeshes) Object.Destroy(ci.mesh);

            foreach (MeshFilter part in parts) Object.Destroy(part.gameObject);
            GameObject holder = new GameObject("Merged");
            holder.transform.SetParent(joint, false);
            holder.AddComponent<MeshFilter>().sharedMesh = merged;
            MeshRenderer renderer = holder.AddComponent<MeshRenderer>();
            renderer.sharedMaterials = materials.ToArray();
            renderer.shadowCastingMode = ShadowCastingMode.On;
            return merged;
        }

        private static void BuildHair(Transform head, HairStyle style, Palette p)
        {
            switch (style)
            {
                case HairStyle.ShortTextured:
                    Part(head, sphere, p.Hair, new Vector3(0f, 0.17f, -0.012f), new Vector3(0.21f, 0.15f, 0.225f));
                    Part(head, sphere, p.Hair, new Vector3(0.012f, 0.19f, 0.07f), new Vector3(0.18f, 0.065f, 0.1f), new Vector3(-18f, 0f, -6f)); // fringe
                    Part(head, sphere, p.Hair, new Vector3(0f, 0.12f, -0.06f), new Vector3(0.2f, 0.16f, 0.12f)); // back
                    break;

                case HairStyle.Undercut:
                    Part(head, sphere, p.Hair, new Vector3(0f, 0.2f, 0f), new Vector3(0.17f, 0.12f, 0.22f)); // volume on top
                    Part(head, sphere, p.Hair, new Vector3(0.015f, 0.215f, 0.07f), new Vector3(0.15f, 0.075f, 0.11f), new Vector3(-15f, 0f, 10f)); // swept fringe
                    Part(head, sphere, p.Hair, new Vector3(0f, 0.11f, -0.055f), new Vector3(0.19f, 0.14f, 0.11f)); // short back
                    break;

                case HairStyle.SlickedBack:
                    Part(head, sphere, p.Hair, new Vector3(0f, 0.175f, -0.02f), new Vector3(0.208f, 0.14f, 0.235f));
                    Part(head, sphere, p.Hair, new Vector3(0f, 0.12f, -0.065f), new Vector3(0.2f, 0.17f, 0.11f));
                    break;

                case HairStyle.Bob: // chin-length bob with a straight fringe
                    Part(head, sphere, p.Hair, new Vector3(0f, 0.16f, -0.015f), new Vector3(0.225f, 0.17f, 0.24f));
                    Part(head, sphere, p.Hair, new Vector3(0f, 0.09f, -0.04f), new Vector3(0.23f, 0.2f, 0.2f));
                    Part(head, sphere, p.Hair, new Vector3(0f, 0.19f, 0.075f), new Vector3(0.17f, 0.06f, 0.08f)); // fringe
                    break;

                case HairStyle.Long: // long straight hair down the back
                    Part(head, sphere, p.Hair, new Vector3(0f, 0.165f, -0.015f), new Vector3(0.22f, 0.16f, 0.24f));
                    Part(head, capsule, p.Hair, new Vector3(0f, 0.0f, -0.075f), new Vector3(0.21f, 0.15f, 0.09f));
                    Part(head, sphere, p.Hair, new Vector3(0.01f, 0.19f, 0.075f), new Vector3(0.16f, 0.055f, 0.08f), new Vector3(-15f, 0f, -8f));
                    break;
            }
        }

        private static void BuildOutfit(PlaceholderRig.Joints j, CharacterAppearance look, Palette p, float sw)
        {
            switch (look.outfit)
            {
                case OutfitStyle.BomberJacket:
                    Part(j.Chest, cylinder, p.Accent, new Vector3(0f, 0.27f, 0f), new Vector3(0.19f, 0.025f, 0.17f)); // ribbed collar
                    Part(j.Chest, cube, p.Inner, new Vector3(0f, 0.2f, 0.105f), new Vector3(0.09f, 0.1f, 0.02f)); // t-shirt at the neck
                    Part(j.Chest, cube, p.Dark, new Vector3(0f, 0.07f, 0.118f), new Vector3(0.012f, 0.3f, 0.01f)); // zip
                    Part(j.Spine, cylinder, p.Accent, new Vector3(0f, 0.02f, 0f), new Vector3(0.31f * sw, 0.025f, 0.2f)); // ribbed hem
                    Part(j.Chest, cube, p.Accent, new Vector3(-0.1f * sw, 0.16f, 0.11f), new Vector3(0.06f, 0.035f, 0.01f)); // chest patch
                    break;

                case OutfitStyle.Hoodie:
                    Part(j.Chest, sphere, p.Top, new Vector3(0f, 0.27f, -0.1f), new Vector3(0.3f, 0.2f, 0.18f)); // hood
                    Part(j.Spine, cube, p.Top, new Vector3(0f, 0.08f, 0.1f), new Vector3(0.23f, 0.09f, 0.03f)); // kangaroo pocket
                    for (int side = -1; side <= 1; side += 2)
                    {
                        Part(j.Chest, cube, p.Inner, new Vector3(side * 0.04f, 0.17f, 0.12f), new Vector3(0.01f, 0.12f, 0.01f)); // drawstrings
                    }

                    break;

                case OutfitStyle.BusinessSuit:
                    Part(j.Chest, cube, p.Inner, new Vector3(0f, 0.19f, 0.104f), new Vector3(0.1f, 0.16f, 0.02f)); // shirt
                    Part(j.Chest, cylinder, p.Inner, new Vector3(0f, 0.27f, 0f), new Vector3(0.14f, 0.02f, 0.14f)); // collar
                    Part(j.Chest, cube, p.Accent, new Vector3(0f, 0.16f, 0.116f), new Vector3(0.04f, 0.2f, 0.012f)); // tie
                    for (int side = -1; side <= 1; side += 2)
                    {
                        Part(j.Chest, cube, p.Top, new Vector3(side * 0.06f, 0.17f, 0.112f), new Vector3(0.045f, 0.2f, 0.012f), new Vector3(0f, 0f, side * 18f)); // lapel
                    }

                    Part(j.Spine, sphere, p.Dark, new Vector3(0f, 0.1f, 0.118f), new Vector3(0.018f, 0.018f, 0.008f)); // button
                    Part(j.Chest, cube, p.Inner, new Vector3(-0.12f * sw, 0.17f, 0.113f), new Vector3(0.05f, 0.012f, 0.008f)); // pocket square
                    break;
            }
        }

        private static Transform Joint(Transform parent, string name, Vector3 localPosition)
        {
            Transform joint = new GameObject(name).transform;
            joint.SetParent(parent, false);
            joint.localPosition = localPosition;
            return joint;
        }

        private static void Part(Transform parent, Mesh mesh, Material material, Vector3 localPosition, Vector3 localScale, Vector3 localEuler = default)
        {
            GameObject part = new GameObject(mesh.name);
            part.transform.SetParent(parent, false);
            part.transform.localPosition = localPosition;
            part.transform.localRotation = Quaternion.Euler(localEuler);
            part.transform.localScale = Vector3.Scale(localScale, Normalisation[mesh]);
            part.AddComponent<MeshFilter>().sharedMesh = mesh;
            MeshRenderer renderer = part.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = localScale.x < 0.02f || localScale.z < 0.015f ? ShadowCastingMode.Off : ShadowCastingMode.On;
            renderer.lightProbeUsage = LightProbeUsage.BlendProbes;
        }

        private static void LoadMeshes()
        {
            if (cube != null)
            {
                return;
            }

            cube = PrimitiveMesh(PrimitiveType.Cube, new Vector3(1f, 1f, 1f));
            cylinder = PrimitiveMesh(PrimitiveType.Cylinder, new Vector3(1f, 2f, 1f));
            // Unity's sphere/capsule are ~800 triangles each; a body uses ~50 parts. 12x8 segments
            // look the same at character scale for a quarter of the triangles (and shadow cost).
            sphere = Register(LowPolyCapsule(0f, 12, 4, "LowPolySphere"), new Vector3(1f, 1f, 1f));
            capsule = Register(LowPolyCapsule(1f, 12, 4, "LowPolyCapsule"), new Vector3(1f, 2f, 1f));
        }

        private static Mesh Register(Mesh mesh, Vector3 authoredSize)
        {
            Vector3 size = mesh.bounds.size;
            Normalisation[mesh] = new Vector3(authoredSize.x / size.x, authoredSize.y / size.y, authoredSize.z / size.z);
            return mesh;
        }

        /// <summary>Capsule of radius 0.5 with a straight section of <paramref name="cylinderHeight"/> (0 = sphere).</summary>
        private static Mesh LowPolyCapsule(float cylinderHeight, int segments, int ringsPerCap, string name)
        {
            var vertices = new List<Vector3>();
            var normals = new List<Vector3>();
            var triangles = new List<int>();
            int rings = ringsPerCap * 2 + 1;
            for (int r = 0; r <= rings; r++)
            {
                // Latitude from +90 to -90; the equator ring is duplicated to insert the cylinder.
                int capRing = r <= ringsPerCap ? r : r - 1;
                float lat = Mathf.PI / 2f - capRing * Mathf.PI / (2 * ringsPerCap);
                float offset = r <= ringsPerCap ? cylinderHeight / 2f : -cylinderHeight / 2f;
                for (int s = 0; s <= segments; s++)
                {
                    float lon = s * 2f * Mathf.PI / segments;
                    Vector3 n = new Vector3(Mathf.Cos(lat) * Mathf.Cos(lon), Mathf.Sin(lat), Mathf.Cos(lat) * Mathf.Sin(lon));
                    vertices.Add(n * 0.5f + Vector3.up * offset);
                    normals.Add(n);
                }
            }

            int stride = segments + 1;
            for (int r = 0; r < rings; r++)
            {
                for (int s = 0; s < segments; s++)
                {
                    int a = r * stride + s, b = a + stride;
                    triangles.AddRange(new[] { a, a + 1, b, b, a + 1, b + 1 });
                }
            }

            Mesh mesh = new Mesh { name = name };
            mesh.SetVertices(vertices);
            mesh.SetNormals(normals);
            mesh.SetTriangles(triangles, 0);
            mesh.RecalculateBounds();
            return mesh;
        }

        private static Mesh PrimitiveMesh(PrimitiveType type, Vector3 authoredSize)
        {
            GameObject temp = GameObject.CreatePrimitive(type);
            Mesh mesh = temp.GetComponent<MeshFilter>().sharedMesh;
            Object.DestroyImmediate(temp);

            Vector3 size = mesh.bounds.size;
            Normalisation[mesh] = new Vector3(authoredSize.x / size.x, authoredSize.y / size.y, authoredSize.z / size.z);
            return mesh;
        }
    }
}
