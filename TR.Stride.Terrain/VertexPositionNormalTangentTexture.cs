using Stride.Core.Mathematics;
using Stride.Graphics;
using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Text;

namespace TR.Stride.Terrain
{
    /// <summary>
    /// Custom vertex type so that we can generate tangents for supporting normal maps
    /// </summary>
    [StructLayout(LayoutKind.Sequential, Pack = 4)]
    public struct VertexPositionNormalTangentTexture : IEquatable<VertexPositionNormalTangentTexture>, IVertex
    {
        public VertexPositionNormalTangentTexture(Vector3 position, Vector3 normal, Vector3 tangent, Vector2 textureCoordinate) : this()
        {
            Position = position;
            Normal = normal;
            Tangent = tangent;
            TextureCoordinate = textureCoordinate;
        }

        public Vector3 Position;
        public Vector3 Normal;
        public Vector3 Tangent;
        public Vector2 TextureCoordinate;

        public static readonly int Size = 44;

        public static readonly VertexDeclaration Layout = new VertexDeclaration(
           VertexElement.Position<Vector3>(),
           VertexElement.Normal<Vector3>(),
           VertexElement.Tangent<Vector3>(),
           VertexElement.TextureCoordinate<Vector2>());

        public bool Equals(VertexPositionNormalTangentTexture other)
            => Position.Equals(other.Position) && Normal.Equals(other.Normal) && Tangent.Equals(other.Tangent) && TextureCoordinate.Equals(other.TextureCoordinate);

        public override bool Equals(object obj)
        {
            if (ReferenceEquals(null, obj)) return false;
            return obj is VertexPositionNormalTangentTexture && Equals((VertexPositionNormalTangentTexture)obj);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                int hashCode = Position.GetHashCode();
                hashCode = (hashCode * 397) ^ Normal.GetHashCode();
                hashCode = (hashCode * 397) ^ TextureCoordinate.GetHashCode();
                return hashCode;
            }
        }

        public VertexDeclaration GetLayout()
            => Layout;

        public void FlipWinding()
            => TextureCoordinate.X = (1.0f - TextureCoordinate.X);

        public static bool operator ==(VertexPositionNormalTangentTexture left, VertexPositionNormalTangentTexture right)
            => left.Equals(right);

        public static bool operator !=(VertexPositionNormalTangentTexture left, VertexPositionNormalTangentTexture right)
            => !left.Equals(right);

        public override string ToString()
            => string.Format("Position: {0}, Normal: {1}, Tangent {2}, Texcoord: {3}", Position, Normal, Tangent, TextureCoordinate);
    }
}
