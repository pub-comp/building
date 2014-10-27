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
                startInfo.Arguments = "Pack -NoDefaultExcludes " + nuspecPath;

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
            var configFile = Path.Combine(folder, @"NuGetPack.config");

            var nuspecPath = Path.ChangeExtension(assemblyPath, ".nuspec");

            var doc = CreateNuspec(
                packageName, version, owner, shortSummary,
                longDescription, releaseNotes, licenseUrl, projectUrl, copyright, tags, nuspecPath, projectPath,
                packagesFile, internalPackagesFile, configFile, isDebug);

            return doc;
        }

        public XDocument CreateNuspec(
            string packageName,
            string version,
            string owner,
            string shortSummary,
            string longDescription,
            string releaseNotes,
            string licenseUrl,
            string projectUrl,
            string copyright,
            string tags,
            string nuspecPath,
            string projectPath,
            string packagesFile,
            string internalPackagesFile,
            string configPath,
            bool isDebug)
        {
            var nuspecFolder = Path.GetDirectoryName(nuspecPath);

            string iconUrl = null;
            bool addFrameworkReferences = false;

            if (File.Exists(configPath))
            {
                var deserializer = new System.Xml.Serialization.XmlSerializer(typeof(NuGetPackConfig));
                using (var stream = new FileStream(configPath, FileMode.Open, FileAccess.Read))
                {
                    var config = deserializer.Deserialize(stream) as NuGetPackConfig;
                    iconUrl = config.IconUrl;
                    addFrameworkReferences = config.AddFrameworkReferences;
                }
            }

            XAttribute dependenciesAttribute;
            var dependenciesInfo = GetDependencies(new[] { packagesFile, internalPackagesFile }, out dependenciesAttribute);
            var elements = GetElements(nuspecFolder, projectPath, isDebug);

            var dependencies = new XElement("group");
            dependencies.Add(dependenciesAttribute);

            var d1 = dependenciesInfo.Where(el => el.ElementType == ElementType.NuGetDependency).Select(el => el.Element).ToList();
            var d2 = elements.Where(el => el.ElementType == ElementType.NuGetDependency).Select(el => el.Element).ToList();

            foreach (var d in d1)
            {
                if (!dependencies.Elements().Any(el => el.ToString() == d.ToString()))
                    dependencies.Add(d);
            }

            foreach (var d in d2)
            {
                if (!dependencies.Elements().Any(el => el.ToString() == d.ToString()))
                    dependencies.Add(d);
            }

            var files = elements.Where(el =>
                el.ElementType != ElementType.AssemblyReference
                && el.ElementType != ElementType.NuGetDependency
                && el.ElementType != ElementType.FrameworkReference)
                .Select(el => el.Element).ToList();
            
            iconUrl = !string.IsNullOrEmpty(iconUrl) ?
                iconUrl
                : @"https://nuget.org/Content/Images/packageDefaultIcon-50x50.png";

            var metadataElement = new XElement("metadata",
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
                        new XElement("dependencies", dependencies),
                        new XElement("references", string.Empty),
                        new XElement("tags", tags));

            if (addFrameworkReferences)
            {
                var frameworkReferences = GetFrameworkReferences(Path.GetDirectoryName(projectPath), projectPath)
                    .Select(el => el.Element)
                    .ToList();

                metadataElement.Add(new XElement("frameworkAssemblies",
                    frameworkReferences));
            }

            var doc = new XDocument(
                new XElement("package",
                    metadataElement,
                    new XElement("files", files)
                )
            );

            return doc;
        }

        public IEnumerable<DependencyInfo> GetDependencies(string[] packagesFiles, out XAttribute dependenciesAttribute)
        {
            dependenciesAttribute = new XAttribute("targetFramework", "net45");

            var result = new List<DependencyInfo>();

            foreach (var packagesFile in packagesFiles)
                result.AddRange(GetDependencies(packagesFile));

            return result;
        }

        public IEnumerable<DependencyInfo> GetDependencies(string packagesFile)
        {
            var result = new List<DependencyInfo>();

            if (!File.Exists(packagesFile))
                return result;

            var packagesConfig = XDocument.Load(packagesFile);

            var packages = packagesConfig.XPathSelectElements("packages/package").ToList();

            foreach (var package in packages)
            {
                result.Add(
                    new DependencyInfo(
                        ElementType.NuGetDependency,
                        new XElement("dependency",
                        new XAttribute("id", package.Attribute("id").Value),
                        new XAttribute("version", package.Attribute("version").Value))));
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

        public IEnumerable<DependencyInfo> GetElements(string nuspecFolder, string projectPath, bool isDebug)
        {
            DebugOut(() => string.Format("\r\n\r\nGetFiles({0}, {1}, {2})\r\n", nuspecFolder, projectPath, isDebug));

            var nuspecToProject = Path.GetDirectoryName(
                AbsolutePathToRelativePath(Path.GetDirectoryName(projectPath), nuspecFolder + "\\"));

            var projectFolder = Path.GetDirectoryName(projectPath);

            DebugOut(() => "nuspecToProject = " + nuspecToProject);
            DebugOut(() => "ProjectFolder = " + projectFolder);

            var result = new List<DependencyInfo>();

            result.AddRange(GetContentFiles(nuspecFolder, projectFolder, projectPath));

            var references = GetReferences(nuspecFolder, projectPath, projectFolder);

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
                result.AddRange(GetDependenciesFromProject(projFolder, projPath));
            }

            return result;
        }

        public IEnumerable<DependencyInfo> GetContentFiles(string nuspecFolder, string projectFolder, string projectPath)
        {
            DebugOut(() => string.Format("\r\n\r\nGetContentFiles({0}, {1})\r\n", projectFolder, projectPath));

            var csProj = XDocument.Load(projectPath);

            var xmlns = csProj.Root.GetDefaultNamespace();
            var contentElements = csProj.Element(xmlns + "Project").Elements(xmlns + "ItemGroup").Elements()
                .Where(el => el.Attribute("Include") != null)
                .Select(el =>
                    new
                    {
                        src = el.Attribute("Include").Value,
                        target = el.Elements(xmlns + "Link").Any()
                            ? el.Elements(xmlns + "Link").First().Value
                            : el.Attribute("Include").Value
                    })
                .Where(st => st.target.ToLower().StartsWith(@"content\"));

            var relativeProjectFolder = AbsolutePathToRelativePath(projectFolder, nuspecFolder + "\\");

            var items = contentElements
                .Select(s =>
                    new DependencyInfo(
                        ElementType.ContentFile,
                        new XElement("file",
                            new XAttribute("src", Path.Combine(relativeProjectFolder, s.src)),
                            new XAttribute("target", Path.Combine(s.target)))))
                .ToList();

            return items;
        }

        public string GetIcon(string nuspecFolder, string projectFolder, string projectPath)
        {
            var csProj = XDocument.Load(projectPath);

            var xmlns = csProj.Root.GetDefaultNamespace();
            var iconElements = csProj.Element(xmlns + "Project").Elements(xmlns + "PropertyGroup").Elements(xmlns + "ApplicationIcon");

            if (!iconElements.Any())
                return null;

            var iconPath = iconElements.Select(i => i.Value).First();

            if (new Uri(iconPath, UriKind.RelativeOrAbsolute).IsAbsoluteUri == false)
            {
                iconPath = AbsolutePathToRelativePath(iconPath, nuspecFolder + "\\");
            }

            return iconPath;
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

        private string GetOutputPath(XDocument csProj, bool isDebug, string projectFolder)
        {
            var xmlns = csProj.Root.GetDefaultNamespace();
            var proj = csProj.Element(xmlns + "Project");
            var propGroups = proj.Elements(xmlns + "PropertyGroup");

            var config = (isDebug ? "Debug|AnyCPU" : "Release|AnyCPU");

            var outputPathElement = propGroups
                .Where(el => el.Attribute("Condition") != null
                    && el.Attribute("Condition").Value.Contains(config))
                .Elements(xmlns + "OutputPath").FirstOrDefault();

            var outputPath = Path.Combine(projectFolder, outputPathElement.Value);
            return outputPath;
        }

        public IEnumerable<DependencyInfo> GetBinaryFiles(
            string nuspecFolder, string projectFolder, string projectPath, bool isDebug, string buildMachineBinFolder)
        {
            DebugOut(() => string.Format("\r\n\r\nGetBinaryFiles({0}, {1}, {2}, {3})\r\n", projectFolder, projectPath, isDebug, buildMachineBinFolder));

            string outputPath;

            var csProj = XDocument.Load(projectPath);

            var xmlns = csProj.Root.GetDefaultNamespace();
            var proj = csProj.Element(xmlns + "Project");
            var propGroups = proj.Elements(xmlns + "PropertyGroup");

            outputPath = GetOutputPath(csProj, isDebug, projectFolder);

            if (!Directory.Exists(outputPath))
                outputPath = buildMachineBinFolder;

            DebugOut(() => "outputPath = " + outputPath);

            var relativeOutputPath = AbsolutePathToRelativePath(outputPath, nuspecFolder + "\\");

            var assemblyNameElement = propGroups.Elements(xmlns + "AssemblyName").FirstOrDefault();

            var assemblyName = assemblyNameElement.Value;

            var files = Directory.GetFiles(outputPath, assemblyName + ".*");

            var items = files
                .Where(file =>
                    !file.EndsWith(".nuspec") && !file.EndsWith(".nupkg")
                    && IsFileWithDifferentExtension(assemblyName, file))
                .Select(file =>
                    new DependencyInfo(
                        ElementType.LibraryFile,
                        new XElement("file",
                            new XAttribute("src", Path.Combine(relativeOutputPath, Path.GetFileName(file))),
                            new XAttribute("target", Path.Combine(@"lib\net45\", Path.GetFileName(file)))
                            ))).ToList();

            return items;
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

        public IEnumerable<DependencyInfo> GetSourceFiles(string nuspecFolder, string projectFolder, string projectPath)
        {
            DebugOut(() => string.Format("\r\n\r\nGetSourceFiles({0}, {1})\r\n", projectFolder, projectPath));

            var csProj = XDocument.Load(projectPath);
            var projectName = Path.GetFileNameWithoutExtension(projectPath);

            var xmlns = csProj.Root.GetDefaultNamespace();
            var codeElements = csProj.Element(xmlns + "Project").Elements(xmlns + "ItemGroup").Elements(xmlns + "Compile");
            var noneElements = csProj.Element(xmlns + "Project").Elements(xmlns + "ItemGroup").Elements(xmlns + "None");

            var relativeProjectFolder = AbsolutePathToRelativePath(projectFolder, nuspecFolder + "\\");

            var items = codeElements.Union(noneElements)
                .Where(el => el.Attribute("Include") != null
                    && !el.Attribute("Include").Value.StartsWith(".."))
                .Select(el => el.Attribute("Include").Value)
                .Select(s =>
                    new DependencyInfo(
                        ElementType.SourceFile,
                        new XElement("file",
                            new XAttribute("src", Path.Combine(relativeProjectFolder, s)),
                            new XAttribute("target", Path.Combine(@"src\", projectName, s)))))
                .ToList();

            return items;
        }

        public IEnumerable<DependencyInfo> GetDependenciesFromProject(string projectFolder, string projectPath)
        {
            DebugOut(() => string.Format("\r\n\r\nGetDependenciesFromProject({0}, {1})\r\n", projectFolder, projectPath));

            var csProj = XDocument.Load(projectPath);
            var projectName = Path.GetFileNameWithoutExtension(projectPath);

            var xmlns = csProj.Root.GetDefaultNamespace();
            var noneElements = csProj.Element(xmlns + "Project").Elements(xmlns + "ItemGroup").Elements(xmlns + "None");

            var packagesFileName = noneElements.Where(el =>
                    el.Attribute("Include") != null && el.Attribute("Include").Value.ToLower() == "packages.config")
                .Select(el => el.Attribute("Include").Value)
                .FirstOrDefault();

            if (packagesFileName == null)
                return new DependencyInfo[0];

            string packagesFile;
            packagesFile = projectFolder + @"\" + packagesFileName;

            DebugOut(() => string.Format("packagesFile: {0}", packagesFile));

            var dependencies = GetDependencies(packagesFile);

            return dependencies;
        }

        public IEnumerable<DependencyInfo> GetFrameworkReferences(string projectFolder, string projectPath)
        {
            DebugOut(() => string.Format("\r\n\r\nGetFrameworkReferences({0}, {1})\r\n", projectFolder, projectPath));

            var csProj = XDocument.Load(projectPath);

            var xmlns = csProj.Root.GetDefaultNamespace();
            var proj = csProj.Element(xmlns + "Project");

            var referencedBinaryFiles = proj.Elements(xmlns + "ItemGroup")
                .SelectMany(el => el.Elements(xmlns + "Reference"))
                .Where(el => el.Attribute("Include") != null && !el.Elements(xmlns + "HintPath").Any())
                .Select(el => el.Attribute("Include").Value)
                .ToList();

            //<frameworkAssembly assemblyName="System.Speech" targetFramework=".NETFramework4.5" />

            var items = referencedBinaryFiles
                .Select(el =>
                    new DependencyInfo(
                        ElementType.FrameworkReference,
                        new XElement("frameworkAssembly",
                            new XAttribute("assemblyName", el))))
                .ToList();

            return items;
        }

        public IEnumerable<DependencyInfo> GetAssemblyReferences(
            string nuspecFolder, string projectFolder, string projectPath, bool isDebug)
        {
            DebugOut(() => string.Format("\r\n\r\nGetAssemblyReferences({0}, {1})\r\n", projectFolder, projectPath));

            var csProj = XDocument.Load(projectPath);
            var projectName = Path.GetFileNameWithoutExtension(projectPath);

            var outputPath = GetOutputPath(csProj, isDebug, projectFolder);

            var xmlns = csProj.Root.GetDefaultNamespace();
            var proj = csProj.Element(xmlns + "Project");
            
            var referencedBinaryFiles = proj.Elements(xmlns + "ItemGroup")
                .SelectMany(el => el.Elements(xmlns + "Reference"))
                .SelectMany(el => el.Elements(xmlns + "HintPath"))
                .Where(el => !el.Value.Contains(@"\packages\"))
                .Select(el => Path.Combine(projectFolder, el.Value))
                .ToList();

            var relativeOutputPath = AbsolutePathToRelativePath(outputPath, nuspecFolder + "\\");

            var items = new List<DependencyInfo>();

            foreach (var file in referencedBinaryFiles)
            {
                items.Add(
                    new DependencyInfo(
                        ElementType.LibraryFile,
                        new XElement("file",
                            new XAttribute("src", Path.Combine(relativeOutputPath, Path.GetFileName(file))),
                            new XAttribute("target", Path.Combine(@"lib\net45\", Path.GetFileName(file)))
                            )));

                var pdbFile = Path.ChangeExtension(file, ".pdb");
                if (File.Exists(pdbFile))
                {
                    items.Add(
                    new DependencyInfo(
                        ElementType.LibraryFile,
                        new XElement("file",
                            new XAttribute("src", Path.Combine(relativeOutputPath, Path.GetFileName(pdbFile))),
                            new XAttribute("target", Path.Combine(@"lib\net45\", Path.GetFileName(pdbFile)))
                            )));
                }

                var xmlFile = Path.ChangeExtension(file, ".pdb");
                if (File.Exists(xmlFile))
                {
                    items.Add(
                    new DependencyInfo(
                        ElementType.LibraryFile,
                        new XElement("file",
                            new XAttribute("src", Path.Combine(relativeOutputPath, Path.GetFileName(xmlFile))),
                            new XAttribute("target", Path.Combine(@"lib\net45\", Path.GetFileName(xmlFile)))
                            )));
                }
            }

            return items;
        }
    }
}
