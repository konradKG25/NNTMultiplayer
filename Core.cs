using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using MelonLoader;
using UnityEngine;

[assembly: MelonInfo(typeof(NNTMultiplayer.Core), "NNTMultiplayer", "1.0.0", "Konrad", null)]

namespace NNTMultiplayer;

public class Core : MelonMod
{
    public static MultiplayerHandler handler;
    public Rect winRect = new Rect(2, 2, 600, 400);
    public static string InviteCodeToConnect;
    public static bool isPaused = false;
    public static Thread inputThread;

    public override void OnInitializeMelon()
    {
        LoggerInstance.Msg("Initialized.");
    }

    public override void OnLateInitializeMelon()
    {
        GameObject gameObject = new GameObject("nntMultiplayerHandler");
        handler = gameObject.AddComponent<MultiplayerHandler>();
        handler.StartListening(6000);
    }

    public override void OnUpdate()
    {
        if (Input.GetKeyDown(KeyCode.Minus) && !isPaused)
        {
            Pauser pauser = new Pauser();
            pauser.PauseAndPrompt();
        }
    }

    private void DrawUI(int windowID)
    {
        GUI.depth = 0;

        GUI.Label(new Rect(10, 30, 100, 20), "Invite Code:");
        InviteCodeToConnect = GUI.TextField(new Rect(10, 60, 170, 20), InviteCodeToConnect);

        if (GUI.Button(new Rect(10, 60, 130, 30), "Connect"))
        {
            
        }
        if (GUI.Button(new Rect(150, 60, 130, 30), "Start Server"))
        {
        }

        GUI.DragWindow(new Rect(0, 0, 10000, 20));
    }

    public override void OnSceneWasLoaded(int buildIndex, string sceneName)
    {
        float a = 0;
        handler.myAbe.sceneName = sceneName;
        foreach (MultiplayerAbe multiplayerAbe in handler.multiplayerAbes)
        {
            if (multiplayerAbe.sceneName == handler.myAbe.sceneName)
            {
                a++;
            }
        }
        if (a == handler.HowManyAbes)
        {
            int b = 0;
            foreach (MultiplayerAbe multiplayerAbe in handler.multiplayerAbes)
            {
                MultiplayerAbe tempAbe = handler.multiplayerAbes[b];
                if (!tempAbe.isLocal)
                {
                    handler.SendMessage(multiplayerAbe.IP, 6000, "sceneName:" + sceneName + "," + handler.myAbe.PlayerNumber);
                    tempAbe.Abe = SpawnAnotherAbe(tempAbe.PlayerNumber);
                    handler.multiplayerAbes[b] = tempAbe;
                }
            }
        }
        else
        {
            int b = 0;
            handler.myAbe.Abe.gameObject.SetActive(false);
            handler.SendMessageToOthers("sceneName:" + sceneName + "," + handler.myAbe.PlayerNumber);
        }
    }

    public override void OnFixedUpdate()
    {
        if (handler.myAbe.Abe != null)
        {
            handler.SendMessageToOthers("AbePosition:" + GetPos());
            handler.SendMessageToOthers("AbeRotation:" + GetRot());
        }
    }
    
    public string GetPos()
    {
        return handler.myAbe.Abe.transform.position.x + "," + handler.myAbe.Abe.transform.position.y + "," + handler.myAbe.Abe.transform.position.z + "," + handler.myAbe.IP;
    }

    public string GetRot()
    {
        return handler.myAbe.Abe.transform.rotation.x + "," + handler.myAbe.Abe.transform.rotation.y + "," + handler.myAbe.Abe.transform.rotation.z + "," + handler.myAbe.IP;
    }

    public static GameObject SpawnAnotherAbe(int playerNumber)
    {
        GameObject abe = GameObject.FindObjectOfType<Abe>().gameObject;
        GameObject abe2 = (GameObject)GameObject.Instantiate(abe);
        Abe abe1 = abe2.GetComponent<Abe>();
        return abe2;
    }
}

public class MultiplayerHandler : MonoBehaviour
{
    private UdpClient listener;
    private Thread listenThread;
    private bool isListening = false;
    public string inviteCode;
    public MultiplayerAbe myAbe;
    public float HowManyAbes;
    public List<MultiplayerAbe> multiplayerAbes = new List<MultiplayerAbe>();

    /// <summary>
    /// Sends a UDP message to the specified IP and port.
    /// </summary>
    public void SendMessage(string ip, int port, string message)
    {
        using UdpClient sender = new UdpClient();
        byte[] data = Encoding.UTF8.GetBytes(message);
        sender.Send(data, data.Length, ip, port);
    }

    public void SendMessageToOthers(string message)
    {
        foreach (MultiplayerAbe multiplayerAbe in multiplayerAbes)
        {
            if (multiplayerAbe.IP != myAbe.IP)
            {
                SendMessage(multiplayerAbe.IP, 6000, message);
            }
        }
    }

