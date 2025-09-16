using Microsoft.AspNetCore.SignalR;
using System.Collections.Concurrent;
namespace ChatServer.Hubs
{
    public class ChatHub : Hub
    {
        private static readonly ConcurrentDictionary<string, string> _users = new();
        public async Task SendMessage(string user, string message)
        {
            await Clients.All.SendAsync("ReceiveMessage", user, message);
        }
        public async Task JoinChat(string user)
        {
            _users[Context.ConnectionId] = user;
            await Clients.Others.SendAsync("UserJoined", user);
            await Clients.All.SendAsync("ReceiveUserList", _users.Values.ToList());
        }
        public async Task LeaveChat(string user)
        {
            _users.TryRemove(Context.ConnectionId, out _);
            await Clients.Others.SendAsync("UserLeft", user);
            await Clients.All.SendAsync("ReceiveUserList", _users.Values.ToList());
        }
        public override async Task OnDisconnectedAsync(Exception? exception)
        {
            if (_users.TryRemove(Context.ConnectionId, out var user))
            {
                await Clients.Others.SendAsync("UserLeft", user);
                await Clients.All.SendAsync("ReceiveUserList", _users.Values.ToList());
            }
            await base.OnDisconnectedAsync(exception);
        }
    }
}
