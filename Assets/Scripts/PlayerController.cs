using System.Collections;
using System.Collections.Generic;
using Unity.Netcode;
using Unity.Netcode.Components;
using UnityEngine;

public enum Role : byte { None = 0, Hider = 1, Hunter = 2 }

[RequireComponent(typeof(CharacterController))]
public class PlayerController : NetworkBehaviour
{
    public static readonly List<PlayerController> All = new List<PlayerController>();
    public static PlayerController Local;

    // Server-written state
    public NetworkVariable<byte> RoleVar = new NetworkVariable<byte>((byte)Role.None);
    public NetworkVariable<bool> Alive = new NetworkVariable<bool>(true);
    public NetworkVariable<int> Hp = new NetworkVariable<int>(100);
    public NetworkVariable<int> PropIndex = new NetworkVariable<int>(-1);
    // Owner-written
    public NetworkVariable<float> Pitch = new NetworkVariable<float>(0,
        NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Owner);

    public Role CurrentRole => (Role)RoleVar.Value;
    public string DisplayName => "Player " + OwnerClientId;

    CharacterController cc;
    Transform head, cam, body, propRoot;
    Camera camera;
    AudioListener listener;
    Light flash;
    Renderer bodyRenderer;

    float yaw, pitch, vy, nextShot;
    int builtProp = -2;

    void Awake()
    {
        cc = GetComponent<CharacterController>();
        head = transform.Find("Head");
        cam = head.Find("Cam");
        body = transform.Find("Body");
        propRoot = transform.Find("PropVisual");
        camera = cam.GetComponent<Camera>();
        listener = cam.GetComponent<AudioListener>();
        flash = head.Find("Flash").GetComponent<Light>();
        bodyRenderer = body.GetComponent<Renderer>();
    }

    public override void OnNetworkSpawn()
    {
        All.Add(this);
        RoleVar.OnValueChanged += (a, b) => Refresh();
        Alive.OnValueChanged += (a, b) => Refresh();
        PropIndex.OnValueChanged += (a, b) => Refresh();

        if (IsServer && GameManager.Instance != null && GameManager.Instance.Current != Phase.Lobby)
            Alive.Value = false; // joined mid-round: spectate

        if (IsOwner)
        {
            Local = this;
            camera.enabled = true;
            listener.enabled = true;
            var menuCam = GameObject.Find("MenuCamera");
            if (menuCam != null) menuCam.SetActive(false);
            var s = MapBuilder.Spawns[Random.Range(1, MapBuilder.Spawns.Length)];
            DoTeleport(s, Random.Range(0f, 360f));
        }
        Refresh();
    }

    public override void OnNetworkDespawn()
    {
        All.Remove(this);
        if (IsOwner)
        {
            Local = null;
            foreach (var c in Resources.FindObjectsOfTypeAll<Transform>())
                if (c.name == "MenuCamera") c.gameObject.SetActive(true);
        }
    }

    // ---------------------------------------------------------------- server

    public void ServerAssign(Role role, int prop)
    {
        RoleVar.Value = (byte)role;
        Alive.Value = true;
        Hp.Value = 100;
        PropIndex.Value = role == Role.Hider ? prop : -1;
    }

    [Rpc(SendTo.Owner)]
    public void TeleportRpc(Vector3 pos, float yawDeg) => DoTeleport(pos, yawDeg);

    [Rpc(SendTo.Server, RequireOwnership = true)]
    void SetPropRpc(int index)
    {
        var gm = GameManager.Instance;
        if (gm.Current != Phase.Hiding && gm.Current != Phase.Hunting) return;
        if (RoleVar.Value != (byte)Role.Hider || !Alive.Value) return;
        if (index < 0 || index >= PropCatalog.Count) return;
        PropIndex.Value = index;
    }

    [Rpc(SendTo.Server, RequireOwnership = true)]
    void ShootRpc(Vector3 origin, Vector3 dir)
    {
        var gm = GameManager.Instance;
        if (gm.Current != Phase.Hunting || RoleVar.Value != (byte)Role.Hunter || !Alive.Value) return;
        if ((origin - head.position).sqrMagnitude > 9f) origin = head.position;
        dir.Normalize();

        Physics.SyncTransforms();
        Vector3 start = origin + dir * 0.7f; // step out of our own capsule
        bool hit = Physics.Raycast(start, dir, out var h, 80f, ~0, QueryTriggerInteraction.Ignore);
        Vector3 end = hit ? h.point : start + dir * 80f;

        if (hit)
        {
            var target = h.collider.GetComponentInParent<PlayerController>();
            if (target != null)
            {
                if (target.RoleVar.Value == (byte)Role.Hider && target.Alive.Value)
                {
                    target.Alive.Value = false;
                    gm.Announce(target.DisplayName + " was found!");
                }
            }
            else if (h.collider.GetComponentInParent<PropTag>() != null)
            {
                // Shot a harmless prop: hunter pays for it
                Hp.Value = Mathf.Max(0, Hp.Value - 10);
                if (Hp.Value == 0) { Alive.Value = false; gm.Announce("The hunter shot too many innocent props!"); }
            }
        }
        ShotFxRpc(start, end);
    }

    [Rpc(SendTo.ClientsAndHost)]
    void ShotFxRpc(Vector3 a, Vector3 b) => StartCoroutine(ShotFx(a, b));

