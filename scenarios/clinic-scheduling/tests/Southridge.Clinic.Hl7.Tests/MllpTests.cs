using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using Xunit;

namespace Southridge.Clinic.Hl7.Tests
{
    public class MllpTests
    {
        [Fact]
        public void FrameWrapsTheMessage()
        {
            byte[] frame = Mllp.Frame("MSH|^~\\&|A\r");

            Assert.Equal(Mllp.StartBlock, frame[0]);
            Assert.Equal(Mllp.EndBlock, frame[frame.Length - 2]);
            Assert.Equal(Mllp.CarriageReturn, frame[frame.Length - 1]);
        }

        [Fact]
        public void ReadSkipsNoiseBeforeTheStartBlockAndReadsConsecutiveMessages()
        {
            var stream = new MemoryStream();
            stream.WriteByte(0x0A);
            byte[] first = Mllp.Frame("MSH|^~\\&|ONE\r");
            byte[] second = Mllp.Frame("MSH|^~\\&|TWO\rPID|1||Ünal\r");
            stream.Write(first, 0, first.Length);
            stream.Write(second, 0, second.Length);
            stream.Position = 0;

            Assert.Equal("MSH|^~\\&|ONE\r", Mllp.Read(stream));
            Assert.Equal("MSH|^~\\&|TWO\rPID|1||Ünal\r", Mllp.Read(stream));
            Assert.Null(Mllp.Read(stream));
        }

        [Fact]
        public void TruncatedMessageThrows()
        {
            var stream = new MemoryStream(new byte[] { Mllp.StartBlock, (byte)'M', (byte)'S', (byte)'H' });

            Assert.Throws<Hl7Exception>(() => Mllp.Read(stream));
        }

        [Fact]
        public void ClientSendsAndReceivesAcknowledgement()
        {
            var listener = new TcpListener(IPAddress.Loopback, 0);
            listener.Start();
            int port = ((IPEndPoint)listener.LocalEndpoint).Port;
            var server = new Thread(() =>
            {
                using (TcpClient c = listener.AcceptTcpClient())
                {
                    NetworkStream s = c.GetStream();
                    Hl7Message received = Hl7Message.Parse(Mllp.Read(s));
                    Mllp.Write(s, Acknowledgement.Create(received, AckCode.Accept, null, "ACK1").ToString());
                }
            });
            server.Start();

            try
            {
                string message = "MSH|^~\\&|SMGPM|SMG|CRLAB|CASCADE|201303121530||ORM^O01|SMG42|P|2.3\rPID|1||1045230-1^^^SMG^MR\r";
                Hl7Message ack = new MllpClient("127.0.0.1", port, 10000).Send(Hl7Message.Parse(message));

                Assert.Equal("SMG42", ack.Get("MSA-2"));
                Assert.True(Acknowledgement.IsPositive(ack));
            }
            finally
            {
                server.Join(5000);
                listener.Stop();
            }
        }
    }
}
