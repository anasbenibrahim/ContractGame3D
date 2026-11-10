using System.IO;
using System.Xml;
using UnityEditor.Android;
using UnityEngine;

namespace Contract.Editor
{
    /// <summary>
    /// Adds the GPS activity mode's declarations to the generated Android manifest:
    /// precise + approximate location (walking distance, checkpoints) and physical activity
    /// (the step counter). GPS and step-counter hardware are declared optional so phones without
    /// them can still install the game (the mode then explains what is unavailable).
    /// Foreground use only: no background-location permission is requested.
    /// </summary>
    public sealed class AndroidManifestPermissions : IPostGenerateGradleAndroidProject
    {
        public int callbackOrder => 10;

        private const string AndroidNs = "http://schemas.android.com/apk/res/android";

        public static readonly string[] Permissions =
        {
            "android.permission.ACCESS_FINE_LOCATION",
            "android.permission.ACCESS_COARSE_LOCATION",
            "android.permission.ACTIVITY_RECOGNITION",
            "android.permission.VIBRATE"   // haptic ticks on touch buttons (normal permission, no prompt)
        };

        public static readonly string[] OptionalFeatures =
        {
            "android.hardware.location",
            "android.hardware.location.gps",
            "android.hardware.sensor.stepcounter"
        };

        public void OnPostGenerateGradleAndroidProject(string unityLibraryPath)
        {
            string path = Path.Combine(unityLibraryPath, "src", "main", "AndroidManifest.xml");
            var doc = new XmlDocument();
            doc.Load(path);
            Patch(doc);
            doc.Save(path);
            Debug.Log("[AndroidManifestPermissions] Location, activity recognition and vibrate declared in " + path);
        }

        /// <summary>Idempotent: adds missing uses-permission / uses-feature elements.</summary>
        public static void Patch(XmlDocument doc)
        {
            XmlElement manifest = doc.DocumentElement;
            foreach (string permission in Permissions) Ensure(doc, manifest, "uses-permission", permission, null);
            foreach (string feature in OptionalFeatures) Ensure(doc, manifest, "uses-feature", feature, "false");
        }

        private static void Ensure(XmlDocument doc, XmlElement manifest, string tag, string name, string required)
        {
            foreach (XmlNode node in manifest.SelectNodes(tag))
            {
                if (node is XmlElement e && e.GetAttribute("name", AndroidNs) == name)
                {
                    if (required != null) e.SetAttribute("required", AndroidNs, required);
                    return;
                }
            }

            XmlElement element = doc.CreateElement(tag);
            element.SetAttribute("name", AndroidNs, name);
            if (required != null) element.SetAttribute("required", AndroidNs, required);
            manifest.PrependChild(element);
        }
    }
}
