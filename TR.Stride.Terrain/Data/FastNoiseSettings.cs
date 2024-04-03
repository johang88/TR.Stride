using Stride.Core;
using static TR.Stride.FastNoiseLite;

namespace TR.Stride.Terrain.Data;
[DataContract(nameof(FastNoiseSettings))]
public class FastNoiseSettings
{
	[DataMemberIgnore]
	public bool DataChanged { get; set; }

	#region General

	public NoiseType Type
	{
		get
		{
			return _type;
		}
		set
		{
			_type = value;
			FastNoise.SetNoiseType(value);
			DataChanged = true;
		}
	}
	private NoiseType _type = NoiseType.Perlin;

	public RotationType3D RotationType3D
	{
		get
		{
			return _rotationType3D;
		}
		set
		{
			_rotationType3D = value;
			FastNoise.SetRotationType3D(value);
			DataChanged = true;
		}
	}
	private RotationType3D _rotationType3D = RotationType3D.None;

	public float Frequency
	{
		get
		{
			return _frequency;
		}
		set
		{
			_frequency = value;
			FastNoise.SetFrequency(value);
			DataChanged = true;
		}
	}
	private float _frequency = 0.01f;

	public int Seed 
	{
		get
		{
			return _seed;
		} 
		set
		{
			_seed = value;
			FastNoise.SetSeed(value);
			DataChanged = true;
		}
	}
	private int _seed = 1337;

	#endregion

	#region Fractal

	public FractalType FractalType
	{
		get
		{
			return _fractalType;
		}
		set
		{
			_fractalType = value;
			FastNoise.SetFractalType(value);
			DataChanged = true;
		}
	}
	private FractalType _fractalType = FastNoiseLite.FractalType.None;

	public float FractalGain
	{
		get
		{
			return _fractalGain;
		}
		set
		{
			_fractalGain = value;
			FastNoise.SetFractalGain(value);
			DataChanged = true;
		}
	}
	private float _fractalGain = 0.5f;

	public int FractalLacunarity
	{
		get
		{
			return _FractalLacunarity;
		}
		set
		{
			_FractalLacunarity = value;
			FastNoise.SetFractalLacunarity(value);
			DataChanged = true;
		}
	}
	private int _FractalLacunarity = 2;

	public int FractalOctaves
	{
		get
		{
			return _fractalOctaves;
		}
		set
		{
			_fractalOctaves = value;
			FastNoise.SetFractalOctaves(value);
			DataChanged = true;
		}
	}
	private int _fractalOctaves = 3;

	public float FractalWeightedStrength
	{
		get
		{
			return _fractalWeightedStrength;
		}
		set
		{
			_fractalWeightedStrength = value;
			FastNoise.SetFractalWeightedStrength(value);
			DataChanged = true;
		}
	}
	private float _fractalWeightedStrength = 0.5f;

	public float FractalPingPongStrength
	{
		get
		{
			return _fractalPingPongStrength;
		}
		set
		{
			_fractalPingPongStrength = value;
			FastNoise.SetFractalPingPongStrength(value);
			DataChanged = true;
		}
	}
	private float _fractalPingPongStrength = 2.0f;

	#endregion

	#region Cellular

	public CellularDistanceFunction CellularDistance
	{
		get
		{
			return _cellularDistance;
		}
		set
		{
			_cellularDistance = value;
			FastNoise.SetCellularDistanceFunction(value);
			DataChanged = true;
		}
	}
	private CellularDistanceFunction _cellularDistance = CellularDistanceFunction.EuclideanSq;

	public CellularReturnType CellularReturnType
	{
		get
		{
			return _cellularReturnType;
		}
		set
		{
			_cellularReturnType = value;
			FastNoise.SetCellularReturnType(value);
			DataChanged = true;
		}
	}
	private CellularReturnType _cellularReturnType = CellularReturnType.Distance;

	public float CellularJitter
	{
		get
		{
			return _cellularJitter;
		}
		set
		{
			_cellularJitter = value;
			FastNoise.SetCellularJitter(value);
			DataChanged = true;
		}
	}
	private float _cellularJitter = 1;

	#endregion

	[DataMemberIgnore]
	public readonly FastNoiseLite FastNoise = new();
}
