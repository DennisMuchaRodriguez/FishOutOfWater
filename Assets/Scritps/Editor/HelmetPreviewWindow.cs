using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

// ============================================================================
//  VISTA PREVIA DEL CASCO EN EL EDITOR
//  Menú Herramientas > Casco > Vista previa del casco: abre esta ventana y arma el visor del casco tal como se ve
//  en el juego (marco, cristal, munición, propulsor, traje, misión, retícula y textos), sin darle Play.
//  Se mira en la ventana Game.
//
//  Cómo funciona: crea una COPIA del canvas del casco ("Casco (vista previa)") y un jugador de ejemplo invisible
//  con los valores de esta ventana (traje, munición, peces...). Ninguno se guarda en la escena (HideFlags.DontSave)
//  y se quitan solos antes de guardar, antes de darle Play, al recompilar y al cerrar la escena (y vuelven después).
//  Tu canvas real y su panel viejo del HUD (hudPanel) no se tocan: se ven y se editan como siempre.
//
//  Para ajustar: cambia los valores del HelmetVisorHUD de la escena en el Inspector (colores, tamaño del visor,
//  opacidades, retícula...) y la vista previa se regenera sola en un momento.
// ============================================================================
[InitializeOnLoad]
public class HelmetPreviewWindow : EditorWindow
{
    const string MenuOpen = "Herramientas/Casco/Vista previa del casco";
    const string MenuHide = "Herramientas/Casco/Quitar vista previa";
    const string CopyName = "Casco (vista previa · no se guarda)";
    const string StandInName = "Casco · jugador de ejemplo";

    static HelmetVisorHUD copy;
    static PlayerController_Base standIn;
    static PlayerShooting standInShooting;
    static double lastTick;
    static double rebuildAt = -1;
    static System.Type gameViewType;

    static HelmetPreviewWindow()
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
        AssemblyReloadEvents.beforeAssemblyReload -= Teardown;
        AssemblyReloadEvents.beforeAssemblyReload += Teardown;
        EditorApplication.update -= Tick;
        EditorApplication.update += Tick;
        HelmetVisorHUD.EditorValidated -= OnVisorChanged;
        HelmetVisorHUD.EditorValidated += OnVisorChanged;

