using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;

// Animación de los botones holográficos: al pasar el mouse (o seleccionarlo con el teclado) el halo
// brilla más y el botón crece un poco; al hacer clic se "hunde" un instante.
public class MenuButtonFX : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, ISelectHandler, IDeselectHandler, IPointerDownHandler
{
    bool hover;
    float punch;
    float glowK;
    Image glow;          // el halo del panel ("Brillo"), si tiene
    float glowBase;

    void Awake()
    {
        Transform g = transform.Find("Brillo");
        if (g != null) glow = g.GetComponent<Image>();
        if (glow != null) glowBase = glow.color.a;
    }

    public void OnPointerEnter(PointerEventData e) { hover = true; }
    public void OnPointerExit(PointerEventData e) { hover = false; }
    public void OnSelect(BaseEventData e) { hover = true; }
    public void OnDeselect(BaseEventData e) { hover = false; }
    public void OnPointerDown(PointerEventData e) { punch = 1f; }

    void OnDisable()
    {
        hover = false;
        punch = 0f;
        glowK = 0f;
        transform.localScale = Vector3.one;
        SetGlow(0f);
    }

    void Update()
    {
        float dt = Time.unscaledDeltaTime;
        punch = Mathf.MoveTowards(punch, 0f, dt * 6f);
        float target = (hover ? 1.04f : 1f) - punch * 0.05f;
        float s = Mathf.Lerp(transform.localScale.x, target, 1f - Mathf.Exp(-16f * dt));
        transform.localScale = new Vector3(s, s, 1f);

        glowK = Mathf.Lerp(glowK, hover ? 1f : 0f, 1f - Mathf.Exp(-14f * dt));
        SetGlow(glowK + punch * 0.6f);
    }

    void SetGlow(float k)
    {
        if (glow == null) return;
        Color c = glow.color;
        c.a = Mathf.Clamp01(glowBase * (1f + 1.2f * k) + 0.12f * k);
        glow.color = c;
    }
}
