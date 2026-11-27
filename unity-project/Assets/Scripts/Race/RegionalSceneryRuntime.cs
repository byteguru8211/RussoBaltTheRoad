using System.Linq;
using UnityEngine;

public static class RegionalSceneryRuntime
{
    public static GameObject Instantiate(string assetName, Transform parent, Vector3 position, Quaternion rotation, float scale = 1f)
    {
        GameObject prefab = Resources.Load<GameObject>("Environment/RegionalKit/" + assetName);
        if (prefab == null) return null;
        Quaternion importedRotation = prefab.transform.rotation;
        GameObject instance = Object.Instantiate(prefab, position, rotation * importedRotation, parent);
        instance.name = assetName;
        instance.transform.localScale = prefab.transform.localScale * scale;
        ConfigureColliders(instance, assetName);
        ConfigureLods(instance, assetName);
        return instance;
    }

    private static void ConfigureLods(GameObject instance, string assetName)
    {
        if (instance.GetComponent<LODGroup>() != null) return;
        GameObject lodPrefab = Resources.Load<GameObject>("Environment/RegionalKit/" + assetName + "_LOD1");
        if (lodPrefab == null) return;
        GameObject lodInstance = Object.Instantiate(lodPrefab, instance.transform.position, instance.transform.rotation, instance.transform);
        lodInstance.name = "LOD1";
        lodInstance.transform.localPosition = Vector3.zero;
        lodInstance.transform.localRotation = Quaternion.identity;
        lodInstance.transform.localScale = Vector3.one;
        Renderer[] high = instance.GetComponentsInChildren<MeshRenderer>(true)
            .Where(renderer => !renderer.transform.IsChildOf(lodInstance.transform)).ToArray();
        Renderer[] low = lodInstance.GetComponentsInChildren<MeshRenderer>(true);
        if (high.Length == 0 || low.Length == 0)
        {
            Object.Destroy(lodInstance);
            return;
        }
        LODGroup group = instance.AddComponent<LODGroup>();
        group.SetLODs(new[]
        {
            new LOD(0.32f, high),
            new LOD(0.08f, low),
            new LOD(0.01f, System.Array.Empty<Renderer>())
        });
        group.RecalculateBounds();
    }

    private static void ConfigureColliders(GameObject instance, string assetName)
    {
        if (assetName.StartsWith("Building_") || assetName == "Prop_Race_Grandstand_01" || assetName == "Prop_Roadside_Shrine_01")
        {
            AddBoundsCollider(instance);
            return;
        }
        if (assetName == "Prop_Alpine_Cliff_01")
        {
            foreach (Transform child in instance.transform)
            {
                if (child.name.StartsWith("Cliff_Mass_")) AddBoundsCollider(child.gameObject);
            }
            return;
        }
        if (assetName == "Prop_Mediterranean_Cypress_01")
        {
            Transform trunk = instance.transform.Find("Cypress_Trunk");
            if (trunk != null) AddBoundsCollider(trunk.gameObject);
            return;
        }
        if (assetName == "Prop_Stone_Bridge_01")
        {
            foreach (Transform child in instance.transform)
            {
                if (child.name.StartsWith("Bridge_Pier_") || child.name == "Bridge_Deck")
                    AddBoundsCollider(child.gameObject);
            }
        }
    }

    private static void AddBoundsCollider(GameObject target)
    {
        MeshRenderer[] renderers = target.GetComponentsInChildren<MeshRenderer>();
        if (renderers.Length == 0) return;
        Bounds localBounds = new Bounds();
        bool initialized = false;
        foreach (MeshRenderer renderer in renderers)
        {
            Bounds world = renderer.bounds;
            for (int corner = 0; corner < 8; corner++)
            {
                Vector3 worldCorner = world.center + Vector3.Scale(world.extents, new Vector3(
                    (corner & 1) == 0 ? -1f : 1f,
                    (corner & 2) == 0 ? -1f : 1f,
                    (corner & 4) == 0 ? -1f : 1f));
                Vector3 localCorner = target.transform.InverseTransformPoint(worldCorner);
                if (!initialized)
                {
                    localBounds = new Bounds(localCorner, Vector3.zero);
                    initialized = true;
                }
                else localBounds.Encapsulate(localCorner);
            }
        }
        if (!initialized) return;
        BoxCollider collider = target.AddComponent<BoxCollider>();
        collider.center = localBounds.center;
        collider.size = localBounds.size;
    }
}