        // Después de recompilar se vuelve a armar (si estaba encendida)
        EditorApplication.delayCall += () =>
        {
            if (HelmetPreviewState.instance.on) Show(false);
        };
    }

    // ======================= MENÚ =======================

    [MenuItem(MenuOpen, false, 120)]
    static void OpenMenu()
    {
        HelmetPreviewWindow w = GetWindow<HelmetPreviewWindow>(false, "Vista previa del casco", true);
        w.minSize = new Vector2(320f, 460f);
        if (Show(true)) FocusGameView();
    }

    [MenuItem(MenuOpen, true)]
    static bool OpenValidate()
    {
        return !EditorApplication.isPlayingOrWillChangePlaymode;
    }

    [MenuItem(MenuHide, false, 121)]
    static void HideMenu()
    {
        Remove();
    }

    [MenuItem(MenuHide, true)]
    static bool HideValidate()
    {
        return HelmetPreviewState.instance.on && !EditorApplication.isPlayingOrWillChangePlaymode;
    }

    // ======================= VENTANA =======================

    void OnGUI()
    {
        HelmetPreviewState s = HelmetPreviewState.instance;
        EditorGUILayout.HelpBox("Copia del casco que NO se guarda en la escena. Mírala en la ventana Game.\n" +
                                "Cambia los valores del HelmetVisorHUD de la escena (Inspector) y se regenera sola.",
                                MessageType.Info);
        if (EditorApplication.isPlayingOrWillChangePlaymode)
        {
            EditorGUILayout.HelpBox("En Play se ve el casco real del juego.", MessageType.None);
            return;
        }

        using (new EditorGUILayout.HorizontalScope())
        {
            if (GUILayout.Button(s.on ? "Regenerar" : "Mostrar", GUILayout.Height(24f)))
            {
                if (Show(true)) FocusGameView();
            }
            using (new EditorGUI.DisabledScope(!s.on))
            {
                if (GUILayout.Button("Quitar", GUILayout.Height(24f))) Remove();
            }
        }

        EditorGUILayout.Space(6f);
        EditorGUI.BeginChangeCheck();
        EditorGUILayout.LabelField("Jugador", EditorStyles.boldLabel);
        s.suit01 = EditorGUILayout.Slider("Traje", s.suit01, 0f, 1f);
        s.fuel01 = EditorGUILayout.Slider("Propulsor", s.fuel01, 0f, 1f);
        int maxAmmo = standInShooting != null ? Mathf.Max(1, standInShooting.maxAmmo) : 30;
        s.ammo = EditorGUILayout.IntSlider("Munición", s.ammo, 0, maxAmmo);
        s.inWater = EditorGUILayout.Toggle("En el agua", s.inWater);
        s.underwater = EditorGUILayout.Toggle("Cámara bajo el agua", s.underwater);
        s.thirdPerson = EditorGUILayout.Toggle("Tercera persona", s.thirdPerson);

        EditorGUILayout.Space(6f);
        EditorGUILayout.LabelField("Misión", EditorStyles.boldLabel);
        s.totalFish = EditorGUILayout.IntSlider("Peces en total", s.totalFish, 1, 60);
        s.fishAlive = EditorGUILayout.IntSlider("Peces a salvo", s.fishAlive, 0, s.totalFish);
        s.lossFraction = EditorGUILayout.Slider(new GUIContent("Se pierde al perder", "Fracción de peces cazados con la que se pierde"),
                                                s.lossFraction, 0.1f, 1f);
        s.totalWaves = EditorGUILayout.IntSlider("Oleadas", s.totalWaves, 1, 10);
        s.wave = EditorGUILayout.IntSlider("Oleada actual", s.wave, 0, s.totalWaves);
        s.birds = EditorGUILayout.IntSlider("Depredadores", s.birds, 0, 20);
        s.defendersTotal = EditorGUILayout.IntSlider("Defensores (total)", s.defendersTotal, 0, 8);
        s.defenders = EditorGUILayout.IntSlider("Defensores con traje", s.defenders, 0, s.defendersTotal);
        s.waiting = EditorGUILayout.Toggle("Entre oleadas", s.waiting);
        if (s.waiting) s.countdown = EditorGUILayout.Slider("Cuenta atrás (s)", s.countdown, 0f, 30f);
        if (EditorGUI.EndChangeCheck())
        {
            ApplySample();
            RepaintGame();
        }

        EditorGUILayout.Space(6f);
        EditorGUILayout.LabelField("Probar efectos", EditorStyles.boldLabel);
        using (new EditorGUI.DisabledScope(copy == null))
        using (new EditorGUILayout.HorizontalScope())
        {
            if (GUILayout.Button("Encendido")) copy.PreviewBoot();
            if (GUILayout.Button("Golpe")) copy.PreviewHit();
            if (GUILayout.Button("Gotas")) copy.PreviewDroplets();
            if (GUILayout.Button("Mancha")) copy.PreviewSplat();
        }

        GUILayout.FlexibleSpace();
        EditorGUILayout.HelpBox("Las piezas del casco se arman por código: para moverlas o cambiar colores y tamaños usa " +
                                "los valores del HelmetVisorHUD. Si tocas la copia \"Casco (vista previa)\" directamente, " +
                                "esos cambios se pierden al regenerar. Los misiles, el escudo y el sonar solo salen en Play " +
                                "(dependen de las mejoras compradas).", MessageType.None);
    }

    // ======================= MOSTRAR Y QUITAR =======================

    // fromUser: muestra los avisos (si se llama sola, por ejemplo al volver de Play, no molesta)
    static bool Show(bool fromUser)
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) return false;
        HelmetVisorHUD real = FindReal();
        if (real == null)
        {
            if (fromUser)
                EditorUtility.DisplayDialog("Vista previa del casco",
                    "Esta escena no tiene el visor del casco (HelmetVisorHUD). Abre SampleScene y vuelve a intentarlo.", "Entendido");
            return false;
        }
        HelmetPreviewState.instance.on = true;
        Teardown();
        Build(real);
        return copy != null;
    }

    static void Remove()
    {
        Teardown();
        HelmetPreviewState.instance.on = false;
        RepaintGame();
    }

    // El visor de la escena (nunca la copia)
    static HelmetVisorHUD FindReal()
    {
        foreach (HelmetVisorHUD h in Object.FindObjectsByType<HelmetVisorHUD>(FindObjectsInactive.Exclude, FindObjectsSortMode.None))
        {
            if (h == null || h.previewMode || EditorUtility.IsPersistent(h)) continue;
            if ((h.gameObject.hideFlags & HideFlags.DontSaveInEditor) != 0) continue;
            return h;
        }
        return null;
    }

    static void Build(HelmetVisorHUD real)
    {
        try
        {
            // 1. Jugador de ejemplo: solo guarda datos (sus scripts no corren fuera de Play). Toma los valores del
            //    jugador de la escena (máximos de traje, propulsor y munición) y se pone donde está él.
            PlayerController_Base realPlayer = real.player != null ? real.player : Object.FindFirstObjectByType<PlayerController_Base>();
            PlayerShooting realShooting = real.shooting != null ? real.shooting : (realPlayer != null ? realPlayer.GetComponent<PlayerShooting>() : null);
            GameObject sgo = new GameObject(StandInName);
            sgo.hideFlags = HideFlags.HideAndDontSave;
            if (realPlayer != null) sgo.transform.SetPositionAndRotation(realPlayer.transform.position, realPlayer.transform.rotation);
            standIn = sgo.AddComponent<PlayerController_Base>();
            if (realPlayer != null) EditorUtility.CopySerialized(realPlayer, standIn);
            standInShooting = sgo.AddComponent<PlayerShooting>();
            if (realShooting != null) EditorUtility.CopySerialized(realShooting, standInShooting);
            ApplySample();

            // 2. Copia del canvas del casco, con los mismos valores que el de la escena. Se dibuja justo debajo del
            //    canvas real, así el panel viejo del HUD (que sigue siendo el de la escena) queda encima del marco.
            GameObject go = new GameObject(CopyName, typeof(RectTransform));
            go.hideFlags = HideFlags.DontSave;
            go.layer = real.gameObject.layer;
            SceneManager.MoveGameObjectToScene(go, real.gameObject.scene);
            Canvas canvas = go.AddComponent<Canvas>();
            Canvas realCanvas = real.GetComponent<Canvas>();
            if (realCanvas != null)
            {
                EditorUtility.CopySerialized(realCanvas, canvas);
                canvas.sortingOrder = realCanvas.sortingOrder - 1;
            }
            CanvasScaler realScaler = real.GetComponent<CanvasScaler>();
            if (realScaler != null)
            {
                CanvasScaler scaler = go.AddComponent<CanvasScaler>();
                EditorUtility.CopySerialized(realScaler, scaler);
            }
            copy = go.AddComponent<HelmetVisorHUD>();
            EditorUtility.CopySerialized(real, copy);
            copy.player = standIn;
            copy.shooting = standInShooting;
            copy.hudPanel = null;
            copy.BuildPreview();
            MarkDontSave(go);
            ApplySample();
            copy.PreviewTick(0f);
        }
        catch (System.Exception e)
        {
            Debug.LogException(e);
            Debug.LogWarning("Vista previa del casco: no se pudo armar; se quitó para no dejar nada a medias.");
            Teardown();
        }
        lastTick = EditorApplication.timeSinceStartup;
        RepaintGame();
    }

    static void MarkDontSave(GameObject root)
    {
        foreach (Transform t in root.GetComponentsInChildren<Transform>(true))
            t.gameObject.hideFlags = HideFlags.DontSave;
    }

    // Borra la copia y el jugador de ejemplo (también los que quedaron de antes de recompilar)
    static void Teardown()
    {
        foreach (HelmetVisorHUD h in Resources.FindObjectsOfTypeAll<HelmetVisorHUD>())
        {
            if (h == null || EditorUtility.IsPersistent(h)) continue;
            if ((h.gameObject.hideFlags & HideFlags.DontSaveInEditor) == 0) continue;   // nunca el de la escena
            if (!h.previewMode && h.gameObject.name != CopyName) continue;
            h.previewMode = true;
            h.DestroyPreviewTextures();
            Object.DestroyImmediate(h.gameObject);
        }
        foreach (PlayerController_Base p in Resources.FindObjectsOfTypeAll<PlayerController_Base>())
        {
            if (p == null || EditorUtility.IsPersistent(p)) continue;
            if (p.gameObject.name == StandInName && (p.gameObject.hideFlags & HideFlags.DontSaveInEditor) != 0)
                Object.DestroyImmediate(p.gameObject);
        }
        copy = null;
        standIn = null;
        standInShooting = null;
    }

    // ======================= VALORES DE EJEMPLO =======================

    static void ApplySample()
    {
        HelmetPreviewState s = HelmetPreviewState.instance;
        if (standIn != null)
        {
            standIn.isDead = false;
            standIn.isThirdPerson = s.thirdPerson;
            standIn.isInWater = s.inWater;
            standIn.currentArmor = standIn.maxArmor * s.suit01;
            standIn.currentJetpackEnergy = standIn.maxJetpackEnergy * s.fuel01;
        }
        if (standInShooting != null)
        {
            standInShooting.enabled = true;
            standInShooting.currentAmmo = Mathf.Clamp(s.ammo, 0, Mathf.Max(1, standInShooting.maxAmmo));
        }
        if (copy == null) return;
        copy.previewUnderwater = s.underwater;
        VisorMissionHUD mission = copy.MissionHud;
        if (mission != null)
        {
            int total = Mathf.Max(1, s.totalFish);
            int waves = Mathf.Max(1, s.totalWaves);
            mission.previewSample = new VisorMissionHUD.Sample
            {
                totalFish = total,
                fishAlive = Mathf.Clamp(s.fishAlive, 0, total),
                maxFishLoss = Mathf.Clamp(Mathf.CeilToInt(total * s.lossFraction), 1, total),
                wave = Mathf.Clamp(s.wave, 0, waves),
                totalWaves = waves,
                birds = Mathf.Max(0, s.birds),
                defenders = Mathf.Clamp(s.defenders, 0, Mathf.Max(0, s.defendersTotal)),
                defendersTotal = Mathf.Max(0, s.defendersTotal),
                countdown = s.waiting ? s.countdown : 0f,
                waveActive = !s.waiting,
                waiting = s.waiting
            };
        }
    }

    // ======================= ANIMACIÓN =======================

    static void Tick()
    {
        if (!HelmetPreviewState.instance.on || EditorApplication.isPlayingOrWillChangePlaymode) return;
        double now = EditorApplication.timeSinceStartup;
        if (rebuildAt > 0 && now >= rebuildAt)
        {
            rebuildAt = -1;
            Show(false);
            return;
        }
        if (copy == null || now - lastTick < 1.0 / 30.0) return;
        float dt = Mathf.Min((float)(now - lastTick), 0.1f);
        lastTick = now;
        ApplySample();
        try
        {
            copy.PreviewTick(dt);
        }
        catch (System.Exception e)
        {
            Debug.LogException(e);
            Debug.LogWarning("Vista previa del casco: falló al animarse y se apagó.");
            Remove();
            return;
        }
        RepaintGame();
    }

    // Cambiaste algo del HelmetVisorHUD en el Inspector: se regenera en un momento (no con cada tecla)
    static void OnVisorChanged(HelmetVisorHUD h)
    {
        if (!HelmetPreviewState.instance.on || EditorApplication.isPlayingOrWillChangePlaymode) return;
        rebuildAt = EditorApplication.timeSinceStartup + 0.3;
    }

    static void RepaintGame()
    {
        EditorApplication.QueuePlayerLoopUpdate();
        if (gameViewType == null) gameViewType = typeof(EditorWindow).Assembly.GetType("UnityEditor.GameView");
        if (gameViewType == null) return;
        foreach (Object w in Resources.FindObjectsOfTypeAll(gameViewType))
        {
            EditorWindow ew = w as EditorWindow;
            if (ew != null) ew.Repaint();
        }
    }

    static void FocusGameView()
    {
        EditorApplication.ExecuteMenuItem("Window/General/Game");
    }

    // ======================= EVENTOS DEL EDITOR =======================

    static void OnPlayModeChanged(PlayModeStateChange change)
    {
        if (!HelmetPreviewState.instance.on) return;
        // Al jugar se quita (el juego arma su propio casco) y al volver se arma otra vez
        if (change == PlayModeStateChange.ExitingEditMode) Teardown();
        else if (change == PlayModeStateChange.EnteredEditMode) EditorApplication.delayCall += () => Show(false);
    }

    static void OnSceneSaving(Scene scene, string path)
    {
        // Nada de la vista previa llega al archivo de la escena
        if (HelmetPreviewState.instance.on) Teardown();
    }

    static void OnSceneSaved(Scene scene)
    {
        if (HelmetPreviewState.instance.on && !EditorApplication.isPlayingOrWillChangePlaymode)
            EditorApplication.delayCall += () => Show(false);
    }

    static void OnSceneClosing(Scene scene, bool removingScene)
    {
        if (HelmetPreviewState.instance.on) Teardown();
    }

    static void OnSceneOpened(Scene scene, OpenSceneMode mode)
    {
        if (!HelmetPreviewState.instance.on || EditorApplication.isPlayingOrWillChangePlaymode) return;
        EditorApplication.delayCall += () => Show(false);
    }
}
