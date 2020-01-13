
using System.Diagnostics;
using System.IO;
using UnityEditor;
using UnityEngine;

public static class Builds
{
    public static string[] Scenes = new string[]
    {
        "Assets/Scenes/Dev.unity"
    };

    public static string BuildDir
    {
        get
        {
            return Path.Combine(new DirectoryInfo(Application.dataPath).Parent.ToString(), "Builds");
        }
    }

    public static string BuildFile
    {
        get
        {
            return BuildDir + "/Waves.exe";
        }
    }

    [MenuItem("Build/Build")]
    public static void Build()
    {
        UnityEngine.Debug.Log("Building to " + BuildFile);
        BuildPlayerOptions options = new BuildPlayerOptions();
        options.scenes = Scenes;
        options.target = BuildTarget.StandaloneWindows64;
        options.options = BuildOptions.CompressWithLz4 | BuildOptions.Development;
        options.locationPathName = BuildFile;
        var result = BuildPipeline.BuildPlayer(options);
    }

    [MenuItem("Build/Scripts Only")]
    public static void BuildScripts()
    {
        UnityEngine.Debug.Log("Building scripts to " + BuildFile);
        BuildPlayerOptions options = new BuildPlayerOptions();
        options.scenes = Scenes;
        options.target = BuildTarget.StandaloneWindows64;
        options.options = BuildOptions.BuildScriptsOnly | BuildOptions.CompressWithLz4 | BuildOptions.Development;
        options.locationPathName = BuildFile;
        var result = BuildPipeline.BuildPlayer(options);
    }

    [MenuItem("Build/Run", priority = 0)]
    public static void Run()
    {
        var path = BuildFile;
        if (!File.Exists(path))
        {
            EditorUtility.DisplayDialog("ERROR - No build", $"No build was found to run.\nExpected at path '{path}'", "Ok");
            return;
        }

        Process process = new Process();
        process.StartInfo.FileName = path;
        process.Start();
    }

    [MenuItem("Build/Run", validate = true)]
    public static bool RunCheck()
    {
        var path = BuildFile;
        return File.Exists(path);
    }

    [MenuItem("Build/Clear")]
    public static void Clear()
    {
        var path = BuildDir;
        UnityEngine.Debug.Log("Clearing build path...");
        EditorUtility.DisplayProgressBar("Clearing...", BuildDir, 0f);
        Directory.Delete(path, true);
        EditorUtility.ClearProgressBar();
    }

    [MenuItem("Build/Clear", validate = true)]
    public static bool ClearCheck()
    {
        return Directory.Exists(BuildDir);
    }

    [MenuItem("Build/Open Folder")]
    public static void OpenDir()
    {
        var dir = BuildDir;

        if (Directory.Exists(dir))
        {
            Process.Start(BuildDir);
        }
    }

    [MenuItem("Build/Open Folder", validate = true)]
    public static bool CheckOpenDir()
    {
        return Directory.Exists(BuildDir);
    }
}
