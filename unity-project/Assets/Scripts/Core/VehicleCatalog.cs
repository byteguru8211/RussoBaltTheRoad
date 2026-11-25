using System;
using System.Collections.Generic;
using UnityEngine;

public static class VehicleCatalog
{
    [Serializable]
    public class VehicleInfo
    {
        public string id;
        public string name;
        public string shortName;
        public string year;
        public string power;
        public string mass;
        public string drive;
        public string gearbox;
        public string gearDescription;
        public int price;
        public Color accent;
    }

    private static readonly List<VehicleInfo> Vehicles = new List<VehicleInfo>
    {
        new VehicleInfo
        {
            id = "russo_balt_c24_55", name = "Russo-Balt C24/55", shortName = "C24/55", year = "1912",
            power = "55 л.с.", mass = "1300 кг", drive = "Задний", gearbox = "4 передачи",
            gearDescription = "Четыре ступени · ручная сцеплением · отдельный задний ход", price = 0,
            accent = new Color(0.66f, 0.55f, 0.34f)
        },
        new VehicleInfo
        {
            id = "benz_1912_racer_concept", name = "Blitzen Benz", shortName = "BLITZEN", year = "1911",
            power = "200 л.с.", mass = "1450 кг", drive = "Цепь", gearbox = "4 передачи",
            gearDescription = "Четыре ступени · цепной привод · ручная коробка", price = 2500,
            accent = new Color(0.62f, 0.25f, 0.19f)
        },
        new VehicleInfo
        {
            id = "ford_model_t_1912", name = "Ford Model T", shortName = "MODEL T", year = "1912",
            power = "20 л.с.", mass = "544 кг", drive = "Задний", gearbox = "Планетарная",
            gearDescription = "Две передачи и задний ход · педали сцепления и планетарный механизм", price = 0,
            accent = new Color(0.20f, 0.34f, 0.27f)
        },
        new VehicleInfo
        {
            id = "fiat_s76_1910", name = "Fiat S76", shortName = "S76", year = "1910",
            power = "290–300 л.с.", mass = "1650 кг", drive = "Двойная цепь", gearbox = "4 передачи",
            gearDescription = "Четыре ступени · две ведущие цепи · ручная коробка", price = 5000,
            accent = new Color(0.55f, 0.57f, 0.49f)
        },
        new VehicleInfo
        {
            id = "mercedes_37_90_1912", name = "Mercedes 37/90", shortName = "37/90", year = "1912",
            power = "90 л.с.", mass = "1950 кг", drive = "Закрытая цепь", gearbox = "4 передачи",
            gearDescription = "Четыре ступени · закрытая цепь · ручная коробка", price = 7500,
            accent = new Color(0.29f, 0.39f, 0.48f)
        }
    };

    public static IReadOnlyList<VehicleInfo> All => Vehicles;

    public static VehicleInfo Find(string id)
    {
        if (string.IsNullOrWhiteSpace(id)) return null;
        return Vehicles.Find(vehicle => vehicle.id == id);
    }

    public static string DisplayName(string id)
    {
        return Find(id)?.name ?? "Russo-Balt C24/55";
    }

    public static string ShortName(string id)
    {
        return Find(id)?.shortName ?? DisplayName(id);
    }

    public static string Specifications(string id)
    {
        VehicleInfo vehicle = Find(id);
        if (vehicle == null) return "";
        return $"{vehicle.year}  ·  {vehicle.power}\n{vehicle.drive}  ·  {vehicle.gearbox}";
    }
}
