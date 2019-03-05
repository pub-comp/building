﻿using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Xml.Linq;

namespace PubComp.Building.NuGetPack
{
    public class NuspecCreatorNewCsProj : NuspecCreatorBase
    {
        public NuspecCreatorNewCsProj()
        {
            TargetFrameworkElement = "TargetFramework";
        }

        public override List<DependencyInfo> GetDependencies(
            string projectPath, out XAttribute dependenciesAttribute)
        {
            var targetFramework = GetTargetFramework(projectPath);

            dependenciesAttribute = new XAttribute("targetFramework", targetFramework);

            var result = new List<DependencyInfo>();

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
            var folders = all.Where(n => n.IndexOf("\\") >= 0).Select(f => Path.GetDirectoryName(f)?.Replace('\\', '/'))
                .Distinct().ToList();


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
            DebugOut(() => $"xmlns={xmlns}");
            var proj = csProj.Element(xmlns + "Project");
            DebugOut(() => $"proj={proj}");
            var propGroups = proj.Elements(xmlns + "PropertyGroup").ToList();
            DebugOut(() => $"propGrp={propGroups.Count}");

            var config = isDebug ? "Debug|AnyCPU" : "Release|AnyCPU";

            var outputPathElement = propGroups
                .Where(el => el.Attribute("Condition") != null
                             && el.Attribute("Condition").Value.Contains(config))
                .Elements(xmlns + "OutputPath").FirstOrDefault();

            DebugOut(() => $"outputPathElement={outputPathElement}");

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

                if (
                    !Directory.Exists(outputPath) // netStandard Projects can omit the Condition Element or any other indication of build type!
                    || !Directory.GetFiles(outputPath).Any(f =>
                        f.ToLower().EndsWith(".dll") || f.ToLower().EndsWith(".exe")))
                    outputPath = Path.Combine(projectFolder,
                        $"bin\\{(!isDebug ? "debug" : "release")}\\{targetFramework}");
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

            return directoryContainsFile && !projIgnoresFile || projIncludesFile;
        }

        /// <summary>
        ///     Add manually all files under content folder, as new csproj doesn't contain explicity all files as XML elements
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

        private XElement GetPackageDependenciesNetStandard(string projectPath, List<XElement> projDependencies)
        {
            var targetFrameworks = GetTargetFrameworks(projectPath);
            if(string.IsNullOrEmpty(targetFrameworks)) targetFrameworks = GetTargetFramework(projectPath);

            var frameworksArr = targetFrameworks.Split(';');

            var result = new XElement("dependencies");
            
            foreach (var frmw in frameworksArr)
            {
                var formattedFramework = FormatTargetFremwork(frmw);
                var group = new XElement("group",
                    new XAttribute("targetFramework", formattedFramework));
                foreach (var dep in projDependencies) group.Add(dep);
                result.Add(group);
            }
            
            //if(result == null) throw new Exception("GetPackageDependenciesNetStandard failed! Couldn't find target frameworks");            

            if (!File.Exists(projectPath))
                return null;

            NuspecCreatorHelper.LoadProject(projectPath, out _, out var xmlns, out var project);

            var condPackRef = project.Elements(xmlns + "ItemGroup").Where(e => e.Attribute(xmlns + "Condition") != null)
                .ToList();
            var nonCondPackRef = project.Elements(xmlns + "ItemGroup")
                .Where(e => e.Attribute(xmlns + "Condition") == null).Elements(xmlns + "PackageReference").ToList();

            foreach (var package in nonCondPackRef)
            {
                foreach (var group in result.Elements("group"))
                {
                    var dep = new XElement("dependency",
                        new XAttribute("id", package.Attribute("Include")?.Value ?? string.Empty),
                        new XAttribute("version", package.Attribute("Version")?.Value ?? string.Empty),
                        new XAttribute("exclude", "Build,Analyzers"));
                    group.Add(dep);    
                }
            }
                
            //result = GetMultiFrameworkDependenciesGroups(projectPath, result);
            AddConditionalPackages(condPackRef, result);

            return result;

            void AddConditionalPackages(List<XElement> conditionalPackages, XElement res)
            {
                foreach (var cond in conditionalPackages)
                {
                    var frmwrk = cond.Attribute(xmlns + "Condition").Value;
                    frmwrk = frmwrk.Substring(frmwrk.LastIndexOf(" ") + 2).Trim('\'');
                    frmwrk = FormatTargetFremwork(frmwrk);
                    var grp = res.Elements(xmlns + "group")
                        .First(g => g.Attribute(xmlns + "targetFramework")?.Value == frmwrk);
                    var packages = cond.Elements().ToList();
                    foreach (var pck in packages)
                    {
                        var toDelete = grp.Elements(xmlns + "dependency")
                            .FirstOrDefault(d =>
                                d.Attribute(xmlns + "id")?.Value == pck.Attribute(xmlns + "Include")?.Value);
                        toDelete?.Remove();

                        var version = pck.Attribute("Version")?.Value;
                        if (version!=null)
                        {
                            grp.Add(
                                new XElement("dependency",
                                    new XAttribute("id", pck.Attribute("Include")?.Value ?? string.Empty),
                                    new XAttribute("version", version),
                                    new XAttribute("exclude", "Build,Analyzers")));
                        }
                        
                    }
                }
            }
        }

