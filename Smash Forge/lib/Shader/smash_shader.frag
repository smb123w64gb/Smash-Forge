#version 330

// A struct is used for what would normally be attributes from the vert/geom shader.
struct VertexAttributes
{
    vec3 viewPosition;
    vec3 objectPosition;
    vec2 texCoord;
    vec2 texCoord2;
    vec2 texCoord3;
    vec2 normaltexCoord;
    vec4 vertexColor;
    vec3 normal;
    vec3 viewNormal;
    vec3 tangent;
    vec3 bitangent;
};

// Textures
uniform sampler2D dif;
uniform sampler2D dif2;
uniform sampler2D dif3;
uniform sampler2D ramp;
uniform sampler2D dummyRamp;
uniform sampler2D normalMap;
uniform sampler2D ao;
uniform samplerCube cube;
uniform samplerCube stagecube;
uniform sampler2D spheremap;
uniform samplerCube cmap;
uniform sampler2D UVTestPattern;

// flags tests
uniform int hasDif;
uniform int hasDif2;
uniform int hasDif3;
uniform int hasStage;
uniform int hasCube;
uniform int hasNrm;
uniform int hasRamp;
uniform int hasAo;
uniform int hasSphereMap;
uniform int hasDummyRamp;

// flags tests
uniform int hasColorGainOffset;
uniform int hasSpecularParams;
uniform int useDiffuseBlend;
uniform int hasDualNormal;
uniform int hasSoftLight;
uniform int hasCustomSoftLight;
uniform int hasFinalColorGain;
uniform int useDifRefMask;
uniform int hasBayoHair;
uniform int softLightBrighten;
uniform int hasUniverseParam;

// Da Flags
uniform uint flags;

// Check if src, dst, alpha function are non zero.
uniform int isTransparent;

// Render Settings
uniform int renderDiffuse;
uniform int renderSpecular;
uniform int renderFresnel;
uniform int renderReflection;
uniform int renderType;
uniform int renderLighting;
uniform int renderVertColor;
uniform int renderNormal;
uniform int useNormalMap;
// Wireframe Rendering
uniform int drawWireframe;
uniform float lineWidth;

uniform int drawSelection;

// Channel Toggles
uniform int renderAlpha;

// Render Pass Intensities
uniform float diffuseIntensity;
uniform float ambientIntensity;
uniform float specularIntensity;
uniform float fresnelIntensity;
uniform float reflectionIntensity;

// Not found in game yet.
uniform vec3 refLightColor;

// Matrices
uniform mat4 mvpMatrix;
uniform mat4 modelViewMatrix;
uniform mat4 sphereMapMatrix;
uniform mat4 rotationMatrix;

// Misc Mesh Attributes
uniform float zBufferOffset;

uniform vec3 cameraPosition;

uniform float bloomThreshold;

uniform sampler2D shadowMap;

// Stage Lighting
uniform int lightSet;
uniform int isStage;
uniform int renderStageLighting;

// Stage Light 1
uniform int renderStageLight1;
uniform vec3 stageLight1Color;
uniform vec3 stageLight1Direction;

// Stage Light 2
uniform int renderStageLight2;
uniform vec3 stageLight2Color;
uniform vec3 stageLight2Direction;

// Stage Light 3
uniform int renderStageLight3;
uniform vec3 stageLight3Color;
uniform vec3 stageLight3Direction;

// Stage Light 4
uniform int renderStageLight4;
uniform vec3 stageLight4Color;
uniform vec3 stageLight4Direction;

// light_set fog
uniform int renderFog;
uniform vec3 stageFogColor;

// Character Lighting
uniform vec3 difLightColor;
uniform vec3 ambLightColor;
uniform vec3 difLightDirection;

uniform vec3 difLight2Color;
uniform vec3 ambLight2Color;
uniform vec3 difLight2Direction;

uniform vec3 difLight3Color;
uniform vec3 ambLight3Color;
uniform vec3 difLight3Direction;

uniform vec3 specLightColor;
uniform vec3 specLightDirection;

// Shared by characters and stages.
uniform vec3 fresGroundColor;
uniform vec3 fresSkyColor;
uniform vec3 fresSkyDirection;
uniform vec3 fresGroundDirection;

