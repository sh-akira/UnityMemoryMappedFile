using System.Collections;
using System.Collections.Generic;
using System.IO.Pipes;
using System.Runtime.Serialization.Json;
using UnityEngine;
using UnityMemoryMappedFile;

public class MemoryMappedFileController : MonoBehaviour
{
    [SerializeField]
    private MainThreadInvoker mainThreadInvoker;


    [SerializeField]
    private Transform CubeTransform;

    private MemoryMappedFileServer server;


    // Use this for initialization
    void Start()
    {
        server = new MemoryMappedFileServer();
        server.ReceivedEvent += Server_Received;
        server.Start("SamplePipeName");

    }

    private void OnApplicationQuit()
    {
        server.ReceivedEvent -= Server_Received;
        server.Stop();
    }

    private async void Server_Received(object sender, DataReceivedEventArgs e)
    {
        if (e.CommandType == typeof(PipeCommands.SendMessage))
        {
            var d = (PipeCommands.SendMessage)e.Data;
            Debug.Log($"[Server]ReceiveFromClient:{d.Message}");
        }
        else if (e.CommandType == typeof(PipeCommands.MoveObject))
        {
            var d = (PipeCommands.MoveObject)e.Data;
            mainThreadInvoker.BeginInvoke(() => //別スレッドからGameObjectに触るときはメインスレッドで処理すること
            {
                var pos = CubeTransform.position;
                pos.x += d.X;
                CubeTransform.position = pos;
            });
        }
        else if (e.CommandType == typeof(PipeCommands.GetCurrentPosition))
        {
            float x = 0.0f;
            await mainThreadInvoker.InvokeAsync(() => x = CubeTransform.position.x); //GameObjectに触るときはメインスレッドで
            await server.SendCommandAsync(new PipeCommands.ReturnCurrentPosition { CurrentX = x }, e.RequestId);
        }
    }

    // Update is called once per frame
    async void Update()
    {
        if (Input.GetKeyDown(KeyCode.T))
        {
            await server.SendCommandAsync(new PipeCommands.SendMessage { Message = "TestFromServer" });
        }
    }
}
