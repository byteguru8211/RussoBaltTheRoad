using UnityEngine;

public static class RegionalLandmarks
{
    public static void Build(TrackData track, TrackBuilder route, Transform parent)
    {
        if (track == null || route == null || parent == null) return;
        switch (track.id)
        {
            case "alps_pass":
                Place("Building_Alpine_Chalet_01", 0.16f, -1f, 10f, track, route, parent);
                Place("Prop_Alpine_Cliff_01", 0.38f, 1f, 9f, track, route, parent);
                Place("Prop_Alpine_Cliff_01", 0.66f, -1f, 11f, track, route, parent);
                Place("Prop_Stone_Bridge_01", 0.84f, 1f, 8f, track, route, parent);
                break;
            case "monaco_finish":
                Place("Building_Coastal_Cottage_01", 0.18f, -1f, 9f, track, route, parent);
                Place("Prop_Mediterranean_Cypress_01", 0.34f, 1f, 7f, track, route, parent);
                Place("Prop_Roadside_Shrine_01", 0.58f, -1f, 6f, track, route, parent);
                Place("Prop_Cobbled_Road_10m_01", 0.74f, 1f, 5f, track, route, parent);
                break;
            case "crimean_coast":
                Place("Building_Coastal_Cottage_01", 0.14f, 1f, 9f, track, route, parent);
                Place("Prop_Vineyard_Rows_01", 0.32f, -1f, 10f, track, route, parent);
                Place("Prop_Mediterranean_Cypress_01", 0.51f, 1f, 7f, track, route, parent);
                Place("Prop_Roadside_Shrine_01", 0.72f, -1f, 6f, track, route, parent);
                Place("Prop_Stone_Bridge_01", 0.88f, 1f, 8f, track, route, parent);
                break;
            case "finland_coast":
                Place("Building_Coastal_Cottage_01", 0.2f, 1f, 10f, track, route, parent);
                Place("Prop_Reed_Cluster_01", 0.39f, -1f, 6f, track, route, parent);
                Place("Prop_Reed_Cluster_01", 0.61f, 1f, 6f, track, route, parent);
                Place("Prop_Roadside_Shrine_01", 0.82f, -1f, 7f, track, route, parent);
                break;
            case "riga_speedway":
                Place("Prop_Race_Grandstand_01", 0.08f, -1f, 13f, track, route, parent);
                Place("Building_Coastal_Cottage_01", 0.27f, 1f, 11f, track, route, parent);
                Place("Prop_Roadside_Shrine_01", 0.53f, -1f, 7f, track, route, parent);
                Place("Prop_Race_Grandstand_01", 0.91f, 1f, 13f, track, route, parent);
                break;
            case "tver_forest":
                Place("Building_Alpine_Chalet_01", 0.17f, -1f, 11f, track, route, parent);
                Place("Prop_Reed_Cluster_01", 0.43f, 1f, 7f, track, route, parent);
                Place("Prop_Roadside_Shrine_01", 0.69f, -1f, 7f, track, route, parent);
                break;
            case "moscow_highway":
                Place("Building_Coastal_Cottage_01", 0.22f, 1f, 10f, track, route, parent);
                Place("Prop_Roadside_Shrine_01", 0.47f, -1f, 7f, track, route, parent);
                Place("Prop_Cobbled_Road_10m_01", 0.76f, 1f, 6f, track, route, parent);
                break;
        }
    }

    private static void Place(string assetName, float progress, float side, float clearance, TrackData track, TrackBuilder route, Transform parent)
    {
        progress = Mathf.Repeat(progress, 1f);
        Vector3 point = route.PointAt(progress);
        Vector3 right = route.RightAt(progress);
        Vector3 position = point + right * side * (track.roadWidth * 0.5f + clearance);
        Physics.SyncTransforms();
        float groundHeight = point.y;
        if (Physics.Raycast(position + Vector3.up * 80f, Vector3.down, out RaycastHit hit, 160f, ~0, QueryTriggerInteraction.Ignore))
            groundHeight = hit.point.y;
        Vector3 tangent = route.TangentAt(progress);
        tangent.y = 0f;
        if (tangent.sqrMagnitude < 0.001f) tangent = Vector3.forward;
        Quaternion rotation = Quaternion.LookRotation(tangent.normalized, Vector3.up);
        GameObject instance = RegionalSceneryRuntime.Instantiate(assetName, parent, position, rotation);
        if (instance != null) TrackSceneryBuilder.GroundInstance(instance, groundHeight);
    }
}
