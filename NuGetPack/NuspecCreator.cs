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

                var startInfo = new ProcessStartInfo
                {
                    UseShellExecute = false,
                    FileName = nuGetExe,
                    Arguments = "Pack -NoDefaultExcludes \"" + nuspecPath + "\""
                };

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
            const string nugetExtension = ".nuget";

            var packageName = Path.GetFileNameWithoutExtension(assemblyPath);
            if (packageName.ToLower().EndsWith(nugetExtension))
                packageName = packageName.Substring(0, packageName.Length - nugetExtension.Length);

            var fileVersion = FileVersionInfo.GetVersionInfo(assemblyPath);
            var version = fileVersion.FileMajorPart + "." + fileVersion.FileMinorPart + "." + fileVersion.FileBuildPart;
            
            if (fileVersion.FilePrivatePart != 0)
                version += "." + fileVersion.FilePrivatePart;

            var fileVersionParts = fileVersion.ProductVersion.Split(new[] { '-' }, StringSplitOptions.None);
            if (fileVersionParts.Length > 1)
            {
                var preReleaseName = string.Join("-", fileVersionParts.Skip(1));

                if (string.IsNullOrWhiteSpace(preReleaseName))
                    preReleaseName = "PreRelease";

                version += "-" + preReleaseName;
            }

            var owners = fileVersion.CompanyName;

            var shortSummary = fileVersion.Comments;
            var longDescription = fileVersion.Comments;
            var copyright = fileVersion.LegalCopyright;
            var releaseNotes = fileVersion.SpecialBuild;
            var keywords = fileVersion.FileDescription;
            var licenseUrl = fileVersion.LegalTrademarks;
            var projectUrl = fileVersion.LegalTrademarks;

            var folder = Path.GetDirectoryName(projectPath);
            var packagesFile = Path.Combine(folder, @"packages.config");
            var internalPackagesFile = Path.Combine(folder, @"internalPackages.config");
            var configFile = Path.Combine(folder, @"NuGetPack.config");

            var nuspecPath = Path.ChangeExtension(assemblyPath, ".nuspec");


            string iconUrl = @"https://nuget.org/Content/Images/packageDefaultIcon-50x50.png";
            bool doAddFrameworkReferences = false;
            bool doIncludeSources = true;
            string authors = owners;

            if (File.Exists(configFile))
            {
                var deserializer = new System.Xml.Serialization.XmlSerializer(typeof(NuGetPackConfig));
                using (var stream = new FileStream(configFile, FileMode.Open, FileAccess.Read))
                {
                    var config = deserializer.Deserialize(stream) as NuGetPackConfig;

                    if (config != null)
                    {
                        doAddFrameworkReferences = config.AddFrameworkReferences;

                        doIncludeSources = config.DoIncludeSources;

                        if (!string.IsNullOrEmpty(config.Authors))
                            authors = config.Authors;

                        if (!string.IsNullOrEmpty(config.Copyright))
                            copyright = config.Copyright;

                        if (!string.IsNullOrEmpty(config.Description))
                            longDescription = config.Description;

                        if (!string.IsNullOrEmpty(config.IconUrl))
                            iconUrl = config.IconUrl;

                        if (!string.IsNullOrEmpty(config.Keywords))
                            keywords = config.Keywords;

                        if (!string.IsNullOrEmpty(config.LicenseUrl))
                            licenseUrl = config.LicenseUrl;

                        if (!string.IsNullOrEmpty(config.Owners))
                            owners = config.Owners;

                        if (!string.IsNullOrEmpty(config.ProjectUrl))
                            projectUrl = config.ProjectUrl;

                        if (!string.IsNullOrEmpty(config.Summary))
                            shortSummary = config.Summary;
                    }
                }
            }

            var doc = CreateNuspec(
                packageName, version, owners, authors, shortSummary,
                longDescription, releaseNotes, licenseUrl, projectUrl, iconUrl, copyright, keywords, nuspecPath, projectPath,
                packagesFile, internalPackagesFile, isDebug, doAddFrameworkReferences, doIncludeSources);

            return doc;
        }

        public XDocument CreateNuspec(
            string packageName,
            string version,
            string owners,
            string authors,
            string shortSummary,
            string longDescription,
            string releaseNotes,
            string licenseUrl,
            string projectUrl,
            string iconUrl,
            string copyright,
            string keywords,
            string nuspecPath,
            string projectPath,
            string packagesFile,
            string internalPackagesFile,
            bool isDebug,
            bool doAddFrameworkReferences,
            bool doIncludeSources)
        {
            var nuspecFolder = Path.GetDirectoryName(nuspecPath);

            XAttribute dependenciesAttribute;
            var dependenciesInfo = GetDependencies(projectPath, new[] { packagesFile, internalPackagesFile }, out dependenciesAttribute);
            var elements = GetElements(nuspecFolder, projectPath, isDebug, doIncludeSources);

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
                el.ElementType != ElementType.NuGetDependency
                && el.ElementType != ElementType.FrameworkReference)
                .Select(el => el.Element).ToList();

            var metadataElement = new XElement("metadata",
                        new XElement("id", packageName),
                        new XElement("version", version),
                        new XElement("title", packageName),
                        new XElement("authors", authors),
                        new XElement("owners", owners),
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
                        new XElement("tags", keywords));

            if (doAddFrameworkReferences)
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

        public IEnumerable<DependencyInfo> GetDependencies(
            string projectPath, string[] packagesFiles, out XAttribute dependenciesAttribute)
        {
            var targetFramework =  "net" + (GetFrameworkVersion(projectPath) ?? "45");

            dependenciesAttribute = new XAttribute("targetFramework", targetFramework);

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

        public IEnumerable<DependencyInfo> GetElements(string nuspecFolder, string projectPath, bool isDebug, bool doIncludeSources)
        {
            DebugOut(() => string.Format("\r\n\r\nGetFiles({0}, {1}, {2})\r\n", nuspecFolder, projectPath, isDebug));

            var nuspecToProject = Path.GetDirectoryName(
                AbsolutePathToRelativePath(Path.GetDirectoryName(projectPath), nuspecFolder + "\\"));

            var projectFolder = Path.GetDirectoryName(projectPath);

            DebugOut(() => "nuspecToProject = " + nuspecToProject);
            DebugOut(() => "ProjectFolder = " + projectFolder);

            var result = new List<DependencyInfo>();

            result.AddRange(GetContentFiles(nuspecFolder, projectFolder, projectPath));
            result.AddRange(GetBinaryFiles(nuspecFolder, projectFolder, projectPath));
            result.AddRange(GetContentFiles(nuspecFolder, projectFolder, projectPath,
                srcFolder: @"sln\", destFolder: @"sln\", flattern: false, elementType: ElementType.SolutionItemsFile));
            result.AddRange(GetContentFiles(nuspecFolder, projectFolder, projectPath,
                srcFolder: @"tools\", destFolder: @"tools\", flattern: false, elementType: ElementType.ToolsFile));
            result.AddRange(GetContentFiles(nuspecFolder, projectFolder, projectPath,
                srcFolder: @"build\", destFolder: @"build\", flattern: false, elementType: ElementType.BuildFile));

            var references = GetReferences(nuspecFolder, projectPath, projectFolder);

            foreach (var reference in references)
            {
                var refFolder = Path.GetDirectoryName(reference);

                DebugOut(() => "refFolder = " + refFolder);

                var refProjFolder = Path.Combine(projectFolder, refFolder);
                var refProjPath = Path.Combine(projectFolder, reference);

                DebugOut(() => "projFolder = " + refProjFolder);
                DebugOut(() => "projPath = " + refProjPath);

                result.AddRange(GetBinaryReferences(nuspecFolder, refProjFolder, refProjPath, isDebug, nuspecFolder));
                
                if (doIncludeSources)
                    result.AddRange(GetSourceFiles(nuspecFolder, refProjFolder, refProjPath));
                
                result.AddRange(GetDependenciesFromProject(refProjFolder, refProjPath));
            }

            return result;
        }

        #region NuGet Project Parsing

        public IEnumerable<DependencyInfo> GetContentFiles(
            string nuspecFolder, string projectFolder, string projectPath,
            string srcFolder = @"content\", string destFolder = @"content\",
            bool flattern = false, ElementType elementType = ElementType.ContentFile)
        {
            DebugOut(() => string.Format("\r\n\r\nGetContentFiles({0}, {1}, {2})\r\n", nuspecFolder, projectFolder, projectPath));

            var csProj = XDocument.Load(projectPath);

            var xmlns = csProj.Root.GetDefaultNamespace();

            srcFolder = srcFolder.ToLower();

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
                .Where(st => st.target.ToLower().StartsWith(srcFolder) && st.target.ToLower() != srcFolder);

            var relativeProjectFolder = AbsolutePathToRelativePath(projectFolder, nuspecFolder + "\\");

            List<DependencyInfo> items;

            if (!flattern)
            {
                items = contentElements
                    .Select(s =>
                        new DependencyInfo(
                            elementType,
                            new XElement("file",
                                new XAttribute("src", Path.Combine(relativeProjectFolder, s.src)),
                                new XAttribute("target", s.target))))
                    .ToList();
            }
            else
            {
                items = contentElements
                    .Select(s =>
                        new DependencyInfo(
                            elementType,
                            new XElement("file",
                                new XAttribute("src", Path.Combine(relativeProjectFolder, s.src)),
                                new XAttribute("target", Path.Combine(destFolder, Path.GetFileName(s.target))))))
                .ToList();
            }

            return items;
        }

        public string GetFrameworkVersion(string projectPath)
        {
            var csProj = XDocument.Load(projectPath);

            var xmlns = csProj.Root.GetDefaultNamespace();

            var targetFrameworkVersion = csProj.Element(xmlns + "Project")
                .Elements(xmlns + "PropertyGroup").Elements(xmlns + "TargetFrameworkVersion")
                .Select(el => el.Value)
                .FirstOrDefault();

            if (string.IsNullOrEmpty(targetFrameworkVersion))
                return null;

            var result = targetFrameworkVersion.Replace("v", string.Empty).Replace(".", string.Empty);
            return result;
        }

        public IEnumerable<DependencyInfo> GetBinaryFiles(string nuspecFolder, string projectFolder, string projectPath)
        {
            var defaultVersionFolder = "net" + (GetFrameworkVersion(projectPath) ?? "45");

            var items = GetContentFiles(nuspecFolder, projectFolder, projectPath,
                srcFolder: @"lib\", destFolder: @"lib\",
                flattern: false, elementType: ElementType.LibraryFile);

            var itemsList = items.ToList();

            foreach (var item in itemsList)
            {
                var target = item.Element.Attribute("target").Value;

                if (target.StartsWith(@"lib\") && !target.StartsWith(@"lib\net"))
                {
                    item.Element.Attribute("target").Value = @"lib\" + defaultVersionFolder + target.Substring(3);
                }
            }

            return itemsList;
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

            // E.g. <frameworkAssembly assemblyName="System.Speech" targetFramework=".NETFramework4.5" />

            var items = referencedBinaryFiles
                .Select(el =>
                    new DependencyInfo(
                        ElementType.FrameworkReference,
                        new XElement("frameworkAssembly",
                            new XAttribute("assemblyName", el))))
                .ToList();

            return items;
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

        #endregion

        #region Referenced Projects Parsing

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

        public IEnumerable<DependencyInfo> GetBinaryReferences(
            string nuspecFolder, string projectFolder, string projectPath, bool isDebug, string buildMachineBinFolder)
        {
            DebugOut(() => string.Format("\r\n\r\nGetBinaryReferences({0}, {1}, {2}, {3})\r\n", projectFolder, projectPath, isDebug, buildMachineBinFolder));

            var versionFolder = "net" + (GetFrameworkVersion(projectPath) ?? "45");

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
                            new XAttribute("target", Path.Combine(@"lib\" + versionFolder + @"\", Path.GetFileName(file)))
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

        #endregion
    }
}
