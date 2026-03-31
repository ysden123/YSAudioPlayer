using System.Text.Json;
using YSAudioPlayerApp;

namespace YSAudioPlayerTest;

[TestClass]
public class LastPositionTests
{
    [TestMethod]
    public void TestSerialize()
    {
        LastPosition position = new()
        {
            FilePath = @"C:\Music\song.mp3",
            CurrentTime = TimeSpan.FromMinutes(2)
        };
        string json = JsonSerializer.Serialize(position);
        Assert.IsNotNull(json);
        Console.WriteLine($"json:{json}");
    }

    [TestMethod]
    public void TestDeserialize()
    {
        LastPosition positionOriginal = new()
        {
            FilePath = @"C:\Music\song.mp3",
            CurrentTime = TimeSpan.FromMinutes(2)
        };
        string json = JsonSerializer.Serialize(positionOriginal);

        LastPosition? positionDeserialized = JsonSerializer.Deserialize<LastPosition>(json);
        Assert.IsNotNull(positionDeserialized);
        Assert.AreEqual(positionOriginal.FilePath, positionDeserialized!.FilePath);
        Assert.AreEqual(positionOriginal.CurrentTime, positionDeserialized!.CurrentTime);
        Assert.AreEqual(positionOriginal.FolderName, positionDeserialized!.FolderName);
    }

    [TestMethod]
    public void ToJsonTest()
    {
        LastPosition position = new()
        {
            FilePath = @"C:\Music\song.mp3",
            CurrentTime = TimeSpan.FromMinutes(2)
        };
        string json = position.ToJson();
        Assert.IsNotNull(json);
        Console.WriteLine($"json:{json}");
    }

    [TestMethod]
    public void FromJsonTest()
    {
        LastPosition positionOriginal = new()
        {
            FilePath = @"C:\Music\song.mp3",
            CurrentTime = TimeSpan.FromMinutes(2)
        };
        string json = positionOriginal.ToJson();

        LastPosition? positionDeserialized = LastPosition.FromJson(json);
        Assert.IsNotNull(positionDeserialized);
        Assert.AreEqual(positionOriginal.FilePath, positionDeserialized!.FilePath);
        Assert.AreEqual(positionOriginal.CurrentTime, positionDeserialized!.CurrentTime);
        Assert.AreEqual(positionOriginal.FolderName, positionDeserialized!.FolderName);
    }

    [TestMethod]
    public void FromJsonEmptyTest()
    {
        LastPosition positionOriginal = new();
        string json = positionOriginal.ToJson();

        LastPosition? positionDeserialized = LastPosition.FromJson(json);
        Assert.IsNotNull(positionDeserialized);
        Assert.AreEqual(positionOriginal.FilePath, positionDeserialized!.FilePath);
        Assert.AreEqual(positionOriginal.CurrentTime, positionDeserialized!.CurrentTime);
        Assert.AreEqual(positionOriginal.FolderName, positionDeserialized!.FolderName);
    }
}
