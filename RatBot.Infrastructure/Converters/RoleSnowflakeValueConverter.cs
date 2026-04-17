using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using RatBot.Domain.Primitives;

namespace RatBot.Infrastructure.Converters;

public sealed class RoleSnowflakeValueConverter() : ValueConverter<ulong, RoleSnowflake>(
    roleId => new RoleSnowflake(roleId),
    roleEntity => roleEntity.Id)
{
    private static readonly Func<ulong, RoleSnowflake> ToProviderCompiled =
        new RoleSnowflakeValueConverter().ConvertToProviderExpression.Compile();

    private static readonly Func<RoleSnowflake, ulong> ToModelCompiled =
        new RoleSnowflakeValueConverter().ConvertFromProviderExpression.Compile();

    public static RoleSnowflake ToRoleEntity(ulong roleId) => ToProviderCompiled(roleId);

    public static ulong ToRoleId(RoleSnowflake roleSnowflakeEntity) => ToModelCompiled(roleSnowflakeEntity);
}