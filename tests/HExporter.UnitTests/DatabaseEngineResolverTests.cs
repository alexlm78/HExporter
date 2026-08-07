using FluentAssertions;
using HExporter.Infrastructure;

namespace HExporter.UnitTests;

public class DatabaseEngineResolverTests
{
    [Fact]
    public void Defaults_to_oracle_when_nothing_set()
        => DatabaseEngineResolver.Resolve(null, null).Should().Be(DatabaseEngine.Oracle);

    [Theory]
    [InlineData("postgres")]
    [InlineData("POSTGRES")]
    [InlineData("postgresql")]
    [InlineData("pg")]
    public void Cli_value_selects_postgres(string cliValue)
        => DatabaseEngineResolver.Resolve(cliValue, null).Should().Be(DatabaseEngine.Postgres);

    [Fact]
    public void Config_value_used_when_cli_not_given()
        => DatabaseEngineResolver.Resolve(null, "postgres").Should().Be(DatabaseEngine.Postgres);

    [Fact]
    public void Cli_value_takes_precedence_over_config()
        => DatabaseEngineResolver.Resolve("oracle", "postgres").Should().Be(DatabaseEngine.Oracle);

    [Fact]
    public void Unknown_value_throws_with_valid_options_listed()
    {
        var act = () => DatabaseEngineResolver.Resolve("mysql", null);

        act.Should().Throw<ArgumentException>()
            .WithMessage("*mysql*oracle*postgres*");
    }
}
