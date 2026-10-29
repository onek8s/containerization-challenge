namespace VanArsdel.Integration.LabStubs.Lab
{
    /// <summary>Finds the scenario folder (schemas, maps, rules, pipelines, samples) from the tool's location.</summary>
    public static class ScenarioPaths
    {
        public static string Root { get; set; } = Find();

        public static string Schemas => Path.Combine(Root, "biztalk", "VanArsdel.Integration.Schemas");
        public static string Maps => Path.Combine(Root, "biztalk", "VanArsdel.Integration.Maps");
        public static string Pipelines => Path.Combine(Root, "biztalk", "VanArsdel.Integration.Pipelines");
        public static string Rules => Path.Combine(Root, "rules");
        public static string Seed => Path.Combine(Root, "samples", "seed");
        public static string Samples => Path.Combine(Root, "samples", "inbound");

        private static string Find()
        {
            var dir = new DirectoryInfo(AppContext.BaseDirectory);
            while (dir != null)
            {
                if (File.Exists(Path.Combine(dir.FullName, "B2BIntegration.BizTalk.sln")))
                {
                    return dir.FullName;
                }

                dir = dir.Parent;
            }

            return Directory.GetCurrentDirectory();
        }
    }
}
