// Cielo estilizado del lago (skybox para URP).
//  - Degradado cenit -> horizonte -> suelo, con una franja cálida en el horizonte del lado del sol.
//  - Sol con disco suave y halo (sigue a la luz direccional principal de la escena).
//  - Nubes procedurales que se mueven con el viento: bordes suaves pero definidos (como pintadas),
//    lado iluminado claro, sombra azulada y "borde de plata" cuando pasan cerca del sol.
// Lo aplica LakeAmbience (Resources/Ambiente/Cielo_Lago). Para volver al cielo anterior, desmarca
// "Use Stylized Sky" en LakeAmbience.
Shader "FishOutOfWater/Cielo Estilizado"
{
    Properties
    {
        [Header(Degradado)]
        _TopColor ("Cenit", Color) = (0.20, 0.47, 0.90, 1)
        _MidColor ("Cielo medio", Color) = (0.40, 0.66, 0.95, 1)
        _HorizonColor ("Horizonte", Color) = (0.72, 0.85, 0.96, 1)
        _GroundColor ("Bajo el horizonte", Color) = (0.50, 0.62, 0.68, 1)
        _MidHeight ("Altura del cielo medio", Range(0.02, 0.9)) = 0.22
        _HorizonSharpness ("Nitidez del horizonte", Range(1, 40)) = 9

        [Header(Sol)]
        _SunColor ("Color del sol", Color) = (1.0, 0.95, 0.82, 1)
        _SunSize ("Tamaño del disco", Range(0.0, 0.1)) = 0.035
        _SunSoftness ("Borde del disco", Range(0.001, 0.05)) = 0.012
        _SunIntensity ("Brillo del disco", Range(0, 8)) = 3
        _GlowColor ("Color del halo", Color) = (1.0, 0.82, 0.55, 1)
        _GlowPower ("Concentración del halo", Range(1, 64)) = 10
        _GlowStrength ("Intensidad del halo", Range(0, 2)) = 0.55
        _HorizonGlow ("Franja cálida del horizonte", Range(0, 2)) = 0.3

        [Header(Nubes)]
        _CloudLit ("Nube iluminada", Color) = (1.0, 0.99, 0.96, 1)
        _CloudShadow ("Sombra de la nube", Color) = (0.54, 0.63, 0.82, 1)
        _CloudCover ("Cobertura (más bajo = más nubes)", Range(0, 1)) = 0.47
        _CloudSoftness ("Suavidad del borde", Range(0.01, 0.5)) = 0.07
        _CloudScale ("Tamaño (más alto = nubes más chicas)", Range(0.05, 3)) = 0.5
        _CloudCurve ("Curvatura de la bóveda", Range(0.02, 1)) = 0.22
        _CloudSpeed ("Velocidad (x, z)", Vector) = (0.012, 0.004, 0, 0)
        _CloudOpacity ("Opacidad", Range(0, 1)) = 0.92
        _CloudShading ("Volumen (luz y sombra)", Range(0, 4)) = 2.2
        _CloudHorizonFade ("Desvanecido en el horizonte", Range(0.01, 0.5)) = 0.12
        _CloudBands ("Bandas de color (0 = suave)", Range(0, 6)) = 3
    }

    SubShader
    {
        Tags { "RenderPipeline" = "UniversalPipeline" "Queue" = "Background" "RenderType" = "Background" "PreviewType" = "Skybox" }
        Cull Off
        ZWrite Off

        Pass
        {
            Name "CieloEstilizado"

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 3.0

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 _TopColor;
                half4 _MidColor;
                half4 _HorizonColor;
                half4 _GroundColor;
                float _MidHeight;
                float _HorizonSharpness;
                half4 _SunColor;
                float _SunSize;
                float _SunSoftness;
                float _SunIntensity;
                half4 _GlowColor;
                float _GlowPower;
                float _GlowStrength;
                float _HorizonGlow;
                half4 _CloudLit;
                half4 _CloudShadow;
                float _CloudCover;
                float _CloudSoftness;
                float _CloudScale;
                float _CloudCurve;
                float4 _CloudSpeed;
                float _CloudOpacity;
                float _CloudShading;
                float _CloudHorizonFade;
                float _CloudBands;
            CBUFFER_END

            // Viento global (lo actualiza LakeAmbience): desplazamiento extra de las nubes con las ráfagas
            float2 _FOW_WindOffset;

            struct Attributes
            {
                float4 positionOS : POSITION;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 dirOS : TEXCOORD0;
            };

            Varyings vert(Attributes v)
            {
                Varyings o;
                o.positionCS = TransformObjectToHClip(v.positionOS.xyz);
                o.dirOS = v.positionOS.xyz;
                return o;
            }

            // ---- Ruido (valor) ----
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
                float2 u = f * f * (3.0 - 2.0 * f);
                float a = Hash21(i);
                float b = Hash21(i + float2(1, 0));
                float c = Hash21(i + float2(0, 1));
                float d = Hash21(i + float2(1, 1));
                return lerp(lerp(a, b, u.x), lerp(c, d, u.x), u.y);
            }

            // Suma de octavas de ruido; 'octaves' = cuántas (5 = detalle fino, 3 = solo la forma grande)
            float Fbm(float2 p, int octaves)
            {
                float s = 0.0;
                float a = 0.5;
                // Rotación entre octavas: así no se nota la cuadrícula del ruido
                const float2x2 m = float2x2(1.6, 1.2, -1.2, 1.6);
                for (int k = 0; k < octaves; k++)
                {
                    s += a * ValueNoise(p);
                    p = mul(m, p);
                    a *= 0.5;
                }
                return s;
            }

            half4 frag(Varyings i) : SV_Target
            {
                float3 d = normalize(i.dirOS);
                float y = d.y;

                // ---- Degradado ----
                float up = saturate(y);
                float3 sky = lerp(_HorizonColor.rgb, _MidColor.rgb, smoothstep(0.0, _MidHeight, up));
                sky = lerp(sky, _TopColor.rgb, smoothstep(_MidHeight, 1.0, up));
                float below = saturate(-y * _HorizonSharpness);
                sky = lerp(sky, _GroundColor.rgb, smoothstep(0.0, 1.0, below));

                // ---- Sol y halo ----
                Light mainLight = GetMainLight();
                // Sin luz principal (vista previa del material): un sol de media mañana
                float3 ld = mainLight.direction;
                float3 sunDir = dot(ld, ld) > 1e-4 ? normalize(ld) : normalize(float3(0.35, 0.55, 0.6));
                float sd = dot(d, sunDir);
                float sunEdge = 1.0 - _SunSize * _SunSize;
                float disk = smoothstep(sunEdge - _SunSoftness * _SunSoftness * 4.0, sunEdge, sd);
                float glow = pow(saturate(sd), _GlowPower) * _GlowStrength;
                // Franja cálida en el horizonte, más fuerte del lado del sol
                float horizonBand = exp(-abs(y) * _HorizonSharpness * 0.8);
                float sunSide = pow(saturate(sd * 0.5 + 0.5), 3.0);
                sky += _GlowColor.rgb * (glow + horizonBand * sunSide * _HorizonGlow);

                // ---- Nubes ----
                float cloudMask = 0.0;
                if (y > 0.0)
                {
                    float2 uv = d.xz / (y + _CloudCurve) * _CloudScale;
                    uv += _Time.y * _CloudSpeed.xy + _FOW_WindOffset;
                    // Forma: ruido grande deformado por otro ruido (bordes con rizos)
                    float2 warp = float2(Fbm(uv * 1.7 + 3.1, 4), Fbm(uv * 1.7 - 7.4, 4)) - 0.5;
                    float2 wuv = uv + warp * 0.6;
                    float n = Fbm(wuv, 5);
                    float cover = smoothstep(_CloudCover, _CloudCover + _CloudSoftness, n);

                    // Volumen con la forma grande (3 octavas): si la densidad baja hacia el sol, ese lado
                    // está iluminado. Así la luz y la sombra van en bloques, no en manchas
                    float2 toSun = sunDir.xz;
                    float lenSun = length(toSun);
                    toSun = lenSun > 1e-4 ? toSun / lenSun : float2(0, 0);
                    float nl = Fbm(wuv, 3);
                    float nl2 = Fbm(wuv + toSun * 0.14, 3);
                    float lit = saturate(0.62 + (nl - nl2) * 3.5 * _CloudShading);
                    // El interior más denso, un poco más oscuro
                    lit *= lerp(1.0, 0.82, smoothstep(_CloudCover + 0.05, _CloudCover + 0.35, n));
                    // Bandas suaves de color (aspecto pintado)
                    if (_CloudBands > 0.5)
                    {
                        float b = floor(lit * _CloudBands + 0.5) / _CloudBands;
                        lit = lerp(lit, b, 0.55);
                    }
                    float3 cloudCol = lerp(_CloudShadow.rgb, _CloudLit.rgb, lit);
                    // Borde de plata cerca del sol y tinte del horizonte
                    float rim = (1.0 - smoothstep(0.0, 0.5, cover)) * cover * 4.0;
                    cloudCol += _GlowColor.rgb * glow * (0.6 + rim);
                    cloudCol = lerp(cloudCol, _HorizonColor.rgb, horizonBand * 0.35);

                    float fade = smoothstep(0.0, _CloudHorizonFade, y);
                    cloudMask = cover * fade * _CloudOpacity;
                    sky = lerp(sky, cloudCol, cloudMask);
                }

                // Disco del sol (tapado por las nubes)
                sky += _SunColor.rgb * disk * _SunIntensity * (1.0 - cloudMask * 0.85);

                return half4(sky, 1.0);
            }
            ENDHLSL
        }
    }
    Fallback Off
}
