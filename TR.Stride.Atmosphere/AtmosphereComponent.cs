using Stride.Core;
using Stride.Core.Annotations;
using Stride.Core.Mathematics;
using Stride.Engine;
using Stride.Engine.Design;
using Stride.Input;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Text;

namespace TR.Stride.Atmosphere
{
    [DataContract, DefaultEntityComponentRenderer(typeof(AtmosphereEntityProcessor))]
    public class AtmosphereComponent : ScriptComponent
    {
        [DataMember(0)] public LightComponent Sun { get; set; }
        [DataMember(1)] public LightComponent Sky { get; set; }

        [DataMember(10)] public float PlanetRadius { get; set; } = 6360.0f;
        [DataMember(11)] public float AtmosphereHeight { get; set; } = 100.0f;

        [DataMember(21)] public float MiePhase { get; set; } = 0.8f;
        [DataMember(21)] public float MieScatteringScale { get; set; } = 0.00692f;
        [DataMember(22)] public Color3 MieScatteringCoefficient { get; set; } = new Color3(147 / 255.0f, 147 / 255.0f, 147 / 255.0f);

        [DataMember(30)] public float MieAbsorptionScale { get; set; } = 0.00077f;
        [DataMember(31)] public Color3 MieAbsorptionCoefficient { get; set; } = new Color3(147 / 255.0f, 147 / 255.0f, 147 / 255.0f);

        [DataMember(40)] public float RayleighScatteringScale { get; set; } = 0.03624f;
        [DataMember(41)] public Color3 RayleighScatteringCoefficient { get; set; } = new Color3(41 / 255.0f, 95 / 255.0f, 230 / 255.0f);

        [DataMember(50)] public float AbsorptionExctinctionScale { get; set; } = 0.00199f;
        [DataMember(51)] public Color3 AbsorptionExctinctionCoefficient { get; set; } = new Color3(83 / 255.0f, 241 / 255.0f, 11 / 255.0f);

        [DataMember(52)] public float AbsorptionDensity0LayerWidth { get; set; } = 25.0f;
        [DataMember(53)] public float AbsorptionDensity0ConstantTerm { get; set; } = -2.0f / 3.0f;
        [DataMember(54)] public float AbsorptionDensity0LinearTerm { get; set; } = 1.0f / 15.0f;
        [DataMember(55)] public float AbsorptionDensity1ConstantTerm { get; set; } = 8.0f / 3.0f;
        [DataMember(56)] public float AbsorptionDensity1LinearTerm { get; set; } = -1.0f / 15.0f;

        [DataMember(60)] public float RayleighScaleHeight { get; set; } = 8.0f;
        [DataMember(61)] public float MieScaleHeight { get; set; } = 1.2f;

        [DataMember(70)] public Color3 GroundAlbedo { get; set; } = new Color3(0, 0, 0);

        [DataMember(80)] public float SunLuminanceFactor { get; set; } = 100.0f;
        [DataMember(81)] public bool RenderSunDisk { get; set; } = true;

        [DataMember(200)] public float AerialPerspectiveDistanceScale { get; set; } = 1.0f;
        /// <summary>
        /// 1 stride unit = 1m
        /// 1 atmosphere unit = 1km
        /// </summary>
        [DataMember(201)] public float StrideToAtmosphereUnitScale { get; set; } = 1.0f / 1000.0f;

        [DataMember(202)] public float MultipleScatteringFactor { get; set; } = 1.0f;

        /// <summary>
        /// Adjust this depending on the scale of your scene,
        /// if you have a small aerial perspective scale then you probably want lower values
        /// or it wont get captured very well close to the camera, large scales work the same but in reverse :)
        /// </summary>
        [DataMember(203)] public float AtmosphereScatteringVolumeKmPerSlice { get; set; } = 4.0f;

        [DataMember(300)] public bool EnableClouds { get; set; } = true;
        [DataMember(301)] public float CloudScale { get; set; } = 0.0001f;
        [DataMember(302)] public float CloudSpeed { get; set; } = 0.01f;
        [DataMember(303)] public float Cloudiness { get; set; } = 0.8f;
    }
}
