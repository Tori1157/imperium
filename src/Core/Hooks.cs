﻿namespace Oxide.Plugins
{
  using Network;
  using Oxide.Core;
  using UnityEngine;

  public partial class Imperium : RustPlugin
  {
    void OnUserApprove(Connection connection)
    {
      Users.SetOriginalName(connection.userid.ToString(), connection.username);
    }

    void OnPlayerInit(BasePlayer player)
    {
      if (player == null) return;

      // If the player hasn't fully connected yet, try again in 2 seconds.
      if (player.IsReceivingSnapshot)
      {
        timer.In(2, () => OnPlayerInit(player));
        return;
      }

      Users.Add(player);
    }

    void OnPlayerDisconnected(BasePlayer player)
    {
      if (player != null)
        Users.Remove(player);
    }

    void OnHammerHit(BasePlayer player, HitInfo hit)
    {
      User user = Users.Get(player);

      if (user != null && user.CurrentInteraction != null)
        user.CompleteInteraction(hit);
    }

    object OnEntityTakeDamage(BaseCombatEntity entity, HitInfo hit)
    {
      if (entity == null || hit == null)
        return null;

      object externalResult = Interface.CallHook("CanEntityTakeDamage", new object[] { entity, hit });

      if (externalResult != null)
      {
        if ((bool)externalResult == false)
          return false;

        return null;
      }

      if (hit.damageTypes.Has(Rust.DamageType.Decay))
        return Decay.AlterDecayDamage(entity, hit);

      User attacker = null;
      User defender = entity.GetComponent<User>();

      if (hit.InitiatorPlayer != null)
        attacker = hit.InitiatorPlayer.GetComponent<User>();

      // A player is being injured by something other than a player/NPC.
      if (attacker == null && defender != null)
        return Pvp.HandleIncidentalDamage(defender, hit);

      // One player is damaging another player.
      if (attacker != null && defender != null)
        return Pvp.HandleDamageBetweenPlayers(attacker, defender, hit);

      // A player is damaging a structure.
      if (attacker != null && defender == null)
        return Raiding.HandleDamageAgainstStructure(attacker, entity, hit);

      // A structure is taking damage from something that isn't a player.
      return Raiding.HandleIncidentalDamage(entity, hit);
    }

    object OnTrapTrigger(BaseTrap trap, GameObject obj)
    {
      var player = obj.GetComponent<BasePlayer>();

      if (trap == null || player == null)
        return null;

      User defender = Users.Get(player);
      return Pvp.HandleTrapTrigger(trap, defender);
    }

    object CanBeTargeted(BaseCombatEntity target, MonoBehaviour turret)
    {
      if (target == null || turret == null)
        return null;

      // Don't interfere with the helicopter.
      if (turret is HelicopterTurret)
        return null;

      var player = target as BasePlayer;

      if (player == null)
        return null;

      User defender = Users.Get(player);
      var entity = turret as BaseCombatEntity;

      if (defender == null || entity == null)
        return null;

      return Pvp.HandleTurretTarget(entity, defender);
    }

    void OnEntitySpawned(BaseNetworkable entity)
    {
      var plane = entity as CargoPlane;
      if (plane != null)
        Hud.GameEvents.BeginEvent(plane);

      var drop = entity as SupplyDrop;
      if (Options.Zones.Enabled && drop != null)
        Zones.CreateForSupplyDrop(drop);

      var heli = entity as BaseHelicopter;
      if (heli != null)
        Hud.GameEvents.BeginEvent(heli);

      var chinook = entity as CH47Helicopter;
      if (chinook != null)
        Hud.GameEvents.BeginEvent(chinook);

      var crate = entity as HackableLockedCrate;
      if (crate != null)
        Hud.GameEvents.BeginEvent(crate);
    }

    void OnEntityKill(BaseNetworkable networkable)
    {
      var entity = networkable as BaseEntity;

      if (!Ready || entity == null)
        return;

      // If a claim TC was destroyed, remove the claim from the area.
      var cupboard = entity as BuildingPrivlidge;
      if (cupboard != null)
      {
        var area = Areas.GetByClaimCupboard(cupboard);
        if (area != null)
        {
          PrintToChat(Messages.AreaClaimLostCupboardDestroyedAnnouncement, area.FactionId, area.Id);
          Log($"{area.FactionId} lost their claim on {area.Id} because the tool cupboard was destroyed (hook function)");
          Areas.Unclaim(area);
        }
        return;
      }

      // If a tax chest was destroyed, remove it from the faction data.
      var container = entity as StorageContainer;
      if (Options.Taxes.Enabled && container != null)
      {
        Faction faction = Factions.GetByTaxChest(container);
        if (faction != null)
        {
          Log($"{faction.Id}'s tax chest was destroyed (hook function)");
          faction.TaxChest = null;
        }
        return;
      }

      // If a helicopter was destroyed, create an event zone around it.
      var helicopter = entity as BaseHelicopter;
      if (Options.Zones.Enabled && helicopter != null)
        Zones.CreateForDebrisField(helicopter);
    }

    void OnDispenserGather(ResourceDispenser dispenser, BaseEntity entity, Item item)
    {
      Taxes.ProcessTaxesIfApplicable(dispenser, entity, item);
      Taxes.AwardBadlandsBonusIfApplicable(dispenser, entity, item);
    }

    void OnDispenserBonus(ResourceDispenser dispenser, BaseEntity entity, Item item)
    {
      Taxes.ProcessTaxesIfApplicable(dispenser, entity, item);
      Taxes.AwardBadlandsBonusIfApplicable(dispenser, entity, item);
    }

    object OnPlayerDie(BasePlayer player, HitInfo hit)
    {
      if (player == null)
        return null;

      // When a player dies, remove them from the area and any zones they were in.
      User user = Users.Get(player);
      if (user != null)
      {
        user.CurrentArea = null;
        user.CurrentZones.Clear();
      }

      return null;
    }

    void OnUserEnteredArea(User user, Area area)
    {
      Area previousArea = user.CurrentArea;

      user.CurrentArea = area;
      user.Hud.Refresh();

      if (previousArea == null)
        return;

      if (area.Type == AreaType.Badlands && previousArea.Type != AreaType.Badlands)
      {
        // The player has entered the badlands.
        user.SendChatMessage(Messages.EnteredBadlands);
      }
      else if (area.Type == AreaType.Wilderness && previousArea.Type != AreaType.Wilderness)
      {
        // The player has entered the wilderness.
        user.SendChatMessage(Messages.EnteredWilderness);
      }
      else if (area.IsClaimed && !previousArea.IsClaimed)
      {
        // The player has entered a faction's territory.
        user.SendChatMessage(Messages.EnteredClaimedArea, area.FactionId);
      }
      else if (area.IsClaimed && previousArea.IsClaimed && area.FactionId != previousArea.FactionId)
      {
        // The player has crossed a border between the territory of two factions.
        user.SendChatMessage(Messages.EnteredClaimedArea, area.FactionId);
      }
    }

    void OnUserEnteredZone(User user, Zone zone)
    {
      user.CurrentZones.Add(zone);
      user.Hud.Refresh();
    }

    void OnUserLeftZone(User user, Zone zone)
    {
      user.CurrentZones.Remove(zone);
      user.Hud.Refresh();
    }

    void OnFactionCreated(Faction faction)
    {
      Hud.RefreshForAllPlayers();
    }

    void OnFactionDisbanded(Faction faction)
    {
      Area[] areas = Instance.Areas.GetAllClaimedByFaction(faction);

      if (areas.Length > 0)
      {
        foreach (Area area in areas)
          PrintToChat(Messages.AreaClaimLostFactionDisbandedAnnouncement, area.FactionId, area.Id);

        Areas.Unclaim(areas);
      }

      Wars.EndAllWarsForEliminatedFactions();
      Hud.RefreshForAllPlayers();
    }

    void OnFactionTaxesChanged(Faction faction)
    {
      Hud.RefreshForAllPlayers();
    }

    void OnAreaChanged(Area area)
    {
      Wars.EndAllWarsForEliminatedFactions();
      Pins.RemoveAllPinsInUnclaimedAreas();
      Hud.GenerateMapOverlayImage();
      Hud.RefreshForAllPlayers();
    }

    void OnDiplomacyChanged()
    {
      Hud.RefreshForAllPlayers();
    }
  }
}
