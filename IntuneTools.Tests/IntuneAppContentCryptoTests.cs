using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Security.Cryptography;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace IntuneTools.Tests;

/// <summary>
/// Deterministic AES-256-CBC + HMAC-SHA256 round-trip checks for the Intune
/// content engine's crypto layer. The engine's internals are reached via
/// reflection because the type is internal to <c>IntuneTools</c>; this keeps
/// the production surface clean while still letting us catch regressions
/// without needing a real Intune tenant.
/// </summary>
public class IntuneAppContentCryptoTests
{
    private const string CryptoTypeName = "IntuneTools.Graph.IntuneHelperClasses.Applications.IntuneAppContentCrypto";
    private const string MaterialTypeName = "IntuneTools.Graph.IntuneHelperClasses.Applications.EncryptionMaterial";

    private static readonly Assembly IntuneToolsAssembly = typeof(IntuneTools.Utilities.ContentTypeRegistry).Assembly;

    private static Type CryptoType => IntuneToolsAssembly.GetType(CryptoTypeName, throwOnError: true)!;
    private static Type MaterialType => IntuneToolsAssembly.GetType(MaterialTypeName, throwOnError: true)!;

    private static object CreateMaterial(byte[] encKey, byte[] hmacKey, byte[] iv)
    {
        return Activator.CreateInstance(MaterialType, encKey, hmacKey, iv)!;
    }

    private static async Task<(byte[] FileDigest, long EncryptedSize)> EncryptAsync(
        Stream src, Stream dst, object material, int bufferSize)
    {
        var method = CryptoType.GetMethod("EncryptStreamAsync", BindingFlags.Public | BindingFlags.Static)!;
        var task = (Task)method.Invoke(null, new[] { src, dst, material, (object)bufferSize, CancellationToken.None })!;
        await task.ConfigureAwait(false);
        var resultProp = task.GetType().GetProperty("Result")!;
        var result = resultProp.GetValue(task)!;
        var fileDigest = (byte[])result.GetType().GetProperty("FileDigest")!.GetValue(result)!;
        var encryptedSize = (long)result.GetType().GetProperty("EncryptedSize")!.GetValue(result)!;
        return (fileDigest, encryptedSize);
    }

    private static async Task DecryptAsync(Stream src, Stream dst, byte[] encKey, byte[] hmacKey, int bufferSize)
    {
        var method = CryptoType.GetMethod("DecryptStreamAsync", BindingFlags.Public | BindingFlags.Static)!;
        var task = (Task)method.Invoke(null, new object[] { src, dst, encKey, hmacKey, bufferSize, CancellationToken.None })!;
        await task.ConfigureAwait(false);
    }

    private static byte[] DeterministicBytes(int length, int seed)
    {
        var bytes = new byte[length];
        var rng = new Random(seed);
        rng.NextBytes(bytes);
        return bytes;
    }

    [Theory]
    [InlineData(0)]            // empty plaintext (PKCS7 still produces 16 bytes)
    [InlineData(1)]            // sub-block
    [InlineData(15)]           // just under one AES block
    [InlineData(16)]           // exactly one AES block
    [InlineData(17)]           // just over one AES block
    [InlineData(1024)]         // a kilobyte
    [InlineData(256 * 1024)]   // exactly the default stream buffer
    [InlineData(1024 * 1024)]  // one MiB — crosses several buffer reads
    public async Task Encrypt_then_Decrypt_round_trips_arbitrary_payloads(int payloadSize)
    {
        var encKey = DeterministicBytes(32, 1);
        var hmacKey = DeterministicBytes(32, 2);
        var iv = DeterministicBytes(16, 3);
        var material = CreateMaterial(encKey, hmacKey, iv);

        var plaintext = DeterministicBytes(payloadSize, 4);

        using var encrypted = new MemoryStream();
        using (var src = new MemoryStream(plaintext))
        {
            var (digest, encSize) = await EncryptAsync(src, encrypted, material, bufferSize: 4096);
            Assert.Equal(plaintext.Length, src.Position);
            Assert.Equal(encrypted.Length, encSize);
            Assert.Equal(SHA256.HashData(plaintext), digest);
        }

        encrypted.Position = 0;
        using var decrypted = new MemoryStream();
        await DecryptAsync(encrypted, decrypted, encKey, hmacKey, bufferSize: 4096);

        Assert.Equal(plaintext, decrypted.ToArray());
    }