    IEnumerator ShotFx(Vector3 a, Vector3 b)
    {
        var g = new GameObject("Shot");
        var lr = g.AddComponent<LineRenderer>();
        lr.material = new Material(Shader.Find("Sprites/Default"));
        lr.startColor = lr.endColor = new Color(1f, .9f, .4f);
        lr.startWidth = lr.endWidth = 0.05f;
        lr.positionCount = 2;
        lr.SetPosition(0, a + Vector3.down * .2f);
        lr.SetPosition(1, b);
        yield return new WaitForSeconds(0.07f);
        Destroy(g);
    }

    // ---------------------------------------------------------------- shared

    void DoTeleport(Vector3 pos, float yawDeg)
    {
        cc.enabled = false;
        vy = 0;
        yaw = yawDeg;
        var rot = Quaternion.Euler(0, yawDeg, 0);
        transform.SetPositionAndRotation(pos, rot);
        var nt = GetComponent<NetworkTransform>();
        if (nt != null && nt.CanCommitToTransform) nt.Teleport(pos, rot, Vector3.one);
        cc.enabled = Alive.Value;
    }

    void Refresh()
    {
        if (!IsSpawned) return;
        var role = CurrentRole;
        bool alive = Alive.Value;
        int prop = PropIndex.Value;
        bool isProp = role == Role.Hider && prop >= 0;

        cc.enabled = alive;
        float h = isProp ? PropCatalog.Defs[prop].Height : 1.8f;
        float r = isProp ? PropCatalog.Defs[prop].Radius : 0.4f;
        cc.radius = r; cc.height = h; cc.center = new Vector3(0, h / 2, 0);

        body.gameObject.SetActive(alive && !isProp && !IsOwner);
        bodyRenderer.material.color = role == Role.Hunter ? new Color(.8f, .1f, .1f) : new Color(.6f, .7f, .9f);
        flash.enabled = alive && role == Role.Hunter;

        int want = alive && isProp ? prop : -1;
        if (want != builtProp)
        {
            builtProp = want;
            foreach (Transform c in propRoot) Destroy(c.gameObject);
            if (want >= 0)
            {
                var v = PropCatalog.Build(want, false);
                v.transform.SetParent(propRoot, false);
            }
        }

        if (IsOwner)
        {
            // hiders play in third person so they can see their disguise
            cam.localPosition = alive && isProp ? new Vector3(0, .5f, -3.5f) : Vector3.zero;
        }
    }

    // ---------------------------------------------------------------- owner input

    void Update()
    {
        if (!IsSpawned) return;
        if (!IsOwner) { head.localRotation = Quaternion.Euler(Pitch.Value, 0, 0); return; }

        var gm = GameManager.Instance;
        var role = CurrentRole;
        var phase = gm.Current;
        bool alive = Alive.Value;
        bool ui = GameUI.CursorFree;

        if (!ui)
        {
            yaw += Input.GetAxis("Mouse X") * 2.2f;
            float minPitch = role == Role.Hider ? -15f : -80f;
            pitch = Mathf.Clamp(pitch - Input.GetAxis("Mouse Y") * 2.2f, minPitch, 80f);
        }
        transform.rotation = Quaternion.Euler(0, yaw, 0);
        head.localRotation = Quaternion.Euler(pitch, 0, 0);
        if (Mathf.Abs(Pitch.Value - pitch) > 0.5f) Pitch.Value = pitch;

        float dt = Time.deltaTime;
        float fwd = ui ? 0 : Input.GetAxisRaw("Vertical");
        float side = ui ? 0 : Input.GetAxisRaw("Horizontal");

        if (alive && cc.enabled)
        {
            bool frozen = role == Role.Hunter && phase == Phase.Hiding;
            Vector3 dir = (transform.right * side + transform.forward * fwd);
            if (dir.sqrMagnitude > 1) dir.Normalize();
            float speed = role == Role.Hunter ? 5.5f : 4.5f;
            if (cc.isGrounded)
            {
                vy = -2f;
                if (!ui && !frozen && Input.GetKeyDown(KeyCode.Space)) vy = 6.5f;
            }
            vy -= 20f * dt;
            Vector3 move = frozen ? Vector3.zero : dir * speed;
            move.y = vy;
            cc.Move(move * dt);
            if (transform.position.y < -10)
                DoTeleport(MapBuilder.Spawns[Random.Range(1, MapBuilder.Spawns.Length)], yaw);

            if (!ui && role == Role.Hider) HiderInput(phase);
            if (!ui && role == Role.Hunter) HunterInput(phase);
        }
        else if (!alive)
        {
            // dead / spectating: free fly
            transform.position += (head.forward * fwd + transform.right * side) * 8f * dt;
        }
    }

    void HiderInput(Phase phase)
    {
        if (phase != Phase.Hiding && phase != Phase.Hunting) return;
        if (Input.GetKeyDown(KeyCode.E))
        {
            if (Physics.Raycast(head.position, head.forward, out var hit, 8f, ~0, QueryTriggerInteraction.Ignore))
            {
                var tag = hit.collider.GetComponentInParent<PropTag>();
                if (tag != null) SetPropRpc(tag.Index);
            }
        }
        for (int i = 0; i < PropCatalog.Count; i++)
            if (Input.GetKeyDown(KeyCode.Alpha1 + i)) SetPropRpc(i);
    }

    void HunterInput(Phase phase)
    {
        if (phase != Phase.Hunting) return;
        if (Input.GetMouseButtonDown(0) && Time.time >= nextShot)
        {
            nextShot = Time.time + 0.4f;
            ShootRpc(cam.position, cam.forward);
        }
    }
}
