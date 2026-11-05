# Third-party notices

Controller images have separate asset licenses; the mod source remains under the
repository's GPL license. This independent mod is not endorsed by Valve or Meta.

## Steam Frame controller diagram

`NOVR/Assets/Controllers/frame-views.png` is Valve Corporation's
[Steam Frame controller diagram](https://gitlab.steamos.cloud/SteamHardware/SteamFrame/-/blob/bdbb41ba63fcadae6ff5c547a92de2d837f708a9/image/Steam_Frame_Controller_LED_Locations.png),
© 2026 Valve Corporation, licensed under
[CC BY-NC-SA 4.0](https://creativecommons.org/licenses/by-nc-sa/4.0/).
The source image is unchanged; the UI crops its views and adds separate binding
labels. The diagram and adapted diagram portions retain attribution,
noncommercial, and share-alike terms. Full license:
`plugins/NOVR/ThirdParty/Valve-Frame-Diagrams-LICENSE.txt` in the binary package.

## Oculus Touch controller diagram

`NOVR/Assets/Controllers/touch-diagram.png` is the user-supplied controller diagram,
included unchanged and cropped into left/right views by the UI. No third-party
license was supplied with this image; it is not covered by the code's GPL license
or the former WebXR assets' MIT license.

## Valve OpenXR controller profile

`NOVR.XR.OpenXR/Features/Valve/SteamFrameControllerProfile.cs` is from
[ValveSoftware/Unity](https://github.com/ValveSoftware/Unity/tree/7e842352310fc2cb458ac6b127ce1afc233173a1),
package `com.valvesoftware.openxr.utils`. NOVR adds an extension-availability guard
and a source notice. Copyright (c) 2025 Valve Software; BSD 3-Clause License.
Full license: `plugins/NOVR/ThirdParty/Valve-SteamFrame-LICENSE.txt`.

Valve glove credits and modification details are included in
`plugins/NOVR/ThirdParty/Valve-Gloves-NOTICE.txt`, with their BSD 3-Clause license
alongside it. Existing NOVR/upstream notices and licensing continue to apply.

## Optional fixed-foveation component

The optional `plugins/NOVR/Native/NovrFoveation.dll` uses NVIDIA NVAPI declarations
and Unity's native render-callback declarations. Pinned sources and licenses are
in `native/foveation/vendor`; the binary package includes their notices beside
the DLL. NVIDIA's driver DLL is loaded from Windows and is not redistributed.
