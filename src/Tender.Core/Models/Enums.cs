using System.Runtime.Serialization;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Tender.Core.Models;

// =====================================================================
// 自訂 Converter：依 [EnumMember(Value="...")] 序列化/反序列化，
// 支援 "manual-redo" 這類含連字號的值。
// =====================================================================

/// <summary>
/// 依 [EnumMember(Value = "...")] attribute 序列化/反序列化 Enum。
/// 若無 [EnumMember]，fallback 為成員名稱小寫。
/// </summary>
public sealed class EnumMemberJsonConverter<TEnum> : JsonConverter<TEnum>
    where TEnum : struct, Enum
{
    private readonly Dictionary<TEnum, string> _toJson;
    private readonly Dictionary<string, TEnum> _fromJson;

    public EnumMemberJsonConverter()
    {
        _toJson = new Dictionary<TEnum, string>();
        _fromJson = new Dictionary<string, TEnum>(StringComparer.OrdinalIgnoreCase);

        foreach (var field in typeof(TEnum).GetFields())
        {
            if (!field.IsLiteral) continue;

            var enumValue = (TEnum)field.GetValue(null)!;
            var member = field.GetCustomAttributes(typeof(EnumMemberAttribute), false)
                              .OfType<EnumMemberAttribute>()
                              .FirstOrDefault();

            var jsonValue = member?.Value ?? field.Name.ToLowerInvariant();
            _toJson[enumValue] = jsonValue;
            _fromJson[jsonValue] = enumValue;
        }
    }

    public override TEnum Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        var str = reader.GetString()
            ?? throw new JsonException($"Expected string for {typeof(TEnum).Name}.");
        if (_fromJson.TryGetValue(str, out var value))
            return value;
        throw new JsonException($"Unknown {typeof(TEnum).Name} value: '{str}'.");
    }

    public override void Write(Utf8JsonWriter writer, TEnum value, JsonSerializerOptions options)
    {
        if (_toJson.TryGetValue(value, out var str))
            writer.WriteStringValue(str);
        else
            writer.WriteStringValue(value.ToString().ToLowerInvariant());
    }
}

// =====================================================================

[JsonConverter(typeof(EnumMemberJsonConverter<RunStatus>))]
public enum RunStatus
{
    [EnumMember(Value = "success")] Success,
    [EnumMember(Value = "failed")]  Failed,
    [EnumMember(Value = "skipped")] Skipped,
}

[JsonConverter(typeof(EnumMemberJsonConverter<TriggerSource>))]
public enum TriggerSource
{
    /// <summary>每日 17:00 Task Scheduler 預設執行。</summary>
    [EnumMember(Value = "scheduled")]   Scheduled,
    /// <summary>桌面程式啟動偵測或 Task Scheduler missed run 補跑。</summary>
    [EnumMember(Value = "catchup")]     Catchup,
    /// <summary>使用者手動「立即更新」抓當天。</summary>
    [EnumMember(Value = "manual")]      Manual,
    /// <summary>使用者對指定日「重新抓取此日」。</summary>
    [EnumMember(Value = "manual-redo")] ManualRedo,
}

[JsonConverter(typeof(EnumMemberJsonConverter<CrawlerMode>))]
public enum CrawlerMode
{
    [EnumMember(Value = "scheduled")]   Scheduled,
    [EnumMember(Value = "catchup")]     Catchup,
    [EnumMember(Value = "manual")]      Manual,
    [EnumMember(Value = "manual-redo")] ManualRedo,
    [EnumMember(Value = "poc")]         Poc,
}
