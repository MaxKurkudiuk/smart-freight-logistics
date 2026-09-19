using BuildingBlocks.Observability;
using BuildingBlocks.Observability.Extensions;
using FluentAssertions;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Observability.Tests.Unit;

public sealed class ObservabilitySettingsFacts
{
    [Fact]
    public void Defaults_AreEmptyAndDisabled()
    {
        var settings = new ObservabilitySettings();

        settings.ServiceName.Should().BeEmpty();
        settings.OtlpEndpoint.Should().BeNull();
        settings.EnableConsole.Should().BeFalse();
    }

    [Fact]
    public void AddObservability_RegistersServiceName_WhenConfigEmpty()
    {
        var builder = WebApplication.CreateBuilder();
        builder.Configuration.Sources.Clear();

        builder.AddObservability("TestService");

        var settings = ResolveSettings(builder);
        settings.ServiceName.Should().Be("TestService");
        settings.OtlpEndpoint.Should().BeNull();
    }

    [Fact]
    public void AddObservability_PrefersConfigServiceName_OverArgument()
    {
        var builder = WebApplication.CreateBuilder();
        builder.Configuration.Sources.Clear();
        builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Observability:ServiceName"] = "FromConfig"
        });

        builder.AddObservability("IgnoredArgument");

        ResolveSettings(builder).ServiceName.Should().Be("FromConfig");
    }

    private static ObservabilitySettings ResolveSettings(WebApplicationBuilder builder)
    {
        var descriptor = builder.Services.Single(s => s.ServiceType == typeof(ObservabilitySettings));
        return descriptor.ImplementationInstance.Should().BeOfType<ObservabilitySettings>().Subject;
    }
}
