using System;
using System.Diagnostics;
using Demo.Include1;

namespace Demo.NetStandardAndNet46
{
    public class Class1
    {
        public string CallIncluded => $"{DemoInclude1.GetHello()}  was called SUCCESFULLY)";
    }
}
