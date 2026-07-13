using FluentAssertions;
using HExporter.Application.Validation;
using HExporter.Core.Models;
using Microsoft.Extensions.Options;

namespace HExporter.UnitTests;

public class ExportRequestValidatorTests
{
    private static ExportRequest Request(string destinationPath) =>
        new("SELECT 1", ExportRequest.NoBinds, ExportFormat.Csv, destinationPath, new ExportOptions());

    [Fact]
    public void No_boundary_configured_allows_any_path()
    {
        var validator = new ExportRequestValidator(Options.Create(new ExportSecurityOptions()));

        var act = () => validator.Validate(Request("../../otro_directorio/salida.csv"));

        act.Should().NotThrow();
    }

    [Fact]
    public void Rejects_path_traversal_outside_allowed_directory()
    {
        var options = Options.Create(new ExportSecurityOptions { AllowedOutputDirectory = "/data/exports" });
        var validator = new ExportRequestValidator(options);

        var act = () => validator.Validate(Request("../../otro_directorio/salida.csv"));

        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Rejects_absolute_path_outside_allowed_directory()
    {
        var options = Options.Create(new ExportSecurityOptions { AllowedOutputDirectory = "/data/exports" });
        var validator = new ExportRequestValidator(options);

        var act = () => validator.Validate(Request("/otro_directorio/salida.csv"));

        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Allows_path_inside_allowed_directory()
    {
        var options = Options.Create(new ExportSecurityOptions { AllowedOutputDirectory = "/data/exports" });
        var validator = new ExportRequestValidator(options);

        var act = () => validator.Validate(Request("reporte.csv"));

        act.Should().NotThrow();
    }

    [Fact]
    public void Stdout_output_bypasses_boundary_check()
    {
        var options = Options.Create(new ExportSecurityOptions { AllowedOutputDirectory = "/data/exports" });
        var validator = new ExportRequestValidator(options);

        var act = () => validator.Validate(Request("-"));

        act.Should().NotThrow();
    }
}
