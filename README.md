# New Input System

The same character controller, driven by Unity's **new Input System** so it works with keyboard + mouse, gamepad and on-screen touch controls.

> **Note:** Logic build mein AI ki help li gayi hai.

## Features
- `PlayerInput` with an Input Actions asset (Move, Look, Attack, Kick, Jump, Crouch, Sprint, SwitchCamera, Menu).
- Automatic control-scheme switching.
- On-screen **touch controls** (two sticks and buttons) that can be toggled from the HUD.
- HUD showing the active device, the matching control hints and the live input values.

## How it is organised
- `PlayerController` - receives input messages and moves the character and camera.
- `InputHUD` - shows device, hints and values, and toggles touch controls.
- `InputSystem_Actions.inputactions` - the action map and bindings.

## Run it
1. Unity **6000.3.25f1** (Universal 3D / URP).
2. Open `Assets/Scenes/SampleScene.unity` and press Play.
3. Click **Touch Controls** in the HUD to try the on-screen controls with the mouse.

## Character and animations (not included)
The character model and animations come from **Mixamo** ([mixamo.com](https://www.mixamo.com)) and are not redistributed in this repository, so they have to be downloaded before opening the project.

1. Download the files below from Mixamo as **FBX for Unity** and keep the same names.
2. Put the model in `Assets/Character/Model/` and the animations in `Assets/Character/Animations/` **before** opening the project in Unity. The `.meta` files in the repo already hold the import settings (Humanoid rig, looping).

| Folder | Files |
|---|---|
| `Model` | `Character.fbx` |
| `Animations` | `Idle`, `Walk`, `BackWalk`, `StrafeLeft`, `StrafeRight`, `Run`, `Jump`, `WallJump`, `Punch`, `Punch2`, `Punch3`, `Kick`, `Kick2`, `Kick3`, `CrouchIdle`, `CrouchWalk` (each `.fbx`) |

Character and animations (c) Adobe Mixamo, used under the Mixamo terms of use.