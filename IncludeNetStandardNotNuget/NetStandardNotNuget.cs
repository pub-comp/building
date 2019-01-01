using System;

namespace IncludeNetStandardNotNuget
{
    public class NetStandardNotNuget
    {
        public string GetName()
        {
            return this.GetType().Name;
        }
    }
}
