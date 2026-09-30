using System.IO;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>Generates the player prefab and Main scene from code (run once, or via -executeMethod).</summary>
public static class ProjectBuilder
{
    const string PrefabPath = "Assets/Prefabs/Player.prefab";
    const string ScenePath = "Assets/Scenes/Main.unity";

    [MenuItem("PropNight/Rebuild Prefab and Scene")]
    public static void BuildAll()
    {
        Directory.CreateDirectory("Assets/Prefabs");
        Directory.CreateDirectory("Assets/Scenes");
        var prefab = BuildPlayerPrefab();
        BuildScene(prefab);

        PlayerSettings.productName = "PropNightClone";
        PlayerSettings.runInBackground = true;
        PlayerSettings.fullScreenMode = FullScreenMode.Windowed;
        PlayerSettings.defaultScreenWidth = 1280;
        PlayerSettings.defaultScreenHeight = 720;
        AssetDatabase.SaveAssets();
        Debug.Log("ProjectBuilder: done");
    }

    static GameObject BuildPlayerPrefab()
    {
        var go = new GameObject("Player");
        go.AddComponent<CharacterController>();
        go.AddComponent<NetworkObject>();
        var nt = go.AddComponent<ClientNetworkTransform>();
        nt.SyncRotAngleX = false; nt.SyncRotAngleZ = false;
        nt.SyncScaleX = false; nt.SyncScaleY = false; nt.SyncScaleZ = false;
        go.AddComponent<PlayerController>();

        var body = GameObject.CreatePrimitive(PrimitiveType.Capsule);
        body.name = "Body";
        body.transform.SetParent(go.transform, false);
        body.transform.localPosition = new Vector3(0, .9f, 0);
        body.transform.localScale = new Vector3(.8f, .9f, .8f);
        Object.DestroyImmediate(body.GetComponent<Collider>());

        new GameObject("PropVisual").transform.SetParent(go.transform, false);

        var head = new GameObject("Head");
        head.transform.SetParent(go.transform, false);
        head.transform.localPosition = new Vector3(0, 1.55f, 0);

        var camGo = new GameObject("Cam");
        camGo.transform.SetParent(head.transform, false);
        var cam = camGo.AddComponent<Camera>();
        cam.fieldOfView = 75; cam.nearClipPlane = 0.05f; cam.farClipPlane = 150;
        cam.clearFlags = CameraClearFlags.SolidColor;
        cam.backgroundColor = new Color(.02f, .03f, .05f);
        cam.enabled = false;
        camGo.AddComponent<AudioListener>().enabled = false;

        var lightGo = new GameObject("Flash");
        lightGo.transform.SetParent(head.transform, false);
        lightGo.transform.localPosition = new Vector3(.25f, -.2f, .3f);
        var l = lightGo.AddComponent<Light>();
        l.type = LightType.Spot; l.range = 40; l.spotAngle = 50; l.intensity = 6; l.color = new Color(1f, .97f, .85f);
        l.enabled = false;

        var asset = PrefabUtility.SaveAsPrefabAsset(go, PrefabPath);
        Object.DestroyImmediate(go);
        return asset;
    }

    static void BuildScene(GameObject playerPrefab)
    {
        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

        var menuCam = new GameObject("MenuCamera");
        menuCam.tag = "MainCamera";
        var c = menuCam.AddComponent<Camera>();
        c.clearFlags = CameraClearFlags.SolidColor;
        c.backgroundColor = new Color(.02f, .03f, .05f);
        menuCam.AddComponent<AudioListener>();
        menuCam.transform.SetPositionAndRotation(new Vector3(0, 22, -38), Quaternion.Euler(30, 0, 0));

        var moon = new GameObject("Moonlight").AddComponent<Light>();
        moon.type = LightType.Directional;
        moon.color = new Color(.5f, .6f, 1f);
        moon.intensity = 0.35f;
        moon.transform.rotation = Quaternion.Euler(50, 30, 0);

        var nmGo = new GameObject("NetworkManager");
        var nm = nmGo.AddComponent<NetworkManager>();
        var utp = nmGo.AddComponent<UnityTransport>();
        nm.NetworkConfig.NetworkTransport = utp;
        nm.NetworkConfig.PlayerPrefab = playerPrefab;

        var gmGo = new GameObject("GameManager");
        gmGo.AddComponent<NetworkObject>();
        gmGo.AddComponent<GameManager>();

        var ui = new GameObject("Game");
        ui.AddComponent<GameBootstrap>();
        ui.AddComponent<GameUI>();

        EditorSceneManager.SaveScene(scene, ScenePath);
        EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(ScenePath, true) };
    }

    public static void BuildWindows()
    {
        BuildAll();
        var report = BuildPipeline.BuildPlayer(new[] { ScenePath }, "../Build/PropNightClone.exe",
            BuildTarget.StandaloneWindows64, BuildOptions.None);
        Debug.Log("Build result: " + report.summary.result);
        if (report.summary.result != UnityEditor.Build.Reporting.BuildResult.Succeeded) EditorApplication.Exit(1);
    }
}
