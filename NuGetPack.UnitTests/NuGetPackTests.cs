using System.IO;
using System.Linq;
using System.Xml.Linq;
using System.Xml.XPath;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace PubComp.Building.NuGetPack.UnitTests
{
    [TestClass]
    public class NuGetPackTests
    {
        private static bool isLocal;
        private static string proj1Csproj;
        private static string proj1Dll;
        private static string proj2Csproj;
        private static string proj2Dll;

#if DEBUG
        private const bool isDebug = true;
#else
        private const bool isDebug = false;
#endif

        #region Initialization and Cleanup

        [ClassInitialize]
        public static void ClassInitialize(TestContext testContext)
        {
            string rootPath, testSrcDir, testBinDir, testRunDir;

            TestResourceFinder.FindResources(testContext, "Building",
                @"Demo.NuGet",
                true, out rootPath, out testSrcDir, out testBinDir, out testRunDir, out isLocal);

            proj1Csproj = testSrcDir + @"\Demo.NuGet.csproj";
            proj1Dll = testBinDir + @"\PubComp.Building.Demo.NuGet.dll";

            TestResourceFinder.FindResources(testContext, "Building",
                @"NuGetPack",
                true, out rootPath, out testSrcDir, out testBinDir, out testRunDir, out isLocal);

            TestResourceFinder.CopyResources(testBinDir, testRunDir);

            proj2Csproj = testSrcDir + @"\NuGetPack.csproj";
            proj2Dll = testBinDir + @"\PubComp.Building.NuGetPack.dll";

            TestResourceFinder.FindResources(testContext, "Building",
                @"NuGetPack.UnitTests",
                true, out rootPath, out testSrcDir, out testBinDir, out testRunDir, out isLocal);

            TestResourceFinder.CopyResources(testBinDir, testRunDir);
        }

        public TestContext TestContext
        {
            get;
            set;
        }

        #endregion

        [TestMethod]
        public void TestGetDependenciesOuter()
        {
            var packagesFile = Path.GetDirectoryName(proj1Csproj) + @"\packages.config";

            var creator = new NuspecCreator();
            var results = creator.GetDependencies(new[] { packagesFile });

            LinqAssert.Count(results, 3);

            LinqAssert.Any(results, obj =>
                obj is XAttribute && ((XAttribute)obj).Name == "targetFramework" && ((XAttribute)obj).Value == "net45");

            LinqAssert.Any(results, obj =>
                obj is XElement && ((XElement)obj).Name == "dependency"
                    && ((XElement)obj).Attribute("id").Value == "Common.Logging.DV"
                    && ((XElement)obj).Attribute("version").Value == "2.2.0");

            LinqAssert.Any(results, obj =>
                obj is XElement && ((XElement)obj).Name == "dependency"
                    && ((XElement)obj).Attribute("id").Value == "FakeItEasy"
                    && ((XElement)obj).Attribute("version").Value == "1.22.0");
        }

        [TestMethod]
        public void TestGetDependenciesInner()
        {
            var packagesFile = Path.GetDirectoryName(proj1Csproj) + @"\packages.config";

            var creator = new NuspecCreator();
            var results = creator.GetDependencies(packagesFile);

            LinqAssert.Count(results, 2);

            LinqAssert.Any(results, obj =>
                obj is XElement && ((XElement)obj).Name == "dependency"
                    && ((XElement)obj).Attribute("id").Value == "Common.Logging.DV"
                    && ((XElement)obj).Attribute("version").Value == "2.2.0");

            LinqAssert.Any(results, obj =>
                obj is XElement && ((XElement)obj).Name == "dependency"
                    && ((XElement)obj).Attribute("id").Value == "FakeItEasy"
                    && ((XElement)obj).Attribute("version").Value == "1.22.0");
        }

        [TestMethod]
        public void TestGetContentFiles()
        {
            var creator = new NuspecCreator();
            var results = creator.GetContentFiles(Path.GetDirectoryName(proj1Dll), @"..\..", proj1Csproj);

            LinqAssert.Count(results, 3);

            LinqAssert.Any(results, el =>
                el.Name == "file"
                && el.Attribute("src").Value == @"..\..\..\Data.txt"
                    && el.Attribute("target").Value == @"content\Data.txt",
                "Found: " + results.First());

            LinqAssert.Any(results, el =>
                el.Name == "file"
                && el.Attribute("src").Value == @"..\..\Info.txt"
                    && el.Attribute("target").Value == @"content\Info.txt",
                "Found: " + results.First());

            LinqAssert.Any(results, el =>
                el.Name == "file"
                && el.Attribute("src").Value == @"..\..\SubContent\Other.txt"
                    && el.Attribute("target").Value == @"content\SubContent\Other.txt",
                "Found: " + results.First());
        }

        [TestMethod]
        public void TestGetBinaryFiles()
        {
            var creator = new NuspecCreator();
            var results = creator.GetBinaryFiles(Path.GetDirectoryName(proj2Dll), @"..\..", proj2Csproj, isDebug, Path.GetDirectoryName(proj2Dll));

            var path = isLocal ? @"..\..\bin\" : @"..\bin\";

            if (isLocal)
            {
#if DEBUG
                path += @"Debug\";
#else
                path += @"Release\";
#endif
            }

            LinqAssert.Count(results, 2);

            LinqAssert.Any(results, el =>
                el.Name == "file"
                && el.Attribute("src").Value == path + @"NuGetPack.exe"
                    && el.Attribute("target").Value == @"lib\net45\NuGetPack.exe",
                "Expected: " + path + @"NuGetPack.exe " + ", Found: " + results.First());

            LinqAssert.Any(results, el =>
                el.Name == "file"
                && el.Attribute("src").Value == path + @"NuGetPack.pdb"
                    && el.Attribute("target").Value == @"lib\net45\NuGetPack.pdb",
                "Expected: " + path + @"NuGetPack.pdb " + ", Found: " + results.First());
        }

        [TestMethod]
        public void TestGetSourceFiles()
        {
            var creator = new NuspecCreator();
            var results = creator.GetSourceFiles(Path.GetDirectoryName(proj2Dll), @"..\..", proj2Csproj);

            LinqAssert.Count(results, 4);

            LinqAssert.Any(results, el =>
                el.Name == "file"
                && el.Attribute("src").Value == @"..\..\Properties\AssemblyInfo.cs"
                    && el.Attribute("target").Value == @"src\NuGetPack\Properties\AssemblyInfo.cs",
                "Found: " + results.First());

            LinqAssert.Any(results, el =>
                el.Name == "file"
                && el.Attribute("src").Value == @"..\..\App.config"
                    && el.Attribute("target").Value == @"src\NuGetPack\App.config",
                "Found: " + results.First());

            LinqAssert.Any(results, el =>
                el.Name == "file"
                && el.Attribute("src").Value == @"..\..\NuspecCreator.cs"
                    && el.Attribute("target").Value == @"src\NuGetPack\NuspecCreator.cs",
                "Found: " + results.First());

            LinqAssert.Any(results, el =>
                el.Name == "file"
                && el.Attribute("src").Value == @"..\..\Program.cs"
                    && el.Attribute("target").Value == @"src\NuGetPack\Program.cs",
                "Found: " + results.First());
        }

        [TestMethod]
        public void TestGetReferences()
        {
            var nuspecFolder = Path.GetDirectoryName(proj1Dll);
            var projFolder = Path.GetDirectoryName(proj1Csproj);

            var creator = new NuspecCreator();
            var results = creator.GetReferences(nuspecFolder, proj1Csproj, projFolder);

            LinqAssert.Count(results, 1);
            LinqAssert.Any(results, r => r == @"..\NuGetPack\NuGetPack.csproj");
        }

        [TestMethod]
        public void TestGetFiles()
        {
            var nuspecFolder = Path.GetDirectoryName(proj1Dll);

            var creator = new NuspecCreator();
            var results = creator.GetFiles(nuspecFolder, proj1Csproj, isDebug);

            Assert.AreNotEqual(0, results.Count());

            LinqAssert.All(results, r => File.Exists(Path.Combine(nuspecFolder, r.Attribute("src").Value)));
        }

        [TestMethod]
        public void TestCreateNuspec()
        {
            var creator = new NuspecCreator();
            var nuspec = creator.CreateNuspec(proj1Csproj, proj1Dll, isDebug);
            Assert.IsNotNull(nuspec);
        }

        [TestMethod]
        public void TestCreatePackage()
        {
            var creator = new NuspecCreator();
            creator.CreatePackage(proj1Csproj, proj1Dll, isDebug);

            var nuspecPath = Path.ChangeExtension(proj1Dll, ".nuspec");

            Assert.IsTrue(File.Exists(nuspecPath));
        }

        [TestMethod]
        public void TestParseVersion()
        {
            var creator = new NuspecCreator();
            var nuspec = creator.CreateNuspec(proj1Csproj, proj1Dll, isDebug);
            
            Assert.IsNotNull(nuspec);

            var version = nuspec.XPathSelectElement(@"/package/metadata/version").Value;

            #if DEBUG
                Assert.AreEqual("1.3.2-PreRelease", version);
            #else
                Assert.AreEqual("1.3.2", version);
            #endif
        }

        [TestMethod]
        public void TestParseName()
        {
            var creator = new NuspecCreator();
            var nuspec = creator.CreateNuspec(proj1Csproj, proj1Dll, isDebug);

            Assert.IsNotNull(nuspec);

            var version = nuspec.XPathSelectElement(@"/package/metadata/title").Value;

            Assert.AreEqual("PubComp.Building.Demo", version);
        }

        [TestMethod]
        public void TestParseDescription()
        {
            var creator = new NuspecCreator();
            var nuspec = creator.CreateNuspec(proj1Csproj, proj1Dll, isDebug);

            Assert.IsNotNull(nuspec);

            var version = nuspec.XPathSelectElement(@"/package/metadata/description").Value;

            Assert.AreEqual("Description goes here", version);
        }

        [TestMethod]
        public void TestParseTags()
        {
            var creator = new NuspecCreator();
            var nuspec = creator.CreateNuspec(proj1Csproj, proj1Dll, isDebug);

            Assert.IsNotNull(nuspec);

            var version = nuspec.XPathSelectElement(@"/package/metadata/tags").Value;

            Assert.AreEqual("Keywords, go, here", version);
        }

        [TestMethod]
        public void TestParseProjectUrl()
        {
            var creator = new NuspecCreator();
            var nuspec = creator.CreateNuspec(proj1Csproj, proj1Dll, isDebug);

            Assert.IsNotNull(nuspec);

            var version = nuspec.XPathSelectElement(@"/package/metadata/projectUrl").Value;

            Assert.AreEqual("https://pubcomp.codeplex.com/", version);
        }

        #region Command-line Tests

        [TestMethod]
        public void TestParseArguments1()
        {
            string projPath, dllPath;
            bool isDebugOut;

            Program.TryParseArguments(
                new[]
                {
                    @"C:\MyProj\MyProj.csproj",
                    @"C:\MyProj\MyProj\bin\Debug\MyProj.dll",
                    @"Debug"
                },
                out projPath, out dllPath, out isDebugOut);

            Assert.AreEqual(@"C:\MyProj\MyProj.csproj", projPath);
            Assert.AreEqual(@"C:\MyProj\MyProj\bin\Debug\MyProj.dll", dllPath);
            Assert.AreEqual(true, isDebugOut);
        }

        [TestMethod]
        public void TestParseArguments2()
        {
            string projPath, dllPath;
            bool isDebugOut;

            Program.TryParseArguments(
                new[]
                {
                    @"C:\MyProj\MyProj.csproj",
                    @"C:\MyProj\MyProj\bin\Release\MyProj.dll",
                    @"Release"
                },
                out projPath, out dllPath, out isDebugOut);

            Assert.AreEqual(@"C:\MyProj\MyProj.csproj", projPath);
            Assert.AreEqual(@"C:\MyProj\MyProj\bin\Release\MyProj.dll", dllPath);
            Assert.AreEqual(false, isDebugOut);
        }

        #endregion
    }
}
