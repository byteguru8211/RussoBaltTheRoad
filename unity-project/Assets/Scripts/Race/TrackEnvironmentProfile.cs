using System;
using UnityEngine;

[CreateAssetMenu(menuName = "RussoBalt/Environment Profile", fileName = "EnvironmentProfile")]
public class TrackEnvironmentProfile : ScriptableObject
{
    public enum BarrierStyle { WoodFence, Guardrail, StonePromenade }

    [Header("Identity")]
    public string id;
    public int seed = 1912;

    [Header("Road and ground")]
    public string roadTexturePrefix = "road_dirt";
    public string groundTexturePrefix = "road_dirt";
    public string snowTexturePrefix = "road_snow";
    public float roadTileMeters = 4f;
    public float groundTileMeters = 8f;
    public float shoulderWidth = 1.25f;
    public float curbWidth = 0.34f;
    public float curbHeight = 0.15f;
    public Color roadTint = Color.white;
    public Color groundTint = Color.white;
    public Color shoulderTint = Color.white;
    public Color curbPrimary = new Color(0.78f, 0.74f, 0.64f);
    public Color curbSecondary = new Color(0.34f, 0.12f, 0.09f);
    public bool alternatingCurbs = true;

    [Header("Roadside safety")]
    public BarrierStyle barrierStyle = BarrierStyle.WoodFence;
    public float barrierOffset = 0.55f;
    public float barrierPostSpacing = 3.2f;
    public bool retainingWalls;

    [Header("Terrain")]
    public int terrainResolution = 193;
    public float terrainSize = 700f;
    public float terrainRelief = 2.5f;
    public float terrainNoiseScale = 0.012f;
    public float mountainHeight = 0f;
    public float roadCutBlend = 22f;
    public float retainingWallDrop = 0f;
    public float snowStartHeight = 8f;
    public float snowBlendHeight = 5f;
    public bool snowCoveredGround;

    [Header("Scenery")]
    public string[] treeResources = { "Pine", "Birch" };
    public string buildingResource = "House";
    public int treeCount = 100;
    public int rockCount;
    public int buildingCount = 8;
    public float treeScaleMin = 0.75f;
    public float treeScaleMax = 1.4f;
    public float sceneryOffsetMin = 8f;
    public float sceneryOffsetMax = 26f;
    public float treeSnowChance;

    [Header("Coast and harbor")]
    public bool seaEnabled;
    public bool harborEnabled;
    public float seaLevel = -1.6f;
    public float coastlineZ = -70f;
    public float coastBlend = 35f;

    [Header("Atmosphere and horizon")]
    public Color fogColor = new Color(0.55f, 0.61f, 0.59f);
    public float fogDensity = 0.004f;
    public Color skyTint = new Color(0.52f, 0.61f, 0.64f);
    public Color skyGroundColor = new Color(0.32f, 0.35f, 0.31f);
    public float atmosphereThickness = 0.65f;
    public float skyExposure = 1.05f;
    public Color sunColor = new Color(1f, 0.87f, 0.68f);
    public float sunIntensity = 1.2f;
    public float bloomIntensity = 0.28f;
    public float exposure = 0.12f;
    public float vignetteIntensity = 0.18f;
    public int horizonLayerCount = 3;
    public float horizonRadius = 430f;
    public float horizonSpacing = 190f;
    public float horizonHeight = 70f;
    public Color horizonNear = new Color(0.31f, 0.38f, 0.37f);
    public Color horizonMiddle = new Color(0.43f, 0.51f, 0.51f);
    public Color horizonFar = new Color(0.58f, 0.65f, 0.65f);

