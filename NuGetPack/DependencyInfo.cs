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

    class DependencyInfoComparer : IEqualityComparer<DependencyInfo>
    {
        #region IEqualityComparer<XElement> Members

        public bool Equals(DependencyInfo x, DependencyInfo y)
        {
            return (string)x.Element.FirstAttribute.Value.ToLower() == (string)y.Element.FirstAttribute.Value.ToLower();
        }

        public int GetHashCode(DependencyInfo obj)
        {
            return ((string)obj.Element.FirstAttribute.Value.ToLower()).GetHashCode();
        }
        
        #endregion
    }

    class XElementComparer : IEqualityComparer<XElement>
    {
        #region IEqualityComparer<XElement> Members

        public bool Equals(XElement x, XElement y)
        {
            return (string)x.FirstAttribute == (string)y.FirstAttribute;
        }

        public int GetHashCode(XElement obj)
        {
            return ((string)obj.FirstAttribute).GetHashCode();
        }

        #endregion
    }
}
