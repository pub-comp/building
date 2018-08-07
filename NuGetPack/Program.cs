using System;

namespace PubComp.Building.NuGetPack
{
    /// <summary>
    /// Run as post-build on a project
    /// e.g. NuGetPack.exe ""$(ProjectPath)"" ""$(TargetPath)"" $(ConfigurationName) [nopkg]"
    /// </summary>
    public class Program
    {
        public static void Main(string[] args)
        {
            CommandLineArguments cla;

            if (!TryParseArguments(args, out cla))
            {
                WriteError();
                return;
            }

            if (cla.Mode != Mode.Solution)
            {
                NusPecCreatorFactory.GetCreator(cla.ProjPath).CreatePackage(
                    cla.ProjPath, cla.DllPath, cla.IsDebug, cla.DoCreateNuPkg, cla.DoIncludeCurrentProj, cla.PreReleaseSuffixOverride);
            }
            else
            {
                NuspecCreatorHelper.CreatePackages(
                    cla.BinFolder, cla.SolutionFolder, cla.IsDebug, cla.DoCreateNuPkg, cla.DoIncludeCurrentProj, cla.PreReleaseSuffixOverride);
            }

            //Console.ReadKey();
        }

        public enum Mode { Solution, Project };

        public class CommandLineArguments
        {
            public Mode Mode { get; set; }
            public string ProjPath { get; set; }
            public string DllPath { get; set; }
            public string BinFolder { get; set; }
            public string SolutionFolder { get; set; }
            public bool IsDebug { get; set; }
            public bool DoCreateNuPkg { get; set; }
            public bool DoIncludeCurrentProj { get; set; }
            public string PreReleaseSuffixOverride { get; set; }
        }

        public static bool TryParseArguments(
            string[] args,
            out CommandLineArguments commandLineArguments)
        {
            Mode mode = Mode.Project;
            Mode? modeVar = null;
            string projPath = null;
            string dllPath = null;
            bool isDebug = false;
            bool doCreateNuPkg = true;
            bool doIncludeCurrentProj = false;
            string binFolder = null;
            string solutionFolder = null;
            string preReleaseSuffixOverride = null;
            commandLineArguments = null;

            string config = null;

            foreach (var arg in args)
            {
                if (arg.ToLower() == "solution")
                {
                    if (modeVar.HasValue)
                        return false;

                    modeVar = Mode.Solution;
                }
                else if (arg.ToLower() == "project")
                {
                    if (modeVar.HasValue)
                        return false;

                    modeVar = Mode.Project;
                }
                else if (arg.ToLower().StartsWith("bin="))
                {
                    if (binFolder != null)
                        return false;

                    binFolder = arg.Substring(4);
                }
                else if (arg.ToLower().StartsWith("sln=") || arg.ToLower().StartsWith("src="))
                {
                    if (solutionFolder != null)
                        return false;

                    solutionFolder = arg.Substring(4);
                }
                else if (arg.EndsWith(".csproj"))
                {
                    if (projPath != null)
                        return false;

                    projPath = arg;
                }
                else if (arg.EndsWith(".dll"))
                {
                    if (dllPath != null)
                        return false;

                    dllPath = arg;
                }
                else if (arg.ToLower() == "debug" || arg.ToLower() == "release")
                {
                    if (config != null)
                        return false;

                    config = arg;
                }
                else if (arg.ToLower() == "nopkg")
                {
                    if (doCreateNuPkg == false)
                        return false;

                    doCreateNuPkg = false;
                }
                else if (arg.ToLower() == "includecurrentproj")
                {
                    if (doIncludeCurrentProj == true)
                        return false;

                    doIncludeCurrentProj = true;
                }
                else if (arg.ToLower().StartsWith("pre=") || arg.ToLower().StartsWith("prereleasesuffixoverride="))
                {
                    if (preReleaseSuffixOverride != null)
                        return false;

                    preReleaseSuffixOverride = arg.Substring(arg.IndexOf('=') + 1);

                    if (preReleaseSuffixOverride.StartsWith("-"))
                        preReleaseSuffixOverride = preReleaseSuffixOverride.Substring(1);
                }
                else
                {
                    return false;
                }
            }

            if (modeVar != Mode.Solution)
            {
                if (projPath == null || dllPath == null)
                    return false;

                if (binFolder != null || solutionFolder != null)
                    return false;
            }
            else
            {
                if (binFolder == null || solutionFolder == null)
                    return false;

                if (projPath != null || dllPath != null)
                    return false;
            }

            mode = modeVar ?? Mode.Project;
            isDebug = (config ?? string.Empty).ToLower() == "debug";

            commandLineArguments = new CommandLineArguments
            {
                Mode = mode,
                ProjPath = projPath,
                DllPath = dllPath,
                BinFolder = binFolder,
                SolutionFolder = solutionFolder,
                IsDebug = isDebug,
                DoCreateNuPkg = doCreateNuPkg,
                DoIncludeCurrentProj = doIncludeCurrentProj,
                PreReleaseSuffixOverride = preReleaseSuffixOverride,
            };

            return true;
        }

        private static void WriteError()
        {
            Console.WriteLine(
                @"Correct usage: NuGetPack.exe [project] <pathToCsProj> <pathToDll> [<Debug|Release>] [nopkg] [pre=|preReleaseSuffixOverride=<suffixForPreRelease>]");
            Console.WriteLine(
                @"Via post build event: NuGetPack.exe [project] ""$(ProjectPath)"" ""$(TargetPath)"" $(ConfigurationName)");
            Console.WriteLine(
                @"or: NuGetPack.exe [project] ""$(ProjectPath)"" ""$(TargetPath)"" $(ConfigurationName) nopkg");
            Console.WriteLine();
            Console.WriteLine(
                @"or for solution level:");
            Console.WriteLine();
            Console.WriteLine(
                @"Correct usage: NuGetPack.exe solution bin=<binFolder> src=<solutionFolder> [<Debug|Release>] [nopkg]");
        }
    }
}
