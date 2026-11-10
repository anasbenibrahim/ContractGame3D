using Contract.Core;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Contract.EditorTools
{
    /// <summary>
    /// Configures the project's third-person camera on the GameWorld main camera.
    /// The game uses the custom collision-aware ThirdPersonCamera component.
    /// </summary>
    public static class CinemachineCameraSetup
    {
        [MenuItem("CONTRACT/Setup/Setup Third Person Camera")]
        public static void SetupCinemachineCamera()
        {
            EditorSceneManager.OpenScene("Assets/Scenes/GameWorld.unity", OpenSceneMode.Single);
            ConfigureCamera();
            EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
            EditorSceneManager.SaveScene(EditorSceneManager.GetActiveScene());
            AssetDatabase.SaveAssets();
            Debug.Log("[CONTRACT] Third-person camera setup complete.");
        }

        public static void ConfigureCamera()
        {
            Camera mainCam = Camera.main;
            if (mainCam == null)
            {
                GameObject camObj = new GameObject("Main Camera");
                camObj.tag = "MainCamera";
                mainCam = camObj.AddComponent<Camera>();
                camObj.AddComponent<AudioListener>();
            }

            GameObject player = GameObject.Find("Player");
            Transform cameraTarget = null;
            if (player != null)
            {
                cameraTarget = player.transform.Find("CameraTarget");
                if (cameraTarget == null)
                {
                    GameObject targetObj = new GameObject("CameraTarget");
                    targetObj.transform.SetParent(player.transform, false);
                    targetObj.transform.localPosition = new Vector3(0f, 1.5f, 0f);
                    cameraTarget = targetObj.transform;
                }
            }

            Contract.CameraSystem.ThirdPersonCamera thirdPersonCamera =
                mainCam.GetComponent<Contract.CameraSystem.ThirdPersonCamera>();
            if (thirdPersonCamera == null)
            {
                thirdPersonCamera = mainCam.gameObject.AddComponent<Contract.CameraSystem.ThirdPersonCamera>();
            }

            InputReader inputReader = AssetDatabase.LoadAssetAtPath<InputReader>("Assets/Settings/InputReader.asset");
            SerializedObject serializedCamera = new SerializedObject(thirdPersonCamera);
            SerializedProperty inputReaderProperty = serializedCamera.FindProperty("inputReader");
            SerializedProperty targetProperty = serializedCamera.FindProperty("target");
            if (inputReaderProperty != null) inputReaderProperty.objectReferenceValue = inputReader;
            if (targetProperty != null) targetProperty.objectReferenceValue = cameraTarget;
            serializedCamera.ApplyModifiedPropertiesWithoutUndo();

            EditorUtility.SetDirty(thirdPersonCamera);
            EditorUtility.SetDirty(mainCam);
        }
    }
}
