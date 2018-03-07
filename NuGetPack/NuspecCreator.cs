﻿using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Xml.Linq;
using System.Xml.XPath;

namespace PubComp.Building.NuGetPack
{
    // ReSharper disable once PartialTypeWithSinglePart
    public partial class NuspecCreator
    {
        private string PackageVersion {get; set;}
        public void CreatePackages(
            string binFolder, string solutionFolder, bool isDebug,
            bool doCreatePkg = true, bool doIncludeCurrentProj = false,
            string preReleaseSuffixOverride = null)
        {
            var projects = new LinkedList<string>();
            FindProjects(solutionFolder, projects);

            foreach (var projectPath in projects)
            {
                string assemblyPath;
                GetAssemblyNameAndPath(
                    projectPath, out string assemblyName, isDebug, binFolder, out assemblyPath);

                if (assemblyName == null)
                    continue;

                CreatePackage(
                    projectPath, assemblyPath, isDebug, doCreatePkg, doIncludeCurrentProj,
                    preReleaseSuffixOverride);
            }
        }

        private void FindProjects(string parentFolder, LinkedList<string> projects)
        {
            if (!Directory.Exists(parentFolder))
                return;

            var configFiles = Directory.GetFiles(parentFolder).Where(f =>
                Path.GetFileName(f).ToLower() == "nugetpack.config")
                .ToList();

            if (configFiles.Any())
            {
                var projectFiles = Directory.GetFiles(parentFolder).Where(f =>
                    Path.GetExtension(f) == ".csproj" || Path.GetExtension(f) == ".vbproj")
                    .ToList();

                if (projectFiles.Count == 1)
                {
                    projects.AddLast(projectFiles[0]);
                }
                else if (projectFiles.Count > 1)
                {
                    throw new ApplicationException($"More than one project file found in folder {parentFolder}");
                }
            }

            var subFolders = Directory.GetDirectories(parentFolder);

            foreach (var folder in subFolders)
            {
                FindProjects(folder, projects);
            }
        }

        public void CreatePackage(
            string projectPath, string assemblyPath, bool isDebug,
            bool doCreatePkg = true, bool doIncludeCurrentProj = false,
            string preReleaseSuffixOverride = null)
        {
            Console.WriteLine("Creating nuspec file");

            NuGetPackConfig config;
            var doc = CreateNuspec(
                projectPath, assemblyPath, isDebug, doIncludeCurrentProj, preReleaseSuffixOverride,
                out config);
            var nuspecPath = Path.ChangeExtension(assemblyPath, ".nuspec");

            DebugOut(() => "nuspecPath = " + nuspecPath);

            DebugOut(() => "Saving");

            Console.WriteLine("Saving nuspec file");

            // ReSharper disable once AssignNullToNotNullAttribute
            doc.Save(nuspecPath);

            if (!doCreatePkg)
                return;

            DebugOut(() => "CreatingPackage");

            var doSeparateSymbols = config?.DoSeparateSymbols ?? false;
            CreatePackage(nuspecPath, doSeparateSymbols);
        }

