using System;
using System.Linq;
using System.Text;
using Litware.OrderHub.Integration.Sftp;
using Xunit;

namespace Litware.OrderHub.Tests
{
    /// <summary>Runs against a real SFTP server when ORDERHUB_TEST_SFTP is set (host;port;user;password;directory[;privatekeyfile]).</summary>
    public class SftpTests
    {
        [Fact]
        public void Upload_list_download_and_move_on_a_real_server()
        {
            string config = Environment.GetEnvironmentVariable("ORDERHUB_TEST_SFTP");
            if (string.IsNullOrEmpty(config)) return;

            var p = config.Split(';');
            var connector = new SshNetSftpConnector(new SftpSettings { Host = p[0], Port = int.Parse(p[1]), Username = p[2], Password = p[3], PrivateKeyFile = p.Length > 5 ? p[5] : null });
            string dir = p[4].TrimEnd('/') + "/t" + Guid.NewGuid().ToString("N").Substring(0, 8);

            using (var session = connector.Connect())
            {
                session.Upload(p[4], "probe.txt", new byte[0]);
                session.Move(p[4].TrimEnd('/') + "/probe.txt", dir);
                session.Upload(dir, "ORD_LW1.xml", Encoding.UTF8.GetBytes("<x/>"));
                session.Upload(dir, "ORD_LW1.xml", Encoding.UTF8.GetBytes("<y/>"));

                var files = session.ListFiles(dir, ".xml");
                Assert.Equal(new[] { dir + "/ORD_LW1.xml" }, files);
                Assert.Equal("<y/>", Encoding.UTF8.GetString(session.Download(files.Single())));
                Assert.Empty(session.ListFiles(dir, ".part"));

                session.Move(files.Single(), dir + "/archive");
                Assert.Empty(session.ListFiles(dir, ".xml"));
                Assert.Single(session.ListFiles(dir + "/archive", ".xml"));
            }
        }
    }
}
