using FilmApp.Services;
using Xunit;

namespace FilmApp.Tests;

public class PasswordHasherTests
{
    [Fact]
    public void Verify_CorrectPassword_ReturnsTrue()
    {
        string hash = PasswordHasher.Hash("secret123");
        Assert.True(PasswordHasher.Verify("secret123", hash));
    }

    [Fact]
    public void Verify_WrongPassword_ReturnsFalse()
    {
        string hash = PasswordHasher.Hash("secret123");
        Assert.False(PasswordHasher.Verify("other", hash));
    }

    [Fact]
    public void Hash_SamePasswordTwice_GivesDifferentHashes()
    {
        Assert.NotEqual(PasswordHasher.Hash("secret123"), PasswordHasher.Hash("secret123"));
    }

    [Theory]
    [InlineData("")]
    [InlineData("abc")]
    [InlineData("1.2.3.4")]
    [InlineData("x.y.z")]
    public void Verify_BrokenStoredValue_ReturnsFalse(string stored)
    {
        Assert.False(PasswordHasher.Verify("secret123", stored));
    }
}