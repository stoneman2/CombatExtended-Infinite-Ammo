using Verse;
using UnityEngine;
using CombatExtendedInfiniteAmmo.Patches;

namespace CombatExtendedInfiniteAmmo;

public enum InfiniteAmmoMode
{
    Off,
    InfiniteReserve,
    InfiniteAmmo
}

public class InfiniteAmmoSettings : ModSettings
{
    public InfiniteAmmoMode ammoMode = InfiniteAmmoMode.InfiniteReserve;
    public bool infiniteTurretAmmo = false;
    public bool infiniteMortarAmmo = false;
    public bool infiniteGrenades = false;
    public bool playerFactionOnly = true;
    public bool enableBalancing = false;
    public float ammoCostMultiplier = 5f;
    public bool limitAmmoStackSize = true;
    public bool limitAmmoSpawns = true;
    
    // Convenience properties for backward compatibility with patch code
    public bool infiniteAmmo => ammoMode == InfiniteAmmoMode.InfiniteAmmo;
    public bool infiniteReserve => ammoMode == InfiniteAmmoMode.InfiniteReserve;
    
    private bool _prevEnableBalancing = false;
    private float _prevAmmoCostMultiplier = 5f;
    private bool _prevLimitAmmoStackSize = true;
    
    public override void ExposeData()
    {
        base.ExposeData();
        
        // Migration: load old booleans if present, then save as enum
        if (Scribe.mode == LoadSaveMode.LoadingVars)
        {
            bool oldInfiniteAmmo = false;
            bool oldInfiniteReserve = false;
            Scribe_Values.Look(ref oldInfiniteAmmo, "infiniteAmmo", false);
            Scribe_Values.Look(ref oldInfiniteReserve, "infiniteReserve", false);
            
            // Check if we have the new enum value saved
            Scribe_Values.Look(ref ammoMode, "ammoMode", InfiniteAmmoMode.Off);
            
            // If ammoMode is Off but old booleans were set, migrate
            if (ammoMode == InfiniteAmmoMode.Off)
            {
                if (oldInfiniteAmmo)
                    ammoMode = InfiniteAmmoMode.InfiniteAmmo;
                else if (oldInfiniteReserve)
                    ammoMode = InfiniteAmmoMode.InfiniteReserve;
            }
        }
        else
        {
            Scribe_Values.Look(ref ammoMode, "ammoMode");
        }
        
        Scribe_Values.Look(ref infiniteTurretAmmo, "infiniteTurretAmmo", false);
        Scribe_Values.Look(ref infiniteMortarAmmo, "infiniteMortarAmmo", false);
        Scribe_Values.Look(ref infiniteGrenades, "infiniteGrenades", false);
        Scribe_Values.Look(ref playerFactionOnly, "playerFactionOnly", true);
        Scribe_Values.Look(ref enableBalancing, "enableBalancing", false);
        Scribe_Values.Look(ref ammoCostMultiplier, "ammoCostMultiplier", 5f);
        Scribe_Values.Look(ref limitAmmoStackSize, "limitAmmoStackSize", true);
        Scribe_Values.Look(ref limitAmmoSpawns, "limitAmmoSpawns", true);
        
        _prevEnableBalancing = enableBalancing;
        _prevAmmoCostMultiplier = ammoCostMultiplier;
        _prevLimitAmmoStackSize = limitAmmoStackSize;
    }

    
    public void DoSettingsWindowContents(Rect inRect)
    {
        Listing_Standard list = new Listing_Standard();
        list.Begin(inRect);
        
        // Header
        Text.Font = GameFont.Medium;
        list.Label("CEInfiniteAmmo_Settings_Header".Translate());
        Text.Font = GameFont.Small;
        list.Gap();
        
        // Player Faction Only
        list.CheckboxLabeled(
            "CEInfiniteAmmo_PlayerOnly_Title".Translate(), 
            ref playerFactionOnly, 
            "CEInfiniteAmmo_PlayerOnly_Desc".Translate()
        );
        list.GapLine();
        
        // Ammo Mode radio buttons
        list.Label("CEInfiniteAmmo_AmmoMode_Title".Translate());
        list.Gap(4f);
        
        if (list.RadioButton("CEInfiniteAmmo_AmmoMode_Off".Translate(), ammoMode == InfiniteAmmoMode.Off, tooltip: "CEInfiniteAmmo_AmmoMode_Off_Desc".Translate()))
            ammoMode = InfiniteAmmoMode.Off;
        
        if (list.RadioButton("CEInfiniteAmmo_InfiniteReserve_Title".Translate(), ammoMode == InfiniteAmmoMode.InfiniteReserve, tooltip: "CEInfiniteAmmo_InfiniteReserve_Desc".Translate()))
            ammoMode = InfiniteAmmoMode.InfiniteReserve;
        
        if (list.RadioButton("CEInfiniteAmmo_InfiniteAmmo_Title".Translate(), ammoMode == InfiniteAmmoMode.InfiniteAmmo, tooltip: "CEInfiniteAmmo_InfiniteAmmo_Desc".Translate()))
            ammoMode = InfiniteAmmoMode.InfiniteAmmo;
        
        list.Gap();
        
        // Infinite Turret Ammo
        list.CheckboxLabeled(
            "CEInfiniteAmmo_InfiniteTurretAmmo_Title".Translate(), 
            ref infiniteTurretAmmo, 
            "CEInfiniteAmmo_InfiniteTurretAmmo_Desc".Translate()
        );
        
        // Infinite Mortar Ammo
        list.CheckboxLabeled(
            "CEInfiniteAmmo_InfiniteMortarAmmo_Title".Translate(), 
            ref infiniteMortarAmmo, 
            "CEInfiniteAmmo_InfiniteMortarAmmo_Desc".Translate()
        );
        
        // Infinite Grenades
        list.CheckboxLabeled(
            "CEInfiniteAmmo_InfiniteGrenades_Title".Translate(), 
            ref infiniteGrenades, 
            "CEInfiniteAmmo_InfiniteGrenades_Desc".Translate()
        );
        
        list.GapLine();

        // Balancing
        list.CheckboxLabeled(
            "CEInfiniteAmmo_BalanceMode_Title".Translate(), 
            ref enableBalancing, 
            "CEInfiniteAmmo_BalanceMode_Desc".Translate()
        );
        
        if (enableBalancing)
        {
            // Sub-options for balancing
            list.CheckboxLabeled(
                "CEInfiniteAmmo_LimitStackSize_Title".Translate(), 
                ref limitAmmoStackSize, 
                "CEInfiniteAmmo_LimitStackSize_Desc".Translate()
            );
            
            list.CheckboxLabeled(
                "CEInfiniteAmmo_LimitSpawns_Title".Translate(), 
                ref limitAmmoSpawns, 
                "CEInfiniteAmmo_LimitSpawns_Desc".Translate()
            );
            
            list.Gap();
            list.Label(
                "CEInfiniteAmmo_BalanceCost_Title".Translate() + ": " + ammoCostMultiplier.ToString("0.0") + "x"
            );
            ammoCostMultiplier = list.Slider(ammoCostMultiplier, 1f, 20f);
        }
        
        list.End();
        
        bool settingsChanged = enableBalancing != _prevEnableBalancing || 
                               ammoCostMultiplier != _prevAmmoCostMultiplier ||
                               limitAmmoStackSize != _prevLimitAmmoStackSize;
        
        if (settingsChanged)
        {
            _prevEnableBalancing = enableBalancing;
            _prevAmmoCostMultiplier = ammoCostMultiplier;
            _prevLimitAmmoStackSize = limitAmmoStackSize;
            AmmoRecipeManager.ApplyRecipeChanges();
            AmmoBalanceManager.ApplyBalanceChanges();
        }
    }
}
