# TR.Stride
Various stride utilities / projects (stride3d.net)

## TR.Stride.Atmosphere
Implementation of https://github.com/sebh/UnrealEngineSkyAtmosphere

![Atmosphere Screenshot](TR.Stride.Atmosphere/Screenshot.jpg?raw=true "Atmosphere Screenshot")

### Usage
* Add `TR.Stride.Atmosphere` to your project
* Add AtmosphereRenderFeature to graphics compositor
* Add SimpleGroupToRenderStageSelector to the newly added render feature, set effect name to AtmosphereRenderSkyRayMarchingEffect
* In MeshRenderFeature add a new sub render feature `AtmosphereTransparentRenderFeature
* MeshRenderFeature render stage selector change effect name in Mesh Transaprent render stage selector from ForwardShadingEffect to AtmosphereForwardShadingEffect
* Change LightDirectionalGroupRenderer to AtmosphereLightDirectionalGroupRenderer in MeshRenderFeature -> ForwardLightingRenderFeeature
* Make sure "Bind Depth As Resource During Transparent Rendering" is checked on the Forward renderer node (this is the default setting)
* Create game object, add light component with type = Sun, this is basically just a regular directional light.
* Create game object, add atmosphere component, link with sun light
* Done :)

### Issues
* Atmosphere can not be moved
* Diretional light might be incorrect in editor after a hot reload, this is due to an issue in stride where component references are not correctly restored

## TR.Stride.Ocean
Implementation of https://github.com/gasgiant/FFT-Ocean/

### Usage
* Add `TR.Stride.Ocean` to your project
* Create an empty entity
* Add OceanComponent to the entity

### Issues
* Currently requires that you have at least one other model in your scene, it will crash due to a missing model processor otherwise.