// NU_ Material Properties
uniform vec4 colorOffset;
uniform vec4 aoMinGain;
uniform vec4 fresnelColor;
uniform vec4 specularColor;
uniform vec4 specularColorGain;
uniform vec4 diffuseColor;
uniform vec4 characterColor;
uniform vec4 colorGain;
uniform vec4 finalColorGain;
uniform vec4 finalColorGain2;
uniform vec4 finalColorGain3;
uniform vec4 reflectionColor;
uniform vec4 fogColor;
uniform vec4 effColorGain;
uniform vec4 zOffset;

// NU_ material params
uniform vec4 fresnelParams;
uniform vec4 specularParams;
uniform vec4 reflectionParams;
uniform vec4 fogParams;
uniform vec4 normalParams;
uniform vec4 angleFadeParams;
uniform vec4 dualNormalScrollParams;
uniform vec4 alphaBlendParams;
uniform vec4 softLightingParams;
uniform vec4 customSoftLightParams;
uniform vec4 effUniverseParam;

float Luminance(vec3 rgb)
{
    const vec3 W = vec3(0.2125, 0.7154, 0.0721);
    return dot(rgb, W);
}

vec3 TintColor(vec3 diffuseColor, float tintAmount)
{
    // Approximates an overlay blend mode. Cheaper than converting to HSV/HSL.
    // Normalize the color to avoid color tint making the model darker.
    if (Luminance(diffuseColor) < 0.01)
        return vec3(1);
    vec3 colorTint = mix(vec3(1), (normalize(diffuseColor) * 2), min(tintAmount * 0.5, 1));
    return colorTint;
}

vec3 RampColor(float rampCoord, sampler2D ramp, int hasRamp){
    // TODO: Vertical component is always 0?
	rampCoord = clamp(rampCoord, 0.01, 0.99);
	return texture(ramp, vec2(1 - rampCoord, 0.0)).rgb * hasRamp;
}

vec3 SphereMapColor(vec3 N, vec3 viewNormal, sampler2D spheremap, int hasSphereMap)
{
    // Calculate UVs based on view space normals.
    vec2 sphereTexcoord = vec2(viewNormal.x, (1 - viewNormal.y));
    return texture(spheremap, sphereTexcoord).xyz * hasSphereMap;
}

vec3 ShiftTangent(vec3 tangent, vec3 normal, float shift) // probably not needed
{
    vec3 shiftedT = tangent + shift * normal;
    return normalize(shiftedT);
}

float Fresnel(vec3 I, vec3 N)
{
    return max(1 - max(dot(I, N), 0), 0);
}

vec3 BumpMapNormal(vec3 inputNormal, vec4 dualNormalScrollParams, sampler2D normalMap, vec2 normaltexCoord, int hasNrm, int useNormalMap,
    int hasDualNormal, vec4 normalParams, vec3 tangent, vec3 bitangent)
{
    if(hasNrm == 0 || useNormalMap == 0)
	   return inputNormal;

    // TODO: How does NU_dualNormalScrollParams work?
    vec3 bumpMapNormal = texture(normalMap, normaltexCoord).xyz;
    vec3 bumpMapNormal2 = texture(normalMap, vec2(normaltexCoord.x + dualNormalScrollParams.x, normaltexCoord.y + dualNormalScrollParams.y)).xyz;
    if (hasDualNormal == 1)
        bumpMapNormal = normalize(bumpMapNormal + bumpMapNormal2);

    // Change normal map intensity.
    bumpMapNormal = mix(vec3(0.5, 0.5, 1), bumpMapNormal, normalParams.x);

    bumpMapNormal = 2.0 * bumpMapNormal - vec3(1);
    mat3 tbnMatrix = mat3(tangent, bitangent, normalize(inputNormal));
    vec3 newNormal = tbnMatrix * bumpMapNormal;
    return normalize(newNormal);
}

vec3 BayoHairDiffuse(vec3 diffuseMap, vec4 colorOffset, vec4 colorGain, vec4 alphaBlendParams)
{
    vec3 diffuseColor = (colorOffset.rgb + diffuseMap.rrr) * colorGain.rgb;
    diffuseColor *= alphaBlendParams.w; // #justbayothings
    return diffuseColor;
}

