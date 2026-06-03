using HarmonyLib;
using Verse;
using RimWorld;
using CombatExtended;
using CombatExtended.Compatibility;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace CombatExtendedInfiniteAmmo.Patches;

public static class AmmoInventoryHelper
{
    public static bool IsMortar(Building_Turret turret)
    {
        if (turret == null) return false;
        return turret.def.building != null && turret.def.building.IsMortar;
    }
    
    /// <summary>
    /// Check if the turret qualifies for infinite turret ammo (excludes mortars).
    /// </summary>
    public static bool IsInfiniteTurret(CompAmmoUser comp, InfiniteAmmoSettings settings)
    {
        if (comp?.turret == null || !settings.infiniteTurretAmmo) return false;
        return !IsMortar(comp.turret);
    }
    
    /// <summary>
    /// Check if the turret is a mortar that qualifies for infinite mortar ammo.
    /// </summary>
    public static bool IsInfiniteMortar(CompAmmoUser comp, InfiniteAmmoSettings settings)
    {
        if (comp?.turret == null || !settings.infiniteMortarAmmo) return false;
        return IsMortar(comp.turret);
    }
    
    // Check if pawn has ANY compatible ammo in inventory
    public static bool HasAnyAmmoInInventory(CompAmmoUser comp)
    {
        if (comp == null) return false;
        return comp.TryFindAmmoInInventory(out _);
    }
    
    // Check if pawn has SPECIFIC ammo type in inventory
    public static bool HasSpecificAmmoInInventory(CompAmmoUser comp, AmmoDef ammoDef)
    {
        if (comp == null || ammoDef == null) return false;
        
        Pawn wielder = comp.Wielder;
        if (wielder == null) return false;
        
        var inventory = wielder.inventory?.innerContainer;
        if (inventory == null) return false;
        
        return inventory.Any(t => t.def == ammoDef);
    }
    
    // Find specific ammo type in inventory, return the Thing
    public static Thing FindSpecificAmmoInInventory(CompAmmoUser comp, AmmoDef ammoDef)
    {
        if (comp == null || ammoDef == null) return null;
        
        Pawn wielder = comp.Wielder;
        if (wielder == null) return null;
        
        var inventory = wielder.inventory?.innerContainer;
        if (inventory == null) return null;
        
        return inventory.FirstOrDefault(t => t.def == ammoDef);
    }
    
    // Check if this comp belongs to an eligible pawn/turret (faction check)
    public static bool IsEligibleForInfiniteAmmo(CompAmmoUser comp, InfiniteAmmoSettings settings)
    {
        if (comp == null || settings == null) return false;
        
        bool isTurret = comp.turret != null;
        
        if (settings.playerFactionOnly)
        {
            Faction playerFaction = Faction.OfPlayer;
            if (isTurret && comp.turret.Faction != playerFaction) return false;
            if (!isTurret && comp.Wielder?.Faction != playerFaction) return false;
        }
        
        return true;
    }
    
    // Check if pawn is eligible for player-faction infinite ammo (non-turret)
    public static bool IsPlayerPawnEligible(CompAmmoUser comp, InfiniteAmmoSettings settings)
    {
        if (comp == null || settings == null) return false;
        if (comp.turret != null) return false;
        if (!settings.infiniteAmmo) return false;
        
        if (settings.playerFactionOnly)
        {
            if (comp.Wielder?.Faction != Faction.OfPlayer) return false;
        }
        
        return true;
    }
}

[HarmonyPatch(typeof(CompAmmoUser), nameof(CompAmmoUser.Notify_ShotFired))]
public static class Patch_CompAmmoUser_Notify_ShotFired
{
    public static bool Prefix(CompAmmoUser __instance)
    {
        var settings = CombatExtendedInfiniteAmmoMod.Settings;
        if (settings == null) return true;
        
        bool isTurret = __instance.turret != null;
        
        if (!AmmoInventoryHelper.IsEligibleForInfiniteAmmo(__instance, settings))
            return true;
        
        // Infinite turret: never deplete
        if (isTurret && AmmoInventoryHelper.IsInfiniteTurret(__instance, settings) && __instance.CurMagCount > 0)
        {
            return false;
        }
        
        // Infinite mortar: never deplete (ammo was consumed on load)
        if (isTurret && AmmoInventoryHelper.IsInfiniteMortar(__instance, settings) && __instance.CurMagCount > 0)
        {
            return false;
        }

        // Infinite ammo for pawns: never deplete mag
        return isTurret || !settings.infiniteAmmo;
    }
}

