using Stride.Core;
using Stride.Core.Mathematics;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TR.Stride.Terrain.Data;
[DataContract(nameof(NoiseTerrainSettings))]
public class NoiseTerrainSettings
{
	public Vector2 HeightRange
	{
		get => _heightmap;
		set
		{
			_heightmap = value;
			NoiseSettings.DataChanged = true;
		}
	}
	private Vector2 _heightmap= new(0, 5);

	public Int2 Size
	{
		get => _size;
		set
		{
			_size = value;
			NoiseSettings.DataChanged = true;
		}
	}
	private Int2 _size = new(512, 512);

	public FastNoiseSettings NoiseSettings { get; set; } = new();

	public float GetHeight(float x, float z)
	{
		var height = NoiseSettings.FastNoise.GetNoise(x, z);
		height = MathUtil.Lerp(HeightRange.X, HeightRange.Y, height);
		return height *= HeightRange.Y;
	}


	public Vector3 GetNormal(int x, int y)
	{
		var heightL = GetHeight(x - 1, y);
		var heightR = GetHeight(x + 1, y);
		var heightD = GetHeight(x, y - 1);
		var heightU = GetHeight(x, y + 1);

		var normal = new Vector3(heightL - heightR, 2.0f, heightD - heightU);
		normal.Normalize();

		return normal;
	}

	public Vector3 GetTangent(int x, int z)
	{
		var flip = 1;
		var here = new Vector3(x, GetHeight(x, z), z);
		var left = new Vector3(x - 1, GetHeight(x - 1, z), z);
		if (left.X < 0.0f)
		{
			flip *= -1;
			left = new Vector3(x + 1, GetHeight(x + 1, z), z);
		}

		left -= here;

		var tangent = left * flip;
		tangent.Normalize();

		return tangent;
	}
}
