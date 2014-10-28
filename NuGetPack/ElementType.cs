using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PubComp.Building.NuGetPack
{
    public enum ElementType
    {
        NuGetDependency,
        FrameworkReference,
        SourceFile,
        ContentFile,
        LibraryFile,
        ToolsFile,
        BuildFile,        
        SolutionItemsFile,
    }
}
