using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections;

public class UI_PlayerStatus : MonoBehaviour
{
    [Header("Referencias al jugador")]
    public PlayerController_Base playerController;
    public PlayerShooting playerShooting;

    [Header("UI Elementos")]
    public GameObject uiPanel;

    [Header("Jetpack")]
    public Slider jetpackSlider;
    public TextMeshProUGUI jetpackText;
    public Image jetpackIcon;
    public Image jetpackBackGround;
    public Image jetpackSliderImage;
    [Header("Munición")]
    public TextMeshProUGUI ammoText;
    public Image ammoIcon;
    public Color ammoEmptyColor = Color.red;

    [Header("Configuración de Parpadeo - NUEVO")]
    public float blinkSpeed = 5f;
    public float lowAmmoThreshold = 5;
    [Tooltip("La munición ahora se ve en el visor del casco (VisorAmmoHUD). Activa esto solo para volver al contador viejo")]
    public bool useOldAmmoCounter = false;
    [Tooltip("El propulsor y la armadura ahora se ven en el visor del casco (VisorStatusHUD). Activa esto solo para volver a las barras viejas")]
    public bool useOldBars = false;
    public float lowEnergyThreshold = 25f;

    [Header("Armadura - NUEVO")]
    public Slider armorSlider;
    public Image armorFillImage;
    public Image armorIcon;
    public TextMeshProUGUI armorText;
    public Image crackOverlay;
    public Image redFilter;
    public Color armorLowColor = Color.red;
    public float armorLowThreshold = 25f;
    public float redFilterIntensity = 0.3f;

    private bool isArmorBlinking = false;
    private Coroutine armorBlinkCoroutine;
    private Coroutine armorDamageFlashCoroutine;
    private Color originalArmorColor;
    private Color originalArmorTextColor;
    private Color originalArmorIconColor;
    private bool isAmmoBlinking = false;
    private bool isEnergyBlinking = false;
    private Coroutine ammoBlinkCoroutine;
    private Coroutine energyBlinkCoroutine;
    private Coroutine damageFlashCoroutine;
    private Color originalAmmoColor;
    private Color originalAmmoTextColor;
    private Color originalEnergyColor;
    private Color originalEnergyTextColor;

    [Header("Efecto UI Impacto - NUEVO")]
    public RectTransform uiPanelRect;
    public float uiShakeDuration = 0.15f;
    public float uiShakeMagnitude = 15f;
    private float uiShakeTimer = 0f;
    private Vector2 originalUIPosition;

    void Start()
    {
        if (playerController == null)
        {
            Debug.Log("FALTA ASIGNAR PLAYER CONTROLLER");
        }
        if (playerShooting == null)
        {
            Debug.Log("FALTA ASIGNAR PLAYER SHOOTING");
        }
        if (uiPanelRect != null)
            originalUIPosition = uiPanelRect.anchoredPosition;


        if (ammoIcon != null) originalAmmoColor = ammoIcon.color;
        if (ammoText != null) originalAmmoTextColor = ammoText.color;
        if (!useOldAmmoCounter)
        {
            if (ammoIcon != null) ammoIcon.gameObject.SetActive(false);
            if (ammoText != null) ammoText.gameObject.SetActive(false);
        }

        if (!useOldBars)
        {
            if (jetpackSlider != null) jetpackSlider.gameObject.SetActive(false);
            if (jetpackText != null) jetpackText.gameObject.SetActive(false);
            if (armorSlider != null) armorSlider.gameObject.SetActive(false);
            if (armorText != null) armorText.gameObject.SetActive(false);
        }

        if (jetpackIcon != null) originalEnergyColor = jetpackIcon.color;
        if (jetpackBackGround != null) originalEnergyColor = jetpackBackGround.color;
        if (jetpackSliderImage != null) originalEnergyColor = jetpackSliderImage.color;
        if (jetpackText != null) originalEnergyTextColor = jetpackText.color;

        if (armorFillImage != null) originalArmorColor = armorFillImage.color;
        if (armorText != null) originalArmorTextColor = armorText.color;
        if (armorIcon != null) originalArmorIconColor = armorIcon.color;


        if (crackOverlay != null) crackOverlay.gameObject.SetActive(false);
        if (redFilter != null)
        {
            Color filterColor = redFilter.color;
            filterColor.a = 0;
            redFilter.color = filterColor;
        }
    }

