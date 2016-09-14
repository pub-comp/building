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
            Mode mode;
            string projPath, dllPath, binFolder, solutionFolder;
            bool isDebug, doCreatePkg, doIncludeCurrentProj;


            if (!TryParseArguments(
                args, out mode, out projPath, out dllPath, out binFolder, out solutionFolder,
                out isDebug, out doCreatePkg, out doIncludeCurrentProj))
            {
                WriteError();
                return;
            }

            var creator = new NuspecCreator();

            if (mode != Mode.Solution)
            {
                creator.CreatePackage(projPath, dllPath, isDebug, doCreatePkg, doIncludeCurrentProj);
            }
            else
            {
                creator.CreatePackages(binFolder, solutionFolder, isDebug, doCreatePkg, doIncludeCurrentProj);
            }
        }

        public enum Mode { Solution, Project };

        public static bool TryParseArguments(
            string[] args,
            out Mode mode,
            out string projPath, out string dllPath, out string binFolder, out string solutionFolder,
            out bool isDebug, out bool doCreateNuPkg, out bool doIncludeCurrentProj)
        {
            mode = Mode.Project;
            Mode? modeVar = null;
            projPath = null;
            dllPath = null;
            isDebug = false;
            doCreateNuPkg = true;
            doIncludeCurrentProj = false;
            binFolder = null;
            solutionFolder = null;

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

            return true;
        }

        private static void WriteError()
        {
            Console.WriteLine(@"Correct usage: NuGetPack.exe [project] <pathToCsProj> <pathToDll> [<Debug|Release>] [nopkg]");
            Console.WriteLine(@"Via post build event: NuGetPack.exe [project] ""$(ProjectPath)"" ""$(TargetPath)"" $(ConfigurationName)");
            Console.WriteLine(@"or: NuGetPack.exe [project] ""$(ProjectPath)"" ""$(TargetPath)"" $(ConfigurationName) nopkg");
            Console.WriteLine();
            Console.WriteLine(@"or for solution level:");
            Console.WriteLine();
            Console.WriteLine(@"Correct usage: NuGetPack.exe solution bin=<binFolder> src=<solutionFolder> [<Debug|Release>] [nopkg]");
        }
    }
}
