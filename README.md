# Unity-GeoAO
Fast ambient occlusion in Unity baked at runtime.

### For Built-in RP checkout the master branch
----

![alt tag](http://i.imgur.com/c1JhyMj.png)

## Explanations

Unity does not allow to compute ambient occlusion on generated mesh at runtime and SSAO does not always provide the quality we deserve.

This small project shows how to quickly compute AO for each vertex in Unity. For N samples positions of camera around the mesh, a depth map is generated by Unity and vertex positions are sent to a shader that reads the depth map and tests if vertices are visible or not. The output colors for each vertex are blended with the result of all the samples.

Another shader is set to the meshes that reads the value for each vertex and darkens it accordingly.

This is an implementation of this technique in Unity : https://github.com/wwwtyro/geo-ambient-occlusion

To compute AO, objects layers are set to __AOLayer__ during computation and restored to the previous layer after the work is done.

Computing AO for a 500k vertices Stanford dragon with 256 samples is done 0.15s on my computer (770 GTX + i7 4790k).

## System

Tested on Windows DX11, DX12 and OpenGL, macOS with OpenGL and Metal, Linux with OpenGL

## Contribute

Pull requests are more than welcome!

## License


[![License: MIT](https://img.shields.io/badge/License-MIT-yellow.svg)](https://opensource.org/licenses/MIT)
