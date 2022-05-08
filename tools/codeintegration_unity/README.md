# Quantum Code Integration v1.0

This is a guide on how to import and keep the simulation code in Unity.


## Introduction

The default way of working with Quantum is to have the simulation code (`quantum_code`) completely separate from Unity (`quantum_unity`). The double solution approach is not to everyone's liking, so with Quantum v2 we introduced an option to include `quantum_code` projects in the solution Unity generates with `QuantumEditorSettings.MergeWithVisualStudioSolution` setting. However, there are still use cases where having simulation code *inside of* Unity may be desirable. For instance, it lets users modify and rebuild simulation code without a license for Visual Studio or Rider.

You can convert your project to use this approach. 
**IMPORTANT:** This is a one-way conversion. 

Any files that you add/remove in Unity will not be added to/removed from `quantum_code/quantum.code/quantum.code.csproj`. This is not a problem if do not intend to use the project; if you plan on using the console runners and/or server plug-ins, you will have to update the project manually yourself.

The procedure is supported for Unity 2019.4 and up.


## Integration Steps

1. Delete `quantum_unity/Assets/Photon/Quantum/Assemblies/quantum.code.dll`
2. Delete `bin` and `obj` from `quantum_code/quantum.code`
3. Copy contents of this folder (`tools/codeintegration_unity`) to `quantum_code/quantum.code`
4. Open `quantum_unity/Packages/manifest.json`, add this dependency: `"com.exitgames.photonquantumcode": "file:../../quantum_code/quantum.code"` 

If you get compile errors due to generated code being missing after opening the Unity project, run the codegen via the `Quantum/Code Integration/Run All CodeGen` menu.


## Integarion Steps (the old way)

There's also an alternative way that involves copying files to Unity project:

1. Delete `quantum_unity/Assets/Photon/Quantum/Assemblies/quantum.code.dll`
2. Copy `tools/codeintegration_unity/QuantumCodeIntegration` to `quantum_unity/Assets/Photon`
3. Create `quantum_unity/Assets/Photon/QuantumCode` directory
4. Copy `tools/codeintegration_unity/PhotonQuantumCode.asmdef` and `tools/codeintegration_unity/PhotonQuantumCode.asmdef.meta` to `quantum_unity/Assets/Photon/QuantumCode`
5. Copy everything (except for `bin` and `obj`) from `quantum_code/quantum.code` to `quantum_unity/Assets/Photon/QuantumCode`


## Gotchas

* `PhotonQuantumCode.asmdef` explicitly removes Unity assemblies references. This is to ensure the nondeterministic Unity code is not mixed with the simulation code; this ensures there's always a way back to `quantum_code` as a standalone project. 
N.B.: Any issues arising from including Unity assemblies will not receive any support.

* If for whatever the reason you happen to run into a "chicken and egg" problem (cannot compile because codegen is out of date, cannot run codegen because there are compile errors) and there is no `Quantum/Code Integration` menu, you can always run the codegen manually via the console (on non-Windows platforms prefix these with `mono`):

`tools/codegen/quantum.codegen.host.exe quantum_unity/Assets/Photon/QuantumCode`

`tools/codegen_unity/quantum.codegen.unity.host.exe quantum_unity/Library/ScriptAssemblies/PhotonQuantumCode.dll quantum_unity/Assets`

Or, if you use .NET Standard 2.1 backend (Unity 2021.2+):

`tools/codegen_unity/netcoreapp3.1/quantum.codegen.unity.host.exe quantum_unity/Library/ScriptAssemblies/PhotonQuantumCode.dll quantum_unity/Assets quantum_unity/Assets/Photon/Quantum/Assemblies`