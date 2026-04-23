using System;
using System.IO;
using System.Security.Cryptography;
using System.Threading;
using System.Threading.Tasks;

namespace IntuneTools.Graph.IntuneHelperClasses.Applications
{
    /// <summary>
    /// Implements the Intune mobile-app content encryption format used by
    /// every binary-upload app type (win32LobApp, macOSPkgApp, macOSDmgApp,
    /// iosLobApp, androidLobApp, etc.).
    ///
    /// On-disk layout produced by <see cref="EncryptStreamAsync"/>:
    /// <code>
    /// [ HMAC-SHA256(MacKey, IV || ciphertext) | 32 bytes ]
    /// [ IV                                    | 16 bytes ]
    /// [ AES-256-CBC ciphertext (PKCS7)        | n bytes  ]
    /// </code>
    /// Decryption (<see cref="DecryptStreamAsync"/>) reverses that layout
    /// after verifying the MAC.
    ///
    /// Both operations stream through fixed-size buffers so multi-GB DMG /
    /// .intunewin payloads never get loaded into RAM.
    ///
    /// All inputs and outputs are deterministic for a fixed
    /// <see cref="EncryptionMaterial"/>, which is how the unit tests
    /// guarantee a regression-free round-trip without needing a tenant.
    /// </summary>
    internal static class IntuneAppContentCrypto
    {
        public const int AesBlockSize = 16;          // bytes
        public const int AesKeySize = 32;            // 256 bits
        public const int HmacKeySize = 32;           // 256 bits
        public const int HmacSize = 32;              // SHA-256 output
        public const int IvSize = AesBlockSize;
        public const int HeaderSize = HmacSize + IvSize;

        /// <summary>
        /// Generates fresh AES-256 + HMAC-SHA256 keys + IV for one upload.
        /// </summary>
        public static EncryptionMaterial CreateMaterial()
        {
            var encryptionKey = RandomNumberGenerator.GetBytes(AesKeySize);
            var hmacKey = RandomNumberGenerator.GetBytes(HmacKeySize);
            var iv = RandomNumberGenerator.GetBytes(IvSize);
            return new EncryptionMaterial(encryptionKey, hmacKey, iv);
        }

