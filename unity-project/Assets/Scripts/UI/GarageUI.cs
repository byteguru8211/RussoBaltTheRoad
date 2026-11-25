using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class GarageUI : MonoBehaviour
{
    private Canvas canvas;
    private Text titleText;
    private Text statsText;
    private Text priceText;
    private Image preview;
    private string selectedVehicle = "russo_balt_c24_55";
    private string selectedTrack = "winter_departure";
    private Color selectedColor = UITheme.Brass;
    private Color normalColor = UITheme.Panel;

    private readonly List<VehicleCard> vehicles = new List<VehicleCard>
    {
        new VehicleCard { id = "russo_balt_c24_55", name = "Russo-Balt C24/55", year = "1912", stats = "55 л.с. · 1300 кг · RWD · 3 передачи", image = "RussoBalt", price = 0 },
        new VehicleCard { id = "benz_1912_racer_concept", name = "Blitzen Benz", year = "1911", stats = "200 л.с. · 1450 кг · цепной привод · 4 передачи", image = "Benz", price = 2500 },
        new VehicleCard { id = "ford_model_t_1912", name = "Ford Model T", year = "1912", stats = "20 л.с. · 544 кг · планетарная КПП", image = "Era", price = 0 },
        new VehicleCard { id = "fiat_s76_1910", name = "Fiat S76", year = "1910", stats = "290–300 л.с. · 1650 кг · двойная цепь", image = "Era", price = 5000 },
        new VehicleCard { id = "mercedes_37_90_1912", name = "Mercedes 37/90", year = "1912", stats = "90 л.с. · 1950 кг · double phaeton", image = "Era", price = 7500 }
    };

    [Serializable]
    private class VehicleCard
    {
        public string id;
        public string name;
        public string year;
        public string stats;
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
        canvas = UIUtils.CreateCanvas("GarageCanvas", 15);
        UIUtils.Background(canvas.transform, "UI/Era", new Color(0.20f, 0.25f, 0.22f, 1f));
        UIUtils.Panel(canvas.transform, "Shade", new Color(0.01f, 0.02f, 0.015f, 0.72f), Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
        UIUtils.Button(canvas.transform, "Back", "Назад", new Vector2(0.03f, 0.89f), new Vector2(0.14f, 0.97f), Vector2.zero, Vector2.zero, () => SceneManager.LoadScene("MainMenu"), 22);
        UIUtils.Text(canvas.transform, "Title", "Гараж", 54, UITheme.Cream, TextAnchor.MiddleLeft, new Vector2(0.17f, 0.87f), new Vector2(0.60f, 0.97f), Vector2.zero, Vector2.zero);
        UIUtils.Text(canvas.transform, "Credits", $"Рубли: {GameManager.Instance.Profile.credits:F0}", 24, UITheme.Brass, TextAnchor.MiddleRight, new Vector2(0.72f, 0.88f), new Vector2(0.96f, 0.96f), Vector2.zero, Vector2.zero);

        preview = UIUtils.Panel(canvas.transform, "Preview", UITheme.Dark, new Vector2(0.52f, 0.34f), new Vector2(0.95f, 0.82f), Vector2.zero, Vector2.zero);
        titleText = UIUtils.Text(canvas.transform, "VehicleName", "", 34, UITheme.Cream, TextAnchor.MiddleLeft, new Vector2(0.54f, 0.25f), new Vector2(0.94f, 0.33f), Vector2.zero, Vector2.zero);
        statsText = UIUtils.Text(canvas.transform, "VehicleStats", "", 22, UITheme.Muted, TextAnchor.MiddleLeft, new Vector2(0.54f, 0.17f), new Vector2(0.94f, 0.25f), Vector2.zero, Vector2.zero);
        priceText = UIUtils.Text(canvas.transform, "Price", "", 21, UITheme.Brass, TextAnchor.MiddleLeft, new Vector2(0.54f, 0.10f), new Vector2(0.75f, 0.16f), Vector2.zero, Vector2.zero);
        UIUtils.Button(canvas.transform, "SelectBuy", "Выбрать / купить", new Vector2(0.76f, 0.08f), new Vector2(0.95f, 0.17f), Vector2.zero, Vector2.zero, SelectOrBuy, 21);

        float y = 0.78f;
        foreach (VehicleCard card in vehicles)
        {
            bool selected = card.id == selectedVehicle;
            Button button = UIUtils.Button(canvas.transform, card.id, $"{card.year}  {card.name}", new Vector2(0.04f, y - 0.085f), new Vector2(0.47f, y), Vector2.zero, Vector2.zero, () => SelectVehicle(card), 22);
            button.image.color = selected ? selectedColor : normalColor;
            button.transform.Find("Label").GetComponent<Text>().color = selected ? UITheme.Dark : UITheme.Cream;
            y -= 0.105f;
        }

        UIUtils.Text(canvas.transform, "TrackTitle", "Маршрут", 28, UITheme.Brass, TextAnchor.MiddleLeft, new Vector2(0.04f, 0.19f), new Vector2(0.47f, 0.25f), Vector2.zero, Vector2.zero);
        AddTrackButton("winter_departure", "Зимний выезд", 0.13f);
        AddTrackButton("alps_pass", "Альпийский перевал", 0.055f);
        AddTrackButton("monaco_finish", "Финиш в Монако", -0.02f);
        UIUtils.Button(canvas.transform, "Start", "На старт", new Vector2(0.54f, 0.01f), new Vector2(0.95f, 0.09f), Vector2.zero, Vector2.zero, StartRace, 28);
        UpdateCard();
    }

    private void AddTrackButton(string id, string label, float y)
    {
        bool unlocked = GameManager.Instance.Profile.unlockedTracks.Contains(id);
        Button button = UIUtils.Button(canvas.transform, id, (unlocked ? "" : "Закрыто · ") + label, new Vector2(0.04f, y), new Vector2(0.47f, y + 0.065f), Vector2.zero, Vector2.zero, () =>
        {
            if (unlocked)
            {
                selectedTrack = id;
                GameManager.Instance.SelectedTrackId = id;
                Destroy(canvas.gameObject);
                Build();
            }
        }, 20);
        button.image.color = selectedTrack == id ? selectedColor : normalColor;
        button.transform.Find("Label").GetComponent<Text>().color = selectedTrack == id ? UITheme.Dark : UITheme.Cream;
    }

    private void SelectVehicle(VehicleCard card)
    {
        selectedVehicle = card.id;
        UpdateCard();
        foreach (VehicleCard other in vehicles)
        {
            Button button = GameObject.Find(other.id)?.GetComponent<Button>();
            if (button == null) continue;
            bool selected = other.id == selectedVehicle;
            button.image.color = selected ? selectedColor : normalColor;
            button.transform.Find("Label").GetComponent<Text>().color = selected ? UITheme.Dark : UITheme.Cream;
        }
    }

    private void UpdateCard()
    {
        VehicleCard card = vehicles.Find(x => x.id == selectedVehicle);
        if (card == null) return;
        titleText.text = $"{card.name} · {card.year}";
        statsText.text = card.stats;
        bool unlocked = GameManager.Instance.Profile.unlockedVehicles.Contains(card.id);
        priceText.text = unlocked ? "В гараже" : $"Цена: {card.price} рублей";
        Texture2D texture = Resources.Load<Texture2D>($"UI/{card.image}");
        if (texture != null)
        {
            Sprite sprite = Sprite.Create(texture, new Rect(0, 0, texture.width, texture.height), new Vector2(0.5f, 0.5f));
            preview.sprite = sprite;
            preview.color = Color.white;
        }
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
        UpdateCard();
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