        private List<XElement> GetProjectDependenciesNetStandard(string projectPath, string preReleaseSuffixOverride, string FinalVersion)
        {
            var result = new List<XElement>();

            if (!File.Exists(projectPath))
                return result;

            var projref = GetProjectIncludeFiles(projectPath, out var verList, out var xmlns, out var proj, true);

            if (projref == null || projref.Count == 0)
                return result;

            for (var i = 0; i < projref.Count; i++)
            {
                var dependantFile = projref[i];
                var ver = verList[i] + (string.IsNullOrEmpty(preReleaseSuffixOverride) ? string.Empty : $"-{preReleaseSuffixOverride}");

                result.Add(
                    new XElement("dependency",
                        new XAttribute("id", dependantFile),
                        new XAttribute("version", ver),
                        new XAttribute("exclude", "Build,Analyzers")));
            }

            return result;
        }

        protected override XElement GetDependenciesForNewCsProj(string projectPath, XElement dependencies, string preReleaseSuffixOverride,string FinalVersion)
        {
            var projDependencies = GetProjectDependenciesNetStandard(projectPath, preReleaseSuffixOverride,FinalVersion);
            var result = GetPackageDependenciesNetStandard(projectPath, projDependencies);

            return result;
        }


        private List<string> GetProjectIncludeFiles(string projectPath, out List<string> verList, out XNamespace xmlns,
            out XElement proj, bool isFileNuget)
        {
            verList = new List<string>();
            NuspecCreatorHelper.LoadProject(projectPath, out _, out xmlns, out proj);
            var x = xmlns;
            var projref = proj?.Elements(xmlns + "ItemGroup")?.Elements(xmlns + "ProjectReference")?.Where(pr =>
                {
                    var file = pr.Attribute(x + "Include")?.Value ?? pr.LastAttribute.Value;
                    file = Path.Combine(Path.GetDirectoryName(projectPath) ?? string.Empty, file);
                    file = Path.Combine(Path.GetDirectoryName(file) ?? "", "NuGetPack.config");
                    return isFileNuget ? File.Exists(file) : !File.Exists(file);
                })
                .Select(f => f.Attribute(x + "Include")?.Value ?? f.LastAttribute.Value).ToList();

            if (projref.Count == 0)
                return projref;

            for (var i = 0; i < projref.Count; i++)
            {
                var pr = projref[i];
                var incProj = Path.Combine(Path.GetDirectoryName(projectPath) ?? string.Empty, pr);
                NuspecCreatorHelper.LoadProject(incProj, out _, out x, out var prj);
                var ver = prj?.Element(xmlns + "PropertyGroup")?.Element(xmlns + "Version")?.Value;
                ver = ver ?? prj?.Element(xmlns + "PropertyGroup")?.Element(xmlns + "FileVersion")?.Value;
                ver = ver ?? prj?.Element(xmlns + "PropertyGroup")?.Element(xmlns + "AssemblyVersion")?.Value;
                if (string.IsNullOrEmpty(ver))
                {
                    var dllFile = GetDllNameFromProjectName(incProj);
                    if (!string.IsNullOrEmpty(dllFile))
                    {
                        ver = ver ?? FileVersionInfo.GetVersionInfo(dllFile).FileVersion;
                        ver = ver ?? FileVersionInfo.GetVersionInfo(dllFile).ProductVersion;
                    }
                }
                ver = ver ?? "1.0.0";
                verList.Add(ver);

                var asem = GetAssemblyName(incProj);
                projref[i] = asem;
            }

            return projref;
        }