[HarmonyPatch(typeof(CompAmmoUser), nameof(CompAmmoUser.TryStartReload))]
public static class Patch_CompAmmoUser_TryStartReload
{
    public static bool Prefix(CompAmmoUser __instance)
    {
        InfiniteAmmoSettings settings = CombatExtendedInfiniteAmmoMod.Settings;
        if (settings == null) return true;
        
        bool isTurret = __instance.turret != null;
        
        if (!AmmoInventoryHelper.IsEligibleForInfiniteAmmo(__instance, settings))
            return true;

        if (isTurret &&
            // Infinite turret (non-mortar): instant refill
            AmmoInventoryHelper.IsInfiniteTurret(__instance, settings) && __instance.HasMagazine)
        {
            __instance.CurMagCount = __instance.MagSize;
            return false;
        }
        // Infinite mortar: if same ammo type selected, instant refill
        // If different ammo type selected, fall through to normal reload (consumes new ammo, returns old)
        else if (isTurret && (AmmoInventoryHelper.IsInfiniteMortar(__instance, settings) && __instance.HasMagazine))
        {
            AmmoDef selected = __instance.SelectedAmmo;
            AmmoDef current = __instance.CurrentAmmo;

            // Same ammo type or no change requested: instant refill
            if (selected != null && selected != current) return true;
            __instance.CurMagCount = __instance.MagSize;
            return false;
            // Different ammo type: let normal reload happen (unloads old, loads new - both consume/return normally)
        }

        // For pawn infiniteAmmo and infiniteReserve, let the normal reload flow happen
        // (TryUnload and LoadAmmo are patched to not consume/return ammo for pawns)
        return true;
    }
}

[HarmonyPatch(typeof(CompAmmoUser), "get_HasAmmo")]
public static class Patch_CompAmmoUser_HasAmmo
{
    public static void Postfix(CompAmmoUser __instance, ref bool __result)
    {
        var settings = CombatExtendedInfiniteAmmoMod.Settings;
        if (settings == null) return;
        
        if (!AmmoInventoryHelper.IsEligibleForInfiniteAmmo(__instance, settings))
            return;
        
        bool isTurret = __instance.turret != null;
        
        if (isTurret && AmmoInventoryHelper.IsInfiniteTurret(__instance, settings))
        {
            __result = true;
            return;
        }
        
        if (isTurret && AmmoInventoryHelper.IsInfiniteMortar(__instance, settings))
        {
            // Mortar has infinite ammo of its currently loaded type
            if (__instance.CurrentAmmo != null && __instance.CurMagCount > 0)
                __result = true;
            return;
        }
        
        if (!isTurret && (settings.infiniteAmmo || settings.infiniteReserve))
        {
            __result = true;
        }
    }
}

