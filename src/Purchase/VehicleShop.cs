using System.Collections.Generic;
using NuclearOption.Networking;
using UnityEngine;

namespace PlayableVehicles
{
    internal static class VehicleShop
    {
        public enum CooldownGroup : byte
        {
            Light,
            AirDefense,
            Tanks,
            Artillery,
        }

        public enum Result : byte
        {
            Purchased,
            Unavailable,
            NoFaction,
            DonationsDisabled,
            DepotUnavailable,
            DepotBusy,
            Cooldown,
            InsufficientFunds,
            Escalation,
            Rank,
            Warheads,
            NoSpace,
            Sold,
            NotYours,
            TooFar,
            Moving,
            Occupied,
        }

        public sealed class Arsenal
        {
            public bool Nuclear;
            public bool Strategic;
            public bool Ballistic;
            public int Warheads;
        }

        private static readonly Dictionary<VehicleDefinition, Arsenal> _arsenals = new Dictionary<VehicleDefinition, Arsenal>();

        public static bool Enabled => GameAccess.VehicleShopAvailable;

        public static List<VehicleDefinition> Catalog
        {
            get
            {
                Encyclopedia encyclopedia = Encyclopedia.i;
                return encyclopedia != null ? encyclopedia.vehicles : null;
            }
        }

        public static bool TryGet(int index, out VehicleDefinition definition)
        {
            List<VehicleDefinition> catalog = Catalog;
            definition = catalog != null && index >= 0 && index < catalog.Count ? catalog[index] : null;
            return definition != null && definition.unitPrefab != null && definition.unitPrefab.GetComponent<GroundVehicle>() != null;
        }

        public static int IndexOf(VehicleDefinition definition)
        {
            List<VehicleDefinition> catalog = Catalog;
            return catalog != null ? catalog.IndexOf(definition) : -1;
        }

        public static float Price(VehicleDefinition definition)
        {
            Unit unit = definition.unitPrefab != null ? definition.unitPrefab.GetComponent<Unit>() : null;
            return definition.value + (unit != null ? unit.GetAmmoValue().Total : 0f);
        }

        public static CooldownGroup Group(VehicleDefinition definition)
        {
            switch (definition.vehicleType)
            {
                case VehicleType.AAA:
                case VehicleType.IR_SAM:
                case VehicleType.R_SAM:
                case VehicleType.RDR:
                    return CooldownGroup.AirDefense;
                case VehicleType.MBT:
                    return CooldownGroup.Tanks;
                case VehicleType.ART:
                    return CooldownGroup.Artillery;
                default:
                    return CooldownGroup.Light;
            }
        }

        public static float Cooldown(VehicleDefinition definition)
        {
            switch (Group(definition))
            {
                case CooldownGroup.AirDefense: return ModConfig.CooldownAirDefense.Value;
                case CooldownGroup.Tanks: return ModConfig.CooldownTanks.Value;
                case CooldownGroup.Artillery: return ModConfig.CooldownArtillery.Value;
                default: return ModConfig.CooldownLight.Value;
            }
        }

        public static bool IsOwnDepot(VehicleDepot depot, FactionHQ hq)
        {
            return depot != null && !depot.disabled && hq != null && depot.NetworkHQ == hq;
        }

        public static bool IsOwn(PurchasePoint point, FactionHQ hq)
        {
            return point != null && point.IsOwn(hq);
        }

        public static Result Check(Player player, PurchasePoint point, VehicleDefinition definition)
        {
            if (!Enabled || !ModConfig.Enabled.Value || definition == null) return Result.Unavailable;
            if (player == null || player.HQ == null) return Result.NoFaction;
            if (player.HQ.preventDonation && (point == null || point.Kind == PurchasePoint.PointKind.Depot)) return Result.DonationsDisabled;
            if (!IsOwn(point, player.HQ)) return Result.DepotUnavailable;
            Result restriction = Restriction(player, definition);
            if (restriction != Result.Purchased) return restriction;
            if (player.Allocation < Price(definition)) return Result.InsufficientFunds;
            return Result.Purchased;
        }

        public static Result Restriction(Player player, VehicleDefinition definition)
        {
            Arsenal arsenal = GetArsenal(definition);
            MissionManager mission = NetworkSceneSingleton<MissionManager>.i;
            if (mission == null || (!arsenal.Nuclear && !arsenal.Ballistic)) return Result.Purchased;

            if (!MissionManager.AllowTactical()) return Result.Escalation;
            if (!arsenal.Nuclear) return Result.Purchased;

            int rank = player != null ? player.PlayerRank : 0;
            if (rank < mission.tacticalMinRank) return Result.Rank;
            if (arsenal.Strategic)
            {
                if (!MissionManager.AllowStrategic()) return Result.Escalation;
                if (rank < mission.strategicMinRank) return Result.Rank;
            }
            if (player == null || player.HQ == null || player.HQ.GetWarheadStockpile() < arsenal.Warheads) return Result.Warheads;
            return Result.Purchased;
        }

