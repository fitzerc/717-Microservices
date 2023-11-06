namespace Microservices.ManageUsers.Dto;

public class AddUserDto
{
    public string Email { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
}