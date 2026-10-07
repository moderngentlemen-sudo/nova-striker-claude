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

`--only` compiles just those assemblies against DLLs already in `<out-dir>`, so on a fresh `<out-dir>` run it once
without `--only`. That builds the packages too. Some package editor assemblies fail (tests, code generators,
TextMeshPro). That is expected: what matters is `ok` for the three `NovaStriker.*` lines.

The roots that work, with the built-in packages under `B=unity/Editor/Data/Resources/PackageManager/BuiltInPackages`
and the registry ones extracted to `pkgs/<name>/` (the versions URP 17.3.0 asks for: Burst 1.8.14, Mathematics
1.3.2, Collections 2.4.3, Searcher 4.9.5):

    pkgs/inputsystem/package pkgs/com.unity.burst/package pkgs/com.unity.mathematics/package
    pkgs/com.unity.collections/package pkgs/com.unity.searcher/package $B/com.unity.ugui
    $B/com.unity.render-pipelines.core $B/com.unity.render-pipelines.universal
    $B/com.unity.render-pipelines.universal-config $B/com.unity.shadergraph

The editor's download link comes from
`https://services.api.unity.com/unity/editor/release/v1/releases?version=6000.3.25f1&platform=LINUX&architecture=X86_64`.
Only `Editor/Data/Managed`, `Editor/Data/NetStandard` and `Editor/Data/Resources/PackageManager/BuiltInPackages` are
needed from it, and `tar --wildcards` can extract just those.
