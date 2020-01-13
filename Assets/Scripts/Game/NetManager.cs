using JNetworking;
using System;
using System.IO;
using UnityEngine;


[DefaultExecutionOrder(-500)]
public class NetManager : NetBehaviour
{
    public Player PlayerPrefab;
    public Character CharacterPrefab;

    private bool record = false;

    private void Start()
    {
        JNet.Init("Project B");

        // TODO move to somewhere more sensible, such as 'game manager'
        Spawnables.NetRegisterAll();
    }


    private void OnDestroy()
    {
        JNet.Dispose();
    }
    private void StartClient()
    {
        JNet.StartClient();

        JNet.GetClient().UponConnect = () =>
        {
            Debug.Log($"Client connected.");

        };
        JNet.GetClient().UponDisconnect = (reason) =>
        {
            Debug.Log($"Client disconnected. ({reason})");

        };

        if (record)
        {
            StartRecording();
        }

        if (JNet.IsServer)
            JNet.ConnectClientToHost(null);
        else
            JNet.ConnectClientToRemote("127.0.0.1", 7777);
    }

    private void Update()
    {
        JNet.Update();
    }

    private void StartServer()
    {
        JNet.StartServer("My Server Name", 7777, 4);
        JNet.GetServer().UponConnection = (client) =>
        {
            // Create a player object.
            string playerName = $"Player #{Player.AllPlayers.Count}";
            Player player = Instantiate(PlayerPrefab);
            player.gameObject.name = playerName;
            player.Name = playerName;

            // Assign the player object reference to the remote data.
            client.Data = player;

            // Spawn with local client authority.
            JNet.Spawn(player, client.Connection);

            // Create character for player.
            var character = Instantiate(CharacterPrefab);
            character.ControllingPlayer = player;
            character.Name = playerName;

            player.Character = character;

            // Spawn with authority.
            JNet.Spawn(character, client.Connection);
        };
        JNet.GetServer().UponDisconnection = (client, reason) =>
        {
            // Remove the player object from existence.
            Player player = client.GetData<Player>();
            if (player != null)
            {
                // Destroy the player game object.
                Destroy(player.gameObject);

                // Also destroy their character.
                Destroy(player.Character.gameObject);
            }
        };
    }

    private void OnGUI()
    {
        if (JNet.IsClient || JNet.IsServer)
        {
            return;
        }

        record = GUILayout.Toggle(record, "Record client");
        if (GUILayout.Button("Start Client"))
        {
            StartClient();
        }

        if (GUILayout.Button("Start Server"))
        {
            StartServer();
        }

        if (GUILayout.Button("Start Host"))
        {
            StartServer();
            StartClient();
        }
    }

    private static string StartPlayback()
    {
        string path = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Desktop), "PBRecording.txt");

        if (File.Exists(path))
        {
            JNet.Playback.StartPlayback(path);
            return "Started playback!";
        }
        else
        {
            return "Recording file not found!";
        }
    }

    private static string StopPlayback()
    {
        JNet.Playback.StopPlayback();
        return "Stopped playback.";
    }

    private static string StartRecording()
    {
        JNet.Playback.StartRecording(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Desktop), "PBRecording.txt"), true);
        return "Started recording.";
    }

    private static string StopRecording()
    {
        JNet.Playback.StopRecording();
        return "Stopped recording.";
    }
}

