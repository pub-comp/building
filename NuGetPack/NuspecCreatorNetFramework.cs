using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Linq;

namespace PubComp.Building.NuGetPack
{
    public class NuspecCreatorNetFramework : NuspecCreatorBase
    {
        public NuspecCreatorNetFramework()
        {
            TargetFrameworkElement = "TargetFrameworkVersion";
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

        public override List<DependencyInfo> GetDependencies(string projectPath, out XAttribute dependenciesAttribute)
        {
            var packagesFiles = new[] { Path.GetDirectoryName(projectPath) + @"\packages.config", Path.GetDirectoryName(projectPath) + @"\internalPackages.config"};

            return GetDependencies(projectPath, packagesFiles, out dependenciesAttribute);
        }

        public override List<DependencyInfo> GetBinaryFiles(
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

        protected override string GetContentFileTarget(XElement el,XNamespace xmlns)
        {
            return el.Elements(xmlns + "Link").FirstOrDefault()?.Value;
        }

        protected override string FormatFrameworkVersion(string targetFrameworkVersion)
        {
            var result = targetFrameworkVersion
                .Replace("v", string.Empty)
                .Replace(".", string.Empty)
                .Replace("net", string.Empty);
            return result;
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

            var outputPath = Path.Combine(projectFolder, outputPathElement.Value);
            

            return outputPath;
        }

        protected override bool DoesProjectContainFile(string projectPath,string file,IEnumerable<XElement> noneElements,IEnumerable<XElement> codeElements)
        {
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
    }
}
