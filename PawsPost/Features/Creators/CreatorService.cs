using Microsoft.EntityFrameworkCore;
using PawsPost.Data;
using PawsPost.Features.PetAds;

namespace PawsPost.Features.Creators;

public class CreatorService(AppDbContext dbContext, IPetAdService petAdService) : ICreatorService
{
    public async Task<CreatorProfileResponse?> GetProfileAsync(
        Guid id, CreatorAdsRequest request, CancellationToken cancellationToken)
    {
        // Only the public fields are read. The email, phone and user name are never loaded.
        var creator = await dbContext.Users
            .AsNoTracking()
            .Where(user => user.Id == id)
            .Select(user => new { user.Id, user.DisplayName })
            .SingleOrDefaultAsync(cancellationToken);

        if (creator is null)
        {
            return null;
        }

        // Only Active ads, for every caller. The service never looks at who is asking.
        var ads = await petAdService.ListActiveByOwnerAsync(creator.Id, request.Page, request.PageSize, cancellationToken);

        return new CreatorProfileResponse(creator.Id, creator.DisplayName, ads);
    }
}