vec3 ColorOffsetGain(vec3 diffuseMap, int hasBayoHair,vec4 colorOffset, vec4 colorGain, vec4 alphaBlendParams)
{
    if (hasBayoHair == 1)
        return BayoHairDiffuse(diffuseMap, colorOffset, colorGain, alphaBlendParams);

    // Offset the shadows first before adjusting the gain.
    vec3 resultingColor = vec3(Luminance(diffuseMap));
    resultingColor += colorOffset.rgb;
    resultingColor *= colorGain.rgb;
    return resultingColor;
}

vec3 SoftLighting(vec3 diffuse, vec4 params, float darkenMultiplier, float saturationMultiplier,
    float halfLambert)
{
    // Higher blend values make the dark region smoother and larger.
    float edgeL = 0.5;
    float edgeR = 0.5 + (params.z / 2);
    float smoothLambert = smoothstep(edgeL, edgeR, halfLambert);

    // Controls ambient brightness.
    float ambientGain = max(1 - (darkenMultiplier * params.y), 0);

    // Controls ambient saturation.
    // TODO: Need to check the math.
    // This next level math hacking may or may not be right.
    vec3 ambientTintColor = normalize(pow(diffuse, vec3(params.x * 0.3)));

    // Creates a custom diffuse gradient rather than using lighting?
    vec3 result = diffuse * mix(ambientTintColor * ambientGain, vec3(1), smoothLambert);
    return result;
}

vec3 FresnelPass(vec3 N, vec3 I, vec4 diffuseMap, float aoBlend, vec3 tintColor,
                 vec3 fresSkyDirection, vec3 fresGroundDirection, vec3 fresSkyColor, vec3 fresGroundColor, vec4 fresnelParams, vec4 fresnelColor, float fresnelIntensity)
{
    // hemisphere fresnel with fresnelParams exponent
    float hemiBlendSky = dot(N, fresSkyDirection) * 0.5 + 0.5;
    vec3 hemiColorSky = mix(vec3(0), fresSkyColor, hemiBlendSky);

    float hemiBlendGround = dot(N, fresGroundDirection) * 0.5 + 0.5;
    vec3 hemiColorGround = mix(vec3(0), fresGroundColor, hemiBlendGround);

    vec3 hemiColorTotal = hemiColorSky + hemiColorGround;

    // TODO: Find a more accurate value.
    float exponentOffset = 2.75;
    float fresnelExponent =  exponentOffset + fresnelParams.x;

    float fresnelTerm = clamp(pow(Fresnel(I, N), fresnelExponent), 0, 1);

    vec3 fresnelPass = hemiColorTotal * fresnelColor.rgb * fresnelTerm;
    fresnelPass *= aoBlend * fresnelIntensity * tintColor;
    return fresnelPass;
}

vec3 ReflectionPass(vec3 N, vec3 I, vec4 diffuseMap, float aoBlend, vec3 tintColor, samplerCube stagecube, int hasCube, vec4 reflectionParams,
                    int hasStage, vec4 reflectionColor, vec3 normal, vec3 viewNormal, sampler2D spheremap, int hasSphereMap, float reflectionIntensity, int useDifRefMask)
{
    vec3 reflectionPass = vec3(0);
	// cubemap reflection
	vec3 R = reflect(I, N);
	R.y *= -1.0;
	vec3 stageCubeColor = texture(stagecube, R).rgb;

    // TODO: Cubemaps from model.nut currently just use the miiverse cubemap.
	if (hasCube == 1)
		reflectionPass += diffuseMap.aaa * stageCubeColor * tintColor * reflectionParams.x;

    // TODO: Stage cubemaps currently just use the miiverse cubemap.
    if (hasStage == 1)
       reflectionPass += reflectionColor.rgb * stageCubeColor.rgb * tintColor;

	reflectionPass += SphereMapColor(normal.xyz, viewNormal, spheremap, hasSphereMap) * reflectionColor.xyz * tintColor;

    // It sort of conserves energy for low values.
    reflectionPass -= 0.5 * Luminance(diffuseMap.rgb);
    reflectionPass = max(vec3(0), reflectionPass);

    reflectionPass *= aoBlend;
    // reflectionPass *= refLightColor;
    reflectionPass *= reflectionIntensity;

    if (useDifRefMask == 1)
        reflectionPass *= diffuseMap.a;

    reflectionPass = pow(reflectionPass, vec3(2.2));
    return reflectionPass;
}

