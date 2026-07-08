/*using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using System.Diagnostics;
using System.IO;
using System.Collections.Generic;

/// <summary>
/// A build processor that compiles and moves external C# projects when a Unity build is initiated.
/// This class is automatically called by Unity during the build process and does not need to be attached to any GameObject.
/// (called only after "Build" and "Build and Run", not after "Play" in unity editor)
/// </summary>
public class CustomBuildProcessor : IPreprocessBuildWithReport, IPostprocessBuildWithReport
{
    /// <summary>
    /// Determines the order of callback execution for multiple IPreprocessBuildWithReport or IPostprocessBuildWithReport implementations
    /// (the lower the number - the earlier it will be executed)
    /// </summary>
    public int callbackOrder { get { return 0; } }

    /// <summary>
    /// Called by Unity before the build process begins
    /// </summary>
    /// <param name="report">Provides information about the build, such as its target platform and output path</param>
    public void OnPreprocessBuild(BuildReport report)
    {
        //CompileExternalPrograms();
        //UnityEngine.Debug.Log("Preprocessing Build: Compiling and moving external programs.");
    }

    /// <summary>
    /// Called by Unity after the build process completes
    /// </summary>
    /// <param name="report">Provides information about the build, such as its target platform and output path.</param>
    public void OnPostprocessBuild(BuildReport report)
    {
        // Implement post-build actions if necessary.
    }








    /// <summary>
    /// Compiles external C# projects to specific directory in Unity
    /// </summary>
    *//*private void CompileExternalPrograms()
    {
        string assetsFolderPath = UnityEngine.Application.dataPath;                                                                 // ~/MOCU/UnityCore/Assets
        string totalRootFolderPath = Directory.GetParent(Directory.GetParent(assetsFolderPath).FullName).FullName;                  // ~/MOCU
        string destinationFolderPath = Path.Combine(assetsFolderPath, "StreamingAssets");                                           // ~/MOCU/UnityCore/Assets/StreamingAssets

        var externalProjectNames= new List<string>() {
            "AudioControl",                                                                                                         // ~/MOCU/AudioControl/AudioControl/AudioControl.csproj
            "PortFinder",                                                                                                           // ~/MOCU/PortFinder/PortFinder/PortFinder.csproj
            //"MoogControl"                                                                                                         // ~/MOCU/MoogControl/MoogControl/MoogControl.csproj
        };

        foreach (var externalProjectName in externalProjectNames)
        {
            string csprojFilePath = Path.Combine(totalRootFolderPath, $@"{externalProjectName}/{externalProjectName}/{externalProjectName}.csproj");

            CompileExternalProject(csprojFilePath, destinationFolderPath);
        }
    }*//*

    /// <summary>
    /// Compiles the specified external C# project using the 'dotnet publish' command
    /// </summary>
    /// <param name="csprojFilePath">The file path of the .csproj file to compile</param>
    *//*private void CompileExternalProject(string csprojFilePath, string destinationFolderPath)
    {
        ProcessStartInfo startInfo = new ProcessStartInfo
        {
            FileName = "dotnet",
            Arguments = $"publish \"{csprojFilePath}\" -c Release -o \"{destinationFolderPath}\"",
            UseShellExecute = true, // false
            RedirectStandardOutput = true,
            CreateNoWindow = false  // true
        };

        using (Process process = Process.Start(startInfo))
        {
            using (StreamReader reader = process.StandardOutput)
            {
                string result = reader.ReadToEnd();
                UnityEngine.Debug.Log(result);
            }
        }
    }*//*
}
*/