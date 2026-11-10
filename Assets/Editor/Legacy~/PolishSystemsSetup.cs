using UnityEditor;

namespace Contract.EditorTools
{
    /// <summary>
    /// Legacy entry point for STAGE 8 setup. Delegates to the current stage setup.
    /// </summary>
    public static class PolishSystemsSetup
    {
        [MenuItem("CONTRACT/Setup/Setup Polish Systems")]
        public static void SetupPolishSystems()
        {
            ContractStage8Setup.RunSetup();
        }
    }
}