    /// <summary>
    /// Starts listening for incoming UDP messages on the given port.
    /// </summary>
    public void StartListening(int port)
    {
        if (isListening)
        {
            Debug.LogWarning("Already listening.");
            return;
        }

        isListening = true;
        listener = new UdpClient(port);

        listenThread = new Thread(() =>
        {
            IPEndPoint remoteEP = new IPEndPoint(IPAddress.Any, 0);

            try
            {
                while (isListening)
                {
                    if (listener.Available > 0)
                    {
                        byte[] data = listener.Receive(ref remoteEP);
                        string message = Encoding.UTF8.GetString(data);
                        MelonLogger.Msg($"Received from {remoteEP}: {message}");
                        if (message.StartsWith("AbePosition:"))
                        {
                            SetAbePos(message);
                        }
                        else if (message.StartsWith("AbeRotation:"))
                        {
                            SetAbeRot(message);
                        }
                        else if (message.StartsWith("NewAbe:"))
                        {
                            SpawnNewAbeConnection(message);
                        }
                        else if (message.StartsWith("sceneName:"))
                        {
                            SetSceneNameForAbe(message);
                        }
                        else if (message.StartsWith("mineExplode:"))
                        {
                            mineExplode(message);
                        }
                        else if (message.StartsWith("mineExplodeB:"))
                        {
                            mineExplodeB(message);
                        }
                        else if (message.StartsWith("mineExplodeC:"))
                        {
                            mineExplodeC(message);
                        }
                    }
                    else
                    {
                        Thread.Sleep(10);
                    }
                }
            }
            catch (SocketException ex)
            {
                MelonLogger.Msg($"UDP Listener socket closed: {ex.Message}");
            }
        })
        {
            IsBackground = true
        };

        listenThread.Start();
    }

    /// <summary>
    /// Stops listening and cleans up the UDP listener.
    /// </summary>
    public void StopListening()
    {
        if (!isListening)
            return;

        isListening = false;

        try
        {
            listener?.Close();
        }
        catch (Exception ex)
        {
            MelonLogger.Msg($"Exception while closing UDP listener: {ex.Message}");
        }

        listener = null;
        listenThread = null;
    }

    public void SetAbePos(string message)
    {
        string coords = message.Split(':')[1]; // "x,y,z"
        string[] parts = coords.Split(',');

        if (parts.Length >= 3 &&
            float.TryParse(parts[0], out float x) &&
            float.TryParse(parts[1], out float y) &&
            float.TryParse(parts[2], out float z))
        {
            Vector3 newPos = new Vector3(x, y, z);
            FindConnectedByIP(parts[3]).Value.Abe.transform.position = newPos;
        }
    }

    public void SetAbeRot(string message)
    {
        string coords = message.Split(':')[1]; // "x,y,z"
        string[] parts = coords.Split(',');

        if (parts.Length >= 3 &&
            float.TryParse(parts[0], out float x) &&
            float.TryParse(parts[1], out float y) &&
            float.TryParse(parts[2], out float z))
        {
            Quaternion newRot = Quaternion.Euler(x, y, z);
            FindConnectedByIP(parts[3]).Value.Abe.transform.rotation = newRot;
        }
    }

    public void SpawnNewAbeConnection(string message)
    {
        string coords = message.Split(':')[1];
        string[] parts = coords.Split(',');

        bool.TryParse(parts[2], out bool b);

        multiplayerAbes.Add(new MultiplayerAbe(parts[0], parts[1], b, multiplayerAbes.Count + 1, new GameObject("abe")));
        if (int.TryParse(parts[3], out int w) && w == 0)
        {
            string bb = message.Replace(",0", ",1");
            SendMessage(parts[0], 6000, bb);
            SendMessageToOthers(bb);
        }
    }

    public string SetupOtherAbeMessage(string ip, string name)
    {
        return "NewAbe:"+ip+","+name+",false,"+(multiplayerAbes.Count + 1)+"1";
    }

    public string SetupMyAbeMessage(string ip, string name)
    {
        return "NewAbe:" + ip + "," + name + ",false," + (multiplayerAbes.Count + 1) + "0";
    }

    public void SetSceneNameForAbe(string message)
    {
        string a = message.Split(':')[1];
        string[] parts = a.Split(',');

        int b = 0;
        int w = 0;

        foreach (MultiplayerAbe multiplayerAbe in multiplayerAbes)
        {
            if (multiplayerAbes[b].PlayerNumber == int.Parse(parts[1]))
            {
                MultiplayerAbe abe = multiplayerAbes[b];
                abe.sceneName = parts[0];
                multiplayerAbes[b] = abe;
                w++;
            }
            if (multiplayerAbes[b].sceneName == parts[0])
            {
                w++;
            }
            b++;
        }

        if (w == HowManyAbes && !myAbe.Abe.activeSelf)
        {
            int c = 0;
            myAbe.Abe.SetActive(true);
            foreach (MultiplayerAbe abe in multiplayerAbes)
            {
                MultiplayerAbe multiplayerAbe = multiplayerAbes[c];
                multiplayerAbe.Abe = Core.SpawnAnotherAbe(abe.PlayerNumber);
            }
        }
    }

