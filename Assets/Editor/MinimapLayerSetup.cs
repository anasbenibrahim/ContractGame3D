using System;
using Contract.Map;
using UnityEngine;
using UnityEditor;
using Object = UnityEngine.Object;

namespace Contract.EditorTools
{
    /// <summary>
    /// Sets up the Minimap rendering layer.
    /// Run: CONTRACT/Setup/Setup Minimap Layer
    /// </summary>
    public static class MinimapLayerSetup
    {
        private const string MinimapLayerName = "Minimap";

        [MenuItem("CONTRACT/Setup/Setup Minimap Layer")]
        public static void SetupMinimapLayer()
        {
            // Add Minimap layer if not exists
            SerializedObject tagManager = new SerializedObject(AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/TagManager.asset")[0]);
            SerializedProperty layers = tagManager.FindProperty("layers");

            bool layerExists = false;
            for (int i = 8; i < 31; i++)
            {
                SerializedProperty layer = layers.GetArrayElementAtIndex(i);
                if (layer.stringValue == MinimapLayerName)
                {
                    layerExists = true;
                    break;
                }
            }

            if (!layerExists)
            {
                for (int i = 8; i < 31; i++)
                {
                    SerializedProperty layer = layers.GetArrayElementAtIndex(i);
                    if (string.IsNullOrEmpty(layer.stringValue))
                    {
                        layer.stringValue = MinimapLayerName;
                        tagManager.ApplyModifiedProperties();
                        Debug.Log($"[CONTRACT] Added '{MinimapLayerName}' layer at index {i}.");
                        break;
                    }
                }
            }

            // Set Minimap camera culling mask
            Camera minimapCam = FindOrCreateMinimapCamera();
            if (minimapCam != null)
            {
                int minimapLayer = LayerMask.NameToLayer(MinimapLayerName);
                int defaultLayer = LayerMask.NameToLayer("Default");
                minimapCam.cullingMask = (1 << minimapLayer) | (1 << defaultLayer);
                EditorUtility.SetDirty(minimapCam);
            }

            // Assign Minimap layer to UI/Map markers
            AssignMinimapLayerToObjects();

            AssetDatabase.SaveAssets();
            Debug.Log("[CONTRACT] Minimap layer setup complete.");
        }

        private static Camera FindOrCreateMinimapCamera()
        {
            GameObject mapManager = GameObject.Find("[MapManager]");
            if (mapManager != null)
            {
                Camera cam = mapManager.GetComponentInChildren<Camera>();
                if (cam != null && cam.name == "MinimapCamera") return cam;
            }

            // Search all
            foreach (Camera cam in Camera.allCameras)
            {
                if (cam.name == "MinimapCamera") return cam;
            }

            return null;
        }

        private static void AssignMinimapLayerToObjects()
        {
            int minimapLayer = LayerMask.NameToLayer(MinimapLayerName);
            if (minimapLayer < 0) return;

            // Assign to MapIcons
            foreach (MapIcon icon in Object.FindObjectsByType<MapIcon>(FindObjectsInactive.Include))
            {
                if (icon.gameObject.layer == 0) // Default layer
                {
                    icon.gameObject.layer = minimapLayer;
                    EditorUtility.SetDirty(icon.gameObject);
                }
            }

            // Assign to player marker if exists
            GameObject player = GameObject.FindGameObjectWithTag("Player");
            if (player != null)
            {
                MapIcon playerIcon = player.GetComponent<MapIcon>();
                if (playerIcon != null && playerIcon.gameObject.layer == 0)
                {
                    playerIcon.gameObject.layer = minimapLayer;
                    EditorUtility.SetDirty(playerIcon.gameObject);
                }
            }
        }
    }
}