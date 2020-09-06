# TR.Stride
Various stride utilities / projects (stride3d.net)

## TR.Stride.Atmosphere
Implementation of https://github.com/sebh/UnrealEngineSkyAtmosphere

![Atmosphere Screenshot](TR.Stride.Atmosphere/Screenshot.jpg?raw=true "Atmosphere Screenshot")

### Usage
* Add `TR.Stride.Atmosphere` to your project
* Add AtmosphereRenderFeature to graphics compositor
* Add SimpleGroupToRenderStageSelector to the newly added render feature, set effect name to AtmosphereRenderSkyRayMarchingEffect
* Make sure "Bind Depth As Resource During Transparent Rendering" is checked on the Forward renderer node (this is the default setting)
* Create game object, add atmosphere component, link with directional light
* Done :)

### Issues
* Transparent objects not supported
* Some weird issues that requires sky view lut resolution to be hardcoded, meaning that it can't easily be changed
* Atmosphere can not be moved
* Diretional light might be incorrect in editor after a hot reload, this is due to an issue in stride where component references are not correctly restored