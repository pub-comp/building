using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Xml.Linq;
using System.Xml.XPath;

namespace PubComp.Building.NuGetPack
{
    public partial class NuspecCreator
    {
        public void CreatePackage(string projectPath, string assemblyPath, bool isDebug)
        {
            Console.WriteLine("Creating nuspec file");

            var doc = CreateNuspec(projectPath, assemblyPath, isDebug);
            var nuspecPath = Path.ChangeExtension(assemblyPath, ".nuspec");

            DebugOut(() => "nuspecPath = " + nuspecPath);

            DebugOut(() => "Saving");

            Console.WriteLine("Saving nuspec file");

            doc.Save(nuspecPath);

            DebugOut(() => "CreatingPackage");

            CreatePackage(nuspecPath);
        }

        public void CreatePackage(string nuspecPath)
        {
            Console.WriteLine("Packing Package");

            var nuGetExe = Path.Combine(Path.GetDirectoryName(this.GetType().Assembly.Location), "NuGet.exe");
            DebugOut(() => "nuGetExe = " + nuGetExe);

            var prevDir = Environment.CurrentDirectory;

            try
            {
                Environment.CurrentDirectory = Path.GetDirectoryName(nuspecPath);

                DebugOut(() => "CurrentDirectory = " + Environment.CurrentDirectory);

                DebugOut(() => nuGetExe + " Pack " + nuspecPath);

                var startInfo = new ProcessStartInfo();
                startInfo.UseShellExecute = false;
                startInfo.FileName = nuGetExe;
                startInfo.Arguments = "Pack " + nuspecPath;

                if (!startInfo.EnvironmentVariables.ContainsKey("EnableNuGetPackageRestore"))
                    startInfo.EnvironmentVariables.Add("EnableNuGetPackageRestore", "true");
                else
                    startInfo.EnvironmentVariables["EnableNuGetPackageRestore"] = "true";

                startInfo.RedirectStandardOutput = true;

                var process = Process.Start(startInfo);

                process.WaitForExit();

                var output = process.StandardOutput.ReadToEnd();
                Console.WriteLine();
                Console.WriteLine(output);
                Console.WriteLine();
            }
            finally
            {
                Environment.CurrentDirectory = prevDir;
            }
        }

        public void PublishPackage(string packagePath)
        {
            Console.WriteLine("Publishing Package");

            var nuGetExe = Path.Combine(Path.GetDirectoryName(this.GetType().Assembly.Location), "NuGet.exe");

            var prevDir = Environment.CurrentDirectory;

            try
            {
                Environment.CurrentDirectory = Path.GetDirectoryName(packagePath);

                var process = Process.Start(nuGetExe, "Push " + packagePath);

                process.WaitForExit();
            }
            finally
            {
                Environment.CurrentDirectory = prevDir;
            }
        }

        public XDocument CreateNuspec(string projectPath, string assemblyPath, bool isDebug)
        {
            const string nugetExtension = ".NuGet";

            var packageName = Path.GetFileNameWithoutExtension(assemblyPath);
            if (packageName.EndsWith(nugetExtension))
                packageName = packageName.Substring(0, packageName.Length - nugetExtension.Length);

            var fileVersion = FileVersionInfo.GetVersionInfo(assemblyPath);
            var version = fileVersion.FileMajorPart + "." + fileVersion.FileMinorPart + "." + fileVersion.FileBuildPart;
            if (fileVersion.ProductVersion.Contains("-"))
                version += "-" + "PreRelease";

            var owner = fileVersion.CompanyName;

            var shortSummary = fileVersion.Comments;
            var longDescription = fileVersion.Comments;
            var copyright = fileVersion.LegalCopyright;
            var releaseNotes = fileVersion.SpecialBuild;
            var tags = fileVersion.FileDescription;
            var licenseUrl = fileVersion.LegalTrademarks;
            var projectUrl = fileVersion.LegalTrademarks;

            var folder = Path.GetDirectoryName(projectPath);
            var packagesFile = Path.Combine(folder, @"packages.config");
            var internalPackagesFile = Path.Combine(folder, @"internalPackages.config");

            var nuspecPath = Path.ChangeExtension(assemblyPath, ".nuspec");

            var doc = CreateNuspec(
                packageName, version, owner, shortSummary,
                longDescription, releaseNotes, null, licenseUrl, projectUrl, copyright, tags, nuspecPath, projectPath,
                packagesFile, internalPackagesFile, isDebug);

            return doc;
        }

