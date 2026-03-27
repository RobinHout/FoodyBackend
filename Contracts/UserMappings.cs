using FoodyBackend.Models;

namespace FoodyBackend.Contracts;

public static class UserMappings
{
    public static UserResponse ToResponse(this User user)
    {
        return new UserResponse(user.Id, user.Username);
    }
}
