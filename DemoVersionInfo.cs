using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

// This should be the same version as below (1.4.1.0), however, for test purposes it is different
[assembly: AssemblyFileVersion("1.3.2.0")]

#if DEBUG
[assembly: AssemblyInformationalVersion("1.4.1-Test")]
#else
[assembly: AssemblyInformationalVersion("1.4.1")]
#endif
