using UnityEngine;
using UnityEngine.SceneManagement;

// Aplica las mejoras del Taller al traje (valores del GDD). No hay que tocar la escena: se agrega solo al
// jugador al cargar cualquier escena que tenga un PlayerController_Base.
// Guarda los valores del Inspector y calcula todo desde ellos, así aplicar dos veces no acumula.
//   Metralleta de burbujas  I: arma principal (teclas 1/2 o rueda)   II: +30 % de daño
//   Misiles teledirigidos   I: 3 misiles, recarga 10 s (tecla E)      II: 4 misiles, 8 s
//   Disparo perforante      I: el cargado atraviesa 2 aves            II: 4 aves
//   Tanque extra            I: +40 % de combustible                   II: +80 %
//   Turbo eficiente         I: el turbo gasta 35 % menos              II: además recarga en el agua +50 %
//   Dash doble              I: 2 dashes seguidos
//   Placas reforzadas       I: +25 de armadura máxima                 II: +50
//   Escudo de burbuja       I: absorbe 1 golpe, vuelve en 20 s        II: 12 s
//   Reparación acuática     I: +2 de armadura por segundo en el agua  II: +4
//   Cargador ampliado       I: +15 balas                              II: +30
//   Imán de cápsulas        I: recoge munición desde 2.5 veces más lejos
//   Sonar                   lo dibuja el visor (VisorSonarHUD)
// Solo en el Editor: F10 = todas en nivel I, Shift+F10 = nivel II, Ctrl+F10 = volver al progreso guardado.
[DisallowMultipleComponent]
public class PlayerUpgrades : MonoBehaviour
{
    PlayerController_Base player;
    PlayerShooting shooting;
    MissileLauncher missiles;
    BubbleShield shield;
    SuitUpgradeVisuals visuals;