        /// <summary>
        /// Encrypts <paramref name="source"/> into <paramref name="destination"/>
        /// using <paramref name="material"/>. Returns the SHA-256 of the
        /// plaintext (the <c>fileDigest</c> Intune expects in
        /// <c>FileEncryptionInfo</c>) and the encrypted size in bytes.
        ///
        /// The destination stream must be seekable: the engine writes the
        /// ciphertext + IV first, then rewinds and writes the HMAC into the
        /// reserved header. (Streaming HMAC over the IV + ciphertext while
        /// also producing the ciphertext only needs one pass.)
        /// </summary>
        public static async Task<EncryptionResult> EncryptStreamAsync(
            Stream source,
            Stream destination,
            EncryptionMaterial material,
            int bufferSize,
            CancellationToken cancellationToken)
        {
            if (source is null) throw new ArgumentNullException(nameof(source));
            if (destination is null) throw new ArgumentNullException(nameof(destination));
            if (material is null) throw new ArgumentNullException(nameof(material));
            if (!destination.CanSeek) throw new ArgumentException("Destination stream must be seekable.", nameof(destination));
            if (bufferSize <= 0) bufferSize = 256 * 1024;

            // Reserve header space; HMAC will be back-patched at the end.
            var headerStart = destination.Position;
            await destination.WriteAsync(new byte[HeaderSize].AsMemory(), cancellationToken).ConfigureAwait(false);

            using var aes = Aes.Create();
            aes.KeySize = AesKeySize * 8;
            aes.Mode = CipherMode.CBC;
            aes.Padding = PaddingMode.PKCS7;
            aes.Key = material.EncryptionKey;
            aes.IV = material.InitializationVector;

            using var hmac = new HMACSHA256(material.HmacKey);
            using var plaintextSha = SHA256.Create();

            // Feed the IV into the MAC first — Intune's MAC covers IV || ciphertext.
            hmac.TransformBlock(material.InitializationVector, 0, IvSize, null, 0);

            long encryptedBytes;
            using (var encryptor = aes.CreateEncryptor())
            using (var cryptoStream = new CryptoStream(
                       new MacAndCountingWriteStream(destination, hmac, leaveOpen: true),
                       encryptor,
                       CryptoStreamMode.Write,
                       leaveOpen: false))
            {
                var buffer = new byte[bufferSize];
                int read;
                while ((read = await source.ReadAsync(buffer.AsMemory(0, buffer.Length), cancellationToken).ConfigureAwait(false)) > 0)
                {
                    plaintextSha.TransformBlock(buffer, 0, read, null, 0);
                    await cryptoStream.WriteAsync(buffer.AsMemory(0, read), cancellationToken).ConfigureAwait(false);
                }
                await cryptoStream.FlushFinalBlockAsync(cancellationToken).ConfigureAwait(false);
                encryptedBytes = destination.Position - headerStart - HeaderSize;
            }

            plaintextSha.TransformFinalBlock(Array.Empty<byte>(), 0, 0);
            hmac.TransformFinalBlock(Array.Empty<byte>(), 0, 0);

            // Patch the header: [MAC][IV].
            var savedPosition = destination.Position;
            destination.Position = headerStart;
            await destination.WriteAsync(hmac.Hash.AsMemory(0, HmacSize), cancellationToken).ConfigureAwait(false);
            await destination.WriteAsync(material.InitializationVector.AsMemory(0, IvSize), cancellationToken).ConfigureAwait(false);
            destination.Position = savedPosition;
            await destination.FlushAsync(cancellationToken).ConfigureAwait(false);

            return new EncryptionResult(
                FileDigest: plaintextSha.Hash!,
                EncryptedSize: encryptedBytes + HeaderSize);
        }

        /// <summary>
        /// Decrypts a payload produced by <see cref="EncryptStreamAsync"/>
        /// (or by Intune itself) into <paramref name="destination"/>. Throws
        /// <see cref="CryptographicException"/> if the MAC does not match the
        /// payload, which prevents tampered or truncated downloads from
        /// being silently re-uploaded.
        /// </summary>
        public static async Task DecryptStreamAsync(
            Stream source,
            Stream destination,
            byte[] encryptionKey,
            byte[] hmacKey,
            int bufferSize,
            CancellationToken cancellationToken)
        {
            if (source is null) throw new ArgumentNullException(nameof(source));
            if (destination is null) throw new ArgumentNullException(nameof(destination));
            if (encryptionKey is null || encryptionKey.Length != AesKeySize) throw new ArgumentException("Encryption key must be 32 bytes.", nameof(encryptionKey));
            if (hmacKey is null || hmacKey.Length != HmacKeySize) throw new ArgumentException("HMAC key must be 32 bytes.", nameof(hmacKey));
            if (bufferSize <= 0) bufferSize = 256 * 1024;

            var header = new byte[HeaderSize];
            await ReadExactAsync(source, header, cancellationToken).ConfigureAwait(false);

            var expectedMac = new byte[HmacSize];
            Buffer.BlockCopy(header, 0, expectedMac, 0, HmacSize);

            var iv = new byte[IvSize];
            Buffer.BlockCopy(header, HmacSize, iv, 0, IvSize);

            // Pass 1: validate MAC over IV || ciphertext. We must do this
            // before writing any plaintext to the destination so a tampered
            // download is never observable downstream.
            using (var hmac = new HMACSHA256(hmacKey))
            {
                hmac.TransformBlock(iv, 0, IvSize, null, 0);
                var startOfCiphertext = source.Position;
                var buf = new byte[bufferSize];
                int n;
                while ((n = await source.ReadAsync(buf.AsMemory(0, buf.Length), cancellationToken).ConfigureAwait(false)) > 0)
                {
                    hmac.TransformBlock(buf, 0, n, null, 0);
                }
                hmac.TransformFinalBlock(Array.Empty<byte>(), 0, 0);
                if (!CryptographicOperations.FixedTimeEquals(hmac.Hash!, expectedMac))
                {
                    throw new CryptographicException("HMAC validation failed for Intune mobile app content.");
                }
                source.Position = startOfCiphertext;
            }

            // Pass 2: decrypt.
            using var aes = Aes.Create();
            aes.KeySize = AesKeySize * 8;
            aes.Mode = CipherMode.CBC;
            aes.Padding = PaddingMode.PKCS7;
            aes.Key = encryptionKey;
            aes.IV = iv;

            using var decryptor = aes.CreateDecryptor();
            using var cryptoStream = new CryptoStream(source, decryptor, CryptoStreamMode.Read, leaveOpen: true);
            var transferBuffer = new byte[bufferSize];
            int read;
            while ((read = await cryptoStream.ReadAsync(transferBuffer.AsMemory(0, transferBuffer.Length), cancellationToken).ConfigureAwait(false)) > 0)
            {
                await destination.WriteAsync(transferBuffer.AsMemory(0, read), cancellationToken).ConfigureAwait(false);
            }
            await destination.FlushAsync(cancellationToken).ConfigureAwait(false);
        }

