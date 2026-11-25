using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class GarageUI : MonoBehaviour
{
    private Canvas canvas;
    private Text vehicleName;
    private Text vehicleStats;
    private Text priceText;
    private Text creditsText;
    private Image preview;
    private Button buyButton;
    private string selectedVehicle = "russo_balt_c24_55";
    private string selectedTrack = "winter_departure";
    private readonly Dictionary<string, Button> vehicleButtons = new Dictionary<string, Button>();
    private readonly Dictionary<string, Button> trackButtons = new Dictionary<string, Button>();

    private readonly List<VehicleCard> vehicles = new List<VehicleCard>
    {
        new VehicleCard { id = "russo_balt_c24_55", name = "Russo-Balt C24/55", year = "1912", power = "55 л.с.", mass = "1300 кг", drive = "Задний", gearbox = "3 передачи", image = "RussoBalt", price = 0 },
        new VehicleCard { id = "benz_1912_racer_concept", name = "Blitzen Benz", year = "1911", power = "200 л.с.", mass = "1450 кг", drive = "Цепь", gearbox = "4 передачи", image = "Benz", price = 2500 },
        new VehicleCard { id = "ford_model_t_1912", name = "Ford Model T", year = "1912", power = "20 л.с.", mass = "544 кг", drive = "Задний", gearbox = "Планетарная", image = "Era", price = 0 },
        new VehicleCard { id = "fiat_s76_1910", name = "Fiat S76", year = "1910", power = "290–300 л.с.", mass = "1650 кг", drive = "Двойная цепь", gearbox = "4 передачи", image = "Era", price = 5000 },
        new VehicleCard { id = "mercedes_37_90_1912", name = "Mercedes 37/90", year = "1912", power = "90 л.с.", mass = "1950 кг", drive = "Закрытая цепь", gearbox = "4 передачи", image = "Era", price = 7500 }
    };

    [Serializable]
    private class VehicleCard
    {
        public string id;
        public string name;
        public string year;
        public string power;
        public string mass;
        public string drive;
        public string gearbox;
        public string image;
        public int price;
    }

    private void Start()
    {
        selectedVehicle = GameManager.Instance.SelectedVehicleId;
        Build();
    }

    private void Build()
    {
        canvas = UIUtils.CreateCanvas("GarageCanvas", 20);
        UIUtils.Background(canvas.transform, "UI/Era", Color.white);
        UIUtils.Scrim(canvas.transform, "Shade", new Color(0.015f, 0.030f, 0.024f, 0.84f), Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
        UIUtils.Button(canvas.transform, "Back", "←  Назад", new Vector2(0.04f, 0.89f), new Vector2(0.15f, 0.97f), Vector2.zero, Vector2.zero, () => SceneManager.LoadScene("MainMenu"), 24);
        UIUtils.Text(canvas.transform, "Title", "Гараж", 54, UITheme.Cream, TextAnchor.MiddleLeft, new Vector2(0.17f, 0.88f), new Vector2(0.55f, 0.97f), Vector2.zero, Vector2.zero, UITheme.TitleFont);
        creditsText = UIUtils.Text(canvas.transform, "Credits", "", 24, UITheme.Brass, TextAnchor.MiddleRight, new Vector2(0.68f, 0.89f), new Vector2(0.84f, 0.96f), Vector2.zero, Vector2.zero);
        UIUtils.Button(canvas.transform, "Profile", "Профиль", new Vector2(0.85f, 0.89f), new Vector2(0.96f, 0.97f), Vector2.zero, Vector2.zero, () => ProfileUI.Show(canvas), 22);
        UIUtils.Panel(canvas.transform, "HeaderRule", UITheme.Brass, new Vector2(0.04f, 0.865f), new Vector2(0.96f, 0.87f), Vector2.zero, Vector2.zero, false);

        UIUtils.Text(canvas.transform, "GarageLabel", "АВТОМОБИЛИ", 20, UITheme.Brass, TextAnchor.MiddleLeft, new Vector2(0.04f, 0.81f), new Vector2(0.29f, 0.85f), Vector2.zero, Vector2.zero);
        float top = 0.80f;
        foreach (VehicleCard card in vehicles)
        {
            Button button = UIUtils.Button(canvas.transform, card.id, $"{card.year}   {card.name}", new Vector2(0.04f, top - 0.088f), new Vector2(0.29f, top), Vector2.zero, Vector2.zero, () => SelectVehicle(card), 23);
            vehicleButtons[card.id] = button;
            top -= 0.103f;
        }

        preview = UIUtils.Card(canvas.transform, "Preview", UITheme.Dark, new Vector2(0.32f, 0.40f), new Vector2(0.65f, 0.81f), Vector2.zero, Vector2.zero);
        vehicleName = UIUtils.Text(canvas.transform, "VehicleName", "", 36, UITheme.Cream, TextAnchor.MiddleLeft, new Vector2(0.32f, 0.33f), new Vector2(0.65f, 0.39f), Vector2.zero, Vector2.zero, UITheme.TitleFont);
        UIUtils.Card(canvas.transform, "Specs", UITheme.Panel, new Vector2(0.68f, 0.40f), new Vector2(0.96f, 0.81f), Vector2.zero, Vector2.zero);
        vehicleStats = UIUtils.Text(canvas.transform, "VehicleStats", "", 24, UITheme.Cream, TextAnchor.UpperLeft, new Vector2(0.70f, 0.43f), new Vector2(0.94f, 0.77f), Vector2.zero, Vector2.zero);
        priceText = UIUtils.Text(canvas.transform, "Price", "", 22, UITheme.Brass, TextAnchor.MiddleLeft, new Vector2(0.70f, 0.405f), new Vector2(0.82f, 0.46f), Vector2.zero, Vector2.zero);
        buyButton = UIUtils.Button(canvas.transform, "SelectBuy", "Выбрать", new Vector2(0.82f, 0.405f), new Vector2(0.95f, 0.47f), Vector2.zero, Vector2.zero, SelectOrBuy, 22);

        UIUtils.Text(canvas.transform, "TrackTitle", "МАРШРУТ", 20, UITheme.Brass, TextAnchor.MiddleLeft, new Vector2(0.04f, 0.275f), new Vector2(0.30f, 0.32f), Vector2.zero, Vector2.zero);
        AddTrackButton("winter_departure", "Зимний выезд", "Россия · 1912", new Vector2(0.04f, 0.17f), new Vector2(0.30f, 0.26f));
        AddTrackButton("alps_pass", "Альпийский перевал", "Европа · 1912", new Vector2(0.34f, 0.17f), new Vector2(0.60f, 0.26f));
        AddTrackButton("monaco_finish", "Финиш в Монако", "Монако · 1912", new Vector2(0.64f, 0.17f), new Vector2(0.90f, 0.26f));
        UIUtils.Button(canvas.transform, "Start", "НА СТАРТ  →", new Vector2(0.68f, 0.055f), new Vector2(0.96f, 0.14f), Vector2.zero, Vector2.zero, StartRace, 30);
        UIUtils.Text(canvas.transform, "Footer", "RUSSO-BALT THE ROAD  ·  1909—1914", 17, UITheme.Muted, TextAnchor.MiddleLeft, new Vector2(0.04f, 0.055f), new Vector2(0.55f, 0.10f), Vector2.zero, Vector2.zero);
        UpdateSelection();
    }

    private void AddTrackButton(string id, string label, string subtitle, Vector2 min, Vector2 max)
    {
        Button button = UIUtils.Button(canvas.transform, id, label, min, max, Vector2.zero, Vector2.zero, () => SelectTrack(id), 23);
        UIUtils.Text(button.transform, "Subtitle", subtitle, 17, UITheme.Muted, TextAnchor.MiddleLeft, new Vector2(0f, 0f), new Vector2(1f, 1f), new Vector2(20f, 38f), new Vector2(-20f, 10f));
        trackButtons[id] = button;
    }

    private void SelectVehicle(VehicleCard card)
    {
        selectedVehicle = card.id;
        UpdateSelection();
    }

    private void SelectTrack(string id)
    {
        if (!GameManager.Instance.Profile.unlockedTracks.Contains(id))
            return;
        selectedTrack = id;
        GameManager.Instance.SelectedTrackId = id;
        UpdateSelection();
    }

    private void UpdateSelection()
    {
        foreach (VehicleCard card in vehicles)
        {
            if (!vehicleButtons.TryGetValue(card.id, out Button button)) continue;
            bool selected = card.id == selectedVehicle;
            button.image.color = selected ? UITheme.Brass : UITheme.Panel;
            button.transform.Find("Label").GetComponent<Text>().color = selected ? UITheme.Dark : UITheme.Cream;
        }
        foreach (var pair in trackButtons)
        {
            bool selected = pair.Key == selectedTrack;
            pair.Value.image.color = selected ? UITheme.Brass : UITheme.Panel;
            pair.Value.transform.Find("Label").GetComponent<Text>().color = selected ? UITheme.Dark : UITheme.Cream;
        }

        VehicleCard selectedCard = vehicles.Find(x => x.id == selectedVehicle);
        if (selectedCard == null) return;
        bool unlocked = GameManager.Instance.Profile.unlockedVehicles.Contains(selectedCard.id);
        vehicleName.text = $"{selectedCard.name} · {selectedCard.year}";
        vehicleStats.text = $"Мощность     {selectedCard.power}\nМасса          {selectedCard.mass}\nПривод         {selectedCard.drive}\nКоробка       {selectedCard.gearbox}\nСостояние   {(unlocked ? "Исправна" : "Требует покупки")}";
        priceText.text = unlocked ? "В гараже" : $"{selectedCard.price} рублей";
        buyButton.transform.Find("Label").GetComponent<Text>().text = unlocked ? "Выбрать" : "Купить";
        creditsText.text = $"Рубли: {GameManager.Instance.Profile.credits:F0}";
        Sprite sprite = UIUtils.LoadSprite($"UI/{selectedCard.image}");
        preview.sprite = sprite;
        preview.color = sprite != null ? Color.white : UITheme.Dark;
        preview.preserveAspect = true;
    }

    private void SelectOrBuy()
    {
        VehicleCard card = vehicles.Find(x => x.id == selectedVehicle);
        if (card == null) return;
        if (!GameManager.Instance.Profile.unlockedVehicles.Contains(card.id))
        {
            if (GameManager.Instance.Profile.credits < card.price)
            {
                priceText.text = "Недостаточно рублей";
                return;
            }
            GameManager.Instance.Profile.credits -= card.price;
            GameManager.Instance.Profile.unlockedVehicles.Add(card.id);
            GameManager.Instance.Save();
        }
        GameManager.Instance.SelectedVehicleId = card.id;
        UpdateSelection();
    }

    private void StartRace()
    {
        if (!GameManager.Instance.Profile.unlockedVehicles.Contains(selectedVehicle))
        {
            priceText.text = "Сначала купите автомобиль";
            return;
        }
        GameManager.Instance.SelectedVehicleId = selectedVehicle;
        GameManager.Instance.SelectedTrackId = selectedTrack;
        GameManager.Instance.Save();
        SceneManager.LoadScene(selectedTrack == "alps_pass" ? "Race_Alps" : selectedTrack == "monaco_finish" ? "Race_Monaco" : "Race_Winter");
    }
}
