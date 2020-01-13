
using Lidgren.Network;
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace JNetworking.Playback
{
    public class JNetPlayback : IDisposable
    {
        public bool IsRecording { get; private set; }
        public bool IsInPlayback { get; private set; }

        private BinaryWriter Writer;
        private Queue<DataChunk> PendingData;

        private float Time;

        internal JNetPlayback()
        {
            PendingData = new Queue<DataChunk>();
        }

        #region Write

        public void StartRecording(string filePath, bool deleteExisting = false)
        {
            if (IsRecording)
                return;
            if (IsInPlayback)
                return;

            if (File.Exists(filePath))
            {
                if (deleteExisting)
                {
                    File.Delete(filePath);
                }
                else
                {
                    JNet.Error($"File {filePath} already exists.");
                    return;
                }                
            }

            try
            {
                Writer = new BinaryWriter(new FileStream(filePath, FileMode.CreateNew, FileAccess.Write, FileShare.None));
            }
            catch(Exception e)
            {
                JNet.Error(e.Message);
                if(Writer != null)
                {
                    Writer.Dispose();
                }
                return;
            }

            Time = 0f;
            IsRecording = true;
        }

        public void LogIncoming(NetIncomingMessage msg)
        {
            if (!IsRecording)
                return;
            if (IsInPlayback)
                return;

            int bits = msg.LengthBits;
            int bytes = msg.LengthBytes;
            byte[] data = msg.Data;

            JNet.Assert(msg.LengthBytes == (int)Math.Ceiling((double)bits / 8), "Unexpected data length.");

            Writer.Write(Time);
            Writer.Write(bits);
            Writer.Write(data, 0, bytes);
        }

        public void StopRecording()
        {
            if (!IsRecording)
                return;

            Writer.Close();
            Writer.Dispose();

            IsRecording = false;
        }

        #endregion

        #region Read

        public void StartPlayback(string filePath)
        {
            if (IsRecording)
                return;
            if (IsInPlayback)
                return;

            if (!File.Exists(filePath))
            {
                JNet.Error($"File {filePath} does not exist.");
                return;
            }

            var bytes = File.ReadAllBytes(filePath);
            int index = 0;
            while (true)
            {
                // Read chunk
                float time = BitConverter.ToSingle(bytes, index);
                index += 4;
                int bits = BitConverter.ToInt32(bytes, index);
                index += 4;

                // Calculate number of bytes from bits.
                int byteCount = (int)Math.Ceiling((double)bits / 8);

                // Copy bytes.
                byte[] data = new byte[byteCount];
                Array.Copy(bytes, index, data, 0, byteCount);
                index += byteCount;


                // Make data chunk.
                var chunk = new DataChunk();
                chunk.BitLength = bits;
                chunk.Time = time;
                chunk.Data = data;

                PendingData.Enqueue(chunk);

                if (index >= bytes.Length)
                    break;
            }

            Debug.Log($"Loaded {PendingData.Count} data chunks.");

            IsInPlayback = true;
            Time = 0f;

            JNet.Log("Started playback.");
        }

        public void Update()
        {
            if (IsInPlayback)
            {
                do
                {
                    if (PendingData.Count == 0)
                    {
                        StopPlayback();
                        break;
                    }

                    var next = PendingData.Peek();
                    if (next.Time <= this.Time)
                    {
                        next = PendingData.Dequeue();

                        // Post data...
                        PostData(next);
                    }
                    else
                    {
                        break;
                    }
                } while (true);
            }

            Time += UnityEngine.Time.unscaledDeltaTime;
        }

        private void PostData(DataChunk c)
        {
            NetIncomingMessage msg = new NetIncomingMessage(NetIncomingMessageType.Data);

            msg.LengthBits = c.BitLength;
            msg.LengthBytes = (int)Math.Ceiling((double)c.BitLength / 8);
            msg.Data = c.Data;

            JNet.GetClient().InjectDataMessage(msg);
        }

        public void StopPlayback()
        {
            if (!IsInPlayback)
                return;

            IsInPlayback = false;

            JNet.Log("Stopped playback.");
        }

        #endregion

        public void Dispose()
        {
            StopPlayback();
            StopRecording();
        }

        private struct DataChunk
        {
            public float Time;
            public int BitLength;
            public byte[] Data;
        }
    }
}
