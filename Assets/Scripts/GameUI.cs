using System.Collections;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using UnityEngine;

/// <summary>Sets up the map at startup.</summary>
public class GameBootstrap : MonoBehaviour
{
    void Awake()
    {
        Application.runInBackground = true;
        MapBuilder.Build();
    }

    // Command line shortcuts for testing: -host [-autostart] | -join <ip>
    IEnumerator Start()
    {
        var args = System.Environment.GetCommandLineArgs();
        var nm = NetworkManager.Singleton;
        var utp = nm.GetComponent<UnityTransport>();
        int i;
        if (System.Array.IndexOf(args, "-host") >= 0)
        {
            utp.SetConnectionData("127.0.0.1", 7777, "0.0.0.0");
            nm.StartHost();
            if (System.Array.IndexOf(args, "-autostart") >= 0)
            {
                while (PlayerController.All.Count < 2) yield return null;
                yield return new WaitForSeconds(2f);
                GameManager.Instance.StartRound();
            }
        }
        else if ((i = System.Array.IndexOf(args, "-join")) >= 0 && i + 1 < args.Length)
        {
            utp.SetConnectionData(args[i + 1], 7777);
            nm.StartClient();
        }
    }
}

/// <summary>Immediate-mode menu + HUD so the project needs no UI assets.</summary>
public class GameUI : MonoBehaviour
{
    public static bool CursorFree = true;

    string ip = "127.0.0.1";
    string port = "7777";
    bool paused;
    int lastSeq = -1;
    float bannerUntil;
    GUIStyle big, mid, small;

    void Update()
    {
        var nm = NetworkManager.Singleton;
        bool connected = nm != null && nm.IsListening;
        var gm = GameManager.Instance;

        if (connected && gm != null && gm.IsSpawned)
        {
            if (Input.GetKeyDown(KeyCode.Escape)) paused = !paused;
            if (nm.IsServer && !paused && Input.GetKeyDown(KeyCode.Return) && gm.Current == Phase.Lobby)
                gm.StartRound();
            if (gm.BannerSeq.Value != lastSeq) { lastSeq = gm.BannerSeq.Value; bannerUntil = Time.time + 4f; }
        }
        else paused = false;

        CursorFree = !connected || paused;
        Cursor.lockState = CursorFree ? CursorLockMode.None : CursorLockMode.Locked;
        Cursor.visible = CursorFree;
    }

    void InitStyles()
    {
        if (big != null) return;
        big = new GUIStyle(GUI.skin.label) { fontSize = 40, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter };
        mid = new GUIStyle(GUI.skin.label) { fontSize = 22, alignment = TextAnchor.MiddleCenter };
        small = new GUIStyle(GUI.skin.label) { fontSize = 16 };
        big.normal.textColor = mid.normal.textColor = small.normal.textColor = Color.white;
    }

    void Text(Rect r, string t, GUIStyle s)
    {
        var old = s.normal.textColor;
        s.normal.textColor = Color.black;
        GUI.Label(new Rect(r.x + 2, r.y + 2, r.width, r.height), t, s);
        s.normal.textColor = old;
        GUI.Label(r, t, s);
    }

