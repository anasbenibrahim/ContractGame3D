using System.Collections.Generic;
using System.Linq;
using Contract.Activity;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;

namespace Contract.Editor.World
{
    /// <summary>
    /// Builds the GPS walking loop for Gangnam: navmesh paths between landmarks (so the route keeps
    /// to sidewalks and uses the zebra crossings), joined into one closed loop and stored on a
    /// <see cref="GpsRoute"/> in GameWorld. Run after the population builder (needs the navmesh).
    /// Headless: -executeMethod Contract.Editor.World.GpsRouteBuilder.Build
    /// </summary>
    public static class GpsRouteBuilder
    {
        // Clockwise: east sidewalk south → north, the food alley, the hotel side, west sidewalk south, back east.
        private static readonly Vector3[] Landmarks =
        {
            new Vector3(11.5f, 0.2f, -45f),
            new Vector3(11.5f, 0.2f, 44f),
            new Vector3(58f, 0.2f, 42f),
            new Vector3(-11.5f, 0.2f, 48f),
            new Vector3(-11.5f, 0.2f, -45f)
        };

        [MenuItem("CONTRACT/World/Build GPS Route")]
        public static void Build()
        {
            var scene = EditorSceneManager.OpenScene("Assets/Scenes/GameWorld.unity", OpenSceneMode.Single);
            BuildInOpenScene(Landmarks);
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
        }

        /// <summary>Builds the loop through <paramref name="landmarks"/> in the open scene (navmesh required).</summary>
        public static GpsRoute BuildInOpenScene(Vector3[] landmarks)
        {
            var points = new List<Vector3>();
            for (int i = 0; i < landmarks.Length; i++)
            {
                Vector3 from = Snap(landmarks[i]), to = Snap(landmarks[(i + 1) % landmarks.Length]);
                var path = new NavMeshPath();
                if (!NavMesh.CalculatePath(from, to, NavMesh.AllAreas, path) || path.status != NavMeshPathStatus.PathComplete)
                {
                    throw new System.InvalidOperationException($"[GpsRouteBuilder] No complete navmesh path between landmarks {i} and {(i + 1) % landmarks.Length}.");
                }

                foreach (Vector3 corner in path.corners)
                {
                    if (points.Count == 0 || Vector3.Distance(points[points.Count - 1], corner) > 0.3f) points.Add(corner);
                }
            }

            if (Vector3.Distance(points[0], points[points.Count - 1]) < 0.3f) points.RemoveAt(points.Count - 1);

            GameObject existing = GameObject.Find("GpsRoute");
            if (existing != null) Object.DestroyImmediate(existing);
            var go = new GameObject("GpsRoute");
            GpsRoute route = go.AddComponent<GpsRoute>();
            route.SetPoints(points.ToArray());
            Debug.Log($"[GpsRouteBuilder] Loop of {points.Count} points, {route.Length:F0} m.");
            return route;
        }

        private static Vector3 Snap(Vector3 p) => NavMesh.SamplePosition(p, out NavMeshHit hit, 6f, NavMesh.AllAreas) ? hit.position : p;
    }
}
