using _717_Microservices.Shared;
using FluentResults;
using Microsoft.AspNetCore.Identity;
using Microsoft.IdentityModel.Tokens;
using Microsoft.ServiceFabric.Services.Communication.Runtime;
using Microsoft.ServiceFabric.Services.Remoting.Runtime;
using Microsoft.ServiceFabric.Services.Runtime;
using System.Fabric;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;

namespace Microservices.ManageUsers;

/// <summary>
/// An instance of this class is created for each service instance by the Service Fabric runtime.
/// </summary>
internal sealed class ManageUsers : StatelessService, IManageUserService
{
    private readonly IUserStore<IdentityUser> _userStore;
    private readonly UserManager<IdentityUser> _userManager;

    internal record JwtSettings(string Issuer, string Audience, byte[] Key);
    internal JwtSettings jwtSettings = new("ManageUsersService", "Microservices.Api", Encoding.UTF8.GetBytes("Mysupersecretkeythatishardtoguess"));

    public ManageUsers(StatelessServiceContext context, IUserStore<IdentityUser> userStore, UserManager<IdentityUser> userManager)
        : base(context)
    {
        _userStore = userStore;
        _userManager = userManager;
    }

    public async Task<(string? token, string? err)> GetTokenForUserAsync(string email, string password)
    {
        var idUser = await _userManager.FindByEmailAsync(email);

            if (idUser is null)
            {
                return (null, "unable to get token");
            }

            var passMatched = await _userManager.CheckPasswordAsync(idUser, password);

            if (!passMatched)
            {
                return (null, "unable to get token");
            }

            var claims = new List<Claim>
            {
                new Claim(ClaimTypes.Email, idUser.Email)
            };

            var securityKey = new SymmetricSecurityKey(jwtSettings.Key);

            var signingCred = new SigningCredentials(securityKey, SecurityAlgorithms.HmacSha256);

            SecurityToken securityToken = new JwtSecurityToken(
                claims: claims,
                expires: DateTime.Now.AddDays(1),
                issuer: jwtSettings.Issuer,
                audience: jwtSettings.Audience,
                signingCredentials: signingCred);

            var token = new JwtSecurityTokenHandler().WriteToken(securityToken);

            return (token, null);
    }

    public async Task<Result> AddUserAsync(string email, string password)
    {
        try
        {
            var idUser = new IdentityUser { Email = email };
            await _userStore.SetUserNameAsync(idUser, email, CancellationToken.None);

            var createRes = await _userManager.CreateAsync(idUser, password);

            if (createRes.Succeeded)
            {
                return Result.Ok();
            }

            return Result.Fail(createRes.Errors.FirstOrDefault().Code ?? "Unknown Error While Adding User");
        }
        catch (Exception e)
        {
            return Result.Fail(e.Message);
        }
    }

    public async Task<(string? UserId, string? err)> GetUserIdByEmailAsync(string email)
    {
        try
        {
            var idUser = await _userManager.FindByEmailAsync(email);

            if (idUser is null)
            {
                return (null, $"unable to find user for {email}");
            }

            return (idUser.Id, null);
        }
        catch (Exception e)
        {
            return (null, e.Message);
        }
    }

    /// <summary>
    /// Optional override to create listeners (e.g., TCP, HTTP) for this service replica to handle client or user requests.
    /// </summary>
    /// <returns>A collection of listeners.</returns>
    protected override IEnumerable<ServiceInstanceListener> CreateServiceInstanceListeners()
    {
        return this.CreateServiceRemotingInstanceListeners();
    }

    /// <summary>
    /// This is the main entry point for your service instance.
    /// </summary>
    /// <param name="cancellationToken">Canceled when Service Fabric needs to shut down this service instance.</param>
    protected override async Task RunAsync(CancellationToken cancellationToken)
    {
        // TODO: Replace the following sample code with your own logic 
        //       or remove this RunAsync override if it's not needed in your service.

        long iterations = 0;

        while (true)
        {
            cancellationToken.ThrowIfCancellationRequested();

            ServiceEventSource.Current.ServiceMessage(this.Context, "Working-{0}", ++iterations);

            await Task.Delay(TimeSpan.FromSeconds(1), cancellationToken);
        }
    }
}