        public void CreatePackage(string nuspecPath, bool doSeparateSymbols)
        {
            if (nuspecPath == null)
                throw new ArgumentNullException(nameof(nuspecPath));

            Console.WriteLine("Packing Package");

            // ReSharper disable once AssignNullToNotNullAttribute
            var nuGetExe = Path.Combine(Path.GetDirectoryName(this.GetType().Assembly.Location), "NuGet.exe");
            DebugOut(() => "nuGetExe = " + nuGetExe);

            var prevDir = Environment.CurrentDirectory;

            try
            {
                // ReSharper disable once AssignNullToNotNullAttribute
                Environment.CurrentDirectory = Path.GetDirectoryName(nuspecPath);

                DebugOut(() => "CurrentDirectory = " + Environment.CurrentDirectory);

                DebugOut(() => nuGetExe + " Pack " + nuspecPath);

                var startInfo = new ProcessStartInfo
                {
                    UseShellExecute = false,
                    FileName = nuGetExe,
                    Arguments = "Pack -NoDefaultExcludes \"" + nuspecPath + "\""
                        + (doSeparateSymbols ? " -Sym" : string.Empty),
                };

                if (!startInfo.EnvironmentVariables.ContainsKey("EnableNuGetPackageRestore"))
                    startInfo.EnvironmentVariables.Add("EnableNuGetPackageRestore", "true");
                else
                    startInfo.EnvironmentVariables["EnableNuGetPackageRestore"] = "true";

                startInfo.RedirectStandardOutput = true;

                var process = Process.Start(startInfo);
                if (process == null)
                    throw new ApplicationException($"Could not start process {nuGetExe}");

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
            if (packagePath == null)
                throw new ArgumentNullException(nameof(packagePath));

            Console.WriteLine("Publishing Package");

            // ReSharper disable once AssignNullToNotNullAttribute
            var nuGetExe = Path.Combine(Path.GetDirectoryName(this.GetType().Assembly.Location), "NuGet.exe");

            var prevDir = Environment.CurrentDirectory;

            try
            {
                // ReSharper disable once AssignNullToNotNullAttribute
                Environment.CurrentDirectory = Path.GetDirectoryName(packagePath);

                var process = Process.Start(nuGetExe, "Push " + packagePath);
                if (process == null)
                    throw new ApplicationException($"Could not start process {nuGetExe}");

                process.WaitForExit();
            }
            finally
            {
                Environment.CurrentDirectory = prevDir;
            }
        }

        public XDocument CreateNuspec(
            string projectPath, string assemblyPath, bool isDebug, bool doIncludeCurrentProj = false,
            string preReleaseSuffixOverride = null)
        {
            return CreateNuspec(
                projectPath, assemblyPath, isDebug, doIncludeCurrentProj, preReleaseSuffixOverride,
                out NuGetPackConfig config);
        }

        public XDocument CreateNuspec(
            string projectPath, string assemblyPath, bool isDebug, bool doIncludeCurrentProj,
            string preReleaseSuffixOverride,
            out NuGetPackConfig config)
        {
            if (projectPath == null)
                throw new ArgumentNullException(nameof(projectPath));

            if (assemblyPath == null)
                throw new ArgumentNullException(nameof(assemblyPath));

            config = null;

            const string nugetExtension = ".nuget";

            var packageName = Path.GetFileNameWithoutExtension(assemblyPath);
            if (packageName.ToLower().EndsWith(nugetExtension))
                packageName = packageName.Substring(0, packageName.Length - nugetExtension.Length);

            var fileVersion = FileVersionInfo.GetVersionInfo(assemblyPath);
            var version = GetVersion(fileVersion, preReleaseSuffixOverride);
            PackageVersion = version;

            var owners = fileVersion.CompanyName;

            var shortSummary = fileVersion.Comments;
            var longDescription = fileVersion.Comments;
            var copyright = fileVersion.LegalCopyright;
            var releaseNotes = fileVersion.SpecialBuild;
            var keywords = fileVersion.FileDescription;
            var licenseUrl = fileVersion.LegalTrademarks;
            var projectUrl = fileVersion.LegalTrademarks;

            var folder = Path.GetDirectoryName(projectPath);
            // ReSharper disable once AssignNullToNotNullAttribute
            var packagesFile = Path.Combine(folder, @"packages.config");
            // ReSharper disable once AssignNullToNotNullAttribute
            var internalPackagesFile = Path.Combine(folder, @"internalPackages.config");
            // ReSharper disable once AssignNullToNotNullAttribute
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
                    config = deserializer.Deserialize(stream) as NuGetPackConfig;

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

                        if (config.DoIncludeCurrentProjectInNuSpec.HasValue)
                            doIncludeCurrentProj = config.DoIncludeCurrentProjectInNuSpec.Value;
                    }
                }
            }

