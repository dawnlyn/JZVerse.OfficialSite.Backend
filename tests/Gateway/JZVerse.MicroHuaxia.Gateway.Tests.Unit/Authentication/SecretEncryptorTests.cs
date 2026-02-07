using FluentAssertions;
using JZVerse.MicroHuaxia.Gateway.Core.Authentication;
using NUnit.Framework;

namespace JZVerse.MicroHuaxia.Gateway.Tests.Unit.Authentication;

[TestFixture]
public class SecretEncryptorTests
{
    [Test]
    public void AesSecretEncryptor_EncryptDecrypt_ShouldRoundTrip()
    {
        // Arrange
        var encryptor = new AesSecretEncryptor("test-master-key-for-encryption");
        var plainText = "my-secret-jwt-key";

        // Act
        var encrypted = encryptor.Encrypt(plainText);
        var decrypted = encryptor.Decrypt(encrypted);

        // Assert
        encrypted.Should().StartWith("encrypted:");
        decrypted.Should().Be(plainText);
    }

    [Test]
    public void AesSecretEncryptor_IsEncrypted_ShouldReturnTrueForEncryptedText()
    {
        // Arrange
        var encryptor = new AesSecretEncryptor("test-master-key");
        var encrypted = encryptor.Encrypt("secret");

        // Act & Assert
        encryptor.IsEncrypted(encrypted).Should().BeTrue();
        encryptor.IsEncrypted("plain-text").Should().BeFalse();
        encryptor.IsEncrypted("encrypted:").Should().BeTrue();
    }

    [Test]
    public void AesSecretEncryptor_GetOrDecrypt_ShouldDecryptIfEncrypted()
    {
        // Arrange
        var encryptor = new AesSecretEncryptor("test-master-key");
        var plainText = "secret";
        var encrypted = encryptor.Encrypt(plainText);

        // Act
        var result1 = encryptor.GetOrDecrypt(encrypted);
        var result2 = encryptor.GetOrDecrypt(plainText);

        // Assert
        result1.Should().Be(plainText);
        result2.Should().Be(plainText);
    }

    [Test]
    public void NoOpSecretEncryptor_EncryptDecrypt_ShouldRoundTrip()
    {
        // Arrange
        var encryptor = new NoOpSecretEncryptor();
        var plainText = "my-secret";

        // Act
        var encrypted = encryptor.Encrypt(plainText);
        var decrypted = encryptor.Decrypt(encrypted);

        // Assert
        encrypted.Should().StartWith("encrypted:");
        decrypted.Should().Be(plainText);
    }

    [Test]
    public void NoOpSecretEncryptor_GetOrDecrypt_ShouldDecryptIfEncrypted()
    {
        // Arrange
        var encryptor = new NoOpSecretEncryptor();
        var plainText = "secret";
        var encrypted = encryptor.Encrypt(plainText);

        // Act
        var result1 = encryptor.GetOrDecrypt(encrypted);
        var result2 = encryptor.GetOrDecrypt(plainText);

        // Assert
        result1.Should().Be(plainText);
        result2.Should().Be(plainText);
    }

    [Test]
    public void AesSecretEncryptor_DifferentKeys_ShouldProduceDifferentResults()
    {
        // Arrange
        var encryptor1 = new AesSecretEncryptor("key-1");
        var encryptor2 = new AesSecretEncryptor("key-2");
        var plainText = "secret";

        // Act
        var encrypted1 = encryptor1.Encrypt(plainText);
        var encrypted2 = encryptor2.Encrypt(plainText);

        // Assert
        encrypted1.Should().NotBe(encrypted2);
    }
}
