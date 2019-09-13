using NetLib.Packets;
using System;
using System.IO;
using System.Net.Sockets;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Formatters.Binary;

namespace NetLib.Services
{
    public abstract class BaseNetClientService
    {
        protected volatile bool Running;
        public Socket ClientSocket { get; set; }

        public class ReadAttachment
        {
            public byte[] ReceivingHeaderBuffer { get; set; } = new byte[sizeof(ulong)];
            public byte[] ReceivingPayloadBuffer { get; set; }
            public ulong NumberOfBytesReadSoFar { get; set; }
            public ulong ExpectedReceivingPayloadLength { get; set; }
        }

        public class WriteAttachment
        {
            // Needed for Write operations
            public bool IsHeaderSent { get; set; } = false;
            public byte[] WriteHeaderBuffer { get; set; }
            public byte[] WritePayloadBuffer { get; set; }
            public ulong NumberOfBytesWrittenSoFar { get; set; }
            public ulong ExpectedWritePayloadLength { get; set; }
        }

        public ReadAttachment InputAttachment { get; set; } = new ReadAttachment();
        public WriteAttachment OutputAttachment { get; set; } = new WriteAttachment();

        protected BaseNetClientService()
        {
        }

        public virtual void InteractAsync()
        {
            ReadHeader();
        }

        private void ReadHeader()
        {
            // BeginReceive is asynchronous, so this function call returns immediately
            // First we want the header so begin receiving the header (8 bytes long)
            ClientSocket.BeginReceive(InputAttachment.ReceivingHeaderBuffer, 0 /*Offset*/,
                InputAttachment.ReceivingHeaderBuffer.Length /* 8 bytes long to read for the header */,
                SocketFlags.None, OnHeaderReceived,
                InputAttachment);
        }

        private void OnHeaderReceived(IAsyncResult result)
        {
            try
            {
                ReadAttachment attachment = (ReadAttachment) result.AsyncState;
                // Keep track of how many bytes we have read, notice the + sign
                attachment.NumberOfBytesReadSoFar += (ulong) ClientSocket.EndReceive(result);

                if (attachment.NumberOfBytesReadSoFar > 0)
                {
                    if (attachment.NumberOfBytesReadSoFar == (ulong) attachment.ReceivingHeaderBuffer.Length)
                    {
                        // We have read the header, now let's proceed to read the payload

                        // Reset this, because we are gonna use it
                        // to keep track of number of bytes read for the payload, instead of Header
                        attachment.NumberOfBytesReadSoFar = 0;

                        attachment.ExpectedReceivingPayloadLength =
                            BitConverter.ToUInt64(attachment.ReceivingHeaderBuffer, 0);

                        // You have to make sure payloadLength is > 0 and < than what you think would be too big,
                        // just to keep hackers away which I don't for this simple snippet
                        // but keep in mind that serialized C# Objects are big ...

                        // Now a decision, should I create a new byte[] or just reuse the one used for the previous packet?
                        if (attachment.ReceivingPayloadBuffer == null || // If the payload byte[] is null
                            // or its capacity is less than we need
                            (ulong) attachment.ReceivingPayloadBuffer.Length <
                            attachment.ExpectedReceivingPayloadLength)
                        {
                            // Then create a new one
                            attachment.ReceivingPayloadBuffer = new byte[attachment.ExpectedReceivingPayloadLength];
                        }

                        // Begin reading the payload now
                        ClientSocket.BeginReceive(attachment.ReceivingPayloadBuffer, 0,
                            (int) attachment.ExpectedReceivingPayloadLength, SocketFlags.None, OnPayloadReceived,
                            attachment);
                        return;
                    }
                    else
                    {
                        // We are not done reading the header, keep reading it
                        // For example if have just read 3 bytes, now we are expecting 5 more bytes (because we know
                        // the header should be 8 bytes long, so we begin reading and filling the buffer from offset 3.
                        // So: NumberOfBytesReadSoFar would be 3,
                        // second parameter would be NumberOfBytesReadSoFar(3),
                        // forth parameter would be 5 (because of: 8 - 3).
                        // Hope make sense, it is easy.
                        ClientSocket.BeginReceive(attachment.ReceivingHeaderBuffer,
                            (int) attachment.NumberOfBytesReadSoFar,
                            (int) ((ulong) attachment.ReceivingHeaderBuffer.LongLength -
                                   attachment.NumberOfBytesReadSoFar),
                            SocketFlags.None,
                            OnHeaderReceived, attachment);
                        return;
                    }
                }
            }
            catch (Exception exception)
            {
                OnDisconnected(exception);
            }
        }

        protected abstract void OnDisconnected(Exception exception);

