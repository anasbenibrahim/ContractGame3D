using System.Linq;
using Contract.Core;
using UnityEngine;

namespace Contract.Map
{
    /// <summary>
    /// Scene-owned map service. Renders the stylised map (geometry on the Minimap layer) once into a
    /// RenderTexture with a top-down orthographic camera, converts world positions to map UVs for
    /// both views, and owns opening / closing the full map (M / gamepad Select).
    /// </summary>
    public sealed class MapManager : MonoBehaviour, IMapService
    {
        public static MapManager Instance { get; private set; }

        [Header("Map area (world X/Z)")]
        [SerializeField] private Rect worldBounds = Rect.MinMaxRect(-100f, -87.5f, 100f, 112.5f);
        [SerializeField] private string regionKorean = "강남";
        [SerializeField] private string regionEnglish = "GANGNAM";

        [Header("Static map render")]
        [SerializeField] private int mapLayer = 13;
        [SerializeField] private float mapGeometryHeight = 300f;
        [SerializeField] private int textureSize = 2048;
        [SerializeField] private Color backgroundColor = new Color(0.055f, 0.063f, 0.08f);

        [Header("Input")]
        [SerializeField] private InputReader inputReader;

        private RenderTexture mapTexture;
        private Transform player;
        private float requestedZoom = -1f;

        public RenderTexture MapTexture => mapTexture;
        public Rect WorldBounds => worldBounds;
        public Transform Player => player;
        public string RegionKorean => regionKorean;
        public string RegionEnglish => regionEnglish;
        public bool IsMapOpen => GameManager.Instance != null && GameManager.Instance.IsMapOpen;

        /// <summary>Zoom requested through <see cref="SetZoom"/> (-1 = none); consumed by the full map view.</summary>
        public float ConsumeRequestedZoom()
        {
            float zoom = requestedZoom;
            requestedZoom = -1f;
            return zoom;
        }

        private void Awake()
        {
            // Scene-owned service: every gameplay scene provides its own instance.
            Instance = this;
            GameManager.Instance?.RegisterService<IMapService>(this);
        }

        private void OnDestroy()
        {
            if (Instance == this)
            {
                Instance = null;
                GameManager.Instance?.Services.Unregister<IMapService>();
            }

            if (mapTexture != null) mapTexture.Release();
        }

        private void OnEnable()
        {
            if (inputReader != null) inputReader.OpenMapPressed += ToggleMap;
        }

        private void OnDisable()
        {
            if (inputReader != null) inputReader.OpenMapPressed -= ToggleMap;
            if (IsMapOpen) CloseMap();
        }

        private void Start()
        {
            GameObject p = GameObject.FindGameObjectWithTag("Player");
            if (p != null) player = p.transform;
            RenderStaticMap();
        }

        /// <summary>Renders the Minimap-layer geometry once (it never changes during play).</summary>
        public void RenderStaticMap()
        {
            if (mapTexture == null)
            {
                mapTexture = new RenderTexture(textureSize, textureSize, 16, RenderTextureFormat.ARGB32)
                {
                    name = "MapTexture",
                    useMipMap = true,
                    autoGenerateMips = true,
                    filterMode = FilterMode.Trilinear,
                    anisoLevel = 4
                };
            }

            GameObject camObject = new GameObject("MapCaptureCamera");
            Camera cam = camObject.AddComponent<Camera>();
            cam.enabled = false;
            cam.orthographic = true;
            cam.orthographicSize = worldBounds.height / 2f;
            cam.aspect = worldBounds.width / worldBounds.height;
            // Just above the flat map geometry so fog has no distance to act over; north (+Z) is up.
            camObject.transform.SetPositionAndRotation(new Vector3(worldBounds.center.x, mapGeometryHeight + 2f, worldBounds.center.y), Quaternion.Euler(90f, 0f, 0f));
            cam.nearClipPlane = 0.5f;
            cam.farClipPlane = 4f;
            cam.cullingMask = 1 << mapLayer;
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = backgroundColor;
            cam.targetTexture = mapTexture;
            cam.Render();
            cam.targetTexture = null;
            Destroy(camObject);
        }

        /// <summary>World position -> 0..1 map coordinates (x east, y north).</summary>
        public Vector2 WorldToUV(Vector3 world) => new Vector2(
            (world.x - worldBounds.xMin) / worldBounds.width,
            (world.z - worldBounds.yMin) / worldBounds.height);

        public Vector3 UVToWorld(Vector2 uv) => new Vector3(
            worldBounds.xMin + uv.x * worldBounds.width, 0f,
            worldBounds.yMin + uv.y * worldBounds.height);

        public void ToggleMap()
        {
            if (IsMapOpen) CloseMap();
            else OpenMap();
        }

        public void OpenMap()
        {
            if (IsMapOpen || GameManager.Instance == null || GameManager.Instance.IsPaused || GameManager.Instance.IsMenuOpen) return;
            GameManager.Instance.OpenMap("full");
            Cursor.lockState = CursorLockMode.None; // mouse drag / buttons on the map
            Cursor.visible = true;
        }

        public void CloseMap()
        {
            if (!IsMapOpen) return;
            GameManager.Instance.CloseMap();
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
        }

        public void RegisterMapIcon(string iconId, MapIconType type, Vector3 position)
        {
            if (string.IsNullOrEmpty(iconId) || MapIcon.All.Any(i => i.IconId == iconId)) return;
            GameObject marker = new GameObject($"MapIcon_{iconId}");
            marker.transform.SetParent(transform, false);
            marker.transform.position = position;
            marker.AddComponent<MapIcon>().Configure(iconId, type, iconId);
        }

        public void UnregisterMapIcon(string iconId)
        {
            MapIcon icon = MapIcon.All.FirstOrDefault(i => i.IconId == iconId);
            if (icon != null) Destroy(icon.gameObject);
        }

        public void SetZoom(float zoom) => requestedZoom = zoom;
    }
}
