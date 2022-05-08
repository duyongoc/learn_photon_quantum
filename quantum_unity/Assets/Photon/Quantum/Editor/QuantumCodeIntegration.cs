

#region quantum_unity/Assets/Photon/Quantum/Editor/CodeIntegration/QuantumCodeIntegrationDllWatcher.cs
namespace Quantum.Editor {
  using System.IO;
  using UnityEditor;

  [InitializeOnLoad]
  internal static class QuantumCodeIntegrationDllWatcher {
    static QuantumCodeIntegrationDllWatcher() {
      EditorApplication.delayCall += () => {
        if (QuantumEditorSettings.InstanceFailSilently?.ImportQuantumLibrariesImmediately != true)
          return;

        var watcher = new FileSystemWatcher() {
          Path = "Assets/Photon/Quantum/Assemblies",
          Filter = "quantum.*.dll",
          NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.CreationTime | NotifyFilters.Size,
          EnableRaisingEvents = true
        };

        bool needsRefresh = false;

        FileSystemEventHandler handler = (sender, e) => needsRefresh = true;
        watcher.Changed += handler;
        watcher.Created += handler;

        EditorApplication.update += () => {
          if (EditorApplication.isPlayingOrWillChangePlaymode)
            return;

          if (!needsRefresh)
            return;
          needsRefresh = false;
          AssetDatabase.Refresh();
        };
      };
    }
  }
}

#endregion

#region quantum_unity/Assets/Photon/Quantum/Editor/CodeIntegration/QuantumCodeIntegrationQtnWatcher.cs
namespace Quantum.Editor {
  using System.Diagnostics;
  using System.IO;
  using UnityEditor;
  using Debug = UnityEngine.Debug;

  [InitializeOnLoad]
  internal static class QuantumCodeIntegrationQtnWatcher {
    static QuantumCodeIntegrationQtnWatcher() {
      EditorApplication.delayCall += () => {

        if (QuantumEditorSettings.InstanceFailSilently?.AutoRunQtnCodeGen != true)
          return;

        var solutionPath = QuantumEditorSettings.Instance.QuantumSolutionPath;

        var quantumCodePath = Path.Combine(Path.GetDirectoryName(solutionPath), "quantum.code");
        var quantumCodeProjectPath = Path.Combine(quantumCodePath, "quantum.code.csproj");
        var quantumCodegenPath = Path.GetFullPath(Path.Combine(Path.GetDirectoryName(solutionPath), "../tools/codegen/quantum.codegen.host.exe"));

        var watcher = new FileSystemWatcher() {
          Path = quantumCodePath,
          Filter = "*.qtn",
          NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.CreationTime | NotifyFilters.Size,
          EnableRaisingEvents = true,
          IncludeSubdirectories = true,
        };

        bool needsRefresh = false;
        Process currentProcess = null;

        FileSystemEventHandler handler = (sender, e) => {
          needsRefresh = true;
        };

        watcher.Changed += handler;
        watcher.Created += handler;

        EditorApplication.update += () => {

          if (currentProcess != null) {
            if (currentProcess.HasExited) {
              var p = currentProcess;
              currentProcess = null;
              if (p.ExitCode != 0) {
                Debug.LogErrorFormat("Qtn compile failed: {0}", p.StandardError.ReadToEnd());
              }
            } else {
              return;
            }
          }

          if (!needsRefresh)
            return;

          needsRefresh = false;

          currentProcess = Process.Start(new ProcessStartInfo() {
            WindowStyle = ProcessWindowStyle.Hidden,
            CreateNoWindow = true,
            UseShellExecute = false,
            Arguments = $"\"{quantumCodeProjectPath}\"",
            FileName = $"\"{quantumCodegenPath}\"",
            RedirectStandardOutput = true,
            RedirectStandardError = true
          });
        };
      };
    }
  }
}
#endregion

