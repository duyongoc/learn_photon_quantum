using Quantum;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

/// <summary>
/// The script will can manage multiple online clients and Quantum players in your Editor. This means the remote view of your player can be visualized in the same Unity instance.
/// Minimum settings:
///   * Requires a valid AppId and working network settings in Photon Server Settings
///   * Drag the QuantumMultiClientRunner prefab into you Quantum game scene (this works similar to the default Runner except it does not reload the Unity scene)
///   * Add game objects that belong to the regular Quantum scene to DisableOnStart (QuantumDefaultRunner, EntityViewUpdater, Your Input Script, CustomCallbacks)
///   * The PlayerInputTemplate is instantiated for each client to gather input by fireing the Unity message PollInput(CallbackPollInput c). Implement your input to support this format:
///     public class QuantumMultiClientTestInput : MonoBehaviour {
///       private void PollInput(CallbackPollInput c) {
///         var i = new Quantum.Input();
///         i.Direction.X = 1;
///         i.Direction.Y = 0;
///         c.SetInput(i, DeterministicInputFlags.Repeatable);
///       }
///      }
/// </summary>
///   * Press "New Client" to add additional online players
///     I = toggle input of the player
///     V = toggle view of the player
///     G = toggle gizmos of the player
///     X = quit player
///   * If you don't experience ghosting try a different cloud that if farther away from you (Fixed Region 'sa' for example)
///   
public class QuantumMultiClientRunner : MonoBehaviour {
  public QuantumMultiClientPlayerView PlayerViewTemplate;
  public UnityEngine.UI.Button CreatePlayerBtn;
  [Tooltip("Quantum scripts in your game scene that are part of the regular setup like EntityViewUpdater, Input and CustomCallbacks need to be disabled when using the MultiClientRunner, add them here.")]
  public List<GameObject> DisableOnStart = new List<GameObject>();
  [Tooltip("Optionally provide non-default editor settings for all additional clients after the first one (to change the gizmo colors for example).")]
  public QuantumEditorSettings EditorSettings;
  [Tooltip("Optionally provide different non-default server app settings")]
  public PhotonServerSettings ServerSettings;
  [Tooltip("Add custom runtime config settings here")]
  public RuntimeConfig RuntimeConfig;
  [Tooltip("Set the max player count")]
  public int PlayerCount = 4;
  [Tooltip("How many players to start with")]
  public int InitialPlayerCount = 0;
  [Tooltip("Add custom runtime player settings here")]
  public RuntimePlayer[] RuntimePlayer;
  [Tooltip("Provide a player input template that is instantiated for the clients. A Unity script that has to implement void Unity message PollInput(CallbackPollInput c)")]
  public GameObject PlayerInputTemplate;
  [Tooltip("Optionally provide a custom EntityViewUpdater game object template that is instantiated for the clients (otherwise a new instance of EntityViewUpdater is created for each player)")]
  public EntityViewUpdater EntityViewUpdaterTemplate;

  List<QuantumMultiClientPlayer> players = new List<QuantumMultiClientPlayer>();

  public IEnumerator Start() {
    PlayerViewTemplate.gameObject.SetActive(false);

    foreach (var go in DisableOnStart) {
      go.SetActive(false);
    }

    for (int i = 0; i < InitialPlayerCount; ++i) {
      bool created = false;
      CreateNewPlayerInternal().OnPlayerCreatedCallback += p => created = true;
      yield return new WaitUntil(() => created);
    }
  }

  public void OnEnable() {
    CreatePlayerBtn.onClick.AddListener(CreateNewPlayer);
    QuantumCallback.Subscribe(this, (CallbackPollInput c) => OnCallbackPollInput(c));
  }

  private void OnCallbackPollInput(CallbackPollInput c) {
    var player = players.Find(p => p.RunnerId == ((QuantumRunner)c.Game.Session.Runner).Id);
    if (player != null && player.Input != null) {
      player.Input.SendMessage("PollInput", c, SendMessageOptions.DontRequireReceiver);
    }
  }

  public void OnDisable() {
    CreatePlayerBtn.onClick.RemoveListener(CreateNewPlayer);
  }

  public void CreateNewPlayer() {
    CreateNewPlayerInternal();
  }
   
  private QuantumMultiClientPlayer CreateNewPlayerInternal() {
    var playerId = 0;
    while (players.Any(p => p.name == $"Client {playerId:00}")) { playerId++; }

    var playerGO = new GameObject($"Client {playerId:00}");
    playerGO.transform.parent = gameObject.transform;
    var player = playerGO.AddComponent<QuantumMultiClientPlayer>();
    player.IsFirstPlayer = players.Count == 0;
    player.PlayerCount = PlayerCount;
    player.RuntimePlayer = (RuntimePlayer != null && RuntimePlayer.Length > players.Count) ? RuntimePlayer[players.Count] : null;
    player.RuntimeConfig = RuntimeConfig != null ? RuntimeConfig.FromByteArray(RuntimeConfig.ToByteArray(RuntimeConfig)) : null;
    player.MapGuid = FindObjectOfType<MapData>().Asset.Settings.Guid;
    player.EntityViewUpdaterTemplate = EntityViewUpdaterTemplate;
    player.PlayerInputTemplate = PlayerInputTemplate;
    player.GizmoSettings = player.IsFirstPlayer == false ? EditorSettings : null;
    player.OnPlayerCreatedCallback = p => OnPlayerCreated(p);
    player.OnPlayerQuitCallback = p => OnPlayerLeft(p);

    player.Run(PhotonServerSettings.CloneAppSettings(ServerSettings?.AppSettings ?? PhotonServerSettings.Instance.AppSettings));

    players.Add(player);

    var viewGo = Instantiate(PlayerViewTemplate.gameObject, transform.GetChild(0));
    player.BindView(viewGo.GetComponent<QuantumMultiClientPlayerView>());
    viewGo.SetActive(true);

    CreatePlayerBtn.transform.parent.SetAsLastSibling();
    CreatePlayerBtn.GetComponentInChildren<UnityEngine.UI.Text>().text = "Connecting...";
    CreatePlayerBtn.interactable = false;
    return player;
  }

  public void OnPlayerLeft(QuantumMultiClientPlayer player) {
    players.Remove(player);
    CreatePlayerBtn.gameObject.SetActive(players.Count < PlayerCount);
  }

  public void OnPlayerCreated(QuantumMultiClientPlayer player) {
    CreatePlayerBtn.gameObject.SetActive(players.Count < PlayerCount);
    CreatePlayerBtn.GetComponentInChildren<UnityEngine.UI.Text>().text = "New Client";
    CreatePlayerBtn.interactable = true;
  }
}