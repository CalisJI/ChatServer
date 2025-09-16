using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Logging.Abstractions;
using System.Collections.Concurrent;

namespace ChatServer.Hubs
{
    public class MonitoringHub: Hub
    {
        private static readonly ConcurrentDictionary<string, ClientInfo> _connectedClients = new();
        private static readonly List<LogEntry> _logEntries = new();

        private static readonly ConcurrentDictionary<string, SystemMetrics> _clientMetrics = new();
        // Client gửi log real-time
        public async Task SendLog(string clientId, string logLevel, string message, string timestamp)
        {
            var logEntry = new LogEntry
            {
                ClientId = clientId,
                LogLevel = logLevel,
                Message = message,
                Timestamp = DateTime.Parse(timestamp),
                ServerTime = DateTime.Now
            };

            _logEntries.Add(logEntry);

            // Broadcast log đến tất cả monitoring clients
            await Clients.All.SendAsync("ReceiveLog", logEntry);
        }

        // Client gửi metrics
        public async Task SendMetrics(string clientId, SystemMetrics metrics)
        {
            // Lưu metrics
            _clientMetrics[clientId] = metrics;

            // Broadcast đến tất cả monitoring clients
            await Clients.All.SendAsync("ReceiveMetrics", clientId, metrics);
        }

        // Lấy tất cả metrics từ server
        public Dictionary<string, SystemMetrics> GetAllMetrics()
        {
            return _clientMetrics.ToDictionary(kvp => kvp.Key, kvp => kvp.Value);
        }

        // Request gửi lại tất cả metrics
        public async Task RequestAllMetrics()
        {
            foreach (var kvp in _clientMetrics)
            {
                await Clients.Caller.SendAsync("ReceiveMetrics", kvp.Key, kvp.Value);
            }
        }

        

        // Client register khi kết nối
        public override async Task OnConnectedAsync()
        {
            var clientId = Context.GetHttpContext().Request.Query["clientId"];
            if (!string.IsNullOrEmpty(clientId))
            {
                _connectedClients[Context.ConnectionId] = new ClientInfo
                {
                    ConnectionId = Context.ConnectionId,
                    ClientId = clientId,
                    ConnectedTime = DateTime.Now,
                    LastActivity = DateTime.Now
                };
                await Clients.All.SendAsync("ClientConnected", clientId);
                // Gửi tất cả metrics hiện có cho client mới
                foreach (var kvp in _clientMetrics)
                {
                    await Clients.Caller.SendAsync("ReceiveMetrics", kvp.Key, kvp.Value);
                    
                }

                
            }

            await base.OnConnectedAsync();
        }


        public override async Task OnDisconnectedAsync(Exception? exception)
        {
            //var clientId = Context.GetHttpContext().Request.Query["clientId"];
            if (_connectedClients.TryRemove(Context.ConnectionId, out var clientInfo))
            {
                _clientMetrics.TryRemove(clientInfo.ClientId, out _);
                await Clients.All.SendAsync("ClientDisconnected", clientInfo.ClientId);
            }
            await base.OnDisconnectedAsync(exception);
        }

        // Lấy danh sách clients đang kết nối
        public List<ClientInfo> GetConnectedClients()
        {
            return _connectedClients.Values.ToList();
        }

        // Lấy log history
        public List<LogEntry> GetLogHistory(DateTime? fromDate = null, DateTime? toDate = null)
        {
            var query = _logEntries.AsQueryable();

            if (fromDate.HasValue)
                query = query.Where(x => x.ServerTime >= fromDate.Value);

            if (toDate.HasValue)
                query = query.Where(x => x.ServerTime <= toDate.Value);

            return query.OrderByDescending(x => x.ServerTime).Take(1000).ToList();
        }
        // Request gửi lại tất cả metrics
       
    }


    public class ClientInfo
    {
        public string ConnectionId { get; set; }
        public string ClientId { get; set; }
        public DateTime ConnectedTime { get; set; }
        public DateTime LastActivity { get; set; }
    }

    public class LogEntry
    {
        public string ClientId { get; set; }
        public string LogLevel { get; set; } // INFO, WARNING, ERROR, DEBUG
        public string Message { get; set; }
        public DateTime Timestamp { get; set; }
        public DateTime ServerTime { get; set; }
    }

    public class SystemMetrics
    {
        public double CpuUsage { get; set; } // %
        public double MemoryUsage { get; set; } // MB
        public double DiskUsage { get; set; } // %
        public int ProcessCount { get; set; }
        public NetworkStats Network { get; set; }
        public DateTime Timestamp { get; set; }
    }

    public class NetworkStats
    {
        public double UploadSpeed { get; set; }
        public double DownloadSpeed { get; set; }
        public double TotalSpeed { get; set; }

    }
}