#region quantum_unity/Assets/Photon/Quantum/Editor/CodeIntegration/QuantumCodeIntegrationVisualStudio.cs
namespace Quantum.Editor {
  using System;
  using System.IO;
  using System.Linq;
  using System.Text;
  using UnityEditor;
  using System.Text.RegularExpressions;
  using System.Collections.Generic;
  using System.Diagnostics;
  using Debug = UnityEngine.Debug;
  using System.Collections.ObjectModel;
  using System.Reflection;

  internal class QuantumCodeIntegrationVisualStudio : AssetPostprocessor {

    const string SolutionFolderTypeGuid = "{2150E333-8FDC-42A3-9474-1A3956D46DE8}";
    const string QuantumFolderGuid = "{15E31304-C8D0-4285-99D7-C240CBDBC87A}";

#if !UNITY_2020_1_OR_NEWER
    static bool HasVisualStudioToolsForUnity;
    [InitializeOnLoadMethod]
    private static void Initialize() {
      EditorApplication.delayCall += () => {
        if (QuantumEditorSettings.InstanceFailSilently?.MergeWithVisualStudioSolution != true)
          return;

        const string assemblyName = "SyntaxTree.VisualStudio.Unity.Bridge";
        const string typeName = "SyntaxTree.VisualStudio.Unity.Bridge.ProjectFilesGenerator";
        const string fieldName = "SolutionFileGeneration";

        // the code below is equivalent to:
        // SyntaxTree.VisualStudio.Unity.Bridge.ProjectFilesGenerator.SolutionFileGeneration += PatchSolution;

        try {
          var assembly = Assembly.Load(assemblyName);
          var type = assembly?.GetType(typeName, true, false);
          var field = type.GetFieldOrThrow(fieldName, BindingFlags.Public | BindingFlags.Static);
          var handler = typeof(QuantumCodeIntegrationVisualStudio).CreateMethodDelegate(nameof(PatchSolution), BindingFlags.NonPublic | BindingFlags.Static, field.FieldType);

          var del = (Delegate)field.GetValue(null);
          del = System.Delegate.Combine(del, handler);
          field.SetValue(null, del);

          // only after we're done disable the AssetPostprocessor way
          HasVisualStudioToolsForUnity = true;
        
        } catch (System.Exception) {
          // do nothing
        }
      };
    }
#else
    static bool HasVisualStudioToolsForUnity => false;
#endif

    /// <summary>
    /// This is a undocumented part of AssetPostprocessor: a method named OnGeneratedSlnSolution will get called
    /// after a sln has been generated. However, it doesn't seem to respect GetPostprocessOrder and when this
    /// method is used to patch solution for Visual Studio, stuff gets messed up. That's why Visual Studio
    /// is handled differently.
    /// </summary>
    /// <param name="solutionPath"></param>
    /// <param name="solutionContent"></param>
    /// <returns></returns>
    private static string OnGeneratedSlnSolution(string solutionPath, string solutionContent) {
      if (HasVisualStudioToolsForUnity) {
        return solutionContent;
      }

      if (QuantumEditorSettings.InstanceFailSilently?.MergeWithVisualStudioSolution != true)
        return solutionContent;

      return PatchSolution(solutionPath, solutionContent);
    }

    private static string PatchSolution(string solutionPath, string solutionContent) {
      var quantumSolutionPath = QuantumEditorSettings.Instance.QuantumSolutionPath;

      if (!File.Exists(quantumSolutionPath)) {
        Debug.LogError("Solution file '" + quantumSolutionPath + "' not found. Check QuantumProjectPath in your QuantumEditorSettings.");
        return solutionContent;
      }

      var altered = PatchSolution(solutionPath, solutionContent, quantumSolutionPath,
        new string[] { }
      );

      var quantumCodePackagesDirectory = Path.Combine(Path.GetDirectoryName(quantumSolutionPath), "packages");
      string nugetConfigContents =
$@"<?xml version=""1.0"" encoding=""utf-8""?>
<configuration>
  <config>
    <!-- Quantum: keep nuget packages in the quantum_code/packages directory -->
    <add key=""repositoryPath"" value=""{System.Security.SecurityElement.Escape(quantumCodePackagesDirectory)}"" />
  </config>
</configuration>";

      if (!File.Exists("nuget.config") || File.ReadAllText("nuget.config") != nugetConfigContents) {
        File.WriteAllText("nuget.config", nugetConfigContents);
      }

      //File.WriteAllText($"{name}.original", content);
      //File.WriteAllText($"{name}.altered", altered);
      return altered;
    } 

