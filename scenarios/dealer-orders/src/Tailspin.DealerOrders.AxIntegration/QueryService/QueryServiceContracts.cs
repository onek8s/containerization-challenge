// Proxy types for the AX 2012 Query Service (http://aos:8101/DynamicsAx/Services/QueryService), trimmed to the
// operations used by Dealer Orders.
using System.Collections.Generic;
using System.Data;
using System.Runtime.Serialization;
using System.ServiceModel;

namespace Tailspin.DealerOrders.AxIntegration.QueryService
{
    internal static class QsNamespaces
    {
        public const string Service = "http://schemas.microsoft.com/dynamics/2011/01/services";
        public const string Paging = "http://schemas.microsoft.com/dynamics/2006/02/documents/QueryMetadata";
        public const string Metadata = "http://schemas.microsoft.com/dynamics/2006/02/documents/QueryMetadata";
        public const string Fault = "http://schemas.microsoft.com/dynamics/2008/01/documents/Fault";
    }

    [DataContract(Name = "Paging", Namespace = QsNamespaces.Paging)]
    [KnownType(typeof(PositionBasedPaging))]
    [KnownType(typeof(ValueBasedPaging))]
    public abstract class Paging
    {
    }

    [DataContract(Name = "PositionBasedPaging", Namespace = QsNamespaces.Paging)]
    public class PositionBasedPaging : Paging
    {
        [DataMember] public int StartingPosition { get; set; }
        [DataMember] public int NumberOfRecordsToFetch { get; set; }
    }

    [DataContract(Name = "ValueBasedPaging", Namespace = QsNamespaces.Paging)]
    public class ValueBasedPaging : Paging
    {
        [DataMember] public int RecordLimit { get; set; }
        [DataMember] public string Bookmark { get; set; }
    }

    public enum FetchMode { OneToOne = 0, OneToN = 1 }
    public enum JoinMode { InnerJoin = 1, OuterJoin = 2, ExistsJoin = 3, NoExistsJoin = 4 }

    [DataContract(Name = "QueryMetadata", Namespace = QsNamespaces.Metadata)]
    public class QueryMetadata
    {
        [DataMember] public string Name { get; set; }
        [DataMember] public List<QueryDataSourceMetadata> DataSources { get; set; } = new List<QueryDataSourceMetadata>();
        [DataMember] public bool AllowCrossCompany { get; set; }
    }

    [DataContract(Name = "QueryDataSourceMetadata", Namespace = QsNamespaces.Metadata)]
    public class QueryDataSourceMetadata
    {
        [DataMember] public string Name { get; set; }
        [DataMember] public string Table { get; set; }
        [DataMember] public bool Enabled { get; set; } = true;
        [DataMember] public bool DynamicFieldList { get; set; } = true;
        [DataMember] public FetchMode FetchMode { get; set; } = FetchMode.OneToOne;
        [DataMember] public JoinMode JoinMode { get; set; } = JoinMode.InnerJoin;
        [DataMember] public List<QueryDataRangeMetadata> Ranges { get; set; } = new List<QueryDataRangeMetadata>();
        [DataMember] public List<QueryDataSourceMetadata> DataSources { get; set; } = new List<QueryDataSourceMetadata>();
        [DataMember] public List<QueryRelationMetadata> Relations { get; set; } = new List<QueryRelationMetadata>();
    }

    [DataContract(Name = "QueryDataRangeMetadata", Namespace = QsNamespaces.Metadata)]
    public class QueryDataRangeMetadata
    {
        [DataMember] public string TableName { get; set; }
        [DataMember] public string FieldName { get; set; }
        [DataMember] public string Value { get; set; }
        [DataMember] public bool Enabled { get; set; } = true;
    }

    [DataContract(Name = "QueryRelationMetadata", Namespace = QsNamespaces.Metadata)]
    public class QueryRelationMetadata
    {
        [DataMember] public string Table { get; set; }
        [DataMember] public string Field { get; set; }
        [DataMember] public string RelatedTable { get; set; }
        [DataMember] public string RelatedField { get; set; }
    }

    [DataContract(Name = "QueryServiceFault", Namespace = QsNamespaces.Fault)]
    public class QueryServiceFault
    {
        [DataMember] public string Message { get; set; }
    }

    [ServiceContract(Namespace = QsNamespaces.Service, ConfigurationName = "IQueryService")]
    public interface IQueryService
    {
        [OperationContract(Action = QsNamespaces.Service + "/QueryService/ExecuteStaticQuery", ReplyAction = QsNamespaces.Service + "/QueryService/ExecuteStaticQueryResponse")]
        [FaultContract(typeof(QueryServiceFault), Name = "QueryServiceFault", Namespace = QsNamespaces.Fault)]
        ExecuteStaticQueryResponse ExecuteStaticQuery(ExecuteStaticQueryRequest request);

        [OperationContract(Action = QsNamespaces.Service + "/QueryService/ExecuteQuery", ReplyAction = QsNamespaces.Service + "/QueryService/ExecuteQueryResponse")]
        [FaultContract(typeof(QueryServiceFault), Name = "QueryServiceFault", Namespace = QsNamespaces.Fault)]
        ExecuteQueryResponse ExecuteQuery(ExecuteQueryRequest request);
    }

    [MessageContract(WrapperName = "ExecuteStaticQuery", WrapperNamespace = QsNamespaces.Service, IsWrapped = true)]
    public class ExecuteStaticQueryRequest
    {
        [MessageBodyMember(Namespace = QsNamespaces.Service, Order = 0)] public string queryName;
        [MessageBodyMember(Namespace = QsNamespaces.Service, Order = 1)] public Paging paging;
    }

    [MessageContract(WrapperName = "ExecuteStaticQueryResponse", WrapperNamespace = QsNamespaces.Service, IsWrapped = true)]
    public class ExecuteStaticQueryResponse
    {
        [MessageBodyMember(Namespace = QsNamespaces.Service, Order = 0)] public DataSet ExecuteStaticQueryResult;
        [MessageBodyMember(Namespace = QsNamespaces.Service, Order = 1)] public Paging paging;
    }

    [MessageContract(WrapperName = "ExecuteQuery", WrapperNamespace = QsNamespaces.Service, IsWrapped = true)]
    public class ExecuteQueryRequest
    {
        [MessageBodyMember(Namespace = QsNamespaces.Service, Order = 0)] public QueryMetadata query;
        [MessageBodyMember(Namespace = QsNamespaces.Service, Order = 1)] public Paging paging;
    }

    [MessageContract(WrapperName = "ExecuteQueryResponse", WrapperNamespace = QsNamespaces.Service, IsWrapped = true)]
    public class ExecuteQueryResponse
    {
        [MessageBodyMember(Namespace = QsNamespaces.Service, Order = 0)] public DataSet ExecuteQueryResult;
        [MessageBodyMember(Namespace = QsNamespaces.Service, Order = 1)] public Paging paging;
    }
}
