using UnityEngine;
using Photon.Deterministic;
using Quantum;

[CreateAssetMenu(menuName = "Quantum/Configurations/SimulationConfig", fileName = "SimulationConfig", order = Quantum.EditorDefines.AssetMenuPriorityConfigurations)]
public partial class SimulationConfigAsset : AssetBase {

  public static SimulationConfigAsset Instance {
    get {
      // Try not to use this anymore. 
      // You can get the SimulationConfig from the DB or use the actual asset in Unity.
      return UnityEngine.Resources.LoadAll<SimulationConfigAsset>("DB")[0];
    }
  }
  
  public override void Loaded() {
    Settings.Physics.PenetrationCorrection = FPMath.Clamp01(Settings.Physics.PenetrationCorrection);
  }

  public override void Reset() {
    Settings = new Quantum.SimulationConfig();

    base.Reset();

    Settings.Physics = new Quantum.PhysicsCommon.Config();
    Settings.Navigation = new Quantum.Navigation.Config();

    Settings.Physics.DefaultPhysicsMaterial.Id = Quantum.PhysicsMaterial.DEFAULT_ID;
    Settings.Physics.DefaultCharacterController2D.Id = Quantum.CharacterController2DConfig.DEFAULT_ID;
    Settings.Physics.DefaultCharacterController3D.Id = Quantum.CharacterController3DConfig.DEFAULT_ID;
    Settings.Navigation.DefaultNavMeshAgent.Id = Quantum.NavMeshAgentConfig.DEFAULT_ID;

    SimulationConfigAssetHelper.ImportLayersFromUnity(this);
  }
}
