using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Xml.Linq;
using System.Xml.XPath;

namespace PubComp.Building.NuGetPack
{
    public abstract partial class NuspecCreatorBase
    {
        protected string TargetFrameworkElement { get; set; }
        public static string SlnOutputFolder;


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

            nuspecPath = SlnOutputFolder == null ? nuspecPath : Path.Combine(SlnOutputFolder, Path.GetFileName(nuspecPath));

            DebugOut(() => "nuspecPath = " + nuspecPath);

            DebugOut(() => "Saving");

            Console.WriteLine("Saving nuspec file");

            if (nuspecPath != null)
            {
                doc.Save(nuspecPath);

                if (!doCreatePkg)
                    return;

                DebugOut(() => "CreatingPackage");

                var doSeparateSymbols = config?.DoSeparateSymbols ?? false;
                CreatePackage(nuspecPath, doSeparateSymbols);
            }
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
                nuspecPath = SlnOutputFolder  == null? nuspecPath : Path.Combine(SlnOutputFolder, Path.GetFileName(nuspecPath));

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
                        if (doAddFrameworkReferences && this is NuspecCreatorNewCsProj)
                            throw new NotSupportedException("AddFrameworkReferences is not Supported for Dot NetStandard projects! Please edit your NuGetPack.config file");

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
                        if (!doIncludeCurrentProj && this is NuspecCreatorNewCsProj)
                            throw new NotSupportedException("Dot NetStandard projects must include Current Project! Please edit your NuGetPack.config file");
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

            var elements = GetElements(
                nuspecFolder, projectPath, isDebug, doIncludeSources, doIncludeCurrentProj,
                preReleaseSuffixOverride,
                out var dependenciesAttribute);
        

            var files = elements.Where(el =>
                el.ElementType != ElementType.NuGetDependency
                && el.ElementType != ElementType.FrameworkReference)
                .Select(el => el.Element).ToList();

            var dependencies = new XElement("group");
            dependencies.Add(dependenciesAttribute);

            var dep = elements.Where(el => el.ElementType == ElementType.NuGetDependency).Select(el => el.Element).ToList();

            foreach (var d in dep)
            {
                if (!dependencies.Elements().Any(el => el.ToString() == d.ToString()))
                    dependencies.Add(d);
            }

            var dependenciesElemnt = GetDependenciesForNewCsProj(projectPath, dependencies, preReleaseSuffixOverride);

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
                        dependenciesElemnt,
                        new XElement("tags", keywords));

            if (doAddFrameworkReferences)
            {
                var frameworkReferences = GetFrameworkReferences(Path.GetDirectoryName(projectPath), projectPath)
                    .Select(el => el.Element)
                    .ToList();

                metadataElement.Add(new XElement("frameworkAssemblies",
                    frameworkReferences));
            }

            var referencesSection = GetReferencesFiles(projectPath);
            if (referencesSection != null)
                metadataElement.Add(referencesSection);
            else
                metadataElement.Add(new XElement("references", string.Empty));

            var contentElements = GetContentElements(projectPath);
            if (projectPath.Length > 0)
                metadataElement.Add(ContentFilesSection(projectPath, contentElements));

            var doc = new XDocument(
                new XElement("package",
                    metadataElement,
                    new XElement("files", files)
                )
            );

