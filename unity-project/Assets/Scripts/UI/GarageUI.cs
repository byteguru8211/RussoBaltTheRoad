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
    private Text gearDescriptionText;
    private Text priceText;
    private Text creditsText;
    private Text routeStatus;
    private VehiclePortraitUI preview;
    private Button buyButton;
    private string selectedVehicle = "russo_balt_c24_55";
    private string selectedTrack;
    private readonly Dictionary<string, Button> vehicleButtons = new Dictionary<string, Button>();
    private readonly Dictionary<string, Button> trackButtons = new Dictionary<string, Button>();
    private readonly Dictionary<string, Text> trackStateLabels = new Dictionary<string, Text>();

    private readonly List<VehicleCard> vehicles = new List<VehicleCard>
    {
        new VehicleCard { id = "russo_balt_c24_55", name = "Russo-Balt C24/55", year = "1912", power = "55 л.с.", mass = "1300 кг", drive = "Задний", gearbox = "4 передачи", gearDescription = "Четыре ступени · ручная сцеплением · отдельный задний ход", image = "RussoBalt", price = 0 },
        new VehicleCard { id = "benz_1912_racer_concept", name = "Blitzen Benz", year = "1911", power = "200 л.с.", mass = "1450 кг", drive = "Цепь", gearbox = "4 передачи", gearDescription = "Четыре ступени · цепной привод · ручная коробка", image = "Benz", price = 2500 },
        new VehicleCard { id = "ford_model_t_1912", name = "Ford Model T", year = "1912", power = "20 л.с.", mass = "544 кг", drive = "Задний", gearbox = "Планетарная", gearDescription = "Две передачи и задний ход · педали сцепления и планетарный механизм", image = "Era", price = 0 },
        new VehicleCard { id = "fiat_s76_1910", name = "Fiat S76", year = "1910", power = "290–300 л.с.", mass = "1650 кг", drive = "Двойная цепь", gearbox = "4 передачи", gearDescription = "Четыре ступени · две ведущие цепи · ручная коробка", image = "Era", price = 5000 },
        new VehicleCard { id = "mercedes_37_90_1912", name = "Mercedes 37/90", year = "1912", power = "90 л.с.", mass = "1950 кг", drive = "Закрытая цепь", gearbox = "4 передачи", gearDescription = "Четыре ступени · закрытая цепь · ручная коробка", image = "Era", price = 7500 }
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
        public string gearDescription;
        public string image;
        public int price;
    }

    private void Start()
    {
        selectedVehicle = GameManager.Instance.SelectedVehicleId;
        selectedTrack = GameManager.Instance.SelectedTrackId;
        Build();
    }

    private void Build()
    {
        canvas = UIUtils.CreateCanvas("GarageCanvas", 20);
        UIUtils.Background(canvas.transform, "UI/Era", Color.white);
        UIUtils.Scrim(canvas.transform, "Shade", new Color(0.015f, 0.030f, 0.024f, 0.88f), Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
        UIUtils.Button(canvas.transform, "Back", "←  Назад", new Vector2(0.04f, 0.89f), new Vector2(0.15f, 0.97f), Vector2.zero, Vector2.zero, () => SceneManager.LoadScene("MainMenu"), 24);
        UIUtils.Text(canvas.transform, "Title", "Гараж", 54, UITheme.Cream, TextAnchor.MiddleLeft, new Vector2(0.17f, 0.88f), new Vector2(0.55f, 0.97f), Vector2.zero, Vector2.zero, UITheme.TitleFont);
        creditsText = UIUtils.Text(canvas.transform, "Credits", "", 24, UITheme.Brass, TextAnchor.MiddleRight, new Vector2(0.61f, 0.89f), new Vector2(0.83f, 0.96f), Vector2.zero, Vector2.zero);
        UIUtils.Button(canvas.transform, "Profile", "Профиль", new Vector2(0.85f, 0.89f), new Vector2(0.96f, 0.97f), Vector2.zero, Vector2.zero, () => ProfileUI.Show(canvas), 22);
        UIUtils.Panel(canvas.transform, "HeaderRule", UITheme.Brass, new Vector2(0.04f, 0.865f), new Vector2(0.96f, 0.87f), Vector2.zero, Vector2.zero, false);

        UIUtils.Text(canvas.transform, "GarageLabel", "АВТОМОБИЛИ  /  1912", 20, UITheme.Brass, TextAnchor.MiddleLeft, new Vector2(0.04f, 0.815f), new Vector2(0.29f, 0.855f), Vector2.zero, Vector2.zero);
        float top = 0.805f;
        foreach (VehicleCard card in vehicles)
        {
            Button button = UIUtils.Button(canvas.transform, card.id, $"{card.year}  ·  {card.name}", new Vector2(0.04f, top - 0.075f), new Vector2(0.29f, top), Vector2.zero, Vector2.zero, () => SelectVehicle(card), 20);
            Text label = button.transform.Find("Label").GetComponent<Text>();
            label.alignment = TextAnchor.MiddleLeft;
            label.rectTransform.anchorMin = new Vector2(0.21f, 0.43f);
            label.rectTransform.anchorMax = new Vector2(0.98f, 0.96f);
            label.rectTransform.offsetMin = new Vector2(0f, 0f);
            label.rectTransform.offsetMax = new Vector2(-8f, 0f);
            RectTransform portraitRect = UIUtils.Rect(button.transform, "Portrait", new Vector2(0.01f, 0.07f), new Vector2(0.19f, 0.93f), Vector2.zero, Vector2.zero);
            VehiclePortraitUI portrait = portraitRect.gameObject.AddComponent<VehiclePortraitUI>();
            portrait.SetVehicle(card.id);
            UIUtils.Text(button.transform, "Spec", $"{card.power}  ·  {card.gearbox}", 14, UITheme.Muted, TextAnchor.MiddleLeft, new Vector2(0.21f, 0.06f), new Vector2(0.98f, 0.41f), Vector2.zero, Vector2.zero);
            vehicleButtons[card.id] = button;
            top -= 0.088f;
        }

        UIUtils.Card(canvas.transform, "Preview", UITheme.Dark, new Vector2(0.32f, 0.43f), new Vector2(0.66f, 0.81f), Vector2.zero, Vector2.zero);
        UIUtils.Text(canvas.transform, "PreviewKicker", "РИГСКАЯ МАСТЕРСКАЯ  ·  ВАШ АВТОМОБИЛЬ", 15, UITheme.Brass, TextAnchor.MiddleLeft, new Vector2(0.34f, 0.765f), new Vector2(0.64f, 0.795f), Vector2.zero, Vector2.zero);
        RectTransform previewRect = UIUtils.Rect(canvas.transform, "VehiclePortrait", new Vector2(0.35f, 0.46f), new Vector2(0.63f, 0.75f), Vector2.zero, Vector2.zero);
        preview = previewRect.gameObject.AddComponent<VehiclePortraitUI>();
        UIUtils.Panel(canvas.transform, "PreviewRule", new Color(0.77f, 0.63f, 0.36f, 0.32f), new Vector2(0.35f, 0.445f), new Vector2(0.63f, 0.448f), Vector2.zero, Vector2.zero, false);
        vehicleName = UIUtils.Text(canvas.transform, "VehicleName", "", 34, UITheme.Cream, TextAnchor.MiddleLeft, new Vector2(0.32f, 0.355f), new Vector2(0.66f, 0.415f), Vector2.zero, Vector2.zero, UITheme.TitleFont);

        UIUtils.Card(canvas.transform, "Specs", UITheme.Panel, new Vector2(0.68f, 0.40f), new Vector2(0.96f, 0.81f), Vector2.zero, Vector2.zero);
        vehicleStats = UIUtils.Text(canvas.transform, "VehicleStats", "", 21, UITheme.Cream, TextAnchor.UpperLeft, new Vector2(0.70f, 0.54f), new Vector2(0.94f, 0.77f), Vector2.zero, Vector2.zero);
        gearDescriptionText = UIUtils.Text(canvas.transform, "GearDescription", "", 17, UITheme.Muted, TextAnchor.UpperLeft, new Vector2(0.70f, 0.425f), new Vector2(0.94f, 0.525f), Vector2.zero, Vector2.zero);
        priceText = UIUtils.Text(canvas.transform, "Price", "", 20, UITheme.Brass, TextAnchor.MiddleLeft, new Vector2(0.70f, 0.405f), new Vector2(0.82f, 0.46f), Vector2.zero, Vector2.zero);
        buyButton = UIUtils.Button(canvas.transform, "SelectBuy", "Выбрать", new Vector2(0.82f, 0.405f), new Vector2(0.95f, 0.47f), Vector2.zero, Vector2.zero, SelectOrBuy, 21);

        UIUtils.Text(canvas.transform, "TrackTitle", "МАРШРУТ", 20, UITheme.Brass, TextAnchor.MiddleLeft, new Vector2(0.04f, 0.31f), new Vector2(0.30f, 0.35f), Vector2.zero, Vector2.zero);
        routeStatus = UIUtils.Text(canvas.transform, "RouteStatus", "", 18, UITheme.Muted, TextAnchor.MiddleRight, new Vector2(0.34f, 0.31f), new Vector2(0.96f, 0.35f), Vector2.zero, Vector2.zero);
        List<TrackData> tracks = GameManager.GetTracks();
        for (int i = 0; i < tracks.Count; i++)
        {
            int column = i % 4;
            int row = i / 4;
            float left = 0.04f + column * 0.15f;
            float bottom = 0.225f - row * 0.062f;
            AddTrackButton(tracks[i], new Vector2(left, bottom), new Vector2(left + 0.14f, bottom + 0.052f));
        }
        UIUtils.Button(canvas.transform, "Start", "НА СТАРТ  →", new Vector2(0.68f, 0.055f), new Vector2(0.96f, 0.14f), Vector2.zero, Vector2.zero, StartRace, 30);
        UIUtils.Button(canvas.transform, "Modes", "Все форматы заезда", new Vector2(0.32f, 0.055f), new Vector2(0.64f, 0.14f), Vector2.zero, Vector2.zero, () =>
        {
            GameManager.Instance.SelectedTrackId = selectedTrack;
            RaceSelectionUI.Show(canvas);
        }, 20);
        UIUtils.Text(canvas.transform, "Footer", "RUSSO-BALT THE ROAD  ·  1909—1914", 17, UITheme.Muted, TextAnchor.MiddleLeft, new Vector2(0.04f, 0.055f), new Vector2(0.28f, 0.10f), Vector2.zero, Vector2.zero);
        UpdateSelection();
    }

    private void AddTrackButton(TrackData track, Vector2 min, Vector2 max)
    {
        Button button = UIUtils.Button(canvas.transform, track.id, track.displayName, min, max, Vector2.zero, Vector2.zero, () => SelectTrack(track.id), 13);
        Text label = button.transform.Find("Label").GetComponent<Text>();
        label.rectTransform.anchorMin = new Vector2(0f, 0.43f);
        label.rectTransform.anchorMax = new Vector2(1f, 0.96f);
        UIUtils.Text(button.transform, "Subtitle", track.Location, 9, UITheme.Muted, TextAnchor.MiddleLeft, new Vector2(0f, 0.06f), new Vector2(1f, 0.41f), new Vector2(8f, 0f), new Vector2(-8f, 0f));
        Text state = UIUtils.Text(button.transform, "State", "", 9, UITheme.Brass, TextAnchor.MiddleRight, new Vector2(0.48f, 0.43f), new Vector2(0.97f, 0.91f), Vector2.zero, Vector2.zero);
        trackButtons[track.id] = button;
        trackStateLabels[track.id] = state;
    }

    private void SelectVehicle(VehicleCard card)
    {
        selectedVehicle = card.id;
        UpdateSelection();
    }

    private void SelectTrack(string id)
    {
        if (!GameManager.Instance.IsTrackUnlocked(id))
        {
            routeStatus.text = GameManager.Instance.GetTrackUnlockRequirement(id);
            routeStatus.color = UITheme.Danger;
            return;
        }
        selectedTrack = id;
        GameManager.Instance.SelectedTrackId = id;
        TrackData track = GameManager.LoadTrack(id);
        routeStatus.text = $"{track.Location}  ·  {RaceTypeInfo.DisplayName(GameManager.Instance.SelectedRaceType)}";
        routeStatus.color = UITheme.Muted;
        UpdateSelection();
    }

    private void UpdateSelection()
    {
        foreach (VehicleCard card in vehicles)
        {
            if (!vehicleButtons.TryGetValue(card.id, out Button button)) continue;
            bool selected = card.id == selectedVehicle;
            bool unlocked = GameManager.Instance.IsVehicleUnlocked(card.id);
            UIUtils.SetButtonColor(button, selected ? UITheme.Brass : UITheme.Panel);
            button.transform.Find("Label").GetComponent<Text>().color = selected ? UITheme.Dark : UITheme.Cream;
            button.transform.Find("Spec").GetComponent<Text>().color = selected ? UITheme.Dark : UITheme.Muted;
            button.transform.Find("Spec").GetComponent<Text>().text = unlocked ? $"{card.power}  ·  {card.gearbox}" : $"{card.power}  ·  {card.price} ₽";
        }
        foreach (var pair in trackButtons)
        {
            bool selected = pair.Key == selectedTrack;
            bool unlocked = GameManager.Instance.IsTrackUnlocked(pair.Key);
            UIUtils.SetButtonColor(pair.Value, selected && unlocked ? UITheme.Brass : UITheme.Panel);
            pair.Value.transform.Find("Label").GetComponent<Text>().color = unlocked ? (selected ? UITheme.Dark : UITheme.Cream) : UITheme.Muted;
            pair.Value.transform.Find("Subtitle").GetComponent<Text>().color = unlocked ? (selected ? UITheme.Dark : UITheme.Muted) : UITheme.Muted;
            trackStateLabels[pair.Key].text = unlocked ? (selected ? "ВЫБРАН" : "ОТКРЫТ") : "ЗАКРЫТО";
            trackStateLabels[pair.Key].color = unlocked ? (selected ? UITheme.Dark : UITheme.Brass) : UITheme.Danger;
        }

        VehicleCard selectedCard = vehicles.Find(x => x.id == selectedVehicle);
        if (selectedCard == null) return;
        bool unlockedVehicle = GameManager.Instance.IsVehicleUnlocked(selectedCard.id);
        vehicleName.text = $"{selectedCard.name} · {selectedCard.year}";
        vehicleStats.text = $"Мощность       {selectedCard.power}\nМасса            {selectedCard.mass}\nПривод           {selectedCard.drive}\nКоробка         {selectedCard.gearbox}\nСостояние     {(unlockedVehicle ? "Исправна" : "Требует покупки")}";
        gearDescriptionText.text = selectedCard.gearDescription;
        priceText.text = unlockedVehicle ? "В гараже" : $"{selectedCard.price} рублей";
        buyButton.transform.Find("Label").GetComponent<Text>().text = unlockedVehicle ? (GameManager.Instance.SelectedVehicleId == selectedCard.id ? "Выбрана" : "Выбрать") : "Купить";
        creditsText.text = $"Рубли: {GameManager.Instance.Profile.credits:F0}";
        preview.SetVehicle(selectedCard.id);
        if (string.IsNullOrEmpty(routeStatus.text))
        {
            TrackData track = GameManager.LoadTrack(selectedTrack);
            routeStatus.text = track != null ? $"{track.Location}  ·  {RaceTypeInfo.DisplayName(GameManager.Instance.SelectedRaceType)}" : "";
            routeStatus.color = UITheme.Muted;
        }
    }

    private void SelectOrBuy()
    {
        VehicleCard card = vehicles.Find(x => x.id == selectedVehicle);
        if (card == null) return;
        if (!GameManager.Instance.IsVehicleUnlocked(card.id))
        {
            if (GameManager.Instance.Profile.credits < card.price)
            {
                priceText.text = "Недостаточно рублей";
                priceText.color = UITheme.Danger;
                return;
            }
            GameManager.Instance.Profile.credits -= card.price;
            GameManager.Instance.Profile.unlockedVehicles.Add(card.id);
            GameManager.Instance.Save();
        }
        GameManager.Instance.SelectVehicle(card.id);
        priceText.color = UITheme.Brass;
        UpdateSelection();
    }

    private void StartRace()
    {
        if (!GameManager.Instance.IsVehicleUnlocked(selectedVehicle))
        {
            priceText.text = "Сначала купите автомобиль";
            priceText.color = UITheme.Danger;
            return;
        }
        if (!GameManager.Instance.IsTrackUnlocked(selectedTrack))
        {
            routeStatus.text = GameManager.Instance.GetTrackUnlockRequirement(selectedTrack);
            routeStatus.color = UITheme.Danger;
            return;
        }
        GameManager.Instance.SelectedVehicleId = selectedVehicle;
        GameManager.Instance.SelectRace(selectedTrack, GameManager.Instance.SelectedRaceType);
        GameManager.Instance.ResetRaceTransientState();
        SceneManager.LoadScene("PreRace");
    }
}
