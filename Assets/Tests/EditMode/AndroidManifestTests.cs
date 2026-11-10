using System.Linq;
using System.Xml;
using Contract.Editor;
using NUnit.Framework;

namespace Contract.Tests.EditMode
{
    /// <summary>The build hook declares exactly the GPS mode's permissions and optional hardware.</summary>
    public sealed class AndroidManifestTests
    {
        private const string Ns = "http://schemas.android.com/apk/res/android";

        private static XmlDocument Manifest() 
        {
            var doc = new XmlDocument();
            doc.LoadXml("<manifest xmlns:android=\"" + Ns + "\" package=\"x\"><uses-permission android:name=\"android.permission.INTERNET\"/><application/></manifest>");
            return doc;
        }

        private static string[] Names(XmlDocument doc, string tag) =>
            doc.DocumentElement.SelectNodes(tag).Cast<XmlElement>().Select(e => e.GetAttribute("name", Ns)).ToArray();

        [Test]
        public void DeclaresLocationAndActivityRecognition_ForegroundOnly()
        {
            XmlDocument doc = Manifest();
            AndroidManifestPermissions.Patch(doc);
            string[] permissions = Names(doc, "uses-permission");
            CollectionAssert.Contains(permissions, "android.permission.ACCESS_FINE_LOCATION");
            CollectionAssert.Contains(permissions, "android.permission.ACCESS_COARSE_LOCATION");
            CollectionAssert.Contains(permissions, "android.permission.ACTIVITY_RECOGNITION");
            CollectionAssert.Contains(permissions, "android.permission.VIBRATE", "haptic feedback on touch buttons");
            CollectionAssert.Contains(permissions, "android.permission.INTERNET", "existing permissions kept");
            CollectionAssert.DoesNotContain(permissions, "android.permission.ACCESS_BACKGROUND_LOCATION", "foreground design: no background location");
        }

        [Test]
        public void GpsAndStepHardware_AreOptional_AndPatchingTwiceChangesNothing()
        {
            XmlDocument doc = Manifest();
            AndroidManifestPermissions.Patch(doc);
            string once = doc.OuterXml;
            AndroidManifestPermissions.Patch(doc);
            Assert.AreEqual(once, doc.OuterXml, "not idempotent");
            foreach (XmlElement feature in doc.DocumentElement.SelectNodes("uses-feature"))
            {
                Assert.AreEqual("false", feature.GetAttribute("required", Ns), feature.GetAttribute("name", Ns));
            }

            Assert.AreEqual(3, Names(doc, "uses-feature").Length);
        }
    }
}
