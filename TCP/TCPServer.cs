
using ChatServer.Hubs;
using Microsoft.AspNetCore.SignalR;
using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
namespace ChatServer.TCP
{
    public class TCPServer
    {
        private TcpListener _tcpListener;
        private readonly IHubContext<MonitoringHub> _hubContext;
        private CancellationTokenSource _cancellationTokenSource;
        private readonly List<TcpClient> _connectedClients = new List<TcpClient>();

        public TCPServer(IHubContext<MonitoringHub> hubContext)
        {
            _hubContext = hubContext;
            _cancellationTokenSource = new CancellationTokenSource();
        }

        // ✅ Update method để nhận port parameter
        public async Task StartAsync(int port)
        {
            try
            {
                _tcpListener = new TcpListener(IPAddress.Any, port);
                _tcpListener.Start();

                Console.WriteLine($"✅ TCP Server started on port {port}");
                await LogToHubAsync("INFO", $"TCP Server started on port {port}");

                while (!_cancellationTokenSource.Token.IsCancellationRequested)
                {
                    var tcpClient = await _tcpListener.AcceptTcpClientAsync();
                    _ = HandleClientAsync(tcpClient, _cancellationTokenSource.Token);
                }
            }
            catch (Exception ex)
            {
                await LogToHubAsync("ERROR", $"TCP Server error: {ex.Message}");
            }
        }

        private async Task HandleClientAsync(TcpClient tcpClient, CancellationToken cancellationToken)
        {
            var clientId = $"{((IPEndPoint)tcpClient.Client.RemoteEndPoint).Address}:{((IPEndPoint)tcpClient.Client.RemoteEndPoint).Port}";
            _connectedClients.Add(tcpClient);

            await LogToHubAsync("INFO", $"TCP Client connected: {clientId}");

            try
            {
                using (var networkStream = tcpClient.GetStream())
                {
                    var buffer = new byte[4096];
                    var messageBuilder = new StringBuilder();

                    while (!cancellationToken.IsCancellationRequested && tcpClient.Connected)
                    {
                        var bytesRead = await networkStream.ReadAsync(buffer, 0, buffer.Length, cancellationToken);

                        if (bytesRead == 0)
                            break;

                        var message = Encoding.UTF8.GetString(buffer, 0, bytesRead);
                        messageBuilder.Append(message);

                        // Process complete messages
                        var messages = messageBuilder.ToString().Split('\n');
                        for (int i = 0; i < messages.Length - 1; i++)
                        {
                            var completeMessage = messages[i].Trim();
                            if (!string.IsNullOrEmpty(completeMessage))
                            {
                                await ProcessMessageAsync(clientId, completeMessage);
                            }
                        }
                        messageBuilder = new StringBuilder(messages[^1]);
                    }
                }
            }
            catch (Exception ex)
            {
                await LogToHubAsync("ERROR", $"TCP Client {clientId} error: {ex.Message}");
            }
            finally
            {
                _connectedClients.Remove(tcpClient);
                tcpClient.Close();
                await LogToHubAsync("INFO", $"TCP Client disconnected: {clientId}");
            }
        }

        private async Task ProcessMessageAsync(string clientId, string message)
        {
            // Parse và xử lý message từ TCP client
            await LogToHubAsync("INFO", $"TCP [{clientId}]: {message}");

            // Gửi message đến tất cả SignalR clients
            await _hubContext.Clients.All.SendAsync("ReceiveTCPMessage", clientId, message, DateTime.Now);

            // Xử lý commands đặc biệt
            if (message.StartsWith("COMMAND:"))
            {
                var command = message.Substring(8).Trim();
                await HandleCommandAsync(clientId, command);
            }
        }

        private async Task HandleCommandAsync(string clientId, string command)
        {
            switch (command.ToUpper())
            {
                case "GET_STATUS":
                    await SendToTCPClient(clientId, "STATUS:OK");
                    break;
                case "GET_CLIENTS":
                    var clientsCount = _connectedClients.Count;
                    await SendToTCPClient(clientId, $"CLIENTS:{clientsCount}");
                    break;
                default:
                    await SendToTCPClient(clientId, $"UNKNOWN_COMMAND:{command}");
                    break;
            }
        }

        public async Task SendToTCPClient(string clientId, string message)
        {
            // Broadcast đến tất cả TCP clients hoặc client cụ thể
            foreach (var client in _connectedClients)
            {
                try
                {
                    var stream = client.GetStream();
                    var data = Encoding.UTF8.GetBytes(message + "\n");
                    await stream.WriteAsync(data, 0, data.Length);
                    await stream.FlushAsync();
                }
                catch
                {
                    // Client might be disconnected
                }
            }
        }

        public async Task BroadcastToTCPClients(string message)
        {
            await SendToTCPClient("ALL", message);
        }

        public async Task StopAsync()
        {
            _cancellationTokenSource.Cancel();
            foreach (var client in _connectedClients)
            {
                client.Close();
            }
            _tcpListener?.Stop();
            await LogToHubAsync("INFO", "TCP Server stopped");
        }

        private async Task LogToHubAsync(string level, string message)
        {
            try
            {
                await _hubContext.Clients.All.SendAsync("ReceiveLog", new
                {
                    ClientId = "TCP-Server",
                    LogLevel = level,
                    Message = message,
                    Timestamp = DateTime.Now,
                    ServerTime = DateTime.Now
                });
            }
            catch
            {
                // Ignore logging errors
            }
        }

        public int GetConnectedClientsCount() => _connectedClients.Count;
        public bool IsRunning => _tcpListener?.Server != null && _tcpListener.Server.IsBound;
    }

}
