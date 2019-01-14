# UnityMemoryMappedFile
共有メモリでUnityを外部アプリからコントロール

# 概要
UnityでWindowsの共有メモリ  
MemoryMappedFile  
を簡単に使用できるようにしたサンプルです。
UnityとWindowsアプリ間で通信ができます。  
(ライブラリ自体はUnity以外同士の通信にも使用できます)  
  
テスト環境は Unity 2018.1.6f1 で Scripting が .NET4.0 です  

# 更新履歴
2019/01/15  
・初版  

# ビルド方法
UnityMemoryMappedFileWPF\UnityMemoryMappedFileWPF.slnを開いてリビルド  
UnityMemoryMappedFile.dllがUnityMemoryMappedFileSample\Assetsに生成されるのを確認  
UnityでUnityMemoryMappedFileSampleを開く  
Playして、UnityMemoryMappedFileWPF側も実行する  

# 使用方法
  
サーバー(Unity)側：  
``` csharp
using UnityEngine;
using UnityMemoryMappedFile;

public class MemoryMappedFileController : MonoBehaviour
{
    private MemoryMappedFileServer server;

    // Use this for initialization
    void Start()
    {
        server = new MemoryMappedFileServer();
        server.ReceivedEvent += Server_Received;
        server.Start("SamplePipeName");

    }

    private async void Server_Received(object sender, DataReceivedEventArgs e)
    {
        if (e.CommandType == typeof(PipeCommands.SendMessage))
        {
            var d = (PipeCommands.SendMessage)e.Data;
            Debug.Log($"[Server]ReceiveFromClient:{d.Message}");
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
```

クライアント(WPF)側：  
``` csharp
using System.Windows;
using UnityMemoryMappedFile;

namespace UnityMemoryMappedFileWPF
{
    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            InitializeComponent();
        }

        private MemoryMappedFileClient client;

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            client = new MemoryMappedFileClient();
            client.ReceivedEvent += Client_Received;
            client.Start("SamplePipeName");
        }

        private async void SendButton_Click(object sender, RoutedEventArgs e)
        {
            await client.SendCommandAsync(new PipeCommands.SendMessage { Message = "TestFromWPF" });
        }
        
        private void Client_Received(object sender, DataReceivedEventArgs e)
        {
            if (e.CommandType == typeof(PipeCommands.SendMessage))
            {
                var d = (PipeCommands.SendMessage)e.Data;
                MessageBox.Show($"[Client]ReceiveFromServer:{d.Message}");
            }
        }
    }
}
```
Unity側でコマンドを受信した際にGameObjectに触るときはメインスレッドで実行する必要があります。  
メインスレッドでActionを実行できるMainThreadInvokerも使用できます  
``` csharp
    [SerializeField]
    private MainThreadInvoker mainThreadInvoker;
    
    [SerializeField]
    private Transform CubeTransform;

    private async void Server_Received(object sender, DataReceivedEventArgs e)
    {
        if (e.CommandType == typeof(PipeCommands.MoveObject))
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
```
UnityMemoryMappedFile\PipeCommands.cs に追記することで自由にコマンドを増やすことができます。  
アプリ間の通信時にはクラスごとバイナリにシリアライズされ転送されるため、  
好きなデータをやり取りすることができます。  
また、SendCommandWaitAsyncを使用することで、Unity側に値をリクエストして、その返答を受け取ることも可能です。  
``` csharp
    await client.SendCommandWaitAsync(new PipeCommands.GetCurrentPosition(), d =>
    {
        var ret = (PipeCommands.ReturnCurrentPosition)d;
        Dispatcher.Invoke(() => ReceiveTextBlock.Text = $"{ret.CurrentX}");
    });
```

詳しい使用方法はソースをご確認ください。
