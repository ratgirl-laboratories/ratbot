using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using RatBot.Domain.Primitives;

namespace RatBot.Infrastructure.Converters;

public class SnowflakeValueConverter<T>() : ValueConverter<T, ulong>(
    snowflake => snowflake.Id,
    id => (T)Activator.CreateInstance(typeof(T), id)!)
    where T : SnowflakeBase;
