
using JNetworking;
using Lidgren.Network;
using UnityEngine;

public class TestBehaviour : NetBehaviour
{
    [SyncVar]
    public int X;

    [SyncVar]
    public float Y;

    [SyncVar(Hook = "MyHook")]
    public string Z;

    [SyncVar]
    public Vector2 Other;

    [SyncVar]
    public Vector3 Other3;

    [SyncVar]
    public Vector4 Other4;

    [SyncVar(FirstOnly = true)]
    public Color ObjectColour;

    [SyncVar]
    public decimal Dec;

    public double Property { get; set; }

    private void Start()
    {
        GetComponentInChildren<MeshRenderer>().material.color = ObjectColour;
    }

    private void Update()
    {
        if (IsServer)
        {
            if (Input.GetKeyDown(KeyCode.F))
            {
                Test();
            }
        }
    }

    public void MyHook(string newString)
    {
        Z = newString;
        Debug.Log("Hook called!");
    }

    public void Test()
    {
        InvokeRPC("MyRPC", new Vector2(123.4f, 69f));
    }

    [Rpc]
    private void MyRPC(Vector2 thing)
    {
        Debug.Log("From RPC: " + thing);
    }

    [Cmd]
    private void MyCMD(double value)
    {
        Debug.Log("From CMD: " + value);
    }

    public override void Serialize(NetOutgoingMessage msg, bool isForFirst)
    {
        Debug.Log("Serialized, " + isForFirst);
    }

    public override void Deserialize(NetIncomingMessage msg, bool first)
    {
        Debug.Log("Deserialized, " + first);
    }
}