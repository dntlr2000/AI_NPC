using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading;

namespace AiCharacterKit.Unity.LocalInference
{
    /// <summary>Uses Windows CNG SHA-256 so multi-gigabyte verification does not depend on Mono's slow managed digest.</summary>
    internal static class WindowsFileDigest
    {
        /// <summary>Hashes an already opened file incrementally with cancellation and releases every native handle.</summary>
        public static byte[] Compute(Stream stream, CancellationToken cancellationToken)
        {
            IntPtr algorithm = IntPtr.Zero, hash = IntPtr.Zero;
            try
            {
                Check(BCryptOpenAlgorithmProvider(out algorithm, "SHA256", null, 0));
                Check(BCryptCreateHash(algorithm, out hash, IntPtr.Zero, 0, IntPtr.Zero, 0, 0));
                var buffer = new byte[128 * 1024];
                int count;
                while ((count = stream.Read(buffer, 0, buffer.Length)) > 0)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    Check(BCryptHashData(hash, buffer, count, 0));
                }
                var digest = new byte[32];
                Check(BCryptFinishHash(hash, digest, digest.Length, 0));
                return digest;
            }
            finally
            {
                if (hash != IntPtr.Zero) BCryptDestroyHash(hash);
                if (algorithm != IntPtr.Zero) BCryptCloseAlgorithmProvider(algorithm, 0);
            }
        }
        /// <summary>Converts native status codes into a content-free verification failure.</summary>
        private static void Check(int status) { if (status != 0) throw new IOException("File digest could not be computed."); }
        /// <summary>Opens the system SHA-256 provider.</summary>
        [DllImport("bcrypt.dll", CharSet = CharSet.Unicode)]
        private static extern int BCryptOpenAlgorithmProvider(out IntPtr algorithm, string id, string implementation, int flags);
        /// <summary>Creates an incremental digest with an operating-system-owned object buffer.</summary>
        [DllImport("bcrypt.dll")]
        private static extern int BCryptCreateHash(IntPtr algorithm, out IntPtr hash, IntPtr buffer, int size, IntPtr secret, int secretSize, int flags);
        /// <summary>Adds one bounded file chunk to the digest.</summary>
        [DllImport("bcrypt.dll")]
        private static extern int BCryptHashData(IntPtr hash, byte[] data, int size, int flags);
        /// <summary>Retrieves the final fixed-length digest.</summary>
        [DllImport("bcrypt.dll")]
        private static extern int BCryptFinishHash(IntPtr hash, byte[] digest, int size, int flags);
        /// <summary>Releases one incremental digest object.</summary>
        [DllImport("bcrypt.dll")]
        private static extern int BCryptDestroyHash(IntPtr hash);
        /// <summary>Releases the system algorithm provider handle.</summary>
        [DllImport("bcrypt.dll")]
        private static extern int BCryptCloseAlgorithmProvider(IntPtr algorithm, int flags);
    }
}
