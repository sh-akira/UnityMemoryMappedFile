using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO.MemoryMappedFiles;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace UnityMemoryMappedFile
{
    public class MemoryMappedFileBase : IDisposable
    {
        private const long capacity = 8192;

        private MemoryMappedFile receiver;
        private MemoryMappedViewAccessor receiverAccessor;

        private MemoryMappedFile sender;
        private MemoryMappedViewAccessor senderAccessor;

        private CancellationTokenSource readCts;

        private string currentPipeName = null;

        public EventHandler<DataReceivedEventArgs> ReceivedEvent;

        public bool IsConnected = false;

        protected async void StartInternal(string pipeName, bool isServer)
        {
            currentPipeName = pipeName;
            readCts = new CancellationTokenSource();
            if (isServer)
            {
                receiver = MemoryMappedFile.CreateOrOpen(pipeName + "_receiver", capacity);
                sender = MemoryMappedFile.CreateOrOpen(pipeName + "_sender", capacity);
            }
            else
            {
                while (true)
                {
                    try
                    {
                        receiver = MemoryMappedFile.OpenExisting(pipeName + "_sender"); //サーバーと逆方向
                        sender = MemoryMappedFile.OpenExisting(pipeName + "_receiver"); //サーバーと逆方向
                        break;
                    }
                    catch (System.IO.FileNotFoundException) { }
                    if (readCts.Token.IsCancellationRequested) return;
                    await Task.Delay(100);
                }
            }
            receiverAccessor = receiver.CreateViewAccessor();
            senderAccessor = sender.CreateViewAccessor();
            var t = Task.Run(() => ReadThread());
            IsConnected = true;
        }

        public async void ReadThread()
        {
            try
            {
                while (true)
                {
                    while (receiverAccessor.ReadByte(0) != 1)
                    {
                        if (readCts.Token.IsCancellationRequested) return;
                         Thread.Sleep(1);// await Task.Delay(1);
                    }

                    long position = 1;
                    //CommandType
                    var length = receiverAccessor.ReadInt32(position);
                    position += sizeof(int);
                    var typeNameArray = new byte[length];
                    receiverAccessor.ReadArray(position, typeNameArray, 0, typeNameArray.Length);
                    position += typeNameArray.Length;
                    //RequestID
                    length = receiverAccessor.ReadInt32(position);
                    position += sizeof(int);
                    var requestIdArray = new byte[length];
                    receiverAccessor.ReadArray(position, requestIdArray, 0, requestIdArray.Length);
                    position += requestIdArray.Length;
                    //Data
                    length = receiverAccessor.ReadInt32(position);
                    position += sizeof(int);
                    var dataArray = new byte[length];
                    receiverAccessor.ReadArray(position, dataArray, 0, dataArray.Length);
                    //Write finish flag
                    receiverAccessor.Write(0, (byte)0);

                    var commandType = PipeCommands.GetCommandType(Encoding.UTF8.GetString(typeNameArray));
                    var requestId = Encoding.UTF8.GetString(requestIdArray);
                    var data = BinarySerializer.Deserialize(dataArray, commandType);
                    if (WaitReceivedDictionary.ContainsKey(requestId))
                    {
                        WaitReceivedDictionary[requestId] = data;
                    }
                    else
                    {
                        ReceivedEvent?.Invoke(this, new DataReceivedEventArgs(commandType, requestId, data));
                    }

                }
            }
            catch (NullReferenceException) { }
        }

        protected ConcurrentDictionary<string, object> WaitReceivedDictionary = new ConcurrentDictionary<string, object>();

        private AsyncLock SendLock = new AsyncLock();

        public async Task<string> SendCommandAsync(object command, string requestId = null, bool needWait = false)
        {
            using (await SendLock.LockAsync())
            {
                return await Task.Run(() => SendCommand(command, requestId, needWait));
            }
        }

        public string SendCommand(object command, string requestId = null, bool needWait = false)
        {
            if (IsConnected == false) return null;
            if (string.IsNullOrEmpty(requestId)) requestId = Guid.NewGuid().ToString();
            var typeNameArray = Encoding.UTF8.GetBytes(command.GetType().Name);
            var requestIdArray = Encoding.UTF8.GetBytes(requestId);
            var dataArray = BinarySerializer.Serialize(command);
            while (senderAccessor.ReadByte(0) == 1) // Wait finish flag
            {
                if (readCts.Token.IsCancellationRequested) return null;
            }
            //Need to wait requestID before send (because sometime return data very fast)
            if (needWait) WaitReceivedDictionary.TryAdd(requestId, null);
            long position = 1;
            //CommandType
            senderAccessor.Write(position, typeNameArray.Length);
            position += sizeof(int);
            senderAccessor.WriteArray(position, typeNameArray, 0, typeNameArray.Length);
            position += typeNameArray.Length;
            //RequestID
            senderAccessor.Write(position, requestIdArray.Length);
            position += sizeof(int);
            senderAccessor.WriteArray(position, requestIdArray, 0, requestIdArray.Length);
            position += requestIdArray.Length;
            //Data
            senderAccessor.Write(position, dataArray.Length);
            position += sizeof(int);
            senderAccessor.WriteArray(position, dataArray, 0, dataArray.Length);
            //Write finish flag
            senderAccessor.Write(0, (byte)1);

            return requestId;
        }

        public async Task SendCommandWaitAsync(object command, Action<object> returnAction)
        {
            var requestId = await SendCommandAsync(command, null, true);
            if (requestId == null) return;
            while (WaitReceivedDictionary[requestId] == null)
            {
                await Task.Delay(10);
            }
            object value; //・・・・
            WaitReceivedDictionary.TryRemove(requestId, out value);
            returnAction(value);
        }

        public void Stop()
        {
            IsConnected = false;
            readCts?.Cancel();
            receiverAccessor?.Dispose();
            senderAccessor?.Dispose();
            receiver?.Dispose();
            sender?.Dispose();
            receiverAccessor = null;
            senderAccessor = null;
            receiver = null;
            sender = null;
        }


        #region IDisposable Support
        private bool disposedValue = false; // 重複する呼び出しを検出するには

        protected virtual void Dispose(bool disposing)
        {
            if (!disposedValue)
            {
                if (disposing)
                {
                    Stop();
                }

                disposedValue = true;
            }
        }

        public void Dispose()
        {
            Dispose(true);
        }
        #endregion
    }
}