    public void mineExplode(string message)
    {
        string a = message.Split(':')[1];
        int.TryParse(a, out var b);
        int bb = 0;
        foreach (FlyingMine flyingMine in GameObject.FindObjectsOfType<FlyingMine>())
        {
            if (bb == b)
            {
                flyingMine.Explode(true, flyingMine.m_damage, Explosion.ExplosionSource.FlyingMine);
            }
            else
            {
                bb++;
            }
        }
    }

    public void mineExplodeB(string message)
    {
        string a = message.Split(':')[1];
        int.TryParse(a, out var b);
        int bb = 0;
        foreach (ProximityMine proximityMine in GameObject.FindObjectsOfType<ProximityMine>())
        {
            if (bb == b)
            {
                proximityMine.Explode(true, proximityMine.m_damage, Explosion.ExplosionSource.ProximityMine);
            }
            else
            {
                bb++;
            }
        }
    }

    public void mineExplodeC(string message)
    {
        string a = message.Split(':')[1];
        int.TryParse(a, out var b);
        int bb = 0;
        foreach (ToggleMine toggleMine in GameObject.FindObjectsOfType<ToggleMine>())
        {
            if (bb == b)
            {
                toggleMine.Explode(true, toggleMine.m_damage, Explosion.ExplosionSource.ToggleMine);
            }
            else
            {
                bb++;
            }
        }
    }

    // Optional: Automatically clean up on destroy
    private void OnDestroy()
    {
        StopListening();
    }
    void Start()
    {
        DontDestroyOnLoad(gameObject);
    }

    public MultiplayerAbe? FindConnectedByIP(string ip)
    {
        foreach (MultiplayerAbe multiplayerAbe in multiplayerAbes)
        {
            if (multiplayerAbe.IP == ip)
            {
                return multiplayerAbe;
            }
        }
        return null;
    }
}

public static class IPEncryption
{
    private static readonly byte[] key = Encoding.UTF8.GetBytes("rK8v9bX4Pd1MqzC7Yh3eLkWpFgTuNsRx");
    private static readonly byte[] iv = Encoding.UTF8.GetBytes("D4s8K1x3V0mQ9rZ2");

    public static string EncryptIP(string ip)
    {
        using Aes aes = Aes.Create();
        aes.Key = key;
        aes.IV = iv;

        ICryptoTransform encryptor = aes.CreateEncryptor();
        byte[] inputBytes = Encoding.UTF8.GetBytes(ip);
        byte[] encrypted = encryptor.TransformFinalBlock(inputBytes, 0, inputBytes.Length);

        return Convert.ToBase64String(encrypted);
    }

    public static string DecryptIP(string encryptedIP)
    {
        using Aes aes = Aes.Create();
        aes.Key = key;
        aes.IV = iv;

        ICryptoTransform decryptor = aes.CreateDecryptor();
        byte[] inputBytes = Convert.FromBase64String(encryptedIP);
        byte[] decrypted = decryptor.TransformFinalBlock(inputBytes, 0, inputBytes.Length);

        return Encoding.UTF8.GetString(decrypted);
    }
}

public struct MultiplayerAbe
{
    public string IP;
    public string Name;
    public int PlayerNumber;
    public bool isLocal;
    public string sceneName;
    public GameObject Abe;

    public MultiplayerAbe(string IP, string Name, bool isLocal, int playerNumber, GameObject abe)
    {
        this.IP = IP;
        this.Name = Name;
        this.isLocal = isLocal;
        if (isLocal)
        {
            Core.handler.myAbe = this;
        }
        Abe = abe;
        PlayerNumber = playerNumber;
    }
}

public class Pauser
{
    public void PauseAndPrompt()
    {
        Core.isPaused = true;
        Time.timeScale = 0f;
        MelonLogger.Msg("==== GAME PAUSED ===");
        MelonLogger.Msg("Enter an invite code and press Enter to connect.");
        MelonLogger.Msg("Or press F and Enter to start a server.");
        MelonLogger.Msg("====================");

        Core.inputThread = new Thread(WaitForInput);
        Core.inputThread.Start();
    }

    public static void WaitForInput()
    {
        while (true)
        {
            string input = Console.ReadLine();

            input = input.Trim();

            if (input.ToUpper() == "F")
            {
                MelonLogger.Msg("[SERVER] Starting server...");
                MelonLogger.Msg(IPEncryption.EncryptIP(IPAddress.Any.ToString()));
                break;
            }
            else
            {
                MelonLogger.Msg($"[CLIENT] Connecting with invite code: {input}"); 
                Core.InviteCodeToConnect = IPEncryption.DecryptIP(Core.InviteCodeToConnect);
                Core.handler.SendMessage(Core.InviteCodeToConnect, 6000, Core.handler.SetupMyAbeMessage(IPAddress.Any.ToString(), "A"));
                break;
            }
        }

        ResumeGame();
    }

    public static void ResumeGame()
    {
        MelonLogger.Msg("=== Resuming game ===");
        Time.timeScale = 1f;
        Core.isPaused = false;
    }
}