        private static async Task ReadExactAsync(Stream source, byte[] buffer, CancellationToken cancellationToken)
        {
            int read = 0;
            while (read < buffer.Length)
            {
                var n = await source.ReadAsync(buffer.AsMemory(read), cancellationToken).ConfigureAwait(false);
                if (n == 0) throw new EndOfStreamException("Encrypted Intune payload ended before the header was complete.");
                read += n;
            }
        }

        /// <summary>
        /// Write-only stream wrapper that forwards bytes to an inner stream
        /// while feeding them into a running HMAC. Used so encryption can
        /// build the ciphertext and the MAC in a single pass.
        /// </summary>
        private sealed class MacAndCountingWriteStream : Stream
        {
            private readonly Stream _inner;
            private readonly HMAC _hmac;
            private readonly bool _leaveOpen;

            public MacAndCountingWriteStream(Stream inner, HMAC hmac, bool leaveOpen)
            {
                _inner = inner;
                _hmac = hmac;
                _leaveOpen = leaveOpen;
            }

            public override bool CanRead => false;
            public override bool CanSeek => false;
            public override bool CanWrite => true;
            public override long Length => throw new NotSupportedException();
            public override long Position { get => throw new NotSupportedException(); set => throw new NotSupportedException(); }
            public override void Flush() => _inner.Flush();
            public override int Read(byte[] buffer, int offset, int count) => throw new NotSupportedException();
            public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
            public override void SetLength(long value) => throw new NotSupportedException();

            public override void Write(byte[] buffer, int offset, int count)
            {
                _hmac.TransformBlock(buffer, offset, count, null, 0);
                _inner.Write(buffer, offset, count);
            }

            public override async ValueTask WriteAsync(ReadOnlyMemory<byte> buffer, CancellationToken cancellationToken = default)
            {
                if (System.Runtime.InteropServices.MemoryMarshal.TryGetArray(buffer, out var seg) && seg.Array != null)
                {
                    _hmac.TransformBlock(seg.Array, seg.Offset, seg.Count, null, 0);
                }
                else
                {
                    var tmp = buffer.ToArray();
                    _hmac.TransformBlock(tmp, 0, tmp.Length, null, 0);
                }
                await _inner.WriteAsync(buffer, cancellationToken).ConfigureAwait(false);
            }

            protected override void Dispose(bool disposing)
            {
                if (disposing && !_leaveOpen) _inner.Dispose();
                base.Dispose(disposing);
            }
        }
    }

    /// <summary>Keys + IV used for one Intune content upload.</summary>
    internal sealed record EncryptionMaterial(byte[] EncryptionKey, byte[] HmacKey, byte[] InitializationVector);

    /// <summary>Outputs from <see cref="IntuneAppContentCrypto.EncryptStreamAsync"/>.</summary>
    internal sealed record EncryptionResult(byte[] FileDigest, long EncryptedSize);
}
