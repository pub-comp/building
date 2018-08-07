using System;
using System.Collections.Generic;
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
        #region Private Fields

        private static bool isLocal;

        private static string testRunDir;

        // ReSharper disable NotAccessedField.Local
        private static string proj1Csproj;
        private static string proj1Dll;
        private static string proj2Csproj;
        private static string proj2Dll;
        private static string proj3Csproj;
        private static string proj3Dll;
        private static string proj4Csproj;
        private static string proj4Dll;
        private static string proj5Csproj;
        private static string proj5Dll;
        private static string projNetStandardCsproj;
        private static string projNetStandardDll;
        private static string projNetStandard2Csproj;
        private static string projNetStandard2Dll;
        // ReSharper restore NotAccessedField.Local

        private static string nuProj1Csproj;
        private static string nuProj1Dll;
        private static string nuProj2Csproj;
        private static string nuProj2Dll;
        private static string nuProj3Csproj;
        private static string nuProj3Dll;

        private static bool isDebugVariable;
        private static string slnPath;

        #endregion

#if DEBUG
        private const bool IsDebug = true;
#else
        private const bool IsDebug = false;
#endif

        #region Initialization and Cleanup

        [ClassInitialize]
        public static void ClassInitialize(TestContext testContext)
        {
            // Using variable instead of constant to escape compiler warnings
            isDebugVariable = IsDebug;

            string rootPath, testSrcDir, testBinDir, testRunDir;

            TestResourceFinder.FindResources(testContext, "Building",
                @"Demo.Library1",
                true, out rootPath, out testSrcDir, out testBinDir, out testRunDir, out isLocal);
            proj1Csproj = testSrcDir + @"\Demo.Library1.csproj";
            proj1Dll = testBinDir + @"\PubComp.Building.Demo.Library1.dll";

            TestResourceFinder.FindResources(testContext, "Building",
                @"Demo.Library2",
                true, out rootPath, out testSrcDir, out testBinDir, out testRunDir, out isLocal);
            proj2Csproj = testSrcDir + @"\Demo.Library2.csproj";
            proj2Dll = testBinDir + @"\PubComp.Building.Demo.Library2.dll";

            TestResourceFinder.FindResources(testContext, "Building",
                @"Demo.Library3",
                true, out rootPath, out testSrcDir, out testBinDir, out testRunDir, out isLocal);
            proj3Csproj = testSrcDir + @"\Demo.Library3.csproj";
            proj3Dll = testBinDir + @"\PubComp.Building.Demo.Library3.dll";

            TestResourceFinder.FindResources(testContext, "Building",
                @"Demo.Library4",
                true, out rootPath, out testSrcDir, out testBinDir, out testRunDir, out isLocal);
            proj4Csproj = testSrcDir + @"\Demo.Library4.csproj";
            proj4Dll = testBinDir + @"\PubComp.Building.Demo.Library4.dll";

            TestResourceFinder.FindResources(testContext, "Building",
                @"Demo.Library5",
                true, out rootPath, out testSrcDir, out testBinDir, out testRunDir, out isLocal);
            proj5Csproj = testSrcDir + @"\Demo.Library5.csproj";
            proj5Dll = testBinDir + @"\PubComp.Building.Demo.Library5.dll";

            TestResourceFinder.FindResources(testContext, "Building",
                @"Demo.LibraryNetStandard",
                true, out rootPath, out testSrcDir, out testBinDir, out testRunDir, out isLocal);
            projNetStandardCsproj = testSrcDir + @"\Demo.LibraryNetStandard.csproj";
            projNetStandardDll = testBinDir + @"\PubComp.Building.Demo.LibraryNetStandard.dll";


            var creator = new NuspecCreatorNetStandard();
            TestResourceFinder.FindResources(testContext, "Building",
                @"Demo.LibraryNetStandard2",
                true, out rootPath, out testSrcDir, out testBinDir, out testRunDir, out isLocal);
            projNetStandard2Csproj = testSrcDir + @"\Demo.LibraryNetStandard2.csproj";
            var targetFramework = creator.GetFrameworkOutputFolder(projNetStandard2Csproj,IsDebug);
            projNetStandard2Dll = $@"{testBinDir}\{targetFramework}\{"PubComp.Building.Demo.LibraryNetStandard2.dll"}";
            
            TestResourceFinder.FindResources(testContext, "Building",
                @"Demo.Package1.NuGet",
                true, out rootPath, out testSrcDir, out testBinDir, out testRunDir, out isLocal);
            nuProj1Csproj = testSrcDir + @"\Demo.Package1.NuGet.csproj";
            nuProj1Dll = testBinDir + @"\PubComp.Building.Demo.Package1.NuGet.dll";

            TestResourceFinder.FindResources(testContext, "Building",
                @"Demo.Package2.NuGet",
                true, out rootPath, out testSrcDir, out testBinDir, out testRunDir, out isLocal);
            nuProj2Csproj = testSrcDir + @"\Demo.Package2.NuGet.csproj";
            nuProj2Dll = testBinDir + @"\PubComp.Building.Demo.Package2.NuGet.dll";

            TestResourceFinder.FindResources(testContext, "Building",
                @"Demo.Package3.NuGet",
                true, out rootPath, out testSrcDir, out testBinDir, out testRunDir, out isLocal);
            nuProj3Csproj = testSrcDir + @"\Demo.Package3.NuGet.csproj";
            nuProj3Dll = testBinDir + @"\PubComp.Building.Demo.Package3.NuGet.dll";

            TestResourceFinder.FindResources(testContext, "Building",
                @"NuGetPack.UnitTests",
                true, out rootPath, out testSrcDir, out testBinDir, out testRunDir, out isLocal);
            TestResourceFinder.CopyResources(testBinDir, testRunDir);

            NuGetPackTests.testRunDir = testRunDir;
            slnPath = rootPath;
        }

        public TestContext TestContext
        {
            get;
            set;
        }

        #endregion

        #region Get Packages Dependencies

        [TestMethod]
        public void TestGetDependenciesOuter()
        {
            var packagesFile = Path.GetDirectoryName(nuProj2Csproj) + @"\packages.config";

            var creator = new NuspecCreatorNetFramework();

            XAttribute dependenciesAttribute;
            var results = creator.GetDependencies(nuProj2Csproj, new[] { packagesFile }, out dependenciesAttribute);
            //var results = creator.GetDependencies(nuProj2Csproj, out dependenciesAttribute);

            LinqAssert.Count(results, 1);
            var elements = results.Select(el => el.Element).ToList();

            Assert.IsNotNull(dependenciesAttribute);
            Assert.AreEqual("targetFramework", dependenciesAttribute.Name);
            Assert.AreEqual("net45", dependenciesAttribute.Value);

            LinqAssert.Any(elements, obj =>
                obj is XElement && ((XElement)obj).Name == "dependency"
                    && ((XElement)obj).Attribute("id").Value == "FakeItEasy"
                    && ((XElement)obj).Attribute("version").Value == "1.24.0");
        }

        [TestMethod]
        public void TestGetPackageDependenciesNetStandard()
        {
            var creator = new NuspecCreatorNetStandard();

            var results = creator.GetDependencies(projNetStandardCsproj, out var dependenciesAttribute);

            LinqAssert.Count(results, 2);
            var elements = results.Select(el => el.Element).ToList();

            Assert.IsNotNull(dependenciesAttribute);
            Assert.AreEqual("targetFramework", dependenciesAttribute.Name);
            Assert.AreEqual(".NETStandard2.0", dependenciesAttribute.Value);

            var dependency = elements.First(el => el.Attribute("id").Value.StartsWith("Newton")); 
            Assert.AreEqual("Newtonsoft.Json", dependency.Attribute("id")?.Value);
            Assert.AreEqual("11.0.2", dependency.Attribute("version")?.Value);
        }

        [TestMethod]
        public void TestGetProjectDependenciesNetStandard()
        {
            var creator = new NuspecCreatorNetStandard();

            var results = creator.GetDependencies(projNetStandardCsproj, out var dependenciesAttribute);

            LinqAssert.Count(results, 2);
            var elements = results.Select(el => el.Element).ToList();

            Assert.IsNotNull(dependenciesAttribute);
            Assert.AreEqual("targetFramework", dependenciesAttribute.Name);
            Assert.AreEqual(".NETStandard2.0", dependenciesAttribute.Value);

            var dependency = elements.First(el => el.Attribute("id").Value.StartsWith("PubComp"));
            Assert.AreEqual("dependency", dependency.Name);
            Assert.AreEqual("PubComp.Building.Demo.LibraryNetStandard2", dependency.Attribute("id")?.Value);
            Assert.AreEqual("1.3.2", dependency.Attribute("version")?.Value);
        }

        [TestMethod]
        public void TestGetDependenciesNoneNetStandard()
        {
            var creator = new NuspecCreatorNetStandard();

            var results = creator.GetDependencies(projNetStandard2Csproj, out var dependenciesAttribute);

            LinqAssert.Count(results, 0);

            Assert.IsNotNull(dependenciesAttribute);
            Assert.AreEqual("targetFramework", dependenciesAttribute.Name);
            Assert.AreEqual(".NETStandard2.0", dependenciesAttribute.Value);
        }

        [TestMethod]
        public void TestGetDependenciesOuter3()
        {
            //var packagesFile = Path.GetDirectoryName(nuProj3Csproj) + @"\packages.config";

            var creator = new NuspecCreatorNetFramework();

            XAttribute dependenciesAttribute;
            var results = creator.GetDependencies(nuProj3Csproj, out dependenciesAttribute);

            LinqAssert.Count(results, 0);

            Assert.IsNotNull(dependenciesAttribute);
            Assert.AreEqual("targetFramework", dependenciesAttribute.Name);
            Assert.AreEqual("net451", dependenciesAttribute.Value);
        }

        [TestMethod]
        public void TestGetDependenciesInner()
        {
            var packagesFile = Path.GetDirectoryName(nuProj2Csproj) + @"\packages.config";

            var creator = new NuspecCreatorNetFramework();
            var results = creator.GetDependencies(packagesFile);

            LinqAssert.Count(results, 1);
            var elements = results.Select(el => el.Element).ToList();

            LinqAssert.Any(elements, obj =>
                obj is XElement && ((XElement)obj).Name == "dependency"
                    && ((XElement)obj).Attribute("id").Value == "FakeItEasy"
                    && ((XElement)obj).Attribute("version").Value == "1.24.0");
        }

        [TestMethod]
        public void TestGetDependenciesViaReferenceInner1()
        {
            var project = proj1Csproj;
            var folder = Path.GetDirectoryName(project);

            var creator = new NuspecCreatorNetFramework();
            var results = creator.GetDependenciesFromProject(folder, project);

            var dependencies = results.Where(r => r.ElementType == ElementType.NuGetDependency)
                .Select(r => r.Element).ToList();

            LinqAssert.Count(dependencies, 2);

            LinqAssert.Count(dependencies.Where(r => r.Attribute("id").Value == "Common.Logging.DV"), 1);
            LinqAssert.Count(dependencies.Where(r => r.Attribute("id").Value == "PubComp.NoSql.Core"), 1);
        }

        [TestMethod]
        public void TestGetDependenciesViaReferenceInner2()
        {
            var project = proj3Csproj;
            var folder = Path.GetDirectoryName(project);

            var creator = new NuspecCreatorNetFramework();
            var results = creator.GetDependenciesFromProject(folder, project);

            var dependencies = results.Where(r => r.ElementType == ElementType.NuGetDependency)
                .Select(r => r.Element).ToList();

            LinqAssert.Count(dependencies, 0);
        }

        [TestMethod]
        public void TestGetDependenciesViaReferenceOuter1()
        {
            var nuspecFolder = Path.GetDirectoryName(nuProj1Dll);

            var creator = new NuspecCreatorNetFramework();

            XAttribute attribute;
            var results = creator.GetElements(
                nuspecFolder, nuProj1Csproj, isDebugVariable, true, false, null, out attribute);

            var dependencies = results.Where(r => r.ElementType == ElementType.NuGetDependency)
                .Select(r => r.Element).ToList();

            LinqAssert.Count(dependencies, 8);

            LinqAssert.Count(dependencies.Where(r => r.Attribute("id").Value == "Common.Logging.DV"), 2);
            LinqAssert.Count(dependencies.Where(r => r.Attribute("id").Value == "Common.Logging.NLog.DV"), 1);
            LinqAssert.Count(dependencies.Where(r => r.Attribute("id").Value == "mongocsharpdriver"), 1);
            LinqAssert.Count(dependencies.Where(r => r.Attribute("id").Value == "NLog"), 1);
            LinqAssert.Count(dependencies.Where(r => r.Attribute("id").Value == "PubComp.NoSql.Core"), 2);
            LinqAssert.Count(dependencies.Where(r => r.Attribute("id").Value == "PubComp.NoSql.MongoDbDriver"), 1);
        }

        [TestMethod]
        public void TestGetDependenciesViaReferenceOuter2()
        {
            var nuspecFolder = Path.GetDirectoryName(nuProj2Dll);

            var creator = new NuspecCreatorNetFramework();

            XAttribute attribute;
            var results = creator.GetElements(
                nuspecFolder, nuProj2Csproj, isDebugVariable, true, false, null, out attribute);

            var dependencies = results.Where(r => r.ElementType == ElementType.NuGetDependency)
                .Select(r => r.Element).ToList();

            LinqAssert.Count(dependencies, 2);
        }

        [TestMethod]
        public void TestGetInternalDependencies1()
        {
            var creator = new NuspecCreatorNetFramework();

            var results = creator.GetInternalDependencies(
                proj5Csproj, isDebugVariable, Path.GetDirectoryName(proj5Dll), null);

            var dependencies = results.Where(r => r.ElementType == ElementType.NuGetDependency)
                .Select(r => r.Element).ToList();

            LinqAssert.Count(dependencies, 1);

            var expectedVersion = "1.3.2" + (isDebugVariable ? "-Test" : string.Empty);

            LinqAssert.Count(dependencies.Where(r =>
                r.Attribute("id").Value == "PubComp.Building.Demo.Library4"
                && r.Attribute("version").Value == expectedVersion), 1);
        }

        [TestMethod]
        public void TestGetInternalDependencies1_OverridePreReleaseEmpty()
        {
            var creator = new NuspecCreatorNetFramework();

            var results = creator.GetInternalDependencies(
                proj5Csproj, isDebugVariable, Path.GetDirectoryName(proj5Dll), String.Empty);

            var dependencies = results.Where(r => r.ElementType == ElementType.NuGetDependency)
                .Select(r => r.Element).ToList();

            LinqAssert.Count(dependencies, 1);

            LinqAssert.Count(dependencies.Where(r =>
                r.Attribute("id").Value == "PubComp.Building.Demo.Library4"
                && r.Attribute("version").Value == "1.3.2"), 1);
        }

        [TestMethod]
        public void TestGetInternalDependencies1_OverridePreReleaseAlpha102()
        {
            var creator = new NuspecCreatorNetFramework();

            var results = creator.GetInternalDependencies(
                proj5Csproj, isDebugVariable, Path.GetDirectoryName(proj5Dll), "alpha102");

            var dependencies = results.Where(r => r.ElementType == ElementType.NuGetDependency)
                .Select(r => r.Element).ToList();

            LinqAssert.Count(dependencies, 1);

            LinqAssert.Count(dependencies.Where(r =>
                r.Attribute("id").Value == "PubComp.Building.Demo.Library4"
                && r.Attribute("version").Value == "1.3.2-alpha102"), 1);
        }

        #endregion

        #region Package content (lib, content, ...)

        [TestMethod]
        public void TestGetContentFiles1()
        {
            var creator = new NuspecCreatorNetFramework();
            var results = creator.GetContentFiles(
                Path.GetDirectoryName(nuProj1Dll), @"..\..", nuProj1Csproj);

            LinqAssert.Count(results, 3);
            var elements = results.Select(el => el.Element).ToList();

            LinqAssert.Any(elements, el =>
                el.Name == "file"
                && el.Attribute("src").Value == @"..\..\..\Data.txt"
                    && el.Attribute("target").Value == @"content\Data.txt",
                "Found: " + results.First());

            LinqAssert.Any(elements, el =>
                el.Name == "file"
                && el.Attribute("src").Value == @"..\..\content\Info.txt"
                    && el.Attribute("target").Value == @"content\Info.txt",
                "Found: " + results.First());

            LinqAssert.Any(elements, el =>
                el.Name == "file"
                && el.Attribute("src").Value == @"..\..\content\SubContent\Other.txt"
                    && el.Attribute("target").Value == @"content\SubContent\Other.txt",
                "Found: " + results.First());
        }

        //[TestMethod]
        //public void test2()
        //{
        //    var nuspecPath = Path.ChangeExtension(proj4Dll, ".nuspec");
        //    var nupkgPath = nuspecPath.Replace(".NuGet.nuspec", ".1.3.2" + (isDebugVariable ? "-Test" : string.Empty) + ".nupkg");
        //    var creator = new NuspecCreatorNetFramework();
        //    creator.CreatePackage(proj4Csproj, proj4Dll, isDebugVariable);
            
        //}


        [TestMethod]
        public void TestGetContentFilesNetStandard()
        {
            //https://docs.microsoft.com/en-us/nuget/reference/nuspec#including-content-files
            var creator = new NuspecCreatorNetStandard();
            var results = creator.GetContentFiles(
                Path.GetDirectoryName(projNetStandard2Dll), @"..\..", projNetStandard2Csproj);

            LinqAssert.Count(results, 3);
            var elements = results.Select(el => el.Element).ToList();

            LinqAssert.Any(elements, el =>
                el.Name == "file"
                && el.Attribute("src")?.Value == @"..\..\..\Data.txt"
                    && el.Attribute("target")?.Value == @"content\Data.txt",
                "Found: " + results.First());

            LinqAssert.Any(elements, el =>
                el.Name == "file"
                && el.Attribute("src")?.Value == @"..\..\content\Info.txt"
                    && el.Attribute("target")?.Value == @"content\Info.txt",
                "Found: " + results.First());

            LinqAssert.Any(elements, el =>
                el.Name == "file"
                && el.Attribute("src")?.Value == @"..\..\content\SubContent\Other.txt"
                    && el.Attribute("target")?.Value == @"content\SubContent\Other.txt",
                "Found: " + results.First());
        }

        [TestMethod]
        public void TestGetContentFiles2()
        {
            var creator = new NuspecCreatorNetFramework();
            var results = creator.GetContentFiles(
                Path.GetDirectoryName(nuProj2Dll), @"..\..", nuProj2Csproj);

            LinqAssert.Count(results, 0);
        }

        [TestMethod]
        public void TestGetSlnFiles()
        {
            var creator = new NuspecCreatorNetFramework();
            var results = creator.GetContentFiles(
                Path.GetDirectoryName(nuProj1Dll), @"..\..", nuProj1Csproj,
                srcFolder: @"sln\", destFolder: @"sln\", flattern: false, elementType: ElementType.SolutionItemsFile);

            LinqAssert.Count(results, 1);
            var elements = results.Select(el => el.Element).ToList();

            LinqAssert.Any(elements, el =>
                el.Name == "file"
                && el.Attribute("src").Value == @"..\..\..\Text.txt"
                    && el.Attribute("target").Value == @"sln\Text.txt",
                "Found: " + results.First());
        }

        [TestMethod]
        public void TestGetBinaryFiles1()
        {
            var creator = new NuspecCreatorNetFramework();
            var results = creator.GetBinaryFiles(
                Path.GetDirectoryName(nuProj1Dll), @"..\..", nuProj1Csproj, false);

            var path = isLocal ? @"..\..\..\Dependencies\" : @"..\..\Dependencies\";

            LinqAssert.Count(results, 2);
            var elements = results.Select(el => el.Element).ToList();

            LinqAssert.Any(elements, el =>
                el.Name == "file"
                && el.Attribute("src").Value == path + @"PubComp.Building.Demo.Binary1.dll"
                    && el.Attribute("target").Value == @"lib\net45\PubComp.Building.Demo.Binary1.dll",
                "Found: " + results.First());

            LinqAssert.Any(elements, el =>
                el.Name == "file"
                && el.Attribute("src").Value == path + @"PubComp.Building.Demo.Binary1.pdb"
                    && el.Attribute("target").Value == @"lib\net45\PubComp.Building.Demo.Binary1.pdb",
                "Found: " + results.First());
        }

        [TestMethod]
        public void TestGetBinaryFiles2()
        {
            var creator = new NuspecCreatorNetFramework();
            var results = creator.GetBinaryFiles(
                Path.GetDirectoryName(nuProj2Dll), @"..\..", nuProj2Csproj, false);

            LinqAssert.Count(results, 0);
        }

        #region Test GetElements

        [TestMethod]
        public void TestGetPackage1Files()
        {
            var creator = new NuspecCreatorNetFramework();
            var path = isLocal ? @"..\..\..\" : @"..\..\";

            var binSuffix = @"bin\";

            if (isLocal)
            {
                if (isDebugVariable)
                    binSuffix += @"Debug\";
                else
                    binSuffix += @"Release\";
            }

            XAttribute attribute;
            var results = creator.GetElements(
                Path.GetDirectoryName(nuProj1Dll), nuProj1Csproj, isDebugVariable, true, false, null, out attribute);

            LinqAssert.Count(results, 22);

            AssertBinaryFile(
                results, "net45", path + @"Demo.Package1.NuGet\..\Dependencies\", @"PubComp.Building.Demo.Binary1.dll");
            AssertBinaryFile(
                results, "net45", path + @"Demo.Package1.NuGet\..\Dependencies\", @"PubComp.Building.Demo.Binary1.pdb");

            AssertBinaryFile(
                results, "net45", path + @"Demo.Library1\" + binSuffix, @"PubComp.Building.Demo.Library1.dll");
            AssertBinaryFile(
                results, "net45", path + @"Demo.Library1\" + binSuffix, @"PubComp.Building.Demo.Library1.pdb");
            AssertBinaryFile(
                results, "net45", path + @"Demo.Library2\" + binSuffix, @"PubComp.Building.Demo.Library2.dll");
            AssertBinaryFile(
                results, "net45", path + @"Demo.Library2\" + binSuffix, @"PubComp.Building.Demo.Library2.pdb");

            AssertSourceFile(
                results, path + @"Demo.Library1\Properties\", @"Demo.Library1\Properties\", "AssemblyInfo.cs");
            AssertSourceFile(
                results, path + @"Demo.Library1\", @"Demo.Library1\", "DemoClass1.cs");

            AssertSourceFile(
                results, path + @"Demo.Library2\Properties\", @"Demo.Library2\Properties\", "AssemblyInfo.cs");
            AssertSourceFile(
                results, path + @"Demo.Library2\", @"Demo.Library2\", "DemoClass2.cs");

            AssertContentFile(
                results, path + @"Demo.Package1.NuGet\content\SubContent\", @"SubContent\", "Other.txt");
            AssertContentFile(
                results, path + @"Demo.Package1.NuGet\..\", @"", "Data.txt");
            AssertContentFile(
                results, path + @"Demo.Package1.NuGet\content\", @"", "Info.txt");

            AssertSlnFile(
                results, path + @"Demo.Package1.NuGet\..\", @"", "Text.txt");

            AssertNuGetDependency(results, "Common.Logging.DV", "2.2.0");
            AssertNuGetDependency(results, "PubComp.NoSql.Core", "2.0.0");

            AssertNuGetDependency(results, "Common.Logging.DV", "2.2.0");
            AssertNuGetDependency(results, "Common.Logging.NLog.DV", "2.2.0");
            AssertNuGetDependency(results, "mongocsharpdriver", "1.9.2");
            AssertNuGetDependency(results, "NLog", "2.1.0");
            AssertNuGetDependency(results, "PubComp.NoSql.Core", "2.0.0");
            AssertNuGetDependency(results, "PubComp.NoSql.MongoDbDriver", "2.0.0");
        }

        [TestMethod]
        public void TestGetPackage2Files()
        {
            var creator = new NuspecCreatorNetFramework();
            var path = isLocal ? @"..\..\..\" : @"..\..\";

            var binSuffix = @"bin\";

            if (isLocal)
            {
                if (isDebugVariable)
                    binSuffix += @"Debug\";
                else
                    binSuffix += @"Release\";
            }

            XAttribute attribute;
            var results = creator.GetElements(
                Path.GetDirectoryName(nuProj2Dll), nuProj2Csproj, isDebugVariable, true, false, null, out attribute);

            LinqAssert.Count(results, 6);

            AssertBinaryFile(
                results, "net45", path + @"Demo.Library3\" + binSuffix, @"PubComp.Building.Demo.Library3.dll");
            AssertBinaryFile(
                results, "net45", path + @"Demo.Library3\" + binSuffix, @"PubComp.Building.Demo.Library3.pdb");

            AssertSourceFile(
                results, path + @"Demo.Library3\Properties\", @"Demo.Library3\Properties\", "AssemblyInfo.cs");
            AssertSourceFile(
                results, path + @"Demo.Library3\", @"Demo.Library3\", "DemoClass3.cs");
        }

        [TestMethod]
        public void TestGetPackage2Files_NoSource()
        {
            var creator = new NuspecCreatorNetFramework();
            var path = isLocal ? @"..\..\..\" : @"..\..\";

            var binSuffix = @"bin\";

            if (isLocal)
            {
                if (isDebugVariable)
                    binSuffix += @"Debug\";
                else
                    binSuffix += @"Release\";
            }

            XAttribute attribute;
            var results = creator.GetElements(
                Path.GetDirectoryName(nuProj2Dll), nuProj2Csproj, isDebugVariable, false, false, null, out attribute);

            LinqAssert.Count(results, 4);

            AssertBinaryFile(
                results, "net45", path + @"Demo.Library3\" + binSuffix, @"PubComp.Building.Demo.Library3.dll");
            AssertBinaryFile(
                results, "net45", path + @"Demo.Library3\" + binSuffix, @"PubComp.Building.Demo.Library3.pdb");
        }

        [TestMethod]
        public void TestGetPackage3Files()
        {
            var creator = new NuspecCreatorNetFramework();
            var path = isLocal ? @"..\..\..\" : @"..\..\";

            var binSuffix = @"bin\";

            if (isLocal)
            {
                if (isDebugVariable)
                    binSuffix += @"Debug\";
                else
                    binSuffix += @"Release\";
            }

            XAttribute attribute;
            var results = creator.GetElements(
                Path.GetDirectoryName(nuProj3Dll), nuProj3Csproj, isDebugVariable, true, false, null, out attribute);

            LinqAssert.Count(results, 13);

            AssertBinaryFile(
                results, "net40", path + @"Demo.Package3.NuGet\..\Dependencies\", @"PubComp.Building.Demo.Binary1.dll");
            AssertBinaryFile(
                results, "net40", path + @"Demo.Package3.NuGet\..\Dependencies\", @"PubComp.Building.Demo.Binary1.pdb");
            AssertBinaryFile(
                results, "net45", path + @"Demo.Package3.NuGet\lib\net45\", @"PubComp.Building.Demo.Binary1.dll");
            AssertBinaryFile(
                results, "net45", path + @"Demo.Package3.NuGet\lib\net45\", @"PubComp.Building.Demo.Binary1.pdb");
            AssertBinaryFile(
                results, "net451", path + @"Demo.Package3.NuGet\..\.nuget\", @"NuGet.exe");
            AssertBinaryFile(
                results, "net40", path + @"Demo.LibraryNet40\" + binSuffix, @"PubComp.Building.Demo.LibraryNet40.dll");
            AssertBinaryFile(
                results, "net40", path + @"Demo.LibraryNet40\" + binSuffix, @"PubComp.Building.Demo.LibraryNet40.pdb");
            AssertBinaryFile(
                results, "net451", path + @"Demo.LibraryNet451\" + binSuffix, @"PubComp.Building.Demo.LibraryNet451.dll");
            AssertBinaryFile(
                results, "net451", path + @"Demo.LibraryNet451\" + binSuffix, @"PubComp.Building.Demo.LibraryNet451.pdb");

            AssertSourceFile(
                results, path + @"Demo.LibraryNet40\Properties\", @"Demo.LibraryNet40\Properties\", "AssemblyInfo.cs");
            AssertSourceFile(
                results, path + @"Demo.LibraryNet40\", @"Demo.LibraryNet40\", "Class40.cs");
            AssertSourceFile(
                results, path + @"Demo.LibraryNet451\Properties\", @"Demo.LibraryNet451\Properties\", "AssemblyInfo.cs");
            AssertSourceFile(
                results, path + @"Demo.LibraryNet451\", @"Demo.LibraryNet451\", "Class451.cs");
        }

        [TestMethod]
        public void TestGetLibrary4Files()
        {
            var creator = new NuspecCreatorNetFramework();
            var path = isLocal ? @"..\..\..\" : @"..\..\";

            var binSuffix = @"bin\";

            if (isLocal)
            {
                if (isDebugVariable)
                    binSuffix += @"Debug\";
                else
                    binSuffix += @"Release\";
            }

            XAttribute attribute;
            var results = creator.GetElements(
                testRunDir, proj4Csproj, isDebugVariable, true, true, null, out attribute);

            LinqAssert.Count(results, 18);

            AssertBinaryFile(
                results, "net45", path + @"Demo.Library4\..\dependencies\", @"PubComp.Building.Demo.Binary1.dll");
            AssertBinaryFile(
                results, "net45", path + @"Demo.Library4\..\dependencies\", @"PubComp.Building.Demo.Binary1.pdb");

            AssertBinaryFile(
                results, "net45", path + @"Demo.Library1\" + binSuffix, @"PubComp.Building.Demo.Library1.dll");
            AssertBinaryFile(
                results, "net45", path + @"Demo.Library1\" + binSuffix, @"PubComp.Building.Demo.Library1.pdb");
            AssertBinaryFile(
                results, "net45", path + @"Demo.Library4\" + binSuffix, @"PubComp.Building.Demo.Library4.dll");
            AssertBinaryFile(
                results, "net45", path + @"Demo.Library4\" + binSuffix, @"PubComp.Building.Demo.Library4.pdb");

            AssertSourceFile(
                results, path + @"Demo.Library1\Properties\", @"Demo.Library1\Properties\", "AssemblyInfo.cs");
            AssertSourceFile(
                results, path + @"Demo.Library1\", @"Demo.Library1\", "DemoClass1.cs");
            AssertSourceFile(
                results, path + @"Demo.Library4\Properties\", @"Demo.Library4\Properties\", "AssemblyInfo.cs");
            AssertSourceFile(
                results, path + @"Demo.Library4\", @"Demo.Library4\", "DemoClass4.cs");

            AssertContentFile(
                results, path + @"Demo.Library4\content\SubContent\", @"SubContent\", "Other.txt");
            AssertContentFile(
                results, path + @"Demo.Library4\..\", @"", "Data.txt");
            AssertContentFile(
                results, path + @"Demo.Library4\content\", @"", "Info.txt");

            AssertSlnFile(
                results, path + @"Demo.Library4\..\", @"", "Text.txt");

            AssertNuGetDependency(results, "Common.Logging.DV", "2.2.0");
            AssertNuGetDependency(results, "PubComp.NoSql.Core", "2.0.0");
            AssertNuGetDependency(results, "NLog", "2.1.0");
        }

        [TestMethod]
        public void TestGetLibrary5Files()
        {
            var creator = new NuspecCreatorNetFramework();
            var path = isLocal ? @"..\..\..\" : @"..\..\";

            var binSuffix = @"bin\";

            if (isLocal)
            {
                if (isDebugVariable)
                    binSuffix += @"Debug\";
                else
                    binSuffix += @"Release\";
            }

            XAttribute attribute;
            var results = creator.GetElements(
                testRunDir, proj5Csproj, isDebugVariable, true, true, null, out attribute);

            LinqAssert.Count(results, 15);

            AssertBinaryFile(
                results, "net45", path + @"Demo.Library2\" + binSuffix, @"PubComp.Building.Demo.Library2.dll");
            AssertBinaryFile(
                results, "net45", path + @"Demo.Library2\" + binSuffix, @"PubComp.Building.Demo.Library2.pdb");
            AssertBinaryFile(
                results, "net45", path + @"Demo.Library5\" + binSuffix, @"PubComp.Building.Demo.Library5.dll");
            AssertBinaryFile(
                results, "net45", path + @"Demo.Library5\" + binSuffix, @"PubComp.Building.Demo.Library5.pdb");

            AssertSourceFile(
                results, path + @"Demo.Library2\Properties\", @"Demo.Library2\Properties\", "AssemblyInfo.cs");
            AssertSourceFile(
                results, path + @"Demo.Library2\", @"Demo.Library2\", "DemoClass2.cs");
            AssertSourceFile(
                results, path + @"Demo.Library5\Properties\", @"Demo.Library5\Properties\", "AssemblyInfo.cs");
            AssertSourceFile(
                results, path + @"Demo.Library5\", @"Demo.Library5\", "DemoClass5.cs");

            // ReSharper disable once ConvertIfStatementToConditionalTernaryExpression
            if (isDebugVariable)
                AssertNuGetDependency(results, "PubComp.Building.Demo.Library4", "1.3.2-Test");
            else
                AssertNuGetDependency(results, "PubComp.Building.Demo.Library4", "1.3.2");

            AssertNuGetDependency(results, "Common.Logging.DV", "2.2.0");
            AssertNuGetDependency(results, "Common.Logging.NLog.DV", "2.2.0");
            AssertNuGetDependency(results, "mongocsharpdriver", "1.9.2");
            AssertNuGetDependency(results, "NLog", "2.1.0");
            AssertNuGetDependency(results, "PubComp.NoSql.Core", "2.0.0");
            AssertNuGetDependency(results, "PubComp.NoSql.MongoDbDriver", "2.0.0");
        }

        private void AssertBinaryFile(
            List<DependencyInfo> results, string netVer, string path, string file)
        {
            var elements = results.Select(el => el.Element).ToList();

            LinqAssert.Any(elements, el =>
                el.Name == "file"
                && el.Attribute("src").Value == path + file
                    && el.Attribute("target").Value == @"lib\" + netVer + @"\" + file,
                "Found: " + results.First());
        }

        private void AssertSourceFile(
            List<DependencyInfo> results, string srcPath, string targetPath, string file)
        {
            var elements = results.Select(el => el.Element).ToList();

            LinqAssert.Any(elements, el =>
                el.Name == "file"
                && el.Attribute("src").Value == srcPath + file
                    && el.Attribute("target").Value == @"src\" + targetPath + file,
                "Found: " + results.First());
        }

        private void AssertContentFile(
            List<DependencyInfo> results, string srcPath, string targetPath, string file)
        {
            var elements = results.Select(el => el.Element).ToList();

            LinqAssert.Any(elements, el =>
                el.Name == "file"
                && el.Attribute("src").Value == srcPath + file
                    && el.Attribute("target").Value == @"content\" + targetPath + file,
                "Found: " + results.First());
        }

        private void AssertSlnFile(
            List<DependencyInfo> results, string srcPath, string targetPath, string file)
        {
            var elements = results.Select(el => el.Element).ToList();

            LinqAssert.Any(elements, el =>
                el.Name == "file"
                && el.Attribute("src").Value == srcPath + file
                    && el.Attribute("target").Value == @"sln\" + targetPath + file,
                "Found: " + results.First());
        }

        private void AssertFrameworkReference(List<DependencyInfo> results, string file)
        {
            LinqAssert.Any(results, r =>
                r.ElementType == ElementType.FrameworkReference
                && r.Element.Attribute("assemblyName") != null
                && r.Element.Attribute("assemblyName").Value == file);
        }

        public void AssertNuGetDependency(List<DependencyInfo> results, string packageName, string version)
        {
            var elements = results.Select(el => el.Element).ToList();

            LinqAssert.Any(elements, obj =>
                obj != null
                && obj.Name == "dependency"
                && obj.Attribute("id").Value == packageName
                && obj.Attribute("version").Value == version);
        }

        #endregion

        [TestMethod]
        public void TestGetBinaryReferences()
        {
            var creator = new NuspecCreatorNetFramework();
            var results = creator.GetBinaryReferences(
                Path.GetDirectoryName(nuProj1Dll), @"..\..\..\Demo.Library3", proj3Csproj, isDebugVariable, Path.GetDirectoryName(proj3Dll));

            var path = isLocal ? @"..\..\..\Demo.Library3\bin\" : @"..\..\Demo.Library3\bin\";

            if (isLocal)
            {
                if (isDebugVariable)
                    path += @"Debug\";
                else
                    path += @"Release\";
            }

            LinqAssert.Count(results, 2);
            var elements = results.Select(el => el.Element).ToList();

            LinqAssert.Any(elements, el =>
                el.Name == "file"
                && el.Attribute("src").Value == path + @"PubComp.Building.Demo.Library3.dll"
                    && el.Attribute("target").Value == @"lib\net45\PubComp.Building.Demo.Library3.dll",
                "Found: " + results.First());

            LinqAssert.Any(elements, el =>
                el.Name == "file"
                && el.Attribute("src").Value == path + @"PubComp.Building.Demo.Library3.pdb"
                    && el.Attribute("target").Value == @"lib\net45\PubComp.Building.Demo.Library3.pdb",
                "Found: " + results.First());
        }

        [TestMethod]
        public void TestGetSourceFiles()
        {
            var creator = new NuspecCreatorNetFramework();
            var results = creator.GetSourceFiles(
                Path.GetDirectoryName(nuProj1Dll), @"..\..\..\Demo.Library3", proj3Csproj);

            LinqAssert.Count(results, 2);
            var elements = results.Select(el => el.Element).ToList();

            LinqAssert.Any(elements, el =>
                el.Name == "file"
                && el.Attribute("src").Value == @"..\..\..\Demo.Library3\Properties\AssemblyInfo.cs"
                    && el.Attribute("target").Value == @"src\Demo.Library3\Properties\AssemblyInfo.cs",
                "Found: " + results.First());

            LinqAssert.Any(elements, el =>
                el.Name == "file"
                && el.Attribute("src").Value == @"..\..\..\Demo.Library3\DemoClass3.cs"
                    && el.Attribute("target").Value == @"src\Demo.Library3\DemoClass3.cs",
                "Found: " + results.First());
        }

        [TestMethod]
        public void TestGetReferences()
        {
            var nuspecFolder = Path.GetDirectoryName(nuProj1Dll);
            var projFolder = Path.GetDirectoryName(nuProj1Csproj);

            var creator = new NuspecCreatorNetFramework();
            var results = creator.GetReferences(nuProj1Csproj);

            LinqAssert.Count(results, 2);
            LinqAssert.Any(results, r => r == @"..\Demo.Library1\Demo.Library1.csproj");
            LinqAssert.Any(results, r => r == @"..\Demo.Library2\Demo.Library2.csproj");
        }

        [TestMethod]
        public void TestGetFrameworkReferences()
        {
            var projFolder = Path.GetDirectoryName(nuProj2Csproj);

            var creator = new NuspecCreatorNetFramework();
            var results = creator.GetFrameworkReferences(projFolder, nuProj2Csproj);

            LinqAssert.Count(results, 1);
            LinqAssert.Any(results, r =>
                r.ElementType == ElementType.FrameworkReference
                && r.Element.Attribute("assemblyName") != null
                && r.Element.Attribute("assemblyName").Value == "System.Xaml");
        }

        [TestMethod]
        public void TestGetFiles()
        {
            var nuspecFolder = Path.GetDirectoryName(nuProj1Dll);

            var creator = new NuspecCreatorNetFramework();

            XAttribute attribute;
            var results = creator.GetElements(
                nuspecFolder, nuProj1Csproj, isDebugVariable, true, false, null, out attribute);

            Assert.AreNotEqual(0, results.Count());
            var files = results.Where(el =>
                    el.ElementType != ElementType.NuGetDependency
                    && el.ElementType != ElementType.FrameworkReference)
                .ToList();
            var elements = files.Select(el => el.Element).ToList();

            LinqAssert.All(elements, r => File.Exists(Path.Combine(nuspecFolder, r.Attribute("src").Value)));
        }

        [TestMethod]
        public void TestGetFilesIncludingVersions()
        {
            var nuspecFolder = Path.GetDirectoryName(nuProj3Dll);

            var creator = new NuspecCreatorNetFramework();

            XAttribute attribute;
            var results = creator.GetElements(
                nuspecFolder, nuProj3Csproj, isDebugVariable, true, false, null, out attribute);

            Assert.AreNotEqual(0, results.Count);

            LinqAssert.Single(results, el =>
                el.ElementType == ElementType.LibraryFile
                && el.Element.Name == "file"
                && el.Element.Attribute("target").Value == @"lib\net451\NuGet.exe");

            LinqAssert.Single(results, el =>
                el.ElementType == ElementType.LibraryFile
                && el.Element.Name == "file"
                && el.Element.Attribute("target").Value == @"lib\net40\PubComp.Building.Demo.Binary1.dll");

            LinqAssert.Single(results, el =>
                el.ElementType == ElementType.LibraryFile
                && el.Element.Name == "file"
                && el.Element.Attribute("target").Value == @"lib\net40\PubComp.Building.Demo.Binary1.pdb");

            LinqAssert.Single(results, el =>
                el.ElementType == ElementType.LibraryFile
                && el.Element.Name == "file"
                && el.Element.Attribute("target").Value == @"lib\net45\PubComp.Building.Demo.Binary1.dll");

            LinqAssert.Single(results, el =>
                el.ElementType == ElementType.LibraryFile
                && el.Element.Name == "file"
                && el.Element.Attribute("target").Value == @"lib\net45\PubComp.Building.Demo.Binary1.pdb");

            LinqAssert.Single(results, el =>
                el.ElementType == ElementType.LibraryFile
                && el.Element.Name == "file"
                && el.Element.Attribute("target").Value == @"lib\net40\PubComp.Building.Demo.LibraryNet40.dll");

            LinqAssert.Single(results, el =>
                el.ElementType == ElementType.LibraryFile
                && el.Element.Name == "file"
                && el.Element.Attribute("target").Value == @"lib\net40\PubComp.Building.Demo.LibraryNet40.pdb");

            LinqAssert.Single(results, el =>
                el.ElementType == ElementType.LibraryFile
                && el.Element.Name == "file"
                && el.Element.Attribute("target").Value == @"lib\net451\PubComp.Building.Demo.LibraryNet451.dll");

            LinqAssert.Single(results, el =>
                el.ElementType == ElementType.LibraryFile
                && el.Element.Name == "file"
                && el.Element.Attribute("target").Value == @"lib\net451\PubComp.Building.Demo.LibraryNet451.pdb");

            var files = results.Where(el =>
                    el.ElementType != ElementType.NuGetDependency
                    && el.ElementType != ElementType.FrameworkReference)
                .ToList();
            var elements = files.Select(el => el.Element).ToList();

            LinqAssert.All(elements, r => File.Exists(Path.Combine(nuspecFolder, r.Attribute("src").Value)));
        }

        #endregion

        #region Create Ouput Tests

        [TestMethod]
        public void TestCreateNuspecNetStandard2()
        {
            var creator = new NuspecCreatorNetFramework();
            var nuspec = creator.CreateNuspec(projNetStandard2Csproj, projNetStandard2Dll, isDebugVariable);
            Assert.IsNotNull(nuspec);
        }

        [TestMethod]
        public void TestCreateNuspec1()
        {
            var creator = new NuspecCreatorNetFramework();
            var nuspec = creator.CreateNuspec(nuProj1Csproj, nuProj1Dll, isDebugVariable);
            Assert.IsNotNull(nuspec);
        }

        [TestMethod]
        public void TestCreatePackage1()
        {
            var nuspecPath = Path.ChangeExtension(nuProj1Dll, ".nuspec");
            var nupkgPath = nuspecPath.Replace(".NuGet.nuspec", ".1.3.2" + (isDebugVariable ? "-Test" : string.Empty) + ".nupkg");

            File.Delete(nupkgPath);
            File.Delete(nuspecPath);

            var creator = new NuspecCreatorNetFramework();
            creator.CreatePackage(nuProj1Csproj, nuProj1Dll, isDebugVariable);

            Assert.IsTrue(File.Exists(nuspecPath));
            Assert.IsTrue(File.Exists(nupkgPath));
        }

        [TestMethod]
        public void TestCreatePackage1_WithoutNuPkg()
        {
            var nuspecPath = Path.ChangeExtension(nuProj1Dll, ".nuspec");
            var nupkgPath = nuspecPath.Replace(".NuGet.nuspec", ".1.3.2" + (isDebugVariable ? "-Test" : string.Empty) + ".nupkg");

            File.Delete(nupkgPath);
            File.Delete(nuspecPath);

            var creator = new NuspecCreatorNetFramework();
            creator.CreatePackage(nuProj1Csproj, nuProj1Dll, isDebugVariable, false);

            Assert.IsTrue(File.Exists(nuspecPath));
            Assert.IsFalse(File.Exists(nupkgPath));
        }

        [TestMethod]
        public void TestCreatePackage2()
        {
            var nuspecPath = Path.ChangeExtension(nuProj2Dll, ".nuspec");
            var nupkgPath = nuspecPath.Replace(".NuGet.nuspec", ".1.3.2" + (isDebugVariable ? "-Test" : string.Empty) + ".nupkg");

            File.Delete(nupkgPath);
            File.Delete(nuspecPath);

            var creator = new NuspecCreatorNetFramework();
            creator.CreatePackage(nuProj2Csproj, nuProj2Dll, isDebugVariable);

            Assert.IsTrue(File.Exists(nuspecPath));
            Assert.IsTrue(File.Exists(nupkgPath));
        }

        [TestMethod]
        public void TestCreatePackage3()
        {
            var nuspecPath = Path.ChangeExtension(nuProj3Dll, ".nuspec");
            var nupkgPath = nuspecPath.Replace(".NuGet.nuspec", ".1.3.2" + (isDebugVariable ? "-Test" : string.Empty) + ".nupkg");
            var nupkgSymPath = Path.ChangeExtension(nupkgPath, ".symbols.nupkg");

            File.Delete(nupkgPath);
            File.Delete(nuspecPath);
            File.Delete(nupkgSymPath);

            var creator = new NuspecCreatorNetFramework();
            creator.CreatePackage(nuProj3Csproj, nuProj3Dll, isDebugVariable);

            Assert.IsTrue(File.Exists(nuspecPath));
            Assert.IsTrue(File.Exists(nupkgPath));
            Assert.IsTrue(File.Exists(nupkgSymPath));
        }

        [TestMethod]
        public void TestCreatePackagesAll_NoNuPkg()
        {
            var nuspecPaths = new List<string>();
            var nupkgPaths = new List<string>();

            foreach (var dll in new [] { nuProj2Dll, nuProj3Dll, proj4Dll, proj5Dll })
            {
                var nuspecPath = Path.ChangeExtension(dll, ".nuspec");

                const string nuGetNuSpec = ".NuGet.nuspec";
                const string nuSpec = ".nuspec";

                var replaceToken = nuspecPath.Contains(nuGetNuSpec)
                    ? nuGetNuSpec
                    : nuSpec;

                //var nupkgPath =
                //    Path.Combine(testRunDir,
                //    Path.GetFileName(nuspecPath).Replace(replaceToken, ".1.3.2" + (isDebugVariable ? "-Test" : string.Empty) + ".nupkg"));

                var nupkgPath = 
                    nuspecPath.Replace(replaceToken, ".1.3.2" + (isDebugVariable ? "-Test" : string.Empty) + ".nupkg");

                File.Delete(nupkgPath);
                File.Delete(nuspecPath);

                nuspecPaths.Add(nuspecPath);
                nupkgPaths.Add(nupkgPath);
            }

            NuspecCreatorHelper.CreatePackages(testRunDir, slnPath, isDebugVariable, false, true);

            foreach (var nuspecPath in nuspecPaths)
            {
                Assert.IsTrue(File.Exists(nuspecPath));
            }

            foreach (var nupkgPath in nupkgPaths)
            {
                Assert.IsFalse(File.Exists(nupkgPath));
            }
        }

        [TestMethod]
        public void TestCreatePackagesAll_WithNuPkg()
        {
            var nuspecPaths = new List<string>();
            var nupkgPaths = new List<string>();

            foreach (var dll in new[] { nuProj2Dll, nuProj3Dll, proj4Dll, proj5Dll })
            {
                var nuspecPath = Path.ChangeExtension(dll, ".nuspec");

                const string nuGetNuSpec = ".NuGet.nuspec";
                const string nuSpec = ".nuspec";

                var replaceToken = nuspecPath.Contains(nuGetNuSpec)
                    ? nuGetNuSpec
                    : nuSpec;

                //var nupkgPath =
                //    Path.Combine(testRunDir,
                //    Path.GetFileName(nuspecPath).Replace(replaceToken, ".1.3.2" + (isDebugVariable ? "-Test" : string.Empty) + ".nupkg"));

                var nupkgPath = nuspecPath.Replace(replaceToken, ".1.3.2" + (isDebugVariable ? "-Test" : string.Empty) + ".nupkg");

                File.Delete(nupkgPath);
                File.Delete(nuspecPath);

                nuspecPaths.Add(nuspecPath);
                nupkgPaths.Add(nupkgPath);
            }

            NuspecCreatorHelper.CreatePackages(testRunDir, slnPath, isDebugVariable, true, true);

            foreach (var nuspecPath in nuspecPaths)
            {
                Assert.IsTrue(File.Exists(nuspecPath), nuspecPath);
            }

            foreach (var nupkgPath in nupkgPaths)
            {
                Assert.IsTrue(File.Exists(nupkgPath), nupkgPath);
            }
        }

        [TestMethod]
        public void TestCreatePackagesAll_WithNuPkg_OverridePreReleaseToNone()
        {
            var nuspecPaths = new List<string>();
            var nupkgPaths = new List<string>();

            foreach (var dll in new[] { nuProj2Dll, nuProj3Dll, proj4Dll, proj5Dll })
            {
                var nuspecPath = Path.ChangeExtension(dll, ".nuspec");

                const string nuGetNuSpec = ".NuGet.nuspec";
                const string nuSpec = ".nuspec";

                var replaceToken = nuspecPath.Contains(nuGetNuSpec)
                    ? nuGetNuSpec
                    : nuSpec;

                var nupkgPath = nuspecPath.Replace(replaceToken, ".1.3.2.nupkg");

                File.Delete(nupkgPath);
                File.Delete(nuspecPath);

                nuspecPaths.Add(nuspecPath);
                nupkgPaths.Add(nupkgPath);
            }

            var creator = new NuspecCreatorNetFramework();
            NuspecCreatorHelper.CreatePackages(testRunDir, slnPath, isDebugVariable, true, true,
                preReleaseSuffixOverride: string.Empty);

            foreach (var nuspecPath in nuspecPaths)
            {
                Assert.IsTrue(File.Exists(nuspecPath), nuspecPath);
            }

            foreach (var nupkgPath in nupkgPaths)
            {
                Assert.IsTrue(File.Exists(nupkgPath), nupkgPath);
            }
        }

        [TestMethod]
        public void TestCreatePackagesAll_WithNuPkg_OverridePreReleaseToAlpha101()
        {
            var nuspecPaths = new List<string>();
            var nupkgPaths = new List<string>();

            foreach (var dll in new[] { nuProj2Dll, nuProj3Dll, proj4Dll, proj5Dll })
            {
                var nuspecPath = Path.ChangeExtension(dll, ".nuspec");

                const string nuGetNuSpec = ".NuGet.nuspec";
                const string nuSpec = ".nuspec";

                var replaceToken = nuspecPath.Contains(nuGetNuSpec)
                    ? nuGetNuSpec
                    : nuSpec;
                
                var nupkgPath = nuspecPath.Replace(replaceToken, ".1.3.2-Alpha101.nupkg");

                File.Delete(nupkgPath);
                File.Delete(nuspecPath);

                nuspecPaths.Add(nuspecPath);
                nupkgPaths.Add(nupkgPath);
            }

            NuspecCreatorHelper.CreatePackages(testRunDir, slnPath, isDebugVariable, true, true,
                preReleaseSuffixOverride: "Alpha101");

            foreach (var nuspecPath in nuspecPaths)
            {
                Assert.IsTrue(File.Exists(nuspecPath), nuspecPath);
            }

            foreach (var nupkgPath in nupkgPaths)
            {
                Assert.IsTrue(File.Exists(nupkgPath), nupkgPath);
            }
        }

        #endregion

        #region Package Metadata Tests

        #region From Assembly Metadata

        [TestMethod]
        public void TestParseVersion()
        {
            var creator = new NuspecCreatorNetFramework();
            var nuspec = creator.CreateNuspec(nuProj1Csproj, nuProj1Dll, isDebugVariable);
            
            Assert.IsNotNull(nuspec);

            var version = nuspec.XPathSelectElement(@"/package/metadata/version").Value;

            // ReSharper disable once ConvertIfStatementToConditionalTernaryExpression
            if (isDebugVariable)
                Assert.AreEqual("1.3.2-Test", version);
            else
                Assert.AreEqual("1.3.2", version);
        }

        [TestMethod]
        public void TestParseName()
        {
            var creator = new NuspecCreatorNetFramework();
            var nuspec = creator.CreateNuspec(nuProj1Csproj, nuProj1Dll, isDebugVariable);

            Assert.IsNotNull(nuspec);

            var version = nuspec.XPathSelectElement(@"/package/metadata/title").Value;

            Assert.AreEqual("PubComp.Building.Demo.Package1", version);
        }

        [TestMethod]
        public void TestParseDescriptionFromAssembly()
        {
            var creator = new NuspecCreatorNetFramework();
            var nuspec = creator.CreateNuspec(nuProj1Csproj, nuProj1Dll, isDebugVariable);

            Assert.IsNotNull(nuspec);

            var version = nuspec.XPathSelectElement(@"/package/metadata/description").Value;

            Assert.AreEqual("Description goes here", version);
        }

        [TestMethod]
        public void TestParseKeywordsFromAssembly()
        {
            var creator = new NuspecCreatorNetFramework();
            var nuspec = creator.CreateNuspec(nuProj1Csproj, nuProj1Dll, isDebugVariable);

            Assert.IsNotNull(nuspec);

            var version = nuspec.XPathSelectElement(@"/package/metadata/tags").Value;

            Assert.AreEqual("Keywords, go, here", version);
        }

        [TestMethod]
        public void TestParseProjectUrlFromAssembly()
        {
            var creator = new NuspecCreatorNetFramework();
            var nuspec = creator.CreateNuspec(nuProj1Csproj, nuProj1Dll, isDebugVariable);

            Assert.IsNotNull(nuspec);

            var version = nuspec.XPathSelectElement(@"/package/metadata/projectUrl").Value;

            Assert.AreEqual("https://pubcomp.codeplex.com/", version);
        }

        #endregion

        #region From Config file

        [TestMethod]
        public void TestParseDescriptionFromConfig()
        {
            var creator = new NuspecCreatorNetFramework();
            var nuspec = creator.CreateNuspec(nuProj2Csproj, nuProj2Dll, isDebugVariable);

            Assert.IsNotNull(nuspec);

            var version = nuspec.XPathSelectElement(@"/package/metadata/description").Value;

            Assert.AreEqual("Demo Description", version);
        }

        [TestMethod]
        public void TestParseSummaryFromConfig()
        {
            var creator = new NuspecCreatorNetFramework();
            var nuspec = creator.CreateNuspec(nuProj2Csproj, nuProj2Dll, isDebugVariable);

            Assert.IsNotNull(nuspec);

            var version = nuspec.XPathSelectElement(@"/package/metadata/summary").Value;

            Assert.AreEqual("Demo Summary", version);
        }

        [TestMethod]
        public void TestParseKeywordsFromConfig()
        {
            var creator = new NuspecCreatorNetFramework();
            var nuspec = creator.CreateNuspec(nuProj2Csproj, nuProj2Dll, isDebugVariable);

            Assert.IsNotNull(nuspec);

            var version = nuspec.XPathSelectElement(@"/package/metadata/tags").Value;

            Assert.AreEqual("Demo, Key, Word", version);
        }

        [TestMethod]
        public void TestParseIconUrlFromConfig()
        {
            var creator = new NuspecCreatorNetFramework();
            var nuspec = creator.CreateNuspec(nuProj2Csproj, nuProj2Dll, isDebugVariable);

            Assert.IsNotNull(nuspec);

            var version = nuspec.XPathSelectElement(@"/package/metadata/iconUrl").Value;

            Assert.AreEqual("http://www.codeplex.com/favicon.ico", version);
        }

        [TestMethod]
        public void TestParseProjectUrlFromConfig()
        {
            var creator = new NuspecCreatorNetFramework();
            var nuspec = creator.CreateNuspec(nuProj2Csproj, nuProj2Dll, isDebugVariable);

            Assert.IsNotNull(nuspec);

            var version = nuspec.XPathSelectElement(@"/package/metadata/projectUrl").Value;

            Assert.AreEqual("https://pubcomp.codeplex.com/#", version);
        }

        [TestMethod]
        public void TestParseLicenseUrlFromConfig()
        {
            var creator = new NuspecCreatorNetFramework();
            var nuspec = creator.CreateNuspec(nuProj2Csproj, nuProj2Dll, isDebugVariable);

            Assert.IsNotNull(nuspec);

            var version = nuspec.XPathSelectElement(@"/package/metadata/licenseUrl").Value;

            Assert.AreEqual("https://pubcomp.codeplex.com/license", version);
        }

        [TestMethod]
        public void TestParseAuthorsFromConfig()
        {
            var creator = new NuspecCreatorNetFramework();
            var nuspec = creator.CreateNuspec(nuProj2Csproj, nuProj2Dll, isDebugVariable);

            Assert.IsNotNull(nuspec);

            var version = nuspec.XPathSelectElement(@"/package/metadata/authors").Value;

            Assert.AreEqual("Demo Author", version);
        }

        [TestMethod]
        public void TestParseOwnersFromConfig()
        {
            var creator = new NuspecCreatorNetFramework();
            var nuspec = creator.CreateNuspec(nuProj2Csproj, nuProj2Dll, isDebugVariable);

            Assert.IsNotNull(nuspec);

            var version = nuspec.XPathSelectElement(@"/package/metadata/owners").Value;

            Assert.AreEqual("Demo Owner", version);
        }

        [TestMethod]
        public void TestParseCopyrightFromConfig()
        {
            var creator = new NuspecCreatorNetFramework();
            var nuspec = creator.CreateNuspec(nuProj2Csproj, nuProj2Dll, isDebugVariable);

            Assert.IsNotNull(nuspec);

            var version = nuspec.XPathSelectElement(@"/package/metadata/copyright").Value;

            Assert.AreEqual("Demo Copyright", version);
        }

        #endregion

        #endregion

        #region Command-line Tests

        [TestMethod]
        public void TestParseArguments_Debug()
        {
            var result = Program.TryParseArguments(
                new[]
                {
                    @"C:\MyProj\MyProj.csproj",
                    @"C:\MyProj\MyProj\bin\Debug\MyProj.dll",
                    @"Debug",
                },
                out Program.CommandLineArguments cla);

            Assert.AreEqual(true, result);
            Assert.AreEqual(Program.Mode.Project, cla.Mode);
            Assert.AreEqual(null, cla.BinFolder);
            Assert.AreEqual(null, cla.SolutionFolder);
            Assert.AreEqual(@"C:\MyProj\MyProj.csproj", cla.ProjPath);
            Assert.AreEqual(@"C:\MyProj\MyProj\bin\Debug\MyProj.dll", cla.DllPath);
            Assert.AreEqual(true, cla.IsDebug);
            Assert.AreEqual(true, cla.DoCreateNuPkg);
            Assert.AreEqual(false, cla.DoIncludeCurrentProj);
            Assert.IsNull(cla.PreReleaseSuffixOverride);
        }

        [TestMethod]
        public void TestParseArguments_Release()
        {
            var result = Program.TryParseArguments(
                new[]
                {
                    @"C:\MyProj\MyProj.csproj",
                    @"C:\MyProj\MyProj\bin\Release\MyProj.dll",
                    @"Release",
                },
                out Program.CommandLineArguments cla);

            Assert.AreEqual(true, result);
            Assert.AreEqual(Program.Mode.Project, cla.Mode);
            Assert.AreEqual(null, cla.BinFolder);
            Assert.AreEqual(null, cla.SolutionFolder);
            Assert.AreEqual(@"C:\MyProj\MyProj.csproj", cla.ProjPath);
            Assert.AreEqual(@"C:\MyProj\MyProj\bin\Release\MyProj.dll", cla.DllPath);
            Assert.AreEqual(false, cla.IsDebug);
            Assert.AreEqual(true, cla.DoCreateNuPkg);
            Assert.AreEqual(false, cla.DoIncludeCurrentProj);
            Assert.IsNull(cla.PreReleaseSuffixOverride);
        }

        [TestMethod]
        public void TestParseArguments_NoPkg()
        {
            var result = Program.TryParseArguments(
                new[]
                {
                    @"C:\MyProj\MyProj.csproj",
                    @"C:\MyProj\MyProj\bin\Release\MyProj.dll",
                    @"Release",
                    @"NoPkg",
                },
                out Program.CommandLineArguments cla);

            Assert.AreEqual(true, result);
            Assert.AreEqual(Program.Mode.Project, cla.Mode);
            Assert.AreEqual(null, cla.BinFolder);
            Assert.AreEqual(null, cla.SolutionFolder);
            Assert.AreEqual(@"C:\MyProj\MyProj.csproj", cla.ProjPath);
            Assert.AreEqual(@"C:\MyProj\MyProj\bin\Release\MyProj.dll", cla.DllPath);
            Assert.AreEqual(false, cla.IsDebug);
            Assert.AreEqual(false, cla.DoCreateNuPkg);
            Assert.AreEqual(false, cla.DoIncludeCurrentProj);
            Assert.IsNull(cla.PreReleaseSuffixOverride);
        }

        [TestMethod]
        public void TestParseArguments_IncludeCurrentPrj()
        {
            var result = Program.TryParseArguments(
                new[]
                {
                    @"C:\MyProj\MyProj.csproj",
                    @"C:\MyProj\MyProj\bin\Release\MyProj.dll",
                    @"Release",
                    @"includecurrentProj",
                },
                out Program.CommandLineArguments cla);

            Assert.AreEqual(true, result);
            Assert.AreEqual(Program.Mode.Project, cla.Mode);
            Assert.AreEqual(null, cla.BinFolder);
            Assert.AreEqual(null, cla.SolutionFolder);
            Assert.AreEqual(@"C:\MyProj\MyProj.csproj", cla.ProjPath);
            Assert.AreEqual(@"C:\MyProj\MyProj\bin\Release\MyProj.dll", cla.DllPath);
            Assert.AreEqual(false, cla.IsDebug);
            Assert.AreEqual(true, cla.DoCreateNuPkg);
            Assert.AreEqual(true, cla.DoIncludeCurrentProj);
            Assert.IsNull(cla.PreReleaseSuffixOverride);
        }

        [TestMethod]
        public void TestParseArguments_NoPkgIncludeCurrentPrj()
        {
            var result = Program.TryParseArguments(
                new[]
                {
                    @"C:\MyProj\MyProj.csproj",
                    @"C:\MyProj\MyProj\bin\Release\MyProj.dll",
                    @"Release",
                    @"NoPkg",
                    @"Includecurrentproj",
                },
                out Program.CommandLineArguments cla);

            Assert.AreEqual(true, result);
            Assert.AreEqual(Program.Mode.Project, cla.Mode);
            Assert.AreEqual(null, cla.BinFolder);
            Assert.AreEqual(null, cla.SolutionFolder);
            Assert.AreEqual(@"C:\MyProj\MyProj.csproj", cla.ProjPath);
            Assert.AreEqual(@"C:\MyProj\MyProj\bin\Release\MyProj.dll", cla.DllPath);
            Assert.AreEqual(false, cla.IsDebug);
            Assert.AreEqual(false, cla.DoCreateNuPkg);
            Assert.AreEqual(true, cla.DoIncludeCurrentProj);
            Assert.AreEqual(true, result);
        }

        [TestMethod]
        public void TestParseArguments_UnidentifiedParam()
        {
            var result = Program.TryParseArguments(
                new[]
                {
                    @"C:\MyProj\MyProj.csproj",
                    @"C:\MyProj\MyProj\bin\Release\MyProj.dll",
                    @"Release",
                    @"x",
                },
                out Program.CommandLineArguments cla);

            Assert.AreEqual(false, result);
        }

        [TestMethod]
        public void TestParseArguments_ExtraNoPkg()
        {
            var result = Program.TryParseArguments(
                new[]
                {
                    @"C:\MyProj\MyProj.csproj",
                    @"C:\MyProj\MyProj\bin\Release\MyProj.dll",
                    @"Release",
                    @"NoPkg",
                    @"NoPkg",
                },
                out Program.CommandLineArguments cla);

            Assert.AreEqual(false, result);
        }

        [TestMethod]
        public void TestParseArguments_ExtraIncludeCurrentProj()
        {
            var result = Program.TryParseArguments(
                new[]
                {
                    @"C:\MyProj\MyProj.csproj",
                    @"C:\MyProj\MyProj\bin\Release\MyProj.dll",
                    @"Release",
                    @"Includecurrentproj",
                    @"includeCurrentproj",
                },
                out Program.CommandLineArguments cla);

            Assert.AreEqual(false, result);
        }

        [TestMethod]
        public void TestParseArguments_ReleaseAndDebug()
        {
            var result = Program.TryParseArguments(
                new[]
                {
                    @"C:\MyProj\MyProj.csproj",
                    @"C:\MyProj\MyProj\bin\Release\MyProj.dll",
                    @"Release",
                    @"debug",
                },
                out Program.CommandLineArguments cla);

            Assert.AreEqual(false, result);
        }

        [TestMethod]
        public void TestParseArguments_ExtraDebug()
        {
            var result = Program.TryParseArguments(
                new[]
                {
                    @"C:\MyProj\MyProj.csproj",
                    @"C:\MyProj\MyProj\bin\Release\MyProj.dll",
                    @"debug",
                    @"Debug",
                },
                out Program.CommandLineArguments cla);

            Assert.AreEqual(false, result);
        }

        [TestMethod]
        public void TestParseArguments_ExtraRelease()
        {
            var result = Program.TryParseArguments(
                new[]
                {
                    @"C:\MyProj\MyProj.csproj",
                    @"C:\MyProj\MyProj\bin\Release\MyProj.dll",
                    @"Release",
                    @"release",
                },
                out Program.CommandLineArguments cla);

            Assert.AreEqual(false, result);
        }

        [TestMethod]
        public void TestParseArguments_SlnMode_ProjDllInsteadOfSlnBin()
        {
            var result = Program.TryParseArguments(
                new[]
                {
                    @"Solution",
                    @"C:\MyProj\MyProj.csproj",
                    @"C:\MyProj\MyProj\bin\Debug\MyProj.dll",
                    @"Debug",
                },
                out Program.CommandLineArguments cla);

            Assert.AreEqual(false, result);
        }

        [TestMethod]
        public void TestParseArguments_ProjMode_SlnBinInsteadOfProjDll()
        {
            var result = Program.TryParseArguments(
                new[]
                {
                    @"Project",
                    @"bin=C:\MyProj\bin\",
                    @"sln=C:\MyProj\sln\",
                    @"Debug",
                },
                out Program.CommandLineArguments cla);

            Assert.AreEqual(false, result);
        }

        [TestMethod]
        public void TestParseArguments_ImplicitProjMode_SlnBinInsteadOfProjDln()
        {
            var result = Program.TryParseArguments(
                new[]
                {
                    @"bin=C:\MyProj\bin\",
                    @"sln=C:\MyProj\sln\",
                    @"Debug",
                },
                out Program.CommandLineArguments cla);

            Assert.AreEqual(false, result);
        }

        [TestMethod]
        public void TestParseArguments_SlnMode()
        {
            var result = Program.TryParseArguments(
                new[]
                {
                    @"Solution",
                    @"sln=C:\MyProj\sln\",
                    @"bin=C:\MyProj\bin\",
                    @"Debug",
                },
                out Program.CommandLineArguments cla);

            Assert.AreEqual(true, result);
            Assert.AreEqual(Program.Mode.Solution, cla.Mode);
            Assert.AreEqual(null, cla.ProjPath);
            Assert.AreEqual(null, cla.DllPath);
            Assert.AreEqual(@"C:\MyProj\bin\", cla.BinFolder);
            Assert.AreEqual(@"C:\MyProj\sln\", cla.SolutionFolder);
            Assert.AreEqual(true, cla.IsDebug);
            Assert.AreEqual(true, cla.DoCreateNuPkg);
            Assert.AreEqual(false, cla.DoIncludeCurrentProj);
        }

        [TestMethod]
        public void TestParseArguments_SlnMode_src()
        {
            var result = Program.TryParseArguments(
                new[]
                {
                    @"Solution",
                    @"src=C:\MyProj\sln\",
                    @"bin=C:\MyProj\bin\",
                    @"Debug",
                },
                out Program.CommandLineArguments cla);

            Assert.AreEqual(true, result);
            Assert.AreEqual(Program.Mode.Solution, cla.Mode);
            Assert.AreEqual(null, cla.ProjPath);
            Assert.AreEqual(null, cla.DllPath);
            Assert.AreEqual(@"C:\MyProj\bin\", cla.BinFolder);
            Assert.AreEqual(@"C:\MyProj\sln\", cla.SolutionFolder);
            Assert.AreEqual(true, cla.IsDebug);
            Assert.AreEqual(true, cla.DoCreateNuPkg);
            Assert.AreEqual(false, cla.DoIncludeCurrentProj);
        }

        [TestMethod]
        public void TestParseArguments_SlnMode_ExtraBin()
        {
            var result = Program.TryParseArguments(
                new[]
                {
                    @"Solution",
                    @"sln=C:\MyProj\sln\",
                    @"bin=C:\MyProj\bin\",
                    @"bin=C:\MyProj\bin\",
                    @"Debug",
                },
                out Program.CommandLineArguments cla);

            Assert.AreEqual(false, result);
        }

        [TestMethod]
        public void TestParseArguments_SlnMode_ExtraSln()
        {
            var result = Program.TryParseArguments(
                new[]
                {
                    @"Solution",
                    @"sln=C:\MyProj\sln\",
                    @"bin=C:\MyProj\bin\",
                    @"sln=C:\MyProj\sln\",
                    @"Debug",
                },
                out Program.CommandLineArguments cla);

            Assert.AreEqual(false, result);
        }

        [TestMethod]
        public void TestParseArguments_SlnMode_ExtraSrc()
        {
            var result = Program.TryParseArguments(
                new[]
                {
                    @"Solution",
                    @"sln=C:\MyProj\sln\",
                    @"bin=C:\MyProj\bin\",
                    @"src=C:\MyProj\sln\",
                    @"Debug",
                },
                out Program.CommandLineArguments cla);

            Assert.AreEqual(false, result);
        }

        [TestMethod]
        public void TestParseArguments_SlnMode_ExtraSrc2()
        {
            var result = Program.TryParseArguments(
                new[]
                {
                    @"Solution",
                    @"src=C:\MyProj\sln\",
                    @"bin=C:\MyProj\bin\",
                    @"src=C:\MyProj\sln\",
                    @"Debug",
                },
                out Program.CommandLineArguments cla);

            Assert.AreEqual(false, result);
        }

        [TestMethod]
        public void TestParseArguments_SlnMode_ExtraProj()
        {
            var result = Program.TryParseArguments(
                new[]
                {
                    @"Solution",
                    @"sln=C:\MyProj\sln\",
                    @"bin=C:\MyProj\bin\",
                    @"C:\MyProj\MyProj.csproj",
                    @"Debug",
                },
                out Program.CommandLineArguments cla);

            Assert.AreEqual(false, result);
        }

        [TestMethod]
        public void TestParseArguments_SlnMode_ExtraDll()
        {
            var result = Program.TryParseArguments(
                new[]
                {
                    @"Solution",
                    @"sln=C:\MyProj\sln\",
                    @"bin=C:\MyProj\bin\",
                    @"C:\MyProj\MyProj\bin\Debug\MyProj.dll",
                    @"Debug",
                },
                out Program.CommandLineArguments cla);

            Assert.AreEqual(false, result);
        }

        [TestMethod]
        public void TestParseArguments_MissingBin()
        {
            var result = Program.TryParseArguments(
                new[]
                {
                    @"Solution",
                    @"sln=C:\MyProj\sln\",
                    @"Debug",
                },
                out Program.CommandLineArguments cla);

            Assert.AreEqual(false, result);
        }

        [TestMethod]
        public void TestParseArguments_MissingSln()
        {
            var result = Program.TryParseArguments(
                new[]
                {
                    @"Solution",
                    @"bin=C:\MyProj\bin\",
                    @"Debug",
                },
                out Program.CommandLineArguments cla);

            Assert.AreEqual(false, result);
        }

        [TestMethod]
        public void TestParseArguments_ProjMode_ExtraBin()
        {
            var result = Program.TryParseArguments(
                new[]
                {
                    @"C:\MyProj\MyProj.csproj",
                    @"C:\MyProj\MyProj\bin\Debug\MyProj.dll",
                    @"bin=C:\MyProj\bin\",
                    @"Debug",
                },
                out Program.CommandLineArguments cla);

            Assert.AreEqual(false, result);
        }

        [TestMethod]
        public void TestParseArguments_ProjMode_ExtraSln()
        {
            var result = Program.TryParseArguments(
                new[]
                {
                    @"C:\MyProj\MyProj.csproj",
                    @"C:\MyProj\MyProj\bin\Debug\MyProj.dll",
                    @"sln=C:\MyProj\sln\",
                    @"Debug",
                },
                out Program.CommandLineArguments cla);

            Assert.AreEqual(false, result);
        }

        [TestMethod]
        public void TestParseArguments_ProjMode_ExtraSrc()
        {
            var result = Program.TryParseArguments(
                new[]
                {
                    @"C:\MyProj\MyProj.csproj",
                    @"C:\MyProj\MyProj\bin\Debug\MyProj.dll",
                    @"src=C:\MyProj\sln\",
                    @"Debug",
                },
                out Program.CommandLineArguments cla);

            Assert.AreEqual(false, result);
        }

        [TestMethod]
        public void TestParseArguments_MissingProj()
        {
            var result = Program.TryParseArguments(
                new[]
                {
                    @"C:\MyProj\MyProj\bin\Debug\MyProj.dll",
                    @"Debug",
                },
                out Program.CommandLineArguments cla);

            Assert.AreEqual(false, result);
        }

        [TestMethod]
        public void TestParseArguments_MissingDll()
        {
            var result = Program.TryParseArguments(
                new[]
                {
                    @"C:\MyProj\MyProj.csproj",
                    @"Debug",
                },
                out Program.CommandLineArguments cla);

            Assert.AreEqual(false, result);
        }

        [TestMethod]
        public void TestParseArguments_PreReleaseOverride1()
        {
            var result = Program.TryParseArguments(
                new[]
                {
                    @"Solution",
                    @"sln=C:\MyProj\sln\",
                    @"bin=C:\MyProj\bin\",
                    @"Debug",
                    @"pre=beta",
                },
                out Program.CommandLineArguments cla);

            Assert.AreEqual(true, result);
            Assert.AreEqual(Program.Mode.Solution, cla.Mode);
            Assert.AreEqual(null, cla.ProjPath);
            Assert.AreEqual(null, cla.DllPath);
            Assert.AreEqual(@"C:\MyProj\bin\", cla.BinFolder);
            Assert.AreEqual(@"C:\MyProj\sln\", cla.SolutionFolder);
            Assert.AreEqual(true, cla.IsDebug);
            Assert.AreEqual(true, cla.DoCreateNuPkg);
            Assert.AreEqual(false, cla.DoIncludeCurrentProj);
            Assert.AreEqual("beta", cla.PreReleaseSuffixOverride);
        }

        [TestMethod]
        public void TestParseArguments_PreReleaseOverride2()
        {
            var result = Program.TryParseArguments(
                new[]
                {
                    @"Solution",
                    @"sln=C:\MyProj\sln\",
                    @"bin=C:\MyProj\bin\",
                    @"Debug",
                    @"preReleaseSuffixOverride=beta",
                },
                out Program.CommandLineArguments cla);

            Assert.AreEqual(true, result);
            Assert.AreEqual(Program.Mode.Solution, cla.Mode);
            Assert.AreEqual(null, cla.ProjPath);
            Assert.AreEqual(null, cla.DllPath);
            Assert.AreEqual(@"C:\MyProj\bin\", cla.BinFolder);
            Assert.AreEqual(@"C:\MyProj\sln\", cla.SolutionFolder);
            Assert.AreEqual(true, cla.IsDebug);
            Assert.AreEqual(true, cla.DoCreateNuPkg);
            Assert.AreEqual(false, cla.DoIncludeCurrentProj);
            Assert.AreEqual("beta", cla.PreReleaseSuffixOverride);
        }

        [TestMethod]
        public void TestParseArguments_PreReleaseOverride3()
        {
            var result = Program.TryParseArguments(
                new[]
                {
                    @"Solution",
                    @"sln=C:\MyProj\sln\",
                    @"bin=C:\MyProj\bin\",
                    @"Debug",
                    @"pre=-beta",
                },
                out Program.CommandLineArguments cla);

            Assert.AreEqual(true, result);
            Assert.AreEqual(Program.Mode.Solution, cla.Mode);
            Assert.AreEqual(null, cla.ProjPath);
            Assert.AreEqual(null, cla.DllPath);
            Assert.AreEqual(@"C:\MyProj\bin\", cla.BinFolder);
            Assert.AreEqual(@"C:\MyProj\sln\", cla.SolutionFolder);
            Assert.AreEqual(true, cla.IsDebug);
            Assert.AreEqual(true, cla.DoCreateNuPkg);
            Assert.AreEqual(false, cla.DoIncludeCurrentProj);
            Assert.AreEqual("beta", cla.PreReleaseSuffixOverride);
        }

        #endregion
    }
}
