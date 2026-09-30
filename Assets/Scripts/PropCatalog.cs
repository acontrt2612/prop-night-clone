using UnityEngine;

/// <summary>Tag on map decoration so hiders can copy it (E) and hunters get penalised for shooting it.</summary>
public class PropTag : MonoBehaviour
{
    public int Index;
}

/// <summary>The disguises. Everything is built from primitives so the project needs no external assets.</summary>
public static class PropCatalog
{
    public struct Def
    {
        public string Name;
        public float Radius, Height;
    }

    public static readonly Def[] Defs =
    {
        new Def { Name = "Crate",  Radius = 0.5f,  Height = 1.0f },
        new Def { Name = "Barrel", Radius = 0.4f,  Height = 1.2f },
        new Def { Name = "Chair",  Radius = 0.35f, Height = 0.9f },
        new Def { Name = "Lamp",   Radius = 0.25f, Height = 2.4f },
        new Def { Name = "Rock",   Radius = 0.35f, Height = 0.7f },
        new Def { Name = "Bin",    Radius = 0.3f,  Height = 0.9f },
    };

    public static int Count => Defs.Length;

    public static GameObject Build(int index, bool colliders)
    {
        var root = new GameObject("Prop_" + Defs[index].Name);
        switch (index)
        {
            case 0:
                Part(root, PrimitiveType.Cube, new Vector3(0, .5f, 0), new Vector3(1, 1, 1), new Color(.45f, .3f, .15f), colliders);
                break;
            case 1:
                Part(root, PrimitiveType.Cylinder, new Vector3(0, .6f, 0), new Vector3(.8f, .6f, .8f), new Color(.2f, .3f, .5f), colliders);
                break;
            case 2:
                Part(root, PrimitiveType.Cube, new Vector3(0, .45f, 0), new Vector3(.7f, .1f, .7f), new Color(.5f, .35f, .2f), colliders);
                Part(root, PrimitiveType.Cube, new Vector3(0, .75f, -.3f), new Vector3(.7f, .5f, .1f), new Color(.5f, .35f, .2f), colliders);
                foreach (var (x, z) in new[] { (-.28f, -.28f), (.28f, -.28f), (-.28f, .28f), (.28f, .28f) })
                    Part(root, PrimitiveType.Cylinder, new Vector3(x, .2f, z), new Vector3(.08f, .2f, .08f), new Color(.35f, .25f, .15f), colliders);
                break;
            case 3:
                Part(root, PrimitiveType.Cylinder, new Vector3(0, 1.1f, 0), new Vector3(.1f, 1.1f, .1f), new Color(.15f, .15f, .15f), colliders);
                Part(root, PrimitiveType.Sphere, new Vector3(0, 2.25f, 0), new Vector3(.4f, .3f, .4f), new Color(1f, .9f, .5f), colliders);
                break;
            case 4:
                Part(root, PrimitiveType.Sphere, new Vector3(0, .3f, 0), new Vector3(.9f, .6f, .8f), new Color(.4f, .4f, .42f), colliders);
                break;
            default:
                Part(root, PrimitiveType.Cylinder, new Vector3(0, .4f, 0), new Vector3(.55f, .4f, .55f), new Color(.15f, .4f, .2f), colliders);
                Part(root, PrimitiveType.Cylinder, new Vector3(0, .82f, 0), new Vector3(.62f, .03f, .62f), new Color(.1f, .3f, .15f), colliders);
                break;
        }
        return root;
    }

    static void Part(GameObject root, PrimitiveType type, Vector3 pos, Vector3 scale, Color color, bool collider)
    {
        var p = GameObject.CreatePrimitive(type);
        p.transform.SetParent(root.transform, false);
        p.transform.localPosition = pos;
        p.transform.localScale = scale;
        p.GetComponent<Renderer>().sharedMaterial = MapBuilder.Mat(color);
        if (!collider) Object.DestroyImmediate(p.GetComponent<Collider>());
    }
}