    // Valores originales (los del Inspector)
    float baseMaxFuel, baseTurboDrain, baseTurboSwimDrain, baseWaterRecharge, baseMaxArmor;
    int baseMaxAmmo, baseDashCharges;
    bool started;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Hook()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
        SceneManager.sceneLoaded += OnSceneLoaded;
        Install();
    }

    static void OnSceneLoaded(Scene scene, LoadSceneMode mode) { Install(); }

    static void Install()
    {
        PlayerController_Base p = FindFirstObjectByType<PlayerController_Base>();
        if (p != null && p.GetComponent<PlayerUpgrades>() == null) p.gameObject.AddComponent<PlayerUpgrades>();
    }

    void Awake()
    {
        player = GetComponent<PlayerController_Base>();
        shooting = GetComponent<PlayerShooting>();
        missiles = GetComponent<MissileLauncher>();
        if (missiles == null) missiles = gameObject.AddComponent<MissileLauncher>();
        shield = GetComponent<BubbleShield>();
        if (shield == null) shield = gameObject.AddComponent<BubbleShield>();
        visuals = GetComponent<SuitUpgradeVisuals>();
        if (visuals == null) visuals = gameObject.AddComponent<SuitUpgradeVisuals>();

        if (player != null)
        {
            baseMaxFuel = player.maxJetpackEnergy;
            baseTurboDrain = player.turboDrainRate;
            baseTurboSwimDrain = player.turboSwimDrainRate;
            baseWaterRecharge = player.jetpackRechargeRate;
            baseMaxArmor = player.maxArmor;
            baseDashCharges = Mathf.Max(1, player.dashCharges);
        }
        if (shooting != null) baseMaxAmmo = shooting.maxAmmo;

        Upgrades.Changed += Apply;
        // Antes del Start del jugador: el traje empieza con armadura, combustible y munición llenos
        Apply();
    }

    void Start()
    {
        started = true;
        if (player != null)
        {
            player.currentArmor = player.maxArmor;
            player.currentJetpackEnergy = player.maxJetpackEnergy;
        }
        if (shooting != null) shooting.currentAmmo = shooting.maxAmmo;
    }

    void OnDestroy()
    {
        Upgrades.Changed -= Apply;
        AmmoPickup.MagnetMultiplier = 1f;
    }

    // Recalcula todo desde los valores originales
    void Apply()
    {
        if (player != null)
        {
            float oldFuel = player.maxJetpackEnergy, oldArmor = player.maxArmor;

            int tank = Upgrades.Level(UpgradeId.ExtraTank);
            player.maxJetpackEnergy = baseMaxFuel * (1f + 0.4f * tank);

            int turbo = Upgrades.Level(UpgradeId.EfficientTurbo);
            float drain = turbo >= 1 ? 0.65f : 1f;
            player.turboDrainRate = baseTurboDrain * drain;
            player.turboSwimDrainRate = baseTurboSwimDrain * drain;
            player.jetpackRechargeRate = baseWaterRecharge * (turbo >= 2 ? 1.5f : 1f);

            player.SetDashCharges(Upgrades.Has(UpgradeId.DoubleDash) ? 2 : baseDashCharges);

            player.maxArmor = baseMaxArmor + 25f * Upgrades.Level(UpgradeId.ReinforcedPlates);

            int repair = Upgrades.Level(UpgradeId.WaterRepair);
            player.waterArmorRegen = repair >= 2 ? 4f : (repair == 1 ? 2f : 0f);

            // Durante la partida (atajo de prueba): lo nuevo se suma lleno, nunca pasa del máximo
            if (started)
            {
                player.currentJetpackEnergy = Mathf.Clamp(player.currentJetpackEnergy + Mathf.Max(0f, player.maxJetpackEnergy - oldFuel), 0f, player.maxJetpackEnergy);
                player.currentArmor = Mathf.Clamp(player.currentArmor + Mathf.Max(0f, player.maxArmor - oldArmor), 0f, player.maxArmor);
            }
        }

        if (shooting != null)
        {
            int oldMax = shooting.maxAmmo;
            shooting.maxAmmo = baseMaxAmmo + 15 * Upgrades.Level(UpgradeId.ExtendedMag);
            if (started) shooting.currentAmmo = Mathf.Clamp(shooting.currentAmmo + Mathf.Max(0, shooting.maxAmmo - oldMax), 0, shooting.maxAmmo);

            int bubble = Upgrades.Level(UpgradeId.BubbleGun);
            shooting.SetBubbleGun(bubble > 0, bubble >= 2 ? 1.3f : 1f);

            int pierce = Upgrades.Level(UpgradeId.PiercingShot);
            shooting.chargedPierce = pierce >= 2 ? 4 : (pierce == 1 ? 2 : 0);
        }

        AmmoPickup.MagnetMultiplier = Upgrades.Has(UpgradeId.PickupMagnet) ? 2.5f : 1f;
        if (missiles != null) missiles.Configure(Upgrades.Level(UpgradeId.HomingMissiles));
        if (shield != null) shield.Configure(Upgrades.Level(UpgradeId.BubbleShield));
        if (visuals != null) visuals.Refresh();
    }

#if UNITY_EDITOR
    // Atajos de prueba (solo en el Editor): no tocan el progreso guardado
    void Update()
    {
        if (!Input.GetKeyDown(KeyCode.F10)) return;
        bool shift = Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift);
        bool ctrl = Input.GetKey(KeyCode.LeftControl) || Input.GetKey(KeyCode.RightControl);
        int level = ctrl ? -1 : (shift ? 2 : 1);
        Upgrades.SetDebugLevel(level);
        string msg = level < 0 ? "MEJORAS: SE USA EL PROGRESO GUARDADO" : "PRUEBA: TODAS LAS MEJORAS EN NIVEL " + (level >= 2 ? "II" : "I");
        Debug.Log("PlayerUpgrades: " + msg);
        GameDirector gd = GameDirector.Instance;
        if (gd != null) gd.ShowToast(msg, MenuUI.Accent);
    }
#endif
}
