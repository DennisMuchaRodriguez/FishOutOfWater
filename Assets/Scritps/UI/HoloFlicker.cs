using UnityEngine;
using UnityEngine.UI;
using TMPro;

// Parpadeo de holograma para los títulos de los menús (lo agrega MenuUI.Title):
// el halo respira y, cada pocos segundos, el texto falla un instante y las copias cian y roja se separan.
// Usa tiempo real: funciona con el juego en pausa.
public class HoloFlicker : MonoBehaviour
{
    TextMeshProUGUI main, cyan, red;
    Image glow;
    Color mainColor, cyanColor, redColor, glowColor;
    Vector2 cyanPos, redPos;
    float size;
    float nextGlitch, glitchTimer;
    float phase;

    public void Setup(TextMeshProUGUI mainText, TextMeshProUGUI cyanText, TextMeshProUGUI redText, Image glowImage, float fontSize)
    {
        main = mainText;
        cyan = cyanText;
        red = redText;
        glow = glowImage;
        size = fontSize;
        if (main != null) mainColor = main.color;
        if (cyan != null) { cyanColor = cyan.color; cyanPos = cyan.rectTransform.anchoredPosition; }
        if (red != null) { redColor = red.color; redPos = red.rectTransform.anchoredPosition; }
        if (glow != null) glowColor = glow.color;
        phase = Random.Range(0f, 10f);
        nextGlitch = Time.unscaledTime + Random.Range(1.5f, 4f);
    }

    void OnDisable()
    {
        // Al ocultar la pantalla queda limpio (sin quedar a medio parpadeo)
        glitchTimer = 0f;
        Apply(1f, 0f, 1f);
    }

    void Update()
    {
        if (main == null) return;
        float time = Time.unscaledTime;
        float dt = Time.unscaledDeltaTime;

        if (time >= nextGlitch)
        {
            glitchTimer = Random.Range(0.06f, 0.16f);
            nextGlitch = time + Random.Range(2.5f, 6f);
        }
        float alpha = 1f, split = 0f;
        if (glitchTimer > 0f)
        {
            glitchTimer -= dt;
            alpha = Random.value > 0.5f ? 0.55f : 0.9f;
            split = Random.Range(-1f, 1f) * size * 0.03f;
        }
        float breathe = 0.85f + 0.15f * Mathf.Sin(time * 1.7f + phase);
        Apply(alpha, split, breathe);
    }

    void Apply(float alpha, float split, float breathe)
    {
        if (main != null) main.color = new Color(mainColor.r, mainColor.g, mainColor.b, mainColor.a * alpha);
        if (cyan != null)
        {
            cyan.rectTransform.anchoredPosition = cyanPos + new Vector2(-Mathf.Abs(split), 0f);
            cyan.color = new Color(cyanColor.r, cyanColor.g, cyanColor.b, cyanColor.a * (split != 0f ? 1.8f : 1f));
        }
        if (red != null)
        {
            red.rectTransform.anchoredPosition = redPos + new Vector2(Mathf.Abs(split), 0f);
            red.color = new Color(redColor.r, redColor.g, redColor.b, redColor.a * (split != 0f ? 1.8f : 1f));
        }
        if (glow != null) glow.color = new Color(glowColor.r, glowColor.g, glowColor.b, glowColor.a * breathe * alpha);
    }
}
