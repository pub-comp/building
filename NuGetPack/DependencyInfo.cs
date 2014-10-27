using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Linq;

namespace PubComp.Building.NuGetPack
{
    public class DependencyInfo
    {
        public ElementType ElementType { get; set; }
        public XElement Element { get; set; }

        public DependencyInfo()
        {
        }

        public DependencyInfo(ElementType elementType, XElement element)
        {
            this.ElementType = elementType;
            this.Element = element;
        }
    }
}
