using System;
using System.Collections.Generic;
using IncludeNet461NoNuget;
using IncludeNetStandardNotNuget;
using IncludeNugetFramework461;
using IncludeNugetNetStandard;

namespace MultiFrameworkNuget
{
    public class MultiFramework
    {
        public void print()
        {
            Console.WriteLine(GetAllNames());
        }

        public List<string> GetAllNames()
        {
            var result = new List<string>
            {
                $"List All Included Projects in {GetType().Name} Nuget",
                new NetStandardNotNuget().GetName(),
                new Net461NoNuget().GetName(),
                new NugetNetStandard().GetName(),
                new NugetFramework461().GetName(),
            };

            return result;
        }
    }
}
