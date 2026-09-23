using System.Text;
using CulinaryBlog.Application.Common.Helpers;
using Microsoft.AspNetCore.Http;
using Xunit;

namespace CulinaryBlog.Tests;

public class FileValidationTests
{
    [Theory]
    [InlineData("524946461000000057454250", "image/webp", true)]
    [InlineData("524946471000000057454250", "image/webp", false)]
    [InlineData("5249464610000000574542", "image/webp", false)]
    [InlineData("89504E470D0A1A0A", "image/png", true)]
    [InlineData("89504E47", "image/png", false)]
    [InlineData("89504E4700000000", "image/png", false)]
    [InlineData("FFD8FF", "image/jpeg", true)]
    [InlineData("FFD8", "image/jpeg", false)]
    [InlineData("00000010667479706176696600000000", "image/avif", true)]
    [InlineData("00000010667479706D69663100000000", "image/avif", false)]
    [InlineData("00000014667479706D6966310000000061766966", "image/avif", true)]
    [InlineData("00000014667479706D69663100000000", "image/avif", false)]
    public async Task RecognizesSignaturesAndRestoresStream(string hex, string mime, bool expected)
    {
        using var stream = new ShortReadStream(Convert.FromHexString(hex));
        stream.Position = 1;
        Assert.Equal(expected, await FileValidationHelper.ValidateMagicBytesAsync(stream, mime));
        Assert.Equal(1, stream.Position);
    }

    [Theory]
    [InlineData("../recipes")]
    [InlineData("recipes#x")]
    [InlineData("recipes?x")]
    [InlineData("recipes//x")]
    [InlineData("/recipes")]
    [InlineData("recipes\\x")]
    [InlineData("recipes/%2e%2e")]
    public void RejectsUnsafeFolders(string folder) =>
        Assert.Throws<ArgumentException>(() => FileValidationHelper.ValidateFolder(folder));

    [Fact]
    public async Task RejectsExtensionMimeMismatchAndOversize()
    {
        using var stream = new MemoryStream(Encoding.ASCII.GetBytes("RIFFxxxxWEBP"));
        var mismatch = new FormFile(stream, 0, stream.Length, "file", "image.png")
        { Headers = new HeaderDictionary(), ContentType = "image/webp" };
        await Assert.ThrowsAsync<ArgumentException>(() => FileValidationHelper.ValidateImageFileAsync(mismatch));
        var oversized = new FormFile(stream, 0, FileValidationHelper.MaxFileSizeBytes + 1, "file", "image.webp")
        { Headers = new HeaderDictionary(), ContentType = "image/webp" };
        await Assert.ThrowsAsync<ArgumentException>(() => FileValidationHelper.ValidateImageFileAsync(oversized));
    }

    private sealed class ShortReadStream(byte[] bytes) : MemoryStream(bytes)
    {
        public override ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken ct = default) =>
            base.ReadAsync(buffer[..Math.Min(2, buffer.Length)], ct);
    }
}
