using System;
using System.CodeDom;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Linq;
using System.Xml.XPath;

namespace PubComp.Building.NuGetPack
{
    public class NuspecCreatorNetStandard : NuspecCreatorBase
    {
        public NuspecCreatorNetStandard()
        {
            TargetFrameworkElement = "TargetFramework";
        }

        public override List<DependencyInfo> GetDependencies(
            string projectPath, out XAttribute dependenciesAttribute)
        {
            var targetFramework = GetTargetFramework(projectPath);

            dependenciesAttribute = new XAttribute("targetFramework", targetFramework);

            var result = new List<DependencyInfo>();
            result.AddRange(GetProjectDependenciesNetStandard(projectPath));
            result.AddRange(GetPackageDependenciesNetStandard(projectPath));


            return result;
        }

        private string GetTargetFramework(string projectPath)
        {
            var version = GetFrameworkVersion(projectPath);
            var targetFramework =
                version != null ? $".{version}".Replace("netstandard", "NETStandard") : ".NETStandard2.0";
            return targetFramework;
        }

        protected override XElement ContentFilesSection(string projectPath, IEnumerable<dynamic> contentElements)
        {
            const string content = "content\\";
            var all = contentElements.Where(f => f.target.Contains(content) ?? false)
                .Select(f => f.target.TrimStart(content.ToCharArray())).Cast<string>().ToList();
            var files = all.Where(n => n.IndexOf("\\") < 0).ToList();
            var folders = all.Where(n => n.IndexOf("\\") >= 0).Select(f => Path.GetDirectoryName(f)?.Replace('\\', '/')).Distinct().ToList();


            var result = files
                .Select(s =>
                    new XElement("files",
                        new XAttribute("include", @"any/any/" + s),
                        new XAttribute("buildAction", "Content"))).ToList();
            result.AddRange(folders
                .Select(s =>
                    new XElement("files",
                        new XAttribute("include", $"**/{s}/*.*"),
                        new XAttribute("buildAction", "Content"))).ToList());

            return new XElement("contentFiles", result);
        }

        protected override string GetOutputPath(XDocument csProj, bool isDebug, string projectFolder)
        {
            var xmlns = csProj.Root.GetDefaultNamespace();
            var proj = csProj.Element(xmlns + "Project");
            var propGroups = proj.Elements(xmlns + "PropertyGroup").ToList();

            var config = (isDebug ? "Debug|AnyCPU" : "Release|AnyCPU");

            var outputPathElement = propGroups
                .Where(el => el.Attribute("Condition") != null
                             && el.Attribute("Condition").Value.Contains(config))
                .Elements(xmlns + "OutputPath").FirstOrDefault();

            string outputPath;
            if (outputPathElement == null)
            {
                var targetFramework = propGroups.FirstOrDefault(pg => pg.Elements("TargetFramework").Any())
                    ?.Element("TargetFramework")?.Value;
                targetFramework = targetFramework ?? propGroups
                                      .FirstOrDefault(pg => pg.Elements("TargetFrameworks").Any())
                                      ?.Element("TargetFrameworks")?.Value;
                var semiColPos = targetFramework.IndexOf(";");
                if (semiColPos > 0)
                    targetFramework = targetFramework.Substring(0, semiColPos);

                outputPath = Path.Combine(projectFolder,
                    $"bin\\{(isDebug ? "debug" : "release")}\\{targetFramework}");

                if (!Directory.Exists(outputPath) // netStandard Projects can omit the Condition Element or any other indication of build type!
                    || !Directory.GetFiles(outputPath).Any(f =>
                        f.ToLower().EndsWith(".dll") || f.ToLower().EndsWith(".exe")))
                {
                    outputPath = Path.Combine(projectFolder,
                        $"bin\\{(!isDebug ? "debug" : "release")}\\{targetFramework}");
                }
            }
            else
            {
                outputPath = Path.Combine(projectFolder, outputPathElement.Value);
            }

            return outputPath;
        }

        protected override bool DoesProjectContainFile(string projectPath, string file,
            IEnumerable<XElement> noneElements, IEnumerable<XElement> codeElements)
        {
            var directoryContainsFile = !Directory.GetFiles(Path.GetDirectoryName(projectPath)).Contains(file);
            var projIgnoresFile = noneElements.Any(e =>
                string.Equals(e.Attribute("Remove")?.Value, file, StringComparison.CurrentCultureIgnoreCase));

            var projIncludesFile = codeElements.Any(e =>
                string.Equals(e.Attribute("Include")?.Value, file, StringComparison.CurrentCultureIgnoreCase));

            return (directoryContainsFile && !projIgnoresFile) || projIncludesFile;
        }

