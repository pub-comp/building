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
        public bool AddFrameworkReferences { get; set; }
        public string IconUrl { get; set; }
    }
}
