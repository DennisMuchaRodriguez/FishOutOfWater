using UnityEngine;
using UnityEngine.UI;

// Pantalla AJUSTES del menú principal (parte de MainMenu): el sonido.
//  VOLUMEN GENERAL, MÚSICA y EFECTOS (AudioSettingsUI); RESTABLECER vuelve a los valores de fábrica.
//  Se guarda solo; VOLVER (o Esc) regresa al título. Los mismos ajustes están en la pausa del juego.
public partial class MainMenu
{
    CanvasGroup settingsScreen;
    AudioSettingsUI.Panel settingsPanel;

    CanvasGroup BuildSettingsScreen()
    {
        RectTransform s = MenuUI.Stretch("Ajustes", root);
        CanvasGroup g = s.gameObject.AddComponent<CanvasGroup>();
        Vector2 top = new Vector2(0.5f, 1f), center = new Vector2(0.5f, 0.5f);

        MenuUI.Title("Encabezado", s, "AJUSTES", 72f, top, new Vector2(0f, -76f), new Vector2(800f, 90f), MenuUI.Cream);
        MenuUI.Rule(s, top, new Vector2(0f, -128f), 460f, MenuUI.Accent);
        MenuUI.Text("Sub", s, "LOS CAMBIOS SE OYEN AL MOMENTO Y SE GUARDAN SOLOS", 15f, top, new Vector2(0f, -150f), new Vector2(900f, 22f), MenuUI.TextDim)
            .characterSpacing = 4f;

        // Ficha SONIDO
        const float w = 640f, h = 390f;
        RectTransform card = MenuUI.HoloPanel("Sonido", s, center, new Vector2(0f, -20f), new Vector2(w, h),
                                              MenuUI.WithAlpha(Color.Lerp(MenuUI.Ink, MenuUI.Accent, 0.1f), 0.9f), MenuUI.WithAlpha(MenuUI.Accent, 0.85f),
                                              MenuUI.WithAlpha(MenuUI.Accent, 0.16f), MenuUI.WithAlpha(MenuUI.Accent, 0.22f), 0.05f);
        float left = -w * 0.5f + 34f;
        MenuUI.Img("HaloIcono", card, MenuUI.Glow, MenuUI.WithAlpha(MenuUI.Accent, 0.35f), top, new Vector2(left + 12f, -40f), new Vector2(46f, 46f));
        MenuUI.Img("Icono", card, MenuUI.Speaker, MenuUI.Cream, top, new Vector2(left + 12f, -40f), new Vector2(26f, 26f));
        TMPro.TextMeshProUGUI title = MenuUI.Text("Titulo", card, "SONIDO", 26f, top, new Vector2(left + 36f + 110f, -40f), new Vector2(220f, 32f), MenuUI.Cream, true);
        title.alignment = TMPro.TextAlignmentOptions.MidlineLeft;
        title.characterSpacing = 3f;
        MenuUI.Img("Linea", card, MenuUI.Pixel, MenuUI.WithAlpha(MenuUI.Accent, 0.45f), top, new Vector2(0f, -66f), new Vector2(w - 60f, 1.5f));
        MenuUI.Img("Rombo", card, MenuUI.Diamond, MenuUI.Accent, top, new Vector2(left, -66f), new Vector2(8f, 8f));

        settingsPanel = AudioSettingsUI.Build(card, center, new Vector2(0f, -30f), w - 90f);

        MenuUI.ComicButton("VOLVER", s, new Vector2(0.5f, 0f), new Vector2(-130f, 52f), new Vector2(220f, 54f), MenuUI.BlueDark, CloseSettings, 24f);
        MenuUI.ComicButton("RESTABLECER", s, new Vector2(0.5f, 0f), new Vector2(130f, 52f), new Vector2(240f, 48f), MenuUI.Blue,
                           () => AudioSettingsUI.ResetDefaults(settingsPanel), 20f);
        return g;
    }

    void CloseSettings()
    {
        PlayerPrefs.Save();
        Go(titleScreen);
    }
}