        public XDocument CreateNuspec(
            string packageName,
            string version,
            string owner,
            string shortSummary,
            string longDescription,
            string releaseNotes,
            string iconUrl,
            string licenseUrl,
            string projectUrl,
            string copyright,
            string tags,
            string nuspecPath,
            string projectPath,
            string packagesFile,
            string internalPackagesFile,
            bool isDebug)
        {
            var nupsecFolder = Path.GetDirectoryName(nuspecPath);

            if (string.IsNullOrEmpty(iconUrl))
                iconUrl = @"https://nuget.org/Content/Images/packageDefaultIcon-50x50.png";

            var dependencies = GetDependencies(new[] { packagesFile, internalPackagesFile });
            var files = GetFiles(nupsecFolder, projectPath, isDebug);

            var doc = new XDocument(
                new XElement("package",
                    new XElement("metadata",
                        new XElement("id", packageName),
                        new XElement("version", version),
                        new XElement("title", packageName),
                        new XElement("authors", owner),
                        new XElement("owners", owner),
                        new XElement("description", longDescription),
                        new XElement("releaseNotes", releaseNotes),
                        new XElement("summary", shortSummary),
                        new XElement("language", "en-US"),
                        new XElement("projectUrl", projectUrl),
                        new XElement("iconUrl", iconUrl),
                        new XElement("requireLicenseAcceptance", false),
                        new XElement("licenseUrl", licenseUrl),
                        new XElement("copyright", copyright),
                        new XElement("dependencies", new XElement("group", dependencies)),
                        new XElement("references", string.Empty),
                        new XElement("tags", tags)
                        ),
                    new XElement("files", files)
                )
            );

            return doc;
        }

        public IEnumerable<XObject> GetDependencies(string[] packagesFiles)
        {
            var result = new List<XObject>
            {
                new XAttribute("targetFramework", "net45")
            };

            foreach (var packagesFile in packagesFiles)
                result.AddRange(GetDependencies(packagesFile));

            return result;
        }

        public IEnumerable<XObject> GetDependencies(string packagesFile)
        {
            var result = new List<XObject>();

            if (!File.Exists(packagesFile))
                return result;

            var packagesConfig = XDocument.Load(packagesFile);

            var packages = packagesConfig.XPathSelectElements("packages/package").ToList();

            foreach (var package in packages)
            {
                result.Add(new XElement("dependency",
                    new XAttribute("id", package.Attribute("id").Value),
                    new XAttribute("version", package.Attribute("version").Value)));
            }

            return result;
        }

        public static string AbsolutePathToRelativePath(string filePath, string referencePath)
        {
            if (!Path.IsPathRooted(filePath))
                return filePath;

            var absolutePath = Path.GetFullPath(filePath);

            var fileUri = new Uri(absolutePath);
            var referenceUri = new Uri(referencePath);
            return referenceUri.MakeRelativeUri(fileUri).ToString().Replace('/', '\\');
        }

        partial void DebugOut(Func<string> getText);

#if DEBUG
        partial void DebugOut(Func<string> getText)
        {
            Console.WriteLine(getText());
        }
#endif

        public IEnumerable<XElement> GetFiles(string nuspecFolder, string projectPath, bool isDebug)
        {
            DebugOut(() => string.Format("\r\n\r\nGetFiles({0}, {1}, {2})\r\n", nuspecFolder, projectPath, isDebug));

            var nuspecToProject = Path.GetDirectoryName(
                AbsolutePathToRelativePath(Path.GetDirectoryName(projectPath), nuspecFolder + "\\"));

            var projectFolder = Path.GetDirectoryName(projectPath);

            DebugOut(() => "nuspecToProject = " + nuspecToProject);
            DebugOut(() => "ProjectFolder = " + projectFolder);

            var references = GetReferences(nuspecFolder, projectPath, projectFolder);

            var result = new List<XElement>();

            foreach (var reference in references)
            {
                var refFolder = Path.GetDirectoryName(reference);

                DebugOut(() => "refFolder = " + refFolder);

                var projFolder = Path.Combine(projectFolder, refFolder);
                var projPath = Path.Combine(projectFolder, reference);

                DebugOut(() => "projFolder = " + projFolder);
                DebugOut(() => "projPath = " + projPath);

                result.AddRange(GetBinaryFiles(nuspecFolder, projFolder, projPath, isDebug, nuspecFolder));
                result.AddRange(GetSourceFiles(nuspecFolder, projFolder, projPath));
            }

            return result;
        }