    void Update()
    {
        if (playerController == null || playerShooting == null || uiPanel == null)
        {
            return;
        }
        if (uiShakeTimer > 0 && uiPanelRect != null)
        {
            uiShakeTimer -= Time.deltaTime;
            uiPanelRect.anchoredPosition = originalUIPosition + Random.insideUnitCircle * uiShakeMagnitude;

            if (uiShakeTimer <= 0)
                uiPanelRect.anchoredPosition = originalUIPosition;
        }


        uiPanel.SetActive(!playerController.isThirdPerson);

        if (!playerController.isThirdPerson)
        {
            UpdateJetpackUI();
            if (useOldAmmoCounter) UpdateAmmoUI();
            UpdateArmorUI();
        }
    }

    void UpdateJetpackUI()
    {
        float porcentaje = playerController.currentJetpackEnergy / playerController.maxJetpackEnergy;

        if (jetpackSlider != null)
        {
            jetpackSlider.value = porcentaje;
        }

        if (jetpackText != null)
        {
            jetpackText.text = $"{Mathf.RoundToInt(porcentaje * 100)}%";
        }

        bool shouldBlink = porcentaje * 100 <= lowEnergyThreshold || playerController.currentJetpackEnergy <= 0;

        if (shouldBlink && !isEnergyBlinking)
        {
            if (energyBlinkCoroutine != null) StopCoroutine(energyBlinkCoroutine);
            energyBlinkCoroutine = StartCoroutine(BlinkEnergy());
        }
        else if (!shouldBlink && isEnergyBlinking)
        {
            if (energyBlinkCoroutine != null) StopCoroutine(energyBlinkCoroutine);
            isEnergyBlinking = false;

            if (jetpackIcon != null) jetpackIcon.color = originalEnergyColor;
            if (jetpackBackGround != null) jetpackBackGround.color = originalEnergyColor;
            if (jetpackSliderImage != null) jetpackSliderImage.color = originalEnergyColor;
            if (jetpackText != null) jetpackText.color = originalEnergyTextColor;
        }
    }
    void UpdateArmorUI()
    {
        float porcentaje = playerController.currentArmor / playerController.maxArmor;

        if (armorSlider != null)
        {
            armorSlider.value = porcentaje;
        }

        if (armorText != null)
        {
            armorText.text = $"{Mathf.RoundToInt(porcentaje * 100)}%";
        }

        bool shouldBlink = porcentaje * 100 <= armorLowThreshold || playerController.currentArmor <= 0;

        if (shouldBlink && !isArmorBlinking)
        {
            if (armorBlinkCoroutine != null) StopCoroutine(armorBlinkCoroutine);
            armorBlinkCoroutine = StartCoroutine(BlinkArmor());

            if (crackOverlay != null) crackOverlay.gameObject.SetActive(true);
            if (redFilter != null) StartCoroutine(PulseRedFilter());
        }
        else if (!shouldBlink && isArmorBlinking)
        {
            if (armorBlinkCoroutine != null) StopCoroutine(armorBlinkCoroutine);
            isArmorBlinking = false;

            if (armorFillImage != null) armorFillImage.color = originalArmorColor;
            if (armorText != null) armorText.color = originalArmorTextColor;
            if (armorIcon != null) armorIcon.color = originalArmorIconColor;

            if (crackOverlay != null) crackOverlay.gameObject.SetActive(false);
            if (redFilter != null)
            {
                Color filterColor = redFilter.color;
                filterColor.a = 0;
                redFilter.color = filterColor;
            }
        }
    }
    public void StartDamageFlash()
    {
        uiShakeTimer = uiShakeDuration;
        if (damageFlashCoroutine != null)
            StopCoroutine(damageFlashCoroutine);
        damageFlashCoroutine = StartCoroutine(DamageFlash());
    }
    IEnumerator BlinkArmor()
    {
        isArmorBlinking = true;

        while (isArmorBlinking)
        {

            if (armorFillImage != null) armorFillImage.color = armorLowColor;
            if (armorText != null) armorText.color = armorLowColor;
            if (armorIcon != null) armorIcon.color = armorLowColor;
            yield return new WaitForSeconds(0.5f / blinkSpeed);

            if (playerController.currentArmor <= 0)
            {
                break;
            }

            if (armorFillImage != null) armorFillImage.color = originalArmorColor;
            if (armorText != null) armorText.color = originalArmorTextColor;
            if (armorIcon != null) armorIcon.color = originalArmorIconColor;
            yield return new WaitForSeconds(0.5f / blinkSpeed);
        }

        if (playerController.currentArmor <= 0)
        {
            if (armorFillImage != null) armorFillImage.color = armorLowColor;
            if (armorText != null) armorText.color = armorLowColor;
            if (armorIcon != null) armorIcon.color = armorLowColor;
        }

        isArmorBlinking = false;
    }

