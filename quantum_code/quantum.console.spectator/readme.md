# Quantum Spectator

The `quantum.console.spectator` project showcases how to create a headless Quantum app (no Unity) that connects to a Photon room an runs the Quantum simulation without adding a Quantum player to it.

## Toolchain

We created a small demo toolchain in the Unity Editor that lets you compile and run a spectator with one menu click (Windows only). It works in tandem with the Quantum demo menu and should be seen as an example how to connect the spectator to custom workflow.

```
Unity Menu > Quantum > Demo > Start Spectator (Create Room)
```
  
* exports required assets
* sets up the EnterRoomParams to be a room creator
* compile the project and start the exe

```
Unity Menu > Quantum > Demo > Start Spectator (Join Current Room)
```

* exports required assets
* sets up the EnterRoomParams to only contain a room name, which enables the spectator to join the room and late-start the simulation
* compile the project and start the exe

You can also add the spectator project to you quantum_code solution, select it as start-up project and press F5 to debug into it (make sure that its assets are up to date).

The project uses the Quantum dlls it finds in the Unity project, if you renamed it, rename the paths in `quantum.console.spectator.csproj` as well or use the Debug/Release dlls from the `assemblies` folder.

## Spectator Assets

```
quantum_code\quantum.console.spectator\bin\assets\
```
  
The location of the assets can be customized in the `App.config` file.

The content of the asset folder is pretty self explanatory:

* AppSettings.json (so the app knows where to connect)
* db.json (the Quantum asset db)
* EnterRoomParams.xml (what room to connect or create, there is a custom implementation of the `EnterRoomParams` class to be able to serialize the hashtable)
* RuntimeConfig.json
* SessionConfig.json
* LutFiles/Folder

## Specialities

* the project uses a new implementation of `SessionContainer` that lets you start the (online) simulation as a spectator: a connection without Quantum player and without the possibility to send input (see `SessionContainer.StartSpectator()`)
* the project uses experimental Photon Realtime async extensions (`PhotonRealtimeAsync`). Feedback is appreciated :)