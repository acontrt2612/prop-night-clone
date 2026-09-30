# Prop Night Clone (Unity 6000.3.20f1 + Netcode for GameObjects)

Online hide-and-seek: one player is the Hunter, the rest disguise themselves as props.

## Run
- Open the project folder in Unity Hub. Keep its path SHORT (Burst fails past 260 chars on Windows).
- Open Assets/Scenes/Main.unity, press Play (Host), and run a Windows build as the second player.
- Or run Build/PropNightClone.exe twice: one clicks Host, the other Join (127.0.0.1).
- Test shortcuts: `PropNightClone.exe -host -autostart`, `PropNightClone.exe -join <ip>`.
- Rebuild prefab/scene: menu PropNight > Rebuild Prefab and Scene.

## Controls
- Hider: WASD, Space, E = copy the prop you look at, 1-6 = pick a prop.
- Hunter: left click = shoot. Shooting a fake prop costs 10 HP.
- Host: Enter starts a round. Esc frees the cursor.

## Rules
Hunter is blind and frozen for 25s while hiders hide. Then 150s to find everyone.
Hiders win if time runs out or the hunter reaches 0 HP.
