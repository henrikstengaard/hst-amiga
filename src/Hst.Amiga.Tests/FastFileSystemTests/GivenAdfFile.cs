namespace Hst.Amiga.Tests.FastFileSystemTests
{
    using System;
    using System.IO;
    using System.Linq;
    using System.Text;
    using System.Threading.Tasks;
    using FileSystems;
    using FileSystems.FastFileSystem;
    using RigidDiskBlocks;
    using Xunit;
    using Constants = FileSystems.FastFileSystem.Constants;
    using File = System.IO.File;
    using FileMode = System.IO.FileMode;

    public class GivenAdfFile
    {
        [Fact]
        public async Task WhenFormatAdfAndCreateFileThenFileExists()
        {
            const string dosType = "DOS3";
            const string diskName = "ADF";
            
            // arrange - adf stream
            var adfStream = new MemoryStream(new byte[FloppyDiskConstants.DoubleDensity.Size]);

            // act - format adf
            await FastFileSystemFormatter.Format(adfStream, FloppyDiskConstants.DoubleDensity.LowCyl,
                FloppyDiskConstants.DoubleDensity.HighCyl,
                FloppyDiskConstants.DoubleDensity.ReservedBlocks, FloppyDiskConstants.DoubleDensity.Heads,
                FloppyDiskConstants.DoubleDensity.Sectors,
                FloppyDiskConstants.BlockSize, FloppyDiskConstants.FileSystemBlockSize,
                DosTypeHelper.FormatDosType(dosType), diskName);
            
            // arrange - mount adf volume
            await using var volume = await FastFileSystemVolume.MountAdf(adfStream);

            // act - create new file
            await volume.CreateFile("NewFile");
            
            // act - read entries recursively from root block
            var entries = (await volume.ListEntries()).OrderBy(x => x.Name).ToList();

            // assert - root block contains 1 entry
            Assert.NotEmpty(entries);
            Assert.Single(entries);

            // assert - file entry "NewFile" exists in root block
            var entry = entries.FirstOrDefault(x => x.Name == "NewFile");
            Assert.NotNull(entry);
            Assert.Equal(EntryType.File, entry.Type);
            Assert.Equal(0U, entry.Size);
        }
        
        [Theory]
        [InlineData("dos1.adf")]
        [InlineData("dos2.adf")]
        [InlineData("dos3.adf")]
        [InlineData("dos4.adf")]
        [InlineData("dos5.adf")]
        public async Task WhenMountAdfAndReadEntriesRecursivelyThenEntriesAreReturned(string adfFilename)
        {
            // arrange - adf file
            var adfPath = Path.Combine("TestData", "FastFileSystems", adfFilename);
            await using var adfStream = File.OpenRead(adfPath);

            // act - mount adf
            var volume = await FastFileSystemHelper.MountAdf(adfStream);

            // act - read entries recursively from root block
            var entries = (await FileSystems.FastFileSystem.Directory.ReadEntries(volume, volume.RootBlockOffset, true))
                .OrderBy(x => x.Name).ToList();

            // assert - root block contains 2 entries
            Assert.NotEmpty(entries);
            Assert.Equal(2, entries.Count);

            // assert - entry "test.txt" in root block
            var entry1 = entries.FirstOrDefault(x => x.Name == "test.txt");
            Assert.NotNull(entry1);
            Assert.Equal(Constants.ST_FILE, entry1.Type);
            Assert.Equal(21U, entry1.Size);
            Assert.Null(entry1.SubDir);

            // assert - entry "testdir" in root block
            var entry2 = entries.FirstOrDefault(x => x.Name == "testdir");
            Assert.NotNull(entry2);
            Assert.Equal(Constants.ST_DIR, entry2.Type);
            Assert.Equal(0U, entry2.Size);
            Assert.NotEmpty(entry2.SubDir);

            // assert - entry "test2.txt" in entry 2 sub directory
            var entry3 = entry2.SubDir.FirstOrDefault(x => x.Name == "test2.txt");
            Assert.NotNull(entry3);
            Assert.Equal(Constants.ST_FILE, entry3.Type);
            Assert.Equal(29U, entry3.Size);
        }

        [Theory]
        [InlineData("dos1.adf")]
        [InlineData("dos2.adf")]
        [InlineData("dos3.adf")]
        [InlineData("dos4.adf")]
        [InlineData("dos5.adf")]
        public async Task WhenMountAdfAndReadFileFromThenDataIsReadCorrectly(string adfFilename)
        {
            // arrange - adf file
            var adfPath = Path.Combine("TestData", "FastFileSystems", adfFilename);
            await using var adfStream = File.OpenRead(adfPath);

            // act - mount adf
            var volume = await FastFileSystemHelper.MountAdf(adfStream);

            // act - open entry stream
            var entryStream = await FileSystems.FastFileSystem.File.Open(volume, volume.RootBlockOffset,
                "test.txt", FileSystems.FileMode.Read);

            // act - read entry stream
            var buffer = new byte[512];
            var bytesRead = await entryStream.ReadAsync(buffer, 0, buffer.Length);

            // assert - read entry matches text
            Assert.Equal(21, bytesRead);
            var text = Encoding.GetEncoding("ISO-8859-1").GetString(buffer, 0, bytesRead);
            Assert.Equal("This is a test file!\n", text);
        }

        [Theory]
        [InlineData("dos1.adf")]
        [InlineData("dos2.adf")]
        [InlineData("dos3.adf")]
        [InlineData("dos4.adf")]
        [InlineData("dos5.adf")]
        public async Task WhenMountAdfAndReadFileFromEntryThenDataIsReadCorrectly(string adfFilename)
        {
            // arrange - adf file
            var adfPath = Path.Combine("TestData", "FastFileSystems", adfFilename);
            await using var adfStream = File.OpenRead(adfPath);

            // act - mount adf
            var volume = await FastFileSystemHelper.MountAdf(adfStream);

            // act - read entries recursively from root block
            var entries = (await FileSystems.FastFileSystem.Directory.ReadEntries(volume, volume.RootBlockOffset, true))
                .OrderBy(x => x.Name).ToList();

            var entry = entries.FirstOrDefault(x => x.Name == "test.txt");
            Assert.NotNull(entry);

            // act - open entry stream
            var entryStream = await FileSystems.FastFileSystem.File.Open(volume, entry);

            // act - read entry stream
            var buffer = new byte[512];
            var bytesRead = await entryStream.ReadAsync(buffer, 0, buffer.Length);

            // assert - read entry matches text
            Assert.Equal(21, bytesRead);
            var text = Encoding.GetEncoding("ISO-8859-1").GetString(buffer, 0, bytesRead);
            Assert.Equal("This is a test file!\n", text);
        }

        [Theory]
        [InlineData("dos1.adf")]
        [InlineData("dos2.adf")]
        [InlineData("dos3.adf")]
        [InlineData("dos4.adf")]
        [InlineData("dos5.adf")]
        public async Task WhenMountAdfAndWriteFileThenFileIsCreated(string adfFilename)
        {
            // arrange - adf file
            var fileName = "newtest.txt";
            var fileContent = "Hello world!";
            var adfPath = Path.Combine("TestData", "FastFileSystems", adfFilename);
            var modifiedAdfPath = string.Concat(Path.GetFileNameWithoutExtension(adfFilename), ".adf");

            // arrange - copy adf file for testing
            File.Copy(adfPath, modifiedAdfPath, true);
            await using var adfStream =
                File.Open(modifiedAdfPath, FileMode.Open, FileAccess.ReadWrite);

            // act - mount adf
            var volume = await FastFileSystemHelper.MountAdf(adfStream);

            // act - open entry stream
            var entryStream = await FileSystems.FastFileSystem.File.Open(volume, volume.RootBlockOffset,
                fileName, FileSystems.FileMode.Write);

            // act - write entry stream
            var buffer = Encoding.ASCII.GetBytes(fileContent);
            await entryStream.WriteAsync(buffer, 0, buffer.Length);
            entryStream.Close();

            // act - read entries recursively from root block
            var entries = (await FileSystems.FastFileSystem.Directory.ReadEntries(volume, volume.RootBlockOffset, true))
                .OrderBy(x => x.Name).ToList();

            // assert - entry exists
            var entry = entries.FirstOrDefault(x => x.Name == fileName);
            Assert.NotNull(entry);
            Assert.Equal(fileName, entry.Name);
            Assert.Equal((uint)fileContent.Length, entry.Size);
            Assert.Equal(Constants.ST_FILE, entry.Type);
        }

        [Theory]
        [InlineData("dos1.adf")]
        [InlineData("dos2.adf")]
        [InlineData("dos3.adf")]
        [InlineData("dos4.adf")]
        [InlineData("dos5.adf")]
        public async Task WhenMountAdfAndCreateDirectoryThenDirectoryIsCreated(string adfFilename)
        {
            // arrange - adf file
            var directoryName = "newdir";
            var adfPath = Path.Combine("TestData", "FastFileSystems", adfFilename);
            var modifiedAdfPath = string.Concat(Path.GetFileNameWithoutExtension(adfFilename), ".adf");

            // arrange - copy adf file for testing
            File.Copy(adfPath, modifiedAdfPath, true);
            await using var adfStream =
                File.Open(modifiedAdfPath, FileMode.Open, FileAccess.ReadWrite);

            // act - mount adf
            var volume = await FastFileSystemHelper.MountAdf(adfStream);

            // act - create directory in root block
            await FileSystems.FastFileSystem.Directory.CreateDirectory(volume, volume.RootBlockOffset, directoryName);

            // act - read entries recursively from root block
            var entries = (await FileSystems.FastFileSystem.Directory.ReadEntries(volume, volume.RootBlockOffset, true))
                .OrderBy(x => x.Name).ToList();

            // assert - entry exists
            var entry = entries.FirstOrDefault(x => x.Name == directoryName);
            Assert.NotNull(entry);
            Assert.Equal(directoryName, entry.Name);
            Assert.Equal(0U, entry.Size);
            Assert.Equal(Constants.ST_DIR, entry.Type);
        }

        [Theory]
        [InlineData("dos1.adf")]
        [InlineData("dos2.adf")]
        [InlineData("dos3.adf")]
        [InlineData("dos4.adf")]
        [InlineData("dos5.adf")]
        public async Task WhenMountAdfAndRenameFileThenFileIsRenamed(string adfFilename)
        {
            // arrange - adf file
            var newName = "renamed_test";
            var adfPath = Path.Combine("TestData", "FastFileSystems", adfFilename);
            var modifiedAdfPath = string.Concat(Path.GetFileNameWithoutExtension(adfFilename), ".adf");

            // arrange - copy adf file for testing
            File.Copy(adfPath, modifiedAdfPath, true);
            await using var adfStream =
                File.Open(modifiedAdfPath, FileMode.Open, FileAccess.ReadWrite);

            // act - mount adf
            var volume = await FastFileSystemHelper.MountAdf(adfStream);

            // act - read entries recursively from root block
            var entries = (await FileSystems.FastFileSystem.Directory.ReadEntries(volume, volume.RootBlockOffset, true))
                .OrderBy(x => x.Name).ToList();

            // act - get first file entry
            var oldEntry = entries.FirstOrDefault(x => x.Type == Constants.ST_FILE);
            Assert.NotNull(oldEntry);

            // act - rename file entry
            var oldName = oldEntry.Name;
            await FileSystems.FastFileSystem.Directory.RenameEntry(volume, oldEntry.Parent, oldName, oldEntry.Parent,
                newName);

            // act - read entries recursively from root block
            entries = (await FileSystems.FastFileSystem.Directory.ReadEntries(volume, volume.RootBlockOffset, true))
                .OrderBy(x => x.Name).ToList();

            // assert - entry with old name doesn't exist
            var entry = entries.FirstOrDefault(x => x.Name == oldName);
            Assert.Null(entry);

            // assert - entry with new name exist
            entry = entries.FirstOrDefault(x => x.Name == newName);
            Assert.NotNull(entry);
            Assert.Equal(newName, entry.Name);
            Assert.Equal(oldEntry.Size, entry.Size);
            Assert.Equal(oldEntry.Type, entry.Type);
        }

        [Theory]
        [InlineData("dos1.adf")]
        [InlineData("dos2.adf")]
        [InlineData("dos3.adf")]
        [InlineData("dos4.adf")]
        [InlineData("dos5.adf")]
        public async Task WhenMountAdfAndDeleteFileThenFileIsDeleted(string adfFilename)
        {
            // arrange - adf file
            var adfPath = Path.Combine("TestData", "FastFileSystems", adfFilename);
            var modifiedAdfPath = string.Concat(Path.GetFileNameWithoutExtension(adfFilename), ".adf");

            // arrange - copy adf file for testing
            File.Copy(adfPath, modifiedAdfPath, true);
            await using var adfStream =
                File.Open(modifiedAdfPath, FileMode.Open, FileAccess.ReadWrite);

            // act - mount adf
            var volume = await FastFileSystemHelper.MountAdf(adfStream);

            // act - read entries recursively from root block
            var entries = (await FileSystems.FastFileSystem.Directory.ReadEntries(volume, volume.RootBlockOffset, true))
                .OrderBy(x => x.Name).ToList();

            // act - get first file entry
            var entry = entries.FirstOrDefault(x => x.Type == Constants.ST_FILE);
            Assert.NotNull(entry);

            // act - remote entry from root block
            var entryName = entry.Name;
            await FileSystems.FastFileSystem.Directory.RemoveEntry(volume, volume.RootBlockOffset, entryName, true);

            // act - read entries recursively from root block
            entries = (await FileSystems.FastFileSystem.Directory.ReadEntries(volume, volume.RootBlockOffset, true))
                .OrderBy(x => x.Name).ToList();

            // assert - entry doesn't exist, is removed
            entry = entries.FirstOrDefault(x => x.Name == entryName);
            Assert.Null(entry);
        }

        [Theory]
        [InlineData("dos1.adf")]
        [InlineData("dos2.adf")]
        [InlineData("dos3.adf")]
        [InlineData("dos4.adf")]
        [InlineData("dos5.adf")]
        public async Task WhenMountAdfAndSetAccessForFileThenEntryIsUpdated(string adfFilename)
        {
            // arrange - adf file
            var adfPath = Path.Combine("TestData", "FastFileSystems", adfFilename);
            var modifiedAdfPath = string.Concat(Path.GetFileNameWithoutExtension(adfFilename), ".adf");

            // arrange - copy adf file for testing
            File.Copy(adfPath, modifiedAdfPath, true);
            await using var adfStream =
                File.Open(modifiedAdfPath, FileMode.Open, FileAccess.ReadWrite);

            // act - mount adf
            var volume = await FastFileSystemHelper.MountAdf(adfStream);

            // act - read entries recursively from root block
            var entries = (await FileSystems.FastFileSystem.Directory.ReadEntries(volume, volume.RootBlockOffset, true))
                .OrderBy(x => x.Name).ToList();

            // act - get first file entry
            var entry = entries.FirstOrDefault(x => x.Type == Constants.ST_FILE);
            Assert.NotNull(entry);

            // act - set access for entry
            var entryName = entry.Name;
            await FileSystems.FastFileSystem.Directory.SetEntryAccess(volume, volume.RootBlockOffset, entryName,
                Constants.ACCMASK_A | Constants.ACCMASK_E | Constants.ACCMASK_W);

            // act - read entries recursively from root block
            entries = (await FileSystems.FastFileSystem.Directory.ReadEntries(volume, volume.RootBlockOffset, true))
                .OrderBy(x => x.Name).ToList();

            // assert - get entry
            entry = entries.FirstOrDefault(x => x.Name == entryName);
            Assert.NotNull(entry);
            Assert.Equal((uint)(Constants.ACCMASK_A | Constants.ACCMASK_E | Constants.ACCMASK_W), entry.Access);

            // act - set access for entry
            await FileSystems.FastFileSystem.Directory.SetEntryAccess(volume, volume.RootBlockOffset, entryName,
                Constants.ACCMASK_R);

            // act - read entries recursively from root block
            entries = (await FileSystems.FastFileSystem.Directory.ReadEntries(volume, volume.RootBlockOffset, true))
                .OrderBy(x => x.Name).ToList();

            // assert - get entry
            entry = entries.FirstOrDefault(x => x.Name == entryName);
            Assert.NotNull(entry);
            Assert.Equal((uint)Constants.ACCMASK_R, entry.Access);
        }

        [Theory]
        [InlineData("dos1.adf")]
        [InlineData("dos2.adf")]
        [InlineData("dos3.adf")]
        [InlineData("dos4.adf")]
        [InlineData("dos5.adf")]
        public async Task WhenMountAdfAndSetCommentForFileThenEntryIsUpdated(string adfFilename)
        {
            // arrange - adf file
            var adfPath = Path.Combine("TestData", "FastFileSystems", adfFilename);
            var modifiedAdfPath = string.Concat(Path.GetFileNameWithoutExtension(adfFilename), ".adf");

            // arrange - copy adf file for testing
            File.Copy(adfPath, modifiedAdfPath, true);
            await using var adfStream =
                File.Open(modifiedAdfPath, FileMode.Open, FileAccess.ReadWrite);

            // act - mount adf
            var volume = await FastFileSystemHelper.MountAdf(adfStream);

            // act - read entries recursively from root block
            var entries = (await FileSystems.FastFileSystem.Directory.ReadEntries(volume, volume.RootBlockOffset, true))
                .OrderBy(x => x.Name).ToList();

            // act - get first file entry
            var entry = entries.FirstOrDefault(x => x.Type == Constants.ST_FILE);
            Assert.NotNull(entry);
            Assert.Equal(string.Empty, entry.Comment);

            // act - set access for entry
            var entryName = entry.Name;
            await FileSystems.FastFileSystem.Directory.SetEntryComment(volume, volume.RootBlockOffset, entryName,
                "A comment");

            // act - read entries recursively from root block
            entries = (await FileSystems.FastFileSystem.Directory.ReadEntries(volume, volume.RootBlockOffset, true))
                .OrderBy(x => x.Name).ToList();

            // assert - get entry
            entry = entries.FirstOrDefault(x => x.Name == entryName);
            Assert.NotNull(entry);
            Assert.Equal("A comment", entry.Comment);
        }
    }
}