[HarmonyPatch(typeof(CompAmmoUser), nameof(CompAmmoUser.LoadAmmo))]
public static class Patch_CompAmmoUser_LoadAmmo
{
    public static bool Prefix(CompAmmoUser __instance, Thing ammo, bool emptyMag)
    {
        var settings = CombatExtendedInfiniteAmmoMod.Settings;
        if (settings == null) return true;
        
        bool isTurret = __instance.turret != null;
        
        if (!AmmoInventoryHelper.IsEligibleForInfiniteAmmo(__instance, settings))
            return true;
        
        // For mortars and turrets: let normal LoadAmmo run (consumes ammo on load/switch)
        if (isTurret) return true;
        
        if (!settings.infiniteReserve && !settings.infiniteAmmo) return true;
        if (!__instance.UseAmmo) return true;
        
        // For pawns with infinite ammo/reserve: don't consume inventory ammo
        AmmoDef ammoToLoad = null;
        
        if (ammo != null)
        {
            ammoToLoad = ammo.def as AmmoDef;
        }
        else
        {
            ammoToLoad = __instance.SelectedAmmo ?? __instance.CurrentAmmo;
            
            if (ammoToLoad == null && __instance.TryFindAmmoInInventory(out Thing foundAmmo))
            {
                ammoToLoad = foundAmmo.def as AmmoDef;
            }
        }
        
        if (ammoToLoad == null) return true;
        
        // Set ammo type without consuming anything
        __instance.CurrentAmmo = ammoToLoad;
        
        // Fill the magazine
        int newMagCount;
        if (__instance.Props.reloadOneAtATime)
        {
            newMagCount = __instance.CurMagCount + __instance.CurAmmoCount;
            if (newMagCount > __instance.MagSize) newMagCount = __instance.MagSize;
        }
        else
        {
            newMagCount = __instance.MagSize;
        }
        
        __instance.CurMagCount = newMagCount;

        __instance.turret?.SetReloading(false);

        // Do NOT consume the ammo Thing - skip original method entirely
        return false;
    }
}

// Patch TryUnload to prevent ammo from being returned to inventory (for pawns only)
[HarmonyPatch]
public static class Patch_CompAmmoUser_TryUnload
{
    public static IEnumerable<MethodBase> TargetMethods()
    {
        return typeof(CompAmmoUser).GetMethods(BindingFlags.Instance | BindingFlags.Public)
            .Where(method => method.Name == nameof(CompAmmoUser.TryUnload)).Cast<MethodBase>();
    }
    
    public static bool Prefix(CompAmmoUser __instance, ref bool __result)
    {
        var settings = CombatExtendedInfiniteAmmoMod.Settings;
        if (settings == null) return true;
        
        bool isTurret = __instance.turret != null;
        
        if (!AmmoInventoryHelper.IsEligibleForInfiniteAmmo(__instance, settings))
            return true;
        
        // For pawns: don't give ammo back, just empty the magazine
        if (isTurret || (!settings.infiniteReserve && !settings.infiniteAmmo)) return true;
        __instance.CurMagCount = 0;
        __result = true;
        return false;

        // For mortars and turrets: let normal TryUnload run (returns ammo when switching types)
    }
}

// Prevent ammo from being consumed from inventory when preparing a shot
[HarmonyPatch(typeof(CompAmmoUser), nameof(CompAmmoUser.TryPrepareShot))]
public static class Patch_CompAmmoUser_TryPrepareShot
{
    public static bool Prefix(CompAmmoUser __instance, ref bool __result)
    {
        var settings = CombatExtendedInfiniteAmmoMod.Settings;
        if (settings == null) return true;
        
        bool isTurret = __instance.turret != null;
        
        if (!AmmoInventoryHelper.IsEligibleForInfiniteAmmo(__instance, settings))
            return true;

        if ((!isTurret || !AmmoInventoryHelper.IsInfiniteTurret(__instance, settings)) &&
            (!isTurret || !AmmoInventoryHelper.IsInfiniteMortar(__instance, settings)) &&
            (isTurret || !settings.infiniteAmmo)) return true;
        __result = true;
        return false;
    }
}

[HarmonyPatch(typeof(CompAmmoUser), "get_MagSize")]
public static class Patch_CompAmmoUser_MagSize
{
    public static bool Prefix(CompAmmoUser __instance, ref int __result)
    {
        var settings = CombatExtendedInfiniteAmmoMod.Settings;
        if (settings == null) return true;
        
        bool isTurret = __instance.turret != null;
        
        if (!AmmoInventoryHelper.IsEligibleForInfiniteAmmo(__instance, settings))
            return true;
        
        // Set mag size to 1 for infinite turrets (not mortars - mortars keep normal mag size)
        if (!isTurret || !AmmoInventoryHelper.IsInfiniteTurret(__instance, settings)) return true;
        __result = 1;
        return false;

    }
}
