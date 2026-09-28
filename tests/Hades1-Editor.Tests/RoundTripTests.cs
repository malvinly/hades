using Hades.SaveFormat;

namespace Hades1Editor.Tests;

public class RoundTripTests
{
    [Fact]
    public void Unedited_save_writes_back_byte_for_byte()
    {
        var original = File.ReadAllBytes(TestData.Profile1);

        var written = HadesSave.Read(original).Write();

        Assert.Equal(original, written);
    }
}
