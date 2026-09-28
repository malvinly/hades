using Hades.SaveFormat;

namespace HadesEditor.Tests;

/// <summary>Crafted Lua data must be rejected with a clear error, never a crash or a huge allocation.</summary>
public class MalformedInputTests
{
    [Fact]
    public void Deeply_nested_tables_are_rejected()
    {
        // Each level: 'T', arraySize 0, hashSize 1, key 'N' + 8 bytes, then the nested value.
        var stream = new MemoryStream();
        var writer = new BinaryWriter(stream);
        writer.Write((byte)1);
        for (var i = 0; i < 100_000; i++)
        {
            writer.Write((byte)'T'); writer.Write(0); writer.Write(1);
            writer.Write((byte)'N'); writer.Write(0L);
        }
        writer.Write((byte)'-');

        var error = Assert.Throws<SaveFormatException>(() => Luabins.Read(stream.ToArray()));
        Assert.Contains("nested deeper", error.Message);
    }

    [Fact]
    public void String_length_beyond_the_data_is_rejected_without_allocating()
    {
        byte[] data = [1, (byte)'S', 0xF0, 0xFF, 0xFF, 0x7F, 1, 2, 3];

        var error = Assert.Throws<SaveFormatException>(() => Luabins.Read(data));
        Assert.Contains("exceeds the remaining data", error.Message);
    }

    [Fact]
    public void Short_string_at_end_of_data_is_rejected()
    {
        byte[] data = [1, (byte)'S', 10, 0, 0, 0, 1, 2, 3];

        Assert.Throws<SaveFormatException>(() => Luabins.Read(data));
    }

    [Fact]
    public void Table_size_beyond_the_data_is_rejected_without_allocating()
    {
        byte[] data = [1, (byte)'T', 0xFF, 0xFF, 0xFF, 0x7F, 0xFF, 0xFF, 0xFF, 0x7F];

        var error = Assert.Throws<SaveFormatException>(() => Luabins.Read(data));
        Assert.Contains("exceeds the remaining data", error.Message);
    }

    [Fact]
    public void Compressed_block_length_beyond_the_file_is_rejected()
    {
        var bytes = File.ReadAllBytes(TestData.Hades1);
        BitConverter.TryWriteBytes(bytes.AsSpan(CompressedLengthOffset(bytes)), int.MaxValue);
        BitConverter.TryWriteBytes(bytes.AsSpan(4), Adler32(bytes.AsSpan(8)));

        var error = Assert.Throws<SaveFormatException>(() => HadesSave.Read(bytes));
        Assert.Contains("exceeds the remaining data", error.Message);
    }

    /// <summary>The compressed block is the last thing in the file, so its length prefix is the int32 that says "everything after me".</summary>
    private static int CompressedLengthOffset(byte[] bytes)
    {
        for (var offset = 8; offset < bytes.Length - 4; offset++)
            if (BitConverter.ToInt32(bytes, offset) == bytes.Length - offset - 4)
                return offset;
        throw new InvalidOperationException("Could not locate the compressed block length prefix");
    }

    private static uint Adler32(ReadOnlySpan<byte> data)
    {
        uint a = 1, b = 0;
        foreach (var value in data)
        {
            a = (a + value) % 65521;
            b = (b + a) % 65521;
        }
        return (b << 16) | a;
    }
}