        public static Arsenal GetArsenal(VehicleDefinition definition)
        {
            if (_arsenals.TryGetValue(definition, out Arsenal arsenal)) return arsenal;

            arsenal = new Arsenal();
            if (definition.unitPrefab != null)
            {
                foreach (Weapon weapon in definition.unitPrefab.GetComponentsInChildren<Weapon>(true))
                {
                    WeaponInfo info = weapon != null ? weapon.info : null;
                    if (info == null) continue;
                    if (info.nuclear)
                    {
                        arsenal.Nuclear = true;
                        arsenal.Strategic |= info.strategic;
                        arsenal.Warheads += Mathf.Max(1, weapon.ammo);
                    }
                    else if (info.missile && info.overHorizon)
                    {
                        arsenal.Ballistic = true;
                    }
                }
            }
            if (!arsenal.Nuclear && !arsenal.Ballistic && definition.unitPrefab != null
                && definition.unitPrefab.name == "Aryx_HLT_HSM")
            {
                arsenal.Ballistic = true;
            }
            _arsenals[definition] = arsenal;
            if (arsenal.Nuclear || arsenal.Ballistic)
            {
                ModLog.Info("Vehicle shop: " + definition.unitName + " nuclear=" + arsenal.Nuclear + " strategic=" + arsenal.Strategic
                    + " ballistic=" + arsenal.Ballistic + " warheads=" + arsenal.Warheads);
            }
            return arsenal;
        }

        public static string RestrictionText(Result restriction, VehicleDefinition definition)
        {
            Arsenal arsenal = GetArsenal(definition);
            MissionManager mission = NetworkSceneSingleton<MissionManager>.i;
            switch (restriction)
            {
                case Result.Escalation:
                    return arsenal.Strategic && MissionManager.AllowTactical() ? "Requires strategic nuclear escalation" : "Requires tactical nuclear escalation";
                case Result.Rank:
                    float rank = mission == null ? 0f : arsenal.Strategic && MissionManager.AllowTactical() ? mission.strategicMinRank : mission.tacticalMinRank;
                    return string.Format("Minimum rank {0:F0} required", rank);
                case Result.Warheads:
                    return string.Format("Not enough warheads ({0} needed)", arsenal.Warheads);
                default:
                    return "Unavailable";
            }
        }

        public static void ConsumeWarheads(FactionHQ hq, VehicleDefinition definition)
        {
            int needed = GetArsenal(definition).Warheads;
            if (needed <= 0 || hq == null) return;
            foreach (Airbase airbase in FactionRegistry.airbaseLookup.Values)
            {
                if (needed <= 0) break;
                if (airbase == null || airbase.CurrentHQ != hq || !airbase.HasStorage()) continue;
                int take = Mathf.Min(needed, airbase.GetWarheads());
                if (take <= 0) continue;
                airbase.RemoveWarheads(take);
                needed -= take;
            }
        }

        public static float Refund(GroundVehicle vehicle)
        {
            VehicleDefinition definition = vehicle != null ? vehicle.definition as VehicleDefinition : null;
            if (definition == null) return 0f;
            float fullAmmo = vehicle.weaponStations != null && vehicle.weaponStations.Count > 0 ? vehicle.GetAmmoValue().Total : 0f;
            float ammoPart = fullAmmo > 0f ? fullAmmo / (Mathf.Max(0f, definition.value) + fullAmmo) : 0f;
            return Price(definition) * ((1f - ammoPart) * HullHealth(vehicle) + ammoPart * AmmoShare(vehicle));
        }

        public static float HullHealth(GroundVehicle vehicle)
        {
            UnitPart hull = vehicle != null ? vehicle.GetComponent<UnitPart>() : null;
            return hull != null ? Mathf.Clamp01(hull.hitPoints / 100f) : 1f;
        }

        public static float AmmoShare(GroundVehicle vehicle)
        {
            if (vehicle == null || vehicle.weaponStations == null || vehicle.weaponStations.Count == 0) return 1f;
            AmmoValue ammo = vehicle.GetAmmoValue();
            return ammo.Total > 0f ? Mathf.Clamp01(ammo.Current / ammo.Total) : 1f;
        }

        public static string Describe(Result result, VehicleDefinition definition)
        {
            switch (result)
            {
                case Result.Purchased: return (definition != null ? definition.unitName : "Vehicle") + " is leaving the depot";
                case Result.NoFaction: return "Join a faction to purchase vehicles";
                case Result.DonationsDisabled: return "Purchasing vehicles is disabled in this mission";
                case Result.DepotUnavailable: return "This depot is not available";
                case Result.DepotBusy: return "Depot is busy, try again in a few seconds";
                case Result.NoSpace: return "No free space near the airbase tower";
                case Result.Sold: return (definition != null ? definition.unitName : "Vehicle") + " sold";
                case Result.NotYours: return "You can only sell vehicles you bought";
                case Result.TooFar: return "The vehicle is too far from this base";
                case Result.Moving: return "Stop the vehicle to sell it";
                case Result.Occupied: return "Someone is in the vehicle";
                case Result.Cooldown: return "Purchase is not available yet";
                case Result.InsufficientFunds: return "Insufficient funds";
                case Result.Escalation:
                case Result.Rank:
                case Result.Warheads: return definition != null ? RestrictionText(result, definition) : "Not available yet";
                default: return "Vehicle purchase is not available";
            }
        }
    }
}
