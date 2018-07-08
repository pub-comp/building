using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PubComp.Building.NuGetPack
{
    internal static class NusPecCreatorFactory
    {
        internal static NuspecCreatorBase GetCreator(string projectPath)
        {
            var creator = IsProjNetStandard(projectPath) ? new NuspecCreatorNetStandard(): (NuspecCreatorBase) new NuspecCreatorNetFramework();
            return creator;
        }

        private static bool IsProjNetStandard(string projectPath)
        {
            NuspecCreatorHelper.LoadProject(projectPath, out _, out _, out var csproj);
            return csproj.Elements("PropertyGroup").Any(e => e.Elements("TargetFramework").Any());
        }
    }
}
