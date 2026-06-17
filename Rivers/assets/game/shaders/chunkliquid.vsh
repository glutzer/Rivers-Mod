#version 330 core
// code will change the version to 430 if USESSBO > 0
#extension GL_ARB_explicit_attrib_location: enable

layout(location = 0) in vec3 xyz;
layout(location = 1) in vec2 uvIn;
layout(location = 2) in vec4 rgbaLightIn;
layout(location = 3) in int renderFlags;   // Check out chunkvertexflags.ash for understanding the contents of this data
layout(location = 4) in vec2 flowVector;
layout(location = 5) in int colormapData;

// Bit 0: Should animate yes/no
// Bit 1: Should texture fade yes/no
// Bit 2-9: Oceanity
// Bits 10-17: x-Distance to upper left corner, where 255 = size of the block texture
// Bits 18-26: y-Distance to upper left corner, where 255 = size of the block texture
// Bit 27: Lava yes/no - use LiquidIsLavaBitPosition

// Bit 28: Weak foamy yes/no - use LiquidWeakFoamBitPosition
// Bit 29: Weak Wavy yes/no - use LiquidWeakWaveBitPosition
// Bit 30: Don't tweak alpha channel - use LiquidFullAlphaBitPosition
// Bit 31: LiquidExposedToSky - use LiquidSkyExposedBitPosition

layout(location = 6) in int waterFlagsIn;

uniform float waterStillCounter;

uniform vec4 rgbaFogIn;
uniform vec3 rgbaAmbientIn;
uniform float fogDensityIn;
uniform float fogMinIn;
uniform vec3 origin;
uniform mat4 projectionMatrix;
uniform mat4 modelViewMatrix;
uniform vec2 blockTextureSize;

uniform vec3 playerViewVec;
uniform vec3 sunPosRel;
uniform vec3 playerPosForFoam;
uniform float subpixelPaddingX;
uniform float subpixelPaddingY;

out vec2 flowVectorf;
out vec4 rgba;
out vec4 rgbaFog;
out float fogAmount;
out vec2 uv;
out vec2 uvSize;
out float waterStillCounterOff;
out vec3 fragWorldPos;
out vec3 fragNormal;
out vec3 fWorldPos;
out float fresnel;
flat out int skyExposed;

flat out vec2 uvBase;
flat out int waterFlags;

#include vertexflagbits.ash
#include shadowcoords.vsh
#include fogandlight.vsh
#include vertexwarp.vsh
#include colormap.vsh


void main(void)
{
	vec4 truePos = vec4(xyz + origin, 1.0);
	vec4 worldPos = truePos;
	
	if ((waterFlagsIn & 1) == 1) {
		float div = ((waterFlagsIn & LiquidWeakWaveBitMask) > 0) ? 90 : 5;
	
		float oceanity = ((waterFlagsIn >> 2) & 0xff) * OneOver255;
		div *= max(0.2, 1 - oceanity);
		
		worldPos = applyLiquidWarping((waterFlagsIn & LiquidIsLavaBitMask) == 0, worldPos, div);
	}
	else if ((waterFlagsIn & LiquidWeakWaveBitMask) > 0) {
		worldPos = applyLiquidWarping((waterFlagsIn & LiquidIsLavaBitMask) == 0, worldPos, 90);
	}
	
	vec4 cameraPos = modelViewMatrix * worldPos;
	
	gl_Position = projectionMatrix * cameraPos;
	
	float x = mod(waterStillCounter + length(worldPos.xz + playerpos.xz) * 0.3333333, 2);

	waterStillCounterOff = smoothstep(0, 1, abs(x - 1));
	if ((waterFlagsIn & 2) == 0) {
		waterStillCounterOff = 1;
	}
	
	
	fragWorldPos = worldPos.xyz + playerPosForFoam;
	fWorldPos = worldPos.xyz;
	
	rgba = applyLight(rgbaAmbientIn, rgbaLightIn, renderFlags, cameraPos);
	rgbaFog = rgbaFogIn;
	fogAmount = getFogLevel(worldPos, fogMinIn, fogDensityIn);
	
        uv = uvIn;
	
	uvSize = vec2((waterFlagsIn >> 10) & 0xff, (waterFlagsIn >> 18) & 0xff) * OneOver255 * blockTextureSize;
	uvBase = uv - uvSize;
	
	flowVectorf = flowVector;
	
	waterFlags = waterFlagsIn;
	fragNormal = unpackNormal(renderFlags);
	skyExposed = (renderFlags >> LiquidSkyExposedBitPosition) & 1;
	
	
	 
	vec3 eyeFresnel = normalize(vec3(worldPos.x, worldPos.y - 3.5, worldPos.z));
	float bias = 0.01;
	float scale = 4.5;
	float power = 3.0;
	
	if ((renderFlags & GlowLevelBitMask) == 0) { // Don't apply to glowing liquids for now, looks weird on lava (makes it less glowy)
		fresnel = max(0.2, bias + scale * pow(1 + dot(eyeFresnel, fragNormal), power));
		
		fresnel = min(fresnel, clamp(20 * (1.05 - length(worldPos.xz) / viewDistance) - 5 + max(0.0, worldPos.y * 0.02), -1.0, 1.5));
		
		rgba.a = clamp(0.8*fresnel, 0, 2); 
		
		if (fragNormal.y < 0.5) {
			rgba.a *= 0.3333333;
		}
	}
	

	calcShadowMapCoords(modelViewMatrix, worldPos);
	calcColorMapUvs(colormapData, truePos + vec4(playerpos, 1), rgbaLightIn.a, false);
	
	// We pretend the decal is closer to the camera to enforce it always being drawn on top
	// Required e.g. when water is besides stairs or slabs
	gl_Position.w += 0.0008 / max(0.1, gl_Position.z);
}