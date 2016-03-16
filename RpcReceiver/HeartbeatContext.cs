using System;
using System.Collections.Generic;
using System.Data.Entity;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using RpcReceiver.Entities;
using RpcReceiver.Mapping;

namespace RpcReceiver
{
    public class HeartbeatContext : DbContext
    {
        public HeartbeatContext() : base("heartbeatConnection")
        {
            Configuration.ProxyCreationEnabled = false;
            Configuration.LazyLoadingEnabled = false;
            //Database.SetInitializer(new MigrateDatabaseToLatestVersion<HeartbeatContext, LearningContextMigrationConfiguration>());
        }

        public DbSet<Group> Group { get; set; }
        public DbSet<Application> Application { get; set; }

        protected override void OnModelCreating(DbModelBuilder modelBuilder)
        {
            modelBuilder.Configurations.Add(new GroupMapper());
            modelBuilder.Configurations.Add(new ApplicationMapping());
            base.OnModelCreating(modelBuilder);
        }
    }
}
