var builder = WebApplication.CreateBuilder(args);

// Add Yarp Reverse Proxy services by reading config from appsettings.json
builder.Services.AddReverseProxy()
    .LoadFromConfig(builder.Configuration.GetSection("ReverseProxy"));

var app = builder.Build();

app.UseRouting();
app.MapReverseProxy();

app.Run();
