using System;
using System.IO;
using System.Net.Sockets;
using System.Text;

namespace Southridge.Clinic.Hl7
{
    /// <summary>Minimal Lower Layer Protocol framing: &lt;VT&gt;message&lt;FS&gt;&lt;CR&gt;.</summary>
    public static class Mllp
    {
        public const byte StartBlock = 0x0B;
        public const byte EndBlock = 0x1C;
        public const byte CarriageReturn = 0x0D;

        /// <summary>HL7 traffic with the hospital and lab is ISO-8859-1 (agreed in the 2009 interface specification).</summary>
        public static readonly Encoding TextEncoding = Encoding.GetEncoding("ISO-8859-1");

        public static byte[] Frame(string message)
        {
            byte[] body = TextEncoding.GetBytes(message);
            byte[] framed = new byte[body.Length + 3];
            framed[0] = StartBlock;
            Buffer.BlockCopy(body, 0, framed, 1, body.Length);
            framed[framed.Length - 2] = EndBlock;
            framed[framed.Length - 1] = CarriageReturn;
            return framed;
        }

        public static void Write(Stream stream, string message)
        {
            byte[] frame = Frame(message);
            stream.Write(frame, 0, frame.Length);
            stream.Flush();
        }

        /// <summary>Reads one framed message. Returns null when the peer closed the connection.</summary>
        public static string Read(Stream stream)
        {
            var buffer = new MemoryStream();
            bool inMessage = false;
            int previous = -1;
            while (true)
            {
                int b = stream.ReadByte();
                if (b < 0)
                {
                    if (inMessage)
                    {
                        throw new Hl7Exception("Connection closed in the middle of a message.");
                    }

                    return null;
                }

                if (!inMessage)
                {
                    if (b == StartBlock)
                    {
                        inMessage = true;
                    }

                    continue;
                }

                if (b == CarriageReturn && previous == EndBlock)
                {
                    byte[] data = buffer.ToArray();
                    return TextEncoding.GetString(data, 0, data.Length - 1);
                }

                buffer.WriteByte((byte)b);
                previous = b;
            }
        }
    }

    /// <summary>Synchronous MLLP sender: one connection per message, waits for the ACK.</summary>
    public class MllpClient
    {
        private readonly string _host;
        private readonly int _port;
        private readonly int _timeoutMilliseconds;

        public MllpClient(string host, int port, int timeoutMilliseconds)
        {
            _host = host;
            _port = port;
            _timeoutMilliseconds = timeoutMilliseconds;
        }

        public Hl7Message Send(Hl7Message message)
        {
            using (var client = new TcpClient())
            {
                client.SendTimeout = _timeoutMilliseconds;
                client.ReceiveTimeout = _timeoutMilliseconds;
                client.Connect(_host, _port);
                using (NetworkStream stream = client.GetStream())
                {
                    Mllp.Write(stream, message.ToString());
                    string reply = Mllp.Read(stream);
                    if (reply == null)
                    {
                        throw new Hl7Exception("No acknowledgement received from " + _host + ":" + _port + ".");
                    }

                    return Hl7Message.Parse(reply);
                }
            }
        }
    }
}
