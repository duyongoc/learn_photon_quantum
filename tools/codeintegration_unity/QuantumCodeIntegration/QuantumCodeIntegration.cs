using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Xml.Linq;
using UnityEditor;
using Debug = UnityEngine.Debug;

namespace Quantum.Editor {

  internal static class QuantumCodeIntegration {
    private const string AssemblyPath          = "Library/ScriptAssemblies/PhotonQuantumCode.dll";
    private const string QuantumPackageName    = "Packages/com.exitgames.photonquantumcode";
    private const string CodeGenPath           = "codegen/quantum.codegen.host.exe";
    private const int CodegenTimeout           = 10000;
    private const string CodeProjectName       = "PhotonQuantumCode";
    private const string QuantumCopiedCodePath = "Assets/Photon/QuantumCode";
    private const string QuantumToolsPath      = "../tools";
    private const int MenuItemPriority         = 200;

    private readonly static string[] AdditionalDllDirectories = new[] {
      "Assets/Photon/Quantum/Assemblies"
    };

    private static string UnityCodeGenPath {
      get {
        if (UseNetStandard_2_1) {
          return "codegen_unity/netcoreapp3.1/quantum.codegen.unity.host.exe";
        } else {
          return "codegen_unity/quantum.codegen.unity.host.exe";
        }
      }
    }



    private static string AdditonalDllDirectoriesArg => string.Join(" ", AdditionalDllDirectories);

    private static bool UseNetStandard_2_1 {
      get {
#if UNITY_2021_2_OR_NEWER
        var target = EditorUserBuildSettings.activeBuildTarget;
        var group = BuildPipeline.GetBuildTargetGroup(target);
        var apiCompatibility = PlayerSettings.GetApiCompatibilityLevel(group);
        if (apiCompatibility == ApiCompatibilityLevel.NET_Standard) {
          return true;
        }
#endif
        return false;
      }
    }

    private static string QuantumCodePath {
      get {
        var packagePath = Path.GetFullPath(QuantumPackageName);
        if (Directory.Exists(packagePath)) {
          return QuantumPackageName;
        }
        return QuantumCopiedCodePath;
      }
    }

    [MenuItem("Quantum/Code Integration/Run All CodeGen", priority = MenuItemPriority)]
    public static void RunAllCodeGen() {
      RunCodeGenTool(CodeGenPath, Path.GetFullPath(QuantumCodePath));
      AssetDatabase.ImportAsset($"{QuantumCodePath}/Core/CodeGen.cs", ImportAssetOptions.ForceUpdate);
      AssetDatabase.Refresh();
    }

    [MenuItem("Quantum/Code Integration/Run Qtn CodeGen", priority = MenuItemPriority + 11)]
    public static void RunQtnCodeGen() {
      RunCodeGenTool(CodeGenPath, Path.GetFullPath(QuantumCodePath));
      AssetDatabase.ImportAsset($"{QuantumCodePath}/Core/CodeGen.cs", ImportAssetOptions.ForceUpdate);
    }

    [MenuItem("Quantum/Code Integration/Run Unity CodeGen", priority = MenuItemPriority + 12)]
    public static void RunUnityCodeGen() {
      RunCodeGenTool(UnityCodeGenPath, AssemblyPath, "Assets", AdditonalDllDirectoriesArg);
      AssetDatabase.Refresh();
    }

    private static string GetConsistentSlashes(string path) {
      path = path.Replace('/', Path.DirectorySeparatorChar);
      path = path.Replace('\\', Path.DirectorySeparatorChar);
      return path;
    }

    private static string GetToolPath(string toolName) {
      var toolPath = Path.Combine(QuantumToolsPath, toolName);
      toolPath = GetConsistentSlashes(toolPath);
      toolPath = Path.GetFullPath(toolPath);
      return toolPath;
    }

    private static string Enquote(string str) {
      return $"\"{str.Trim('\'', '"')}\"";
    }

    private static void RunCodeGenTool(string toolName, params string[] args) {
      var output = new StringBuilder();
      var hadStdErr = false;

      var path = GetToolPath(toolName);

      if (UnityEngine.SystemInfo.operatingSystemFamily != UnityEngine.OperatingSystemFamily.Windows) {
        ArrayUtility.Insert(ref args, 0, path);
        path = "mono";
      }

      var startInfo = new ProcessStartInfo() {
        FileName = path,
        Arguments = string.Join(" ", args.Select(Enquote)),
        CreateNoWindow = true,
        UseShellExecute = false,
        RedirectStandardOutput = true,
        RedirectStandardError = true,
      };

      using (var proc = new Process()) {
        proc.StartInfo = startInfo;

        proc.OutputDataReceived += (sender, e) => {
          if (e.Data != null) {
            output.AppendLine(e.Data);
          }
        };

        proc.ErrorDataReceived += (sender, e) => {
          if (e.Data != null) {
            output.AppendLine(e.Data);
            hadStdErr = true;
          }
        };

        proc.Start();
        proc.BeginOutputReadLine();
        proc.BeginErrorReadLine();

        if (!proc.WaitForExit(CodegenTimeout)) {
          throw new TimeoutException($"{toolName} timed out");
        }

        if (proc.ExitCode != 0) {
          throw new InvalidOperationException($"{toolName} (args: {string.Join(" ", args)}) failed with {proc.ExitCode}:\n{output}");
        } else if (hadStdErr) {
          Debug.LogWarning($"{toolName} succeeded, but there were problems.\n{output}");
        } else {
          Debug.Log($"{toolName} succeeded.\n{output}");
        }
      }
    }

