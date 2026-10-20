namespace BinaryTernaryCraft.Shaders
{
    public static class ShaderSources
    {
        public const string VoxelVertexShader = @"#version 450 core
layout(location = 0) in vec3 aPos;
layout(location = 1) in vec3 aNormal;
layout(location = 2) in vec2 aTexCoords;
layout(location = 3) in uint aGlobalLogicIndex;
layout(location = 4) in vec3 aColorTint;

uniform mat4 uView;
uniform mat4 uProjection;

layout(std430, binding = 0) readonly buffer StateBuffer {
    int States[];
};

out vec3 vNormal;
out vec2 vTexCoords;
flat out uint vGlobalLogicIndex;
out vec3 vColorTint;
flat out int vTritState;

void main()
{
    vNormal = aNormal;
    vTexCoords = aTexCoords;
    vGlobalLogicIndex = aGlobalLogicIndex;
    vColorTint = aColorTint;
    
    // Read dynamic TritState directly from SSBO on GPU
    vTritState = States[aGlobalLogicIndex];

    gl_Position = uProjection * uView * vec4(aPos, 1.0);
}
";

        public const string VoxelFragmentShader = @"#version 450 core
in vec3 vNormal;
in vec2 vTexCoords;
flat in uint vGlobalLogicIndex;
in vec3 vColorTint;
flat in int vTritState;

uniform sampler2D uTextureAtlas;

out vec4 FragColor;

void main()
{
    vec4 texColor = texture(uTextureAtlas, vTexCoords);
    if (texColor.a < 0.1) discard;

    // Simple directional sun lighting
    vec3 lightDir = normalize(vec3(0.4, 0.9, 0.3));
    float diff = max(dot(vNormal, lightDir), 0.25);
    vec3 baseColor = texColor.rgb * diff * vColorTint;

    // Dynamic Trit RGB Emissive Glow:
    // -1: Glowing Red (R)
    //  0: Neutral Green (G)
    // +1: Glowing Blue (B)
    vec3 tritGlow = vec3(0.0);
    if (vTritState == -1) {
        tritGlow = vec3(1.0, 0.08, 0.08); // Vivid Red
    } else if (vTritState == 1) {
        tritGlow = vec3(0.08, 0.45, 1.0); // Electric Blue
    } else {
        tritGlow = vec3(0.08, 0.85, 0.2); // Emerald Green
    }

    // If texture pixel is near-white or metallic, it represents the active logic indicator
    float indicatorFactor = step(0.65, (texColor.r + texColor.g + texColor.b) / 3.0);
    vec3 finalRgb = mix(baseColor, tritGlow * 1.35, indicatorFactor);

    FragColor = vec4(finalRgb, texColor.a);
}
";

        public const string KineticVertexShader = @"#version 450 core
layout(location = 0) in vec3 aPos;
layout(location = 1) in vec3 aNormal;
layout(location = 2) in vec2 aTexCoords;
layout(location = 3) in uint aGlobalLogicIndex;
layout(location = 4) in vec3 aColorTint;

// Instanced Attributes
layout(location = 5) in vec3 aInstancePos;
layout(location = 6) in vec3 aInstanceAxis;
layout(location = 7) in vec3 aInstancePivot;
layout(location = 8) in int aInstanceNetworkId;
layout(location = 9) in float aInstanceGearRatio;

uniform mat4 uView;
uniform mat4 uProjection;
uniform float uTime;

layout(std430, binding = 1) readonly buffer KineticBuffer {
    float Speeds[];
};

out vec3 vNormal;
out vec2 vTexCoords;

// Rodrigues' rotation formula for arbitrary axis rotation around pivot
vec3 rotateAxis(vec3 v, vec3 axis, float angle) {
    float c = cos(angle);
    float s = sin(angle);
    return v * c + cross(axis, v) * s + axis * dot(axis, v) * (1.0 - c);
}

void main()
{
    vTexCoords = aTexCoords;

    float speed = Speeds[aInstanceNetworkId];
    float angle = uTime * speed * aInstanceGearRatio;

    // Rotate vertex around model pivot on GPU
    vec3 localPos = aPos - aInstancePivot;
    vec3 rotatedPos = rotateAxis(localPos, aInstanceAxis, angle) + aInstancePivot;
    vec3 worldPos = rotatedPos + aInstancePos;

    vNormal = rotateAxis(aNormal, aInstanceAxis, angle);

    gl_Position = uProjection * uView * vec4(worldPos, 1.0);
}
";

        public const string KineticFragmentShader = @"#version 450 core
in vec3 vNormal;
in vec2 vTexCoords;

uniform sampler2D uTextureAtlas;

out vec4 FragColor;

void main()
{
    vec4 texColor = texture(uTextureAtlas, vTexCoords);
    if (texColor.a < 0.1) discard;

    vec3 lightDir = normalize(vec3(0.4, 0.9, 0.3));
    float diff = max(dot(vNormal, lightDir), 0.35);

    FragColor = vec4(texColor.rgb * diff, texColor.a);
}
";
    }
}
