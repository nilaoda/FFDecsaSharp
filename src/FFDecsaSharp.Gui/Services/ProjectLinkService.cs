using System.Diagnostics;

namespace FFDecsaSharp.Gui.Services;

internal static class ProjectLinkService
{
    public const string GitHubUrl = "https://github.com/nilaoda/FFDecsaSharp";

    public static void OpenGitHub()
    {
        Process.Start(new ProcessStartInfo(GitHubUrl) { UseShellExecute = true });
    }
}
