using System;
using System.IO;

namespace Kiwindow.Engine
{
    /// <summary>
    /// Where Kiwindow keeps its data. The standalone app and the SwissTechKnife module share this folder,
    /// so a profile captured in one shows up in the other.
    /// </summary>
    public static class Paths
    {
        /// <summary>%LOCALAPPDATA%\Kiwindow, or <c>KIWINDOW_HOME</c> when set (isolated data for testing).</summary>
        public static string DataRoot { get; } = !string.IsNullOrEmpty(Environment.GetEnvironmentVariable("KIWINDOW_HOME"))
            ? Environment.GetEnvironmentVariable("KIWINDOW_HOME")
            : Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Kiwindow");

        public static string Profiles => Path.Combine(DataRoot, "profiles");

        public static string LastApplyLog => Path.Combine(DataRoot, "last-apply.log");
    }
}
