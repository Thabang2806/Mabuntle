using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using Swyftly.Application.Identity;

namespace Swyftly.Api.Notifications;

[Authorize(Policy = SwyftlyPolicies.BuyerOnly)]
public sealed class NotificationHub : Hub
{
}
