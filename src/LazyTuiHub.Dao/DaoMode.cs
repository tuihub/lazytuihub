using System.Text.Json.Serialization;

namespace LazyTuiHub.Dao;

[JsonConverter(typeof(JsonStringEnumConverter<DaoMode>))]
public enum DaoMode
{
    Unified,
    BackendSpecific,
    Native,
}