    private static string MakeRelativePath(string fromDirectory, string toPath) {

      // sanitize paths
      fromDirectory = fromDirectory.Replace('\\', '/').TrimEnd('/') + '/';
      toPath = toPath.Replace('\\', '/');


      Uri fromUri = new Uri(fromDirectory);
      Uri toUri = new Uri(toPath);

      if (fromUri.Scheme != toUri.Scheme) { return toPath; } // path can't be made relative.

      Uri relativeUri = fromUri.MakeRelativeUri(toUri);
      String relativePath = Uri.UnescapeDataString(relativeUri.ToString());

      if (toUri.Scheme.Equals("file", StringComparison.InvariantCultureIgnoreCase)) {
        relativePath = relativePath.Replace(Path.AltDirectorySeparatorChar, Path.DirectorySeparatorChar);
      }

      return relativePath;
    }

    private static string PatchSolution(string solutionPath, string solutionContent, string quantumSolutionPath, string[] quantumProjectsToIgnore) {

      var unitySoltution = Solution.Parse(solutionContent);

      if (File.Exists(solutionPath)) {
        // Unity seems to generate different header depending on whether you choose "Open C# Project"
        // or double click on something; keep the old one to stop solution from reloading
        var existingSolution = Solution.Parse(File.ReadAllText(solutionPath));
        unitySoltution.Header = existingSolution.Header;
      }

      var quantumSolution = Solution.Parse(File.ReadAllText(quantumSolutionPath));
      var quantumSolutionDir = Path.GetDirectoryName(quantumSolutionPath);

      // remove any quantum projects already in unity
      foreach (var project in quantumSolution.Projects) {
        unitySoltution.RemoveProject(project.Guid);
      }
      unitySoltution.RemoveProject(QuantumFolderGuid);


      // remove "Solution Items" too
      var allQuantumProjectsToIgnore = quantumProjectsToIgnore.Concat(
        quantumSolution.Projects
          .Where(x => x.Name == "Solution Items")
          .Select(x => x.Guid)
          .ToList());
      foreach (var ignored in allQuantumProjectsToIgnore) {
        if (quantumSolution.Projects.Contains(ignored)) {
          var project = quantumSolution.Projects[ignored];
          foreach (var nested in project.NestedProjects.ToList()) {
            quantumSolution.RemoveProject(nested);
          }
          quantumSolution.RemoveProject(ignored);
        }
      }


      // find quantum.code guid
      var systemsProject = quantumSolution.Projects.FirstOrDefault(x => x.Name == "quantum.code");
      if (systemsProject == null)
        throw new InvalidOperationException("quantum.code project not found");

      // make each project dependent on quantum
      foreach (var project in unitySoltution.Projects) {
        project.Dependencies.Remove(systemsProject.Guid);
        project.Dependencies.Add(systemsProject.Guid);
      }

      // additional projects seem to be alphabetically arranged
      foreach (var project in quantumSolution.Projects.OrderBy(x => x.Name)) {
        var configurations = project.Configurations
          .Where(x => unitySoltution.Configurations.ContainsKey(x.Key))
          .ToDictionary(x => x.Key, x => x.Value);

        unitySoltution.Projects.Add(new Project() {
          Guid = project.Guid,
          Name = project.Name,
          TypeGuid = project.TypeGuid,
          NestedProjects = project.NestedProjects,
          Path = project.IsFolder ? project.Name : Path.Combine(quantumSolutionDir, project.Path),
          Configurations = configurations
        });
      }

      // add and nest quantum_code folder
      var quantumFolder = new Project() {
        Guid = QuantumFolderGuid,
        TypeGuid = SolutionFolderTypeGuid,
        Name = "quantum_code",
        Path = "quantum_code",
      };
      unitySoltution.Projects.Add(quantumFolder);

      var projectsBeingNested = quantumSolution.Projects.SelectMany(x => x.NestedProjects).ToLookup(x => x);
      foreach (var rootProjects in quantumSolution.Projects.Where(x => !projectsBeingNested.Contains(x.Guid))) {
        quantumFolder.NestedProjects.Add(rootProjects.Guid);
      }

      using (var writer = new StringWriter()) {
        unitySoltution.Save(writer);
        return writer.ToString();
      }
    }

