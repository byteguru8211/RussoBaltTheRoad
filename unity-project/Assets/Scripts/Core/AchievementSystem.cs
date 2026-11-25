using System;
using System.Collections.Generic;
using UnityEngine;

[Serializable]
public class AchievementDefinition
{
    public string id;
    public string title;
    public string description;
}

public class AchievementSystem : MonoBehaviour
{
    public static AchievementSystem Instance { get; private set; }
    public List<AchievementDefinition> definitions = new List<AchievementDefinition>
    {
        new AchievementDefinition { id = "first_start", title = "Первый оборот", description = "Запустить двигатель впервые." },
        new AchievementDefinition { id = "first_finish", title = "Дорога begins", description = "Завершить первую гонку." },
        new AchievementDefinition { id = "winner", title = "Победа в гараже", description = "Занять первое место." },
        new AchievementDefinition { id = "hundred_km", title = "Сто километров", description = "Проехать 100 км без полного сброса." },
        new AchievementDefinition { id = "repair", title = "Ремонт на обочине", description = "Выполнить ремонт во время заезда." },
        new AchievementDefinition { id = "alps", title = "Через Альпы", description = "Завершить горный этап." },
        new AchievementDefinition { id = "monaco", title = "Монако", description = "Добраться до финиша экспедиции." }
    };

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
    }

    public void Unlock(string id)
    {
        if (GameManager.Instance == null || GameManager.Instance.Profile.achievements.Contains(id))
            return;
        GameManager.Instance.Profile.achievements.Add(id);
        SaveSystem.SaveProfile(GameManager.Instance.Profile);
        AchievementDefinition definition = definitions.Find(x => x.id == id);
        if (definition != null)
            Debug.Log($"Достижение: {definition.title}");
    }
}