        protected override string GetContentFileTarget(XElement el, XNamespace xmlns)
        {
            return el.Attribute("Link")?.Value;
        }

        /// <summary>
        /// Add manually all files under content folder, as new csproj doesn't contain explicity all files as XML elements
        /// </summary>
        /// <param name="projectPath"></param>
        /// <returns></returns>
        protected override IEnumerable<dynamic> GetConcreateContentElements(string projectPath)
        {
            var dirName = Path.GetDirectoryName(projectPath);
            if (!Directory.Exists(dirName + @"\content"))
                return new List<dynamic>();
            var filenames = Directory.GetFiles(dirName + @"\content", "*", SearchOption.AllDirectories);
            var res = filenames.Select(f =>
            {
                var path = f.Replace($@"{dirName}\", "");
                return new
                {
                    src = path,
                    target = path
                };
            });

            return res;
        }

        protected override string FormatFrameworkVersion(string targetFrameworkVersion)
        {
            return targetFrameworkVersion;
        }

        private List<DependencyInfo> GetPackageDependenciesNetStandard(string projectPath)
        {
            var result = new List<DependencyInfo>();

            if (!File.Exists(projectPath))
                return result;

            var project = XDocument.Load(projectPath);

            var packages = project.XPathSelectElements("//ItemGroup//PackageReference").ToList();

            foreach (var package in packages)
            {
                result.Add(
                    new DependencyInfo(
                        ElementType.NuGetDependency,
                        new XElement("dependency",
                            new XAttribute("id", package.Attribute("Include")?.Value ?? String.Empty),
                            new XAttribute("version", package.Attribute("Version")?.Value ?? String.Empty),
                            new XAttribute("exclude", "Build,Analyzers"))));
            }

            return result;
        }

        private IEnumerable<DependencyInfo> GetProjectDependenciesNetStandard(string projectPath)
        {
            var result = new List<DependencyInfo>();

            if (!File.Exists(projectPath))
                return result;

            var projref = GetProjectIncludeFiles(projectPath, out var verList, out var xmlns, out var proj, true);

            if (projref == null || projref.Count == 0)
                return result;

            for (var i = 0; i < projref.Count; i++)
            {
                var dependantFile = projref[i];
                var ver = verList[i];

                result.Add(
                    new DependencyInfo(
                        ElementType.NuGetDependency,
                        new XElement("dependency",
                            new XAttribute("id", dependantFile),
                            new XAttribute("version", ver),
                            new XAttribute("exclude", "Build,Analyzers"))));
            }

            return result;
        }

        private List<string> GetProjectIncludeFiles(string projectPath, out List<string> verList, out XNamespace xmlns, out XElement proj, bool isFileNuget)
        {
            verList = new List<string>();
            NuspecCreatorHelper.LoadProject(projectPath, out XDocument _, out xmlns, out proj);
            var x = xmlns;
            var projref = proj?.Elements(xmlns + "ItemGroup")?.Elements(xmlns + "ProjectReference")?.Where(pr =>
                {
                    var file = pr.Attribute(x + "Include")?.Value ?? pr.LastAttribute.Value;
                    file = Path.Combine(Path.GetDirectoryName(projectPath) ?? String.Empty, file);
                    file = Path.Combine(Path.GetDirectoryName(file) ?? "", "NuGetPack.config");
                    return (isFileNuget ? File.Exists(file) : !File.Exists(file));
                })
                .Select(f => f.Attribute(x + "Include")?.Value ?? f.LastAttribute.Value).ToList();

            if (projref.Count == 0)
                return projref;

            for (var i = 0; i < projref.Count; i++)
            {
                var pr = projref[i];
                var incProj = Path.Combine(Path.GetDirectoryName(projectPath) ?? String.Empty, pr);
                NuspecCreatorHelper.LoadProject(incProj, out XDocument _, out x, out var prj);
                var ver = prj?.Element(xmlns + "PropertyGroup")?.Element(xmlns + "Version")?.Value ?? "1.0.0";
                verList.Add(ver);

                var asem = GetAssemblyName(incProj);
                projref[i] = asem;
            }

            return projref;
        }

        public override List<DependencyInfo> GetBinaryFiles(
            string nuspecFolder, string projectFolder, string projectPath, bool isDebug)
        {
            NuspecCreatorHelper.LoadProject(projectPath, out var csProj, out _, out _);

            var outputPath =  GetOutputPath(csProj, isDebug, projectFolder);

            if (!Directory.Exists(outputPath)
                || !Directory.GetFiles(outputPath).Any(f =>
                    f.ToLower().EndsWith(".dll") || f.ToLower().EndsWith(".exe"))
            )
            {
                outputPath = nuspecFolder;
            }

            outputPath = SlnOutputFolder ?? outputPath;

            var includeFiles = GetProjectIncludeFiles(projectPath, out _, out _, out _, false);
            var files = includeFiles.Select(f => f + ".DLL").ToList();
            files.AddRange(includeFiles.Where(f => File.Exists(f + ".PDB")).Select(f => f + ".PDB"));


            string[] frameworks;
            var pathHeader = string.Empty;
            var targetFrameworks = GetTargetFrameworks(projectPath);
            if (string.IsNullOrEmpty(targetFrameworks))
                frameworks = new[] {GetTargetFramework(projectPath).TrimStart('.')};
            else
            {
                frameworks = Directory.GetDirectories(outputPath);
                if (!frameworks.Any())
                {
                    frameworks = targetFrameworks.Split(';');
                    pathHeader = @"..\";
                }

                for (var i = 0; i < frameworks.Length; i++)
                {
                    var sep = frameworks[i].LastIndexOf('\\');
                    if (sep > 0)
                        frameworks[i] = frameworks[i].Substring(sep + 1);
                }
            }

            var items = CreateBinFilesDepInfoList(frameworks, files);
            files.Clear();

            if (Directory.GetFiles(Path.Combine(outputPath, pathHeader)).Any(f =>
                f.ToLower().EndsWith(".dll") || f.ToLower().EndsWith(".exe")))
            {
                files = GetProjectBinaryFiles(projectPath, outputPath)
                    .Select(Path.GetFileName).ToList();
                items.AddRange(CreateBinFilesDepInfoList(frameworks, files));
            }
            else
            {
                foreach (var framework in frameworks)
                {
                    files.AddRange(GetProjectBinaryFiles(projectPath,
                            Path.Combine(outputPath, SlnOutputFolder == null ? $"..\\{framework}" : framework))
                        .Select(f =>
                        {
                            var frmwrk = f.Substring(Path.GetDirectoryName(Path.GetDirectoryName(f)).Length).TrimStart('\\');
                            var prevDir = !string.IsNullOrEmpty(SlnOutputFolder) ? string.Empty : @"..\";
                            return prevDir + frmwrk;
                        }).ToList());
                }

                items.AddRange(files
                    .Select(s =>
                        new DependencyInfo(
                            ElementType.LibraryFile,
                            new XElement("file",
                                new XAttribute("src", s),
                                new XAttribute("target", Path.Combine($"lib\\{ExtractFramework(s)}")))))
                    .ToList());
            }

            return items;
        }

        private string ExtractFramework(string file)
        {
            if(string.IsNullOrEmpty(SlnOutputFolder))
                file = file.Substring(3);
            var result = file.Substring(0, file.LastIndexOf('\\'));
            return result;
        }

        private static List<DependencyInfo> CreateBinFilesDepInfoList(string[] frameworks, List<string> files)
        {
            var result = new List<DependencyInfo>();
            foreach (var framework in frameworks)
            {
                result.AddRange(files
                    .Select(s =>
                        new DependencyInfo(
                            ElementType.LibraryFile,
                            new XElement("file",
                                new XAttribute("src", s),
                                new XAttribute("target", Path.Combine($"lib\\{framework}")))))
                    .ToList());
            }

            return result;
        }

        public override XElement GetReferencesFiles(string projectPath)
        {
            var includeFiles = GetProjectIncludeFiles(projectPath, out _, out _, out _, false);
            var files = (includeFiles.Select(pr => pr + ".DLL")).ToList();
            //files.AddRange(includeFiles.Select(pr => pr + ".PDB"));

            if (files.Count == 0)
                return null;

            var items = files
                .Select(s =>
                    new XElement("reference",
                        new XAttribute("file", s)));

            return 
                new XElement("references", 
                    new XElement("group", items));
        }


        protected override IEnumerable<DependencyInfo> GetContentFilesForNetStandard(string projectPath, List<DependencyInfo> files)
        {
            const string content = "content\\";
            const string targetDir = @"contentFiles\any\any\";

            if (files.Count == 0)
                return new List<DependencyInfo>();

            NuspecCreatorHelper.LoadProject(projectPath, out XDocument _, out var xmlns, out _);
            var result = files.Where(f =>
                    !f.Element.Attribute(xmlns + "target")?.Value?.TrimStart(content.ToCharArray())?.Contains("\\") ?? false)
                .Select(f => new DependencyInfo(f.ElementType, new XElement(f.Element))).ToList();

            foreach (var d in result)
            {
                var v = d.Element.Attribute(xmlns + "target")?.Value;
                d.Element.Attribute(xmlns + "target")?.SetValue(targetDir + v.TrimStart(content.ToCharArray()));
            }

            var includedFolders = files.Where(f =>
                    f.Element.Attribute(xmlns + "target")?.Value?.Contains(content) ?? false)
                .Select(f => f.Element.Attribute(xmlns + "target")?.Value?.TrimStart(content.ToCharArray()))
                .Where(f => f.Contains("\\")).Select(f => f.Remove(f.IndexOf("\\"))).Distinct().ToList();

            var srcDir = files.Where(f => f.Element.Attribute(xmlns + "target")?.Value?.Contains(content) ?? false)
                              .Select(f => f.Element.Attribute(xmlns + "src")?.Value)
                              .FirstOrDefault();

            if (string.IsNullOrEmpty(srcDir))
                return result;
            srcDir = srcDir.Substring(0, srcDir.IndexOf(content) + content.Length);

            result.AddRange(includedFolders.Select(s =>
                    new DependencyInfo(
                        ElementType.ContentFile,
                        new XElement("file",
                            new XAttribute("src", srcDir + s + @"\**"),
                            new XAttribute("target", targetDir + s))))
                .ToList());

            return result;
        }

        protected override IEnumerable<XElement> GetProjectReference(XElement proj, XNamespace xmlns)
        {
            return new List<XElement>();
        }

        protected override void IncludeCurrentProject(string nuspecFolder, string projectPath, bool isDebug,
            bool doIncludeSources, string preReleaseSuffixOverride, List<DependencyInfo> result, string projectFolder)
        {
        }

        protected override XElement GetMultiFrameworkDependenciesGroups(string projectPath, XElement dependencies)
        {
            var result = new XElement("dependencies", dependencies);
            var targetFrameworks = GetTargetFrameworks(projectPath);
            if (string.IsNullOrEmpty(targetFrameworks))
                return result;
            var frameworks = targetFrameworks.Split(';');

            var existingFramework = dependencies.Attribute("targetFramework")?.Value;
            if (string.IsNullOrEmpty(existingFramework))
                return result;

            foreach (var frmwrk in frameworks)
            {
                var f = FormatTargetFremwork(frmwrk);
                if (existingFramework == f)
                    continue;
                var grp = new XElement("group");
                grp.Add(new XAttribute("targetFramework", f));
                dependencies.AddBeforeSelf(grp);
            }

            return result;
        }

        private static string FormatTargetFremwork(string frmwrk)
        {
            var f = frmwrk;
            if (f.StartsWith("nets", StringComparison.OrdinalIgnoreCase))
                f = ".NETS" + f.Substring(4);
            else if (f.StartsWith("net", StringComparison.OrdinalIgnoreCase))
                f = ".NETFramework" + f.Substring(3, 1) + "." + f.Substring(4);
            if (f.EndsWith("d2"))
                f = f + ".0";
            return f;
        }

        private static string GetTargetFrameworks(string projectPath)
        {
            NuspecCreatorHelper.LoadProject(projectPath, out _, out var xmlns, out var proj);
            var propGroups = proj.Elements(xmlns + "PropertyGroup").ToList();
            var targetFrameworks = propGroups.FirstOrDefault(pg => pg.Elements("TargetFrameworks").Any())
                ?.Element("TargetFrameworks")?.Value;
            return targetFrameworks;
        }
    }
}