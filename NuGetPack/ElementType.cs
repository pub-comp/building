using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PubComp.Building.NuGetPack
{
    public enum ElementType
    {
        SourceFile,
        ContentFile,
        LibraryFile,
        ToolsFile,
        BuildFile,
        FrameworkReference,
        AssemblyReference,
        NuGetDependency
    }
}
