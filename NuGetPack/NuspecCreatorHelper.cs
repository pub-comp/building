using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Linq;

namespace PubComp.Building.NuGetPack
{
    public static class NuspecCreatorHelper
    {
        public static void CreatePackages(
            string binFolder, string solutionFolder, bool isDebug,
            bool doCreatePkg = true, bool doIncludeCurrentProj = false,
            string preReleaseSuffixOverride = null)
        {
            var projects = new LinkedList<string>();
            FindProjects(solutionFolder, projects);
            
            foreach (var projectPath in projects)
            {
                var creator = NusPecCreatorFactory.GetCreator(projectPath);
                creator.GetAssemblyNameAndPath(
                    projectPath, out string assemblyName, isDebug, binFolder, out var assemblyPath);

                if (assemblyName == null)
                    continue;

                creator.CreatePackage(
                    projectPath, assemblyPath, isDebug, doCreatePkg, doIncludeCurrentProj,
                    preReleaseSuffixOverride);
            }
        }


        internal static void LoadProject(
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

        private static void FindProjects(string parentFolder, LinkedList<string> projects)
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
    }
}
