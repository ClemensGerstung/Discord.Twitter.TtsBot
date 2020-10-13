using System;
using Discord.Twitter.TtsBot.AdminAccess;
using Google.Protobuf.WellKnownTypes;
using Microsoft.EntityFrameworkCore;

namespace Discord.Twitter.TtsBot
{
  public class DatabaseContext : DbContext
  {
    public DbSet<TwitterUser> Users { get; set; }
    public DbSet<QueueItem> Items { get; set; }

    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
    {
      base.OnConfiguring(optionsBuilder);

      optionsBuilder.UseSqlite("Data Source=datastorage.db",
        options =>
        {
          //options.MigrationsAssembly(Assembly.GetExecutingAssembly().FullName);
        });
      optionsBuilder.EnableSensitiveDataLogging();
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
      base.OnModelCreating(modelBuilder);

      modelBuilder.Entity<TwitterUser>()
                  .ToTable("Users")
                  .HasKey(user => user.Id);
      modelBuilder.Entity<QueueItem>()
                  .ToTable("Items")
                  .HasKey(qi => qi.TweetId);

      modelBuilder.Entity<QueueItem>()
                  .Property(qi => qi.Created)
                  .HasConversion(ts => ts.ToDateTime().ToString("s"),
                                 v => Timestamp.FromDateTime(DateTime.Parse(v).ToUniversalTime()));
    }

  }
}
