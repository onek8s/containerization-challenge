using System.Configuration;
using System.Data.SqlClient;

namespace Tailspin.DealerOrders.Data
{
    public class SqlConnectionFactory
    {
        public SqlConnectionFactory(string connectionString)
        {
            ConnectionString = connectionString;
        }

        public string ConnectionString { get; }

        public static SqlConnectionFactory FromConfig(string name = "DealerOrders")
        {
            var setting = ConfigurationManager.ConnectionStrings[name]
                ?? throw new ConfigurationErrorsException($"Connection string '{name}' is missing.");
            return new SqlConnectionFactory(setting.ConnectionString);
        }

        public SqlConnection Open()
        {
            var connection = new SqlConnection(ConnectionString);
            connection.Open();
            return connection;
        }
    }
}
