// Agua estilizada para el lago (URP).
// Estilo del protagonista: azul marino profundo, brillo de borde cian, espuma luminosa.
// La malla la genera LakeWater.cs; en uv.x viene la profundidad (m) de cada vértice,
// así la espuma de orilla y el degradado de color no necesitan textura de profundidad.
Shader "FishOutOfWater/Agua Estilizada"
{
    Properties
    {
        [Header(Colores)]
        _ShallowColor ("Color poco profundo (orilla)", Color) = (0.16, 0.55, 0.95, 0.55)
        _DeepColor ("Color profundo", Color) = (0.02, 0.07, 0.22, 0.93)
        _MaxDepth ("Profundidad del color profundo (m)", Float) = 6
        _UnderColor ("Color visto desde abajo", Color) = (0.22, 0.62, 1.0, 0.55)

        [Header(Brillo de borde (como el traje))]
        _RimColor ("Color del brillo", Color) = (0.3, 0.7, 1.0, 1)
        _RimPower ("Concentración", Range(0.5, 8)) = 3
        _RimStrength ("Intensidad", Range(0, 2)) = 0.7

        [Header(Espuma de orilla)]
        _FoamColor ("Color de la espuma", Color) = (0.7, 0.93, 1.0, 1)
        _FoamDepth ("Ancho de la espuma (m)", Float) = 0.9
        _FoamGlow ("Brillo de la espuma", Range(0, 3)) = 1.3

        [Header(Borde en objetos medio sumergidos)]
        _EdgeFoamColor ("Color del borde", Color) = (1, 1, 1, 1)
        _EdgeFoamWidth ("Ancho del borde (m)", Range(0.02, 1.5)) = 0.35
        _EdgeFoamStrength ("Intensidad del borde", Range(0, 2)) = 1.2

        [Header(Ondas (agua calmada))]
        _WaveHeight ("Altura de las ondas (m)", Range(0, 0.3)) = 0.035
        _WaveScale ("Tamaño de las ondas", Float) = 0.08
        _WaveSpeed ("Velocidad de las ondas", Float) = 0.22
        _NormalStrength ("Relieve de la superficie", Range(0, 2)) = 0.6

        [Header(Reflejo del sol)]
        _SunColor ("Color del reflejo", Color) = (1, 1, 1, 1)
        _SunSize ("Tamaño del reflejo", Range(0.85, 0.999)) = 0.975
        _Sparkle ("Destellos", Range(0, 1)) = 0.4

        [Header(Anillos de impacto)]
        _RippleColor ("Color de los anillos", Color) = (0.75, 0.96, 1, 1)
        _RippleSpeed ("Velocidad de expansión", Float) = 3.2
        _RippleLife ("Duración (s)", Float) = 2.2
        _RippleWidth ("Grosor", Float) = 0.35
    }

    SubShader
    {
        Tags { "RenderPipeline" = "UniversalPipeline" "RenderType" = "Transparent" "Queue" = "Transparent-10" "IgnoreProjector" = "True" }

        Pass
        {
            Name "AguaEstilizada"
            Tags { "LightMode" = "UniversalForward" }

            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            Cull Off

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 3.0
            #pragma multi_compile_fog

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"

            #define MAX_RIPPLES 16

            // Anillos de impacto (los envía LakeWater.cs): xy = posición XZ, z = tiempo de inicio, w = fuerza
            float4 _WaterRipples[MAX_RIPPLES];

            CBUFFER_START(UnityPerMaterial)
                half4 _ShallowColor;
                half4 _DeepColor;
                half4 _UnderColor;
                half4 _RimColor;
                half4 _FoamColor;
                half4 _SunColor;
                half4 _RippleColor;
                half4 _EdgeFoamColor;
                float _EdgeFoamWidth;
                float _EdgeFoamStrength;
                float _MaxDepth;
                float _RimPower;
                float _RimStrength;
                float _FoamDepth;
                float _FoamGlow;
                float _WaveHeight;
                float _WaveScale;
                float _WaveSpeed;
                float _NormalStrength;
                float _SunSize;
                float _Sparkle;
                float _RippleSpeed;
                float _RippleLife;
                float _RippleWidth;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv : TEXCOORD0; // x = profundidad en metros
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                float depth : TEXCOORD1;
                float fogFactor : TEXCOORD2;
            };

            // ---------- Ruido ----------
            float Hash21(float2 p)
            {
                p = frac(p * float2(123.34, 456.21));
                p += dot(p, p + 45.32);
                return frac(p.x * p.y);
            }

            float ValueNoise(float2 p)
            {
                float2 i = floor(p);
                float2 f = frac(p);
                float a = Hash21(i);
                float b = Hash21(i + float2(1, 0));
                float c = Hash21(i + float2(0, 1));
                float d = Hash21(i + float2(1, 1));
                float2 u = f * f * (3.0 - 2.0 * f);
                return lerp(lerp(a, b, u.x), lerp(c, d, u.x), u.y);
            }

            // Ondas suaves y lentas (agua calmada), resultado en [-1, 1]
            float WaveHeight(float2 xz, float t)
            {
                float2 p = xz * _WaveScale;
                float h = ValueNoise(p + float2(t, t * 0.7)) * 0.6
                        + ValueNoise(p * 2.3 - float2(t * 0.8, t * 1.1)) * 0.4;
                return (h - 0.5) * 2.0;
            }

            Varyings vert(Attributes v)
            {
                Varyings o;
                float3 ws = TransformObjectToWorld(v.positionOS.xyz);
                ws.y += WaveHeight(ws.xz, _Time.y * _WaveSpeed) * _WaveHeight;
                o.positionWS = ws;
                o.positionCS = TransformWorldToHClip(ws);
                o.depth = v.uv.x;
                o.fogFactor = ComputeFogFactor(o.positionCS.z);
                return o;
            }

            half4 frag(Varyings i, FRONT_FACE_TYPE face : FRONT_FACE_SEMANTIC) : SV_Target
            {
                bool front = IS_FRONT_VFACE(face, true, false);
                float t = _Time.y;
                float2 xz = i.positionWS.xz;

                // ---------- Normal: ondas + micro-relieve ----------
                float wt = t * _WaveSpeed;
                float e = 0.6;
                float hL = WaveHeight(xz - float2(e, 0), wt);
                float hR = WaveHeight(xz + float2(e, 0), wt);
                float hD = WaveHeight(xz - float2(0, e), wt);
                float hU = WaveHeight(xz + float2(0, e), wt);
                float2 slope = float2(hR - hL, hU - hD) * _NormalStrength * 0.5;
                float detail = ValueNoise(xz * 0.9 + t * 0.35) - ValueNoise(xz * 1.3 - t * 0.27);
                slope += detail * 0.08 * _NormalStrength;

                // ---------- Anillos de impacto ----------
                float rippleGlow = 0;
                float2 rippleSlope = 0;
                for (int r = 0; r < MAX_RIPPLES; r++)
                {
                    float4 rp = _WaterRipples[r];
                    if (rp.w <= 0.001) continue;
                    float age = t - rp.z;
                    if (age < 0.0 || age > _RippleLife) continue;

                    float2 dv = xz - rp.xy;
                    float d = length(dv) + 1e-4;
                    float radius = age * _RippleSpeed * (0.6 + rp.w * 0.6);
                    float fade = 1.0 - age / _RippleLife;
                    fade *= fade;

                    float x1 = (d - radius) / _RippleWidth;
                    float g1 = exp(-x1 * x1);
                    float x2 = (d - radius * 0.55) / (_RippleWidth * 0.8);
                    float g2 = exp(-x2 * x2);

                    rippleGlow += (g1 + g2 * 0.5) * rp.w * fade;
                    rippleSlope += (dv / d) * (-2.0 * x1 * g1) * rp.w * fade * 0.25;
                }
                slope += rippleSlope;
                rippleGlow = saturate(rippleGlow);

                float3 N = normalize(float3(-slope.x, 1.0, -slope.y));
                if (!front) N = -N;
                float3 V = GetWorldSpaceNormalizeViewDir(i.positionWS);

                Light mainLight = GetMainLight();
                float3 L = normalize(mainLight.direction);
                half3 sun = min(mainLight.color.rgb, 1.5);

                float fres = pow(1.0 - saturate(dot(N, V)), _RimPower);

                // ---------- Vista desde abajo (bajo el agua) ----------
                if (!front)
                {
                    half3 under = _UnderColor.rgb * (0.75 + 0.5 * fres) + _RippleColor.rgb * rippleGlow * 0.6;
                    under = MixFog(under, i.fogFactor);
                    return half4(under, saturate(_UnderColor.a + rippleGlow * 0.3));
                }

                // ---------- Color base por profundidad ----------
                float depth01 = saturate(i.depth / max(0.01, _MaxDepth));
                float dShade = smoothstep(0.0, 1.0, depth01);
                half4 baseCol = lerp(_ShallowColor, _DeepColor, dShade);
                float ndl = saturate(dot(N, L));
                half3 col = baseCol.rgb * (0.75 + 0.25 * ndl);

                // Brillo de borde (fresnel), como la luz de contorno del traje
                col += _RimColor.rgb * fres * _RimStrength;

                // Reflejo del sol estilizado + destellos
                float3 H = normalize(L + V);
                float ndh = saturate(dot(N, H));
                float spec = smoothstep(_SunSize, _SunSize + 0.006, ndh);
                float sparkle = step(1.0 - _Sparkle * 0.12, ValueNoise(xz * 6.0 + t * 0.6)) * smoothstep(_SunSize - 0.1, _SunSize, ndh);
                col += _SunColor.rgb * sun * (spec + sparkle);

                // Espuma de orilla: borde roto con ruido + líneas que llegan a la orilla
                float fn = ValueNoise(xz * 1.6 + t * 0.2);
                float foamMask = 1.0 - saturate(i.depth / max(0.01, _FoamDepth));
                float foam = smoothstep(0.38, 0.48, foamMask + (fn - 0.5) * 0.5);
                float band = frac(i.depth / max(0.01, _FoamDepth * 1.5) + t * 0.25);
                float lines = smoothstep(0.88, 0.94, band) * (1.0 - saturate(i.depth / max(0.01, _FoamDepth * 3.0))) * step(0.35, fn);
                foam = saturate(foam + lines);
                col = lerp(col, _FoamColor.rgb * _FoamGlow, foam);

                // Anillos
                col += _RippleColor.rgb * rippleGlow * 0.9;

                // ---------- Borde blanco donde un objeto atraviesa la superficie ----------
                // Compara la profundidad de la escena detrás del agua con la del agua misma:
                // si la diferencia es pequeña, aquí hay un pez/jugador/roca saliendo del agua.
                float2 screenUV = GetNormalizedScreenSpaceUV(i.positionCS);
                float sceneEye = LinearEyeDepth(SampleSceneDepth(screenUV), _ZBufferParams);
                float waterEye = LinearEyeDepth(i.positionCS.z, _ZBufferParams);
                float thickness = max(0.0, sceneEye - waterEye);
                float edgeNoise = ValueNoise(xz * 3.0 + t * 0.9);
                float edgeBand = 1.0 - saturate(thickness / max(0.01, _EdgeFoamWidth));
                float edgeFoam = smoothstep(0.35, 0.65, edgeBand + (edgeNoise - 0.5) * 0.4);
                float edgeGlow = (1.0 - saturate(thickness / max(0.01, _EdgeFoamWidth * 2.0))) * 0.25;
                float edge = saturate(edgeFoam + edgeGlow) * _EdgeFoamStrength;
                col = lerp(col, _EdgeFoamColor.rgb, saturate(edge));

                float alpha = lerp(_ShallowColor.a, _DeepColor.a, dShade);
                alpha = saturate(alpha + fres * 0.25 + foam + rippleGlow * 0.5 + spec * 0.5 + edge);

                col = MixFog(col, i.fogFactor);
                return half4(col, alpha);
            }
            ENDHLSL
        }
    }

    FallBack Off
}
