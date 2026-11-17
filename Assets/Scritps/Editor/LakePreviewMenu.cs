using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

// ============================================================================
//  VISTA PREVIA DEL LAGO EN EL EDITOR
//  Menú Herramientas > Lago:
//   - "Vista previa en el editor": genera la vegetación (Decoracion_Lago) y aplica el cielo, la niebla y la luz
//     de LakeAmbience para verlos sin darle Play.
//   - "Regenerar vista previa": la vuelve a generar (por ejemplo, después de cambiar Decoracion_Lago).
//   - "Quitar vista previa": la borra y deja el cielo y la luz como estaban.
//  La vista previa NO se guarda en la escena: la decoración se crea con HideFlags.DontSave y el cielo, la niebla y
//  el sol se devuelven a sus valores originales antes de guardar, antes de darle Play y al cerrar la escena (y se
//  vuelven a aplicar después). Al darle Play se esconde y el juego genera su propia decoración, como siempre.
//  Las hojas, el polen y los efectos bajo el agua solo se ven en Play.
// ============================================================================
[InitializeOnLoad]
public static class LakePreviewMenu
{
    const string MenuShow = "Herramientas/Lago/Vista previa en el editor";
    const string MenuRegen = "Herramientas/Lago/Regenerar vista previa";
    const string MenuHide = "Herramientas/Lago/Quitar vista previa";
    const string RootName = "Decoracion (vista previa)";

    static LakePreviewMenu()
    {
        EditorApplication.playModeStateChanged -= OnPlayModeChanged;
        EditorApplication.playModeStateChanged += OnPlayModeChanged;
        EditorSceneManager.sceneSaving -= OnSceneSaving;
        EditorSceneManager.sceneSaving += OnSceneSaving;
        EditorSceneManager.sceneSaved -= OnSceneSaved;
        EditorSceneManager.sceneSaved += OnSceneSaved;
        EditorSceneManager.sceneClosing -= OnSceneClosing;
        EditorSceneManager.sceneClosing += OnSceneClosing;
        EditorSceneManager.sceneOpened -= OnSceneOpened;
        EditorSceneManager.sceneOpened += OnSceneOpened;
    }

    // ======================= MENÚ =======================

    [MenuItem(MenuShow, false, 100)]
    static void ShowMenu()
    {
        if (!Show(true)) return;
        Menu.SetChecked(MenuShow, true);
    }

    [MenuItem(MenuShow, true)]
    static bool ShowValidate()
    {
        Menu.SetChecked(MenuShow, LakePreviewState.instance.on);
        return !EditorApplication.isPlayingOrWillChangePlaymode;
    }

    [MenuItem(MenuRegen, false, 101)]
    static void RegenMenu()
    {
        DestroyRoot();
        Show(true);
    }

    [MenuItem(MenuRegen, true)]
    static bool RegenValidate()
    {
        return !EditorApplication.isPlayingOrWillChangePlaymode && LakePreviewState.instance.on;
    }

    [MenuItem(MenuHide, false, 102)]
    static void HideMenu()
    {
        Remove();
        LakePreviewState.instance.on = false;
        Menu.SetChecked(MenuShow, false);
        SceneView.RepaintAll();
    }

    [MenuItem(MenuHide, true)]
    static bool HideValidate()
    {
        return !EditorApplication.isPlayingOrWillChangePlaymode && LakePreviewState.instance.on;
    }

    // ======================= MOSTRAR Y QUITAR =======================