        private void OnPayloadReceived(IAsyncResult result)
        {
            ReadAttachment attachment = (ReadAttachment) result.AsyncState;
            // Keep track of how many bytes we have read, notice the + sign
            attachment.NumberOfBytesReadSoFar += (ulong) ClientSocket.EndReceive(result);

            if (attachment.NumberOfBytesReadSoFar > 0)
            {
                // If we are here that means the Header was already Read
                // A common bug is to do if(NumberOfBytesReadSoFar < ReceivingPayloadBuffer.Length)
                // that is wrong when reusing the ReceivingPayloadBuffer (which we are in this case)
                // because what would happen if wew are reusing a payload buffer bigger than
                // the currently expected payload size? well, you guessed it, we would be mixing
                // to packets in the same buffer...
                if (attachment.NumberOfBytesReadSoFar < attachment.ExpectedReceivingPayloadLength)
                {
                    // If we have not filled the buffer that means we have to keep reading
                    // Obviously we don't want to override what we have read so far, so adjust
                    // the offset, and also the length because now we have to take into account
                    // what we have read so far
                    ClientSocket.BeginReceive(attachment.ReceivingPayloadBuffer,
                        (int) attachment.NumberOfBytesReadSoFar, // Offset
                        (int) ((ulong) attachment.ReceivingPayloadBuffer.LongLength -
                               attachment.NumberOfBytesReadSoFar),
                        SocketFlags.None,
                        OnPayloadReceived,
                        attachment);
                }
                else
                {
                    IFormatter formatter = new BinaryFormatter();
                    MemoryStream memoryStream = new MemoryStream(attachment.ReceivingPayloadBuffer);
                    Packet packet = (Packet) formatter.Deserialize(memoryStream);


                    attachment.NumberOfBytesReadSoFar = 0;
                    // Empty the byte[] arrays, this is actually not needed, but it is a good practice
                    Array.Clear(attachment.ReceivingHeaderBuffer, 0, attachment.ReceivingHeaderBuffer.Length);
                    Array.Clear(attachment.ReceivingPayloadBuffer, 0, attachment.ReceivingPayloadBuffer.Length);

                    // Read another packet
                    ReadHeader();

                    OnPacketReceived(packet);
                }
            }
            else
            {
                // This most likely means socket closed
                Console.WriteLine("Read 0 bytes");
            }
        }

        protected abstract void OnException(Exception exception);

        public virtual void ShutdownConnection()
        {
            Running = false;

            ClientSocket.Shutdown(SocketShutdown.Both);
            ClientSocket.Close();
        }

        protected abstract void OnPacketReceived(Packet packet);

        public virtual void SendPacket(Packet packet)
        {
            IFormatter formatter = new BinaryFormatter();
            MemoryStream memoryStream = new MemoryStream();
            formatter.Serialize(memoryStream, packet);
            OutputAttachment.WritePayloadBuffer = memoryStream.GetBuffer();

            OutputAttachment.ExpectedWritePayloadLength = (ulong) OutputAttachment.WritePayloadBuffer.LongLength;

            // The header buffer contains the payload size
            OutputAttachment.WriteHeaderBuffer = BitConverter.GetBytes(OutputAttachment.ExpectedWritePayloadLength);

            // Begin sending the header first
            ClientSocket.BeginSend(OutputAttachment.WriteHeaderBuffer,
                0,
                OutputAttachment.WriteHeaderBuffer.Length, SocketFlags.None, OnSendHeaderCallback, OutputAttachment);
        }


        public void OnSendHeaderCallback(IAsyncResult result)
        {
            WriteAttachment attachment = (WriteAttachment) result.AsyncState;
            attachment.NumberOfBytesWrittenSoFar += (ulong) ClientSocket.EndSend(result);

            if (attachment.NumberOfBytesWrittenSoFar < (ulong) attachment.WriteHeaderBuffer.LongLength)
            {
                // We have not finished sending the header, keep sending it
                ClientSocket.BeginSend(attachment.WriteHeaderBuffer,
                    (int) attachment.NumberOfBytesWrittenSoFar,
                    (int) ((ulong) attachment.WriteHeaderBuffer.LongLength -
                           attachment.NumberOfBytesWrittenSoFar),
                    SocketFlags.None, OnSendHeaderCallback, attachment);
            }
            else
            {
                // The header was sent completely
                attachment.IsHeaderSent = true;
                attachment.NumberOfBytesWrittenSoFar = 0;
                // We have sent the header, begin sending the payload now
                ClientSocket.BeginSend(attachment.WritePayloadBuffer,
                    0,
                    (int) attachment.ExpectedWritePayloadLength,
                    SocketFlags.None, OnSendPayloadCallback, attachment);
            }
        }


        private void OnSendPayloadCallback(IAsyncResult asyncResult)
        {
            WriteAttachment attachment = (WriteAttachment) asyncResult.AsyncState;
            attachment.NumberOfBytesWrittenSoFar += (ulong) ClientSocket.EndSend(asyncResult);

            if (attachment.NumberOfBytesWrittenSoFar < attachment.ExpectedWritePayloadLength)
            {
                // Write buffer has not been sent completely
                // send the remaining
                ClientSocket.BeginSend(attachment.WritePayloadBuffer,
                    (int) attachment.NumberOfBytesWrittenSoFar,
                    (int) (attachment.ExpectedWritePayloadLength - attachment.NumberOfBytesWrittenSoFar),
                    SocketFlags.None, OnSendHeaderCallback, attachment);
            }
            else
            {
                OnPacketSent();
            }
        }

        protected abstract void OnPacketSent();
    }
}