using StudyData.Data;
using StudyData.Data.Seed;

namespace StudyData.Edc.Web.Services;

public static class AdminCommands
{
    public static async Task RunAsync(IServiceProvider services, string[] args)
    {
        switch (args[0])
        {
            case "setup-sample-study":
                using (var scope = services.CreateScope())
                {
                    await DbInitializer.CreateSampleStudyAsync(scope.ServiceProvider.GetRequiredService<StudyDataContext>());
                }

                Console.WriteLine($"Study {SampleStudy.Code} created.");
                break;
            case "add-site-user" when args.Length >= 5:
                var password = await DbInitializer.AddSiteUserAsync(services, args[1], args[2], args[3], args[4],
                    args.Length > 5 ? args[5] : "Coordinator", args.Contains("--sign"));
                Console.WriteLine(password.Length > 0
                    ? $"{args[1]} created. Initial password: {password} (give it to the user by phone; the authenticator is set up at first sign-in)."
                    : $"{args[1]} delegated to site {args[4]}.");
                break;
            default:
                Console.WriteLine("Usage: add-site-user <email> \"<full name>\" <study> <site> <role> [--sign]");
                break;
        }
    }
}