float AnisotropicSpecExponent(vec3 halfAngle, float width, float height, vec3 tangent, vec3 bitangent)
{
    // Blinn-phong with some anistropic bits stolen from an anistropic GGX BRDF.
    vec3 X = normalize(tangent);
    vec3 Y = normalize(bitangent);
    float xComponent = max(pow((dot(halfAngle, X) / width), 2), 0);
    float yComponent = max(pow((dot(halfAngle, Y) / height), 2), 0);

    return xComponent + yComponent;
}

vec3 BayoHairSpecular(vec3 diffuseMap, vec3 I, vec3 specLightDirection, vec4 reflectionParams, vec3 tangent, vec3 bitangent)
{
    float specMask = diffuseMap.b;

    vec3 halfAngle = normalize(I + specLightDirection);
    float exponent = AnisotropicSpecExponent(halfAngle, reflectionParams.z, reflectionParams.w, tangent, bitangent);
    float specularTerm = pow(dot(bitangent.xyz, halfAngle), exponent);

    // TODO: Find proper constants.
    vec3 specularColorTotal = vec3(1) * specularTerm * specMask;
    return specularColorTotal;
}

vec3 SpecularPass(vec3 N, vec3 I, vec4 diffuseMap, float aoBlend, vec3 tintColor,
                vec3 specLightDirection, vec4 reflectionParams, vec3 tangent, vec3 bitangent, int hasSpecularParams,
                    vec4 specularParams, vec4 specularColor, int hasBayoHair, int hasColorGainOffset, vec4 specularColorGain, uint flags, vec3 specLightColor,
                    float specularIntensity)
{
    vec3 specularPass = vec3(0);

    // Only uses the anisotropic exponent for mats without NU_specularParams.
    vec3 halfAngle = normalize(I + specLightDirection);
    float exponent = AnisotropicSpecExponent(halfAngle, reflectionParams.z, reflectionParams.w, tangent, bitangent);
    if (hasSpecularParams == 1)
        exponent = specularParams.y;

    float blinnPhongSpec = dot(halfAngle, N);
    blinnPhongSpec = pow(blinnPhongSpec, exponent);

    vec3 specColorTotal = specularColor.rgb * blinnPhongSpec;

    // TODO: Fix bayo hair calculations.
    if (hasBayoHair == 1)
        specColorTotal = BayoHairSpecular(diffuseMap.rgb, I, specLightDirection, reflectionParams, tangent, bitangent);

    if (hasColorGainOffset == 1)
        specColorTotal *= specularColorGain.rgb;

    // TODO: Not sure how this works. Specular works differently for eye mats.
    if ((flags & 0x00E10000u) == 0x00E10000u)
        specColorTotal *= diffuseMap.rgb;

    // It sort of conserves energy for low values.
    specularPass += max(specColorTotal - vec3(0.5 * Luminance(diffuseMap.rgb)), vec3(0));
    specularPass *= aoBlend;

    specularPass *= tintColor;
    specularPass *= specLightColor;
    specularPass *= specularIntensity;

    specularPass = pow(specularPass, vec3(1));
    return specularPass;
}

float AngleFade(vec3 N, vec3 I, vec4 angleFadeParams)
{
    float fresnelBlend = Fresnel(I, N);
    float angleFadeAmount = mix(angleFadeParams.x, angleFadeParams.y, fresnelBlend);
    return max((1 - angleFadeAmount), 0);
}

vec3 FogColor(vec3 inputColor, vec4 fogParams, float depth, vec3 stageFogColor, int renderFog, int renderStageLighting)
{
    depth = clamp((depth / fogParams.y), 0, 1);
    float fogIntensity = mix(fogParams.z, fogParams.w, depth);
    if(renderFog == 1 && renderStageLighting == 1)
        return mix(inputColor, pow((stageFogColor), vec3(2.2)), fogIntensity);
    else
        return inputColor;
}

vec3 UniverseColor(float universeParam, vec3 objectPosition, vec3 cameraPosition, mat4 modelViewMatrix, sampler2D dif)
{
    // TODO: Doesn't work for all angles (viewing from below).
    vec3 projVec = normalize(objectPosition.xyz - cameraPosition.xyz);
    projVec = mat3(modelViewMatrix) * projVec;
    // Cheap approximations of trig functions.
    float uCoord = projVec.x * 0.5 + 0.5;
    float vCoord = projVec.y * 0.5 + 0.5;
    // Gamma correct again to make the texture dark enough.
    return pow(texture(dif, vec2(uCoord * universeParam, 1 - vCoord)).rgb, vec3(2.2));
}

