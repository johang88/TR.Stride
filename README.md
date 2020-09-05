# TR.Stride
Various stride utilities / projects (stride3d.net)

## TR.Stride.Atmosphere
Implementation of https://github.com/sebh/UnrealEngineSkyAtmosphere

### Usage
* Add `TR.Stride.Atmosphere` to your project
* Disable BindDepthAsResourceDuringTransparentRendering in Forward Render in graphics compositor
* Add AtmosphereRenderFeature to graphics compositor
* Create game object, add atmosphere component, link with directional light
* Done :)

### Issues
Quite a few ... 

* Fast atmospheric perspective is bugged and recommended to be turned off.
* Transparent objects not supported
* Requires BindDepthAsResourceDuringTransparentRendering to be disabled
* Some weird issues that requires skuview lut resolution to be hardcoded, meaning that it can't easily be changed
* AtmosphereRenderFeature needs a few rounds of refactoring
* Atmosphere can currently not be moved