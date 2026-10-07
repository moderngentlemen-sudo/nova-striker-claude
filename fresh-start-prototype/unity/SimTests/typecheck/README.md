# Type-checking the Unity project without Unity

`ucompile.py` compiles the project's assemblies (and the packages they use) with Roslyn against the Unity
editor's managed DLLs, to catch C# errors without opening the editor. It does not compile shaders or run
anything; the GitHub build (or the editor) is the full check.

It expects, next to the script:
- `unity/Editor/Data/`: the Unity 6000.3.25f1 Linux editor, extracted (`Unity.tar.xz` from Unity's download
  archive for that version). The built-in packages (uGUI, URP, Core RP) come from its
  `Resources/PackageManager/BuiltInPackages`.
- the other packages from `Packages/manifest.json` (Input System 1.17.0, Burst, Collections, Mathematics),
  each extracted from the Unity package registry tarball.
- .NET 8 SDK (`~/.dotnet`); the Roslyn path is set at the top of the script.

Usage (the package roots are the folders holding each package.json):

    python3 ucompile.py <out-dir> <package roots...> ../../Assets --only NovaStriker.Sim,NovaStriker.Game,NovaStriker.Editor
    # add --player to compile without UNITY_EDITOR, as a player build does
