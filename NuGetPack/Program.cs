using System;

namespace PubComp.Building.NuGetPack
{
    /// <summary>
    /// Run as post-build on a project
    /// e.g. NuGetPack.exe ""$(ProjectPath)"" ""$(TargetPath)"" $(ConfigurationName)"
    /// </summary>
    public class Program
    {
        public static void Main(string[] args)
        {
            string projPath, dllPath;
            bool isDebug;

            if (!TryParseArguments(args, out projPath, out dllPath, out isDebug))
            {
                WriteError();
                return;
            }

            var creator = new NuspecCreator();
            creator.CreatePackage(projPath, dllPath, isDebug);
        }

        public static bool TryParseArguments(string[] args, out string projPath, out string dllPath, out bool isDebug)
        {
            projPath = null;
            dllPath = null;
            isDebug = false;

            string config = null;

            if (args.Length < 2 || args.Length > 3)
                return false;

            foreach (var arg in args)
            {
                if (arg.EndsWith(".csproj"))
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
            }

            isDebug = (config ?? string.Empty).ToLower() == "debug";

            if (args.Length == 2 && (projPath == null || dllPath == null))
                return false;

            if (args.Length == 3 && (projPath == null || dllPath == null || config == null))
                return false;

            return true;
        }

        private static void WriteError()
        {
            Console.WriteLine(@"Correct usage: NuGetPack.exe <pathToCsProj> <pathToDll> [<Debug|Release>]");
            Console.WriteLine(@"Via post build event: NuGetPack.exe ""$(ProjectPath)"" ""$(TargetPath)"" $(ConfigurationName)");
        }
    }
}
