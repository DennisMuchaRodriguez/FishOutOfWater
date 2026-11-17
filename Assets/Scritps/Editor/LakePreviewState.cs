using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

// Estado de la vista previa del lago (LakePreviewMenu): si está encendida y lo que había antes en la escena
// (cielo, niebla, luz ambiente y color del sol). Vive solo en memoria y sobrevive a las recompilaciones
// (ScriptableSingleton), así se puede devolver todo como estaba. Va en su propio archivo porque Unity necesita
// que el nombre del archivo coincida con la clase para volver a encontrarla después de recompilar.
public class LakePreviewState : ScriptableSingleton<LakePreviewState>
{
    public bool on;
    public bool hasSaved;
    public Material skybox;
    public bool fog;
    public FogMode fogMode;
    public Color fogColor;
    public float fogDensity, fogStart, fogEnd;
    public AmbientMode ambientMode;
    public Color ambientSky, ambientEquator, ambientGround, ambientLight;
    public float ambientIntensity;
    public Light sun;
    public Color sunColor;

    public void Save(Light sunLight)
    {
        skybox = RenderSettings.skybox;
        fog = RenderSettings.fog;
        fogMode = RenderSettings.fogMode;
        fogColor = RenderSettings.fogColor;
        fogDensity = RenderSettings.fogDensity;
        fogStart = RenderSettings.fogStartDistance;
        fogEnd = RenderSettings.fogEndDistance;
        ambientMode = RenderSettings.ambientMode;
        ambientSky = RenderSettings.ambientSkyColor;
        ambientEquator = RenderSettings.ambientEquatorColor;
        ambientGround = RenderSettings.ambientGroundColor;
        ambientLight = RenderSettings.ambientLight;
        ambientIntensity = RenderSettings.ambientIntensity;
        sun = sunLight;
        sunColor = sunLight != null ? sunLight.color : Color.white;
        hasSaved = true;
    }

    public void Restore()
    {
        RenderSettings.skybox = skybox;
        RenderSettings.fog = fog;
        RenderSettings.fogMode = fogMode;
        RenderSettings.fogColor = fogColor;
        RenderSettings.fogDensity = fogDensity;
        RenderSettings.fogStartDistance = fogStart;
        RenderSettings.fogEndDistance = fogEnd;
        RenderSettings.ambientMode = ambientMode;
        RenderSettings.ambientSkyColor = ambientSky;
        RenderSettings.ambientEquatorColor = ambientEquator;
        RenderSettings.ambientGroundColor = ambientGround;
        RenderSettings.ambientLight = ambientLight;
        RenderSettings.ambientIntensity = ambientIntensity;
        if (sun != null) sun.color = sunColor;
        hasSaved = false;
    }
}