    // fromUser: muestra los avisos (si se llama sola, por ejemplo al volver de Play, no molesta)
    static bool Show(bool fromUser)
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) return false;
        LakeWater water = Object.FindFirstObjectByType<LakeWater>();
        if (water == null)
        {
            if (fromUser)
                EditorUtility.DisplayDialog("Vista previa del lago",
                    "Esta escena no tiene agua del lago (LakeWater). Abre SampleScene y vuelve a intentarlo.", "Entendido");
            return false;
        }
        LakePreviewState state = LakePreviewState.instance;
        state.on = true;

        // ---- Vegetación ----
        if (FindRoot() == null)
        {
            if (HasSavedDecoration())
            {
                if (fromUser)
                    Debug.Log("Vista previa del lago: la escena ya tiene decoración guardada (LakeDecorator con hijos); " +
                              "solo se aplica el cielo y la luz.");
            }
            else
            {
                DecorationSet set = Resources.Load<DecorationSet>(LakeDecorator.ResourcePath);
                if (set == null)
                {
                    Debug.LogWarning("Vista previa del lago: falta Resources/" + LakeDecorator.ResourcePath + ".");
                }
                else
                {
                    GameObject root = new GameObject(RootName);
                    root.hideFlags = HideFlags.DontSave;
                    SceneManager.MoveGameObjectToScene(root, water.gameObject.scene);
                    LakeDecorator d = root.AddComponent<LakeDecorator>();
                    d.previewOnly = true;
                    d.set = set;
                    d.water = water;
                    d.generateOnStart = false;
                    try
                    {
                        EditorUtility.DisplayProgressBar("Vista previa del lago", "Colocando árboles y plantas...", 0.5f);
                        d.Generate();
                    }
                    finally
                    {
                        EditorUtility.ClearProgressBar();
                    }
                    // Nada de esto se guarda en la escena
                    foreach (Transform t in root.GetComponentsInChildren<Transform>(true))
                        t.gameObject.hideFlags = HideFlags.DontSave;
                    if (fromUser && !string.IsNullOrEmpty(d.LastReport))
                        Debug.Log("Vista previa del lago (no se guarda en la escena): " + d.LastReport);
                }
            }
        }
        else
        {
            SetRootActive(true);
        }

        // ---- Cielo, niebla, luz y sol ----
        ApplyLighting();
        SceneView.RepaintAll();
        return true;
    }

    static void Remove()
    {
        DestroyRoot();
        RestoreLighting();
    }

    static GameObject FindRoot()
    {
        foreach (LakeDecorator d in Resources.FindObjectsOfTypeAll<LakeDecorator>())
        {
            if (d == null || EditorUtility.IsPersistent(d)) continue;
            if (d.gameObject.name == RootName && (d.gameObject.hideFlags & HideFlags.DontSaveInEditor) != 0)
                return d.gameObject;
        }
        return null;
    }

    static void DestroyRoot()
    {
        GameObject root = FindRoot();
        int guard = 0;
        while (root != null && guard++ < 8)
        {
            Object.DestroyImmediate(root);
            root = FindRoot();
        }
    }

    static void SetRootActive(bool active)
    {
        GameObject root = FindRoot();
        if (root != null && root.activeSelf != active) root.SetActive(active);
    }

    // ¿La escena ya tiene una decoración generada y guardada? (entonces no se duplica)
    static bool HasSavedDecoration()
    {
        foreach (LakeDecorator d in Object.FindObjectsByType<LakeDecorator>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            if ((d.gameObject.hideFlags & HideFlags.DontSaveInEditor) != 0) continue;
            if (d.transform.childCount > 0) return true;
        }
        return false;
    }

    // ======================= CIELO Y LUZ =======================

    static void ApplyLighting()
    {
        LakePreviewState state = LakePreviewState.instance;
        if (!state.hasSaved) state.Save(FindSun());

        // Usa el LakeAmbience de la escena si lo agregaste a mano; si no, uno temporal con los valores de fábrica
        LakeAmbience amb = Object.FindFirstObjectByType<LakeAmbience>();
        GameObject temp = null;
        if (amb == null)
        {
            temp = new GameObject("AmbienteTemporal");
            temp.hideFlags = HideFlags.HideAndDontSave;
            amb = temp.AddComponent<LakeAmbience>();
        }
        amb.Apply();
        if (temp != null) Object.DestroyImmediate(temp);
    }

    static void RestoreLighting()
    {
        LakePreviewState state = LakePreviewState.instance;
        if (!state.hasSaved) return;
        state.Restore();
        DynamicGI.UpdateEnvironment();
    }

    static Light FindSun()
    {
        if (RenderSettings.sun != null) return RenderSettings.sun;
        Light best = null;
        foreach (Light l in Object.FindObjectsByType<Light>(FindObjectsSortMode.None))
        {
            if (l.type != LightType.Directional || !l.isActiveAndEnabled) continue;
            if (best == null || l.intensity > best.intensity) best = l;
        }
        return best;
    }

    // ======================= EVENTOS DEL EDITOR =======================

    static void OnPlayModeChanged(PlayModeStateChange change)
    {
        LakePreviewState state = LakePreviewState.instance;
        if (!state.on) return;
        if (change == PlayModeStateChange.ExitingEditMode)
        {
            // Al jugar: la vista previa se esconde (el juego genera la suya) y la luz vuelve a la de la escena
            SetRootActive(false);
            RestoreLighting();
        }
        else if (change == PlayModeStateChange.EnteredEditMode)
        {
            EditorApplication.delayCall += () => Show(false);
        }
    }

    static void OnSceneSaving(Scene scene, string path)
    {
        // Lo que se guarda es la luz original de la escena (la vegetación de la vista previa nunca se guarda)
        if (LakePreviewState.instance.on) RestoreLighting();
    }

    static void OnSceneSaved(Scene scene)
    {
        if (LakePreviewState.instance.on && !EditorApplication.isPlayingOrWillChangePlaymode) ApplyLighting();
    }

    static void OnSceneClosing(Scene scene, bool removingScene)
    {
        if (!LakePreviewState.instance.on) return;
        // Los objetos DontSave sobreviven al cambio de escena: se borran aquí para no arrastrarlos a otra
        DestroyRoot();
        RestoreLighting();
    }

    static void OnSceneOpened(Scene scene, OpenSceneMode mode)
    {
        if (!LakePreviewState.instance.on || EditorApplication.isPlayingOrWillChangePlaymode) return;
        EditorApplication.delayCall += () => Show(false);
    }
}
