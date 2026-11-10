using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace Contract.Editor.World
{
    /// <summary>A shop or business sign: Korean name, optional English line, colour scheme.</summary>
    public readonly struct Biz
    {
        public readonly string Ko, En, Board;
        public readonly Color Text;

        public Biz(string ko, string en, string board, Color text)
        {
            Ko = ko; En = en; Board = board; Text = text;
        }
    }

    /// <summary>
    /// Reusable Korean urban modules. Repeated props are saved as prefabs under
    /// Assets/Prefabs/Environment/Korean; buildings are generated per plot (every one is unique).
    /// Convention: a module's front faces local +Z, pivot at ground level.
    /// </summary>
    public sealed class KoreanModules
    {
        public const string PrefabFolder = "Assets/Prefabs/Environment/Korean";
        public const float FloorHeight = 3.5f;
        public const float GroundFloorHeight = 4.2f;

        private readonly EnvKit kit;
        private readonly Dictionary<string, GameObject> prefabs = new Dictionary<string, GameObject>();
        private readonly System.Random rng = new System.Random(2024);

        public static readonly Color White = Color.white;
        public static readonly Color Dark = new Color(0.08f, 0.08f, 0.09f);

        public KoreanModules(EnvKit kit)
        {
            this.kit = kit;
            Directory.CreateDirectory(PrefabFolder);
            BuildPrefabs();
        }

        // =====================================================================================
        // Prefab props
        // =====================================================================================

        public GameObject Place(Transform parent, string prefab, Vector3 position, float yaw = 0f)
        {
            GameObject instance = (GameObject)PrefabUtility.InstantiatePrefab(prefabs[prefab], parent);
            instance.transform.localPosition = position;
            instance.transform.localRotation = Quaternion.Euler(0f, yaw, 0f);
            return instance;
        }

        private void BuildPrefabs()
        {
            SavePrefab("StreetLight", StreetLight);
            SavePrefab("GinkgoTree", GinkgoTree);
            SavePrefab("Bench", Bench);
            SavePrefab("Bollard", Bollard);
            SavePrefab("TrashBins", TrashBins);
            SavePrefab("SignalMast_Red", t => SignalMast(t, red: true));
            SavePrefab("SignalMast_Green", t => SignalMast(t, red: false));
            SavePrefab("PedSignal_Red", t => PedSignal(t, red: true));
            SavePrefab("PedSignal_Green", t => PedSignal(t, red: false));
            SavePrefab("BusStop", BusStop);
            SavePrefab("Car_White", t => Car(t, "CarWhite", false));
            SavePrefab("Car_Black", t => Car(t, "CarBlack", false));
            SavePrefab("Car_Silver", t => Car(t, "CarSilver", false));
            SavePrefab("Car_Blue", t => Car(t, "CarBlue", false));
            SavePrefab("Taxi", t => Car(t, "TaxiOrange", true));
            SavePrefab("DeliveryScooter", DeliveryScooter);
            SavePrefab("Planter", Planter);
            SavePrefab("Pojangmacha", Pojangmacha);
        }

        private void SavePrefab(string name, System.Action<Transform> build)
        {
            GameObject root = new GameObject(name);
            build(root.transform);
            string path = $"{PrefabFolder}/{name}.prefab";
            prefabs[name] = PrefabUtility.SaveAsPrefabAsset(root, path);
            Object.DestroyImmediate(root);
        }

        private void StreetLight(Transform t)
        {
            kit.Block(t, "Base", Vector3.zero, new Vector3(0.45f, 0.5f, 0.45f), "MetalDark", collider: false);
            kit.Primitive(t, PrimitiveType.Cylinder, "Pole", new Vector3(0f, 4.2f, 0f), new Vector3(0.18f, 4f, 0.18f), "MetalDark");
            kit.Block(t, "Arm", new Vector3(0f, 8f, 1.1f), new Vector3(0.12f, 0.12f, 2.4f), "MetalDark", collider: false, euler: new Vector3(-8f, 0f, 0f));
            kit.Block(t, "Head", new Vector3(0f, 7.85f, 2.3f), new Vector3(0.45f, 0.14f, 0.8f), "MetalDark", collider: false);
            kit.Block(t, "Lens", new Vector3(0f, 7.83f, 2.3f), new Vector3(0.38f, 0.02f, 0.7f), "LampWarm", collider: false, castShadows: false);

            Light light = new GameObject("Light").AddComponent<Light>();
            light.transform.SetParent(t, false);
            light.transform.localPosition = new Vector3(0f, 7.6f, 2.3f);
            light.type = LightType.Point;
            light.color = new Color(1f, 0.85f, 0.66f);
            light.intensity = 9f;
            light.range = 19f;
            light.shadows = LightShadows.None;

            CapsuleCollider pole = t.gameObject.AddComponent<CapsuleCollider>();
            pole.center = new Vector3(0f, 4f, 0f);
            pole.height = 8f;
            pole.radius = 0.2f;
        }

        private void GinkgoTree(Transform t)
        {
            kit.Block(t, "Grate", new Vector3(0f, 0f, 0f), new Vector3(1.3f, 0.03f, 1.3f), "MetalDark", collider: false);
            kit.Primitive(t, PrimitiveType.Cylinder, "Trunk", new Vector3(0f, 2f, 0f), new Vector3(0.24f, 2f, 0.24f), "Bark");
            // Seoul's street ginkgos are tall and columnar: stacked, slightly offset crowns.
            kit.Primitive(t, PrimitiveType.Sphere, "Crown1", new Vector3(0f, 3.6f, 0f), new Vector3(2.6f, 2.4f, 2.6f), "Ginkgo");
            kit.Primitive(t, PrimitiveType.Sphere, "Crown2", new Vector3(0.2f, 5.1f, -0.1f), new Vector3(2.2f, 2.3f, 2.2f), "GinkgoLight");
            kit.Primitive(t, PrimitiveType.Sphere, "Crown3", new Vector3(-0.1f, 6.4f, 0.1f), new Vector3(1.5f, 1.8f, 1.5f), "Ginkgo");

            CapsuleCollider trunk = t.gameObject.AddComponent<CapsuleCollider>();
            trunk.center = new Vector3(0f, 1.5f, 0f);
            trunk.height = 3f;
            trunk.radius = 0.2f;
        }

        private void Bench(Transform t)
        {
            for (int i = 0; i < 3; i++)
            {
                kit.Block(t, "Slat" + i, new Vector3(0f, 0.45f, -0.15f + i * 0.15f), new Vector3(1.8f, 0.05f, 0.12f), "Wood", collider: false);
            }

            kit.Block(t, "Back", new Vector3(0f, 0.6f, -0.25f), new Vector3(1.8f, 0.35f, 0.05f), "Wood", collider: false, euler: new Vector3(-10f, 0f, 0f));
            kit.Block(t, "LegL", new Vector3(-0.75f, 0f, 0f), new Vector3(0.08f, 0.45f, 0.45f), "MetalDark", collider: false);
            kit.Block(t, "LegR", new Vector3(0.75f, 0f, 0f), new Vector3(0.08f, 0.45f, 0.45f), "MetalDark", collider: false);
            BoxCollider box = t.gameObject.AddComponent<BoxCollider>();
            box.center = new Vector3(0f, 0.45f, -0.05f);
            box.size = new Vector3(1.8f, 0.9f, 0.55f);
        }

        private void Bollard(Transform t)
        {
            kit.Primitive(t, PrimitiveType.Cylinder, "Post", new Vector3(0f, 0.45f, 0f), new Vector3(0.16f, 0.45f, 0.16f), "Steel", collider: true);
            kit.Primitive(t, PrimitiveType.Cylinder, "Band", new Vector3(0f, 0.75f, 0f), new Vector3(0.165f, 0.03f, 0.165f), "SignYellow");
        }

        private void TrashBins(Transform t)
        {
            kit.Block(t, "General", new Vector3(-0.35f, 0f, 0f), new Vector3(0.6f, 1f, 0.5f), "MetalLight");
            kit.Block(t, "Recycle", new Vector3(0.35f, 0f, 0f), new Vector3(0.6f, 1f, 0.5f), "MetalLight");
            kit.Text(t, "LabelGeneral", "일반쓰레기", new Vector3(-0.35f, 0.75f, 0.26f), new Vector2(0.5f, 0.12f), Dark, kit.SignTextMaterial);
            kit.Text(t, "LabelRecycle", "재활용", new Vector3(0.35f, 0.75f, 0.26f), new Vector2(0.5f, 0.12f), new Color(0.05f, 0.35f, 0.15f), kit.SignTextMaterial);
        }

        private void Planter(Transform t)
        {
            kit.Block(t, "Box", Vector3.zero, new Vector3(2.4f, 0.6f, 0.9f), "Planter");
            for (int i = 0; i < 4; i++)
            {
                kit.Primitive(t, PrimitiveType.Sphere, "Shrub" + i, new Vector3(-0.9f + i * 0.6f, 0.75f, 0f), new Vector3(0.7f, 0.5f, 0.7f), i % 2 == 0 ? "Ginkgo" : "GinkgoLight");
            }
        }

        /// <summary>포장마차: orange-tarp street tent bar with plastic tables and stools.</summary>
        private void Pojangmacha(Transform t)
        {
            kit.Block(t, "Tarp", Vector3.zero, new Vector3(4.2f, 2.5f, 3.2f), "Tarp", collider: false);
            kit.Block(t, "Roof", new Vector3(0f, 2.5f, 0f), new Vector3(4.4f, 0.12f, 3.4f), "Tarp", collider: false);
            kit.Block(t, "Opening", new Vector3(0f, 0.1f, 1.61f), new Vector3(2f, 2f, 0.02f), "Shopfront", collider: false, castShadows: false);
            kit.Text(t, "Name", "포차", new Vector3(-1.4f, 2.05f, 1.62f), new Vector2(1f, 0.55f), new Color(0.25f, 0.05f, 0.02f), kit.SignTextMaterial);
            for (int i = 0; i < 2; i++)
            {
                float x = -1f + i * 2f;
                kit.Block(t, "Table" + i, new Vector3(x, 0f, 2.6f), new Vector3(0.8f, 0.7f, 0.8f), i == 0 ? "PlasticBlue" : "PlasticRed", collider: false);
                kit.Primitive(t, PrimitiveType.Cylinder, "Stool" + i + "a", new Vector3(x - 0.65f, 0.2f, 2.6f), new Vector3(0.35f, 0.2f, 0.35f), "PlasticRed");
                kit.Primitive(t, PrimitiveType.Cylinder, "Stool" + i + "b", new Vector3(x + 0.65f, 0.2f, 2.6f), new Vector3(0.35f, 0.2f, 0.35f), "PlasticBlue");
            }

            Light glow = new GameObject("Glow").AddComponent<Light>();
            glow.transform.SetParent(t, false);
            glow.transform.localPosition = new Vector3(0f, 2f, 2.4f);
            glow.type = LightType.Point;
            glow.color = new Color(1f, 0.6f, 0.3f);
            glow.intensity = 2.5f;
            glow.range = 6f;
            glow.shadows = LightShadows.None;

            Steam(t, new Vector3(0.9f, 2.6f, 0.6f));

            BoxCollider c = t.gameObject.AddComponent<BoxCollider>();
            c.center = new Vector3(0f, 1.25f, 0f);
            c.size = new Vector3(4.2f, 2.5f, 3.2f);
        }

        /// <summary>Cooking steam rising through the tent roof (cheap: ~40 particles, no lighting).</summary>
        internal void Steam(Transform t, Vector3 at)
        {
            GameObject go = new GameObject("Steam");
            go.transform.SetParent(t, false);
            go.transform.localPosition = at;
            go.transform.localRotation = Quaternion.Euler(-90f, 0f, 0f);
            ParticleSystem ps = go.AddComponent<ParticleSystem>();
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            ParticleSystem.MainModule main = ps.main;
            main.duration = 4f;
            main.loop = true;
            main.prewarm = true;
            main.startLifetime = new ParticleSystem.MinMaxCurve(2.5f, 4f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(0.35f, 0.7f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.9f, 1.8f);
            main.startColor = new Color(1f, 0.95f, 0.9f, 0.5f);
            main.maxParticles = 40;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            ParticleSystem.EmissionModule emission = ps.emission;
            emission.rateOverTime = 9f;
            ParticleSystem.ShapeModule shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Circle;
            shape.radius = 0.35f;
            ParticleSystem.SizeOverLifetimeModule size = ps.sizeOverLifetime;
            size.enabled = true;
            size.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0f, 0.6f, 1f, 1.8f));
            ParticleSystem.ColorOverLifetimeModule color = ps.colorOverLifetime;
            color.enabled = true;
            var gradient = new Gradient();
            gradient.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(0.3f, 0.15f), new GradientAlphaKey(0f, 0.8f) });
            color.color = gradient;
            ParticleSystem.VelocityOverLifetimeModule drift = ps.velocityOverLifetime;
            drift.enabled = true;
            drift.space = ParticleSystemSimulationSpace.World;
            drift.x = new ParticleSystem.MinMaxCurve(0.15f, 0.15f);
            drift.y = new ParticleSystem.MinMaxCurve(0f, 0f);
            drift.z = new ParticleSystem.MinMaxCurve(0.05f, 0.05f);
            ParticleSystemRenderer renderer = go.GetComponent<ParticleSystemRenderer>();
            renderer.sharedMaterial = kit.SteamMaterial;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            ps.Play();
        }

        /// <summary>Korean horizontal 4-lamp signal on a mast. Arm along local -X, signal faces local -Z.</summary>
        private void SignalMast(Transform t, bool red)
        {
            kit.Primitive(t, PrimitiveType.Cylinder, "Pole", new Vector3(0f, 3.3f, 0f), new Vector3(0.24f, 3.3f, 0.24f), "MetalDark", collider: true);
            kit.Block(t, "Arm", new Vector3(-3.5f, 6.3f, 0f), new Vector3(7f, 0.18f, 0.18f), "MetalDark", collider: false);
            Transform head = EnvKit.Group(t, "Signal", new Vector3(-5.2f, 5.75f, 0f));
            kit.Block(head, "Housing", Vector3.zero, new Vector3(1.6f, 0.45f, 0.35f), "Black", collider: false);
            string[] lamps = { red ? "SignalRed" : "SignalOff", "SignalOff", "SignalOff", red ? "SignalOff" : "SignalGreen" };
            for (int i = 0; i < 4; i++)
            {
                kit.Primitive(head, PrimitiveType.Cylinder, "Lamp" + i, new Vector3(-0.57f + i * 0.38f, 0.225f, -0.18f), new Vector3(0.3f, 0.02f, 0.3f), lamps[i],
                    euler: new Vector3(90f, 0f, 0f), castShadows: false);
                kit.Block(head, "Visor" + i, new Vector3(-0.57f + i * 0.38f, 0.39f, -0.26f), new Vector3(0.34f, 0.03f, 0.16f), "Black", collider: false);
            }
        }

        private void PedSignal(Transform t, bool red)
        {
            kit.Primitive(t, PrimitiveType.Cylinder, "Pole", new Vector3(0f, 1.6f, 0f), new Vector3(0.12f, 1.6f, 0.12f), "MetalDark", collider: true);
            Transform box = EnvKit.Group(t, "Head", new Vector3(0f, 2.4f, 0f));
            kit.Block(box, "Housing", Vector3.zero, new Vector3(0.4f, 0.8f, 0.25f), "Black", collider: false);
            kit.Block(box, "Upper", new Vector3(0f, 0.45f, -0.13f), new Vector3(0.3f, 0.3f, 0.02f), red ? "SignalRed" : "SignalOff", collider: false, castShadows: false);
            kit.Block(box, "Lower", new Vector3(0f, 0.08f, -0.13f), new Vector3(0.3f, 0.3f, 0.02f), red ? "SignalOff" : "SignalGreen", collider: false, castShadows: false);
        }

        private void BusStop(Transform t)
        {
            kit.Block(t, "Roof", new Vector3(0f, 2.7f, -0.2f), new Vector3(6.2f, 0.12f, 1.9f), "MetalLight", collider: false);
            kit.Block(t, "RoofLight", new Vector3(0f, 2.68f, 0f), new Vector3(5.6f, 0.02f, 0.15f), "LampCool", collider: false, castShadows: false);
            kit.Block(t, "BackGlass", new Vector3(0f, 0.15f, -1.05f), new Vector3(6f, 2.5f, 0.04f), "GlassClear");
            kit.Block(t, "SideGlass", new Vector3(-3f, 0.15f, -0.5f), new Vector3(0.04f, 2.5f, 1.1f), "GlassClear");
            for (int i = -1; i <= 1; i += 2)
            {
                kit.Block(t, "Post" + i, new Vector3(i * 3f, 0f, -1.05f), new Vector3(0.1f, 2.7f, 0.1f), "MetalDark", collider: false);
            }

            kit.Block(t, "Bench", new Vector3(0.6f, 0.42f, -0.75f), new Vector3(3.2f, 0.06f, 0.4f), "Wood", collider: false);
            // Illuminated ad panel on the open end (Korean bus shelters always carry one).
            kit.Sign(t, "Ad", new Vector3(3.05f, 2.35f, -0.5f), new Vector2(1.1f, 2f), "SignWhite", "서울 카페\n신메뉴", new Color(0.35f, 0.18f, 0.08f),
                sub: "SEOUL CAFE", yaw: 90f, depth: 0.12f);
            // Stop name + arrival board.
            Transform pole = EnvKit.Group(t, "SignPole", new Vector3(-2.6f, 0f, 0.6f));
            kit.Primitive(pole, PrimitiveType.Cylinder, "Pole", new Vector3(0f, 1.6f, 0f), new Vector3(0.1f, 1.6f, 0.1f), "MetalDark");
            kit.Sign(pole, "StopName", new Vector3(0f, 3.3f, 0.06f), new Vector2(0.9f, 0.55f), "SignBlue", "정류장", White, sub: "강남역", depth: 0.08f);
            kit.Sign(t, "Arrivals", new Vector3(1.5f, 2.55f, -0.98f), new Vector2(2.4f, 0.35f), "SignDark", "146 곧 도착  ·  341 3분", new Color(1f, 0.65f, 0.1f), neonText: true, depth: 0.06f);

            BoxCollider back = t.gameObject.AddComponent<BoxCollider>();
            back.center = new Vector3(0f, 1.3f, -1.05f);
            back.size = new Vector3(6f, 2.6f, 0.1f);
        }

        private void Car(Transform t, string paint, bool taxi)
        {
            kit.Block(t, "Body", new Vector3(0f, 0.3f, 0f), new Vector3(1.8f, 0.75f, 4.5f), paint, collider: false);
            kit.Block(t, "Cabin", new Vector3(0f, 1.05f, -0.2f), new Vector3(1.6f, 0.6f, 2.5f), "CarGlass", collider: false);
            kit.Block(t, "RoofPanel", new Vector3(0f, 1.6f, -0.3f), new Vector3(1.55f, 0.06f, 1.9f), paint, collider: false);
            foreach (float x in new[] { -0.82f, 0.82f })
            {
                foreach (float z in new[] { -1.45f, 1.45f })
                {
                    kit.Primitive(t, PrimitiveType.Cylinder, "Wheel", new Vector3(x, 0.34f, z), new Vector3(0.66f, 0.12f, 0.66f), "Rubber", euler: new Vector3(0f, 0f, 90f));
                }

                kit.Block(t, "Head", new Vector3(x * 0.85f, 0.75f, 2.24f), new Vector3(0.4f, 0.14f, 0.04f), "HeadLight", collider: false, castShadows: false);
                kit.Block(t, "Tail", new Vector3(x * 0.85f, 0.8f, -2.24f), new Vector3(0.4f, 0.14f, 0.04f), "TailLight", collider: false, castShadows: false);
            }

            if (taxi)
            {
                Transform top = EnvKit.Group(t, "TaxiSign", new Vector3(0f, 1.66f, -0.3f));
                kit.Block(top, "Box", Vector3.zero, new Vector3(0.7f, 0.25f, 0.3f), "SignYellow", collider: false);
                kit.Text(top, "Front", "TAXI", new Vector3(0f, 0.13f, 0.16f), new Vector2(0.6f, 0.2f), Dark, kit.SignTextMaterial);
                kit.Text(top, "Back", "TAXI", new Vector3(0f, 0.13f, -0.16f), new Vector2(0.6f, 0.2f), Dark, kit.SignTextMaterial, new Vector3(0f, 0f, 0f));
            }

            BoxCollider box = t.gameObject.AddComponent<BoxCollider>();
            box.center = new Vector3(0f, 0.85f, 0f);
            box.size = new Vector3(1.8f, 1.4f, 4.5f);
        }

        private void DeliveryScooter(Transform t)
        {
            kit.Block(t, "Body", new Vector3(0f, 0.3f, 0f), new Vector3(0.4f, 0.4f, 1.4f), "CarWhite", collider: false);
            kit.Block(t, "Seat", new Vector3(0f, 0.7f, -0.2f), new Vector3(0.35f, 0.12f, 0.6f), "Black", collider: false);
            kit.Block(t, "Column", new Vector3(0f, 0.6f, 0.6f), new Vector3(0.12f, 0.6f, 0.12f), "MetalDark", collider: false, euler: new Vector3(-15f, 0f, 0f));
            kit.Block(t, "Handle", new Vector3(0f, 1.15f, 0.72f), new Vector3(0.65f, 0.05f, 0.05f), "MetalDark", collider: false);
            kit.Primitive(t, PrimitiveType.Cylinder, "WheelF", new Vector3(0f, 0.25f, 0.6f), new Vector3(0.5f, 0.06f, 0.5f), "Rubber", euler: new Vector3(0f, 0f, 90f));
            kit.Primitive(t, PrimitiveType.Cylinder, "WheelR", new Vector3(0f, 0.25f, -0.55f), new Vector3(0.5f, 0.06f, 0.5f), "Rubber", euler: new Vector3(0f, 0f, 90f));
            Transform box = EnvKit.Group(t, "DeliveryBox", new Vector3(0f, 0.8f, -0.55f));
            kit.Block(box, "Box", Vector3.zero, new Vector3(0.5f, 0.45f, 0.45f), "RedPaint", collider: false);
            kit.Text(box, "Label", "배달", new Vector3(0f, 0.24f, -0.23f), new Vector2(0.4f, 0.2f), White, kit.SignTextMaterial, new Vector3(0f, 0f, 0f));
            BoxCollider c = t.gameObject.AddComponent<BoxCollider>();
            c.center = new Vector3(0f, 0.6f, 0f);
            c.size = new Vector3(0.6f, 1.2f, 1.5f);
        }

        // =====================================================================================
        // Buildings (unique per plot). Front faces local +Z.
        // =====================================================================================

        /// <summary>
        /// Korean commercial mid-rise: shopfront ground floor, stacked floor signs (간판),
        /// a projecting vertical sign and rooftop clutter.
        /// </summary>
        public Transform Commercial(Transform parent, string name, Vector3 position, float yaw, float width, float depth, int floors,
            Biz ground, Biz[] floorSigns, Biz? vertical = null, Biz? rooftop = null)
        {
            Transform b = EnvKit.Group(parent, name, position, yaw);
            float upper = (floors - 1) * FloorHeight;
            float height = GroundFloorHeight + upper;

            // Ground floor: concrete shell + lit shopfront + sign band.
            kit.Block(b, "GroundFloor", Vector3.zero, new Vector3(width, GroundFloorHeight, depth), "Concrete", EnvTextureFactory.ConcreteTile);
            Storefront(b, width, depth, rng.NextDouble() < 0.5 ? "Shopfront" : "ShopfrontCool");
            kit.Sign(b, "Ground_" + ground.Ko, new Vector3(0f, GroundFloorHeight - 0.15f, depth / 2f + 0.1f), new Vector2(width * 0.92f, 0.9f), ground.Board, ground.Ko, ground.Text, ground.En);

            // Upper floors.
            kit.Block(b, "Upper", new Vector3(0f, GroundFloorHeight, 0f), new Vector3(width, upper, depth), "Commercial", EnvTextureFactory.CommercialTile);
            Roof(b, width, depth, height);

            // Stacked floor signs: one band per floor on the spandrel, like real Korean facades.
            for (int i = 0; i < floorSigns.Length && i < floors - 1; i++)
            {
                Biz s = floorSigns[i];
                float y = GroundFloorHeight + i * FloorHeight + 0.95f;
                float signWidth = width * (0.55f + (float)rng.NextDouble() * 0.3f);
                float offset = (width - signWidth) / 2f * (rng.NextDouble() < 0.5 ? -1f : 1f);
                kit.Sign(b, $"F{i + 2}_{s.Ko}", new Vector3(offset, y, depth / 2f + 0.09f), new Vector2(signWidth, 0.85f), s.Board, s.Ko, s.Text, s.En);
            }

            if (vertical.HasValue)
            {
                ProjectingSign(b, vertical.Value, new Vector3(width / 2f - 0.4f, GroundFloorHeight + 1f, depth / 2f + 0.7f), Mathf.Min(upper - 1.5f, 9f));
            }

            if (rooftop.HasValue)
            {
                RooftopSign(b, rooftop.Value, height, width, depth);
            }

            return b;
        }

        /// <summary>Glass office tower on a podium, LED crown and a lit corporate name.</summary>
        public const float LobbyHeight = 4.5f;

        public Transform GlassTower(Transform parent, string name, Vector3 position, float yaw, float width, float depth, float height,
            string crownColor, Biz topName, Biz[] podium, bool enterable = false)
        {
            Transform b = EnvKit.Group(parent, name, position, yaw);
            const float podiumHeight = 8f;

            if (enterable)
            {
                EnterablePodium(b, width + 2f, depth + 2f, podiumHeight);
            }
            else
            {
                kit.Block(b, "Podium", Vector3.zero, new Vector3(width + 2f, podiumHeight, depth + 2f), "Concrete", EnvTextureFactory.ConcreteTile);
            }

            if (!enterable) // the enterable podium has its own glass front and doorway
            {
                kit.Block(b, "PodiumGlass", new Vector3(0f, 0.3f, (depth + 2f) / 2f + 0.02f), new Vector3(width + 1.6f, podiumHeight - 1.6f, 0.06f), "ShopfrontCool", collider: false);
                for (float x = -(width + 1.6f) / 2f; x <= (width + 1.6f) / 2f + 0.01f; x += (width + 1.6f) / Mathf.Round((width + 1.6f) / 2.4f))
                {
                    kit.Block(b, "PodiumMullion", new Vector3(x, 0.3f, (depth + 2f) / 2f + 0.06f), new Vector3(0.1f, podiumHeight - 1.6f, 0.06f), "MetalDark", collider: false, castShadows: false);
                }

                kit.Block(b, "PodiumTransom", new Vector3(0f, 3.4f, (depth + 2f) / 2f + 0.06f), new Vector3(width + 1.6f, 0.12f, 0.06f), "MetalDark", collider: false, castShadows: false);
            }
            for (int i = 0; i < podium.Length; i++)
            {
                float segment = (width + 2f) / podium.Length;
                float x = -(width + 2f) / 2f + segment * (i + 0.5f);
                kit.Sign(b, "Podium_" + podium[i].Ko, new Vector3(x, podiumHeight - 0.35f, (depth + 2f) / 2f + 0.1f), new Vector2(segment * 0.85f, 1f),
                    podium[i].Board, podium[i].Ko, podium[i].Text, podium[i].En);
            }

            kit.Block(b, "Tower", new Vector3(0f, podiumHeight, 0f), new Vector3(width, height - podiumHeight, depth), "Glass", EnvTextureFactory.GlassTile);
            kit.Block(b, "Crown", new Vector3(0f, height - 1.2f, 0f), new Vector3(width + 0.3f, 0.5f, depth + 0.3f), crownColor, collider: false, castShadows: false);
            kit.Block(b, "Parapet", new Vector3(0f, height, 0f), new Vector3(width, 1.2f, depth), "MetalDark", collider: false);
            kit.Sign(b, "TopName", new Vector3(0f, height - 2.4f, depth / 2f + 0.15f), new Vector2(Mathf.Min(width * 0.8f, 16f), 2.6f), "SignDark",
                topName.Ko, topName.Text, topName.En, neonText: true, depth: 0.2f);

            AviationLights(b, width, depth, height + 1.2f);
            kit.Primitive(b, PrimitiveType.Cylinder, "Helipad", new Vector3(0f, height + 1.21f, 0f), new Vector3(Mathf.Min(width, depth) * 0.6f, 0.02f, Mathf.Min(width, depth) * 0.6f),
                "Concrete", castShadows: false);
            return b;
        }

        /// <summary>
        /// Hollow podium: side/back walls, a glass front with a 3 m entrance, a lobby ceiling at
        /// <see cref="LobbyHeight"/> and solid mass above. The interior floor is the block slab.
        /// </summary>
        private void EnterablePodium(Transform b, float w, float d, float podiumHeight)
        {
            const float t = 0.3f, door = 3f;
            kit.Block(b, "WallBack", new Vector3(0f, 0f, -d / 2f + t / 2f), new Vector3(w, podiumHeight, t), "Concrete", EnvTextureFactory.ConcreteTile);
            kit.Block(b, "WallLeft", new Vector3(-w / 2f + t / 2f, 0f, 0f), new Vector3(t, podiumHeight, d), "Concrete", EnvTextureFactory.ConcreteTile);
            kit.Block(b, "WallRight", new Vector3(w / 2f - t / 2f, 0f, 0f), new Vector3(t, podiumHeight, d), "Concrete", EnvTextureFactory.ConcreteTile);
            float side = (w - door) / 2f;
            // Front: collidable glass either side of the entrance, solid band above the lobby.
            kit.Block(b, "FrontL", new Vector3(-door / 2f - side / 2f, 0f, d / 2f - 0.1f), new Vector3(side, LobbyHeight, 0.1f), "GlassClear");
            kit.Block(b, "FrontR", new Vector3(door / 2f + side / 2f, 0f, d / 2f - 0.1f), new Vector3(side, LobbyHeight, 0.1f), "GlassClear");
            kit.Block(b, "DoorFrame", new Vector3(0f, 2.9f, d / 2f - 0.1f), new Vector3(door, 0.25f, 0.2f), "MetalDark", collider: false);
            kit.Block(b, "Ceiling", new Vector3(0f, LobbyHeight, 0f), new Vector3(w, podiumHeight - LobbyHeight, d), "Concrete", EnvTextureFactory.ConcreteTile);
            kit.Block(b, "LobbyFloor", new Vector3(0f, 0f, 0f), new Vector3(w - 0.6f, 0.02f, d - 0.6f), "Marble", collider: false, castShadows: false);
            for (float x = -w / 2f + 2f; x < w / 2f - 1f; x += 4f)
            {
                for (float z = -d / 2f + 2f; z < d / 2f - 1f; z += 4f)
                {
                    kit.Block(b, "CeilingLight", new Vector3(x, LobbyHeight - 0.05f, z), new Vector3(1.2f, 0.04f, 0.3f), "LampCool", collider: false, castShadows: false);
                }
            }
        }

        /// <summary>Hotel: glass tower with a tall vertical name sign on the corner.</summary>
        public Transform Hotel(Transform parent, string name, Vector3 position, float yaw, float width, float depth, float height, Biz hotel, Biz[] podium)
        {
            Transform b = GlassTower(parent, name, position, yaw, width, depth, height, "SignWhite", hotel, podium);
            Transform sign = EnvKit.Group(b, "VerticalName", new Vector3(width / 2f + 0.6f, 12f, depth / 2f - 1.5f), 90f);
            kit.Block(sign, "Board", Vector3.zero, new Vector3(2.4f, 18f, 0.4f), "SignDark", collider: false);
            string stacked = string.Join("\n", hotel.Ko.Replace(" ", "").ToCharArray());
            kit.Text(sign, "Front", stacked, new Vector3(0f, 9f, 0.22f), new Vector2(2f, 17f), hotel.Text, kit.NeonTextMaterial);
            kit.Text(sign, "Back", stacked, new Vector3(0f, 9f, -0.22f), new Vector2(2f, 17f), hotel.Text, kit.NeonTextMaterial, new Vector3(0f, 0f, 0f));
            return b;
        }

        /// <summary>Korean apartment slab (아파트) with the block number and brand painted on.</summary>
        public Transform Apartment(Transform parent, string name, Vector3 position, float yaw, float width, float depth, int floors, string number, string brand)
        {
            Transform b = EnvKit.Group(parent, name, position, yaw);
            float height = floors * 2.8f;
            kit.Block(b, "Slab", Vector3.zero, new Vector3(width, height, depth), "Apartment", EnvTextureFactory.ApartmentTile);
            kit.Block(b, "Roof", new Vector3(0f, height, 0f), new Vector3(width * 0.3f, 3f, depth * 0.8f), "Concrete", collider: false);
            kit.Text(b, "Brand", brand, new Vector3(0f, height + 1.6f, depth * 0.4f + 0.05f), new Vector2(width * 0.3f, 2.2f), new Color(0.6f, 0.95f, 0.8f), kit.NeonTextMaterial);
            // Big block number on both end walls - the most recognisable apartment detail.
            kit.Text(b, "NumberE", number, new Vector3(width / 2f + 0.05f, height - 4f, 0f), new Vector2(depth * 0.9f, 4f), new Color(0.25f, 0.3f, 0.4f), kit.SignTextMaterial, new Vector3(0f, -90f, 0f));
            kit.Text(b, "NumberW", number, new Vector3(-width / 2f - 0.05f, height - 4f, 0f), new Vector2(depth * 0.9f, 4f), new Color(0.25f, 0.3f, 0.4f), kit.SignTextMaterial, new Vector3(0f, 90f, 0f));
            AviationLights(b, width, depth, height);
            return b;
        }

        // ---- building parts -----------------------------------------------------------------

        private void Storefront(Transform b, float width, float depth, string glass)
        {
            float z = depth / 2f + 0.03f;
            kit.Block(b, "ShopGlass", new Vector3(0f, 0.15f, z), new Vector3(width - 0.8f, GroundFloorHeight - 1.4f, 0.05f), glass, collider: false);
            int mullions = Mathf.Max(2, Mathf.RoundToInt(width / 3f));
            for (int i = 0; i <= mullions; i++)
            {
                float x = -width / 2f + 0.4f + i * (width - 0.8f) / mullions;
                kit.Block(b, "Mullion", new Vector3(x, 0.15f, z + 0.03f), new Vector3(0.08f, GroundFloorHeight - 1.4f, 0.06f), "MetalDark", collider: false, castShadows: false);
            }
        }

        private void Roof(Transform b, float width, float depth, float height)
        {
            kit.Block(b, "Parapet", new Vector3(0f, height, 0f), new Vector3(width + 0.2f, 0.6f, depth + 0.2f), "Roof", collider: false);
            kit.Primitive(b, PrimitiveType.Cylinder, "WaterTank", new Vector3(-width * 0.25f, height + 1.6f, -depth * 0.2f), new Vector3(2f, 1f, 2f), "MetalLight");
            kit.Block(b, "Stairwell", new Vector3(width * 0.2f, height, -depth * 0.15f), new Vector3(3f, 3f, 3.5f), "Concrete", collider: false);
            int units = 2 + rng.Next(3);
            for (int i = 0; i < units; i++)
            {
                kit.Block(b, "AC" + i, new Vector3(-width * 0.35f + i * 1.4f, height + 0.6f, depth * 0.25f), new Vector3(1.1f, 0.9f, 0.8f), "MetalLight", collider: false);
            }
        }

        private void ProjectingSign(Transform b, Biz biz, Vector3 position, float length)
        {
            // Perpendicular to the facade so it reads along the street, text on both faces.
            Transform sign = EnvKit.Group(b, "Vertical_" + biz.Ko, position, 90f);
            kit.Block(sign, "Board", Vector3.zero, new Vector3(1.2f, length, 0.3f), biz.Board, collider: false, castShadows: false);
            string stacked = string.Join("\n", biz.Ko.Replace(" ", "").ToCharArray());
            Material textMaterial = biz.Board == "SignDark" ? kit.NeonTextMaterial : kit.SignTextMaterial;
            kit.Text(sign, "Front", stacked, new Vector3(0f, length / 2f, 0.17f), new Vector2(1f, length * 0.92f), biz.Text, textMaterial);
            kit.Text(sign, "Back", stacked, new Vector3(0f, length / 2f, -0.17f), new Vector2(1f, length * 0.92f), biz.Text, textMaterial, new Vector3(0f, 0f, 0f));
        }

        private void RooftopSign(Transform b, Biz biz, float height, float width, float depth)
        {
            Transform frame = EnvKit.Group(b, "RooftopSign", new Vector3(0f, height + 0.6f, depth / 2f - 1f));
            kit.Block(frame, "FrameL", new Vector3(-width * 0.35f, 0f, 0f), new Vector3(0.15f, 3f, 0.15f), "MetalDark", collider: false);
            kit.Block(frame, "FrameR", new Vector3(width * 0.35f, 0f, 0f), new Vector3(0.15f, 3f, 0.15f), "MetalDark", collider: false);
            kit.Sign(frame, "Name", new Vector3(0f, 3.2f, 0f), new Vector2(width * 0.75f, 2.2f), "SignDark", biz.Ko, biz.Text, biz.En, neonText: true);
        }

        private void AviationLights(Transform b, float width, float depth, float y)
        {
            foreach (float x in new[] { -width / 2f + 0.5f, width / 2f - 0.5f })
            {
                foreach (float z in new[] { -depth / 2f + 0.5f, depth / 2f - 0.5f })
                {
                    kit.Primitive(b, PrimitiveType.Sphere, "AviationLight", new Vector3(x, y + 0.3f, z), Vector3.one * 0.35f, "SignalRed", castShadows: false);
                }
            }
        }
    }
}
