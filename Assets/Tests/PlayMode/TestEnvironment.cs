using System.Reflection;
using Contract.SaveSystem;
using NUnit.Framework;

namespace Contract.Tests
{
    /// <summary>
    /// Runs once before any play-mode test: redirects every save written during the test run
    /// (mission autosaves, the save-on-quit) to a separate folder so tests never touch the
    /// player's real save slot.
    /// </summary>
    [SetUpFixture]
    public sealed class TestEnvironment
    {
        public const string TestSaveFolder = "Saves_Tests";

        [OneTimeSetUp]
        public void RedirectSaves()
        {
            SaveManager save = SaveManager.Instance;
            Assert.IsNotNull(save, "SaveManager should exist once play mode starts");
            typeof(SaveManager).GetField("saveRootFolder", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(save, TestSaveFolder);
        }
    }
}
