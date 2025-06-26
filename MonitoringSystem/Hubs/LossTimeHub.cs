using Microsoft.AspNetCore.SignalR;
using System.Threading.Tasks;

namespace MonitoringSystem.Hubs
{
    public class LossTimeHub : Hub
    {
        public async Task SendMessage(string message)
        {
            await Clients.All.SendAsync("ReceiveMessage", message);
        }
    }
}