vec3 CharacterDiffuseLighting(float halfLambert, vec3 ambLightColor, float ambientIntensity, vec3 difLightColor, float diffuseIntensity)
{
    vec3 ambient = ambLightColor * ambientIntensity;
    vec3 diffuse = difLightColor * diffuseIntensity;
    return mix(ambient, diffuse, halfLambert);
}

vec3 StageDiffuseLighting(vec3 N, vec3 stageLight1Color, vec3 stageLight2Color, vec3 stageLight3Color, vec3 stageLight4Color,
                            vec3 stageLight1Direction, vec3 stageLight2Direction, vec3 stageLight3Direction, vec3 stageLight4Direction,
                            int renderStageLight1, int renderStageLight2, int renderStageLight3, int renderStageLight4)
{
    // TODO: Do stages use half lambert?
    vec3 stageLight1 = stageLight1Color * max((dot(N, stageLight1Direction)), 0);
    vec3 stageLight2 = stageLight2Color * max((dot(N, stageLight2Direction)), 0);
    vec3 stageLight3 = stageLight3Color * max((dot(N, stageLight3Direction)), 0);
    vec3 stageLight4 = stageLight4Color * max((dot(N, stageLight4Direction)), 0);

    vec3 lighting = vec3(0);
    lighting += (stageLight1 * renderStageLight1);
    lighting += (stageLight2 * renderStageLight2);
    lighting += (stageLight3 * renderStageLight3);
    lighting += (stageLight4 * renderStageLight4);
    return lighting;
}

vec3 Lighting(vec3 N, float halfLambert, vec3 ambLightColor, float ambientIntensity, vec3 difLightColor,
                            float diffuseIntensity, uint flags, int renderStageLighting, int isStage, vec3 stageLight1Color, vec3 stageLight2Color, vec3 stageLight3Color, vec3 stageLight4Color,
                            vec3 stageLight1Direction, vec3 stageLight2Direction, vec3 stageLight3Direction, vec3 stageLight4Direction,
                            int renderStageLight1, int renderStageLight2, int renderStageLight3, int renderStageLight4)
{
    // Flags for ignoring stage lighting. TODO: Replace bitwise operations.
    if (((flags & 0x0F000000u) < 0x04000000u) || renderStageLighting != 1)
        return vec3(1);

    if (isStage == 1) // stage lighting
        return StageDiffuseLighting(N, stageLight1Color, stageLight2Color, stageLight3Color, stageLight4Color,
                                    stageLight1Direction, stageLight2Direction, stageLight3Direction, stageLight4Direction,
                                    renderStageLight1, renderStageLight2, renderStageLight3, renderStageLight4);
    else
        return CharacterDiffuseLighting(halfLambert, ambLightColor, ambientIntensity, difLightColor, diffuseIntensity);

    return vec3(1);
}

vec3 DiffuseAOBlend(int hasNrm, sampler2D normalMap, vec2 texCoord, vec4 aoMinGain)
{
    // Calculate the effect of NU_aoMinGain on the ambient occlusion map.
    // TODO: Max should be 1 but doesn't look correct.
    float maxAOBlendValue = 1.25;
    float aoMap = 1;
    if (hasNrm == 1)
        aoMap = pow(texture(normalMap, texCoord).a, 2.2);
    vec3 aoBlend = vec3(aoMap);
    return min((aoBlend + aoMinGain.rgb), vec3(maxAOBlendValue));
}

float AmbientOcclusionBlend(vec4 diffuseMap, vec4 aoMinGain, VertexAttributes vert)
{
    // Ambient occlusion uses sRGB gamma.
    // Not all materials have an ambient occlusion map.
    float aoMap = pow(texture(normalMap, vert.texCoord).a, 2.2);
    if (hasNrm != 1)
        aoMap = 1;

    // The diffuse map for colorGain/Offset materials does a lot of things.
    if (hasColorGainOffset == 1 || useDiffuseBlend == 1)
        aoMap = Luminance(pow(diffuseMap.rgb, vec3(1 / 2.2)));

    float aoMixIntensity = aoMinGain.w;
    if (useDiffuseBlend == 1) // aomingain but no ao map (mainly for trophies)
        aoMixIntensity = 0;

    return mix(aoMap, 1, aoMixIntensity);
}

