using System.Collections.Generic;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

// Antes de cada build deja el terreno del lago PINTADO DE VERDAD (lo mismo que el menú Herramientas > Terreno >
// Pintar terreno (permanente)): las capas y la mezcla quedan guardadas en los TerrainData del proyecto.
// Por qué: en el editor TerrainStylizer pinta al dar Play sobre una copia del TerrainData; en la build eso deja un
// terreno que choca pero no se dibuja (se ve el cielo y los árboles flotando). Un terreno que ya viene pintado es el
// caso normal de Unity: la build incluye lo que necesita para dibujarlo y al cargar no hay que pintar nada.
// Solo pinta si hace falta (la primera vez, o si cambiaron las capas del TerrainStylizer): las siguientes builds no
// tocan nada. Los TerrainData pintados cambian en el proyecto: súbelos al repositorio junto con lo demás.
class TerrainBuildPaint : IPreprocessBuildWithReport
{
    public int callbackOrder { get { return -100; } }

    public void OnPreprocessBuild(BuildReport report)
    {
        foreach (string path in ScenesWithStylizer())
        {
            try
            {
                int painted = PaintScene(path);
                if (painted > 0)
                    Debug.Log("TerrainBuildPaint: " + painted + " terrenos de " + path + " pintados y guardados para la build.");
            }
            catch (System.Exception e)
            {
                Debug.LogWarning("TerrainBuildPaint: no se pudo pintar el terreno de " + path + " antes de la build (" + e.Message +
                                 "). Abre la escena y usa Herramientas > Terreno > Pintar terreno (permanente), y vuelve a hacer la build.");
            }
        }
    }

    // Las escenas del proyecto que tienen un TerrainStylizer (en este juego, solo la del lago)
    static List<string> ScenesWithStylizer()
    {
        List<string> result = new List<string>();
        string script = StylizerScriptPath();
        if (string.IsNullOrEmpty(script)) return result;
        foreach (string guid in AssetDatabase.FindAssets("t:Scene", new[] { "Assets" }))
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            foreach (string dep in AssetDatabase.GetDependencies(path, false))
            {
                if (dep == script) { result.Add(path); break; }
            }
        }
        return result;
    }

    static string StylizerScriptPath()
    {
        foreach (string guid in AssetDatabase.FindAssets("TerrainStylizer t:MonoScript"))
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            MonoScript ms = AssetDatabase.LoadAssetAtPath<MonoScript>(path);
            if (ms != null && ms.GetClass() == typeof(TerrainStylizer)) return path;
        }
        return null;
    }

    static int PaintScene(string path)
    {
        Scene scene = SceneManager.GetSceneByPath(path);
        bool opened = false;
        if (!scene.IsValid() || !scene.isLoaded)
        {
            scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Additive);
            opened = true;
        }
        try
        {
            TerrainStylizer stylizer = null;
            List<Terrain> terrains = new List<Terrain>();
            foreach (GameObject root in scene.GetRootGameObjects())
            {
                if (stylizer == null) stylizer = root.GetComponentInChildren<TerrainStylizer>(true);
                terrains.AddRange(root.GetComponentsInChildren<Terrain>(true));
            }
            if (stylizer == null || terrains.Count == 0) return 0;
            return stylizer.PaintAssetsIfNeeded(terrains.ToArray());
        }
        finally
        {
            // Solo se cambiaron los TerrainData (ya guardados): la escena se cierra sin guardar
            if (opened) EditorSceneManager.CloseScene(scene, true);
        }
    }
}
