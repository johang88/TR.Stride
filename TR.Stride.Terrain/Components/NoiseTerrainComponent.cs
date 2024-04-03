using Stride.Engine.Design;
using Stride.Engine;
using TR.Stride.Terrain.Data;
using TR.Stride.Terrain.Processors;
using Stride.Core;
using Stride.Rendering;

namespace TR.Stride.Terrain.Components;

[DataContract(nameof(NoiseTerrainComponent))]
[DefaultEntityComponentRenderer(typeof(NoiseTerrainProcessor))]
[ComponentCategory("Terrain")]
public class NoiseTerrainComponent : EntityComponent
{
	[DataMember(0)]
	public Material Material { get; set; }

	[DataMember(10)]
	public NoiseTerrainSettings NoiseData { get; set; } = new();

	[DataMember(20)]
	public float Size { get; set; } = 512;

	[DataMember(30)]
	public bool CastShadows { get; set; }
}
