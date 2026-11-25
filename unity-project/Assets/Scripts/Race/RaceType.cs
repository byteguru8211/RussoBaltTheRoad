using UnityEngine;

public enum RaceType
{
    Circuit,
    Sprint,
    Checkpoint
}

public static class RaceTypeInfo
{
    public static string DisplayName(RaceType type)
    {
        return type switch
        {
            RaceType.Circuit => "Круговой заезд",
            RaceType.Sprint => "Спринт / TimeTrial",
            RaceType.Checkpoint => "Контрольные точки",
            _ => "Круговой заезд"
        };
    }

    public static string ShortName(RaceType type)
    {
        return type switch
        {
            RaceType.Circuit => "КРУГ",
            RaceType.Sprint => "СПРИНТ",
            RaceType.Checkpoint => "МАРШРУТ",
            _ => "КРУГ"
        };
    }

    public static string Description(RaceType type)
    {
        return type switch
        {
            RaceType.Circuit => "Соперники и полный дистанционный заезд с кругами.",
            RaceType.Sprint => "Один быстрый круг против соперников; важен личный рекорд.",
            RaceType.Checkpoint => "Маршрут по контрольным точкам без соперников и с лимитом времени.",
            _ => ""
        };
    }

    public static int Laps(TrackData track, RaceType type)
    {
        if (track == null) return 1;
        return type == RaceType.Circuit ? Mathf.Max(1, track.laps) : 1;
    }

    public static int AiCount(TrackData track, RaceType type)
    {
        if (track == null) return 0;
        return type == RaceType.Checkpoint ? 0 : Mathf.Max(0, track.aiCount);
    }
}