vec3 DiffusePass(vec3 N, vec4 diffuseMap, VertexAttributes vert)
{
    vec3 diffusePass = vec3(0);

    if (hasUniverseParam == 1)
        return UniverseColor(effUniverseParam.x, vert.objectPosition, cameraPosition, modelViewMatrix, dif);

    // Diffuse uses a half lambert for softer lighting.
    float halfLambert = dot(difLightDirection, N) * 0.5 + 0.5;
    float halfLambert2 = dot(difLight2Direction, N) * 0.5 + 0.5;
    float halfLambert3 = dot(difLight3Direction, N) * 0.5 + 0.5;
    vec3 diffuseColorFinal = vec3(0); // result of diffuse map, aoBlend, and some NU_values

    if (hasColorGainOffset == 1) // probably a more elegant solution...
        diffuseColorFinal = ColorOffsetGain(diffuseMap.rgb, hasBayoHair, colorOffset, colorGain, alphaBlendParams);
    else
        diffuseColorFinal = diffuseMap.rgb * DiffuseAOBlend(hasNrm, normalMap, vert.texCoord, aoMinGain) * diffuseColor.rgb;

    // Stage lighting
    // vec3 lighting = Lighting(N, halfLambert);
    vec3 lighting = vec3(1);

    // Dummy ramp calculations.
    // vec3 rampTotal = (0.2 * RampColor(halfLambert, ramp, hasRamp)) + (0.5 * RampColor(halfLambert, dummyRamp, hasDummyRamp));
    // vec3 rampAdd = pow(rampTotal, vec3(2.2));

    diffusePass = diffuseColorFinal * lighting;
    // TODO: Fix ramp shading. Disabled for now.
    // diffusePass += diffusePass * min(rampAdd, vec3(1));

    vec3 softLightDif = diffuseColorFinal * difLightColor;
    vec3 softLightAmb = diffuseColorFinal * ambLightColor;
    if (hasSoftLight == 1)
        diffusePass = SoftLighting(softLightDif, softLightingParams, 0.3, 0.0561, halfLambert);
    else if (hasCustomSoftLight == 1)
        diffusePass = SoftLighting(softLightDif, customSoftLightParams, 0.3, 0.114, halfLambert);

    // Flags used for brightening diffuse for softlightingparams.
    if (softLightBrighten == 1)
        diffusePass *= 1.5;

    diffusePass = pow(diffusePass, vec3(1));
    return diffusePass;
}

vec2 DistortedUvCoords(VertexAttributes vert, sampler2D normalMap, vec4 normalParams, int useNormalMap)
{
    // Similar effect to du dv but uses just the normal map.
    // TODO: does du dv affect other UV channels?
    float offsetIntensity = 0;
    if(useNormalMap == 1)
        offsetIntensity = normalParams.z;
    vec2 textureOffset = 1 - texture(normalMap, vert.normaltexCoord).xy;
    textureOffset = (textureOffset * 2) - 1; // Remap [0,1] to [-1,1]
    return vert.texCoord + (textureOffset * offsetIntensity);
}

vec4 DiffuseMapTotal(VertexAttributes vert)
{
    vec4 diffuseMapTotal = vec4(0);

    // Some stage water materials have NU_diffuseColor but no diffuse map.
    if (hasDif != 1)
        return diffuseColor;

    vec2 offsetTexCoord = DistortedUvCoords(vert, normalMap, normalParams, useNormalMap);

    // Blends all of the different diffuse textures together.
    if (hasDif == 1)
    {
        vec4 diffuse1 = texture(dif, offsetTexCoord) * finalColorGain.rgba;
        diffuseMapTotal = diffuse1;

        // TODO: 2nd diffuse texture. doesnt work properly with stages.
        if (hasDif2 == 1 && hasDif3 != 1)
        {
            vec4 diffuse2 = texture(dif2, vert.texCoord2) * finalColorGain2.rgba;
            diffuseMapTotal = mix(diffuse2, diffuse1, diffuse1.a);
            diffuseMapTotal.a = 1.0;

            if (hasDif3 == 1)
            {
                vec4 diffuse3 = texture(dif3, vert.texCoord3) * finalColorGain3.rgba;
                diffuseMapTotal = mix(diffuse3, diffuseMapTotal, diffuse3.a);
            }
        }
    }

    // TODO: Create uniform for flags comparison.
    if (hasAo == 1)
    {
        vec3 difAO = texture(ao, vert.texCoord2).rgb;
        float normalBlend = vert.normal.y * 0.5 + 0.5;

        if ((flags & 0x00410000u) == 0x00410000u)
            diffuseMapTotal.rgb = mix(difAO, diffuseMapTotal.rgb, normalBlend);
        else
            diffuseMapTotal.rgb *= difAO.rgb;
    }

    return pow(diffuseMapTotal, vec4(2.2, 2.2, 2.2, 1));
}

