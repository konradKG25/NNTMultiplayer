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
            }
        }
        else
        {
            int b = 0;
            handler.myAbe.Abe.gameObject.SetActive(false);
        }
    }

    public override void OnFixedUpdate()
    {
        if (handler.myAbe.Abe != null)
        {
        }
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
    public IPEndPoint remoteEP;

    public void SendMessageToOthers(string message)
    {
        foreach (MultiplayerAbe multiplayerAbe in multiplayerAbes)
        {
            if (multiplayerAbe.IP != myAbe.IP)
            {
            }
        }
    }

    public enum PlayerAction : byte
    {
        SetPosition,
        SetRotation,
        NewPlayer,
        MineExplode,
        MineExplodeB,
        MineExplodeC
    }

    // This enforces the struct will be "packed", i.e. its contents will be laid sequentially in memory:
    [StructLayout(LayoutKind.Sequential)]
    public struct PositionPacket
    {
        public float X { get; set; }
        public float Y { get; set; }
        public float Z { get; set; }
        public string IP { get; set; }
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct RotationPacket
    {
        public float X { get; set; }
        public float Y { get; set; }
        public float Z { get; set; }
        public string IP { get; set; }
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct NewPlayerPacket
    {
        public string IP { get; set; }
        public bool isLocal { get; set; }
        public bool B { get; set; }
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct MineExplode
    {
        public float Wich { get; set; }
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct MineExplodeB
    {
        public float Wich { get; set; }
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct MineExplodeC
    {
        public float Wich { get; set; }
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
            remoteEP = new IPEndPoint(IPAddress.Any, 0);

            try
            {
                while (isListening)
                {
                    if (listener.Available > 0)
                    {
                        byte[] data = listener.Receive(ref remoteEP);

                        PlayerAction action = (PlayerAction)data[0];
                        if (action == PlayerAction.SetPosition)
                        {
                            
                        }
                        else if (action == PlayerAction.SetRotation)
                        {

                        }
                        else if (action == PlayerAction.NewPlayer)
                        {

                        }
                        else if (action == PlayerAction.MineExplode)
                        {

                        }
                        else if (action == PlayerAction.MineExplodeB)
                        {

                        }
                        else if (action == PlayerAction.MineExplodeC)
                        {

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

    void ByteArrayToStructure(byte[] bytearray, ref object obj)
    {
        int len = Marshal.SizeOf(obj);
        IntPtr i = Marshal.AllocHGlobal(len);
        Marshal.Copy(bytearray, 0, i, len);
        obj = Marshal.PtrToStructure(i, obj.GetType());
        Marshal.FreeHGlobal(i);
    }

    public void SetAbePosition()
    {
        byte[] packet = listener.Receive(ref remoteEP);
        PositionPacket pp = new PositionPacket();

        object obj = pp;
        ByteArrayToStructure(packet, ref obj);
        pp = (PositionPacket)obj;

        Vector3 newPos = new Vector3(pp.X, pp.Y, pp.Z);
        FindConnectedByIP(pp.IP).Value.Abe.transform.position = newPos;
    }

    public MultiplayerAbe? FindConnectedByIP(string IP)
    {
        foreach (MultiplayerAbe p in multiplayerAbes)
        {
            if (p.IP == IP) return p;
        }
        return null;
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

    // Optional: Automatically clean up on destroy
    private void OnDestroy()
    {
        StopListening();
    }
    void Start()
    {
        DontDestroyOnLoad(gameObject);
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
    public string sceneName;
    public GameObject Abe;

    public MultiplayerAbe(string IP, bool isLocal, GameObject abe)
    {
        this.IP = IP;
        if (isLocal)
        {
            Core.handler.myAbe = this;
        }
        Abe = abe;
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