    void OnGUI()
    {
        InitStyles();
        float s = Screen.height / 720f;
        GUI.matrix = Matrix4x4.Scale(new Vector3(s, s, 1));
        float W = Screen.width / s, H = 720f;

        var nm = NetworkManager.Singleton;
        if (nm == null) return;

        if (!nm.IsListening) { DrawMenu(nm, W, H); return; }

        var gm = GameManager.Instance;
        var me = PlayerController.Local;
        if (gm == null || !gm.IsSpawned || me == null) { Text(new Rect(0, H / 2 - 20, W, 40), "Connecting...", mid); return; }

        var phase = gm.Current;
        var role = me.CurrentRole;

        // blind hunter during the hiding phase
        if (role == Role.Hunter && phase == Phase.Hiding)
        {
            GUI.color = Color.black;
            GUI.DrawTexture(new Rect(0, 0, W, H), Texture2D.whiteTexture);
            GUI.color = Color.white;
            Text(new Rect(0, H / 2 - 40, W, 80), "The hiders are hiding...\n" + Mathf.CeilToInt(gm.TimeLeft) + "s", mid);
        }

        // top bar
        string phaseName = phase switch
        {
            Phase.Lobby => "LOBBY",
            Phase.Hiding => "HIDE!  " + Mathf.CeilToInt(gm.TimeLeft) + "s",
            Phase.Hunting => "HUNT  " + Mathf.CeilToInt(gm.TimeLeft) + "s",
            _ => "ROUND OVER",
        };
        Text(new Rect(0, 10, W, 40), phaseName, mid);

        int alive = 0, players = PlayerController.All.Count;
        foreach (var p in PlayerController.All)
            if (p.CurrentRole == Role.Hider && p.Alive.Value) alive++;

        string roleText = role switch
        {
            Role.Hunter => "You are the HUNTER  |  HP " + me.Hp.Value,
            Role.Hider => "You are a HIDER" + (me.Alive.Value ? "" : " (found - spectating)"),
            _ => "Waiting for the host to start",
        };
        Text(new Rect(20, 10, 500, 30), roleText, small);
        Text(new Rect(20, 34, 500, 30), "Players: " + players + (phase == Phase.Lobby ? "" : "   Hiders alive: " + alive), small);

        // help
        string help = role switch
        {
            Role.Hunter => "WASD move | Mouse look | Left click shoot | Shooting fake props costs HP",
            Role.Hider => "WASD move | E copy the prop you look at | 1-" + PropCatalog.Count + " pick a prop | Space jump",
            _ => phase == Phase.Lobby ? "WASD move | Space jump" : "Round in progress - you will join the next one",
        };
        Text(new Rect(20, H - 34, W, 30), help + "  |  Esc: free cursor", small);

        // crosshair
        if (!CursorFree && me.Alive.Value)
            Text(new Rect(W / 2 - 10, H / 2 - 14, 20, 28), "+", mid);

        // banner
        if (Time.time < bannerUntil)
            Text(new Rect(0, H * 0.22f, W, 60), gm.BannerText.Value.ToString(), mid);

        // host controls
        if (nm.IsServer && phase == Phase.Lobby)
        {
            Text(new Rect(0, H * 0.4f, W, 50), "Press ENTER to start the round", mid);
            if (CursorFree && GUI.Button(new Rect(W / 2 - 90, H * 0.4f + 60, 180, 44), "Start round"))
                gm.StartRound();
        }
        if (paused && GUI.Button(new Rect(W / 2 - 90, H * 0.7f, 180, 44), "Disconnect"))
            nm.Shutdown();
    }

    void DrawMenu(NetworkManager nm, float W, float H)
    {
        GUI.color = new Color(0, 0, .05f, .85f);
        GUI.DrawTexture(new Rect(0, 0, W, H), Texture2D.whiteTexture);
        GUI.color = Color.white;
        Text(new Rect(0, 90, W, 70), "PROP NIGHT", big);
        Text(new Rect(0, 150, W, 40), "Hide as an object. Survive the hunter.", mid);

        float x = W / 2 - 150, y = 260;
        GUI.Label(new Rect(x, y, 100, 30), "Address", small);
        ip = GUI.TextField(new Rect(x + 90, y, 210, 30), ip, small);
        GUI.Label(new Rect(x, y + 40, 100, 30), "Port", small);
        port = GUI.TextField(new Rect(x + 90, y + 40, 210, 30), port, small);

        var utp = nm.GetComponent<UnityTransport>();
        ushort.TryParse(port, out var p);
        if (p == 0) p = 7777;
        if (GUI.Button(new Rect(x, y + 100, 145, 46), "Host"))
        {
            utp.SetConnectionData("127.0.0.1", p, "0.0.0.0");
            nm.StartHost();
        }
        if (GUI.Button(new Rect(x + 155, y + 100, 145, 46), "Join"))
        {
            utp.SetConnectionData(ip, p);
            nm.StartClient();
        }
    }
}