vec3 RenderPasses(vec4 diffuseMap, vec3 N, vec3 I, VertexAttributes vert)
{
    // Separate render pass calculations.
    vec3 diffusePass = DiffusePass(N, diffuseMap, vert);

    // Use total diffuse pass instead of just diffuse map color for tint.
    vec3 specTintColor = TintColor(diffusePass, specularColor.a);
    vec3 fresTintColor = TintColor(diffusePass, fresnelColor.a);
    vec3 reflTintColor = TintColor(diffusePass, reflectionColor.a);

    // The ambient occlusion calculations for diffuse are done separately.
    float ambientOcclusionBlend = AmbientOcclusionBlend(diffuseMap, aoMinGain, vert);
    // vec3 specularPass = SpecularPass(N, I, diffuseMap, ambientOcclusionBlend, specTintColor);
    // vec3 fresnelPass = FresnelPass(N, I, diffuseMap, ambientOcclusionBlend, fresTintColor);
	// vec3 reflectionPass = ReflectionPass(N, I, diffuseMap, ambientOcclusionBlend, reflTintColor);

	vec3 resultingColor = vec3(0);

	if(renderLighting == 1)
	{
        // Prevent negative colors for some GPUs.
    	resultingColor += max((diffusePass * renderDiffuse), 0);
    	// resultingColor += max((fresnelPass * renderFresnel), 0);
    	// resultingColor += max((specularPass * renderSpecular), 0);
    	// resultingColor += max((reflectionPass * renderReflection), 0);

        // light_set_param.bin fog
        // resultingColor = FogColor(resultingColor, fogParams, viewPosition.z, stageFogColor);
	}
	else
		resultingColor = diffusePass;

    resultingColor = pow(resultingColor, vec3(1 / 2.2));
    return resultingColor;
}

vec4 SmashShader(VertexAttributes vert)
{
    vec4 resultingColor = vec4(vert.normal, 1);

    vec3 I = vec3(0,0,-1) * mat3(mvpMatrix);
    vec3 N = BumpMapNormal(vert.normal, dualNormalScrollParams, normalMap, vert.normaltexCoord, hasNrm, useNormalMap,
        hasDualNormal, normalParams, vert.tangent, vert.bitangent);

    // zOffset correction
    // TODO: Divide by far plane?
    // gl_FragDepth = gl_FragCoord.z - (zOffset.x / 1500) - zBufferOffset;

    // Calculate diffuse map blending.
    vec4 diffuseMapTotal = DiffuseMapTotal(vert);

    // TODO: Research how mii colors work.
    // diffuseMapTotal *= characterColor.rgba;
    // Material lighting done in SmashShader
    resultingColor.rgb = RenderPasses(diffuseMapTotal, N, I, vert);

    // TODO: Max vertex color value?
    if (renderVertColor == 1 || hasFinalColorGain == 1)
        resultingColor *= min(vert.vertexColor, vec4(1));

    // Universe mats are weird...
    if (hasUniverseParam != 1)
        resultingColor.rgb *= effColorGain.rgb;

    // Adjust the alpha.
    // TODO: Is this affected by multiple diffuse maps?
    resultingColor.a *= texture(dif, vert.texCoord).a;
    resultingColor.a *= finalColorGain.a;
    resultingColor.a *= effColorGain.a;
    resultingColor.a *= AngleFade(N, I, angleFadeParams);
    resultingColor.a += alphaBlendParams.x; // TODO: Ryu works differently.

    // Alpha override for render settings. Fixes alpha for renders.
    if (renderAlpha != 1 || isTransparent != 1)
        resultingColor.a = 1;

    return resultingColor;
}