    [Fact]
    public async Task Decrypt_throws_when_MAC_is_tampered()
    {
        var encKey = DeterministicBytes(32, 11);
        var hmacKey = DeterministicBytes(32, 12);
        var iv = DeterministicBytes(16, 13);
        var material = CreateMaterial(encKey, hmacKey, iv);
        var plaintext = DeterministicBytes(2048, 14);

        using var encrypted = new MemoryStream();
        using (var src = new MemoryStream(plaintext))
        {
            await EncryptAsync(src, encrypted, material, bufferSize: 4096);
        }

        // Flip a bit inside the MAC header (byte 0).
        var blob = encrypted.ToArray();
        blob[0] ^= 0x01;

        using var tampered = new MemoryStream(blob);
        using var sink = new MemoryStream();
        await Assert.ThrowsAsync<CryptographicException>(() => DecryptAsync(tampered, sink, encKey, hmacKey, bufferSize: 4096));
    }

    [Fact]
    public async Task Decrypt_throws_when_ciphertext_is_truncated()
    {
        var encKey = DeterministicBytes(32, 21);
        var hmacKey = DeterministicBytes(32, 22);
        var iv = DeterministicBytes(16, 23);
        var material = CreateMaterial(encKey, hmacKey, iv);
        var plaintext = DeterministicBytes(4096, 24);

        using var encrypted = new MemoryStream();
        using (var src = new MemoryStream(plaintext))
        {
            await EncryptAsync(src, encrypted, material, bufferSize: 4096);
        }

        // Drop the trailing 32 bytes — that breaks both the MAC and PKCS7 padding.
        var blob = encrypted.ToArray().Take(encrypted.ToArray().Length - 32).ToArray();

        using var truncated = new MemoryStream(blob);
        using var sink = new MemoryStream();
        await Assert.ThrowsAsync<CryptographicException>(() => DecryptAsync(truncated, sink, encKey, hmacKey, bufferSize: 4096));
    }

    [Fact]
    public async Task Encrypt_layout_is_MAC_then_IV_then_ciphertext()
    {
        var encKey = DeterministicBytes(32, 31);
        var hmacKey = DeterministicBytes(32, 32);
        var iv = DeterministicBytes(16, 33);
        var material = CreateMaterial(encKey, hmacKey, iv);
        var plaintext = DeterministicBytes(64, 34);

        using var encrypted = new MemoryStream();
        using (var src = new MemoryStream(plaintext))
        {
            await EncryptAsync(src, encrypted, material, bufferSize: 4096);
        }

        var blob = encrypted.ToArray();

        // MAC sits at offset 0 (32 bytes).
        var mac = blob.Take(32).ToArray();

        // IV at offset 32 (16 bytes) and matches the IV we passed in.
        var ivOnDisk = blob.Skip(32).Take(16).ToArray();
        Assert.Equal(iv, ivOnDisk);

        // The MAC must match HMACSHA256(MacKey, IV || ciphertext) — which is
        // what the Intune device-side decryption verifies. Recompute it
        // independently of the engine to lock in the wire format.
        var ciphertext = blob.Skip(32 + 16).ToArray();
        using var hmac = new HMACSHA256(hmacKey);
        hmac.TransformBlock(ivOnDisk, 0, ivOnDisk.Length, null, 0);
        hmac.TransformBlock(ciphertext, 0, ciphertext.Length, null, 0);
        hmac.TransformFinalBlock(Array.Empty<byte>(), 0, 0);
        Assert.Equal(hmac.Hash!, mac);
    }
}