    IEnumerator PulseRedFilter()
    {
        while (playerController.currentArmor > 0 && playerController.currentArmor / playerController.maxArmor * 100 <= armorLowThreshold)
        {
            float pulse = Mathf.Sin(Time.time * blinkSpeed) * 0.5f + 0.5f;
            float alpha = pulse * redFilterIntensity;

            if (redFilter != null)
            {
                Color filterColor = redFilter.color;
                filterColor.a = alpha;
                redFilter.color = filterColor;
            }

            yield return null;
        }
    }

    IEnumerator DamageFlash()
    {

        if (redFilter != null)
        {
            float tiempo = 0f;
            float duracion = uiShakeDuration;

            while (tiempo < duracion)
            {
                tiempo += Time.deltaTime;

                float alpha = Mathf.Lerp(redFilterIntensity * 2, 0, tiempo / duracion);

                Color filterColor = redFilter.color;
                filterColor.a = alpha;
                redFilter.color = filterColor;


                yield return null;
            }

            Color finalColor = redFilter.color;
            finalColor.a = 0f;
            redFilter.color = finalColor;
        }
    }
    void UpdateAmmoUI()
    {
        float porcentaje = (float)playerShooting.currentAmmo / playerShooting.maxAmmo;


        if (ammoText != null)
        {
            ammoText.text = playerShooting.currentAmmo.ToString() + "/" + playerShooting.maxAmmo.ToString();
        }

        bool shouldBlink = playerShooting.currentAmmo <= lowAmmoThreshold;

        if (shouldBlink && !isAmmoBlinking)
        {
            if (ammoBlinkCoroutine != null) StopCoroutine(ammoBlinkCoroutine);
            ammoBlinkCoroutine = StartCoroutine(BlinkAmmo());
        }
        else if (!shouldBlink && isAmmoBlinking)
        {
            if (ammoBlinkCoroutine != null) StopCoroutine(ammoBlinkCoroutine);
            isAmmoBlinking = false;

            if (ammoIcon != null) ammoIcon.color = originalAmmoColor;
            if (ammoText != null) ammoText.color = originalAmmoTextColor;
        }
    }

    IEnumerator BlinkAmmo()
    {
        isAmmoBlinking = true;

        while (isAmmoBlinking)
        {
            if (ammoIcon != null) ammoIcon.color = ammoEmptyColor;
            if (ammoText != null) ammoText.color = ammoEmptyColor;
            yield return new WaitForSeconds(0.5f / blinkSpeed);

            if (playerShooting.currentAmmo <= 0)
            {
                break;
            }

            if (ammoIcon != null) ammoIcon.color = originalAmmoColor;
            if (ammoText != null) ammoText.color = originalAmmoTextColor;
            yield return new WaitForSeconds(0.5f / blinkSpeed);
        }

        if (playerShooting.currentAmmo <= 0)
        {
            if (ammoIcon != null) ammoIcon.color = ammoEmptyColor;
            if (ammoText != null) ammoText.color = ammoEmptyColor;
        }

        isAmmoBlinking = false;
    }

    IEnumerator BlinkEnergy()
    {
        isEnergyBlinking = true;

        while (isEnergyBlinking)
        {

            if (jetpackIcon != null) jetpackIcon.color = ammoEmptyColor;
            if (jetpackBackGround != null) jetpackBackGround.color = ammoEmptyColor;
            if (jetpackSliderImage != null) jetpackSliderImage.color = ammoEmptyColor;
            if (jetpackText != null) jetpackText.color = ammoEmptyColor;
            yield return new WaitForSeconds(0.5f / blinkSpeed);

            if (playerController.currentJetpackEnergy <= 0)
            {
                break;
            }

            if (jetpackIcon != null) jetpackIcon.color = originalEnergyColor;
            if (jetpackBackGround != null) jetpackBackGround.color = originalEnergyColor;
            if (jetpackSliderImage != null) jetpackSliderImage.color = originalEnergyColor;
            if (jetpackText != null) jetpackText.color = originalEnergyTextColor;
            yield return new WaitForSeconds(0.5f / blinkSpeed);
        }


        if (playerController.currentJetpackEnergy <= 0)
        {
            if (jetpackIcon != null) jetpackIcon.color = ammoEmptyColor;
            if (jetpackBackGround != null) jetpackBackGround.color = ammoEmptyColor;
            if (jetpackSliderImage != null) jetpackSliderImage.color = ammoEmptyColor;
            if (jetpackText != null) jetpackText.color = ammoEmptyColor;
        }

        isEnergyBlinking = false;
    }

    public void SetDeadState(bool dead)
    {
        if (redFilter != null)
        {
            Color c = redFilter.color;
            c.a = dead ? redFilterIntensity * 2 : 0;
            redFilter.color = c;
        }
        if (crackOverlay != null)
        {
            crackOverlay.gameObject.SetActive(dead);
        }
    }
}