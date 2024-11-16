using System.IO.Abstractions.TestingHelpers;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using PLang.Modules.FileModule;

namespace PLangTests.Modules.FileModule;

[TestClass]
public class ProgramTests : BasePLangTest
{
    private Program p;

    [TestInitialize]
    public void Init()
    {
        Initialize();
        p = new Program(fileSystem, settings, logger, pseudoRuntime, engine);
        p.Init(container, null, null, null, memoryStack, logger, context, typeHelper, llmServiceFactory, settings,
            appCache, null);
    }

    [TestMethod]
    public async Task ReadBinaryFileAndConvertToBase64_Test()
    {
        var path = Path.Join(fileSystem.RootDirectory, "image.png");
        var onePx = "iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAYAAAAfFcSJAAAADUlEQVR42mP8z8BQDwAEhQGAhKmMIQAAAABJRU5ErkJggg==";
        var bytes = Convert.FromBase64String(onePx);
        fileSystem.AddFile(path, new MockFileData(bytes));


        var base64 = await p.ReadBinaryFileAndConvertToBase64(path);
        Assert.AreEqual(onePx, base64);
    }

    [TestMethod]
    public async Task ReadTextFile_Test()
    {
        var path = Path.Join(fileSystem.RootDirectory, "file.txt");
        var text = "Hello PLang World";
        fileSystem.AddFile(path, new MockFileData(text));

        var txt = await p.ReadTextFile(path);
        Assert.AreEqual(text, txt);
    }


    [TestMethod]
    public async Task ReadFileAsStream_Test()
    {
        var path = Path.Join(fileSystem.RootDirectory, "file.txt");
        var text = "Hello PLang World";
        fileSystem.AddFile(path, new MockFileData(text));

        Stream? stream = null;
        try
        {
            stream = await p.ReadFileAsStream(path);

            Assert.AreEqual(text.Length, stream.Length);
        }
        finally
        {
            if (stream != null) stream.Close();
        }
    }

    [TestMethod]
    public async Task ReadMultipleTextFiles_Test()
    {
        var path1 = Path.Join(fileSystem.RootDirectory, "file1.txt");
        var path2 = Path.Join(fileSystem.RootDirectory, "file2.txt");

        var text1 = "Text 1";
        var text2 = "Text 2";

        fileSystem.AddFile(path1, new MockFileData(text1));
        fileSystem.AddFile(path2, new MockFileData(text2));

        var files = await p.ReadMultipleTextFiles(fileSystem.RootDirectory, "*.txt");

        Assert.AreEqual(text1, files[0].Content);
        Assert.AreEqual(text2, files[1].Content);
    }

    [TestMethod]
    public async Task WriteToFile_Test()
    {
        var path = Path.Join(fileSystem.RootDirectory, "file.txt");
        var text = "Hello PLang World";

        await p.WriteToFile(path, text);


        Assert.IsTrue(fileSystem.File.Exists(path));
        Assert.AreEqual(text, fileSystem.File.ReadAllText(path));
    }