        private static string GetDllNameFromProjectName(string projFile)
        {
            var path = Path.GetDirectoryName(projFile);
            var fileName = $"*{Path.GetFileNameWithoutExtension(projFile)}.DLL";
            fileName = Directory.EnumerateFiles(path, fileName, SearchOption.AllDirectories).FirstOrDefault();
            return fileName;
        }

        public override List<DependencyInfo> GetBinaryFiles(
            string nuspecFolder, string projectFolder, string projectPath, bool isDebug)
        {
            NuspecCreatorHelper.LoadProject(projectPath, out var csProj, out _, out _);

            var outputPath = GetOutputPath(csProj, isDebug, projectFolder);

            if (!Directory.Exists(outputPath)
                || !Directory.GetFiles(outputPath).Any(f =>
                    f.ToLower().EndsWith(".dll") || f.ToLower().EndsWith(".exe"))
            )
                outputPath = nuspecFolder;

            outputPath = SlnOutputFolder ?? outputPath;


            var frameworks = GetProject(projectPath, outputPath, out var pathHeader);

            var files = new List<string>();
            var includeFiles = GetProjectIncludeFiles(projectPath, out _, out _, out _, false);
            var outPath = outputPath.TrimEnd('\\').ToLower();
            outPath = outPath.EndsWith(frameworks[0].ToLower())
                ? outPath.Substring(0, outPath.Length - frameworks[0].Length)
                : outputPath;
            foreach (var framework in frameworks)
            {
                files.AddRange(includeFiles
                    .Where(f => File.Exists(Path.Combine(outPath, Path.Combine(framework, f + ".DLL"))))
                    .Select(f => outputPath != outPath
                        ? $"..\\{framework}\\{f}.DLL"
                        : $"{framework}\\{f}.DLL").ToList());
                files.AddRange(includeFiles
                    .Where(f => File.Exists(Path.Combine(outPath, Path.Combine(framework, f + ".PDB"))))
                    .Select(f => outputPath != outPath
                        ? $"..\\{framework}\\{f}.PDB"
                        : $"{framework}\\{f}.PDB").ToList());
            }

            var items = CreateBinFilesDepInfoList(frameworks, files);
            files.Clear();

            outPath = SlnOutputFolder != null ? Path.Combine(outputPath, frameworks.First()) : outputPath;

            if (frameworks.Length < 2 &&
                Directory.GetFiles(Path.Combine(outPath, pathHeader)).Any(f =>
                    f.ToLower().EndsWith(".dll") || f.ToLower().EndsWith(".exe")))
            {
                files = GetProjectBinaryFiles(projectPath, outPath)
                    .Select(Path.GetFileName).ToList();
                files = files.Select(f =>
                    SlnOutputFolder == null ? $"..\\{frameworks[0]}\\{f}" : $"{frameworks[0]}\\{f}").ToList();
                items.AddRange(CreateBinFilesDepInfoList(frameworks, files));
            }
            else
            {
                foreach (var framework in frameworks)
                    files.AddRange(GetProjectBinaryFiles(projectPath,
                            Path.Combine(outputPath, SlnOutputFolder == null ? $"..\\{framework}" : framework))
                        .Select(f =>
                        {
                            var frmwrk = f.Substring(Path.GetDirectoryName(Path.GetDirectoryName(f)).Length)
                                .TrimStart('\\');
                            var prevDir = !string.IsNullOrEmpty(SlnOutputFolder) ? string.Empty : @"..\";
                            return prevDir + frmwrk;
                        }).ToList());

                items.AddRange(files
                    .Select(s =>
                        new DependencyInfo(
                            ElementType.LibraryFile,
                            new XElement("file",
                                new XAttribute("src", s),
                                new XAttribute("target", $"lib\\{ExtractFramework(s)}"))))
                    .ToList());
            }

            return items;
        }

