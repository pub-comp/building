using System;
using System.Collections.Generic;
using System.Configuration;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PubComp.Building.NuGetPack
{
    [System.Xml.Serialization.XmlType("NuGetPack", Namespace=null)]
    public class NuGetPackConfig
    {
        /// <summary>
        /// Optional, if set to true adds framework references from NuGet project to NuGet package
        /// </summary>
        public bool AddFrameworkReferences { get; set; }

        /// <summary>
        /// Optional, if set to false does not include sources
        /// </summary>
        public bool DoIncludeSources { get; set; }

        /// <summary>
        /// Optional, if set to non-empty value, sets NuGet package icon, otherwise uses default
        /// </summary>
        public string IconUrl { get; set; }

        /// <summary>
        /// Optional, if not set (or if empty), value is taken from NuGet project's AssemblyInfo (from AssemblyTrademark)
        /// </summary>
        public string ProjectUrl { get; set; }

        /// <summary>
        /// Optional, if not set (or if empty), value is taken from NuGet project's AssemblyInfo (from AssemblyTrademark)
        /// </summary>
        public string LicenseUrl { get; set; }

        /// <summary>
        /// Optional, if not set (or if empty), value is taken from NuGet project's AssemblyInfo (from AssemblyCompany)
        /// </summary>
        public string Authors { get; set; }

        /// <summary>
        /// Optional, if not set (or if empty), value is taken from NuGet project's AssemblyInfo (from AssemblyCompany)
        /// </summary>
        public string Owners { get; set; }

        /// <summary>
        /// Optional, if not set (or if empty), value is taken from NuGet project's AssemblyInfo (from AssemblyCopyright)
        /// </summary>
        public string Copyright { get; set; }

        /// <summary>
        /// Optional, if not set (or if empty), value is taken from NuGet project's AssemblyInfo (from AssemblyDescription)
        /// </summary>
        public string Description { get; set; }

        /// <summary>
        /// Optional, if not set (or if empty), value is taken from NuGet project's AssemblyInfo (from AssemblyDescription)
        /// </summary>
        public string Summary { get; set; }

        /// <summary>
        /// Optional, if not set (or if empty), value is taken from NuGet project's AssemblyInfo (from AssemblyTitle)
        /// </summary>
        public string Keywords { get; set; }
    }
}