    private class ProjectsCollection : KeyedCollection<string, Project> {
      protected override string GetKeyForItem(Project item) {
        return item.Guid;
      }
    }

    [DebuggerDisplay("{Guid} {Name}")]
    private class Project {
      public string TypeGuid;
      public string Name;
      public string Path;
      public string Guid;
      public List<string> Unhandled = new List<string>();
      public List<string> NestedProjects = new List<string>();
      public List<string> Dependencies = new List<string>();
      public Dictionary<string, Dictionary<string, string>> Configurations = new Dictionary<string, Dictionary<string, string>>();
      public bool IsFolder => TypeGuid == SolutionFolderTypeGuid;
    }

    private class Solution {
      public string Header;

      public readonly ProjectsCollection Projects = new ProjectsCollection();
      public readonly List<string> UnhandledGlobalSections = new List<string>();
      public readonly Dictionary<string, string> Configurations = new Dictionary<string, string>();

      public void RemoveProject(string guid) {
        if (Projects.Remove(guid)) {
          foreach (var p in Projects) {
            p.NestedProjects.Remove(guid);
          }
        }
      }

      public void Save(TextWriter writer) {
        writer.WriteLine(Header);
        foreach (var project in Projects) {
          writer.WriteLine($@"Project(""{project.TypeGuid}"") = ""{project.Name}"", ""{project.Path}"", ""{project.Guid}""");
          foreach (var line in project.Unhandled) {
            writer.WriteLine(line);
          }
          if (project.Dependencies.Any()) {
            writer.WriteLine("\tProjectSection(ProjectDependencies) = postProject");
            foreach (var dep in project.Dependencies.OrderBy(x => x)) {
              writer.WriteLine($"\t\t{dep} = {dep}");
            }
            writer.WriteLine("\tEndProjectSection");
          }
          writer.WriteLine("EndProject");
        }

        writer.WriteLine("Global");
        foreach (var line in UnhandledGlobalSections) {
          writer.WriteLine(line);
        }


        // configuaration
        writer.WriteLine("\tGlobalSection(SolutionConfigurationPlatforms) = preSolution");
        foreach (var kv in Configurations) {
          writer.WriteLine($"\t\t{kv.Key} = {kv.Value}");
        }
        writer.WriteLine("\tEndGlobalSection");


        // configuaration
        writer.WriteLine("\tGlobalSection(ProjectConfigurationPlatforms) = postSolution");
        foreach (var project in Projects) {
          foreach (var kv in project.Configurations) {
            foreach (var prop in kv.Value) {
              writer.WriteLine($"\t\t{project.Guid}.{kv.Key}.{prop.Key} = {prop.Value}");
            }
          }
        }
        writer.WriteLine("\tEndGlobalSection");


        // add nested section
        var nestedInfo = Projects.SelectMany(parent => parent.NestedProjects.Select(child => new { parent, child })).ToList();
        if (nestedInfo.Any()) {
          writer.WriteLine("\tGlobalSection(NestedProjects) = preSolution");
          foreach (var entry in nestedInfo.OrderBy(x => x.parent.Guid)) {
            writer.WriteLine($"\t\t{entry.child} = {entry.parent.Guid}");
          }
          writer.WriteLine("\tEndGlobalSection");
        }

        writer.WriteLine("EndGlobal");
      }

      public static Solution Parse(string contents) {
        var lines = contents.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);

        const string ProjectHeader = "Project";
        const string GlobalHeader = "Global";

        var result = new Solution();

