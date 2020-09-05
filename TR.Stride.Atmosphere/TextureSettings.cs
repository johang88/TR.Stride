using Stride.Graphics;
using Stride.Core;

namespace TR.Stride.Atmosphere
{
    [DataContract]
    public struct TextureSettings2d
    {
        [DataMember] public int Width;
        [DataMember] public int Height;
        [DataMember] public PixelFormat Format;

        public TextureSettings2d(int width, int height, PixelFormat format)
        {
            Width = width;
            Height = height;
            Format = format;
        }
    }

    [DataContract]
    public struct TextureSettingsSquare
    {
        [DataMember] public int Size;
        [DataMember] public PixelFormat Format;

        public TextureSettingsSquare(int size, PixelFormat format)
        {
            Size = size;
            Format = format;
        }
    }

    [DataContract]
    public struct TextureSettingsVolume
    {
        [DataMember] public int Size { get; set; }
        [DataMember] public int Slices { get; set; }
        [DataMember] public PixelFormat Format { get; set; }

        public TextureSettingsVolume(int size, int slices, PixelFormat format)
        {
            Size = size;
            Slices = slices;
            Format = format;
        }
    }
}
