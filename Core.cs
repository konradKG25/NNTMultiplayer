using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using HarmonyLib;
using MelonLoader;
using UnityEngine;
using static NNTMultiplayer.MultiplayerHandler;

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

    public void SceneLoaded(int buildIndex, string sceneName)
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
        if (handler.myAbe.Abe != null)
        {
            if (a == handler.multiplayerAbes.Count)
            {
                int b = 0;
                foreach (MultiplayerAbe multiplayerAbe in handler.multiplayerAbes)
                {
                    MultiplayerAbe tempAbe = handler.multiplayerAbes[b];
                    tempAbe.Abe = SpawnAnotherAbe();
                    b++;
                }
            }
            else
            {
                int b = 0;
                handler.myAbe.Abe.gameObject.SetActive(false);
            }
        }
    }

    public override void OnFixedUpdate()
    {
        if (handler.myAbe.Abe != null)
        {
            SendPosition();
            SendRotation();
        }
        if (handler.myAbe.sceneName != Application.loadedLevelName)
        {
            SceneLoaded(Application.loadedLevel, Application.loadedLevelName);
        }
        if (handler.waitingForAbe)
        {
            if (GameObject.FindObjectOfType<Abe>() != null)
            {
                handler.waitingForAbe = false;
                foreach (MultiplayerAbe multiplayerAbe in handler.multiplayerAbes)
                {
                    GameObject abe = multiplayerAbe.Abe;
                    abe = SpawnAnotherAbe();
                }
            }
        }
    }

    public void SendPosition()
    {
        PositionPacket pp = new PositionPacket();

        pp.X = handler.myAbe.Abe.transform.position.x;
        pp.Y = handler.myAbe.Abe.transform.position.y;
        pp.Z = handler.myAbe.Abe.transform.position.z;
        pp.IP = handler.myAbe.IP;

        byte[] data = handler.StructureToByteArray(pp);

        handler.SendPacketToOthers(data);
    }

    public void SendRotation()
    {
        RotationPacket pp = new RotationPacket();

        pp.X = handler.myAbe.Abe.transform.rotation.x;
        pp.Y = handler.myAbe.Abe.transform.rotation.y;
        pp.Z = handler.myAbe.Abe.transform.rotation.z;
        pp.IP = handler.myAbe.IP;

        byte[] data = handler.StructureToByteArray(pp);

        handler.SendPacketToOthers(data);
    }

    public static GameObject SpawnAnotherAbe()
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
    public bool waitingForAbe = false;
    public MultiplayerAbe lastAbeConnected;

    public enum PlayerAction : byte
    {
        SetPosition,
        SetRotation,
        NewPlayer,
        AbeSceneChange
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
        public bool GetMy {  get; set; }
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct ChangeScenePacket
    {
        public string SCENE { get; set; }
        public string IP { get; set; }
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
                        MelonLogger.Msg("Recived Message!");

                        PlayerAction action = (PlayerAction)data[0];
                        if (action == PlayerAction.SetPosition)
                        {
                            GetSetAbePosition();
                        }
                        else if (action == PlayerAction.SetRotation)
                        {
                            GetSetAbeRotation();
                        }
                        else if (action == PlayerAction.NewPlayer)
                        {
                            GetNewPlayer();
                        }
                        else if (action == PlayerAction.AbeSceneChange)
                        {
                            GetChangeSceneNameForAbe();
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

    public byte[] StructureToByteArray(object obj)
    {
        int len = Marshal.SizeOf(obj);
        byte[] arr = new byte[len];
        IntPtr ptr = Marshal.AllocHGlobal(len);
        Marshal.StructureToPtr(obj, ptr, true);
        Marshal.Copy(ptr, arr, 0, len);
        Marshal.FreeHGlobal(ptr);
        return arr;
    }

    void ByteArrayToStructure(byte[] bytearray, ref object obj)
    {
        int len = Marshal.SizeOf(obj);
        IntPtr i = Marshal.AllocHGlobal(len);
        Marshal.Copy(bytearray, 0, i, len);
        obj = Marshal.PtrToStructure(i, obj.GetType());
        Marshal.FreeHGlobal(i);
    }

    public void GetSetAbePosition()
    {
        byte[] packet = listener.Receive(ref remoteEP);
        PositionPacket pp = new PositionPacket();

        object obj = pp;
        ByteArrayToStructure(packet, ref obj);
        pp = (PositionPacket)obj;

        MainThreadDispatcher.Enqueue(() =>
        {
            SetAbePosition(pp);
        });
    }

    public void SetAbePosition(PositionPacket pp)
    {
        Vector3 newPos = new Vector3(pp.X, pp.Y, pp.Z);
        FindConnectedByIP(pp.IP).Abe.transform.position = newPos;
    }

    public void GetSetAbeRotation()
    {
        byte[] packet = listener.Receive(ref remoteEP);
        RotationPacket pp = new RotationPacket();

        object obj = pp;
        ByteArrayToStructure(packet, ref obj);
        pp = (RotationPacket)obj;

        MainThreadDispatcher.Enqueue(() =>
        {
            SetAbeRotation(pp);
        });
    }

    public void SetAbeRotation(RotationPacket pp)
    {
        Quaternion newRot = Quaternion.Euler(pp.X, pp.Y, pp.Z);
        FindConnectedByIP(pp.IP).Abe.transform.rotation = newRot;
    }

    public void GetNewPlayer()
    {
        byte[] packet = listener.Receive(ref remoteEP);
        NewPlayerPacket pp = new NewPlayerPacket();

        object obj = pp;
        ByteArrayToStructure(packet, ref obj);
        pp = (NewPlayerPacket)obj;

        MainThreadDispatcher.Enqueue(() =>
        {
            NewPlayer(pp);
        });
    }

    public void NewPlayer(NewPlayerPacket pp)
    {
        multiplayerAbes.Add(new MultiplayerAbe(pp.IP, pp.isLocal, new GameObject("abe")));
        if (pp.GetMy)
        {
            foreach (MultiplayerAbe multiplayerAbe in multiplayerAbes)
            {
                if (multiplayerAbe.IP != lastAbeConnected.IP)
                {
                    SendPacket(NewPlayerPacketCreateData(multiplayerAbe.IP, true, false, false), pp.IP, 6000);
                }
            }
        }
        else
        {
            if (!pp.B)
            {
                NewPlayerPacketCreate(pp.IP, true, true, false, false, false, "");
                NewPlayerPacketCreate(pp.IP, true, false, true, false, false, "");
            }
        }
    }

    public void NewPlayerPacketCreate(string ip, bool b, bool isLocal, bool others, bool straight, bool getAll, string ipB)
    {
        NewPlayerPacket pp = new NewPlayerPacket();

        pp.IP = ip;
        pp.B = b;
        pp.isLocal = isLocal;
        pp.GetMy = getAll;

        byte[] data = StructureToByteArray(pp);

        if (others)
        {
            SendPacketToOthers(data);
        }
        else if (b)
        {
            SendPacket(data, ip, 6000);
        }
        else if (straight)
        {
            SendPacket(data, ipB, 6000);
        }
    }

    public byte[] NewPlayerPacketCreateData(string ip, bool b, bool isLocal, bool others)
    {
        NewPlayerPacket pp = new NewPlayerPacket();

        pp.IP = ip;
        pp.B = b;
        pp.isLocal = isLocal;
        pp.GetMy = false;

        return StructureToByteArray(pp);
    }

    public void GetChangeSceneNameForAbe()
    {
        byte[] packet = listener.Receive(ref remoteEP);
        ChangeScenePacket pp = new ChangeScenePacket();

        object obj = pp;
        ByteArrayToStructure(packet, ref obj);
        pp = (ChangeScenePacket)obj;

        MainThreadDispatcher.Enqueue(() =>
        {
            ChangeSceneNameForAbe(pp);
        });
    }

    public void ChangeSceneNameForAbe(ChangeScenePacket pp)
    {
        MultiplayerAbe multiplayerAbe = FindConnectedByIP(pp.IP);
        multiplayerAbe.sceneName = pp.SCENE;

        int a = 0;
        foreach (MultiplayerAbe multiplayerAbe1 in multiplayerAbes)
        {
            if (multiplayerAbe1.sceneName == myAbe.sceneName)
            {
                a++;
            }
        }
        if (a == multiplayerAbes.Count)
        {
            if (myAbe.Abe != null)
            {
                myAbe.Abe.SetActive(true);
                foreach (MultiplayerAbe multiplayerAbe1 in multiplayerAbes)
                {
                    GameObject abe = multiplayerAbe1.Abe;
                    abe = Core.SpawnAnotherAbe();
                }
            }
            else if (myAbe.Abe == null && myAbe.sceneName != "Front_End")
            {
                waitingForAbe = true;
            }
        }
    }

    public void CreateScenePacket(string ip, string scene)
    {
        ChangeScenePacket pp = new ChangeScenePacket();

        pp.IP = ip;
        pp.SCENE = scene;

        byte[] data = StructureToByteArray(pp);

        SendPacketToOthers(data);
    }

    public MultiplayerAbe FindConnectedByIP(string IP)
    {
        foreach (MultiplayerAbe p in multiplayerAbes)
        {
            if (p.IP == IP) return p;
        }
        return new MultiplayerAbe("", false, gameObject);
    }

    public void SendPacket(byte[] data, string ip, int port)
    {
        using (UdpClient sender = new UdpClient())
        {
            sender.Send(data, data.Length, ip, port);
        }
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

    public void SendPacketToOthers(byte[] data)
    {
        foreach (MultiplayerAbe multiplayerAbe in multiplayerAbes)
        {
            if (multiplayerAbe.IP != myAbe.IP)
            {
                SendPacket(data, multiplayerAbe.IP, 6000);
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

    [HarmonyPatch(typeof(FlyingMine), "Explode")]
    public class FlyingMineSender
    {
        public static List<FlyingMine> ExplodedSentMines = new List<FlyingMine>();
        public static List<ProximityMine> ExplodedSentMinesB = new List<ProximityMine>();
        public static List<ToggleMine> ExplodedSentMinesC = new List<ToggleMine>();
        public static void Prefix(FlyingMine __instance)
        {
            if (!ExplodedSentMines.Contains(__instance))
            {
                int a = 0;
                foreach (FlyingMine flyingMine in GameObject.FindObjectsOfType<FlyingMine>())
                {
                    a++;
                    if (flyingMine == __instance)
                    {
                        ExplodedSentMines.Add(flyingMine);
                    }
                }
            }
            
        }
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
        string a = "";
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
                a = input;
                break;
            }
        }

        ResumeGame();

        if (a != "")
        {
            Core.InviteCodeToConnect = IPEncryption.DecryptIP(a);
            Core.handler.SendPacket(Core.handler.NewPlayerPacketCreateData(IPAddress.Any.ToString(), false, false, false), Core.InviteCodeToConnect, 6000);
            Core.handler.NewPlayerPacketCreate(IPAddress.Any.ToString(), false, false, false, false, true, "");
            MelonLogger.Msg("Connected!");
        }
    }

    public static void ResumeGame()
    {
        MelonLogger.Msg("=== Resuming game ===");
        Time.timeScale = 1f;
        Core.isPaused = false;
    }
}