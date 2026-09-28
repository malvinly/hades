using Hades.SaveFormat;

namespace HadesEditor.Tests;

public class RoundTripTests
{
    public static TheoryData<string> Saves => [TestData.Hades1, TestData.Hades2];

    [Theory]
    [MemberData(nameof(Saves))]
    public void Unedited_save_writes_back_byte_for_byte(string path)
    {
        var original = File.ReadAllBytes(path);

        var written = HadesSave.Read(original).Write();

        Assert.Equal(original, written);
    }
}