        // get header
        int i = 0;
        {
          StringBuilder builder = new StringBuilder();
          for (; i < lines.Length; ++i) {
            var line = lines[i];
            if (line.StartsWith(GlobalHeader) || line.StartsWith(ProjectHeader)) {
              break;
            }
            builder.AppendLine(line);
          }
          result.Header = builder.ToString();
        }

        // get projects
        {
          var regex = new Regex(@"Project\(\""(.*)\""\) = \""(.*)\"", \""(.*)\"", \""(.*)\""", RegexOptions.Compiled);
          for (; i < lines.Length; ++i) {
            var match = regex.Match(lines[i]);
            if (!match.Success)
              break;

            var project = new Project() {
              TypeGuid = match.Groups[1].Value,
              Name = match.Groups[2].Value,
              Path = match.Groups[3].Value,
              Guid = match.Groups[4].Value,
            };

            while (lines[++i] != "EndProject") {
              if (lines[i].TrimStart().StartsWith("ProjectSection(ProjectDependencies) = postProject")) {
                // nested projects
                var dependencyRegex = new Regex(@"(\{.*) = (.*)");
                while (!lines[++i].TrimStart().StartsWith("EndProjectSection")) {
                  var dependencyMatch = dependencyRegex.Match(lines[i]);
                  if (!dependencyMatch.Success)
                    throw new InvalidOperationException($"Unexpected line: {lines[i]} (at {i})");
                  Debug.Assert(dependencyMatch.Groups[1].Value == dependencyMatch.Groups[2].Value);
                  project.Dependencies.Add(dependencyMatch.Groups[1].Value);
                }
              } else {
                project.Unhandled.Add(lines[i]);
              }
            }

            result.Projects.Add(project);
          }
        }

        // get globals
        {
          if (lines[i++] != GlobalHeader)
            throw new InvalidOperationException();

          for (; i < lines.Length; ++i) {
            if (lines[i] == "EndGlobal") {
              break;
            }
            if (lines[i].TrimStart().StartsWith("GlobalSection(SolutionConfigurationPlatforms) = preSolution")) {
              // solution configurations
              var r = new Regex(@"\s*(.*) = (.*)");
              while (!lines[++i].TrimStart().StartsWith("EndGlobalSection")) {
                var match = r.Match(lines[i]);
                if (!match.Success)
                  throw new InvalidOperationException($"Unexpected line: {lines[i]} (at {i})");
                result.Configurations.Add(match.Groups[1].Value, match.Groups[2].Value);
              }
            } else if (lines[i].TrimStart().StartsWith("GlobalSection(NestedProjects) = preSolution")) {
              // nested projects
              var r = new Regex(@"(\{.*) = (.*)");
              while (!lines[++i].TrimStart().StartsWith("EndGlobalSection")) {
                var match = r.Match(lines[i]);
                if (!match.Success)
                  throw new InvalidOperationException($"Unexpected line: {lines[i]} (at {i})");
                var parent = result.Projects[match.Groups[2].Value];
                var child = result.Projects[match.Groups[1].Value];
                parent.NestedProjects.Add(child.Guid);
              }
            } else if (lines[i].TrimStart().StartsWith("GlobalSection(ProjectConfigurationPlatforms) = postSolution")) {
              // nested projects
              var r = new Regex(@"(\{.*\})\.(.*?)\.(.*) = (.*)");
              while (!lines[++i].TrimStart().StartsWith("EndGlobalSection")) {
                var match = r.Match(lines[i]);
                if (!match.Success)
                  throw new InvalidOperationException($"Unexpected line: {lines[i]} (at {i})");

                var project = result.Projects[match.Groups[1].Value];

                Dictionary<string, string> conf;
                if (!project.Configurations.TryGetValue(match.Groups[2].Value, out conf)) {
                  conf = new Dictionary<string, string>();
                  project.Configurations.Add(match.Groups[2].Value, conf);
                }

                conf.Add(match.Groups[3].Value, match.Groups[4].Value);
              }
            } else {
              result.UnhandledGlobalSections.Add(lines[i]);
            }
          }
        }

        return result;
      }
    }
  }
}
#endregion