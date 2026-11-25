using System;
using System.Collections.Generic;
using System.Linq;
using Unity.Netcode;
using UnityEngine;

public static class RaceStandings
{
    public class Standing
    {
        public string Name;
        public float Progress;
        public float GapMeters;
        public bool IsPlayer;
        public int Place;
        public ulong ClientId;
    }

    public static List<Standing> Build(RaceManager race)
    {
        List<Standing> rows = new List<Standing>();
        if (race == null) return rows;
        NetworkPlayerCar[] networkCars = UnityEngine.Object.FindObjectsByType<NetworkPlayerCar>();
        float trackLength = race.TrackBuilder != null ? Mathf.Max(1f, race.TrackBuilder.TrackLength) : 1f;
        if (networkCars.Length > 0)
        {
            foreach (NetworkPlayerCar car in networkCars)
            {
                if (car == null) continue;
                bool isPlayer = car.IsOwner;
                rows.Add(new Standing
                {
                    Name = NetworkName(car, isPlayer),
                    Progress = car.RaceProgress != null ? car.RaceProgress.Value : 0f,
                    IsPlayer = isPlayer,
                    ClientId = car.OwnerClientId
                });
            }
        }
        else
        {
            rows.Add(new Standing
            {
                Name = GameManager.Instance != null ? GameManager.Instance.Profile.playerName : "Гонщик",
                Progress = race.PlayerRaceProgress,
                IsPlayer = true
            });
            string[] fallbackNames = { "Blitzen Benz", "Ford Model T", "Fiat S76", "Mercedes 37/90", "Russo-Balt C24/55", "Benz 1911" };
            for (int i = 0; i < race.AI.Count; i++)
            {
                AIDriver1912 ai = race.AI[i];
                if (ai == null) continue;
                rows.Add(new Standing
                {
                    Name = AiName(ai, fallbackNames[i % fallbackNames.Length]),
                    Progress = ai.RaceProgress,
                    IsPlayer = false
                });
            }
        }

        rows.Sort((a, b) => b.Progress.CompareTo(a.Progress));
        float leaderProgress = rows.Count > 0 ? rows[0].Progress : 0f;
        for (int i = 0; i < rows.Count; i++)
        {
            rows[i].Place = i + 1;
            rows[i].GapMeters = Mathf.Max(0f, leaderProgress - rows[i].Progress) * trackLength;
        }
        return rows;
    }

    public static Standing FindPlayer(List<Standing> rows)
    {
        return rows?.Find(row => row.IsPlayer);
    }

    private static string NetworkName(NetworkPlayerCar car, bool isPlayer)
    {
        if (car.DisplayName != null && !string.IsNullOrWhiteSpace(car.DisplayName.Value.ToString()))
            return car.DisplayName.Value.ToString();
        if (isPlayer && GameManager.Instance != null) return GameManager.Instance.Profile.playerName;
        if (LobbyManager.Instance != null)
        {
            foreach (LobbyEntry entry in LobbyManager.Instance.Entries)
                if (entry.ClientId == car.OwnerClientId && !string.IsNullOrWhiteSpace(entry.Name.ToString()))
                    return entry.Name.ToString();
        }
        return car.OwnerClientId == NetworkManager.Singleton.LocalClientId && GameManager.Instance != null
            ? GameManager.Instance.Profile.playerName
            : $"Пилот {car.OwnerClientId + 1}";
    }

    private static string AiName(AIDriver1912 ai, string fallback)
    {
        if (ai.vehicle == null) return fallback;
        string objectName = ai.vehicle.gameObject.name.Replace("(Clone)", "").Trim();
        foreach (VehicleCatalog.VehicleInfo vehicle in VehicleCatalog.All)
        {
            if (objectName.Equals(vehicle.id, StringComparison.OrdinalIgnoreCase))
                return vehicle.shortName;
        }
        return string.IsNullOrWhiteSpace(objectName) ? fallback : objectName;
    }
}