    [Conditional("QUANTUM_CODE_INTEGRATION_TRACE")]
    static void LogTrace(string message) {
      Debug.Log($"[<color=#add8e6>Quantum/CodeIntegration</color>]: {message}");
    }

    private class CodeDllWatcher {

      const string DelayedUnityCodeGenSentinel = "Temp/RunUnityCodeGen";

      static void CheckSentinel() {
        if (File.Exists(DelayedUnityCodeGenSentinel)) {
          var path = File.ReadAllText(DelayedUnityCodeGenSentinel);

          LogTrace($"Sentinel found with: {path}");
          File.Delete(DelayedUnityCodeGenSentinel);
          if (File.Exists(path)) {
            RunCodeGenTool(UnityCodeGenPath, path, "Assets", AdditonalDllDirectoriesArg);
            AssetDatabase.Refresh();
          } else {
            Debug.LogWarning($"Unable to run Unity codegen on {path} - file does not exist.");
          }
        }
      }

      [InitializeOnLoadMethod]
      private static void Initialize() {

        UnityEditor.Compilation.CompilationPipeline.assemblyCompilationFinished += (path, messages) => {
          if (!IsPathThePhotonQuantumCodeAssembly(path)) {
            LogTrace($"Recompiled other assembly: {path} {(string.Join(" ", messages.Select(x => x.message)))}");
            return;
          }

          LogTrace($"Recompiled Quantum Code assembly: {path}");
          if (messages.Any(x => x.type == UnityEditor.Compilation.CompilerMessageType.Error)) {
            LogTrace($"Quantum Code had errors, not following up with Unity codegen");
            return;
          }

#if UNITY_2020_3_OR_NEWER
          File.WriteAllText(DelayedUnityCodeGenSentinel, path);
          EditorApplication.delayCall += () => {
            LogTrace("Checking sentinel in delayCall");
            CheckSentinel();
          };
        };

        LogTrace("Checking sentinel after reinitialize");
        CheckSentinel();
#else
          RunCodeGenTool(UnityCodeGenPath, path, "Assets", AdditonalDllDirectoriesArg);
          AssetDatabase.Refresh();
        };
#endif

      }

      private static bool IsPathThePhotonQuantumCodeAssembly(string path) {
        return string.Equals(Path.GetFileNameWithoutExtension(path), CodeProjectName, StringComparison.OrdinalIgnoreCase);
      }
    }

    private class QtnPostprocessor : AssetPostprocessor {

      [System.Diagnostics.CodeAnalysis.SuppressMessage("CodeQuality", "IDE0051:Remove unused private members", Justification = "Undocummented AssetPostprocessor callback")]
      private static string OnGeneratedCSProject(string path, string content) {
        if (Path.GetFileNameWithoutExtension(path) != CodeProjectName) {
          return content;
        }

        return AddQtnFilesToCsproj(content);
      }

      [System.Diagnostics.CodeAnalysis.SuppressMessage("CodeQuality", "IDE0051:Remove unused private members", Justification = "AssetPostprocessor callback")]
      private static void OnPostprocessAllAssets(string[] importedAssets, string[] deletedAssets, string[] movedAssets, string[] movedFromAssetPaths) {
        if (importedAssets.Any(IsValidQtnPath) || deletedAssets.Any(IsValidQtnPath) || movedAssets.Any(IsValidQtnPath) || movedFromAssetPaths.Any(IsValidQtnPath)) {
          RunQtnCodeGen();
          DeferredAssetDatabaseRefresh();
        }
      }

      private static string AddQtnFilesToCsproj(string content) {
        // find all the qtns
        var root = Path.GetFullPath(QuantumCodePath);
        var qtns = Directory.GetFiles(root, "*.qtn", SearchOption.AllDirectories);
        if (qtns.Length == 0) {
          return content;
        }

        XDocument doc = XDocument.Load(new StringReader(content));
        var ns = doc.Root.Name.Namespace;

        var group = new XElement(ns + "ItemGroup");
        foreach (var qtn in qtns) {
          group.Add(new XElement(ns + "None", new XAttribute("Include", GetConsistentSlashes(qtn))));
        }

        doc.Root.Add(group);
        using (var writer = new StringWriter()) {
          doc.Save(writer);
          writer.Flush();
          return writer.GetStringBuilder().ToString();
        }
      }

      private static void DeferredAssetDatabaseRefresh() {
        EditorApplication.update -= DeferredAssetDatabaseRefreshHandler;
        EditorApplication.update += DeferredAssetDatabaseRefreshHandler;
      }

      private static void DeferredAssetDatabaseRefreshHandler() {
        EditorApplication.update -= DeferredAssetDatabaseRefreshHandler;
        AssetDatabase.Refresh();
      }

      private static bool IsValidQtnPath(string path) {
        if (!string.Equals(Path.GetExtension(path), ".qtn", StringComparison.OrdinalIgnoreCase)) {
          return false;
        }
        return true;
      }
    }
  }
}