            return doc;
        }

        protected abstract XElement GetDependenciesForNewCsProj(string projectPath, XElement dependencies, string preReleaseSuffixOverride);

        public abstract List<DependencyInfo> GetDependencies(string projectPath, out XAttribute dependenciesAttribute);

        public abstract List<DependencyInfo> GetBinaryFiles(
            string nuspecFolder, string projectFolder, string projectPath, bool isDebug);

        public abstract XElement GetReferencesFiles(string projectPath);


        protected virtual XElement ContentFilesSection(string projectPath, IEnumerable<dynamic> contentElements)
        {
            return null;
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
                result.Add(
                    new DependencyInfo(
                        ElementType.NuGetDependency,
                        new XElement("dependency",
                        new XAttribute("id", package.Attribute("id").Value),
                        new XAttribute("version", package.Attribute("version").Value))));
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
        /// <param name="isProjNetStandard"></param>
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

                GetAssemblyNameAndPath(refProjPath, out var assemblyName, isDebug, buildMachineBinFolder, out var assemblyPath);

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

        

        public  string AbsolutePathToRelativePath(string filePath, string referencePath)
        {
            if (!Path.IsPathRooted(filePath))
                return filePath;

            var absolutePath = Path.GetFullPath(filePath);

            var fileUri = new Uri(absolutePath);
            var referenceUri = new Uri(SlnOutputFolder ?? referencePath);
            var relpath = referenceUri.MakeRelativeUri(fileUri).ToString().Replace('/', '\\');

            DebugOut(() => "filePath = " + filePath);
            DebugOut(() => "referencePath = " + referencePath);
            DebugOut(() => "Relative Path = " + relpath);
            return relpath;
        }

        // ReSharper disable once PartialMethodWithSinglePart

#if DEBUG
        protected void DebugOut(Func<string> getText)
        {
            Console.WriteLine(getText());
        }
#else
        protected void DebugOut(Func<string> getText){}
#endif
        protected abstract IEnumerable<DependencyInfo> GetContentFilesForNetStandard(string projectPath, List<DependencyInfo> files);

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

            var result = new List<DependencyInfo>();

            result.AddRange(GetDependencies(
                projectPath, out dependenciesAttribute));


            result.AddRange(GetContentFiles(nuspecFolder, projectFolder, projectPath));
            result.AddRange(GetContentFilesForNetStandard(projectPath, result));
            result.AddRange(GetBinaryFiles(nuspecFolder, projectFolder, projectPath, isDebug));
            result.AddRange(GetContentFiles(nuspecFolder, projectFolder, projectPath,
                srcFolder: @"sln\", destFolder: @"sln\", flattern: false, elementType: ElementType.SolutionItemsFile));
            result.AddRange(GetContentFiles(nuspecFolder, projectFolder, projectPath,
                srcFolder: @"tools\", destFolder: @"tools\", flattern: false, elementType: ElementType.ToolsFile));
            result.AddRange(GetContentFiles(nuspecFolder, projectFolder, projectPath,
                srcFolder: @"build\", destFolder: @"build\", flattern: false, elementType: ElementType.BuildFile));

            if (doIncludeCurrentProj)
            {
                IncludeCurrentProject(nuspecFolder, projectPath, isDebug, doIncludeSources, preReleaseSuffixOverride, result, projectFolder);
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

                var refProjFolder = Path.Combine(projectFolder ?? string.Empty, refFolder);
                var refProjPath = Path.Combine(projectFolder ?? string.Empty, reference);

                DebugOut(() => "projFolder = " + refProjFolder);
                DebugOut(() => "projPath = " + refProjPath);

                result.AddRange(GetBinaryReferences(nuspecFolder, refProjFolder, refProjPath, isDebug, nuspecFolder));
                
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

        protected abstract void IncludeCurrentProject(string nuspecFolder, string projectPath, bool isDebug,
            bool doIncludeSources, string preReleaseSuffixOverride, List<DependencyInfo> result, string projectFolder);

        #region NuGet Project Parsing

        protected abstract string GetContentFileTarget(XElement el,XNamespace xmlns);

        public List<DependencyInfo> GetContentFiles(
            string nuspecFolder, string projectFolder, string projectPath,
            string srcFolder = @"content\", string destFolder = @"content\",
            bool flattern = false, ElementType elementType = ElementType.ContentFile)
        {
            DebugOut(() => $"\r\n\r\nGetContentFiles({nuspecFolder}, {projectFolder}, {projectPath})\r\n");

            XNamespace xmlns;
            XElement proj;
            NuspecCreatorHelper.LoadProject(projectPath, out XDocument csProj, out xmlns, out proj);

            srcFolder = srcFolder.ToLower();

            var contentElements = GetContentElements(projectPath, srcFolder, elementType);
            
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

        private IEnumerable<dynamic> GetContentElements(string projectPath, string srcFolder = @"content\", ElementType elementType = ElementType.ContentFile)
        {
            XNamespace xmlns;
            XElement proj;
            NuspecCreatorHelper.LoadProject(projectPath, out XDocument _, out xmlns, out proj);

            srcFolder = srcFolder.ToLower();

            var contentElements = proj.Elements(xmlns + "ItemGroup").Elements()
                .Where(el => el.Attribute("Include") != null)
                .Select(el =>
                    new
                    {
                        src = el.Attribute("Include").Value,
                        target = GetContentFileTarget(el, xmlns) ??
                                 el.Attribute("Include").Value
                    })
                .Where(st => st.target.ToLower().StartsWith(srcFolder) && st.target.ToLower() != srcFolder)
                .Union(elementType == ElementType.ContentFile ? GetConcreateContentElements(projectPath) : new List<dynamic>());

            return contentElements;
        }

        protected virtual IEnumerable<dynamic> GetConcreateContentElements(string projectFolder)
        {
            return new List<dynamic>();
        }

        protected abstract string FormatFrameworkVersion(string targetFrameworkVersion);

        protected string GetFrameworkVersion(string projectPath)
        {
            NuspecCreatorHelper.LoadProject(projectPath, out XDocument _, out XNamespace xmlns, out XElement proj);

            var targetFrameworkVersion = proj
                .Elements(xmlns + "PropertyGroup")
                .Elements(xmlns + TargetFrameworkElement)
                .Select(el => el.Value)
                .FirstOrDefault();

            if (string.IsNullOrEmpty(targetFrameworkVersion))
            {
                var targetFrameworksVersion = proj
                    .Elements(xmlns + "PropertyGroup")
                    .Elements(xmlns + "TargetFrameworks")
                    .Select(el => el.Value)
                    .FirstOrDefault();

                targetFrameworkVersion = targetFrameworksVersion.Split(';').FirstOrDefault();
            }

            if (string.IsNullOrEmpty(targetFrameworkVersion))
                return null;
            
            return FormatFrameworkVersion(targetFrameworkVersion);
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
            NuspecCreatorHelper.LoadProject(projectPath, out XDocument csProj, out xmlns, out proj);

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
            NuspecCreatorHelper.LoadProject(projectPath, out XDocument csProj, out xmlns, out proj);

            var elements = GetProjectReference(proj, xmlns);

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


        protected abstract IEnumerable<XElement> GetProjectReference(XElement proj, XNamespace xmlns);


        #endregion


        public string GetFrameworkOutputFolder(string projectPath,bool isDebug)
        {
            var projectFolder = Path.GetDirectoryName(projectPath);
            var csProj = XDocument.Load(projectPath);
            var fullPath = GetOutputPath(csProj, isDebug, projectFolder);
            return Path.GetFileName(fullPath);
        }
        #region Referenced Projects Parsing



        /// <summary>
        /// Get the output file path of the given project
        /// </summary>
        /// <param name="csProj"></param>
        /// <param name="isDebug"></param>
        /// <param name="projectFolder"></param>
        /// <param name="isNetStandard"></param>
        /// <returns></returns>
        protected abstract string GetOutputPath(XDocument csProj, bool isDebug, string projectFolder);
        

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

            NuspecCreatorHelper.LoadProject(projectPath, out var csProj, out _, out _);

            var outputPath = GetOutputPath(csProj, isDebug, projectFolder);

            if (!Directory.Exists(outputPath)
                || !Directory.GetFiles(outputPath).Any(f =>
                    f.ToLower().EndsWith(".dll") ||f.ToLower().EndsWith(".exe"))
            )
            {
                outputPath = buildMachineBinFolder;
            }

            DebugOut(() => "outputPath = " + outputPath);

            var relativeOutputPath = AbsolutePathToRelativePath(outputPath, nuspecFolder + "\\");

            var versionFolder = "net" + (GetFrameworkVersion(projectPath) ?? "45");

            var files = GetProjectBinaryFiles(projectPath, outputPath);

            var items = files
                .Select(file =>
                    new DependencyInfo(
                        ElementType.LibraryFile,
                        new XElement("file",
                            new XAttribute("src", Path.Combine(relativeOutputPath, Path.GetFileName(file))),
                            new XAttribute("target", Path.Combine(@"lib\" + versionFolder + @"\", Path.GetFileName(file)))
                        ))).ToList();

            return items;
        }

        protected List<string> GetProjectBinaryFiles(string projectPath, string outputPath)
        {
            var assemblyName = GetAssemblyName(projectPath);

            var files = Directory.GetFiles(outputPath, assemblyName + ".*").ToList();

            files = files
                .Where(file =>
                    !file.EndsWith(".nuspec") && !file.EndsWith(".nupkg")
                                              && IsFileWithDifferentExtension(assemblyName, file)).ToList();
            return files;
        }

        /// <summary>
        /// Gets the path and name of the binary created by a project
        /// </summary>
        /// <param name="projectPath"></param>
        /// <param name="assemblyName"></param>
        /// <param name="isDebug"></param>
        /// <param name="buildMachineBinFolder"></param>
        /// <param name="assemblyPath"></param>
        public void GetAssemblyNameAndPath(
            string projectPath, out string assemblyName, bool isDebug, string buildMachineBinFolder, 
            out string assemblyPath)
        {
            DebugOut(() => $"\r\n\r\nGetAssemblyName({projectPath})\r\n");

            NuspecCreatorHelper.LoadProject(projectPath, out var csProj, out _, out _);

            var asmName = GetAssemblyName(projectPath);

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

            var dllExe = new[] {".dll", ".exe"};

            var asmFile = Directory.GetFiles(outputPath).FirstOrDefault(f =>
                Path.GetFileNameWithoutExtension(f) == asmName
                && dllExe.Contains(Path.GetExtension(f).ToLower()));

            if (asmFile == null)
                throw new ApplicationException($"Could not find path for {asmName}, searched {outputPath}");

            assemblyName = asmName;
            assemblyPath = asmFile;
        }

        protected static string GetAssemblyName(string projectPath)
        {
            NuspecCreatorHelper.LoadProject(projectPath, out _, out var xmlns, out var proj);

            var propGroups = proj.Elements(xmlns + "PropertyGroup");
            var assemblyNameElement = propGroups.Elements(xmlns + "AssemblyName").FirstOrDefault();

            var asmName = assemblyNameElement?.Value ?? Path.GetFileName(projectPath)?.Replace(".csproj", String.Empty).TrimEnd('.');
            return asmName;
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
            NuspecCreatorHelper.LoadProject(projectPath, out XDocument csProj, out xmlns, out proj);

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

        protected abstract bool DoesProjectContainFile(string projectPath, string file,
            IEnumerable<XElement> noneElements, IEnumerable<XElement> codeElements);
        /// <summary>
        /// Checks if a project contains a file with a given name
        /// </summary>
        /// <param name="projectPath"></param>
        /// <param name="file"></param>
        /// <param name="isProjNetStandard"></param>
        /// <returns></returns>
        private bool DoesProjectContainFile(string projectPath, string file)
        {
            DebugOut(() => $"\r\n\r\nDoesProjectContainFile({projectPath}, {file})\r\n");

            NuspecCreatorHelper.LoadProject(projectPath, out var _, out var xmlns, out var proj);

            var codeElements = proj.Elements(xmlns + "ItemGroup").Elements(xmlns + "Compile");
            var noneElements = proj.Elements(xmlns + "ItemGroup").Elements(xmlns + "None");
            
            return DoesProjectContainFile(projectPath, file, noneElements, codeElements);
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
            NuspecCreatorHelper.LoadProject(projectPath, out XDocument csProj, out xmlns, out proj);

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

        #endregion
    }
}
