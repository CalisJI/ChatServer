using ChatServer.Hubs;
using ChatServer.TCP;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.

builder.Services.AddControllers();
// Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
// Thêm SignalR
builder.Services.AddSignalR();

// Thêm CORS
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowAll", policy =>
    {
        policy.AllowAnyHeader()
              .AllowAnyMethod()
              .SetIsOriginAllowed((host) => true) // Cho phép tất cả origins
              .AllowCredentials();
    });
});

//builder.WebHost.ConfigureKestrel(serverOptions =>
//{
//    serverOptions.ListenAnyIP(9000); // Listen trên tất cả IP addresses, port 9000
//});

// ✅ Đăng ký services với đúng dependency
builder.Services.AddSingleton<TCPServer>();
builder.Services.AddSingleton<IHostedService, TCPServerBackgroundService>();

// ✅ Đảm bảo MonitoringHub nhận được TCPServer
builder.Services.AddSingleton(provider =>
{
    var tcpServer = provider.GetRequiredService<TCPServer>();
    return new MonitoringHub(tcpServer);
});


var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}



app.UseCors("AllowAll");
app.UseAuthorization();

app.MapControllers();

// Map SignalR Hub
app.MapHub<ChatHub>("/chatHub");
// Thêm hub mapping
app.MapHub<MonitoringHub>("/monitoringHub");


// ✅ Lấy TCP Server instance và khởi động trên port khác
var tcpServer = app.Services.GetRequiredService<TCPServer>();
var tcpPort = 5555; // Port khác với SignalR

// Khởi động TCP Server trong background
_ = Task.Run(async () =>
{
    try
    {
        await tcpServer.StartAsync(tcpPort);
        Console.WriteLine($"✅ TCP Server running on port {tcpPort}");
    }
    catch (Exception ex)
    {
        Console.WriteLine($"❌ TCP Server failed: {ex.Message}");
    }
});

app.Run("http://*:9000");


// ✅ TCP Server Background Service
public class TCPServerBackgroundService : IHostedService
{
    private readonly TCPServer _tcpServer;
    private readonly IConfiguration _configuration;

    public TCPServerBackgroundService(TCPServer tcpServer, IConfiguration configuration)
    {
        _tcpServer = tcpServer;
        _configuration = configuration;
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        var tcpPort = _configuration.GetValue<int>("TCPServer:Port", 5555);
        await _tcpServer.StartAsync(tcpPort);
    }

    public async Task StopAsync(CancellationToken cancellationToken)
    {
        await _tcpServer.StopAsync();
    }
}