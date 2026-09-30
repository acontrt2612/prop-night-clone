using System.Collections.Generic;
using UnityEngine;

/// <summary>Builds the night arena at startup. Uses a fixed seed so host and clients get the identical map.</summary>
public static class MapBuilder
{
    public const float Half = 40f;
    public static Vector3[] Spawns;

    static readonly Dictionary<Color, Material> mats = new Dictionary<Color, Material>();

    public static Material Mat(Color c)
    {
        if (mats.TryGetValue(c, out var m) && m != null) return m;
        var shader = Shader.Find("Standard") ?? Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Sprites/Default");
        m = new Material(shader) { color = c };
        m.SetFloat("_Glossiness", 0.15f);
        mats[c] = m;
        return m;
    }

    static GameObject Box(Transform parent, string name, Vector3 pos, Vector3 size, Color c)
    {
        var g = GameObject.CreatePrimitive(PrimitiveType.Cube);
        g.name = name;
        g.transform.SetParent(parent, false);
        g.transform.position = pos;
        g.transform.localScale = size;
        g.GetComponent<Renderer>().sharedMaterial = Mat(c);
        return g;
    }

    public static void Build()
    {
        var root = new GameObject("Map").transform;
        var rng = new System.Random(20240613);
        float R(float a, float b) => a + (float)rng.NextDouble() * (b - a);

        // Night look
        RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
        RenderSettings.ambientLight = new Color(.07f, .08f, .13f);
        RenderSettings.fog = true;
        RenderSettings.fogMode = FogMode.ExponentialSquared;
        RenderSettings.fogDensity = 0.025f;
        RenderSettings.fogColor = new Color(.02f, .03f, .05f);
        RenderSettings.skybox = null;

        // Ground + boundary
        Box(root, "Ground", new Vector3(0, -.5f, 0), new Vector3(Half * 2 + 4, 1, Half * 2 + 4), new Color(.13f, .17f, .13f));
        var wallC = new Color(.22f, .22f, .27f);
        Box(root, "WallN", new Vector3(0, 3, Half + .5f), new Vector3(Half * 2 + 4, 6, 1), wallC);
        Box(root, "WallS", new Vector3(0, 3, -Half - .5f), new Vector3(Half * 2 + 4, 6, 1), wallC);
        Box(root, "WallE", new Vector3(Half + .5f, 3, 0), new Vector3(1, 6, Half * 2 + 4), wallC);
        Box(root, "WallW", new Vector3(-Half - .5f, 3, 0), new Vector3(1, 6, Half * 2 + 4), wallC);

        // Spawn points: 0 = centre, then a ring
        var spawns = new List<Vector3> { new Vector3(0, .1f, 0) };
        for (int i = 0; i < 12; i++)
        {
            float a = i * Mathf.PI * 2 / 12;
            spawns.Add(new Vector3(Mathf.Cos(a) * 16, .1f, Mathf.Sin(a) * 16));
        }
        Spawns = spawns.ToArray();

        bool NearSpawn(Vector3 p, float d)
        {
            foreach (var s in Spawns) if ((s - p).sqrMagnitude < d * d) return true;
            return false;
        }

        // Interior walls to break up line of sight
        for (int i = 0; i < 18; i++)
        {
            Vector3 p; float len = R(6, 14);
            int tries = 0;
            do { p = new Vector3(R(-32, 32), 1.5f, R(-32, 32)); } while (NearSpawn(p, len * .5f + 3) && ++tries < 50);
            bool alongX = rng.Next(2) == 0;
            Box(root, "Wall" + i, p, alongX ? new Vector3(len, 3, .6f) : new Vector3(.6f, 3, len), wallC * R(.8f, 1.2f));
        }

        // Decoration the hiders can blend into
        int lamps = 0;
        for (int i = 0; i < 190; i++)
        {
            Vector3 p; int tries = 0;
            do { p = new Vector3(R(-Half + 2, Half - 2), 0, R(-Half + 2, Half - 2)); } while (NearSpawn(p, 2f) && ++tries < 20);
            int idx = rng.Next(PropCatalog.Count);
            var g = PropCatalog.Build(idx, true);
            g.transform.SetParent(root, false);
            g.transform.position = p;
            g.transform.rotation = Quaternion.Euler(0, R(0, 360), 0);
            g.AddComponent<PropTag>().Index = idx;
            if (idx == 3 && lamps++ < 10)
            {
                var l = new GameObject("LampLight").AddComponent<Light>();
                l.transform.SetParent(g.transform, false);
                l.transform.localPosition = new Vector3(0, 2.2f, 0);
                l.type = LightType.Point; l.range = 9; l.intensity = 1.4f; l.color = new Color(1f, .85f, .5f);
            }
        }
    }
}
