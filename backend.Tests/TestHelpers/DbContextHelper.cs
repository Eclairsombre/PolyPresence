using backend.Data;
using Microsoft.EntityFrameworkCore;

namespace backend.Tests.TestHelpers;

internal static class DbContextHelper
{
    public static ApplicationDbContext CreateInMemoryDbContext()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        return new ApplicationDbContext(options);
    }
}
