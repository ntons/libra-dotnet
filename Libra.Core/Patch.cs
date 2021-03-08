using ZstdNet;

namespace Libra.Core
{
    // Patch.From(source, target) => patch
    // Patch.To(source, patch) => target
    class Patch
    {
        public static byte[] From(byte[] source, byte[] target)
        {
            var options = new CompressionOptions(source);
            var compressor = new Compressor(options);
            return compressor.Wrap(target);
        }
        public static byte[] To(byte[] source, byte[] patch)
        {
            var options = new DecompressionOptions(source);
            var decompressor = new Decompressor(options);
            return decompressor.Unwrap(patch);
        }
    }
}
