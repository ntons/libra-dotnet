using System;
using ZstdNet;

namespace Patch
{
    public class Patch
    {
        public static byte[] Diff(byte[] source, byte[] target)
        {
            var options = new CompressionOptions(source);
            var compressor = new Compressor(options);
            return compressor.Wrap(target);
        }
        public static byte[] Merge(byte[] source, byte[] patch)
        {
            var options = new DecompressionOptions(source);
            var decompressor = new Decompressor(options);
            return decompressor.Unwrap(patch);
        }
    }
}
