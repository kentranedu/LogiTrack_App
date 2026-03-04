using Microsoft.EntityFrameworkCore.Design;

public class LogiTrackContextFactory : IDesignTimeDbContextFactory<LogiTrackContext>
{
    public LogiTrackContext CreateDbContext(string[] args)
    {
        return new LogiTrackContext();
    }
}