    public static TrackEnvironmentProfile CreatePreset(string trackId)
    {
        TrackEnvironmentProfile profile = CreateInstance<TrackEnvironmentProfile>();
        profile.id = trackId;
        switch (trackId)
        {
            case "winter_departure":
                profile.seed = 12011912;
                profile.roadTexturePrefix = "road_snow";
                profile.groundTexturePrefix = "road_snow";
                profile.roadTileMeters = 3.5f;
                profile.groundTileMeters = 7f;
                profile.roadTint = new Color(0.91f, 0.95f, 1f);
                profile.groundTint = new Color(0.83f, 0.89f, 0.88f);
                profile.shoulderTint = new Color(0.94f, 0.97f, 1f);
                profile.curbPrimary = new Color(0.88f, 0.9f, 0.86f);
                profile.curbSecondary = new Color(0.19f, 0.23f, 0.22f);
                profile.barrierStyle = BarrierStyle.WoodFence;
                profile.terrainResolution = 193;
                profile.terrainRelief = 1.8f;
                profile.snowCoveredGround = true;
                profile.treeCount = 120;
                profile.buildingCount = 8;
                profile.treeResources = new[] { "Pine", "Birch", "Pine" };
                profile.treeSnowChance = 0.45f;
                profile.fogDensity = 0.0045f;
                profile.fogColor = new Color(0.66f, 0.72f, 0.71f);
                profile.skyTint = new Color(0.57f, 0.65f, 0.68f);
                profile.horizonHeight = 28f;
                profile.sunIntensity = 1.05f;
                profile.bloomIntensity = 0.22f;
                break;

            case "alps_pass":
                profile.seed = 7021912;
                profile.roadTexturePrefix = "road_dirt";
                profile.groundTexturePrefix = "road_dirt";
                profile.roadTint = new Color(0.72f, 0.73f, 0.69f);
                profile.groundTint = new Color(0.56f, 0.61f, 0.48f);
                profile.shoulderTint = new Color(0.83f, 0.87f, 0.84f);
                profile.curbPrimary = new Color(0.8f, 0.78f, 0.69f);
                profile.curbSecondary = new Color(0.42f, 0.14f, 0.1f);
                profile.barrierStyle = BarrierStyle.Guardrail;
                profile.retainingWalls = true;
                profile.terrainResolution = 257;
                profile.terrainSize = 820f;
                profile.terrainRelief = 12f;
                profile.terrainNoiseScale = 0.009f;
                profile.mountainHeight = 115f;
                profile.roadCutBlend = 14f;
                profile.retainingWallDrop = 4.5f;
                profile.snowStartHeight = 7f;
                profile.snowBlendHeight = 6f;
                profile.treeCount = 85;
                profile.rockCount = 70;
                profile.buildingCount = 3;
                profile.treeResources = new[] { "Pine", "Pine", "Pine", "Birch" };
                profile.treeScaleMin = 0.8f;
                profile.treeScaleMax = 1.55f;
                profile.sceneryOffsetMin = 9f;
                profile.sceneryOffsetMax = 32f;
                profile.treeSnowChance = 0.22f;
                profile.fogDensity = 0.0065f;
                profile.fogColor = new Color(0.61f, 0.67f, 0.66f);
                profile.skyTint = new Color(0.48f, 0.59f, 0.64f);
                profile.atmosphereThickness = 0.85f;
                profile.horizonHeight = 155f;
                profile.horizonRadius = 470f;
                profile.horizonSpacing = 210f;
                profile.horizonNear = new Color(0.25f, 0.32f, 0.32f);
                profile.horizonMiddle = new Color(0.39f, 0.47f, 0.47f);
                profile.horizonFar = new Color(0.57f, 0.64f, 0.64f);
                profile.sunIntensity = 1.15f;
                break;

            case "monaco_finish":
                profile.seed = 6031912;
                profile.roadTexturePrefix = "road_cobblestone";
                profile.groundTexturePrefix = "road_cobblestone";
                profile.roadTileMeters = 2.5f;
                profile.groundTileMeters = 5f;
                profile.roadTint = new Color(0.88f, 0.84f, 0.73f);
                profile.groundTint = new Color(0.76f, 0.71f, 0.58f);
                profile.shoulderTint = new Color(0.78f, 0.75f, 0.65f);
                profile.curbPrimary = new Color(0.76f, 0.72f, 0.61f);
                profile.curbSecondary = profile.curbPrimary;
                profile.alternatingCurbs = false;
                profile.barrierStyle = BarrierStyle.StonePromenade;
                profile.barrierOffset = 0.8f;
                profile.terrainResolution = 193;
                profile.terrainSize = 760f;
                profile.terrainRelief = 5f;
                profile.terrainNoiseScale = 0.01f;
                profile.roadCutBlend = 20f;
                profile.treeCount = 28;
                profile.buildingCount = 46;
                profile.treeResources = new[] { "Pine", "Birch" };
                profile.treeScaleMin = 0.7f;
                profile.treeScaleMax = 1.15f;
                profile.sceneryOffsetMin = 10f;
                profile.sceneryOffsetMax = 30f;
                profile.seaEnabled = true;
                profile.harborEnabled = true;
                profile.seaLevel = -1.6f;
                profile.coastlineZ = -72f;
                profile.coastBlend = 38f;
                profile.fogDensity = 0.0022f;
                profile.fogColor = new Color(0.7f, 0.79f, 0.79f);
                profile.skyTint = new Color(0.43f, 0.66f, 0.78f);
                profile.skyGroundColor = new Color(0.55f, 0.59f, 0.51f);
                profile.atmosphereThickness = 0.55f;
                profile.horizonHeight = 78f;
                profile.horizonNear = new Color(0.34f, 0.42f, 0.38f);
                profile.horizonMiddle = new Color(0.45f, 0.54f, 0.5f);
                profile.horizonFar = new Color(0.61f, 0.7f, 0.69f);
                profile.sunColor = new Color(1f, 0.9f, 0.72f);
                profile.sunIntensity = 1.4f;
                profile.exposure = 0.16f;
                break;

            case "moscow_highway":
                profile.seed = 19121912;
                profile.roadTexturePrefix = "road_snow";
                profile.groundTexturePrefix = "road_snow";
                profile.roadTileMeters = 4f;
                profile.groundTileMeters = 8f;
                profile.roadTint = new Color(0.82f, 0.86f, 0.88f);
                profile.groundTint = new Color(0.78f, 0.84f, 0.86f);
                profile.shoulderTint = new Color(0.9f, 0.93f, 0.94f);
                profile.barrierStyle = BarrierStyle.WoodFence;
                profile.terrainResolution = 193;
                profile.terrainRelief = 2.2f;
                profile.snowCoveredGround = true;
                profile.treeCount = 115;
                profile.buildingCount = 7;
                profile.treeResources = new[] { "Pine", "Birch", "Pine" };
                profile.treeSnowChance = 0.55f;
                profile.fogDensity = 0.0052f;
                profile.fogColor = new Color(0.64f, 0.7f, 0.73f);
                profile.skyTint = new Color(0.55f, 0.63f, 0.68f);
                profile.horizonHeight = 34f;
                profile.sunColor = new Color(1f, 0.82f, 0.62f);
                profile.sunIntensity = 1.0f;
                profile.bloomIntensity = 0.2f;
                break;

            case "finland_coast":
                profile.seed = 25121912;
                profile.roadTexturePrefix = "road_dirt";
                profile.groundTexturePrefix = "road_dirt";
                profile.roadTileMeters = 3.2f;
                profile.groundTileMeters = 7f;
                profile.roadTint = new Color(0.7f, 0.67f, 0.58f);
                profile.groundTint = new Color(0.45f, 0.52f, 0.43f);
                profile.shoulderTint = new Color(0.66f, 0.64f, 0.54f);
                profile.barrierStyle = BarrierStyle.StonePromenade;
                profile.barrierOffset = 0.75f;
                profile.terrainResolution = 193;
                profile.terrainSize = 780f;
                profile.terrainRelief = 4.5f;
                profile.roadCutBlend = 24f;
                profile.treeCount = 52;
                profile.rockCount = 34;
                profile.buildingCount = 16;
                profile.treeResources = new[] { "Pine", "Birch" };
                profile.sceneryOffsetMin = 10f;
                profile.sceneryOffsetMax = 28f;
                profile.seaEnabled = true;
                profile.harborEnabled = true;
                profile.seaLevel = -1.8f;
                profile.coastlineZ = -82f;
                profile.coastBlend = 42f;
                profile.fogDensity = 0.006f;
                profile.fogColor = new Color(0.6f, 0.68f, 0.7f);
                profile.skyTint = new Color(0.42f, 0.58f, 0.66f);
                profile.skyGroundColor = new Color(0.48f, 0.53f, 0.5f);
                profile.atmosphereThickness = 0.72f;
                profile.horizonHeight = 48f;
                profile.horizonNear = new Color(0.26f, 0.34f, 0.35f);
                profile.horizonMiddle = new Color(0.38f, 0.47f, 0.49f);
                profile.horizonFar = new Color(0.55f, 0.63f, 0.65f);
                profile.sunColor = new Color(1f, 0.86f, 0.66f);
                profile.sunIntensity = 1.08f;
                profile.exposure = 0.1f;
                break;
        }
        return profile;
    }
}