        public IEnumerable<string> GetReferences(string nuspecFolder, string projectPath, string projectFolder)
        {
            var csProj = XDocument.Load(projectPath);

            var xmlns = csProj.Root.GetDefaultNamespace();
            var elements = csProj.Element(xmlns + "Project").Elements(xmlns + "ItemGroup").Elements(xmlns + "ProjectReference");

            var references = elements
                .Select(r => r.Attribute("Include").Value)
                .ToList();

            return references;
        }

        public IEnumerable<XElement> GetBinaryFiles(string nuspecFolder, string projectFolder, string projectPath, bool isDebug, string buildMachineBinFolder)
        {
            DebugOut(() => string.Format("\r\n\r\nGetBinaryFiles({0}, {1}, {2}, {3})\r\n", projectFolder, projectPath, isDebug, buildMachineBinFolder));

            string outputPath;

            var csProj = XDocument.Load(projectPath);
            var projectName = Path.GetFileNameWithoutExtension(projectPath);

            var xmlns = csProj.Root.GetDefaultNamespace();
            var proj = csProj.Element(xmlns + "Project");
            var propGroups = proj.Elements(xmlns + "PropertyGroup");

            var config = (isDebug ? "Debug|AnyCPU" : "Release|AnyCPU");

            var outputPathElement = propGroups
                .Where(el => el.Attribute("Condition") != null
                    && el.Attribute("Condition").Value.Contains(config))
                .Elements(xmlns + "OutputPath").FirstOrDefault();

            outputPath = Path.Combine(projectFolder, outputPathElement.Value);

            if (!Directory.Exists(outputPath))
                outputPath = buildMachineBinFolder;

            DebugOut(() => "outputPath = " + outputPath);

            var relativeOutputPath = AbsolutePathToRelativePath(outputPath, nuspecFolder + "\\");

            var assemblyNameElement = propGroups.Elements(xmlns + "AssemblyName").FirstOrDefault();

            var assemblyName = assemblyNameElement.Value;

            var files = Directory.GetFiles(outputPath, assemblyName + ".*");

            var result = files
                .Where(file =>
                    !file.EndsWith(".nuspec") && !file.EndsWith(".nupkg")
                    && IsFileWithDifferentExtension(assemblyName, file))
                .Select(file =>
                new XElement("file",
                    new XAttribute("src", Path.Combine(relativeOutputPath, Path.GetFileName(file))),
                    new XAttribute("target", Path.Combine(@"lib\net45\", Path.GetFileName(file)))
                    )).ToList();

            return result;
        }

        private bool IsFileWithDifferentExtension(string expectedFileNameWithoutExtension, string actualFilePath)
        {
            var actualFileName = Path.GetFileName(actualFilePath);

            if (!actualFileName.StartsWith(expectedFileNameWithoutExtension))
                return false;

            var suffix = actualFileName.Substring(expectedFileNameWithoutExtension.Length);
            if (!suffix.StartsWith("."))
                return false;

            if (suffix.Substring(1).Contains("."))
                return false;

            return true;
        }

        public IEnumerable<XElement> GetSourceFiles(string nuspecFolder, string projectFolder, string projectPath)
        {
            DebugOut(() => string.Format("\r\n\r\nGetSourceFiles({0}, {1})\r\n", projectFolder, projectPath));

            var csProj = XDocument.Load(projectPath);
            var projectName = Path.GetFileNameWithoutExtension(projectPath);

            var xmlns = csProj.Root.GetDefaultNamespace();
            var codeElements = csProj.Element(xmlns + "Project").Elements(xmlns + "ItemGroup").Elements(xmlns + "Compile");
            var noneElements = csProj.Element(xmlns + "Project").Elements(xmlns + "ItemGroup").Elements(xmlns + "None");

            var relativeProjectFolder = AbsolutePathToRelativePath(projectFolder, nuspecFolder + "\\");

            var sources = codeElements.Union(noneElements)
                .Where(el => el.Attribute("Include") != null
                    && !el.Attribute("Include").Value.StartsWith(".."))
                .Select(el => el.Attribute("Include").Value)
                .Select(s =>
                    new XElement("file",
                        new XAttribute("src", Path.Combine(relativeProjectFolder, s)),
                        new XAttribute("target", Path.Combine(@"src\", projectName, s))))
                .ToList();

            return sources;
        }
    }
}
