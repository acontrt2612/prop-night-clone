using System.Collections.Generic;
using Unity.Collections;
using Unity.Netcode;
using UnityEngine;

public enum Phase : byte { Lobby, Hiding, Hunting, End }

/// <summary>Server-driven round state: Lobby -> Hiding -> Hunting -> End -> Lobby.</summary>
public class GameManager : NetworkBehaviour
{
    public const float HidingTime = 25f, HuntTime = 150f, EndTime = 8f;

    public static GameManager Instance;

    public NetworkVariable<byte> PhaseVar = new NetworkVariable<byte>((byte)Phase.Lobby);
    public NetworkVariable<double> PhaseEnd = new NetworkVariable<double>(0);
    public NetworkVariable<int> BannerSeq = new NetworkVariable<int>(0);
    public NetworkVariable<FixedString128Bytes> BannerText = new NetworkVariable<FixedString128Bytes>();

    public Phase Current => (Phase)PhaseVar.Value;

    public float TimeLeft => NetworkManager != null && IsSpawned
        ? Mathf.Max(0, (float)(PhaseEnd.Value - NetworkManager.ServerTime.Time))
        : 0;

    void Awake() => Instance = this;

    public void Announce(string s)
    {
        if (!IsServer) return;
        BannerText.Value = s;
        BannerSeq.Value++;
    }

    void SetPhase(Phase p, float duration)
    {
        PhaseVar.Value = (byte)p;
        PhaseEnd.Value = NetworkManager.ServerTime.Time + duration;
    }

    public void StartRound()
    {
        if (!IsServer || Current != Phase.Lobby) return;
        var players = new List<PlayerController>(PlayerController.All);
        if (players.Count < 2) { Announce("Need at least 2 players to start"); return; }

        int hunter = Random.Range(0, players.Count);
        var spots = new List<Vector3>(MapBuilder.Spawns);
        spots.RemoveAt(0); // centre is the hunter's
        for (int i = spots.Count - 1; i > 0; i--)
        {
            int j = Random.Range(0, i + 1);
            (spots[i], spots[j]) = (spots[j], spots[i]);
        }

        int n = 0;
        for (int i = 0; i < players.Count; i++)
        {
            var p = players[i];
            if (i == hunter)
            {
                p.ServerAssign(Role.Hunter, -1);
                p.TeleportRpc(MapBuilder.Spawns[0], Random.Range(0f, 360f));
            }
            else
            {
                p.ServerAssign(Role.Hider, Random.Range(0, PropCatalog.Count));
                p.TeleportRpc(spots[n++ % spots.Count], Random.Range(0f, 360f));
            }
        }
        SetPhase(Phase.Hiding, HidingTime);
        Announce("Hiders: find a spot! The hunter is blind for " + (int)HidingTime + "s");
    }

    void EndRound(string message)
    {
        SetPhase(Phase.End, EndTime);
        Announce(message);
    }

    void Update()
    {
        if (!IsServer || !IsSpawned) return;
        double now = NetworkManager.ServerTime.Time;
        switch (Current)
        {
            case Phase.Hiding:
                if (now >= PhaseEnd.Value)
                {
                    SetPhase(Phase.Hunting, HuntTime);
                    Announce("The hunter is loose!");
                }
                break;

            case Phase.Hunting:
                PlayerController hunter = null;
                int hidersAlive = 0;
                foreach (var p in PlayerController.All)
                {
                    if (p.RoleVar.Value == (byte)Role.Hunter) hunter = p;
                    else if (p.RoleVar.Value == (byte)Role.Hider && p.Alive.Value) hidersAlive++;
                }
                if (hunter == null || !hunter.Alive.Value) EndRound("HIDERS WIN! The hunter is out.");
                else if (hidersAlive == 0) EndRound("HUNTER WINS! Everyone was found.");
                else if (now >= PhaseEnd.Value) EndRound("HIDERS WIN! They survived the night.");
                break;

            case Phase.End:
                if (now >= PhaseEnd.Value)
                {
                    foreach (var p in PlayerController.All) p.ServerAssign(Role.None, -1);
                    SetPhase(Phase.Lobby, 0);
                    Announce("Back to the lobby");
                }
                break;
        }
    }
}