            var doc = CreateNuspec(
                packageName, version, owners, authors, shortSummary,
                longDescription, releaseNotes, licenseUrl, projectUrl, iconUrl, copyright, keywords, nuspecPath, projectPath,
                isDebug, doAddFrameworkReferences, doIncludeSources,
                doIncludeCurrentProj, preReleaseSuffixOverride);

            return doc;
        }

        /// <summary>
        /// Gets the version of a file from its metadata
        /// </summary>
        /// <param name="fileVersion"></param>
        /// <param name="preReleaseSuffixOverride"></param>
        /// <returns></returns>
        private string GetVersion(FileVersionInfo fileVersion, string preReleaseSuffixOverride)
        {
            var version = fileVersion.FileMajorPart + "." + fileVersion.FileMinorPart + "." + fileVersion.FileBuildPart;

            if (fileVersion.FilePrivatePart != 0)
                version += "." + fileVersion.FilePrivatePart;

            if (preReleaseSuffixOverride == null)
            {
                var fileVersionParts = fileVersion.ProductVersion.Split(new[] { '-' }, StringSplitOptions.None);
                if (fileVersionParts.Length > 1)
                {
                    var preReleaseName = string.Join("-", fileVersionParts.Skip(1));

                    if (string.IsNullOrWhiteSpace(preReleaseName))
                        preReleaseName = "PreRelease";

                    version += "-" + preReleaseName;
                }
            }
            else if (preReleaseSuffixOverride != string.Empty)
            {
                version += "-" + preReleaseSuffixOverride;
            }

            return version;
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
            bool isDebug,
            bool doAddFrameworkReferences,
            bool doIncludeSources,
            bool doIncludeCurrentProj,
            string preReleaseSuffixOverride)
        {
            var nuspecFolder = Path.GetDirectoryName(nuspecPath);

            XAttribute dependenciesAttribute;

            var elements = GetElements(
                nuspecFolder, projectPath, isDebug, doIncludeSources, doIncludeCurrentProj,
                preReleaseSuffixOverride,
                out dependenciesAttribute);

            var dependencies = new XElement("group");
            dependencies.Add(dependenciesAttribute);

            var dep = elements.Where(el => el.ElementType == ElementType.NuGetDependency).Select(el => el.Element).ToList();

            foreach (var d in dep)
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

                frameworkReferences.AddRange(
                    elements.Where(el => el.ElementType == ElementType.FrameworkReference)
                    .Distinct(new DependencyInfoComparer())
                    .Select(el => el.Element).ToList());
    
                metadataElement.Add(new XElement("frameworkAssemblies",
                    frameworkReferences.Distinct(new XElementComparer())));
            }

            var doc = new XDocument(
                new XElement("package",
                    metadataElement,
                    new XElement("files", files)
                )
            );

            return doc;
        }

        public List<DependencyInfo> GetDependencies(
            string projectPath, string[] packagesFiles, out XAttribute dependenciesAttribute)
        {
            var targetFramework =  "net" + (GetFrameworkVersion(projectPath) ?? "45");

            dependenciesAttribute = new XAttribute("targetFramework", targetFramework);

            var result = new List<DependencyInfo>();

            foreach (var packagesFile in packagesFiles)
                result.AddRange(GetDependencies(packagesFile));

            return result;
        }

        public List<DependencyInfo> GetDependencies(string packagesFile)
        {
            var result = new List<DependencyInfo>();

            if (!File.Exists(packagesFile))
                return result;

            var packagesConfig = XDocument.Load(packagesFile);

            var packages = packagesConfig.XPathSelectElements("packages/package").ToList();

            foreach (var package in packages)
            {
                // ReSharper disable PossibleNullReferenceException
                string version = PackageVersion;
                if (package.Attribute("version") != null )
                {
                    version = package.Attribute("version").Value;
                }

                result.Add(
                    new DependencyInfo(
                        ElementType.NuGetDependency,
                        new XElement("dependency",
                        new XAttribute("id", package.Attribute("id").Value),
                        new XAttribute("version", version)))
                        );
                // ReSharper enable PossibleNullReferenceException
            }

            return result;
        }

        /// <summary>
        /// Gets project references for a given project that should be NuGet dependencies
        /// </summary>
        /// <param name="projectPath"></param>
        /// <param name="isDebug"></param>
        /// <param name="buildMachineBinFolder"></param>
        /// <param name="preReleaseSuffixOverride"></param>
        /// <returns></returns>
        public List<DependencyInfo> GetInternalDependencies(
            string projectPath, bool isDebug, string buildMachineBinFolder,
            string preReleaseSuffixOverride)
        {
            var references = GetReferences(projectPath, true);
            var projectFolder = Path.GetDirectoryName(projectPath);

            var results = new List<DependencyInfo>();

            foreach (var reference in references)
            {
                // ReSharper disable once AssignNullToNotNullAttribute
                var refProjPath = Path.Combine(projectFolder, reference);

                string assemblyName, assemblyPath;
                GetAssemblyNameAndPath(refProjPath, out assemblyName, isDebug, buildMachineBinFolder, out assemblyPath);

                if (assemblyName == null)
                    continue;

                DebugOut(() => "assemblyPath = " + assemblyPath);

                var fileVersion = FileVersionInfo.GetVersionInfo(assemblyPath);
                var referenceVersion = GetVersion(fileVersion, preReleaseSuffixOverride);

                results.Add(
                    new DependencyInfo(
                        ElementType.NuGetDependency,
                        new XElement("dependency",
                        new XAttribute("id", assemblyName),
                        new XAttribute("version", referenceVersion))));
            }

            return results;
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

        // ReSharper disable once PartialMethodWithSinglePart
        partial void DebugOut(Func<string> getText);

#if DEBUG
        partial void DebugOut(Func<string> getText)
        {
            Console.WriteLine(getText());
        }
#endif

        /// <summary>
        /// Get NuSpec elements from a given project and its references
        /// </summary>
        /// <param name="nuspecFolder">Output folder</param>
        /// <param name="projectPath">Path to project file to process</param>
        /// <param name="isDebug">For local builds, if true looks in Debug folder under bin folders, if false looks under release folders</param>
        /// <param name="doIncludeSources">If true, includes source files in NuSpec</param>
        /// <param name="doIncludeCurrentProj">
        /// <param name="preReleaseSuffixOverride"></param>
        /// <param name="dependenciesAttribute"></param>
        /// If true, treats given project as regular project,
        ///  if not treats it as a NuGet definition project only - does not take binaries or source from project
        /// </param>
        /// <param name="preReleaseSuffixOverride"></param>
        /// <param name="dependenciesAttribute"></param>
        /// <returns>Elements for NuSpec</returns>
        public List<DependencyInfo> GetElements(
            string nuspecFolder, string projectPath, bool isDebug, bool doIncludeSources,
            bool doIncludeCurrentProj, string preReleaseSuffixOverride,
            out XAttribute dependenciesAttribute)
        {
            if (nuspecFolder == null)
                throw new ArgumentNullException(nameof(nuspecFolder));

            if (projectPath == null)
                throw new ArgumentNullException(nameof(projectPath));

            DebugOut(() => $"\r\n\r\nGetFiles({nuspecFolder}, {projectPath}, {isDebug})\r\n");

            var projectFolder = Path.GetDirectoryName(projectPath);

            DebugOut(() => "nuspecToProject = " + Path.GetDirectoryName(
                AbsolutePathToRelativePath(Path.GetDirectoryName(projectPath), nuspecFolder + "\\")));

            DebugOut(() => "ProjectFolder = " + projectFolder);

            var packagesFile = Path.Combine(projectFolder, "packages.config");
            var internalPackagesFile = Path.Combine(projectFolder, "internalPackages.config");

            var result = new List<DependencyInfo>();

            result.AddRange(GetDependencies(
                projectPath, new[] { packagesFile, internalPackagesFile }, out dependenciesAttribute));


            result.AddRange(GetContentFiles(nuspecFolder, projectFolder, projectPath));
            result.AddRange(GetBinaryFiles(nuspecFolder, projectFolder, projectPath));
            result.AddRange(GetContentFiles(nuspecFolder, projectFolder, projectPath,
                srcFolder: @"sln\", destFolder: @"sln\", flattern: false, elementType: ElementType.SolutionItemsFile));
            result.AddRange(GetContentFiles(nuspecFolder, projectFolder, projectPath,
                srcFolder: @"tools\", destFolder: @"tools\", flattern: false, elementType: ElementType.ToolsFile));
            result.AddRange(GetContentFiles(nuspecFolder, projectFolder, projectPath,
                srcFolder: @"build\", destFolder: @"build\", flattern: false, elementType: ElementType.BuildFile));

            if (doIncludeCurrentProj)
            {
                result.AddRange(GetInternalDependencies(projectPath, isDebug, nuspecFolder, preReleaseSuffixOverride));

                result.AddRange(GetBinaryReferences(nuspecFolder, projectFolder, projectPath, isDebug, nuspecFolder));

                if (doIncludeSources)
                    result.AddRange(GetSourceFiles(nuspecFolder, projectFolder, projectPath));

                result.AddRange(GetDependenciesFromProject(projectFolder, projectPath));
            }

            bool? referencesContainNuGetPackConfig =
                (doIncludeCurrentProj ? false : (bool?)null);

            var references = GetReferences(projectPath, referencesContainNuGetPackConfig);

            foreach (var reference in references)
            {
                var refFolder = Path.GetDirectoryName(reference);

                DebugOut(() => "refFolder = " + refFolder);

                if (refFolder == null)
                    throw new NullReferenceException(nameof(refFolder));

                // ReSharper disable once AssignNullToNotNullAttribute
                var refProjFolder = Path.Combine(projectFolder, refFolder);
                // ReSharper disable once AssignNullToNotNullAttribute
                var refProjPath = Path.Combine(projectFolder, reference);

                DebugOut(() => "projFolder = " + refProjFolder);
                DebugOut(() => "projPath = " + refProjPath);

                result.AddRange(GetBinaryReferences(nuspecFolder, refProjFolder, refProjPath, isDebug, nuspecFolder));

                result.AddRange(GetFrameworkReferences(refProjFolder, refProjPath));
                
                if (doIncludeSources)
                    result.AddRange(GetSourceFiles(nuspecFolder, refProjFolder, refProjPath));
                
                result.AddRange(GetDependenciesFromProject(refProjFolder, refProjPath));
            }

            var exclude = result.Where(r =>
                r.Element.Attribute("target") != null
                && !string.IsNullOrEmpty(r.Element.Attribute("target").Value)
                &&
                (
                    r.Element.Attribute("target").Value.ToLower().EndsWith(@"\packages.config")
                    || r.Element.Attribute("target").Value.ToLower().EndsWith(@"\internalpackages.config")
                    || r.Element.Attribute("target").Value.ToLower().EndsWith(@"\nugetpack.config")
                )).ToList();

            result = result.Except(exclude).ToList();

            return result;
        }

        #region NuGet Project Parsing

        public List<DependencyInfo> GetContentFiles(
            string nuspecFolder, string projectFolder, string projectPath,
            string srcFolder = @"content\", string destFolder = @"content\",
            bool flattern = false, ElementType elementType = ElementType.ContentFile)
        {
            DebugOut(() => $"\r\n\r\nGetContentFiles({nuspecFolder}, {projectFolder}, {projectPath})\r\n");
            
            XNamespace xmlns;
            XElement proj;
            LoadProject(projectPath, out XDocument csProj, out xmlns, out proj);

            srcFolder = srcFolder.ToLower();

            var contentElements = proj.Elements(xmlns + "ItemGroup").Elements()
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
                // ReSharper disable AssignNullToNotNullAttribute
                items = contentElements
                    .Select(s =>
                        new DependencyInfo(
                            elementType,
                            new XElement("file",
                                new XAttribute("src", Path.Combine(relativeProjectFolder, s.src)),
                                new XAttribute("target", Path.Combine(destFolder, Path.GetFileName(s.target))))))
                .ToList();
                // ReSharper enable AssignNullToNotNullAttribute
            }

            return items;
        }

        public string GetFrameworkVersion(string projectPath)
        {
            XNamespace xmlns;
            XElement proj;
            LoadProject(projectPath, out XDocument csProj, out xmlns, out proj);

            var targetFrameworkVersion = proj
                .Elements(xmlns + "PropertyGroup").Elements(xmlns + "TargetFrameworkVersion")
                .Select(el => el.Value)
                .FirstOrDefault();

            if (string.IsNullOrEmpty(targetFrameworkVersion))
                return null;

            var result = targetFrameworkVersion.Replace("v", string.Empty).Replace(".", string.Empty);
            return result;
        }

        public List<DependencyInfo> GetBinaryFiles(
            string nuspecFolder, string projectFolder, string projectPath)
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

        /// <summary>
        /// Gets the framework references for a given project file
        /// </summary>
        /// <param name="projectFolder"></param>
        /// <param name="projectPath"></param>
        /// <returns></returns>
        public List<DependencyInfo> GetFrameworkReferences(string projectFolder, string projectPath)
        {
            DebugOut(() => $"\r\n\r\nGetFrameworkReferences({projectFolder}, {projectPath})\r\n");
            
            XNamespace xmlns;
            XElement proj;
            LoadProject(projectPath, out XDocument csProj, out xmlns, out proj);

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

        /// <summary>
        /// Gets the project references for a given project file
        /// </summary>
        /// <param name="projectPath"></param>
        /// <param name="referencesContainNuGetPackConfig">
        /// Enables filtering only references that should be NuGet dependencies (true),
        /// only references that should not be NuGet dependencies (false),
        /// or no filtering (null)
        /// </param>
        /// <returns></returns>
        public List<string> GetReferences(string projectPath, bool? referencesContainNuGetPackConfig = null)
        {
            XNamespace xmlns;
            XElement proj;
            LoadProject(projectPath, out XDocument csProj, out xmlns, out proj);

            var elements = proj
                .Elements(xmlns + "ItemGroup").Elements(xmlns + "ProjectReference");

            var references = elements
                .Select(r => r.Attribute("Include").Value)
                .ToList();

            if (!referencesContainNuGetPackConfig.HasValue)
                return references;

            var projectFolder = Path.GetDirectoryName(projectPath);

            var results = new List<string>();

            foreach (var reference in references)
            {
                // ReSharper disable once AssignNullToNotNullAttribute
                var referencePath = Path.GetFullPath(Path.Combine(projectFolder, reference));

                var isPkgProject = DoesProjectContainFile(referencePath, "nugetpack.config");

                if (isPkgProject == referencesContainNuGetPackConfig.Value)
                    results.Add(reference);
            }

            return results;
        }  

        #endregion

        #region Referenced Projects Parsing

        /// <summary>
        /// Get the output file path of the given project
        /// </summary>
        /// <param name="csProj"></param>
        /// <param name="isDebug"></param>
        /// <param name="projectFolder"></param>
        /// <returns></returns>
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

        /// <summary>
        /// Get referenced projects' output binaries that this NuSpec should include
        /// </summary>
        /// <param name="nuspecFolder"></param>
        /// <param name="projectFolder"></param>
        /// <param name="projectPath"></param>
        /// <param name="isDebug"></param>
        /// <param name="buildMachineBinFolder"></param>
        /// <returns></returns>
        public List<DependencyInfo> GetBinaryReferences(
            string nuspecFolder, string projectFolder, string projectPath, bool isDebug, string buildMachineBinFolder)
        {
            DebugOut(() =>
                $"\r\n\r\nGetBinaryReferences({projectFolder}, {projectPath}, {isDebug}, {buildMachineBinFolder})\r\n");

            var versionFolder = "net" + (GetFrameworkVersion(projectPath) ?? "45");

            string outputPath;

            XDocument csProj;
            XNamespace xmlns;
            XElement proj;
            LoadProject(projectPath, out csProj, out xmlns, out proj);

            var propGroups = proj.Elements(xmlns + "PropertyGroup");

            outputPath = GetOutputPath(csProj, isDebug, projectFolder);

            if (!Directory.Exists(outputPath)
                || !Directory.GetFiles(outputPath).Any(f =>
                    f.ToLower().EndsWith(".dll") ||f.ToLower().EndsWith(".exe"))
                )
            {
                outputPath = buildMachineBinFolder;
            }

            DebugOut(() => "outputPath = " + outputPath);

            var relativeOutputPath = AbsolutePathToRelativePath(outputPath, nuspecFolder + "\\");

            var assemblyNameElement = propGroups.Elements(xmlns + "AssemblyName").FirstOrDefault();

            if (assemblyNameElement == null)
                throw new ApplicationException($"Could find AssemblyName in {projectPath})");

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

        /// <summary>
        /// Gets the path and name of the binary created by a project
        /// </summary>
        /// <param name="projectPath"></param>
        /// <param name="assemblyName"></param>
        /// <param name="isDebug"></param>
        /// <param name="buildMachineBinFolder"></param>
        /// <param name="assemblyPath"></param>
        private void GetAssemblyNameAndPath(
            string projectPath, out string assemblyName, bool isDebug, string buildMachineBinFolder, out string assemblyPath)
        {
            DebugOut(() => $"\r\n\r\nGetAssemblyName({projectPath})\r\n");

            XDocument csProj;
            XNamespace xmlns;
            XElement proj;
            LoadProject(projectPath, out csProj, out xmlns, out proj);

            var propGroups = proj.Elements(xmlns + "PropertyGroup");

            var assemblyNameElement = propGroups.Elements(xmlns + "AssemblyName").FirstOrDefault();

            if (assemblyNameElement == null)
                throw new ApplicationException($"Could find AssemblyName in {projectPath})");

            var asmName = assemblyNameElement.Value;

            if (asmName.StartsWith("$"))
            {
                // Inside a VSIX template output project
                assemblyName = null;
                assemblyPath = null;
                return;
            }

            var projectFolder = Path.GetDirectoryName(projectPath);

            var outputPath = GetOutputPath(csProj, isDebug, projectFolder);

            if (!Directory.Exists(outputPath)
                || !Directory.GetFiles(outputPath).Any(f =>
                    f.ToLower().EndsWith(".dll") || f.ToLower().EndsWith(".exe"))
                )
            {
                outputPath = buildMachineBinFolder;
            }

            DebugOut(() => "outputPath = " + outputPath);

            var dllExe = new[] { ".dll", ".exe" };

            var asmFile = Directory.GetFiles(outputPath).FirstOrDefault(f =>
                Path.GetFileNameWithoutExtension(f) == asmName
                && dllExe.Contains(Path.GetExtension(f).ToLower()));

            if (asmFile == null)
                throw new ApplicationException($"Could not find path for {asmName}, searched {outputPath}");

            assemblyName = asmName;
            assemblyPath = asmFile;
        }

        private bool IsFileWithDifferentExtension(
            string expectedFileNameWithoutExtension, string actualFilePath)
        {
            var actualFileName = Path.GetFileName(actualFilePath);
            if (actualFileName == null)
                throw new ArgumentNullException(nameof(actualFileName));

            if (!actualFileName.StartsWith(expectedFileNameWithoutExtension))
                return false;

            var suffix = actualFileName.Substring(expectedFileNameWithoutExtension.Length);
            if (!suffix.StartsWith("."))
                return false;

            if (suffix.Substring(1).Contains("."))
                return false;

            return true;
        }

        /// <summary>
        /// Get the source files of the given project file
        /// </summary>
        /// <param name="nuspecFolder"></param>
        /// <param name="projectFolder"></param>
        /// <param name="projectPath"></param>
        /// <returns></returns>
        public List<DependencyInfo> GetSourceFiles(string nuspecFolder, string projectFolder, string projectPath)
        {
            DebugOut(() => $"\r\n\r\nGetSourceFiles({projectFolder}, {projectPath})\r\n");

            var projectName = Path.GetFileNameWithoutExtension(projectPath);
            
            XNamespace xmlns;
            XElement proj;
            LoadProject(projectPath, out XDocument csProj, out xmlns, out proj);

            if (proj == null)
                throw new ApplicationException($"Could find namespace in load project file {projectPath}");

            var codeElements = proj.Elements(xmlns + "ItemGroup").Elements(xmlns + "Compile");

            // TODO: Remove commented out line after confirming it is redundant
            //var noneElements = proj.Elements(xmlns + "ItemGroup").Elements(xmlns + "None");

            var relativeProjectFolder = AbsolutePathToRelativePath(projectFolder, nuspecFolder + "\\");

            var items = codeElements//.Union(noneElements)
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

        /// <summary>
        /// Checks if a project contains a file with a given name
        /// </summary>
        /// <param name="projectPath"></param>
        /// <param name="file"></param>
        /// <returns></returns>
        public bool DoesProjectContainFile(string projectPath, string file)
        {
            DebugOut(() => $"\r\n\r\nDoesProjectContainFile({projectPath}, {file})\r\n");
            
            XNamespace xmlns;
            XElement proj;
            LoadProject(projectPath, out XDocument csProj, out xmlns, out proj);

            var codeElements = proj.Elements(xmlns + "ItemGroup").Elements(xmlns + "Compile");
            var noneElements = proj.Elements(xmlns + "ItemGroup").Elements(xmlns + "None");

            var result = codeElements
                .Union(noneElements)
                .Any(el =>
                    el.Attribute("Include") != null
                    && string.Compare(
                        el.Attribute("Include").Value,
                        file,
                        StringComparison.InvariantCultureIgnoreCase) == 0);

            return result;
        }

        /// <summary>
        /// Gets the NuGet packages a given project is depended on
        /// </summary>
        /// <param name="projectFolder"></param>
        /// <param name="projectPath"></param>
        /// <returns></returns>
        public IList<DependencyInfo> GetDependenciesFromProject(string projectFolder, string projectPath)
        {
            DebugOut(() => $"\r\n\r\nGetDependenciesFromProject({projectFolder}, {projectPath})\r\n");
            
            XNamespace xmlns;
            XElement proj;
            LoadProject(projectPath, out XDocument csProj, out xmlns, out proj);

            var noneElements = proj.Elements(xmlns + "ItemGroup").Elements(xmlns + "None");

            var packagesFileName = noneElements.Where(el =>
                    el.Attribute("Include") != null && el.Attribute("Include").Value.ToLower() == "packages.config")
                .Select(el => el.Attribute("Include").Value)
                .FirstOrDefault();

            if (packagesFileName == null)
                return new DependencyInfo[0];

            string packagesFile;
            packagesFile = projectFolder + @"\" + packagesFileName;

            DebugOut(() => $"packagesFile: {packagesFile}");

            var dependencies = GetDependencies(packagesFile);

            return dependencies;
        }

        /// <summary>
        /// Loads the XML of a given project by file path
        /// </summary>
        /// <param name="projectPath"></param>
        /// <param name="csProj"></param>
        /// <param name="xmlns"></param>
        /// <param name="proj"></param>
        private void LoadProject(
            string projectPath, out XDocument csProj, out XNamespace xmlns, out XElement proj)
        {
            csProj = XDocument.Load(projectPath);

            if (csProj == null)
                throw new ApplicationException($"Could not load project file {projectPath}");

            if (csProj.Root == null)
                throw new ApplicationException($"Could find load namespace from project file {projectPath}");

            xmlns = csProj.Root.GetDefaultNamespace();

            proj = csProj.Element(xmlns + "Project");

            if (proj == null)
                throw new ApplicationException($"Could not project from project file {projectPath}");
        }

        #endregion
    }
}