        private string[] GetProject(string projectPath, string outputPath, out string pathHeader)
        {
            string[] frameworks;
            pathHeader = string.Empty;
            var targetFrameworks = GetTargetFrameworks(projectPath);
            if (string.IsNullOrEmpty(targetFrameworks))
            {
                frameworks = new[] {GetTargetFramework(projectPath).TrimStart('.')};
            }
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

            return frameworks;
        }

        private string ExtractFramework(string file)
        {
            var lastBSlesh = file.LastIndexOf('\\');
            if (lastBSlesh < 1)
                return file;

            var result = file.Substring(0, lastBSlesh);
            lastBSlesh = result.LastIndexOf('\\');

            return lastBSlesh < 0 ? result : result.Substring(lastBSlesh + 1);
        }

        private List<DependencyInfo> CreateBinFilesDepInfoList(string[] frameworks, List<string> files)
        {
            var result = new List<DependencyInfo>();
            result.AddRange(files
                .Select(s =>
                    new DependencyInfo(
                        ElementType.LibraryFile,
                        new XElement("file",
                            new XAttribute("src", s),
                            new XAttribute("target", Path.Combine($"lib\\{ExtractFramework(s)}")))))
                .ToList());

            return result;
        }

        public override XElement GetReferencesFiles(string projectPath)
        {
            var includeFiles = GetProjectIncludeFiles(projectPath, out _, out _, out _, false);
            var files = includeFiles.Select(pr => pr + ".DLL").ToList();

            if (files.Count == 0)
                return null;

            var projDllFile = GetDllNameFromProjectName(projectPath);
            projDllFile = Path.GetFileName(projDllFile);
            files.Add(projDllFile);

             var items = files
                .Select(s =>
                    new XElement("reference",
                        new XAttribute("file", s)));

            return
                new XElement("references",
                    new XElement("group", items));
        }

        protected override string GetContentFileTarget(XElement el, XNamespace xmlns)
        {
            return el.Attribute("Link")?.Value;
        }

        protected override IEnumerable<DependencyInfo> GetContentFilesForNetStandard(string projectPath,
            List<DependencyInfo> files)
        {
            const string content = "content\\";
            const string targetDir = @"contentFiles\any\any\";

            if (files.Count == 0)
                return new List<DependencyInfo>();

            NuspecCreatorHelper.LoadProject(projectPath, out _, out var xmlns, out _);
            var result = files.Where(f =>
                    !f.Element.Attribute(xmlns + "target")?.Value?.TrimStart(content.ToCharArray())?.Contains("\\") ??
                    false)
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
            bool doIncludeSources, string preReleaseSuffixOverride,string FinalVersion, List<DependencyInfo> result, string projectFolder)
        {
        }

        private static string FormatTargetFremwork(string frmwrk)
        {
            var f = frmwrk;
            if (f.StartsWith("nets", StringComparison.OrdinalIgnoreCase))
                f = ".NETS" + f.Substring(4);
            else if (f.StartsWith("net", StringComparison.OrdinalIgnoreCase))
            {
                var versionString = f.Substring(3);
                f = ".NETFramework";
                foreach (var digitStr in versionString)
                {
                    f +=  $"{digitStr}.";    
                }
                f = f.Remove(f.Length - 1);
            }
                
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