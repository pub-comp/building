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
            var framework = GetTargetFramework(projectPath);
            framework = framework.TrimStart('.');

            var result = contentElements
                .Select(s => Path.GetFileName(s.src))
                .Select(s =>
                    new XElement("files",
                        new XAttribute("include", $"any/{framework}/" + s),
                        new XAttribute("buildAction", "Content"))).ToList();

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
                    .Element("TargetFramework");
                outputPath = Path.Combine(projectFolder,
                    $"bin\\{(isDebug ? "debug" : "release")}\\{targetFramework.Value}");
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

            XNamespace xmlns;
            XElement proj;
            NuspecCreatorHelper.LoadProject(projectPath, out XDocument _, out xmlns, out proj);
            //var projref = project.XPathSelectElements("//ItemGroup//ProjectReference").ToList();
            var projref = proj?.Elements(xmlns + "ItemGroup")?.Elements(xmlns + "ProjectReference")?.ToList();

            if (projref == null || projref.Count == 0)
                return result;

            foreach (var pr in projref)
            {
                var incProj = pr.Attribute(xmlns + "Include")?.Value ?? pr.LastAttribute.Value;
                if (string.IsNullOrWhiteSpace(incProj))
                    continue;

                incProj = Path.Combine(Path.GetDirectoryName(projectPath) ?? String.Empty, incProj);
                NuspecCreatorHelper.LoadProject(incProj, out XDocument _, out xmlns, out proj);
                var asem = proj?.Element(xmlns + "PropertyGroup")?.Element(xmlns + "AssemblyName")?.Value;
                if (string.IsNullOrWhiteSpace(asem))
                {
                    asem = Path.ChangeExtension(Path.GetFileName(projectPath), "");
                    asem = asem.TrimEnd('.');
                }

                //var ver = proj.XPathSelectElement("//PropertyGroup//Version")?.Value ?? "1.0.0"; ?? proj?.Element(xmlns + "PropertyGroup")?.LastAttribute?.Value 
                var ver = proj?.Element(xmlns + "PropertyGroup")?.Element(xmlns + "Version")?.Value ?? "1.0.0";

                result.Add(
                    new DependencyInfo(
                        ElementType.NuGetDependency,
                        new XElement("dependency",
                            new XAttribute("id", asem),
                            new XAttribute("version", ver),
                            new XAttribute("exclude", "Build,Analyzers"))));
            }

            return result;
        }

        public override List<DependencyInfo> GetBinaryFiles(
            string nuspecFolder, string projectFolder, string projectPath)
        {
            var files = new List<string>();

            XNamespace xmlns;
            XElement proj;
            NuspecCreatorHelper.LoadProject(projectPath, out XDocument csProj, out xmlns, out proj);

            var asem = proj?.Element(xmlns + "PropertyGroup")?.Element(xmlns + "AssemblyName")?.Value;

            if (string.IsNullOrWhiteSpace(asem))
                asem = Path.GetFileName(projectPath)?.Replace(".csproj", String.Empty);
            files.Add(asem + ".DLL");
            files.Add(asem + ".PDB");

            var framework = GetTargetFramework(projectPath);
            framework = framework.TrimStart('.');

            var items = files
                .Select(s =>
                    new DependencyInfo(
                        ElementType.LibraryFile,
                        new XElement("file",
                            new XAttribute("src", s),
                            new XAttribute("target", Path.Combine($"lib\\{framework}")))))
                .ToList();

            return items;
        }


        protected override IEnumerable<XElement> GetProjectReference(XElement proj, XNamespace xmlns)
        {
            return new List<XElement>();
        }
    }
}