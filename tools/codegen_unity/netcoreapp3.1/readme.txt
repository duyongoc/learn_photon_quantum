This version of unity codegen tools requires the photon dlls to be linked explicitly by `--additional-deps`:
Run from command line:
```
Quantum> tools\codegen_unity\netcoreapp3.1\quantum.codegen.unity.host.exe quantum_unity\Assets\Photon\Quantum\Assemblies\quantum.code.dll quantum_unity\Assets --additional-deps assemblies\release
```

Run as PostBuildEvent from Visual Studio:
```
<PostBuildEvent Condition="'$(OS)' == 'Windows_NT'">"$(ProjectDir)..\..\tools\codegen_unity\netcoreapp3.1\quantum.codegen.unity.host.exe" "$(TargetDir)\quantum.code.dll" "$(ProjectDir)..\..\quantum_unity\Assets" --additional-deps "$(ProjectDir)..\..\assemblies\$(Configuration)"</PostBuildEvent>
```