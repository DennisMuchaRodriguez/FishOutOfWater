using UnityEngine;
using UnityEngine.EventSystems;

// Pequeña animación al pasar el mouse por un botón
public class MenuButtonFX : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, ISelectHandler, IDeselectHandler
{
    bool hover;

    public void OnPointerEnter(PointerEventData e) { hover = true; }
    public void OnPointerExit(PointerEventData e) { hover = false; }
    public void OnSelect(BaseEventData e) { hover = true; }
    public void OnDeselect(BaseEventData e) { hover = false; }

    void Update()
    {
        float target = hover ? 1.06f : 1f;
        float s = Mathf.Lerp(transform.localScale.x, target, 1f - Mathf.Exp(-14f * Time.unscaledDeltaTime));
        transform.localScale = new Vector3(s, s, 1f);
    }
}