    [TestMethod]
    public async Task ReadExcel()
    {
        var path = "Book1.xlsx";
        var fullPath = Path.Combine(fileSystem.RootDirectory, path);
        var stream = new FileStream(fullPath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
        byte[] fileBytes;
        using (var reader = new BinaryReader(stream))
        {
            fileBytes = reader.ReadBytes((int)stream.Length);
        }

        fileSystem.AddFile(fullPath, new MockFileData(fileBytes));

        var dictionary = new Dictionary<string, object>();
        dictionary.Add("Sheet-1", "Sheet1");
        dictionary.Add("Orders 2023-B3:E5", "Orders2023");
        await p.ReadExcelFile(path, true, dictionary);

        Assert.IsNotNull(memoryStack.Get("Sheet1"));
        Assert.IsNotNull(memoryStack.Get("Orders2023"));
        Assert.AreEqual((double)1, memoryStack.Get("Sheet1[1].Id"));


        // when excel file is read, it is not fully loaded. This give the speed of loading 10 million rows instantly
        // This mean that you cannot call some function, such as Count on it until you Load the data.
        memoryStack.Put("Sheet1", memoryStack.Get("Sheet1.Load()"));


        Assert.AreEqual(2, memoryStack.Get("Sheet1.Count"));
        Assert.AreEqual(1, memoryStack.Get("Sheet1.Take(1).Count()"));
    }

    [TestMethod]
    public async Task SaveExcel()
    {
        var path = "ExcelTestFile.xlsx";
        fileSystem.AddDirectory(fileSystem.RootDirectory);
        List<Dictionary<string, object>> sheet1 = new();
        var row = new Dictionary<string, object>();
        row.Add("A", "Id");
        row.Add("B", "Name");
        sheet1.Add(row);
        row = new Dictionary<string, object>();
        row.Add("A", "1");
        row.Add("B", "Oscar Martinez");
        sheet1.Add(row);
        memoryStack.Put("Sheet1", sheet1);

        List<Dictionary<string, object>> sheet2 = new();
        row = new Dictionary<string, object>();
        row.Add("A", "Id");
        row.Add("B", "Name");
        sheet2.Add(row);
        row = new Dictionary<string, object>();
        row.Add("A", "1");
        row.Add("B", "Pam Beesly");
        sheet2.Add(row);
        memoryStack.Put("Sheet2", sheet2);

        var sheets = new Dictionary<string, object>();
        sheets.Add("Sheet1", memoryStack.Get("Sheet1"));
        sheets.Add("Sheet2", memoryStack.Get("Sheet2"));

        await p.WriteExcelFile(path, sheets, overwrite: true);
        Assert.IsNotNull(memoryStack.Get("Sheet1"));
    }

    [TestMethod]
    public async Task ReadCsv()
    {
        var path = "Test5x2.csv";
        var fullPath = Path.Combine(fileSystem.RootDirectory, path);

        fileSystem.AddFile(fullPath, new MockFileData(File.ReadAllText(fullPath)));

        var data = await p.ReadCsvFile(path) as IEnumerable<object>;
        var nr = 2;
        foreach (dynamic row in data)
        {
            Assert.AreEqual(row.A1, "A" + nr);
            Assert.AreEqual(row.B1, "B" + nr);
            nr++;
        }

        await p.WriteCsvFile(path, data);
    }

    [TestMethod]
    public async Task SaveCsv()
    {
        var path = "CSVTestFile.csv";
        fileSystem.AddDirectory(fileSystem.RootDirectory);
        List<dynamic> sheet1 = new();
        sheet1.Add(new { A = "Id", B = "Name" });
        sheet1.Add(new { A = "1", B = "Oscar Martinez" });

        List<dynamic> sheet2 = new();
        sheet2.Add(new { A = "2", B = "Pam Beesly" });


        await p.WriteCsvFile(path, sheet1);
        await p.WriteCsvFile(path, sheet2, true);
    }


    [TestMethod]
    public async Task ReadExcel_1MillionRows()
    {
        var path = "Test1,000,000x10.xlsx";
        var fullPath = Path.Combine(fileSystem.RootDirectory, path);
        var stream = new FileStream(fullPath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
        byte[] fileBytes;
        using (var reader = new BinaryReader(stream))
        {
            fileBytes = reader.ReadBytes((int)stream.Length);
        }

        fileSystem.AddFile(fullPath, new MockFileData(fileBytes));


        await p.ReadExcelFile(path, false);

        Assert.IsNotNull(memoryStack.Get("Sheet1"));
    }


    [TestMethod]
    public async Task AppendToFile_Test()
    {
        var path = Path.Join(fileSystem.RootDirectory, "file.txt");
        var text = "Hello PLang World";

        await p.AppendToFile(path, text);
        await p.AppendToFile(path, text);


        Assert.IsTrue(fileSystem.File.Exists(path));
        Assert.AreEqual(text + text, fileSystem.File.ReadAllText(path));
    }

    [TestMethod]
    public async Task CopyFile_Test()
    {
        var path = Path.Join(fileSystem.RootDirectory, "file.txt");
        var copyPath = Path.Join(fileSystem.RootDirectory, "file2.txt");
        var text = "Hello PLang World";

        await p.WriteToFile(path, text);
        await p.CopyFile(path, copyPath);

        Assert.IsTrue(fileSystem.File.Exists(copyPath));
        Assert.AreEqual(text, fileSystem.File.ReadAllText(copyPath));
    }

    [TestMethod]
    public async Task DeleteFile_Test()
    {
        var path = Path.Join(fileSystem.RootDirectory, "file.txt");
        var text = "Hello PLang World";

        await p.WriteToFile(path, text);


        Assert.IsTrue(fileSystem.File.Exists(path));

        await p.DeleteFile(path);

        Assert.IsFalse(fileSystem.File.Exists(path));
    }

    [TestMethod]
    public async Task GetFileInfo_Test()
    {
        var path = Path.Join(fileSystem.RootDirectory, "file.txt");
        var text = "Hello PLang World";

        await p.WriteToFile(path, text);
        var fi = await p.GetFileInfo(path);


        Assert.IsTrue(fi.Exists);
    }
    /*
    [TestMethod]
    public async Task CreateDirectory_Test()
    {
        string path = Path.Join(fileSystem.RootDirectory, "folder");

        await p.CreateDirectory(path);
        Assert.IsTrue(await p.DirectoryExists(path));

    }
    [TestMethod]
    public async Task DeleteDirectory_Test()
    {
        string path = Path.Join(fileSystem.RootDirectory, "folder");

        await p.CreateDirectory(path);
        Assert.IsTrue(await p.DirectoryExists(path));
        await p.DeleteDirectory(path);
        Assert.IsFalse(await p.DirectoryExists(path));
    }*/

    [TestMethod]
    public async Task GetFilesInDir_Test()
    {
        fileSystem.AddDirectory(fileSystem.RootDirectory);
        fileSystem.AddFile(Path.Join(fileSystem.RootDirectory, "start.goal"), new MockFileData(""));
        fileSystem.AddFile(Path.Join(fileSystem.RootDirectory, "setup.goal"), new MockFileData(""));
        fileSystem.AddFile(Path.Join(fileSystem.RootDirectory, "run.goal"), new MockFileData(""));
        fileSystem.AddFile(Path.Join(fileSystem.RootDirectory, "web.goal"), new MockFileData(""));
        fileSystem.AddFile(Path.Join(fileSystem.RootDirectory, "apps", "test", "test.goal"), new MockFileData(""));
        fileSystem.AddFile(Path.Join(fileSystem.RootDirectory, "apps", "test", "run.goal"), new MockFileData(""));

        var paths = await p.GetFilePathsInDirectory(fileSystem.RootDirectory, "*.goal", new[] { "test/test.goal" },
            true);

        Assert.IsFalse(paths.Contains("test.goal"));
        Assert.IsTrue(paths.Length > 5);
        var i = 0;
    }
}